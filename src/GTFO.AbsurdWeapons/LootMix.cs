using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using GameData;
using HarmonyLib;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// Every consumable loot table (what boxes and lockers hold, and what lies around a zone) becomes a
/// weighted lottery of just five things: Hand Grenades, Stim Packs, health kits, tool kits and ammo
/// kits. The kits are the game's own resource packs; those normally get their contents from the
/// zone's resource budget (those spawn as normal), so the ones found as consumable loot are given
/// <see cref="KitSize"/> as their consumable contents - 20% of a pack by default. The table's in-a-box chance is
/// set so <see cref="OutInTheOpenShare"/> of them lie around the zone instead, and every artifact
/// spot gets a Hand Grenade instead of an artifact (<see cref="ArtifactsBecomeHandGrenades"/>).
/// After GTFO.EZ's own loot tweaks; the host's settings decide what spawns.
/// </summary>
[HarmonyPatch]
internal static class LootMix
{
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<float> HandGrenadeWeight = null!;
    internal static ConfigEntry<float> StimPackWeight = null!;
    internal static ConfigEntry<float> HealthKitWeight = null!;
    internal static ConfigEntry<float> ToolKitWeight = null!;
    internal static ConfigEntry<float> AmmoKitWeight = null!;
    internal static ConfigEntry<int> KitSize = null!;
    internal static ConfigEntry<float> OutInTheOpenShare = null!;
    internal static ConfigEntry<bool> ArtifactsBecomeHandGrenades = null!;

    internal static void Bind(ConfigFile config)
    {
        const string s = "Loot";
        Enabled = config.Bind(s, "Loot Mix Enabled", true,
            "Consumable loot is only Hand Grenades, Stim Packs and health, tool and ammo kits. Off: vanilla consumable loot. Takes effect on restart.");
        HandGrenadeWeight = config.Bind(s, "HandGrenadeWeight", 2f, "Relative odds a loot consumable is a Hand Grenade. Takes effect on restart.");
        StimPackWeight = config.Bind(s, "StimPackWeight", 1f, "Relative odds it's a Stim Pack. Takes effect on restart.");
        HealthKitWeight = config.Bind(s, "HealthKitWeight", 1f, "Relative odds it's a health kit. Takes effect on restart.");
        ToolKitWeight = config.Bind(s, "ToolKitWeight", 1f, "Relative odds it's a tool kit. Takes effect on restart.");
        AmmoKitWeight = config.Bind(s, "AmmoKitWeight", 1f, "Relative odds it's an ammo kit. Takes effect on restart.");
        KitSize = config.Bind(s, "KitSize", 20, "Contents of a kit found as consumable loot, in percent of a full pack (20 = one use). Kits from the zone's resource budget are unaffected. Takes effect on restart.");
        OutInTheOpenShare = config.Bind(s, "OutInTheOpenShare", 0.6f,
            new ConfigDescription("Share (0-1) of loot consumables left lying out in the open instead of in boxes and lockers. Takes effect on restart.",
                new AcceptableValueRange<float>(0f, 1f)));
        ArtifactsBecomeHandGrenades = config.Bind(s, "ArtifactsBecomeHandGrenades", true,
            "Every artifact the level would place - out in the open or in a box - is a Hand Grenade instead. Every player needs the same setting (the level is built on each game).");
    }

    // Level generation sets each pickup spot up as one thing or another; an artifact spot is set up
    // as a Hand Grenade instead, through the game's own consumable path. Generation runs on every
    // game from the same seed, so everyone gets the same grenades.
    [HarmonyPatch(typeof(LevelGeneration.LG_PickupItem), nameof(LevelGeneration.LG_PickupItem.SetupAsArtifact))]
    [HarmonyPrefix]
    private static bool InsteadOfArtifact(LevelGeneration.LG_PickupItem __instance, int randomSeed)
    {
        try
        {
            if (!ArtifactsBecomeHandGrenades.Value || !GameDataBlockBase<ItemDataBlock>.HasBlock(HandGrenade.ItemId)) return true;
            __instance.SetupAsConsumable(randomSeed, HandGrenade.ItemId);
            return false;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons couldn't turn an artifact into a Hand Grenade: {e.Message}");
            return true;
        }
    }

    [HarmonyPatch(typeof(GameDataInit), nameof(GameDataInit.Initialize))]
    [HarmonyPostfix]
    [HarmonyAfter(Plugin.GtfoEzGuid)]
    [HarmonyPriority(Priority.Low)] // after the Hand Grenade and Stim Pack have registered their items
    private static void Apply()
    {
        try
        {
            if (!Enabled.Value) return;

            var mix = new List<(uint Id, float Weight)>();
            void Add(uint? id, ConfigEntry<float> weight, string what)
            {
                if (id == null) Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons loot: no {what} item found - left out of the loot.");
                else if (weight.Value > 0f) mix.Add((id.Value, weight.Value));
            }
            Add(GameDataBlockBase<ItemDataBlock>.HasBlock(HandGrenade.ItemId) ? HandGrenade.ItemId : null, HandGrenadeWeight, "Hand Grenade");
            Add(GameDataBlockBase<ItemDataBlock>.HasBlock(StimPack.ItemId) ? StimPack.ItemId : null, StimPackWeight, "Stim Pack");
            Add(Kit("health", "medi", "health"), HealthKitWeight, "health kit");
            Add(Kit("tool", "tool"), ToolKitWeight, "tool kit");
            Add(Kit("ammo", "ammo"), AmmoKitWeight, "ammo kit");
            if (mix.Count == 0) return;

            var inBoxes = 1f - Mathf.Clamp01(OutInTheOpenShare.Value);
            var tables = 0;
            foreach (var block in GameDataBlockBase<ConsumableDistributionDataBlock>.GetAllBlocks())
            {
                if (block?.SpawnData == null || block.SpawnData.Count == 0) continue;
                block.SpawnData.Clear();
                foreach (var (id, weight) in mix) block.SpawnData.Add(new ConsumableSpawnData { ItemID = id, Weight = weight });
                block.ChanceToSpawnInResourceContainer = inBoxes;
                tables++;
            }
            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons: consumable loot is {string.Join(", ", mix.ConvertAll(m => $"item {m.Id} x{m.Weight}"))} ({tables} loot tables, {1f - inBoxes:P0} out in the open).");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons couldn't set the loot mix: {e}");
        }
    }

    // The resource pack whose name has one of `keys` in it (tool kits checked before ammo, so the
    // tool pack's "ammo" doesn't count as an ammo kit; disinfection packs never match). Given
    // KitSize as its consumable contents if it has none of its own.
    private static uint? Kit(string what, params string[] keys)
    {
        foreach (var block in GameDataBlockBase<ItemDataBlock>.GetAllBlocks())
        {
            if (block == null || block.inventorySlot != InventorySlot.ResourcePack) continue;
            var name = (block.name ?? "").ToLowerInvariant();
            if (name.Contains("disinfect")) continue;
            if (what == "ammo" && name.Contains("tool")) continue;
            var match = false;
            foreach (var key in keys) match |= name.Contains(key);
            if (!match) continue;

            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons loot: {what} kit is '{block.name}' (item {block.persistentID}, contents {block.ConsumableAmmoMin}-{block.ConsumableAmmoMax}).");
            if (block.ConsumableAmmoMax > 0)
                Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons loot: '{block.name}' already had consumable contents - if zone resource packs read them too, they now start at {KitSize.Value}% as well.");
            block.ConsumableAmmoMin = KitSize.Value;
            block.ConsumableAmmoMax = KitSize.Value;
            return block.persistentID;
        }
        return null;
    }
}
