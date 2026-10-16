using System;
using System.Collections.Generic;
using Enemies;
using Gear;
using GTFO.API;
using HarmonyLib;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>How one kind of seeker looks, flies and hits.</summary>
internal sealed record SeekerKind(byte Id, ProjectileType Projectile)
{
    internal float Speed { get; init; }
    // False: flies dead straight where it was fired, never looking for a target.
    internal bool Homing { get; init; } = true;
    // Smart lock (Titanfall's Smart Pistol): each shot locks, as it's fired, onto one of the
    // enemies nearest the crosshair - cycling through up to MaxLocks of them - and curves onto it.
    internal bool SmartLock { get; init; }
    internal float LockConeDegrees { get; init; }
    internal float LockRange { get; init; }
    internal int MaxLocks { get; init; }
    // How far along the aim the curve is pulled before bending onto the target (share of distance).
    internal float CurveBend { get; init; } = 0.4f;
    internal float TurnDegreesPerSecond { get; init; }
    internal float Lifetime { get; init; }
    internal float SearchRange { get; init; }
    internal float SearchConeDegrees { get; init; }
    // Flies straight (with its launch spread) this long before homing: the fan-out.
    internal float LaunchSeconds { get; init; }
    // Random cone each seeker leaves the muzzle in, on top of the gun's own spread.
    internal float SpreadDegrees { get; init; }
    // Side-to-side weave while searching, for a living-swarm look (off once locked on).
    internal float WobbleDegrees { get; init; }
    internal float WobbleHz { get; init; }
    // Area blast on impact (0 radius = none) and the flash that marks it.
    internal float BlastRadius { get; init; }
    internal float BlastDamage { get; init; }
    internal float BlastStagger { get; init; }
    internal Color FlashColor { get; init; }
    internal float FlashRange { get; init; }
    // On hitting an enemy, this many of this kind burst out to hunt other enemies.
    internal SeekerKind? SplitInto { get; init; }
    internal int SplitCount { get; init; }
    // Damage for seekers that aren't fired by a gun (split shards); guns use the archetype's.
    internal float OwnDamage { get; init; }
    // Doom-style rolled damage: DamageScale x 1d(DamageDie) per hit instead of a fixed amount
    // (0 = off). The archetype's damage is then just the average the loadout screen shows.
    internal int DamageDie { get; init; }
    internal float DamageScale { get; init; }
    // Recolors the borrowed projectile's glow, trail and body (alpha 0 = keep its own colors).
    internal Color Tint { get; init; }

    // Turn rates are 1000% faster (11x) than the first tuning: once locked on, a seeker turns
    // almost instantly, so nearly every one connects.
    private const float TurnBoost = 11f;

    private static readonly Color ElectricBlue = new(0.45f, 0.75f, 1f); // Mjolnir's thunderclap

    // Split shards: quick, short-lived, a smaller shock, no further splits. (Nothing splits into
    // them right now; kept for a weapon that wants them.)
    internal static readonly SeekerKind Shard = new(3, ProjectileType.SemiTargetingQuick)
    {
        Speed = 30f, TurnDegreesPerSecond = 420f * TurnBoost, Lifetime = 2.5f, SearchRange = 20f, SearchConeDegrees = 180f,
        LaunchSeconds = 0.08f, SpreadDegrees = 70f, OwnDamage = 8f,
        BlastRadius = 1.5f, BlastDamage = 4f, BlastStagger = 3f,
        FlashColor = ElectricBlue, FlashRange = 3f,
    };

    // Plasma Gun, after Doom's (1993): blue plasma balls that fly dead straight at 25 map units a
    // tic (~28.6 m/s, scaling Doom's 56-unit-tall player to 1.83 m) for 5 x 1d8 damage each. No
    // homing, no recoil, no splitting. The one addition is a small electric splash on impact like
    // Mjolnir's (blue flash, falloff to 40%, x3 stagger) - Doom 2016's plasma rifle splashes too.
    internal static readonly SeekerKind Plasma = new(1, ProjectileType.TargetingSmall)
    {
        Speed = 28.6f, Homing = false, Lifetime = 6f,
        DamageDie = 8, DamageScale = 5f,
        Tint = new Color(0.3f, 0.55f, 1f),
        BlastRadius = 1.25f, BlastDamage = 6f, BlastStagger = 3f,
        FlashColor = ElectricBlue, FlashRange = 2f,
    };

