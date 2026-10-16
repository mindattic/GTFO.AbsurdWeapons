using System;
using System.Collections.Generic;
using Agents;
using Enemies;
using Gear;
using GTFO.API;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace GTFO.Weapons;

/// <summary>What a hammer's hit releases around the impact.</summary>
internal sealed record ImpactBurst(byte Id, uint WeaponId, bool ChargedOnly, bool SparesStruckEnemy,
    float Radius, float Damage, float EdgeDamageFraction, float StaggerMulti,
    Color FlashColor, float FlashRange, float FlashIntensity, float FlashSeconds)
{
    // Physical knockback (metres slid, see Knockback): the struck enemy along the swing (light /
    // charged hit), everyone else in the burst straight out from the impact. 0 = none.
    internal float KnockStruckLight { get; init; }
    internal float KnockStruckCharged { get; init; }
    internal float KnockArea { get; init; }

    // Mjolnir's thunderclap: charged hits only; shocks the enemies around the one it struck.
    internal static readonly ImpactBurst Thunderclap = new(1, MeleeInjector.MjolnirId, ChargedOnly: true, SparesStruckEnemy: true,
        Radius: 5f, Damage: 8f, EdgeDamageFraction: 0.4f, StaggerMulti: 3f,
        new Color(0.45f, 0.75f, 1f), FlashRange: 7f, FlashIntensity: 6f, FlashSeconds: 0.25f);

    // The Warhammer's shockwave: any hit that lands staggers everything around it, struck enemy
    // included. Barely any damage - stagger is damage x stagger multiplier, so 1 x 100 knocks
    // back anything short of the game's stagger-immune heavies.
    internal static readonly ImpactBurst Shockwave = new(2, MeleeInjector.WarhammerId, ChargedOnly: false, SparesStruckEnemy: false,
        Radius: 6f, Damage: 1f, EdgeDamageFraction: 1f, StaggerMulti: 100f,
        new Color(1f, 0.82f, 0.6f), FlashRange: 9f, FlashIntensity: 8f, FlashSeconds: 0.35f)
    {
        KnockStruckLight = 1.5f, KnockStruckCharged = 4f, KnockArea = 2f,
    };

    internal static ImpactBurst? ForWeapon(uint id) => id == Thunderclap.WeaponId ? Thunderclap : id == Shockwave.WeaponId ? Shockwave : null;
    internal static ImpactBurst? ForId(byte id) => id == Thunderclap.Id ? Thunderclap : id == Shockwave.Id ? Shockwave : null;
}

/// <summary>
/// Area effects released when a hammer's hit lands: Mjolnir's charged-hit thunderclap and the
/// Warhammer's shockwave (see <see cref="ImpactBurst"/>). Every enemy in range the impact can
/// reach (not through walls) takes the burst's damage and stagger through the enemy limb's
/// normal bullet path, so it syncs. The flash is sent to teammates so they see it too.
/// </summary>
[HarmonyPatch]
internal static class ImpactBursts
{
    private const string BurstEvent = "MindAttic.GTFO.Weapons.ImpactBurst";

    internal static void Init()
    {
        NetworkAPI.RegisterEvent<BurstMessage>(BurstEvent, (_, message) =>
        {
            var burst = ImpactBurst.ForId(message.Burst);
            if (burst != null) BurstFlash.Spawn(burst, new Vector3(message.X, message.Y, message.Z));
        });
    }

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.DoAttackDamage))]
    [HarmonyPostfix]
    private static void AfterAttackDamage(MeleeWeaponFirstPerson __instance, MeleeWeaponDamageData data, bool isPush)
    {
        try
        {
            if (isPush || data == null) return;
            var burst = ImpactBurst.ForWeapon(__instance.MeleeArchetypeData?.persistentID ?? 0);
            if (burst == null || (burst.ChargedOnly && !IsChargedAttack(__instance.CurrentStateName))) return;

            var owner = __instance.Owner;
            if (owner == null || !owner.IsLocallyOwned) return;

            var center = data.hitPos;
            var hitEnemy = data.damageGO != null ? data.damageGO.GetComponentInParent<EnemyAgent>() : null;
            var struck = burst.SparesStruckEnemy ? hitEnemy : null;
            var caught = AreaDamage.Apply(center, burst.Radius, burst.Damage, burst.EdgeDamageFraction, burst.StaggerMulti,
                owner.Cast<Agent>(), struck, data.gearCategoryId);

            if (burst.KnockArea > 0f)
            {
                foreach (var enemy in caught)
                    if (hitEnemy == null || enemy.Pointer != hitEnemy.Pointer)
                        Knockback.Push(enemy, enemy.Position - center, burst.KnockArea);
            }
            if (hitEnemy != null)
            {
                var distance = IsChargedAttack(__instance.CurrentStateName) ? burst.KnockStruckCharged : burst.KnockStruckLight;
                Knockback.Push(hitEnemy, hitEnemy.Position - owner.Position, distance);
            }

            BurstFlash.Spawn(burst, center);
            NetworkAPI.InvokeEvent(BurstEvent, new BurstMessage(burst.Id, center));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.Weapons impact burst failed: {e}");
        }
    }

    internal static bool IsChargedAttack(eMeleeWeaponState state) => state is
        eMeleeWeaponState.AttackChargeReleaseLeft or eMeleeWeaponState.AttackChargeReleaseRight or
        eMeleeWeaponState.AttackChargeHitLeft or eMeleeWeaponState.AttackChargeHitRight;

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct BurstMessage
    {
        public readonly byte Burst;
        public readonly float X, Y, Z;
        public BurstMessage(byte burst, Vector3 position) => (Burst, X, Y, Z) = (burst, position.x, position.y, position.z);
    }
}

