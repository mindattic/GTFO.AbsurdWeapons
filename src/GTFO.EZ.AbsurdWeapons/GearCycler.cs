using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using GameData;
using Gear;
using MindAttic.GTFO.Shared;
using Player;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// Swaps the weapon in the slot you're holding for the next (or previous) one in that slot's
/// cycle, mid-level - main, special, tool or melee. The cycle is the gear you dropped in with
/// (added the first time you swap that slot) followed by this mod's weapons for the slot, so what
/// you brought is always one press away. Keys come from the config ([Gear Cycling], PageUp /
/// PageDown by default). It equips through the game's own loadout path
/// (PlayerBackpackManager.EquipLocalGear, which syncs to teammates), then re-wields the slot, and a
/// five-second warden message names what you switched to.
/// Every weapon keeps its own ammo for the rest of the level: what's in its magazine and the
/// slot's reserve are saved when you swap away from it and put back when you swap to it again. One
/// of this mod's weapons starts full the first time it's equipped in a level. Home puts every
/// swapped slot back to the gear you dropped in with, all at once, to resume a serious run.
/// </summary>
internal sealed class GearCycler : MonoBehaviour
{
    private static ConfigEntry<KeyCode> nextKey = null!;
    private static ConfigEntry<KeyCode> previousKey = null!;
    private static ConfigEntry<KeyCode> restoreKey = null!;
    private static ConfigEntry<bool> slotKeysCycle = null!;

    // The game's own select-slot actions (so rebinds are followed): pressed again while that slot is
    // already in hand, it cycles the slot - the game itself does nothing with that press.
    private static readonly (InputAction Action, InventorySlot Slot)[] SlotKeys =
    {
        (InputAction.SelectMelee, InventorySlot.GearMelee),
        (InputAction.SelectStandard, InventorySlot.GearStandard),
        (InputAction.SelectSpecial, InventorySlot.GearSpecial),
        (InputAction.SelectTool, InventorySlot.GearClass),
    };

    private sealed class SavedAmmo
    {
        public int? Clip;
        public AmmoType? AmmoType;
        public float Reserve;
    }

    // Each swapped slot's gear as it was before this level's first swap of it.
    private readonly Dictionary<InventorySlot, GearIDRange> starting = new();

    // Each weapon's own ammo (by gear category), saved when it's swapped away.
    private readonly Dictionary<uint, SavedAmmo> ammo = new();

    // Slots whose newly equipped gear is still being built: the category expected there. Its ammo
    // goes in once it's loaded, then `wieldSlot` is wielded.
    private readonly Dictionary<InventorySlot, uint> pending = new();
    private InventorySlot wieldSlot = InventorySlot.None;
    private float pendingFrom, pendingGiveUpAt;

    public GearCycler(IntPtr ptr) : base(ptr) { }

    internal static void Bind(ConfigFile config)
    {
        nextKey = config.Bind("Gear Cycling", "NextWeaponKey", KeyCode.PageUp,
            "Swaps the weapon in the slot you're holding (main, special, tool or melee) for the next one in that slot's cycle: the gear you dropped in with, then this mod's weapons.");
        previousKey = config.Bind("Gear Cycling", "PreviousWeaponKey", KeyCode.PageDown,
            "Swaps it for the previous one.");
        restoreKey = config.Bind("Gear Cycling", "RestoreLoadoutKey", KeyCode.Home,
            "Puts every slot you've swapped this level - main, special, tool and melee at once - back to the gear you dropped in with, with that gear's own ammo.");
        slotKeysCycle = config.Bind("Gear Cycling", "SlotKeysCycle", true,
            "Pressing a slot's own select key (melee, main, special, tool - 1 to 4 by default) while that slot is already in hand cycles it to the next weapon, same as NextWeaponKey.");
    }

