using System;
using Gear;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// Keeps this mod's gun in the local player's hands fully loaded with a full reserve, every frame,
/// so it never runs dry and a gun just swapped in by <see cref="GearCycler"/> starts loaded.
/// SeekerGuns also refills the clip after every shot, which covers several shots in one frame.
/// The Grenade Launcher (a tool) is kept full by GrenadeLauncher.
/// </summary>
internal sealed class InfiniteAmmo : MonoBehaviour
{
    public InfiniteAmmo(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
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