    // Hive Pistol: the Hybrid's swarm projectile fired like Titanfall's Smart Pistol. Each round
    // leaves the barrel along your aim and bends smoothly onto one of up to six enemies locked
    // near the crosshair, taking turns between them. A round fired with nothing locked - or whose
    // target dies mid-flight - hunts down the nearest enemy instead.
    internal static readonly SeekerKind Hive = new(2, ProjectileType.SemiTargetingQuick)
    {
        Speed = 45f, Lifetime = 3f,
        TurnDegreesPerSecond = 1500f, SearchRange = 50f, SearchConeDegrees = 120f,
        SmartLock = true, LockConeDegrees = 35f, LockRange = 60f, MaxLocks = 6, CurveBend = 0.4f,
        FlashColor = new Color(1f, 0.55f, 0.25f), FlashRange = 3f,
    };

    internal static SeekerKind? ForGun(uint id) => id switch
    {
        GunInjector.PlasmaPistolId or GunInjector.PlasmaRifleId => Plasma,
        GunInjector.HivePistolId or GunInjector.HiveRifleId => Hive,
        _ => null,
    };
    internal static SeekerKind? ForId(byte id) => id == Plasma.Id ? Plasma : id == Hive.Id ? Hive : id == Shard.Id ? Shard : null;
}

/// <summary>
/// Turns the Plasma Gun's and Hive Pistol's bullets into projectiles. Each gun's hitscan ray (one
/// per pellet) is swallowed and replaced by a <see cref="SeekerProjectile"/>, so ammo, recoil,
/// muzzle flash and sound stay vanilla. Smart-lock kinds leave from the gun's muzzle and curve
/// onto an enemy locked near the crosshair. The shooter's own game flies the real one and deals
/// its damage (through the normal bullet path, which syncs); everyone else is sent a message and
/// flies a harmless copy so they see the same shots.
/// </summary>
[HarmonyPatch]
internal static class SeekerGuns
{
    private const string ShotEvent = "MindAttic.GTFO.AbsurdWeapons.SeekerShot";

    // The archetype (and weapon) whose Fire() is running right now, if it's one of ours.
    private static uint firingArchetype;
    private static BulletWeapon? firingWeapon;

    // Smart lock: the enemy the last smart round went to, so the next takes the one after it.
    private static IntPtr lastLock;

    internal static void Init()
    {
        NetworkAPI.RegisterEvent<SeekerShot>(ShotEvent, OnRemoteShot);
    }

    // Shotgun overrides Fire, so both are patched. Each prefix saves the outer value and its
    // finalizer restores it - even if Fire throws - which stays right if Shotgun.Fire calls the base Fire.
    [HarmonyPatch(typeof(BulletWeapon), nameof(BulletWeapon.Fire))]
    [HarmonyPrefix]
    private static void BeforeBulletFire(BulletWeapon __instance, out uint __state) => BeginFire(__instance, out __state);

    [HarmonyPatch(typeof(BulletWeapon), nameof(BulletWeapon.Fire))]
    [HarmonyFinalizer]
    private static void AfterBulletFire(BulletWeapon __instance, uint __state) => EndFire(__instance, __state);

    [HarmonyPatch(typeof(Shotgun), nameof(Shotgun.Fire))]
    [HarmonyPrefix]
    private static void BeforeShotgunFire(Shotgun __instance, out uint __state) => BeginFire(__instance, out __state);

    [HarmonyPatch(typeof(Shotgun), nameof(Shotgun.Fire))]
    [HarmonyFinalizer]
    private static void AfterShotgunFire(Shotgun __instance, uint __state) => EndFire(__instance, __state);

    // A teammate's gun on this screen is a synced copy whose Fire overrides the base one; patched
    // the same way so its rays are swallowed and its tracer muted like the shooter's own.
    [HarmonyPatch(typeof(BulletWeaponSynced), nameof(BulletWeaponSynced.Fire))]
    [HarmonyPrefix]
    private static void BeforeSyncedFire(BulletWeaponSynced __instance, out uint __state) => BeginFire(__instance, out __state);

    [HarmonyPatch(typeof(BulletWeaponSynced), nameof(BulletWeaponSynced.Fire))]
    [HarmonyFinalizer]
    private static void AfterSyncedFire(BulletWeaponSynced __instance, uint __state) => EndFire(__instance, __state);

