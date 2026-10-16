using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using GameData;
using Gear;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// Swaps the weapon in the slot you're holding for the next (or previous) one of this mod's
/// weapons for that slot, mid-level - main, special, tool or melee - so they can be tried without
/// going back to the lobby. Keys come from the config ([Gear Cycling], PageUp / PageDown by
/// default). It equips through the game's own loadout path (PlayerBackpackManager.EquipLocalGear,
/// which syncs to teammates), then re-wields the slot, and a one-second warden message names what
/// you switched to. Vanilla gear is never in the cycle.
/// </summary>
internal sealed class GearCycler : MonoBehaviour
{
    private static ConfigEntry<KeyCode> nextKey = null!;
    private static ConfigEntry<KeyCode> previousKey = null!;

    private InventorySlot pendingWield = InventorySlot.None;
    private uint pendingChosen; // the category just equipped, while its wield is pending
    private float wieldAt;

    public GearCycler(IntPtr ptr) : base(ptr) { }

    internal static void Bind(ConfigFile config)
    {
        nextKey = config.Bind("Gear Cycling", "NextWeaponKey", KeyCode.PageUp,
            "Swaps the weapon in the slot you're holding (main, special, tool or melee) for the next of this mod's weapons for that slot, mid-level.");
        previousKey = config.Bind("Gear Cycling", "PreviousWeaponKey", KeyCode.PageDown,
            "Swaps it for the previous one.");
    }

    private void Update()
    {
        try
        {
            if (pendingWield != InventorySlot.None && Time.time >= wieldAt)
            {
                var slot = pendingWield;
                pendingWield = InventorySlot.None;
                PlayerManager.GetLocalPlayerAgent()?.Sync?.WantsToWieldSlot(slot);
            }

            int step;
            if (Input.GetKeyDown(nextKey.Value)) step = 1;
            else if (Input.GetKeyDown(previousKey.Value)) step = -1;
            else return;
            if (FocusStateManager.CurrentState != eFocusState.FPS) return;
            Cycle(step);
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

        PlayerBackpackManager.EquipLocalGear(chosen);
        pendingWield = slot;
        pendingChosen = chosen.GetCompID(eGearComponent.Category);
        wieldAt = Time.time + 0.4f; // the new item is built asynchronously
        var name = GameDataBlockBase<GearCategoryDataBlock>.GetBlock(chosen.GetCompID(eGearComponent.Category))?.PublicName?.UntranslatedText;
        Show($"{name ?? "weapon"}  [{next + 1}/{ours.Count}]");
    }

    private static bool IsOurs(uint categoryId) => DataBlocks.AddedCategories.Contains(categoryId);

    private static void Show(string message)
    {
        try
        {
            GuiManager.PlayerLayer?.m_wardenIntel?.ShowSubObjectiveMessage("", message, false, 200f, 1f);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons: {message} (no warden message: {e.Message})");
        }
    }
}
