using System;
using Gear;
using HarmonyLib;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// Light-attack speed for the weapons that have their own (MeleeInjector.WithLightAttackTempo):
/// the Shadow Dagger stabs 2.5x as fast. The attack timings live in each weapon's copied animation set; this plays the first-person
/// animation at the same pace during light attacks and back at normal speed otherwise. Charge-ups
/// keep their timing, which the full-charge checks are measured against.
/// </summary>
[HarmonyPatch]
internal static class SwordTempo
{
    internal static float SpeedFor(uint id) => id switch
    {
        MeleeInjector.ShadowDaggerId => WeaponSettings.Or(WeaponSettings.ShadowDagger.LightAttackSpeed, Stealth.StabSpeed),
        _ => 1f,
    };

    // The charged swing's pace (its release and hit; MeleeInjector.WithChargedSwingTempo): the
    // Katana's end-of-dash cut is twice as fast.
    internal static float ChargedSpeedFor(uint id) => id == MeleeInjector.KatanaId ? 2f : 1f;

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.ChangeState))]
    [HarmonyPostfix]
    private static void AfterChangeState(MeleeWeaponFirstPerson __instance, eMeleeWeaponState newState)
    {
        try
        {
            var id = __instance.MeleeArchetypeData?.persistentID ?? 0;
            var light = SpeedFor(id);
            var charged = ChargedSpeedFor(id);
            if (light == 1f && charged == 1f) return;
            var animator = __instance.WeaponAnimator;
            if (animator == null) return;
            animator.speed = newState switch
            {
                eMeleeWeaponState.AttackMissLeft or eMeleeWeaponState.AttackMissRight
                    or eMeleeWeaponState.AttackHitLeft or eMeleeWeaponState.AttackHitRight => light,
                eMeleeWeaponState.AttackChargeReleaseLeft or eMeleeWeaponState.AttackChargeReleaseRight
                    or eMeleeWeaponState.AttackChargeHitLeft or eMeleeWeaponState.AttackChargeHitRight => charged,
                _ => 1f,
            };
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons sword tempo failed: {e.Message}");
        }
    }
}
