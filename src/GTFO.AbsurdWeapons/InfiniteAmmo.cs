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
/// With [Ammo] InfiniteAmmo on, firing costs nothing: SeekerGuns puts the magazine back after every
/// shot of any gun (vanilla ones too) and the stream guns skip their drain.
/// </summary>
internal static class InfiniteAmmo
{
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<float> StreamPercentPerSecond = null!;
    internal static ConfigEntry<KeyCode> ToggleKey = null!;

    internal static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Ammo", "InfiniteAmmo", false,
            "On: every gun - this mod's and the game's own - never runs out or reloads. Off: guns use ammo normally (this mod's each start full the first time they're equipped in a level).");
        StreamPercentPerSecond = config.Bind("Ammo", "StreamPercentPerSecond", 2f,
            "The lasers, Chain Lightning and Shrink Beam have no magazine and never reload: every second of sustained fire uses this percent of a full ammo reserve (2 = 1% every half second).");
        ToggleKey = config.Bind("Ammo", "InfiniteAmmoToggleKey", KeyCode.Insert,
            "Turns InfiniteAmmo on or off in a level and shows which as a warden message.");
    }

    internal static void Toggle()
    {
        Enabled.Value = !Enabled.Value;
        GearCycler.Show($"Infinite ammo: {(Enabled.Value ? "ON" : "OFF")}");
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

}

