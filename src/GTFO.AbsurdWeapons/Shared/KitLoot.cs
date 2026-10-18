// Generated from GTFO.EZ src/GTFO.EZ/Shared/KitLoot.cs (39a419b) - don't edit here.
// Change GTFO.EZ's copy and run GTFO.EZ/tools/publish-shared.ps1.
using System;
using System.Collections.Generic;
using GameData;
using Gear;
using HarmonyLib;
using LevelGeneration;
using Player;
using UnityEngine;

namespace MindAttic.GTFO.Shared;

// Consumables as kits: the game's own health, tool and ammo resource packs, found sparsely in
// lockers and out in the open on top of the zone's normal resource packs (which are left as they
// are). Each holds MinCharges-MaxCharges charges - one charge is one use, 20% of a pack - and there
// are only as many as add up to LootPercent of the zone's resource budget, split between the three
// kinds the way that budget is. A mod can add Extras (GTFO.AbsurdWeapons: Hand Grenades, Stim
// Packs) to the same lottery: they come on top, at their weights' odds against the kits' own
// weights (1 each by default), and the kits stay at LootPercent. GTFO.EZ owns this; GTFO.AbsurdWeapons runs the same code with its own settings.
internal sealed class KitLootSettings
{
    internal string LogName = "Kits";
    internal float LootPercent = 20f;
    internal int MinCharges = 1;
    internal int MaxCharges = 2;
    internal float HealthWeight = 1f;
    internal float ToolWeight = 1f;
    internal float AmmoWeight = 1f;
    internal List<(uint Id, float Weight)> Extras = new();
}

internal static class KitLoot
{
    internal const int ChargePercent = 20;

    // Kit item id -> its charge range, for rounding each spawned kit to whole charges.
    internal static readonly Dictionary<uint, (int Min, int Max)> Charges = new();

    // Replaces every consumable table's items with the kits (and settings.Extras) and sets its
    // count so the average zone gets settings.LootPercent of its resource budget as kits. A table
    // keeps its relative size: bigger tables, more kits. `perTable` runs on each table after.
    // Returns the number of tables filled.
    internal static int Fill(KitLootSettings settings, Action<ConsumableDistributionDataBlock>? perTable = null)
    {
        var min = Math.Max(1, settings.MinCharges);
        var max = Math.Max(min, settings.MaxCharges);
        var (health, weaponAmmo, toolAmmo) = AverageBudgetPerZone();

        var kits = new List<(uint Id, float Weight)>();
        Add(settings, kits, Find(settings, "health", min, max, "medi", "health"), health * settings.HealthWeight, "health");
        Add(settings, kits, Find(settings, "tool", min, max, "tool"), toolAmmo * settings.ToolWeight, "tool");
        Add(settings, kits, Find(settings, "ammo", min, max, "ammo"), weaponAmmo * settings.AmmoWeight, "ammo");
        if (kits.Count == 0) return 0;

        // The budget only splits the kits between kinds; together they weigh what their settings
        // add up to (3 by default), so Extras' weights read as plain odds against them.
        float budgetWeight = 0f;
        foreach (var (_, weight) in kits) budgetWeight += weight;
        var scale = (Mathf.Max(0f, settings.HealthWeight) + Mathf.Max(0f, settings.ToolWeight) + Mathf.Max(0f, settings.AmmoWeight)) / budgetWeight;
        for (var i = 0; i < kits.Count; i++) kits[i] = (kits[i].Id, kits[i].Weight * scale);

        var averageKit = (min + max) / 2f * ChargePercent / 100f;
        var kitsPerZone = Mathf.Max(0f, settings.LootPercent) / 100f * (health + weaponAmmo + toolAmmo) / averageKit;

        // Extras share the lottery, so each table needs that many more slots for the kits to still
        // come out at kitsPerZone.
        float kitWeight = 0f, extraWeight = 0f;
        foreach (var (_, weight) in kits) kitWeight += weight;
        foreach (var (_, weight) in settings.Extras) extraWeight += Mathf.Max(0f, weight);
        var slotsPerZone = kitsPerZone * (kitWeight + extraWeight) / kitWeight;

        var averageSpawns = AverageSpawnsPerZone();
        int tables = 0, slots = 0;
        foreach (var block in GameDataBlockBase<ConsumableDistributionDataBlock>.GetAllBlocks())
        {
            if (block?.SpawnData == null || block.SpawnData.Count == 0) continue;
            block.SpawnData.Clear();
            foreach (var (id, weight) in kits) block.SpawnData.Add(new ConsumableSpawnData { ItemID = id, Weight = weight });
            foreach (var (id, weight) in settings.Extras)
                if (weight > 0f) block.SpawnData.Add(new ConsumableSpawnData { ItemID = id, Weight = weight });
            block.SpawnsPerZone = averageSpawns > 0f ? Mathf.RoundToInt(slotsPerZone * block.SpawnsPerZone / averageSpawns) : 0;
            perTable?.Invoke(block);
            slots += block.SpawnsPerZone;
            tables++;
        }

        SharedOwner.Log.LogInfo($"{settings.LogName}: {min}-{max} charge kits, {kitsPerZone:0.##} per zone on average ({settings.LootPercent:0.#}% of " +
            $"{health:0.##} health + {weaponAmmo:0.##} ammo + {toolAmmo:0.##} tool packs){(extraWeight > 0f ? $" plus {settings.Extras.Count} extra item(s)" : "")} - " +
            $"{tables} table(s) went from {averageSpawns:0.##} consumables each on average to {(tables > 0 ? (float)slots / tables : 0f):0.##}.");
        return tables;
    }

