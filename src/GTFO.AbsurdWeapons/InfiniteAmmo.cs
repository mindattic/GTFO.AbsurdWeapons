using System;
using BepInEx.Configuration;
using Gear;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// Ammo for this mod's guns. By default they use ammo like any other gun - a magazine, a reserve
/// in the slot's ammo pool, reloads, ammo packs - except the stream guns (lasers, Chain Lightning,
/// Shrink Beam), which never fire through the game: they drain the reserve directly while firing
/// and never reload (<see cref="TrySpend"/>).
/// With [Ammo] InfiniteAmmo on, the gun in the local player's hands is instead kept fully loaded
/// with a full reserve every frame, and SeekerGuns refills the clip after every shot.
/// </summary>
internal sealed class InfiniteAmmo : MonoBehaviour
{
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<float> StreamPercentPerSecond = null!;

    public InfiniteAmmo(IntPtr ptr) : base(ptr) { }

    internal static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Ammo", "InfiniteAmmo", false,
            "On: this mod's guns never run out or reload. Off: they use ammo like vanilla guns (each starts full the first time it's equipped in a level).");
        StreamPercentPerSecond = config.Bind("Ammo", "StreamPercentPerSecond", 2f,
            "The lasers, Chain Lightning and Shrink Beam have no magazine and never reload: every second of sustained fire uses this percent of a full ammo reserve (2 = 1% every half second).");
    }

    // The stream guns (lasers, Chain Lightning, Shrink Beam) have no magazine and never reload:
    // sustained fire drains the slot's reserve directly, StreamPercentPerSecond of a full reserve
    // per second. Their magazine just shows full while there's reserve left and empty when not.
    internal static bool HasStreamAmmo(PlayerAgent player)
    {
        if (Enabled.Value) return true;
        var reserve = PlayerBackpackManager.LocalBackpack?.AmmoStorage?.GetInventorySlotAmmo(player.Inventory.WieldedSlot);
        return reserve != null && reserve.AmmoInPack > 0f;
    }

    // One tick of a stream gun's fire, `seconds` long. False (no tick) once the reserve is empty.
    internal static bool TrySpend(PlayerAgent player, float seconds)
    {
        if (Enabled.Value) return true;
        var gun = player.Inventory?.WieldedItem?.TryCast<BulletWeapon>();
        var slot = player.Inventory?.WieldedSlot ?? InventorySlot.None;
        var storage = PlayerBackpackManager.LocalBackpack?.AmmoStorage;
        var reserve = storage?.GetInventorySlotAmmo(slot);
        if (gun == null || storage == null || reserve == null) return false;
        var left = reserve.AmmoInPack;
        if (left > 0f) left = Mathf.Max(0f, left - reserve.AmmoMaxCap * StreamPercentPerSecond.Value / 100f * seconds);
        var fired = reserve.AmmoInPack > 0f;
        storage.SetAmmo(reserve.AmmoType, left);
        gun.SetCurrentClip(left > 0f ? gun.ClipSize : 0);
        storage.SetClipAmmoInSlot(slot);
        storage.UpdateAllAmmoUI();
        storage.NeedsSync = true;
        return fired;
    }

    // Sets the local player's wielded gun's magazine and syncs it to the HUD and teammates.
    internal static void SetClip(BulletWeapon gun, int clip)
    {
        gun.SetCurrentClip(Mathf.Clamp(clip, 0, gun.ClipSize));
        var player = gun.Owner;
        var storage = PlayerBackpackManager.LocalBackpack?.AmmoStorage;
        if (player == null || storage == null) return;
        storage.SetClipAmmoInSlot(player.Inventory.WieldedSlot);
        storage.UpdateAllAmmoUI();
        storage.NeedsSync = true;
    }

    // One round for a Tentacle Cannon tentacle that bit an enemy (its shots themselves are free),
    // off the gun the shooter is holding - free if they've since put it away.
    internal static void SpendTentacleBite(PlayerAgent owner)
    {
        if (Enabled.Value || !owner.IsLocallyOwned) return;
        var gun = owner.Inventory?.WieldedItem?.TryCast<BulletWeapon>();
        if (gun == null || !TentacleCannon.IsTentacleGun(gun.ArchetypeData?.persistentID ?? 0)) return;
        SetClip(gun, gun.GetCurrentClip() - 1);
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

