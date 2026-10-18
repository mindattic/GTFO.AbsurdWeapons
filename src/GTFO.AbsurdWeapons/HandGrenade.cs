using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using GameData;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using LevelGeneration;
using Localization;
using Player;
using SNetwork;
using UnityEngine;
using static GTFO.AbsurdWeapons.DataBlocks;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Hand Grenade: a consumable, thrown like a glowstick - hold to wind up, release to throw -
/// but it's a lock melter rigged to blow. It's the glowstick's own item cloned under a new id, so
/// the game's throw, physics bounce and sync all stay vanilla; this mod swaps the look for the lock
/// melter's (in your hand and in flight), switches the glow off, and blows it up when its fuse
/// runs out. Everyone is given a stack of 99 in their consumable slot when they drop in, and it's
/// infinite: the stack never goes down while it's in that slot.
/// </summary>
[HarmonyPatch]
internal static class HandGrenade
{
    // An ItemDataBlock id (not a gear category). Vanilla items are in the low hundreds.
    internal const uint ItemId = 1203;

    internal static ConfigEntry<float> FuseSeconds = null!;
    internal static ConfigEntry<float> BlastRadius = null!;
    internal static ConfigEntry<float> BlastDamage = null!;
    internal static ConfigEntry<float> BlastEdgeFraction = null!;
    internal static ConfigEntry<float> BlastStagger = null!;
    internal static ConfigEntry<bool> GiveOnDrop = null!;
    internal static ConfigEntry<int> StackSize = null!;
    internal static ConfigEntry<bool> OnlyLootConsumable = null!;
    internal static ConfigEntry<float> OutInTheOpenShare = null!;
    internal static ConfigEntry<float> ThrowForceMultiplier = null!;

    private static bool added;

    internal static void Bind(ConfigFile config)
    {
        FuseSeconds = config.Bind("Hand Grenade", "FuseSeconds", 3f, "Seconds from the throw until it explodes.");
        BlastRadius = config.Bind("Hand Grenade", "BlastRadius", 6f, "Metres the blast reaches.");
        BlastDamage = config.Bind("Hand Grenade", "BlastDamage", 60f, "Damage at the blast's center.");
        BlastEdgeFraction = config.Bind("Hand Grenade", "BlastEdgeFraction", 0.25f, "Share of damage left at the blast's edge.");
        BlastStagger = config.Bind("Hand Grenade", "BlastStagger", 6f, "Blast stagger multiplier.");
        GiveOnDrop = config.Bind("Hand Grenade", "GiveOnDrop", true,
            "Every player is given a stack of Hand Grenades in their consumable slot when they drop into a level (the host's setting decides).");
        StackSize = config.Bind("Hand Grenade", "StackSize", 99, "How many Hand Grenades the stack holds (it's kept full: they never run out).");
        OnlyLootConsumable = config.Bind("Hand Grenade", "OnlyLootConsumable", true,
            "Every consumable found in boxes and lockers is a Hand Grenade instead (takes effect on restart; the host's setting decides what spawns).");
        OutInTheOpenShare = config.Bind("Hand Grenade", "OutInTheOpenShare", 0.6f,
            "With OnlyLootConsumable on: the share (0-1) of Hand Grenades left lying around the level, out in the open like artifacts, instead of in boxes and lockers.");
        ThrowForceMultiplier = config.Bind("Hand Grenade", "ThrowForceMultiplier", 2f, "Multiplies the glowstick's own wind-up throw strength, so it lobs much farther.");
    }

    internal static bool IsOurs(ItemDataBlock? block) => block != null && block.persistentID == ItemId;