    private void Update()
    {
        try
        {
            if (GameStateManager.CurrentStateName != eGameStateName.InLevel)
            {
                starting.Clear();
                ammo.Clear();
                pending.Clear();
                wieldSlot = InventorySlot.None;
                return;
            }

            if (pending.Count > 0) FinishPending();

            if (FocusStateManager.CurrentState != eFocusState.FPS) return;
            if (Input.GetKeyDown(restoreKey.Value)) Restore();
            else if (Input.GetKeyDown(nextKey.Value)) Cycle(1);
            else if (Input.GetKeyDown(previousKey.Value)) Cycle(-1);
            else if (slotKeysCycle.Value && SlotKeyPressedAgain()) Cycle(1);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons gear cycling failed: {e}");
        }
    }

    private void Cycle(int step)
    {
        var player = PlayerManager.GetLocalPlayerAgent();
        if (player == null || !player.Alive) return;
        var slot = player.Inventory.WieldedSlot;
        if (slot is not (InventorySlot.GearStandard or InventorySlot.GearSpecial or InventorySlot.GearClass or InventorySlot.GearMelee))
        {
            Show("Hold a main, special, tool or melee weapon to cycle it");
            return;
        }

        var ours = new List<GearIDRange>();
        foreach (var gear in GearManager.GetAllGearForSlot(slot))
            if (IsOurs(gear.GetCompID(eGearComponent.Category))) ours.Add(gear);
        if (ours.Count == 0)
        {
            Show("No GTFO.EZ.AbsurdWeapons weapons for this slot");
            return;
        }

        var current = pending.TryGetValue(slot, out var building) ? building : CategoryIn(slot);
        if (!starting.ContainsKey(slot))
        {
            var held = GearIn(slot);
            if (held != null) starting[slot] = held;
        }

        // The gear you dropped in with first (unless it's one of ours already), then ours.
        var cycle = new List<GearIDRange>();
        if (starting.TryGetValue(slot, out var start) && !IsOurs(start.GetCompID(eGearComponent.Category))) cycle.Add(start);
        cycle.AddRange(ours);

        var index = cycle.FindIndex(g => g.GetCompID(eGearComponent.Category) == current);
        var next = Formulas.CycleIndex(index, step, cycle.Count);
        var chosen = cycle[next];
        var chosenCategory = chosen.GetCompID(eGearComponent.Category);

        if (!pending.ContainsKey(slot)) SaveAmmo(slot);
        Equip(slot, chosen, slot);
        var name = GameDataBlockBase<GearCategoryDataBlock>.GetBlock(chosenCategory)?.PublicName?.UntranslatedText;
        Show($"{name ?? "weapon"}  [{next + 1}/{cycle.Count}]");
    }

    // A slot's select key pressed while that slot is already the one in hand.
    private static bool SlotKeyPressedAgain()
    {
        var wielded = PlayerManager.GetLocalPlayerAgent()?.Inventory?.WieldedSlot ?? InventorySlot.None;
        foreach (var (action, slot) in SlotKeys)
            if (slot == wielded && InputMapper.GetButtonDownKeyMouse(action, eFocusState.FPS)) return true;
        return false;
    }

    private void Restore()
    {
        var player = PlayerManager.GetLocalPlayerAgent();
        if (player == null || !player.Alive) return;
        if (starting.Count == 0)
        {
            Show("Already on your starting loadout");
            return;
        }

        var hand = player.Inventory.WieldedSlot;
        foreach (var (slot, gear) in starting)
        {
            var category = gear.GetCompID(eGearComponent.Category);
            var current = pending.TryGetValue(slot, out var building) ? building : CategoryIn(slot);
            if (current == category) continue;
            if (!pending.ContainsKey(slot)) SaveAmmo(slot);
            Equip(slot, gear, hand);
        }
        starting.Clear();
        Show("Starting loadout restored");
    }

    private void Equip(InventorySlot slot, GearIDRange gear, InventorySlot wieldAfter)
    {
        PlayerBackpackManager.EquipLocalGear(gear);
        pending[slot] = gear.GetCompID(eGearComponent.Category);
        wieldSlot = wieldAfter;
        pendingFrom = Time.time + 0.4f; // the new item is built asynchronously
        pendingGiveUpAt = Time.time + 5f;
    }