    [HarmonyPatch(typeof(ShotgunSynced), nameof(ShotgunSynced.Fire))]
    [HarmonyPrefix]
    private static void BeforeSyncedShotgunFire(ShotgunSynced __instance, out uint __state) => BeginFire(__instance, out __state);

    [HarmonyPatch(typeof(ShotgunSynced), nameof(ShotgunSynced.Fire))]
    [HarmonyFinalizer]
    private static void AfterSyncedShotgunFire(ShotgunSynced __instance, uint __state) => EndFire(__instance, __state);

    private static void BeginFire(BulletWeapon weapon, out uint previous)
    {
        previous = firingArchetype;
        firingArchetype = weapon.ArchetypeData?.persistentID ?? 0;
        firingWeapon = weapon;
        if (LaserGuns.ForGun(firingArchetype) != null || firingArchetype is GunInjector.RailgunId or GunInjector.ChainLightningId)
            LaserGuns.MuteTracer(weapon);
        if (TentacleCannon.IsTentacleGun(firingArchetype) && TentacleCannon.DiagnosticShots > 0)
            Plugin.Logger.LogInfo("GTFO.AbsurdWeapons Tentacle Rifle: Fire() called.");
        if (firingArchetype == GunInjector.FlakCannonId) FlakCannon.ResetShot();
    }

    private static void EndFire(BulletWeapon weapon, uint previous)
    {
        firingArchetype = previous;
        if (previous == 0) firingWeapon = null;

        // Infinite ammo: our guns' clips are topped back up after every shot, so they never run
        // dry, never reload and never draw on the ammo reserve.
        try
        {
            if (!GunInjector.IsOurs(weapon.ArchetypeData?.persistentID ?? 0)) return;
            var owner = weapon.Owner;
            if (owner == null || !owner.IsLocallyOwned) return;
            if (weapon.GetCurrentClip() < weapon.ClipSize) weapon.SetCurrentClip(weapon.ClipSize);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons couldn't refill a clip: {e.Message}");
        }
    }