/// <summary>
/// Damage and stagger to every living enemy within a radius that the center can see (not
/// through walls), once each through its limb nearest the center. Uses the bullet damage path,
/// so it syncs; call it only on the attacker's own game.
/// </summary>
internal static class AreaDamage
{
    internal static List<EnemyAgent> Apply(Vector3 center, float radius, float damage, float edgeDamageFraction, float staggerMulti,
        Agent source, EnemyAgent? spared, uint gearCategoryId)
    {
        var caught = new List<EnemyAgent>();
        var nearestLimb = new Dictionary<IntPtr, (Dam_EnemyDamageLimb Limb, Vector3 Point, float Distance)>();
        foreach (var collider in Physics.OverlapSphere(center, radius, LayerManager.MASK_ENEMY_DAMAGABLE))
        {
            var limb = collider.GetComponent<Dam_EnemyDamageLimb>();
            var enemy = limb?.m_base?.Owner;
            if (limb == null || enemy == null || !enemy.Alive) continue;
            if (spared != null && enemy.Pointer == spared.Pointer) continue;

            var point = collider.ClosestPoint(center);
            var distance = Vector3.Distance(center, point);
            if (nearestLimb.TryGetValue(enemy.Pointer, out var known) && known.Distance <= distance) continue;
            nearestLimb[enemy.Pointer] = (limb, point, distance);
        }

        // Lifted slightly off the floor so a low wall edge doesn't block the whole burst.
        var from = center + Vector3.up * 0.2f;
        foreach (var (limb, point, distance) in nearestLimb.Values)
        {
            if (Physics.Linecast(from, point, LayerManager.MASK_WORLD)) continue;

            var falloff = Mathf.Lerp(1f, edgeDamageFraction, distance / radius);
            var direction = (point - center).sqrMagnitude > 0.0001f ? (point - center).normalized : Vector3.up;
            limb.BulletDamage(damage * falloff, source, point, direction, -direction,
                allowDirectionalBonus: false, staggerMulti, 1f, gearCategoryId);
            if (limb.m_base?.Owner != null) caught.Add(limb.m_base.Owner);
        }
        return caught;
    }
}

/// <summary>A short flash of the game's own light type at an impact, fading out.</summary>
internal sealed class BurstFlash : MonoBehaviour
{
    private EffectLight light = null!;
    private float peak, duration, elapsed;

    public BurstFlash(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(ImpactBurst burst, Vector3 position) =>
        Spawn(position + Vector3.up * 0.5f, burst.FlashColor, burst.FlashRange, burst.FlashIntensity, burst.FlashSeconds);

    [HideFromIl2Cpp]
    internal static void Spawn(Vector3 position, Color color, float range, float intensity, float seconds)
    {
        var go = new GameObject("GTFO.Weapons Flash");
        go.transform.position = position;
        var flash = go.AddComponent<BurstFlash>();
        flash.peak = intensity;
        flash.duration = seconds;
        flash.light = go.AddComponent<EffectLight>();
        flash.light.Color = color;
        flash.light.Range = range;
        flash.light.Intensity = intensity;
        flash.light.enabled = true;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= duration)
        {
            Destroy(gameObject);
            return;
        }
        light.Intensity = peak * (1f - elapsed / duration);
    }
}
