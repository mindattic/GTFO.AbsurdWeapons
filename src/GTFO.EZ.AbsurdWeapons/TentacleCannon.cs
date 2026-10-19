using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using Enemies;
using GTFO.API;
using MindAttic.GTFO.Shared;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Player;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// The Tentacle Cannon: a full-auto gun whose every round is a burst of strikers' tentacles - the
/// game's own, drawn by its GPU tentacle renderer. They come from twelve fixed spots just below the
/// player's eyeline (an arc under the bottom of the screen), each round's tentacles taking free
/// spots in random order; a spot isn't used again until the tentacle on it has retracted. With an
/// enemy in reach near the crosshair every tentacle goes for that one enemy (each at its own spot
/// on it) and bites - damage and a heavy stagger through the limb's normal bullet path, so it
/// syncs. With none, each reaches out along its own random line for whatever it meets - walls,
/// the floor, crates (and breaks a door lock it grabs). Teammates are sent each lash and see the
/// same thing; only the shooter's game deals damage.
/// </summary>
internal static class TentacleCannon
{
    // The gun that fires tentacles: the Tentacle Cannon.
    internal static bool IsTentacleGun(uint id) => id == GunInjector.TentacleCannonId;

    private static ConfigEntry<float> EngageConeDegrees = null!; // the enemy nearest the crosshair within this cone (and in reach) draws every tentacle
    private static ConfigEntry<float> GrabConeDegrees = null!;   // with no enemy, each tentacle's own random line within this cone of the aim
    private static ConfigEntry<int> TentaclesPerShot = null!;
    private static ConfigEntry<float> IdleReach = null!;   // how far out a tentacle wriggles when its line meets nothing
    internal static ConfigEntry<float> StaggerMulti = null!;

    // How far a tentacle reaches - the strikers' own (EAB_MovingEnemeyTentacle's
    // m_absoluteMaxDistanceToHitTarget), read off the first striker found in the level. Lock range
    // and a grab's reach are both this, so a lash is never drawn longer than a real one.
    private static ConfigEntry<float> FallbackReach = null!; // until a striker has been found

    internal static void Bind(ConfigFile config)
    {
        EngageConeDegrees = config.Bind("Tentacle Guns", "EngageConeDegrees", 20f, "The enemy nearest the crosshair within this cone (and in reach) draws every tentacle.");
        GrabConeDegrees = config.Bind("Tentacle Guns", "GrabConeDegrees", 25f, "With no enemy in reach, each tentacle reaches along its own random line within this many degrees of the aim.");
        TentaclesPerShot = config.Bind("Tentacle Guns", "TentaclesPerShot", 3, "Tentacles each round sends out (each needs a free one of the twelve spawn spots).");
        StaggerMulti = config.Bind("Tentacle Guns", "StaggerMulti", 12f, "Stagger multiplier per bite.");
        IdleReach = config.Bind("Tentacle Guns", "IdleReach", 4f, "Metres a tentacle wriggles out when its line meets nothing in reach.");
        FallbackReach = config.Bind("Tentacle Guns", "FallbackReach", 25f, "Reach used until a real striker's is found in the level.");
    }
    private static float reach;
    private static float nextReachLookup;

    internal static float Reach
    {
        get
        {
            if (reach <= 0f && Time.time >= nextReachLookup) LookUpReach();
            return reach > 0f ? reach : FallbackReach.Value;
        }
    }

    private static void LookUpReach()
    {
        nextReachLookup = Time.time + 5f;
        try
        {
            var longest = 0f;
            foreach (var found in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<EAB_MovingEnemeyTentacle>()))
            {
                var ability = found.TryCast<EAB_MovingEnemeyTentacle>();
                if (ability != null) longest = Mathf.Max(longest, ability.m_absoluteMaxDistanceToHitTarget);
            }
            if (longest <= 0f) return;
            reach = longest;
            Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons: tentacle reach is {reach:0.##} m (the strikers' own).");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons couldn't read the strikers' tentacle reach: {e.Message}");
        }
    }
    private const string LashEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.TentacleLash";

    // The twelve spawn spots, in the camera's frame (right, up, forward): a half ring 0.8 m out
    // around the view, from just below level on either side round under it, and hardly ahead of
    // the eye - ~80 deg off the aim, well outside the view on every side, so a tentacle's start is
    // never seen: they sweep in from off the sides and bottom of the screen. Never above 15 deg
    // below level, so they all stay below the eyeline.
    internal const int SpotCount = 12;
    private static readonly Vector3[] Spots = BuildSpots();
    private static readonly bool[] spotTaken = new bool[SpotCount]; // the local shooter's: a lash is on it

