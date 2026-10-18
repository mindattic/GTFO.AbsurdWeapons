using System;
using Gear;
using HarmonyLib;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// Light- and charged-attack speed for melee weapons with their own tempo (MeleeInjector.WithTempo;
/// built in: the Shadow Dagger stabs 2.5x as fast, the Katana's charged cut is 2x). The attack
/// timings live in each weapon's copied animation set; this plays the first-person animation at the
/// same pace during those attacks and back at normal speed otherwise. Charge-ups keep their timing,
/// which the full-charge checks are measured against.
/// </summary>
[HarmonyPatch]
internal static class SwordTempo
{
    internal static float SpeedFor(uint id) =>
        WeaponSettings.ForCategory(id) is { } s ? WeaponSettings.Or(s.LightAttackSpeed, s.BuiltInLightAttackSpeed) : 1f;

    // The charged swing's pace (its release and hit; MeleeInjector.WithTempo).
    internal static float ChargedSpeedFor(uint id) =>
        WeaponSettings.ForCategory(id) is { } s ? WeaponSettings.Or(s.ChargedAttackSpeed, s.BuiltInChargedAttackSpeed) : 1f;

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
