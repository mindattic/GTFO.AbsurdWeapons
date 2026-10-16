using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using GameData;
using Gear;
using Il2CppInterop.Runtime;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// Swaps the weapon in the slot you're holding for the next (or previous) one of this mod's
/// weapons for that slot, mid-level - main, special, tool or melee - so they can be tried without
/// going back to the lobby. Keys come from the config ([Gear Cycling], PageUp / PageDown by
/// default). It equips through the game's own loadout path (PlayerBackpackManager.EquipLocalGear,
/// which syncs to teammates), then re-wields the slot, and a five-second warden message names what
/// you switched to. Vanilla gear is never in the cycle.
/// The first swap of a slot in a level saves what was in it - the gear, its magazine and its
/// reserve ammo - and Home puts every swapped slot back exactly as saved, all at once, to resume a
/// serious run.
/// </summary>
internal sealed class GearCycler : MonoBehaviour
{
    private static ConfigEntry<KeyCode> nextKey = null!;
    private static ConfigEntry<KeyCode> previousKey = null!;
    private static ConfigEntry<KeyCode> restoreKey = null!;

    private InventorySlot pendingWield = InventorySlot.None;
    private uint pendingChosen; // the category just equipped, while its wield is pending
    private float wieldAt;

    private sealed class SavedSlot
    {
        public GearIDRange Gear = null!;
        public int? Clip;
        public AmmoType? AmmoType;
        public float Reserve;
    }

    // Each slot as it was before this level's first swap of it.
    private readonly Dictionary<InventorySlot, SavedSlot> saved = new();

    // A restore whose gear is being built: its ammo goes back once every item is loaded.
    private Dictionary<InventorySlot, SavedSlot>? restoring;
    private InventorySlot restoreWield;
    private float restoreFrom, restoreGiveUpAt;

    public GearCycler(IntPtr ptr) : base(ptr) { }

    internal static void Bind(ConfigFile config)
    {
        nextKey = config.Bind("Gear Cycling", "NextWeaponKey", KeyCode.PageUp,
            "Swaps the weapon in the slot you're holding (main, special, tool or melee) for the next of this mod's weapons for that slot, mid-level.");
        previousKey = config.Bind("Gear Cycling", "PreviousWeaponKey", KeyCode.PageDown,
            "Swaps it for the previous one.");
        restoreKey = config.Bind("Gear Cycling", "RestoreLoadoutKey", KeyCode.Home,
            "Puts back every slot you've swapped this level - main, special, tool and melee at once - as it was before its first swap, ammo and magazines included.");
    }

    private void Update()
    {
        try
        {
            if (GameStateManager.CurrentStateName != eGameStateName.InLevel)
            {
                saved.Clear();
                restoring = null;
                showing = false;
                pendingWield = InventorySlot.None;
                return;
            }

            if (restoring != null) FinishRestore();

            if (pendingWield != InventorySlot.None && Time.time >= wieldAt)
            {
                var slot = pendingWield;
                pendingWield = InventorySlot.None;
                PlayerManager.GetLocalPlayerAgent()?.Sync?.WantsToWieldSlot(slot);
            }

            if (FocusStateManager.CurrentState != eFocusState.FPS) return;
            if (Input.GetKeyDown(restoreKey.Value)) Restore();
            else if (Input.GetKeyDown(nextKey.Value)) Cycle(1);
            else if (Input.GetKeyDown(previousKey.Value)) Cycle(-1);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons gear cycling failed: {e}");
        }
    }

    private void Cycle(int step)
    {
        var player = PlayerManager.GetLocalPlayerAgent();
        if (player == null || !player.Alive) return;
        if (restoring != null) return;
        var slot = player.Inventory.WieldedSlot;
        if (slot is not (InventorySlot.GearStandard or InventorySlot.GearSpecial or InventorySlot.GearClass or InventorySlot.GearMelee))
        {
            Show("Hold a main, special, tool or melee weapon to cycle it");
            return;
        }

        // This mod's weapons for that slot, in loadout order.
        var ours = new List<GearIDRange>();
        foreach (var gear in GearManager.GetAllGearForSlot(slot))
            if (IsOurs(gear.GetCompID(eGearComponent.Category))) ours.Add(gear);
        if (ours.Count == 0)
        {
            Show("No GTFO.AbsurdWeapons weapons for this slot");
            return;
        }

        // Until the last swap's item is in hand, step on from what was just chosen.
        var current = pendingWield == slot ? pendingChosen : player.Inventory.WieldedItem?.GearCategoryData?.persistentID ?? 0;
        var index = ours.FindIndex(g => g.GetCompID(eGearComponent.Category) == current);
        var next = Formulas.CycleIndex(index, step, ours.Count);
        var chosen = ours[next];

        if (!saved.ContainsKey(slot)) Save(slot);
        PlayerBackpackManager.EquipLocalGear(chosen);
        pendingWield = slot;
        pendingChosen = chosen.GetCompID(eGearComponent.Category);
        wieldAt = Time.time + 0.4f; // the new item is built asynchronously
        var name = GameDataBlockBase<GearCategoryDataBlock>.GetBlock(chosen.GetCompID(eGearComponent.Category))?.PublicName?.UntranslatedText;
        Show($"{name ?? "weapon"}  [{next + 1}/{ours.Count}]");
    }