    private static Vector3[] BuildSpots()
    {
        var spots = new Vector3[SpotCount];
        for (var i = 0; i < SpotCount; i++)
        {
            var angle = Mathf.Lerp(-165f, -15f, i / (float)(SpotCount - 1)) * Mathf.Deg2Rad; // left, round under, to right
            var radius = i % 2 == 0 ? 0.8f : 0.9f; // two staggered rings
            spots[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0.15f);
        }
        return spots;
    }

    // Spot `index` in the world, for the camera at `eye` looking along `aim`.
    internal static Vector3 SpotPosition(int index, Vector3 eye, Vector3 aim)
    {
        var right = Vector3.Cross(Vector3.up, aim);
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        right.Normalize();
        var up = Vector3.Cross(aim, right);
        var spot = Spots[index];
        return eye + right * spot.x + up * spot.y + aim * spot.z;
    }

    internal static void FreeSpot(int index)
    {
        if (index >= 0 && index < SpotCount) spotTaken[index] = false;
    }

    // A free spot at random, taken; -1 when all twelve have a tentacle on them.
    private static int TakeSpot()
    {
        var free = new List<int>();
        for (var i = 0; i < SpotCount; i++)
            if (!spotTaken[i]) free.Add(i);
        if (free.Count == 0) return -1;
        var spot = free[UnityEngine.Random.Range(0, free.Count)];
        spotTaken[spot] = true;
        return spot;
    }

    // Diagnostics while the cannon is being brought up: the first few lashes log what happened.
    internal static int DiagnosticShots = 8;

    internal static void Init()
    {
        RemoteEvents.Register<LashMessage>(LashEvent, (_, message) =>
            TentacleLash.Spawn(new Vector3(message.X, message.Y, message.Z), -1,
                new Vector3(message.EndX, message.EndY, message.EndZ), Vector3.zero, null, null, 0f, 0f, 0));
    }

    // One round: TentaclesPerShot tentacles, each from a free spawn spot.
    internal static void FirePellet(PlayerAgent owner, Vector3 eye, Vector3 pellet, float damage, uint gearCategoryId)
    {
        var aim = owner.FPSCamera != null ? owner.FPSCamera.Forward : pellet;
        var range = Reach;
        // One target for all of them: the enemy nearest the crosshair, in reach.
        var nearby = SeekerGuns.FindLocks(eye, aim, EngageConeDegrees.Value, range);
        var target = nearby.Count > 0 ? nearby[0] : null;

        for (var n = 0; n < Mathf.Max(1, TentaclesPerShot.Value); n++)
        {
            var spot = TakeSpot();
            if (spot < 0) return; // every spot still has a tentacle on it
            var start = SpotPosition(spot, eye, aim);

            // On an enemy: its own spot on the body, so they don't all land on one point. Otherwise:
            // its own random line, out to whatever it meets.
            var offset = UnityEngine.Random.insideUnitSphere * 0.25f;
            var end = target != null
                ? target.AimTarget.position + offset
                : SomethingToGrab(eye, Cone(aim, GrabConeDegrees.Value), range);

            if (DiagnosticShots > 0)
            {
                DiagnosticShots--;
                Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons Tentacle Cannon: spot {spot} at {start}, target {(target != null ? target.name : "none")}, damage {damage}.");
            }

            TentacleLash.Spawn(start, spot, end, offset, target, owner, damage, StaggerMulti.Value, gearCategoryId);
            NetworkAPI.InvokeEvent(LashEvent, new LashMessage(start, end));
        }
    }

    // With no enemy to bite, a tentacle reaches along `line` for the first thing it meets - a wall,
    // the floor, a crate - out to its full reach; meeting nothing, it wriggles a short way out.
    private static Vector3 SomethingToGrab(Vector3 eye, Vector3 line, float range) =>
        Physics.Raycast(eye, line, out var hit, range, LayerManager.MASK_BULLETWEAPON_RAY) ? hit.point : eye + line * Mathf.Min(range, IdleReach.Value);