    [HarmonyPatch(typeof(Weapon), nameof(Weapon.CastWeaponRay),
        new[] { typeof(Transform), typeof(Weapon.WeaponHitData), typeof(Vector3), typeof(int) },
        new[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal })]
    [HarmonyPrefix]
    private static bool BeforeWeaponRay(Transform alignTransform, ref Weapon.WeaponHitData weaponRayData, Vector3 originPos, ref bool __result)
    {
        // Once one of our guns has taken the shot (it may already have dealt damage), an error
        // must not let the vanilla bullet through as well.
        var ours = false;
        try
        {
            var laser = LaserGuns.ForGun(firingArchetype) ?? LaserGuns.ForGun(weaponRayData.gearCategoryId);
            if (laser != null)
            {
                ours = true;
                weaponRayData.maxRayDist = 0f;
                __result = false;
                var shooter = weaponRayData.owner ?? firingWeapon?.Owner;
                if (shooter != null && shooter.IsLocallyOwned)
                {
                    var aim = weaponRayData.fireDir.sqrMagnitude > 0.01f ? weaponRayData.fireDir.normalized : alignTransform.forward;
                    LaserGuns.Tick(laser, shooter, firingWeapon?.MuzzleAlign, originPos, aim, weaponRayData.damage,
                        weaponRayData.staggerMulti, weaponRayData.precisionMulti, weaponRayData.gearCategoryId);
                }
                return false;
            }

            if (firingArchetype == GunInjector.ChainLightningId || weaponRayData.gearCategoryId == GunInjector.ChainLightningId)
            {
                ours = true;
                weaponRayData.maxRayDist = 0f;
                __result = false;
                var shooter = weaponRayData.owner ?? firingWeapon?.Owner;
                if (shooter != null && shooter.IsLocallyOwned)
                {
                    var aim = weaponRayData.fireDir.sqrMagnitude > 0.01f ? weaponRayData.fireDir.normalized : alignTransform.forward;
                    ChainLightning.Fire(shooter, originPos, aim, weaponRayData.damage, weaponRayData.gearCategoryId);
                }
                return false;
            }

            if (firingArchetype == GunInjector.RailgunId || weaponRayData.gearCategoryId == GunInjector.RailgunId)
            {
                ours = true;
                weaponRayData.maxRayDist = 0f;
                __result = false;
                var shooter = weaponRayData.owner ?? firingWeapon?.Owner;
                if (shooter != null && shooter.IsLocallyOwned)
                {
                    var aim = weaponRayData.fireDir.sqrMagnitude > 0.01f ? weaponRayData.fireDir.normalized : alignTransform.forward;
                    Railgun.Fire(shooter, originPos, aim, weaponRayData.damage, weaponRayData.staggerMulti,
                        weaponRayData.precisionMulti, weaponRayData.gearCategoryId);
                }
                return false;
            }

            if (firingArchetype == GunInjector.FlamethrowerId || weaponRayData.gearCategoryId == GunInjector.FlamethrowerId)
            {
                ours = true;
                weaponRayData.maxRayDist = 0f;
                __result = false;
                var shooter = weaponRayData.owner ?? firingWeapon?.Owner;
                if (shooter != null && shooter.IsLocallyOwned)
                {
                    var aim = weaponRayData.fireDir.sqrMagnitude > 0.01f ? weaponRayData.fireDir.normalized : alignTransform.forward;
                    FlameThrower.Tick(shooter, originPos, aim, weaponRayData.damage, weaponRayData.gearCategoryId);
                }
                return false;
            }

            if (firingArchetype == GunInjector.FlakCannonId || weaponRayData.gearCategoryId == GunInjector.FlakCannonId)
            {
                ours = true;
                weaponRayData.maxRayDist = 0f;
                __result = false;
                // Shotgun pellets come without an owner in the hit data: fall back to the gun's.
                var shooter = weaponRayData.owner ?? firingWeapon?.Owner;
                if (shooter != null && shooter.IsLocallyOwned)
                {
                    FlakCannon.FirePellet(shooter, firingWeapon?.MuzzleAlign, originPos, PelletDirection(alignTransform, weaponRayData),
                        weaponRayData.damage, weaponRayData.gearCategoryId);
                }
                return false;
            }

            if (TentacleCannon.IsTentacleGun(firingArchetype) || TentacleCannon.IsTentacleGun(weaponRayData.gearCategoryId))
            {
                ours = true;
                if (TentacleCannon.DiagnosticShots > 0)
                    Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons Tentacle Rifle ray: archetype {firingArchetype}, category {weaponRayData.gearCategoryId}, owner {(weaponRayData.owner != null ? "set" : "null")}.");
                weaponRayData.maxRayDist = 0f;
                __result = false;
                // Shotgun pellets come without an owner in the hit data: fall back to the gun's.
                var shooter = weaponRayData.owner ?? firingWeapon?.Owner;
                if (shooter != null && shooter.IsLocallyOwned)
                {
                    // One tentacle per pellet, along that pellet's spread direction.
                    TentacleCannon.FirePellet(shooter, firingWeapon?.MuzzleAlign, originPos, PelletDirection(alignTransform, weaponRayData),
                        weaponRayData.damage, weaponRayData.gearCategoryId);
                }
                return false;
            }

            var kind = SeekerKind.ForGun(firingArchetype) ?? SeekerKind.ForGun(weaponRayData.gearCategoryId);
            if (kind == null) return true;
            ours = true;

            // No hitscan bullet for these guns, whoever is holding one.
            weaponRayData.maxRayDist = 0f;
            __result = false;

            // Shotgun pellets come without an owner in the hit data: fall back to the gun's.
            var owner = weaponRayData.owner ?? firingWeapon?.Owner;
            if (owner == null || !owner.IsLocallyOwned) return false;

            var direction = PelletDirection(alignTransform, weaponRayData);
            var position = originPos + direction * 0.5f; // clear of the shooter's own body
            EnemyAgent? locked = null;
            if (kind.SmartLock)
            {
                // Out of the barrel, not the eye; the lock is picked from where the crosshair is.
                var muzzle = firingWeapon?.MuzzleAlign;
                if (muzzle != null) position = muzzle.position + direction * 0.1f;
                locked = NextLock(kind, originPos, direction);
            }

            SeekerProjectile.Spawn(kind, position, direction, owner, weaponRayData.damage,
                weaponRayData.staggerMulti, weaponRayData.precisionMulti, weaponRayData.gearCategoryId,
                lockTarget: locked);
            NetworkAPI.InvokeEvent(ShotEvent, new SeekerShot(kind.Id, position, direction,
                locked != null ? locked.AimTarget.position : null));
            return false;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons shot failed: {e}");
            if (!ours) return true;
            weaponRayData.maxRayDist = 0f;
            __result = false;
            return false;
        }
    }

