using System;
using BepInEx.Configuration;
using Gear;
using Player;
using MindAttic.GTFO.Shared;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// Ammo for this mod's guns. By default they use ammo like any other gun - a magazine, a reserve
/// in the slot's ammo pool, reloads, ammo packs - except the held-trigger guns (lasers, Chain
/// Lightning, Shrink Beam, Tentacle Cannon), which spend ammo by how long the trigger is held:
/// every frame of fire costs that frame's time (<see cref="Drain"/>), so a full magazine lasts
/// <see cref="MagazineSeconds"/> of sustained fire. The stream guns never reload - an empty
/// magazine is refilled from the reserve - while the Tentacle Cannon reloads like any gun.
/// With infinite ammo on (Insert - the shared toggle every GTFO.* mod has, Shared/InfiniteToggles.cs,
/// published from GTFO.EZ), firing costs nothing: SeekerGuns puts the magazine back after every shot
/// of any gun (vanilla ones too) and the held-trigger guns skip their drain.
/// </summary>
internal static class InfiniteAmmo
{
    internal static ConfigEntry<float> MagazineSeconds = null!;

    internal static bool On => InfiniteToggles.InfiniteAmmoOn;

    internal static void Bind(ConfigFile config)
    {
        MagazineSeconds = config.Bind("Ammo", "MagazineSeconds", 10f,
            "The lasers, Chain Lightning, Shrink Beam and Tentacle Cannon spend ammo by how long the trigger is held: " +
            "a full magazine lasts this many seconds of sustained fire. The stream guns (all but the Tentacle Cannon) " +
            "never reload - an empty magazine is refilled straight from the reserve.");
    }

    // Rounds owed but not yet taken: a frame's share of a round, carried to the next frame.
    private static float owed;
    private static IntPtr owedGun;

    // Whether the wielded gun has any ammo left - in its magazine or, for a stream gun, its reserve.
    internal static bool HasStreamAmmo(PlayerAgent player)
    {
        if (On) return true;
        var gun = player.Inventory?.WieldedItem?.TryCast<BulletWeapon>();
        var storage = PlayerBackpackManager.LocalBackpack?.AmmoStorage;
        if (gun == null || storage == null) return false;
        if (gun.GetCurrentClip() > 0) return true;
        var reserve = storage.GetInventorySlotAmmo(player.Inventory!.WieldedSlot);
        return reserve != null && storage.GetBulletsInPack(reserve.AmmoType) > 0;
    }

    // `seconds` of held trigger on the wielded gun: that share of a magazine is spent. `refill`: a
    // stream gun, whose empty magazine is topped up from the reserve here (it never reloads).
    // False once there's nothing left to fire.
    internal static bool Drain(PlayerAgent player, float seconds, bool refill = true)
    {
        if (On) return true;
        var gun = player.Inventory?.WieldedItem?.TryCast<BulletWeapon>();
        var slot = player.Inventory?.WieldedSlot ?? InventorySlot.None;
        var storage = PlayerBackpackManager.LocalBackpack?.AmmoStorage;
        var reserve = storage?.GetInventorySlotAmmo(slot);
        if (gun == null || storage == null || reserve == null) return false;

        if (owedGun != gun.Pointer)
        {
            owedGun = gun.Pointer;
            owed = 0f;
        }
        owed += Mathf.Max(0f, seconds) * gun.ClipSize / Mathf.Max(0.1f, MagazineSeconds.Value);
        var rounds = Mathf.FloorToInt(owed);
        owed -= rounds;

        var clip = gun.GetCurrentClip();
        var changed = false; // most frames spend less than a round: nothing to show or sync then
        while (rounds > 0)
        {
            if (clip <= 0)
            {
                if (!refill) break;
                var take = Mathf.Min(gun.ClipSize, storage.GetBulletsInPack(reserve.AmmoType));
                if (take <= 0) break;
                storage.UpdateBulletsInPack(reserve.AmmoType, -take);
                clip = take;
            }
            var spent = Mathf.Min(rounds, clip);
            clip -= spent;
            rounds -= spent;
            changed = true;
        }
        if (clip <= 0 && refill)
        {
            var take = Mathf.Min(gun.ClipSize, storage.GetBulletsInPack(reserve.AmmoType));
            if (take > 0)
            {
                storage.UpdateBulletsInPack(reserve.AmmoType, -take);
                clip = take;
                changed = true;
            }
        }
        if (changed) Show(gun, storage, reserve, clip);
        return clip > 0;
    }

    // Sets the local player's wielded gun's magazine and syncs it to the HUD and teammates.
    internal static void SetClip(BulletWeapon gun, int clip)
    {
        var storage = PlayerBackpackManager.LocalBackpack?.AmmoStorage;
        var player = gun.Owner;
        var reserve = player != null ? storage?.GetInventorySlotAmmo(player.Inventory.WieldedSlot) : null;
        if (storage == null || reserve == null)
        {
            gun.SetCurrentClip(Mathf.Clamp(clip, 0, gun.ClipSize));
            return;
        }
        Show(gun, storage, reserve, clip);
    }

    // The magazine set and shown. Only the HUD is updated directly: the reserve is the game's own
    // pool (changed through UpdateBulletsInPack above), synced to teammates with NeedsSync.
    private static void Show(BulletWeapon gun, PlayerAmmoStorage storage, InventorySlotAmmo reserve, int clip)
    {
        clip = Mathf.Clamp(clip, 0, gun.ClipSize);
        if (gun.GetCurrentClip() != clip) gun.SetCurrentClip(clip);
        storage.UpdateSlotAmmoUI(reserve, clip);
        storage.NeedsSync = true;
    }
}