    // Averaged over the difficulty tiers - a consumable table isn't tied to one tier, so the kit
    // count can only follow the typical zone, not each one.
    private static (float Health, float WeaponAmmo, float ToolAmmo) AverageBudgetPerZone()
    {
        float health = 0f, weaponAmmo = 0f, toolAmmo = 0f;
        var tiers = 0;
        foreach (var block in GameDataBlockBase<ExpeditionBalanceDataBlock>.GetAllBlocks())
        {
            if (block == null) continue;
            health += block.HealthPerZone;
            weaponAmmo += block.WeaponAmmoPerZone;
            toolAmmo += block.ToolAmmoPerZone;
            tiers++;
        }
        return tiers == 0 ? (1f, 1f, 1f) : (health / tiers, weaponAmmo / tiers, toolAmmo / tiers);
    }

    private static float AverageSpawnsPerZone()
    {
        int total = 0, tables = 0;
        foreach (var block in GameDataBlockBase<ConsumableDistributionDataBlock>.GetAllBlocks())
        {
            if (block?.SpawnData == null || block.SpawnData.Count == 0) continue;
            total += block.SpawnsPerZone;
            tables++;
        }
        return tables == 0 ? 0f : (float)total / tables;
    }

    private static void Add(KitLootSettings settings, List<(uint, float)> kits, uint? id, float weight, string what)
    {
        if (id == null) SharedOwner.Log.LogWarning($"{settings.LogName}: no {what} kit item found - left out.");
        else if (weight > 0f) kits.Add((id.Value, weight));
    }

    // The resource pack whose name has one of `keys` in it. Tool packs are skipped when looking for
    // ammo (a tool refill's name can contain "ammo"); disinfection packs never match. A kit spawned
    // as a consumable has no contents of its own, so ConsumableAmmoMin/Max are set to the charges.
    private static uint? Find(KitLootSettings settings, string what, int min, int max, params string[] keys)
    {
        foreach (var block in GameDataBlockBase<ItemDataBlock>.GetAllBlocks())
        {
            if (block == null || block.inventorySlot != InventorySlot.ResourcePack) continue;
            var name = (block.name ?? string.Empty).ToLowerInvariant();
            if (name.Contains("disinfect") || (what == "ammo" && name.Contains("tool"))) continue;

            var match = false;
            foreach (var key in keys) match |= name.Contains(key);
            if (!match) continue;

            SharedOwner.Log.LogInfo($"{settings.LogName}: {what} kit is '{block.name}' (item {block.persistentID}).");
            block.ConsumableAmmoMin = min * ChargePercent;
            block.ConsumableAmmoMax = max * ChargePercent;
            Charges[block.persistentID] = (min, max);
            return block.persistentID;
        }
        return null;
    }
}

// The game rolls a consumable kit's contents anywhere between ConsumableAmmoMin and Max, so a kit
// could hold 1.4 charges. Rounded to whole charges as it spawns. Level generation is seeded the
// same on every client, so each one rounds the same roll the same way, and a second mod's copy of
// this patch finds nothing left to round.
[HarmonyPatch(typeof(LG_PickupItem))]
internal static class KitLoot_SetupAsConsumable_Patch
{
    private static int _logged;

    [HarmonyPatch(nameof(LG_PickupItem.SetupAsConsumable), new[] { typeof(int), typeof(uint) })]
    [HarmonyPostfix]
    private static void AfterSetup(LG_PickupItem __instance) => Snap(__instance);

    [HarmonyPatch(nameof(LG_PickupItem.SetupAsConsumable), new[] { typeof(ConsumableDistributionDataBlock), typeof(int), typeof(uint) })]
    [HarmonyPostfix]
    private static void AfterSetupFromTable(LG_PickupItem __instance) => Snap(__instance);

    private static void Snap(LG_PickupItem pickup)
    {
        try
        {
            if (KitLoot.Charges.Count == 0) return;
            var pack = pickup.GetComponentInChildren<ResourcePackPickup>();
            if (pack?.ItemDataBlock == null || !KitLoot.Charges.TryGetValue(pack.ItemDataBlock.persistentID, out var range)) return;

            var custom = pack.GetCustomData();
            var charges = Mathf.Clamp(Mathf.RoundToInt(custom.ammo / KitLoot.ChargePercent), range.Min, range.Max);
            var ammo = charges * KitLoot.ChargePercent;
            if (custom.ammo == ammo) return;

            if (_logged++ < 3) SharedOwner.Log.LogInfo($"Kits: '{pack.ItemDataBlock.name}' rolled {custom.ammo:0.#}%, rounded to {charges} charge(s).");
            custom.ammo = ammo;
            pack.SetCustomData(custom, false);
        }
        catch (Exception e)
        {
            SharedOwner.Log.LogWarning($"Kits: couldn't round a kit to whole charges. {e.Message}");
        }
    }
}
