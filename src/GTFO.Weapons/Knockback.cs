using System;
using System.Collections.Generic;
using Enemies;
using GTFO.API;
using Il2CppInterop.Runtime.Attributes;
using SNetwork;
using UnityEngine;
using UnityEngine.AI;

namespace GTFO.Weapons;

/// <summary>
/// Physical knockback: slides an enemy across its walkable ground, fast then easing off, and stops
/// at walls and ledges (a NavMesh raycast) so nothing is shoved through geometry. Enemy positions
/// belong to the host, so the host does the sliding: a client's knockback is sent to the host,
/// which finds the enemy at that spot and pushes it. Everyone then sees it through the game's own
/// enemy position sync.
/// </summary>
internal static class Knockback
{
    private const string KnockEvent = "MindAttic.GTFO.Weapons.Knockback";

    internal static void Init()
    {
        NetworkAPI.RegisterEvent<KnockMessage>(KnockEvent, (_, message) =>
        {
            if (!SNet.IsMaster) return;
            var enemy = EnemyNear(new Vector3(message.X, message.Y, message.Z));
            if (enemy != null) KnockbackSlides.Start(enemy, new Vector3(message.DirX, 0f, message.DirZ), message.Distance);
        });
    }

    // Pushes `enemy` `distance` metres along `direction` (flattened to the ground).
    internal static void Push(EnemyAgent enemy, Vector3 direction, float distance)
    {
        if (enemy == null || !enemy.Alive || distance <= 0f) return;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();

        if (SNet.IsMaster) KnockbackSlides.Start(enemy, direction, distance);
        else NetworkAPI.InvokeEvent(KnockEvent, new KnockMessage(enemy.Position, direction, distance));
    }

    private static EnemyAgent? EnemyNear(Vector3 position)
    {
        EnemyAgent? best = null;
        var bestDistance = float.MaxValue;
        foreach (var collider in Physics.OverlapSphere(position, 2f, LayerManager.MASK_ENEMY_DAMAGABLE))
        {
            var enemy = collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner;
            if (enemy == null || !enemy.Alive) continue;
            var distance = (enemy.Position - position).sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; best = enemy; }
        }
        return best;
    }

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct KnockMessage
    {
        public readonly float X, Y, Z, DirX, DirZ, Distance;

        public KnockMessage(Vector3 position, Vector3 direction, float distance)
        {
            (X, Y, Z) = (position.x, position.y, position.z);
            (DirX, DirZ) = (direction.x, direction.z);
            Distance = distance;
        }
    }
}

/// <summary>Runs the knockback slides in progress, a little each frame.</summary>
internal sealed class KnockbackSlides : MonoBehaviour
{
    private const float Sharpness = 14f;    // how quickly a slide covers its distance (ease-out)
    private const float MaxSeconds = 0.6f;
    private const int AllAreas = -1; // NavMesh.AllAreas, missing from the interop build

    private sealed class Slide
    {
        public EnemyAgent Enemy = null!;
        public Vector3 Direction;
        public float Remaining, Age;
    }

    private static readonly Dictionary<IntPtr, Slide> slides = new();

    public KnockbackSlides(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Start(EnemyAgent enemy, Vector3 direction, float distance)
    {
        // A new hit replaces a slide still running on the same enemy.
        slides[enemy.Pointer] = new Slide { Enemy = enemy, Direction = direction.normalized, Remaining = distance };
    }

    private void Update()
    {
        if (slides.Count == 0) return;
        var finished = new List<IntPtr>();
        foreach (var (key, slide) in slides)
        {
            try
            {
                var enemy = slide.Enemy;
                slide.Age += Time.deltaTime;
                if (enemy == null || !enemy.Alive || slide.Remaining < 0.02f || slide.Age > MaxSeconds)
                {
                    finished.Add(key);
                    continue;
                }

                var step = slide.Remaining * (1f - Mathf.Exp(-Sharpness * Time.deltaTime));
                var from = enemy.Position;
                var to = from + slide.Direction * step;
                if (!NavMesh.SamplePosition(from, out var fromHit, 1f, AllAreas)) { finished.Add(key); continue; }
                // Stop at the edge of walkable ground (walls, ledges).
                if (NavMesh.Raycast(fromHit.position, to, out var blocked, AllAreas))
                {
                    to = blocked.position;
                    slide.Remaining = 0f;
                }
                else
                {
                    slide.Remaining -= step;
                }
                if (NavMesh.SamplePosition(to, out var toHit, 1f, AllAreas))
                    enemy.AI.NavmeshAgentWarp(toHit.position);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"GTFO.Weapons knockback slide failed: {e.Message}");
                finished.Add(key);
            }
        }
        foreach (var key in finished) slides.Remove(key);
    }
}