    private void Save(InventorySlot slot)
    {
        var backpack = PlayerBackpackManager.LocalBackpack;
        if (backpack == null || !backpack.TryGetBackpackItem(slot, out var item) || item?.GearIDRange == null) return;
        var entry = new SavedSlot { Gear = item.GearIDRange };
        var gun = item.Instance?.TryCast<BulletWeapon>();
        if (gun != null) entry.Clip = gun.GetCurrentClip();
        if (slot is InventorySlot.GearStandard or InventorySlot.GearSpecial or InventorySlot.GearClass)
        {
            var type = PlayerAmmoStorage.GetAmmoTypeFromSlot(slot);
            entry.AmmoType = type;
            entry.Reserve = backpack.AmmoStorage.GetAmmoInPack(type);
        }
        saved[slot] = entry;
    }

    private void Restore()
    {
        var player = PlayerManager.GetLocalPlayerAgent();
        if (player == null || !player.Alive || restoring != null) return;
        if (saved.Count == 0)
        {
            Show("Already on your starting loadout");
            return;
        }

        var backpack = PlayerBackpackManager.LocalBackpack;
        foreach (var (slot, entry) in saved)
        {
            var current = backpack != null && backpack.TryGetBackpackItem(slot, out var item) ? item?.GearIDRange : null;
            if (current == null || current.GetCompID(eGearComponent.Category) != entry.Gear.GetCompID(eGearComponent.Category))
                PlayerBackpackManager.EquipLocalGear(entry.Gear);
        }

        restoring = new Dictionary<InventorySlot, SavedSlot>(saved);
        saved.Clear();
        pendingWield = InventorySlot.None;
        restoreWield = player.Inventory.WieldedSlot;
        restoreFrom = Time.time + 0.4f; // the items are built asynchronously
        restoreGiveUpAt = Time.time + 5f;
        Show("Starting loadout restored");
    }

    // Once every restored item is built and loaded: its magazine and reserve go back as saved, and
    // the slot that was held is wielded again.
    private void FinishRestore()
    {
        if (Time.time < restoreFrom) return;
        var backpack = PlayerBackpackManager.LocalBackpack;
        if (backpack == null) return;

        var ready = true;
        foreach (var (slot, entry) in restoring!)
        {
            if (!backpack.TryGetBackpackItem(slot, out var item) || item == null || !item.IsLoaded || item.Instance == null
                || item.GearIDRange?.GetCompID(eGearComponent.Category) != entry.Gear.GetCompID(eGearComponent.Category))
                ready = false;
        }
        if (!ready && Time.time < restoreGiveUpAt) return;
        if (!ready) Plugin.Logger.LogWarning("GTFO.AbsurdWeapons: the starting loadout took too long to build; restoring the ammo anyway.");

        var storage = backpack.AmmoStorage;
        foreach (var (slot, entry) in restoring!)
        {
            if (entry.AmmoType.HasValue) storage.SetAmmo(entry.AmmoType.Value, entry.Reserve);
            if (entry.Clip.HasValue && backpack.TryGetBackpackItem(slot, out var item))
                item?.Instance?.TryCast<BulletWeapon>()?.SetCurrentClip(entry.Clip.Value);
        }
        storage.UpdateAllAmmoUI();
        storage.NeedsSync = true;
        restoring = null;
        PlayerManager.GetLocalPlayerAgent()?.Sync?.WantsToWieldSlot(restoreWield);
    }

    private static bool IsOurs(uint categoryId) => DataBlocks.AddedCategories.Contains(categoryId);

    // Whether one of our messages is queued or on screen (cleared by its onDone).
    private static bool showing;

    // A new message replaces ours if one is still up, instead of queueing behind it. The game's own
    // messages are never cut short: only while ours is up is the panel's sequence stopped and its
    // queue reset.
    private static void Show(string message)
    {
        try
        {
            var intel = GuiManager.PlayerLayer?.m_wardenIntel;
            if (intel == null) return;
            if (showing)
            {
                if (intel.m_newObjectiveRoutine != null) intel.StopCoroutine(intel.m_newObjectiveRoutine);
                if (intel.m_revealTextRoutine != null) intel.StopCoroutine(intel.m_revealTextRoutine);
                intel.m_newObjectiveRoutine = null;
                intel.m_revealTextRoutine = null;
                intel.ResetSubObjectiveMesssagQueue();
            }
            showing = true;
            intel.ShowSubObjectiveMessage("", message, false, 200f, 5f,
                DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() => showing = false)));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons: {message} (no warden message: {e.Message})");
        }
    }
}
