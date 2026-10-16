using System;
using System.Collections.Generic;
using Agents;
using Enemies;
using FX_EffectSystem;
using GTFO.API;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Chain Lightning Gun: every shot is a bolt of lightning. It strikes the enemy under the
/// crosshair (or, if the shot would miss, the enemy nearest the aim within a narrow cone), then
/// jumps to the nearest enemy it hasn't hit yet, again and again, up to five jumps, each one
/// weaker than the last. Jumps need line of sight. Damage goes through the bullet path on the
/// shooter's game, so it syncs; teammates are sent the bolt's path and see the same lightning.
/// </summary>
internal static class ChainLightning
{
    internal const int MaxJumps = 5;
    private const float Range = 40f;
    private const float AssistConeDegrees = 6f; // a near miss still finds its first target
    private const float JumpRange = 8f;
    private const float JumpFalloff = 0.75f;    // each jump does this share of the one before
    private const float StaggerMulti = 2f;
    private const string BoltEvent = "MindAttic.GTFO.AbsurdWeapons.ChainLightning";

    internal static void Init()
    {
        NetworkAPI.RegisterEvent<BoltMessage>(BoltEvent, (_, message) => LightningBolt.Spawn(message.Points(), message.Count > 2));
    }

    // One shot from the local shooter.
    internal static void Fire(PlayerAgent owner, Vector3 eye, Vector3 aim, float damage, uint gearCategoryId)
    {
        var path = new List<Vector3> { LaserGuns.ScreenStart(eye, aim) };
        var struck = new HashSet<IntPtr>();

        // The first target: whatever enemy the shot hits, else the enemy nearest the aim.
        EnemyAgent? current = null;
        Dam_EnemyDamageLimb? limb = null;
        var point = eye + aim * Range;
        if (Physics.Raycast(eye, aim, out var hit, Range, LayerManager.MASK_BULLETWEAPON_RAY))
        {
            point = hit.point;
            limb = hit.collider.GetComponent<Dam_EnemyDamageLimb>();
            current = limb?.m_base?.Owner;
        }
        if (current == null)
        {
            var locks = SeekerGuns.FindLocks(eye, aim, AssistConeDegrees, Range);
            if (locks.Count > 0)
            {
                current = locks[0];
                limb = NearestLimb(current, current.AimTarget.position);
                point = current.AimTarget.position;
            }
        }

        if (current == null || limb == null || !current.Alive)
        {
            // A plain miss: the bolt crackles out to whatever it hit, harmlessly.
            path.Add(point);
            Send(path);
            return;
        }

        var strike = damage;
        for (var jump = 0; jump <= MaxJumps && current != null && limb != null; jump++)
        {
            struck.Add(current.Pointer);
            var from = path[path.Count - 1];
            var direction = (point - from).sqrMagnitude > 0.0001f ? (point - from).normalized : aim;
            limb.BulletDamage(strike, owner.Cast<Agent>(), point, direction, -direction,
                allowDirectionalBonus: false, StaggerMulti, 1f, gearCategoryId);
            path.Add(point);

            strike *= JumpFalloff;
            current = NextTarget(point, struck);
            if (current == null) break;
            point = current.AimTarget.position;
            limb = NearestLimb(current, point);
        }
        Send(path);
    }

    // A lightning pulse from a point (thrown Mjolnir): it strikes the nearest enemy within `range`
    // that it can see, then jumps on like a bolt, `jumps` times. Returns whether it struck anything.
    internal static bool Pulse(PlayerAgent owner, Vector3 from, float range, float damage, int jumps, uint gearCategoryId)
    {
        var struck = new HashSet<IntPtr>();
        var current = NextTarget(from, struck, range);
        if (current == null) return false;

        var path = new List<Vector3> { from };
        var strike = damage;
        for (var jump = 0; jump <= jumps && current != null; jump++)
        {
            var point = current.AimTarget.position;
            var limb = NearestLimb(current, point);
            if (limb == null) break;
            struck.Add(current.Pointer);
            var direction = (point - path[path.Count - 1]).normalized;
            limb.BulletDamage(strike, owner.Cast<Agent>(), point, direction, -direction,
                allowDirectionalBonus: false, StaggerMulti, 1f, gearCategoryId);
            path.Add(point);
            strike *= JumpFalloff;
            current = NextTarget(point, struck);
        }
        Send(path);
        return true;
    }

    private static void Send(List<Vector3> path)
    {
        LightningBolt.Spawn(path.ToArray(), path.Count > 2);
        NetworkAPI.InvokeEvent(BoltEvent, new BoltMessage(path));
    }

    // The nearest living enemy within jump range of `from` that hasn't been struck, in sight.
    private static EnemyAgent? NextTarget(Vector3 from, HashSet<IntPtr> struck, float range = JumpRange)
    {
        EnemyAgent? best = null;
        var bestDistance = float.MaxValue;
        foreach (var collider in Physics.OverlapSphere(from, range, LayerManager.MASK_ENEMY_DAMAGABLE))
        {
            var enemy = collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner;
            if (enemy == null || !enemy.Alive || struck.Contains(enemy.Pointer)) continue;
            var chest = enemy.AimTarget.position;
            var distance = Vector3.Distance(from, chest);
            if (distance >= bestDistance) continue;
            if (Physics.Linecast(from, chest, LayerManager.MASK_WORLD)) continue;
            best = enemy;
            bestDistance = distance;
        }
        return best;
    }

