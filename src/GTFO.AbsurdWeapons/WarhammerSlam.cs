using System;
using BepInEx.Configuration;
using FX_EffectSystem;
using GameData;
using Gear;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Warhammer's slam: a fully charged swing always comes down on the ground in front of you at
/// the moment it would land - the shockwave (stagger and knockback all around, see
/// <see cref="ImpactBurst.Shockwave"/>), a burst of dust and the flash - whether or not it hits an
/// enemy. When it does hit one, that hit's own shockwave is the slam (no second one).
/// </summary>
[HarmonyPatch]
internal sealed class WarhammerSlam : MonoBehaviour
{
    internal static ConfigEntry<float> HitDelay = null!;
    internal static ConfigEntry<float> Reach = null!;
    private const float FullChargeLeeway = 0.95f;

    [HideFromIl2Cpp]
    internal static void Bind(ConfigFile config)
    {
        HitDelay = config.Bind("Warhammer", "SlamDelaySeconds", 0.25f, "Seconds after a full charge is released that the hammer hits the ground (the swing's own hit moment).");
        Reach = config.Bind("Warhammer", "SlamReach", 1.8f, "Metres in front of you the slam lands.");
    }

    private static float chargeStarted = -1f;
    private static float slamAt = -1f, releasedAt;
    private static PlayerAgent? owner;
    private static uint gearCategoryId;

    public WarhammerSlam(IntPtr ptr) : base(ptr) { }

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.ChangeState))]
    [HarmonyPostfix]
    [HideFromIl2Cpp]
    private static void AfterChangeState(MeleeWeaponFirstPerson __instance, eMeleeWeaponState newState)
    {
        try
        {
            var archetype = __instance.MeleeArchetypeData;
            if (archetype == null || archetype.persistentID != MeleeInjector.WarhammerId) return;
            var player = __instance.Owner;
            if (player == null || !player.IsLocallyOwned) return;

            if (newState is eMeleeWeaponState.AttackChargeUpLeft or eMeleeWeaponState.AttackChargeUpRight)
            {
                chargeStarted = Time.time;
                return;
            }
            if (newState is not (eMeleeWeaponState.AttackChargeReleaseLeft or eMeleeWeaponState.AttackChargeReleaseRight)) return;
            if (chargeStarted < 0f) return;
            var held = Time.time - chargeStarted;
            chargeStarted = -1f;

            var animations = GameDataBlockBase<MeleeAnimationSetDataBlock>.GetBlock(archetype.MeleeAnimationSet);
            var full = animations != null ? animations.MaxDamageChargeTime : 1.8f;
            if (!Formulas.IsFullCharge(held, full, FullChargeLeeway)) return;

            owner = player;
            gearCategoryId = archetype.persistentID;
            releasedAt = Time.time;
            slamAt = Time.time + HitDelay.Value;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Warhammer slam failed: {e}");
        }
    }

    private void Update()
    {
        if (slamAt < 0f || Time.time < slamAt) return;
        slamAt = -1f;
        var player = owner;
        owner = null;
        try
        {
            if (player == null || !player.Alive) return;
            // The swing already landed on an enemy since the release: its shockwave was the slam.
            if (ImpactBursts.LastBurstTime >= releasedAt) return;

            var forward = player.FPSCamera != null ? player.FPSCamera.Forward : player.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : player.transform.forward;
            var from = player.Position + Vector3.up * 1.2f;
            var ahead = from + forward * Reach.Value;
            // Short of a wall in the way, then down to the floor.
            if (Physics.Raycast(from, forward, out var wall, Reach.Value, LayerManager.MASK_WORLD)) ahead = wall.point - forward * 0.3f;
            var ground = Physics.Raycast(ahead, Vector3.down, out var floor, 4f, LayerManager.MASK_WORLD) ? floor.point : player.Position + forward * Reach.Value;

            ImpactBursts.Release(ImpactBurst.Shockwave, player, ground, gearCategoryId);
            try
            {
                FX_Manager.PlayEffect(false, FX_GroupName.Impact_Concrete, ground + Vector3.up * 0.05f, Quaternion.LookRotation(Vector3.up), true);
            }
            catch (Exception)
            {
                // The dust is a nicety.
            }
            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons Warhammer: slammed the ground at {ground}.");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Warhammer slam failed: {e.Message}");
        }
    }
}