    // A random direction within `degrees` of `forward` (a round cone).
    private static Vector3 Cone(Vector3 forward, float degrees)
    {
        var right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        right.Normalize();
        var up = Vector3.Cross(forward, right);
        var offset = UnityEngine.Random.insideUnitCircle * degrees;
        return (Quaternion.AngleAxis(offset.x, up) * Quaternion.AngleAxis(offset.y, right) * forward).normalized;
    }

    // The world point that appears on screen where the gun's muzzle is drawn. GTFO draws the
    // first-person gun with its own camera (FPSCamera.m_itemCamera) and field of view, so the
    // muzzle's real world position doesn't line up with the barrel you see - tentacles started
    // there float in mid-air or come from under the camera. So: where the item camera shows the
    // muzzle, re-projected through the world camera at the same depth.
    internal static Vector3 BarrelOnScreen(PlayerAgent? owner, Transform muzzle, float behind)
    {
        var point = muzzle.position - muzzle.forward * behind;
        try
        {
            var fps = owner?.FPSCamera;
            var itemCamera = fps?.m_itemCamera;
            var worldCamera = fps?.GetComponent<Camera>();
            if (itemCamera == null || worldCamera == null) return point;
            return worldCamera.ViewportToWorldPoint(itemCamera.WorldToViewportPoint(point));
        }
        catch (Exception)
        {
            return point;
        }
    }

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct LashMessage
    {
        public readonly float X, Y, Z, EndX, EndY, EndZ;

        public LashMessage(Vector3 start, Vector3 end)
        {
            (X, Y, Z) = (start.x, start.y, start.z);
            (EndX, EndY, EndZ) = (end.x, end.y, end.z);
        }
    }
}

/// <summary>
/// One tentacle lash: a pooled striker tentacle (GPUCurvy) whose control points are laid along a
/// snaking line from its spawn spot to its target every frame - out, bite, hang, back in - then
/// handed back to the pool, freeing the spot.
/// </summary>
internal sealed class TentacleLash : MonoBehaviour
{
    // Out instantly (it bites the moment it lands), a hang to thrash on screen, back in: ~0.75 s,
    // so only a few are out at once.
    private const float OutSeconds = 0.04f;
    private const float HangSeconds = 0.5f;
    private const float InSeconds = 0.2f;
    private const int FallbackPointCount = 12;

    private GPUCurvy? curvy;
    private Il2CppStructArray<GPUC_ControlPoint> points = null!;
    private int spot = -1;     // the shooter's spawn spot it rises from (-1 on a teammate's copy)
    private float sink;        // metres below the spot it starts, for the thicker types
    private Vector3 start, end, targetOffset;
    private EnemyAgent? target;
    private PlayerAgent? owner; // null on a teammate's (visual-only) copy
    private float damage, staggerMulti, age, seed;

    // This lash's own thrash, rolled when it spawns so no two move alike: the plane it whips in,
    // two overlapping waves (frequency along the body, speed and direction over time, phase), how
    // big it is, how it arcs and how fast its violence pulses.
    private float planeAngle, freqA, freqB, speedA, speedB, phaseB, sizeScale, arc, pulseHz;
    private uint gearCategoryId;
    private bool bitten;
    private bool idle; // no enemy to reach: a short, tight wriggle

    private static int diagnosticLashes = 5;
    private static int diagnosticDraws = 5;
    private static float lastBiteLog = -10f;
    private bool loggedMidLash;