    // The glowstick's item, cloned as ours. After GTFO.EZ, same as the guns.
    [HarmonyPatch(typeof(GameDataInit), nameof(GameDataInit.Initialize))]
    [HarmonyPostfix]
    [HarmonyAfter(Plugin.GtfoEzGuid)]
    private static void AddItem()
    {
        try
        {
            if (GameDataBlockBase<ItemDataBlock>.HasBlock(ItemId))
            {
                if (!added) Plugin.Logger.LogError($"GTFO.AbsurdWeapons: item id {ItemId} is already taken by another mod's datablock - not adding the Hand Grenade.");
                return;
            }
            var source = FindGlowstick();
            if (source == null)
            {
                Plugin.Logger.LogError("GTFO.AbsurdWeapons: couldn't find the glowstick's item datablock - not adding the Hand Grenade.");
                return;
            }

            var item = Clone(source);
            item.persistentID = ItemId;
            item.name = "HandGrenade";
            item.publicName = DisplayName("Hand Grenade");
            item.LocalizedName = new LocalizedText { Id = 0, UntranslatedText = DisplayName("Hand Grenade") };
            item.ConsumableAmmoMin = StackSize.Value;
            item.ConsumableAmmoMax = StackSize.Value;
            GameDataBlockBase<ItemDataBlock>.AddBlock(item, -1);
            added = true;
            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons added the Hand Grenade (consumable; item id {ItemId}, cloned from '{source.name}' {source.persistentID}; sync id {item.syncID} vs source {source.syncID}).");
            if (OnlyLootConsumable.Value) MakeOnlyLoot();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons failed to add the Hand Grenade: {e}");
        }
    }

    // Every consumable loot table's entries point at the Hand Grenade, weights kept, so whatever a
    // box or locker would have held is one. (Med, ammo and tool packs aren't in these tables - the
    // zone's resource budget fills those - so they still spawn.) The table's own in-a-box chance
    // decides the rest: OutInTheOpenShare of them are left lying around the zone instead, scattered
    // like artifacts. After GTFO.EZ's own loot tweaks.
    private static void MakeOnlyLoot()
    {
        var tables = 0;
        var inBoxes = 1f - Mathf.Clamp01(OutInTheOpenShare.Value);
        foreach (var block in GameDataBlockBase<ConsumableDistributionDataBlock>.GetAllBlocks())
        {
            if (block?.SpawnData == null || block.SpawnData.Count == 0) continue;
            foreach (var entry in block.SpawnData) entry.ItemID = ItemId;
            block.ChanceToSpawnInResourceContainer = inBoxes;
            tables++;
        }
        Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons: Hand Grenades are the only loot consumable ({tables} loot tables, {1f - inBoxes:P0} out in the open).");
    }

    // The plain glowstick: a consumable whose thrown prefab is a glowstick, preferring the one
    // without a seasonal color in its name.
    private static ItemDataBlock? FindGlowstick()
    {
        ItemDataBlock? best = null;
        var bestScore = -1;
        foreach (var block in GameDataBlockBase<ItemDataBlock>.GetAllBlocks())
        {
            if (block == null || block.persistentID == ItemId || block.inventorySlot != InventorySlot.Consumable) continue;
            var glow = Contains(block.name, "glowstick");
            var prefabs = block.InstancePrefabs;
            if (prefabs != null)
                for (var i = 0; i < prefabs.Count; i++)
                    if (Contains(prefabs[i], "glowstick")) glow = true;
            if (!glow) continue;

            var score = Contains(block.name, "christmas") || Contains(block.name, "halloween") || Contains(block.name, "yellow") ? 0 : 1;
            if (score > bestScore) { best = block; bestScore = score; }
        }
        return best;
    }

    private static bool Contains(string? text, string part) => text != null && text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

    // In your hand: the glowstick's model hidden, the lock melter's in its place.
    [HarmonyPatch(typeof(ThrowingWeapon), nameof(ThrowingWeapon.OnWield))]
    [HarmonyPostfix]
    private static void AfterWield(ThrowingWeapon __instance)
    {
        try
        {
            if (!IsOurs(__instance.ItemDataBlock)) return;
            var held = __instance.m_movingThrowObj;
            if (held != null) Reskin(held.gameObject);
            // Thrown with a grenade's sound, not a glowstick's.
            if (!string.IsNullOrWhiteSpace(Sounds.HandGrenadeThrow.Value)) __instance.m_throwSoundEvent = Sounds.HandGrenadeThrow.Value.Trim();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons couldn't reskin the held Hand Grenade: {e.Message}");
        }
    }

    // Thrown twice as hard as a glowstick's own wind-up would give it (ThrowForceMultiplier):
    // ThrowingWeapon.Throw takes the wind-up charge (0-1) and turns it into thrust itself
    // (CalcThrust), so scaling the charge here scales however far it flies.
    [HarmonyPatch(typeof(ThrowingWeapon), nameof(ThrowingWeapon.Throw))]
    [HarmonyPrefix]
    private static void BeforeThrow(ThrowingWeapon __instance, ref float strength)
    {
        if (IsOurs(__instance.ItemDataBlock)) strength *= ThrowForceMultiplier.Value;
    }

