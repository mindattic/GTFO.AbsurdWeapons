using System;
using Gear;
using HarmonyLib;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// Light-attack speed for the weapons that have their own (MeleeInjector.WithLightAttackTempo):
/// the swords step from fastest (Tanto) to slowest (Odachi), and the Shadow Dagger stabs 2.5x as
/// fast. The attack timings live in each weapon's copied animation set; this plays the first-person
/// animation at the same pace during light attacks and back at normal speed otherwise. Charge-ups
/// keep their timing, which the full-charge checks are measured against.
/// </summary>
[HarmonyPatch]
internal static class SwordTempo
{
    internal static float SpeedFor(uint id) => id switch
    {
        MeleeInjector.TantoId => 1.6f,
        MeleeInjector.WakizashiId => 1.25f,
        MeleeInjector.OdachiId => 0.85f,
        MeleeInjector.ShadowDaggerId => Stealth.StabSpeed,
        _ => 1f,
    };

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.ChangeState))]
    [HarmonyPostfix]
    private static void AfterChangeState(MeleeWeaponFirstPerson __instance, eMeleeWeaponState newState)
    {
        try
        {
            var speed = SpeedFor(__instance.MeleeArchetypeData?.persistentID ?? 0);
            if (speed == 1f) return;
            var animator = __instance.WeaponAnimator;
            if (animator == null) return;
            animator.speed = newState is eMeleeWeaponState.AttackMissLeft or eMeleeWeaponState.AttackMissRight
                or eMeleeWeaponState.AttackHitLeft or eMeleeWeaponState.AttackHitRight ? speed : 1f;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons sword tempo failed: {e.Message}");
        }
    }
}
