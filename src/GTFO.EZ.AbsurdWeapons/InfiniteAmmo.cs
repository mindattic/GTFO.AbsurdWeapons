using System;
using BepInEx.Configuration;
using Gear;
using Player;
using MindAttic.GTFO.Shared;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// Ammo for this mod's guns. By default they use ammo like any other gun - a magazine, a reserve
/// in the slot's ammo pool, reloads, ammo packs - except the stream guns (lasers, Chain Lightning,
/// Shrink Beam), which never fire through the game: they drain the reserve directly while firing
/// and never reload (<see cref="TrySpend"/>).
/// With infinite ammo on (Insert - the shared toggle every GTFO.* mod has, Shared/InfiniteToggles.cs,
/// published from GTFO.EZ), firing costs nothing: SeekerGuns puts the magazine back after every shot
/// of any gun (vanilla ones too) and the stream guns skip their drain.
/// </summary>
internal static class InfiniteAmmo
{
    internal static ConfigEntry<float> StreamPercentPerSecond = null!;

    internal static bool On => InfiniteToggles.InfiniteAmmoOn;

    internal static void Bind(ConfigFile config)
    {
        StreamPercentPerSecond = config.Bind("Ammo", "StreamPercentPerSecond", 2f,
            "The lasers, Chain Lightning and Shrink Beam have no magazine and never reload: every second of sustained fire uses this percent of a full ammo reserve (2 = 1% every half second).");
    }

    // The stream guns (lasers, Chain Lightning, Shrink Beam) never fire through the game or reload:
    // sustained fire drains their ammo directly, StreamPercentPerSecond of a full reserve per second.
    // Their ammo is one pool - the reserve plus whatever the game loaded into the magazine (it fills
    // a 60-100 round magazine from the reserve on spawn, which can leave the reserve itself near
    // empty) - and the magazine just shows as much of that pool as fits.
    internal static bool HasStreamAmmo(PlayerAgent player)
    {
        if (On) return true;
        var gun = player.Inventory?.WieldedItem?.TryCast<BulletWeapon>();
        var reserve = PlayerBackpackManager.LocalBackpack?.AmmoStorage?.GetInventorySlotAmmo(player.Inventory!.WieldedSlot);
        return gun != null && reserve != null && StreamPool(gun, reserve) > 0f;
    }

    // One tick of a stream gun's fire, `seconds` long. False (no tick) once its ammo is gone.
    internal static bool TrySpend(PlayerAgent player, float seconds)
    {
        if (On) return true;
        var gun = player.Inventory?.WieldedItem?.TryCast<BulletWeapon>();
        var slot = player.Inventory?.WieldedSlot ?? InventorySlot.None;
        var storage = PlayerBackpackManager.LocalBackpack?.AmmoStorage;
        var reserve = storage?.GetInventorySlotAmmo(slot);
        if (gun == null || storage == null || reserve == null) return false;

        var pool = StreamPool(gun, reserve);
        if (pool <= 0f) return false;
        pool = Mathf.Max(0f, pool - reserve.AmmoMaxCap * StreamPercentPerSecond.Value / 100f * seconds);

        // Show as much of what's left in the magazine as fits; the rest stays in the reserve.
        var cost = Mathf.Max(0.0001f, reserve.CostOfBullet);
        var clip = Mathf.Min(gun.ClipSize, Mathf.FloorToInt(pool / cost));
        storage.SetAmmo(reserve.AmmoType, pool - clip * cost);
        gun.SetCurrentClip(clip);
        storage.SetClipAmmoInSlot(slot);
        storage.UpdateAllAmmoUI();
        storage.NeedsSync = true;
        return true;
    }

    // A stream gun's whole ammo: its reserve plus the rounds sitting in its magazine.
    private static float StreamPool(BulletWeapon gun, InventorySlotAmmo reserve) =>
        reserve.AmmoInPack + gun.GetCurrentClip() * reserve.CostOfBullet;

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
        if (On || !owner.IsLocallyOwned) return;
        var gun = owner.Inventory?.WieldedItem?.TryCast<BulletWeapon>();
        if (gun == null || !TentacleCannon.IsTentacleGun(gun.ArchetypeData?.persistentID ?? 0)) return;
        SetClip(gun, gun.GetCurrentClip() - 1);
    }

}