    // The glowstick model sitting as loot (in a box, a locker, or out in the open) before anyone
    // picks it up: a separate instance from the held/thrown one (GlowstickInstance), so it needs
    // its own reskin.
    [HarmonyPatch(typeof(ItemInLevel), nameof(ItemInLevel.Setup))]
    [HarmonyPostfix]
    private static void AfterPickupSetup(ItemInLevel __instance, ItemDataBlock data)
    {
        try
        {
            if (!IsOurs(data)) return;
            Reskin(__instance.gameObject);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons couldn't reskin a dropped Hand Grenade: {e.Message}");
        }
    }

    // A thrown one bounces with a grenade's clank instead of a glowstick's clatter.
    [HarmonyPatch(typeof(GlowstickInstance), nameof(GlowstickInstance.OnCollisionEnter))]
    [HarmonyPrefix]
    private static bool BeforeCollision(GlowstickInstance __instance)
    {
        try
        {
            if (!IsOurs(__instance.ItemDataBlock)) return true;
            Sounds.PlayLimited($"hand-grenade:{__instance.Pointer}", 0.15f, Sounds.HandGrenadeBounce.Value, __instance.transform.position);
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    // Thrown: the same reskin, and the fuse. Runs on every client's copy of the thrown item.
    [HarmonyPatch(typeof(GlowstickInstance), nameof(GlowstickInstance.Setup))]
    [HarmonyPostfix]
    private static void AfterInstanceSetup(GlowstickInstance __instance, ItemDataBlock data)
    {
        try
        {
            if (!IsOurs(data) || __instance.GetComponent<HandGrenadeFuse>() != null) return;
            __instance.enabled = false; // no glow fading in and out
            if (__instance.m_light != null) __instance.m_light.enabled = false;
            Reskin(__instance.gameObject);
            __instance.gameObject.AddComponent<HandGrenadeFuse>();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons couldn't arm a Hand Grenade: {e}");
        }
    }

    private const string ModelName = "GTFO.AbsurdWeapons Hand Grenade Model";

    // Hides every renderer and light already on `go` and adds the lock melter's meshes, on the
    // same layer (the first-person item has its own).
    internal static void Reskin(GameObject go)
    {
        if (go.transform.Find(ModelName) != null) return;
        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        foreach (var light in go.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (var light in go.GetComponentsInChildren<EffectLight>(true)) light.enabled = false;

        var model = new GameObject(ModelName);
        model.transform.SetParent(go.transform, false);
        LauncherGrenade.BuildModel(model.transform);
        foreach (var child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = go.layer;
    }
}

/// <summary>
/// A thrown Hand Grenade's fuse: blinks red faster and faster, then blows. The thrower's game (or
/// the host's, if the item has no owner) deals the damage, through the bullet path so it syncs;
/// every game shows the blast. The host then despawns the item.
/// </summary>
internal sealed class HandGrenadeFuse : MonoBehaviour
{
    private static readonly Color FlashColor = new(1f, 0.05f, 0.05f, 1f);

    private float age, nextFlash;
    private bool exploded, lit;
    private EffectLight? blink;

    public HandGrenadeFuse(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        if (exploded) return;
        try
        {
            age += Time.deltaTime;
            var fuse = HandGrenade.FuseSeconds.Value;
            if (age >= fuse)
            {
                Explode();
                return;
            }

            if (blink == null)
            {
                var lightObject = new GameObject("Blink");
                lightObject.transform.SetParent(transform, false);
                blink = lightObject.AddComponent<EffectLight>();
                blink.Color = FlashColor;
                blink.Range = 2.5f;
                blink.enabled = true;
            }
            var interval = Formulas.GrenadeFlashInterval(age, fuse);
            if (age >= nextFlash)
            {
                nextFlash = age + interval;
                lit = true;
                if (interval > 0.12f) CellSound.Post(AK.EVENTS.DEPLOYER_BEEP_TARGET_ACQUIRED, transform.position);
            }
            else if (lit && age >= nextFlash - interval * 0.55f)
            {
                lit = false;
            }
            blink.transform.position = transform.position + Vector3.up * 0.1f;
            blink.Intensity = lit ? 3f + 3f * Mathf.Clamp01(age / fuse) : 0f;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Hand Grenade fuse failed: {e.Message}");
            Explode();
        }
    }

    [HideFromIl2Cpp]
    private void Explode()
    {
        if (exploded) return;
        exploded = true;
        var position = transform.position;
        Explosion.Show(position);

        var item = GetComponent<Item>();
        try
        {
            var owner = item != null ? item.Owner : null;
            var source = owner != null ? (owner.IsLocallyOwned ? owner : null) : (SNet.IsMaster ? PlayerManager.GetLocalPlayerAgent() : null);
            if (source != null)
                AreaDamage.Apply(position, HandGrenade.BlastRadius.Value, HandGrenade.BlastDamage.Value, HandGrenade.BlastEdgeFraction.Value,
                    HandGrenade.BlastStagger.Value, source.Cast<Agent>(), null, HandGrenade.ItemId);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Hand Grenade blast failed: {e.Message}");
        }

        // Gone: the host despawns the synced item for everyone; until that arrives it's hidden here.
        try
        {
            var replicator = item?.ReplicationWrapper?.Replicator;
            if (SNet.IsMaster && replicator != null)
            {
                ItemReplicationManager.DeSpawn(replicator);
                return;
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons couldn't despawn a Hand Grenade: {e.Message}");
        }
        gameObject.SetActive(false);
    }
}

/// <summary>
/// Always-on: keeps the local player's Hand Grenades topped up (infinite), and on the host gives
/// every player a full stack when they drop into a level.
/// </summary>
internal sealed class HandGrenadeSupply : MonoBehaviour
{
    private readonly HashSet<ulong> supplied = new();
    private float nextSupplyCheck;

    public HandGrenadeSupply(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        try
        {
            if (GameStateManager.CurrentStateName != eGameStateName.InLevel)
            {
                supplied.Clear();
                return;
            }
            KeepFull();
            if (!SNet.IsMaster || !HandGrenade.GiveOnDrop.Value || Time.time < nextSupplyCheck) return;
            nextSupplyCheck = Time.time + 2f;
            foreach (var player in PlayerManager.PlayerAgentsInLevel)
            {
                if (player == null || !player.Alive || player.Owner == null || supplied.Contains(player.Owner.Lookup)) continue;
                if (Give(player)) supplied.Add(player.Owner.Lookup);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Hand Grenade supply failed: {e.Message}");
            nextSupplyCheck = Time.time + 10f;
        }
    }

    // While a Hand Grenade is in the local consumable slot, its count never goes down.
    private static void KeepFull()
    {
        var backpack = PlayerBackpackManager.LocalBackpack;
        if (backpack == null || !backpack.TryGetBackpackItem(InventorySlot.Consumable, out var item) || item == null) return;
        if (item.ItemID != HandGrenade.ItemId && !HandGrenade.IsOurs(item.Instance?.ItemDataBlock)) return;
        var storage = backpack.AmmoStorage;
        var ammo = storage?.GetInventorySlotAmmo(InventorySlot.Consumable);
        if (storage == null || ammo == null || ammo.IsFull) return;
        storage.SetAmmo(ammo.AmmoType, ammo.AmmoMaxCap);
        storage.UpdateAllAmmoUI();
        storage.NeedsSync = true;
    }

    // A full stack straight into the player's consumable slot (the host adds it; it syncs to them).
    [HideFromIl2Cpp]
    private static bool Give(PlayerAgent player)
    {
        var node = player.CourseNode;
        if (GameDataBlockBase<ItemDataBlock>.GetBlock(HandGrenade.ItemId) == null || node == null) return false;

        var data = new pItemData { itemID_gearCRC = HandGrenade.ItemId, slot = InventorySlot.Consumable };
        data.custom.ammo = HandGrenade.StackSize.Value;
        data.originCourseNode.Set(node);
        PlayerBackpackManager.MasterAddItem(data, player.Owner);
        Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons: {HandGrenade.StackSize.Value} Hand Grenades given to {player.Owner.NickName}.");
        return true;
    }
}
