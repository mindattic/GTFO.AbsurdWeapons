// Generated from GTFO.EZ src/GTFO.EZ/Shared/InfiniteToggles.cs (39a419b) - don't edit here.
// Change GTFO.EZ's copy and run GTFO.EZ/tools/publish-shared.ps1.
using System;
using Gear;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Player;
using UnityEngine;

namespace MindAttic.GTFO.Shared;

// Insert toggles infinite ammo, Delete infinite health, in every GTFO.* mod, each announced as a
// warden message. Only the owning copy (SharedOwner) reads the keys and refills ammo; the on/off
// state is shared, so every copy's damage block and any mod's own guns (InfiniteAmmoOn) agree.
// Off at the start of every session.
internal static class InfiniteToggles
{
    private const string AmmoKey = "InfiniteAmmo";
    private const string HealthKey = "InfiniteHealth";

    internal static bool InfiniteAmmoOn => SharedOwner.Get(AmmoKey);
    internal static bool InfiniteHealthOn => SharedOwner.Get(HealthKey);

    private static readonly InventorySlot[] AmmoSlots = { InventorySlot.GearStandard, InventorySlot.GearSpecial, InventorySlot.GearClass };

    private static int _lastFrame = -1;

    internal static void Tick()
    {
        // The host's bots run the same Update; once a frame is enough.
        if (Time.frameCount == _lastFrame || !SharedOwner.IsOwner) return;
        _lastFrame = Time.frameCount;

        if (FocusStateManager.CurrentState == eFocusState.FPS)
        {
            if (Input.GetKeyDown(KeyCode.Insert)) Toggle(AmmoKey, "Infinite ammo");
            if (Input.GetKeyDown(KeyCode.Delete)) Toggle(HealthKey, "Infinite health");
        }

        if (InfiniteAmmoOn) Refill();
    }

    private static void Toggle(string key, string what)
    {
        var on = !SharedOwner.Get(key);
        SharedOwner.Set(key, on);
        Show($"{what}: {(on ? "ON" : "OFF")}");
    }

    // Whether one of these messages is queued or on screen (cleared by its onDone).
    private static bool _showing;

    // A new toggle message replaces one still up instead of queueing behind it; the game's own
    // messages are never cut short.
    private static void Show(string message)
    {
        try
        {
            var intel = GuiManager.PlayerLayer?.m_wardenIntel;
            if (intel == null) return;
            if (_showing)
            {
                if (intel.m_newObjectiveRoutine != null) intel.StopCoroutine(intel.m_newObjectiveRoutine);
                if (intel.m_revealTextRoutine != null) intel.StopCoroutine(intel.m_revealTextRoutine);
                intel.m_newObjectiveRoutine = null;
                intel.m_revealTextRoutine = null;
                intel.ResetSubObjectiveMesssagQueue();
            }
            _showing = true;
            intel.ShowSubObjectiveMessage("", message, false, 200f, 5f,
                DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() => _showing = false)));
        }
        catch (Exception e)
        {
            SharedOwner.Log.LogInfo($"{message} (no warden message: {e.Message})");
        }
    }

    // Tops every gear slot's reserve and the wielded gun's magazine back up to full.
    private static void Refill()
    {
        try
        {
            if (!PlayerManager.TryGetLocalPlayerAgent(out var player) || player == null || !player.Alive) return;
            var storage = PlayerBackpackManager.LocalBackpack?.AmmoStorage;
            if (storage == null) return;

            var changed = false;
            foreach (var slot in AmmoSlots)
            {
                var ammo = storage.GetInventorySlotAmmo(slot);
                if (ammo == null || ammo.AmmoInPack >= ammo.AmmoMaxCap) continue;
                storage.SetAmmo(ammo.AmmoType, ammo.AmmoMaxCap);
                changed = true;
            }

            var gun = player.Inventory?.WieldedItem?.TryCast<BulletWeapon>();
            if (gun != null && gun.GetCurrentClip() < gun.ClipSize)
            {
                gun.SetCurrentClip(gun.ClipSize);
                storage.SetClipAmmoInSlot(player.Inventory!.WieldedSlot);
                changed = true;
            }

            if (!changed) return;
            storage.UpdateAllAmmoUI();
            storage.NeedsSync = true;
        }
        catch (Exception e)
        {
            SharedOwner.Log.LogWarning($"Infinite ammo: refill failed. {e.Message}");
        }
    }

    // Damage to your own player is dropped while infinite health is on. Bots and other players
    // still take damage.
    internal static bool AllowDamage(Dam_PlayerDamageLocal damage)
    {
        if (!InfiniteHealthOn) return true;
        var agent = damage.Owner;
        return agent == null || agent.Owner == null || agent.Owner.IsBot;
    }
}

[HarmonyPatch(typeof(Dam_PlayerDamageLocal))]
internal static class InfiniteToggles_Patches
{
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.Update))]
    [HarmonyPostfix]
    private static void AfterUpdate() => InfiniteToggles.Tick();

    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveBulletDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveExplosionDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveMeleeDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceivePushDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveFireDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveTentacleAttackDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveTentacleTrapGrabDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveParasiteTrapDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveTentacleTankGrabDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveShooterProjectileDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveParasiteDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveStickyDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveFallDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveNoAirDamage))]
    [HarmonyPatch(nameof(Dam_PlayerDamageLocal.ReceiveSetDead))]
    [HarmonyPrefix]
    private static bool BeforeDamage(Dam_PlayerDamageLocal __instance) => InfiniteToggles.AllowDamage(__instance);
}
