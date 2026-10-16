using System;
using System.Collections.Generic;
using Agents;
using Enemies;
using GTFO.API;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Player;
using UnityEngine;

namespace GTFO.Weapons;

/// <summary>
/// The Tentacle Cannon: a full-auto gun whose every round is a striker's tentacle - the game's
/// own, drawn by its GPU tentacle renderer. Each round's tentacle hunts an enemy near its line of
/// fire (one not already taken by another tentacle fired the same frame, if the gun ever fires
/// several at once). It snakes out of the barrel, bites (damage and a heavy stagger, through the
/// enemy limb's normal bullet path, so it syncs), hangs a moment and retracts. With no enemy near
/// its line it reaches for the nearest thing to grab instead - crates, boxes, walls, the floor. Teammates are sent each lash
/// and see the same thing; only the shooter's game deals damage.
/// </summary>
internal static class TentacleCannon
{
    private const float LockConeDegrees = 15f; // around each tentacle's own line
    private const float FanSideDegrees = 30f;  // each tentacle leaves up to this far left or right of the aim...
    private const float FanUpDownDegrees = 5f; // ...but stays close to the aim's height: a wide, level fan
    private const float SpawnBehind = 0.05f;   // a touch behind the muzzle point, so they emerge from inside the barrel
    private const float LockRange = 25f;
    private const float MissReach = 15f; // how far an unlocked lash reaches
    private const float StaggerMulti = 5f;
    private const string LashEvent = "MindAttic.GTFO.Weapons.TentacleLash";

    // Enemies already claimed by a tentacle of the shot being fired (a shot's pellets all come in
    // the same frame).
    private static readonly HashSet<IntPtr> claimed = new();
    private static int claimedFrame = -1;

    // Diagnostics while the cannon is being brought up: the first few lashes log what happened.
    internal static int DiagnosticShots = 8;

    internal static void Init()
    {
        NetworkAPI.RegisterEvent<LashMessage>(LashEvent, (_, message) =>
            TentacleLash.Spawn(new Vector3(message.X, message.Y, message.Z), null,
                new Vector3(message.EndX, message.EndY, message.EndZ), null, null, 0f, 0f, 0));
    }

    // One pellet: one tentacle along (and bending onto an enemy near) the pellet's direction.
    internal static void FirePellet(PlayerAgent owner, Transform? muzzle, Vector3 eye, Vector3 pellet, float damage, uint gearCategoryId)
    {
        if (claimedFrame != Time.frameCount)
        {
            claimedFrame = Time.frameCount;
            claimed.Clear();
        }

        // Fan out: every tentacle takes its own random line within a wide, flat fan around the aim.
        var aim = pellet;
        pellet = Fan(pellet, FanSideDegrees, FanUpDownDegrees);
        var start = muzzle != null ? BarrelOnScreen(owner, muzzle, SpawnBehind) : eye + aim * 0.5f;
        EnemyAgent? target = null;
        foreach (var enemy in SeekerGuns.FindLocks(eye, pellet, LockConeDegrees, LockRange))
        {
            if (!claimed.Add(enemy.Pointer)) continue;
            target = enemy;
            break;
        }

        var end = target != null ? target.AimTarget.position : SomethingToGrab(eye, pellet);

        if (DiagnosticShots > 0)
        {
            DiagnosticShots--;
            Plugin.Logger.LogInfo($"GTFO.Weapons Tentacle Cannon pellet: muzzle {(muzzle != null ? "found" : "missing")} at {start}, target {(target != null ? target.name : "none")}, damage {damage}.");
        }

        TentacleLash.Spawn(start, muzzle, end, target, owner, damage, StaggerMulti, gearCategoryId, SpawnBehind);
        NetworkAPI.InvokeEvent(LashEvent, new LashMessage(start, end));
    }

    // With no enemy to bite, a tentacle reaches for something to grab: it probes a cone around its
    // line - a flat fan, wide sideways but close to the aim's height - and latches onto the nearest
    // surface it finds: a crate, a box, a railing, a wall. With nothing in reach it flails out along
    // its line.
    private const float GrabSideDegrees = 35f; // the probes fan out sideways...
    private const float GrabUpDownDegrees = 8f; // ...but stay near the aim's height
    private const float GrabReach = 12f;
    private const int GrabProbes = 10;

