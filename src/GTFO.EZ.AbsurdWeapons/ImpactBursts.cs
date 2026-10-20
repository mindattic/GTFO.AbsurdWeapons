using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using Enemies;
using Gear;
using GTFO.API;
using MindAttic.GTFO.Shared;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

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

    // Damage per metre of knockback left unspent when a flung enemy hits a wall (see Knockback).
    internal float SlamDamagePerMeter { get; init; }

    // Mjolnir's thunderclap: every hit that lands shocks the enemies around the one it struck. (A
    // full charge also throws the hammer - MjolnirThrow.) Built inside Bind(), not as a static field
    // initializer: those would run (as part of this type's static constructor) the moment Bind()
    // itself is first called, before Bind()'s own body had assigned the config entries they read.
    internal static ImpactBurst Thunderclap = null!;
    internal static ImpactBurst Shockwave = null!;

    internal static void Bind(ConfigFile config)
    {
        var thunderclapRadius = config.Bind("Mjolnir", "ThunderclapRadius", 5f, "Metres the thunderclap reaches.");
        var thunderclapDamage = config.Bind("Mjolnir", "ThunderclapDamage", 8f, "Damage at the thunderclap's center.");
        var thunderclapEdgeFraction = config.Bind("Mjolnir", "ThunderclapEdgeFraction", 0.4f, "Share of damage left at the thunderclap's edge.");
        var thunderclapStagger = config.Bind("Mjolnir", "ThunderclapStagger", 3f, "Thunderclap stagger multiplier.");
        Thunderclap = new(1, MeleeInjector.MjolnirId, ChargedOnly: false, SparesStruckEnemy: true,
            Radius: thunderclapRadius.Value, Damage: thunderclapDamage.Value, EdgeDamageFraction: thunderclapEdgeFraction.Value, StaggerMulti: thunderclapStagger.Value,
            new Color(0.45f, 0.75f, 1f), FlashRange: 7f, FlashIntensity: 6f, FlashSeconds: 0.25f);

        var shockwaveRadius = config.Bind("Warhammer", "ShockwaveRadius", 6f, "Metres the shockwave reaches.");
        var shockwaveStagger = config.Bind("Warhammer", "ShockwaveStagger", 100f, "Shockwave stagger multiplier.");
        var shockwaveKnockStruckLight = config.Bind("Warhammer", "ShockwaveKnockStruckLight", 4f, "Metres the struck enemy slides on a light hit.");
        var shockwaveKnockStruckCharged = config.Bind("Warhammer", "ShockwaveKnockStruckCharged", 12f, "Metres the struck enemy slides on a charged hit.");
        var shockwaveKnockArea = config.Bind("Warhammer", "ShockwaveKnockArea", 6f, "Metres everyone else in the shockwave slides.");
        var shockwaveSlamDamagePerMeter = config.Bind("Warhammer", "ShockwaveSlamDamagePerMeter", 5f, "Wall-slam damage per metre of unspent slide.");
        // The Warhammer's shockwave: any hit that lands staggers everything around it, struck enemy
        // included. Barely any damage - stagger is damage x stagger multiplier, so 1 x 100 knocks
        // back anything short of the game's stagger-immune heavies.
        Shockwave = new(2, MeleeInjector.WarhammerId, ChargedOnly: false, SparesStruckEnemy: false,
            Radius: shockwaveRadius.Value, Damage: 1f, EdgeDamageFraction: 1f, StaggerMulti: shockwaveStagger.Value,
            new Color(1f, 0.82f, 0.6f), FlashRange: 9f, FlashIntensity: 8f, FlashSeconds: 0.35f)
        {
            KnockStruckLight = shockwaveKnockStruckLight.Value, KnockStruckCharged = shockwaveKnockStruckCharged.Value,
            KnockArea = shockwaveKnockArea.Value, SlamDamagePerMeter = shockwaveSlamDamagePerMeter.Value,
        };
    }

    // The burst a weapon's landed hit releases (not the release-triggered ones).
    internal static ImpactBurst? ForWeapon(uint id) => id == Thunderclap.WeaponId ? Thunderclap : id == Shockwave.WeaponId ? Shockwave : null;
    internal static ImpactBurst? ForId(byte id) => id switch
    {
        1 => Thunderclap,
        2 => Shockwave,
        _ => null,
    };
}