    // Titanfall-style lock: the living, visible enemies within the cone around the crosshair,
    // nearest the crosshair first, up to MaxLocks; successive shots take turns between them.
    private static EnemyAgent? NextLock(SeekerKind kind, Vector3 eye, Vector3 aim)
    {
        var locks = FindLocks(eye, aim, kind.LockConeDegrees, kind.LockRange);
        if (locks.Count == 0) return null;

        // The one after the enemy last fired at (nearest the crosshair first if that one's gone).
        var count = Math.Min(locks.Count, kind.MaxLocks);
        var last = locks.FindIndex(0, count, enemy => enemy.Pointer == lastLock);
        var pick = locks[(last + 1) % count];
        lastLock = pick.Pointer;
        return pick;
    }

    // The living enemies the eye can see within `cone` degrees of the aim and `range` metres,
    // nearest the crosshair first.
    internal static List<EnemyAgent> FindLocks(Vector3 eye, Vector3 aim, float cone, float range)
    {
        var locks = new List<(EnemyAgent Enemy, float Angle)>();
        var seen = new HashSet<IntPtr>();
        foreach (var collider in Physics.OverlapSphere(eye, range, LayerManager.MASK_ENEMY_DAMAGABLE))
        {
            var enemy = collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner;
            if (enemy == null || !enemy.Alive || !seen.Add(enemy.Pointer)) continue;

            var chest = enemy.AimTarget.position;
            var angle = Vector3.Angle(aim, chest - eye);
            if (angle > cone) continue;
            if (Physics.Linecast(eye, chest, LayerManager.MASK_WORLD)) continue;
            locks.Add((enemy, angle));
        }
        locks.Sort((a, b) => a.Angle.CompareTo(b.Angle));
        return locks.ConvertAll(l => l.Enemy);
    }

    // The ray's aim with the pellet's spread applied - the parts CastWeaponRay would have added.
    private static Vector3 PelletDirection(Transform align, Weapon.WeaponHitData data)
    {
        var forward = data.fireDir.sqrMagnitude > 0.01f ? data.fireDir.normalized : align.forward;
        var right = align != null ? align.right : Vector3.Cross(Vector3.up, forward).normalized;
        var up = Vector3.Cross(forward, right).normalized;

        var spread = UnityEngine.Random.insideUnitCircle * data.randomSpread;
        var yaw = data.angOffsetX + spread.x;
        var pitch = data.angOffsetY + spread.y;
        return (Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(-pitch, right) * forward).normalized;
    }

    private static void OnRemoteShot(ulong sender, SeekerShot shot)
    {
        try
        {
            var kind = SeekerKind.ForId(shot.Kind);
            if (kind == null) return;
            Vector3? lockPoint = shot.Locked != 0 ? new Vector3(shot.LockX, shot.LockY, shot.LockZ) : null;
            SeekerProjectile.Spawn(kind, new Vector3(shot.X, shot.Y, shot.Z), new Vector3(shot.DirX, shot.DirY, shot.DirZ),
                owner: null, damage: 0f, staggerMulti: 0f, precisionMulti: 0f, gearCategoryId: 0, lockPoint: lockPoint);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons couldn't show a teammate's seeker: {e}");
        }
    }

    // Plain floats rather than Unity vectors so GTFO-API can copy it as raw bytes.
    private readonly struct SeekerShot
    {
        public readonly byte Kind;
        public readonly byte Locked; // 1 when the shot curves onto Lock*: where the target was
        public readonly float X, Y, Z, DirX, DirY, DirZ, LockX, LockY, LockZ;

        public SeekerShot(byte kind, Vector3 position, Vector3 direction, Vector3? lockPoint)
        {
            Kind = kind;
            (X, Y, Z) = (position.x, position.y, position.z);
            (DirX, DirY, DirZ) = (direction.x, direction.y, direction.z);
            Locked = lockPoint.HasValue ? (byte)1 : (byte)0;
            var point = lockPoint ?? Vector3.zero;
            (LockX, LockY, LockZ) = (point.x, point.y, point.z);
        }
    }
}