    private static Vector3 SomethingToGrab(Vector3 eye, Vector3 line)
    {
        var best = eye + line * MissReach;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < GrabProbes; i++)
        {
            var probe = i == 0 ? line : Fan(line, GrabSideDegrees, GrabUpDownDegrees);
            if (Physics.Raycast(eye, probe, out var hit, GrabReach, LayerManager.MASK_BULLETWEAPON_RAY) && hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                best = hit.point;
            }
        }
        return best;
    }

    // A random direction within a flat fan around `forward`: up to `sideways` degrees left or right,
    // `upDown` degrees up or down.
    private static Vector3 Fan(Vector3 forward, float sideways, float upDown)
    {
        var right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        right.Normalize();
        var up = Vector3.Cross(forward, right);
        var yaw = UnityEngine.Random.Range(-sideways, sideways);
        var pitch = UnityEngine.Random.Range(-upDown, upDown);
        return (Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(pitch, right) * forward).normalized;
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
/// snaking line from the barrel to its target every frame - out, bite, hang, back in - then
/// handed back to the pool.
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
    private Transform? follow; // the shooter's muzzle, so the lash stays on the gun
    private float behind;      // how far behind the muzzle (along its facing) the lash starts
    private Vector3 start, end;
    private EnemyAgent? target;
    private PlayerAgent? owner; // null on a teammate's (visual-only) copy
    private float damage, staggerMulti, age, seed;

    // This lash's own thrash, rolled when it spawns so no two move alike: the plane it whips in,
    // two overlapping waves (frequency along the body, speed and direction over time, phase), how
    // big it is, how it arcs and how fast its violence pulses.
    private float planeAngle, freqA, freqB, speedA, speedB, phaseB, sizeScale, arc, pulseHz;
    private uint gearCategoryId;
    private bool bitten;

    private static int diagnosticLashes = 5;
    private static int diagnosticDraws = 5;
    private bool loggedMidLash;

    public TentacleLash(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(Vector3 start, Transform? follow, Vector3 end, EnemyAgent? target, PlayerAgent? owner,
        float damage, float staggerMulti, uint gearCategoryId, float behind = 0f)
    {
        if (!GPUCurvyManager.TryAllocateGPUCurvy(GPUCurvyType.Striker, out var curvy) || curvy == null)
        {
            Plugin.Logger.LogWarning("GTFO.Weapons: no free tentacle to lash with (the game's tentacle pool gave none).");
            return;
        }
        if (TentacleCannon.DiagnosticShots >= 0 && diagnosticLashes-- > 0)
            Plugin.Logger.LogInfo($"GTFO.Weapons tentacle allocated: id {curvy.GPUC_ID}, {curvy.m_pointCount} control points, visible {curvy.IsVisible}, from {start} to {end}.");

        var go = new GameObject("GTFO.Weapons Tentacle Lash");
        var lash = go.AddComponent<TentacleLash>();
        lash.curvy = curvy;
        lash.points = new Il2CppStructArray<GPUC_ControlPoint>(curvy.m_pointCount > 1 ? curvy.m_pointCount : FallbackPointCount);
        lash.follow = follow;
        lash.behind = behind;
        lash.start = start;
        lash.end = end;
        lash.target = target;
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
        lash.sizeScale = UnityEngine.Random.Range(0.7f, 1.9f);
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

            if (follow != null) start = TentacleCannon.BarrelOnScreen(owner, follow, behind);
            if (target != null && target.Alive) end = target.AimTarget.position;

            if (!bitten && age >= OutSeconds)
            {
                bitten = true;
                Bite();
            }
            Draw(Mathf.Clamp01(extend));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.Weapons tentacle lash failed: {e.Message}");
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
        var amplitude = Mathf.Min(1f, length * 0.1f) * sizeScale * pulse;

        var count = points.Length;
        for (var i = 0; i < count; i++)
        {
            var t = count > 1 ? i / (float)(count - 1) : 0f;
            var envelope = Mathf.Sin(t * Mathf.PI);
            var waveA = Mathf.Sin(t * Mathf.PI * freqA - age * speedA + seed);
            var waveB = Mathf.Sin(t * Mathf.PI * freqB - age * speedB + phaseB);
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
            Plugin.Logger.LogInfo($"GTFO.Weapons tentacle mid-lash: visible {curvy.IsVisible}, head at {head}, start {start}, end {end}, extend {extend:0.##}, body tiles {curvy.CurrentBodyTileCount}.");
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
        if (enemy == null || !enemy.Alive) return;

        Dam_EnemyDamageLimb? nearest = null;
        var best = float.MaxValue;
        foreach (var limb in enemy.GetComponentsInChildren<Dam_EnemyDamageLimb>())
        {
            var distance = (limb.transform.position - end).sqrMagnitude;
            if (distance < best) { best = distance; nearest = limb; }
        }
        if (nearest == null) return;

        var direction = (end - start).normalized;
        nearest.BulletDamage(damage, owner.Cast<Agent>(), end, direction, -direction,
            allowDirectionalBonus: false, staggerMulti, 1f, gearCategoryId);
    }

    private void OnDestroy()
    {
        if (curvy == null) return;
        try
        {
            GPUCurvyManager.DeallocateGPUCurvy(curvy);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.Weapons couldn't return a tentacle: {e.Message}");
        }
        curvy = null;
    }
}