    // The weapon now in `slot`: its magazine and the slot's reserve, kept under its category.
    private void SaveAmmo(InventorySlot slot)
    {
        var backpack = PlayerBackpackManager.LocalBackpack;
        if (backpack == null || !backpack.TryGetBackpackItem(slot, out var item) || item?.GearIDRange == null) return;
        var entry = new SavedAmmo();
        var gun = item.Instance?.TryCast<BulletWeapon>();
        if (gun != null) entry.Clip = gun.GetCurrentClip();
        if (slot is InventorySlot.GearStandard or InventorySlot.GearSpecial or InventorySlot.GearClass)
        {
            var type = PlayerAmmoStorage.GetAmmoTypeFromSlot(slot);
            entry.AmmoType = type;
            entry.Reserve = backpack.AmmoStorage.GetAmmoInPack(type);
        }
        ammo[item.GearIDRange.GetCompID(eGearComponent.Category)] = entry;
    }

    // Once every newly equipped item is built and loaded: each gets its own ammo back (one of
    // ours seen for the first time this level starts full), and the slot that was held is wielded.
    private void FinishPending()
    {
        if (Time.time < pendingFrom) return;
        var backpack = PlayerBackpackManager.LocalBackpack;
        if (backpack == null) return;

        var ready = true;
        foreach (var (slot, category) in pending)
        {
            if (!backpack.TryGetBackpackItem(slot, out var item) || item == null || !item.IsLoaded || item.Instance == null
                || item.GearIDRange?.GetCompID(eGearComponent.Category) != category)
                ready = false;
        }
        if (!ready && Time.time < pendingGiveUpAt) return;
        if (!ready) Plugin.Logger.LogWarning("GTFO.EZ.AbsurdWeapons: swapped gear took too long to build; setting its ammo anyway.");

        var storage = backpack.AmmoStorage;
        foreach (var (slot, category) in pending)
        {
            backpack.TryGetBackpackItem(slot, out var item);
            var gun = item?.Instance?.TryCast<BulletWeapon>();
            var hasReserve = slot is InventorySlot.GearStandard or InventorySlot.GearSpecial or InventorySlot.GearClass;
            if (ammo.TryGetValue(category, out var saved))
            {
                if (saved.AmmoType.HasValue) storage.SetAmmo(saved.AmmoType.Value, saved.Reserve);
                if (saved.Clip.HasValue) gun?.SetCurrentClip(saved.Clip.Value);
            }
            else if (IsOurs(category))
            {
                if (hasReserve)
                {
                    var slotAmmo = storage.GetInventorySlotAmmo(slot);
                    if (slotAmmo != null) storage.SetAmmo(slotAmmo.AmmoType, slotAmmo.AmmoMaxCap);
                }
                if (gun != null) gun.SetCurrentClip(gun.ClipSize);
            }
            if (hasReserve) storage.SetClipAmmoInSlot(slot);
        }
        storage.UpdateAllAmmoUI();
        storage.NeedsSync = true;
        pending.Clear();

        if (wieldSlot != InventorySlot.None) PlayerManager.GetLocalPlayerAgent()?.Sync?.WantsToWieldSlot(wieldSlot);
        wieldSlot = InventorySlot.None;
    }

    private static GearIDRange? GearIn(InventorySlot slot)
    {
        var backpack = PlayerBackpackManager.LocalBackpack;
        return backpack != null && backpack.TryGetBackpackItem(slot, out var item) ? item?.GearIDRange : null;
    }

    private static uint CategoryIn(InventorySlot slot) => GearIn(slot)?.GetCompID(eGearComponent.Category) ?? 0;

    private static bool IsOurs(uint categoryId) => DataBlocks.AddedCategories.Contains(categoryId);

    // A new message replaces one of ours (or another GTFO.* mod's) still up, instead of queueing
    // behind it; the game's own messages are never cut short (MindAttic.GTFO.Shared.WardenMessage).
    internal static void Show(string message)
    {
        try
        {
            WardenMessage.Show(message);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons: {message} (no warden message: {e.Message})");
        }
    }
}