/// <summary>
/// Area effects released when a hammer's hit lands: Mjolnir's thunderclap and the
/// Warhammer's shockwave (see <see cref="ImpactBurst"/>). Every enemy in range the impact can
/// reach (not through walls) takes the burst's damage and stagger through the enemy limb's
/// normal bullet path, so it syncs. The flash is sent to teammates so they see it too.
/// </summary>
[HarmonyPatch]
internal static class ImpactBursts
{
    private const string BurstEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.ImpactBurst";
    private const float SwingSeconds = 0.3f; // hits closer together than this are the same swing
    private static float lastBurstTime = -10f;
    internal static float LastBurstTime => lastBurstTime; // WarhammerSlam: did the swing already burst?

    internal static void Init()
    {
        RemoteEvents.Register<BurstMessage>(BurstEvent, (_, message) =>
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

            // A cleaving swing calls this once per enemy it hits: only its first hit bursts; the
            // others just get their own shove.
            var firstHitOfSwing = Formulas.IsFirstHitOfSwing(Time.time, lastBurstTime, SwingSeconds);
            if (!firstHitOfSwing)
            {
                if (hitEnemy != null && burst.KnockStruckLight > 0f)
                {
                    var shove = IsChargedAttack(__instance.CurrentStateName) ? burst.KnockStruckCharged : burst.KnockStruckLight;
                    Knockback.Push(hitEnemy, hitEnemy.Position - owner.Position, shove, burst.SlamDamagePerMeter, owner, data.gearCategoryId);
                }
                return;
            }
            lastBurstTime = Time.time;
            var struck = burst.SparesStruckEnemy ? hitEnemy : null;
            var caught = AreaDamage.Apply(center, burst.Radius, burst.Damage, burst.EdgeDamageFraction, burst.StaggerMulti,
                owner.Cast<Agent>(), struck, data.gearCategoryId);

            if (burst.KnockArea > 0f)
            {
                foreach (var enemy in caught)
                    if (hitEnemy == null || enemy.Pointer != hitEnemy.Pointer)
                        Knockback.Push(enemy, enemy.Position - center, burst.KnockArea, burst.SlamDamagePerMeter, owner, data.gearCategoryId);
            }
            if (hitEnemy != null)
            {
                var distance = IsChargedAttack(__instance.CurrentStateName) ? burst.KnockStruckCharged : burst.KnockStruckLight;
                Knockback.Push(hitEnemy, hitEnemy.Position - owner.Position, distance, burst.SlamDamagePerMeter, owner, data.gearCategoryId);
            }

            BurstFlash.Spawn(burst, center);
            NetworkAPI.InvokeEvent(BurstEvent, new BurstMessage(burst.Id, center));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons impact burst failed: {e}");
        }
    }

    // A burst set off by the weapon itself (the Warhammer's slam, WarhammerSlam) rather than by a hit:
    // damage and stagger around `center`, everyone caught thrown out from it, and the flash.
    // It counts as its swing's burst, so a hit from the same swing landing just after it only
    // shoves the struck enemy instead of setting off a second shockwave.
    internal static void Release(ImpactBurst burst, PlayerAgent owner, Vector3 center, uint gearCategoryId)
    {
        lastBurstTime = Time.time;
        var caught = AreaDamage.Apply(center, burst.Radius, burst.Damage, burst.EdgeDamageFraction, burst.StaggerMulti,
            owner.Cast<Agent>(), null, gearCategoryId);
        if (burst.KnockArea > 0f)
            foreach (var enemy in caught)
                Knockback.Push(enemy, enemy.Position - center, burst.KnockArea, burst.SlamDamagePerMeter, owner, gearCategoryId);
        BurstFlash.Spawn(burst, center);
        NetworkAPI.InvokeEvent(BurstEvent, new BurstMessage(burst.Id, center));
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

            var falloff = Formulas.AreaFalloff(distance, radius, edgeDamageFraction);
            var direction = (point - center).sqrMagnitude > 0.0001f ? (point - center).normalized : Vector3.up;
            limb.BulletDamage(damage * falloff, source, point, direction, -direction,
                allowDirectionalBonus: false, staggerMulti, 1f, gearCategoryId);
            if (limb.m_base?.Owner != null) caught.Add(limb.m_base.Owner);
        }
        LockDamage.Radius(center, radius, damage, source);
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
    internal static void Spawn(ImpactBurst burst, Vector3 position)
    {
        Spawn(position + Vector3.up * 0.5f, burst.FlashColor, burst.FlashRange, burst.FlashIntensity, burst.FlashSeconds);
        if (burst == ImpactBurst.Thunderclap) Sounds.Play(Sounds.Thunderclap.Value, position); // every game shows the burst, so every game hears it
    }

    [HideFromIl2Cpp]
    internal static void Spawn(Vector3 position, Color color, float range, float intensity, float seconds)
    {
        var go = new GameObject("GTFO.EZ.AbsurdWeapons Flash");
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
