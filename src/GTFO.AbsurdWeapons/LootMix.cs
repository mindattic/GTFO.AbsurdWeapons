using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using GameData;
using HarmonyLib;
using MindAttic.GTFO.Shared;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// Consumable loot (what lockers and boxes hold, and what lies around a zone) is GTFO.EZ's kit loot -
/// the shared Shared/KitLoot.cs, published from GTFO.EZ: a few small health, tool and ammo kits of
/// <see cref="KitChargesMin"/>-<see cref="KitChargesMax"/> charges, adding <see cref="KitLootPercent"/>
/// to the zone's normal resource packs - with Hand Grenades and Stim Packs added to the same
/// lottery at their weights' odds against the kits. The table's in-a-box chance is set so
/// <see cref="OutInTheOpenShare"/> of them lie around the zone instead. Artifacts are left alone.
/// With GTFO.EZ installed it steps aside on consumable loot while this is on, so this mod's
/// settings decide. The host's settings decide what spawns.
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
    internal static ConfigEntry<float> KitLootPercent = null!;
    internal static ConfigEntry<int> KitChargesMin = null!;
    internal static ConfigEntry<int> KitChargesMax = null!;
    internal static ConfigEntry<float> OutInTheOpenShare = null!;

    internal static void Bind(ConfigFile config)
    {
        const string s = "Loot";
        Enabled = config.Bind(s, "Loot Mix Enabled", true,
            "Consumable loot is a few small health, tool and ammo kits (GTFO.EZ's kit loot) plus Hand Grenades and Stim Packs. Off: vanilla consumable loot. Takes effect on restart.");
        HandGrenadeWeight = config.Bind(s, "HandGrenadeWeight", 4f, "Odds a loot consumable is a Hand Grenade, against the kits' weights (1 each). Takes effect on restart.");
        StimPackWeight = config.Bind(s, "StimPackWeight", 1f, "Relative odds it's a Stim Pack. Takes effect on restart.");
        HealthKitWeight = config.Bind(s, "HealthKitWeight", 1f, "Relative odds it's a health kit. Takes effect on restart.");
        ToolKitWeight = config.Bind(s, "ToolKitWeight", 1f, "Relative odds it's a tool kit. Takes effect on restart.");
        AmmoKitWeight = config.Bind(s, "AmmoKitWeight", 1f, "Relative odds it's an ammo kit. Takes effect on restart.");
        KitLootPercent = config.Bind(s, "KitLootPercent", 20f,
            "How much the kits found as loot add, in percent of a zone's normal health, ammo and tool pack budget. Kits from the zone's resource budget are unaffected. Takes effect on restart.");
        KitChargesMin = config.Bind(s, "KitChargesMin", 1, "Fewest charges a kit found as loot holds (one charge is one use, 20% of a pack). Takes effect on restart.");
        KitChargesMax = config.Bind(s, "KitChargesMax", 2, "Most charges a kit found as loot holds. Takes effect on restart.");
        OutInTheOpenShare = config.Bind(s, "OutInTheOpenShare", 0.6f,
            new ConfigDescription("Share (0-1) of loot consumables left lying out in the open instead of in boxes and lockers. Takes effect on restart.",
                new AcceptableValueRange<float>(0f, 1f)));
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

            var extras = new List<(uint Id, float Weight)>();
            Add(extras, GameDataBlockBase<ItemDataBlock>.HasBlock(HandGrenade.ItemId) ? HandGrenade.ItemId : null, HandGrenadeWeight, "Hand Grenade");
            if (StimPack.Enabled.Value) Add(extras, GameDataBlockBase<ItemDataBlock>.HasBlock(StimPack.ItemId) ? StimPack.ItemId : null, StimPackWeight, "Stim Pack");

            // GTFO.EZ's kit loot, with this mod's settings, and the Hand Grenade and Stim Pack in
            // the same lottery.
            var inBoxes = 1f - Mathf.Clamp01(OutInTheOpenShare.Value);
            var tables = KitLoot.Fill(new KitLootSettings
            {
                LogName = "GTFO.AbsurdWeapons loot",
                LootPercent = KitLootPercent.Value,
                MinCharges = KitChargesMin.Value,
                MaxCharges = KitChargesMax.Value,
                HealthWeight = HealthKitWeight.Value,
                ToolWeight = ToolKitWeight.Value,
                AmmoWeight = AmmoKitWeight.Value,
                Extras = extras,
            }, block => block.ChanceToSpawnInResourceContainer = inBoxes);
            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons loot: {tables} table(s), {1f - inBoxes:P0} out in the open.");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons couldn't set the loot mix: {e}");
        }
    }
}
