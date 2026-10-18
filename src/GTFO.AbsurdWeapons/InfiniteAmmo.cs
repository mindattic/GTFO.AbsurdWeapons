using System;
using BepInEx.Configuration;
using Gear;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// Ammo for this mod's guns. By default they use ammo like any other gun - a magazine, a reserve
/// in the slot's ammo pool, reloads, ammo packs - and the stream guns (lasers, Chain Lightning,
/// Shrink Beam), which never fire through the game, spend one round per tick here (<see cref="TrySpend"/>).
/// With [Ammo] InfiniteAmmo on, the gun in the local player's hands is instead kept fully loaded
/// with a full reserve every frame, and SeekerGuns refills the clip after every shot.
/// </summary>
internal sealed class InfiniteAmmo : MonoBehaviour
{
    internal static ConfigEntry<bool> Enabled = null!;

    public InfiniteAmmo(IntPtr ptr) : base(ptr) { }

    internal static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Ammo", "InfiniteAmmo", false,
            "On: this mod's guns never run out or reload. Off: they use ammo like vanilla guns (each starts full the first time it's equipped in a level).");
    }

    // One round for one tick of a stream gun's beam. False (no tick) while it's reloading or
    // empty; an empty one starts reloading itself, as a vanilla gun would on an empty trigger pull.
    internal static bool TrySpend(PlayerAgent player, BulletWeapon gun)
    {
        if (Enabled.Value) return true;
        var inventory = player.Inventory;
        if (inventory == null || gun.IsReloading) return false;
        var clip = gun.GetCurrentClip();
        if (clip <= 0)
        {
            if (inventory.CanReloadCurrent()) inventory.TriggerReload();
            return false;
        }
        gun.SetCurrentClip(clip - 1);
        var storage = PlayerBackpackManager.LocalBackpack?.AmmoStorage;
        if (storage != null)
        {
            storage.SetClipAmmoInSlot(inventory.WieldedSlot);
            storage.UpdateAllAmmoUI();
            storage.NeedsSync = true;
        }
        return true;
    }

    private void Update()
    {
        if (!Enabled.Value) return;
        try
        {
            var player = PlayerManager.GetLocalPlayerAgent();
            if (player == null || !player.Alive) return;
            var gun = player.Inventory?.WieldedItem?.TryCast<BulletWeapon>();
            if (gun == null || !GunInjector.IsOurs(gun.ArchetypeData?.persistentID ?? 0)) return;

            var changed = false;
            if (gun.GetCurrentClip() < gun.ClipSize)
            {
                gun.SetCurrentClip(gun.ClipSize);
                changed = true;
            }

            var storage = PlayerBackpackManager.LocalBackpack?.AmmoStorage;
            var reserve = storage?.GetInventorySlotAmmo(player.Inventory!.WieldedSlot);
            if (storage == null || reserve == null) return;
            if (!reserve.IsFull)
            {
                storage.SetAmmo(reserve.AmmoType, reserve.AmmoMaxCap);
                changed = true;
            }
            if (!changed) return;
            storage.SetClipAmmoInSlot(player.Inventory!.WieldedSlot);
            storage.UpdateAllAmmoUI();
            storage.NeedsSync = true;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons couldn't keep the gun loaded: {e.Message}");
        }
    }
}