    // Shared with FlameThrower's afterburn ticks: the limb of `enemy` nearest `point`.
    internal static Dam_EnemyDamageLimb? NearestLimb(EnemyAgent enemy, Vector3 point)
    {
        Dam_EnemyDamageLimb? nearest = null;
        var best = float.MaxValue;
        foreach (var limb in enemy.GetComponentsInChildren<Dam_EnemyDamageLimb>())
        {
            var distance = (limb.transform.position - point).sqrMagnitude;
            if (distance < best) { best = distance; nearest = limb; }
        }
        return nearest;
    }

    // Plain fields so GTFO-API can copy it as raw bytes: the gun plus up to six strikes.
    private readonly struct BoltMessage
    {
        public readonly byte Count;
        public readonly float X0, Y0, Z0, X1, Y1, Z1, X2, Y2, Z2, X3, Y3, Z3, X4, Y4, Z4, X5, Y5, Z5, X6, Y6, Z6;

        public BoltMessage(List<Vector3> path)
        {
            Count = (byte)Math.Min(path.Count, MaxJumps + 2);
            Vector3 P(int i) => i < path.Count ? path[i] : Vector3.zero;
            (X0, Y0, Z0) = (P(0).x, P(0).y, P(0).z);
            (X1, Y1, Z1) = (P(1).x, P(1).y, P(1).z);
            (X2, Y2, Z2) = (P(2).x, P(2).y, P(2).z);
            (X3, Y3, Z3) = (P(3).x, P(3).y, P(3).z);
            (X4, Y4, Z4) = (P(4).x, P(4).y, P(4).z);
            (X5, Y5, Z5) = (P(5).x, P(5).y, P(5).z);
            (X6, Y6, Z6) = (P(6).x, P(6).y, P(6).z);
        }

        public Vector3[] Points()
        {
            var all = new[]
            {
                new Vector3(X0, Y0, Z0), new Vector3(X1, Y1, Z1), new Vector3(X2, Y2, Z2), new Vector3(X3, Y3, Z3),
                new Vector3(X4, Y4, Z4), new Vector3(X5, Y5, Z5), new Vector3(X6, Y6, Z6),
            };
            var points = new Vector3[Math.Min((int)Count, all.Length)];
            Array.Copy(all, points, points.Length);
            return points;
        }
    }
}

/// <summary>
/// One bolt's look: a jagged line through every strike point, re-jagged every frame so it
/// crackles, with a flash and sparks at each enemy struck, gone in a fraction of a second.
/// </summary>
internal sealed class LightningBolt : MonoBehaviour
{
    private const float Seconds = 0.16f;
    private const float SegmentLength = 0.35f;
    private const float Jitter = 0.18f;
    private static readonly Color BoltColor = new(0.7f, 0.85f, 1f, 1f);
    private static readonly Color FlashColor = new(0.45f, 0.75f, 1f);
    private static Material? material;

    private LineRenderer line = null!;
    private Vector3[] points = Array.Empty<Vector3>();
    private float age;

    public LightningBolt(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(Vector3[] points, bool struckEnemies)
    {
        if (points.Length < 2) return;
        var go = new GameObject("GTFO.AbsurdWeapons Lightning Bolt");
        var bolt = go.AddComponent<LightningBolt>();
        bolt.points = points;
        bolt.line = go.AddComponent<LineRenderer>();
        bolt.line.useWorldSpace = true;
        bolt.line.numCapVertices = 2;
        bolt.line.material = material ??= new Material(Shader.Find("Sprites/Default"));
        bolt.Jag();

        // Every strike point after the gun flashes and sparks (a plain miss just sparks).
        for (var i = 1; i < points.Length; i++)
        {
            if (struckEnemies) BurstFlash.Spawn(points[i], FlashColor, 4f, 4f, 0.15f);
            try
            {
                FX_Manager.PlayEffect(false, FX_GroupName.Impact_Electronics, points[i], Quaternion.LookRotation(points[i] - points[i - 1]), false);
            }
            catch (Exception)
            {
                // Sparks are a nicety; the bolt and flash still show.
            }
        }
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Seconds)
        {
            Destroy(gameObject);
            return;
        }
        Jag();
    }

    // Lays a fresh jagged path through the strike points: each stretch split into short segments
    // nudged sideways at random, pinned at the strike points themselves.
    [HideFromIl2Cpp]
    private void Jag()
    {
        var jagged = new List<Vector3> { points[0] };
        for (var i = 1; i < points.Length; i++)
        {
            var from = points[i - 1];
            var to = points[i];
            var segments = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(from, to) / SegmentLength), 1, 120);
            for (var s = 1; s < segments; s++)
                jagged.Add(Vector3.Lerp(from, to, s / (float)segments) + UnityEngine.Random.insideUnitSphere * Jitter);
            jagged.Add(to);
        }

        line.positionCount = jagged.Count;
        for (var i = 0; i < jagged.Count; i++) line.SetPosition(i, jagged[i]);
        var fade = 1f - age / Seconds;
        var width = 0.05f * (0.6f + 0.4f * UnityEngine.Random.value);
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = new Color(BoltColor.r, BoltColor.g, BoltColor.b, fade);
    }
}
