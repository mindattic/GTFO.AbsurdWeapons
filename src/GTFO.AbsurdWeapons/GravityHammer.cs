using System;
using System.Collections.Generic;
using GameData;
using Gear;
using HarmonyLib;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Gravity Hammer: its charge never swings by itself (MeleeInjector gives it a copy of the
/// hammer's animation set with the auto-attack switched off), and while you hold it, it drags
/// every enemy within 12 m that it can see toward you. Release a full charge and it lets go in a
/// blast like the Warhammer's shockwave, only bigger: it staggers everything around the point in
/// front of you and throws them back out (ImpactBurst.GravityBlast). The pull and the blast move
/// enemies through Knockback, which the host performs, so everyone sees the same thing.
/// </summary>
[HarmonyPatch]
internal static class GravityHammer
{
    internal const float PullRange = 12f;
    internal const float PullStep = 1.2f;      // metres per tug
    internal const float PullInterval = 0.2f;  // seconds between tugs
    internal const float PullStopDistance = 2.5f; // close enough: no closer than this
    private const float FullChargeLeeway = 0.95f;

    // The local player while they hold the Gravity Hammer's charge.
    internal static PlayerAgent? Holder;
    internal static float ChargeStarted = -1f;

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.ChangeState))]
    [HarmonyPostfix]
    private static void AfterChangeState(MeleeWeaponFirstPerson __instance, eMeleeWeaponState newState)
    {
        try
        {
            var archetype = __instance.MeleeArchetypeData;
            if (archetype == null || archetype.persistentID != MeleeInjector.GravityHammerId) return;
            var owner = __instance.Owner;
            if (owner == null || !owner.IsLocallyOwned) return;

            if (newState is eMeleeWeaponState.AttackChargeUpLeft or eMeleeWeaponState.AttackChargeUpRight)
            {
                Holder = owner;
                ChargeStarted = Time.time;
                return;
            }

            var held = ChargeStarted >= 0f ? Time.time - ChargeStarted : 0f;
            Holder = null;
            ChargeStarted = -1f;
            if (newState is not (eMeleeWeaponState.AttackChargeReleaseLeft or eMeleeWeaponState.AttackChargeReleaseRight)) return;

            var animations = GameDataBlockBase<MeleeAnimationSetDataBlock>.GetBlock(archetype.MeleeAnimationSet);
            var full = animations != null ? animations.MaxDamageChargeTime : 1.8f;
            if (!Formulas.IsFullCharge(held, full, FullChargeLeeway)) return;

            var forward = owner.FPSCamera != null ? owner.FPSCamera.Forward : owner.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : owner.transform.forward;
            var from = owner.Position + Vector3.up * 0.6f;
            var center = from + forward * 2.5f;
            if (Physics.Raycast(from, forward, out var wall, 2.5f, LayerManager.MASK_WORLD))
                center = wall.point - forward * 0.3f; // a wall in the way: go off on this side of it
            ImpactBursts.Release(ImpactBurst.GravityBlast, owner, center, archetype.persistentID);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Gravity Hammer failed: {e}");
        }
    }
}

/// <summary>Drags enemies toward the local player while they hold the Gravity Hammer's charge, with a pulsing violet glow.</summary>
internal sealed class GravityPull : MonoBehaviour
{
    private static readonly Color GlowColor = new(0.6f, 0.3f, 1f);
    private EffectLight? glow;
    private float nextTug;

    public GravityPull(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        var holder = GravityHammer.Holder;
        try
        {
            if (holder == null || !holder.Alive || !StillCharging(holder))
            {
                GravityHammer.Holder = null;
                if (glow != null) glow.Intensity = 0f;
                return;
            }

            var held = Time.time - GravityHammer.ChargeStarted;
            Glow(holder, held);
            if (Time.time < nextTug) return;
            nextTug = Time.time + GravityHammer.PullInterval;

            var center = holder.Position + Vector3.up;
            var tugged = new HashSet<IntPtr>();
            foreach (var collider in Physics.OverlapSphere(center, GravityHammer.PullRange, LayerManager.MASK_ENEMY_DAMAGABLE))
            {
                var enemy = collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner;
                if (enemy == null || !enemy.Alive || !tugged.Add(enemy.Pointer)) continue; // one tug per enemy per pulse

                var toward = holder.Position - enemy.Position;
                var distance = toward.magnitude;
                if (distance <= GravityHammer.PullStopDistance) continue;
                if (Physics.Linecast(center, enemy.AimTarget.position, LayerManager.MASK_WORLD)) continue;
                Knockback.Push(enemy, toward, Mathf.Min(GravityHammer.PullStep, distance - GravityHammer.PullStopDistance));
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons gravity pull failed: {e.Message}");
            GravityHammer.Holder = null;
        }
    }

    // Still holding the Gravity Hammer's charge: it's the wielded item and in a charge-up state.
    private static bool StillCharging(PlayerAgent holder)
    {
        var melee = holder.Inventory?.WieldedItem?.TryCast<MeleeWeaponFirstPerson>();
        if (melee == null || melee.MeleeArchetypeData?.persistentID != MeleeInjector.GravityHammerId) return false;
        return melee.CurrentStateName is eMeleeWeaponState.AttackChargeUpLeft or eMeleeWeaponState.AttackChargeUpRight;
    }

    private void Glow(PlayerAgent holder, float held)
    {
        if (glow == null)
        {
            var go = new GameObject("GTFO.AbsurdWeapons Gravity Glow");
            DontDestroyOnLoad(go);
            glow = go.AddComponent<EffectLight>();
            glow.Color = GlowColor;
            glow.Range = 5f;
            glow.enabled = true;
        }
        var forward = holder.FPSCamera != null ? holder.FPSCamera.Forward : holder.transform.forward;
        glow.transform.position = holder.Position + Vector3.up * 1.2f + forward * 1.2f;
        var build = Mathf.Clamp01(held / 1.8f);
        glow.Intensity = (1.5f + 2.5f * build) * (0.75f + 0.25f * Mathf.Sin(Time.time * 12f));
    }
}