    public TentacleLash(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(Vector3 start, int spot, Vector3 end, Vector3 targetOffset, EnemyAgent? target, PlayerAgent? owner,
        float damage, float staggerMulti, uint gearCategoryId)
    {
        // Girth comes with the tentacle type (each type's pool shares one shape), so each lash rolls
        // its type: mostly strikers', some big strikers', the odd tank's - a mixed, writhing mass.
        // The thicker ones rise from lower down so their bulk stays below the eyeline too.
        var roll = UnityEngine.Random.value;
        var type = roll < 0.55f ? GPUCurvyType.Striker : roll < 0.85f ? GPUCurvyType.StrikerBig : GPUCurvyType.Tank;
        var sink = type == GPUCurvyType.Striker ? 0f : type == GPUCurvyType.StrikerBig ? 0.1f : 0.2f;
        if (!GPUCurvyManager.TryAllocateGPUCurvy(type, out var curvy) || curvy == null)
        {
            sink = 0f;
            if (type != GPUCurvyType.Striker) GPUCurvyManager.TryAllocateGPUCurvy(GPUCurvyType.Striker, out curvy);
        }
        if (curvy == null)
        {
            Plugin.Logger.LogWarning("GTFO.EZ.AbsurdWeapons: no free tentacle to lash with (the game's tentacle pool gave none).");
            TentacleCannon.FreeSpot(spot);
            return;
        }
        if (TentacleCannon.DiagnosticShots >= 0 && diagnosticLashes-- > 0)
            Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons tentacle allocated: id {curvy.GPUC_ID}, {curvy.m_pointCount} control points, visible {curvy.IsVisible}, from {start} to {end}.");

        var go = new GameObject("GTFO.EZ.AbsurdWeapons Tentacle Lash");
        var lash = go.AddComponent<TentacleLash>();
        lash.curvy = curvy;
        lash.points = new Il2CppStructArray<GPUC_ControlPoint>(curvy.m_pointCount > 1 ? curvy.m_pointCount : FallbackPointCount);
        lash.spot = spot;
        lash.sink = sink;
        lash.start = start + Vector3.down * sink;
        lash.end = end;
        lash.targetOffset = targetOffset;
        lash.target = target;
        lash.idle = target == null;
        lash.owner = owner;
        lash.damage = damage;
        lash.staggerMulti = staggerMulti;
        lash.gearCategoryId = gearCategoryId;
        lash.seed = UnityEngine.Random.value * 10f;
        // Whips side to side, never up and down: the plane stays within 20 deg of level.
        lash.planeAngle = UnityEngine.Random.Range(-20f, 20f) + (UnityEngine.Random.value < 0.5f ? 0f : 180f);
        lash.freqA = UnityEngine.Random.Range(1.5f, 5f);
        lash.freqB = UnityEngine.Random.Range(2f, 7f);
        lash.speedA = UnityEngine.Random.Range(10f, 30f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        lash.speedB = UnityEngine.Random.Range(8f, 26f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        lash.phaseB = UnityEngine.Random.value * Mathf.PI * 2f;
        lash.sizeScale = UnityEngine.Random.Range(0.7f, 1.3f);
        lash.arc = UnityEngine.Random.Range(-0.3f, 0.3f); // a slight bow, either way
        lash.pulseHz = UnityEngine.Random.Range(3f, 9f);
        lash.Draw(0f);
    }

    private void Update()
    {
        try
        {
            age += Time.deltaTime;
            if (age >= OutSeconds + HangSeconds + InSeconds)
            {
                Destroy(gameObject);
                return;
            }

            float extend;
            if (age < OutSeconds) extend = age / OutSeconds;
            else if (age < OutSeconds + HangSeconds) extend = 1f;
            else extend = 1f - (age - OutSeconds - HangSeconds) / InSeconds;

            var camera = owner != null ? owner.FPSCamera : null;
            if (spot >= 0 && camera != null) start = TentacleCannon.SpotPosition(spot, camera.Position, camera.Forward) + Vector3.down * sink;
            if (target != null && target.Alive) end = target.AimTarget.position + targetOffset;
            var reach = TentacleCannon.Reach;
            if ((end - start).sqrMagnitude > reach * reach) end = start + (end - start).normalized * reach;

            if (!bitten && age >= OutSeconds)
            {
                bitten = true;
                Sounds.Play(Sounds.TentacleBite.Value, end, owner != null);
                Bite();
            }
            Draw(Mathf.Clamp01(extend));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons tentacle lash failed: {e.Message}");
            Destroy(gameObject);
        }
    }

    // Lays the control points along a thrashing snake from start to end: a big sideways wave and a
    // small vertical one (in this lash's own near-level plane), zero at both ends, rippling along
    // the body at their own speeds and pulsing in strength - so every lash whips differently.
    [HideFromIl2Cpp]
    private void Draw(float extend)
    {
        if (curvy == null) return;
        var line = end - start;
        var length = line.magnitude;
        var forward = length > 0.001f ? line / length : Vector3.forward;
        var side = Vector3.Cross(Vector3.up, forward);
        if (side.sqrMagnitude < 0.001f) side = Vector3.right;
        side = Quaternion.AngleAxis(planeAngle, forward) * side.normalized;
        var up = Vector3.Cross(forward, side);
        var pulse = 0.65f + 0.35f * Mathf.Sin(age * pulseHz * Mathf.PI * 2f + seed);
        // Small enough to stay inside the spew cone: at most ~4% of the length either side.
        // Reaching for an enemy: a gentle sway that keeps it on course. Idle: a tight, lively wriggle.
        // Lively but never extreme: the bend stays under ~10% of the length either side.
        var amplitude = (idle ? Mathf.Min(0.3f, length * 0.07f) : Mathf.Min(0.45f, length * 0.05f)) * sizeScale * pulse;

        // The spline runs through the inner control points only - the first and last just steer its
        // ends (the head was measured landing at 75% of the line with 5 points). So the second point
        // sits on the barrel and the second-to-last on the target, and the outer two lie beyond.
        var count = points.Length;
        var inner = count >= 4 ? count - 3 : 0;
        for (var i = 0; i < count; i++)
        {
            var t = inner > 0 ? (i - 1) / (float)inner : count > 1 ? i / (float)(count - 1) : 0f;
            var envelope = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            var wriggle = idle ? 1.8f : 1f; // idle tentacles squirm faster
            var waveA = Mathf.Sin(t * Mathf.PI * freqA - age * speedA * wriggle + seed);
            var waveB = Mathf.Sin(t * Mathf.PI * freqB - age * speedB * wriggle + phaseB);
            var position = start + line * t
                + side * (waveA * 1.4f * amplitude * envelope)
                + up * ((waveB * 0.25f + arc) * amplitude * envelope);
            points[i] = new GPUC_ControlPoint { pos = position };
        }

        // An enemy's tentacle has its owner switch it visible; ours has no owner, so it's switched
        // on here every frame it's out.
        curvy.m_isVisible = true;
        curvy.FeedControlPositions(points);
        curvy.FeedSettings(extend, 1f, up);
        curvy.CalculateEndPointsAndUpdateGPU();

        if (!loggedMidLash && age >= OutSeconds && diagnosticDraws-- > 0)
        {
            loggedMidLash = true;
            curvy.GetHeadPos(extend, out var head, out _);
            curvy.GetHeadPos(0f, out var tail, out _);
            Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons tentacle mid-lash: visible {curvy.IsVisible}, head at {head}, tail at {tail}, {count} control points, start {start}, end {end}, extend {extend:0.##}, body tiles {curvy.CurrentBodyTileCount}.");
        }
    }

    // The bite: damage and a heavy stagger to the target (or whatever enemy is at the lash's end),
    // through its limb nearest the end. Only the shooter's game deals it.
    [HideFromIl2Cpp]
    private void Bite()
    {
        if (owner == null) return;

        var enemy = target;
        if (enemy == null)
        {
            foreach (var collider in Physics.OverlapSphere(end, 0.6f, LayerManager.MASK_ENEMY_DAMAGABLE))
            {
                enemy = collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner;
                if (enemy != null) break;
            }
        }
        if (enemy == null)
        {
            // Grabbed a door lock instead.
            LockDamage.Radius(end, 0.5f, damage, owner.Cast<Agent>());
            return;
        }
        if (!enemy.Alive) return;

        Dam_EnemyDamageLimb? nearest = null;
        var best = float.MaxValue;
        foreach (var limb in enemy.GetComponentsInChildren<Dam_EnemyDamageLimb>())
        {
            var distance = (limb.transform.position - end).sqrMagnitude;
            if (distance < best) { best = distance; nearest = limb; }
        }
        if (nearest == null) return;

        var healthBefore = nearest.m_health;
        var direction = (end - start).normalized;
        nearest.BulletDamage(damage, owner.Cast<Agent>(), end, direction, -direction,
            allowDirectionalBonus: false, staggerMulti, 1f, gearCategoryId);
        if (damage > 0f) InfiniteAmmo.SpendTentacleBite(owner); // only a bite that lands costs a round

        // Proof it landed, not just that it was sent: the limb's own health before and after,
        // throttled so continuous fire doesn't flood the log.
        if (Time.time - lastBiteLog > 0.5f)
        {
            lastBiteLog = Time.time;
            Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons Tentacle Cannon bite: {enemy.name} limb health {healthBefore:0.#} -> {nearest.m_health:0.#} (dealt {damage:0.#}).");
        }
    }

    private void OnDestroy()
    {
        TentacleCannon.FreeSpot(spot);
        spot = -1;
        if (curvy == null) return;
        try
        {
            GPUCurvyManager.DeallocateGPUCurvy(curvy);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons couldn't return a tentacle: {e.Message}");
        }
        curvy = null;
    }
}
