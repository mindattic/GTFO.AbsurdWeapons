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
/// set so <see cref="OutInTheOpenShare"/> of them lie around the zone instead, and its spawns per zone
/// are scaled by <see cref="SpawnsPerZoneMultiplier"/>: Hand Grenades are half the mix and there's
/// more loot, so they're common. Artifacts are left alone - as rare as vanilla.
/// After GTFO.EZ's own loot tweaks, and it takes precedence over them: a table GTFO.EZ already
/// turned into kits only (its Consumables Are Kits) gets this mix too, so the kits follow this
/// mod's weights and KitSize. The host's settings decide what spawns.
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
    internal static ConfigEntry<float> SpawnsPerZoneMultiplier = null!;

    internal static void Bind(ConfigFile config)
    {
        const string s = "Loot";
        Enabled = config.Bind(s, "Loot Mix Enabled", true,
            "Consumable loot is only Hand Grenades, Stim Packs and health, tool and ammo kits. Off: vanilla consumable loot. Takes effect on restart.");
        HandGrenadeWeight = config.Bind(s, "HandGrenadeWeight", 4f, "Relative odds a loot consumable is a Hand Grenade. Takes effect on restart.");
        StimPackWeight = config.Bind(s, "StimPackWeight", 1f, "Relative odds it's a Stim Pack. Takes effect on restart.");
        HealthKitWeight = config.Bind(s, "HealthKitWeight", 1f, "Relative odds it's a health kit. Takes effect on restart.");
        ToolKitWeight = config.Bind(s, "ToolKitWeight", 1f, "Relative odds it's a tool kit. Takes effect on restart.");
        AmmoKitWeight = config.Bind(s, "AmmoKitWeight", 1f, "Relative odds it's an ammo kit. Takes effect on restart.");
        KitSize = config.Bind(s, "KitSize", 20, "Contents of a kit found as consumable loot, in percent of a full pack (20 = one use). Kits from the zone's resource budget are unaffected. Takes effect on restart.");
        OutInTheOpenShare = config.Bind(s, "OutInTheOpenShare", 0.6f,
            new ConfigDescription("Share (0-1) of loot consumables left lying out in the open instead of in boxes and lockers. Takes effect on restart.",
                new AcceptableValueRange<float>(0f, 1f)));
        SpawnsPerZoneMultiplier = config.Bind(s, "SpawnsPerZoneMultiplier", 1.5f,
            "Multiplies how many loot consumables spawn per zone (on top of GTFO.EZ's own multiplier, if installed), so Hand Grenades are common. Takes effect on restart.");
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

            static void Add(List<(uint Id, float Weight)> into, uint? id, ConfigEntry<float> weight, string what)
            {
                if (id == null) Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons loot: no {what} item found - left out of the loot.");
                else if (weight.Value > 0f) into.Add((id.Value, weight.Value));
            }

            var mix = new List<(uint Id, float Weight)>();
            Add(mix, GameDataBlockBase<ItemDataBlock>.HasBlock(HandGrenade.ItemId) ? HandGrenade.ItemId : null, HandGrenadeWeight, "Hand Grenade");
            Add(mix, GameDataBlockBase<ItemDataBlock>.HasBlock(StimPack.ItemId) ? StimPack.ItemId : null, StimPackWeight, "Stim Pack");
            Add(mix, Kit("health", "medi", "health"), HealthKitWeight, "health kit");
            Add(mix, Kit("tool", "tool"), ToolKitWeight, "tool kit");
            Add(mix, Kit("ammo", "ammo"), AmmoKitWeight, "ammo kit");
            if (mix.Count == 0) return;

            // Every table gets this mix, including ones GTFO.EZ's Consumables Are Kits already made
            // kits-only: where the two mods' loot settings clash, this mod's win - its kit weights
            // and KitSize replace GTFO.EZ's, with the Hand Grenade and Stim Pack alongside.
            var inBoxes = 1f - Mathf.Clamp01(OutInTheOpenShare.Value);
            int tables = 0, fromGtfoEz = 0;
            foreach (var block in GameDataBlockBase<ConsumableDistributionDataBlock>.GetAllBlocks())
            {
                if (block?.SpawnData == null || block.SpawnData.Count == 0) continue;
                if (IsKitsOnly(block)) fromGtfoEz++;
                block.SpawnData.Clear();
                foreach (var (id, weight) in mix) block.SpawnData.Add(new ConsumableSpawnData { ItemID = id, Weight = weight });
                block.ChanceToSpawnInResourceContainer = inBoxes;
                block.SpawnsPerZone = Mathf.CeilToInt(block.SpawnsPerZone * Mathf.Max(0f, SpawnsPerZoneMultiplier.Value));
                tables++;
            }
            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons loot: {tables} table(s) are {string.Join(", ", mix.ConvertAll(m => $"item {m.Id} x{m.Weight}"))}, kits at {KitSize.Value}%"
                + (fromGtfoEz > 0 ? $" ({fromGtfoEz} were GTFO.EZ kit tables - this mod's loot settings take precedence)" : "") + $"; {1f - inBoxes:P0} out in the open.");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons couldn't set the loot mix: {e}");
        }
    }

    // A table whose every entry is a resource pack - which is what GTFO.EZ's Consumables Are Kits
    // leaves behind (vanilla tables always hold at least one non-pack consumable).
    private static bool IsKitsOnly(ConsumableDistributionDataBlock block)
    {
        foreach (var entry in block.SpawnData)
        {
            var item = GameDataBlockBase<ItemDataBlock>.GetBlock(entry.ItemID);
            if (item == null || item.inventorySlot != InventorySlot.ResourcePack) return false;
        }
        return true;
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
