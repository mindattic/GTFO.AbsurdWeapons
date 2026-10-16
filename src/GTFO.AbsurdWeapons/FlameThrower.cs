using System;
using System.Collections.Generic;
using Agents;
using Enemies;
using GTFO.API;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Flamethrower: a short, brutal cone of fire with heavy range falloff - a wall of death up
/// close, nearly useless across a room (TF2, Natural Selection 2). Every tick that lands an enemy
/// stacks afterburn, a damage-over-time that keeps ticking for a few seconds after they leave the
/// stream (capped in stacks), and a burning enemy can set off anything else standing close to it -
/// fire spreads through a cluster instead of being confined to the beam. Hold Aim to lob a fuel
/// bomb instead: it explodes on contact into a lingering patch of fire rather than one instant
/// blast, for reach the cone itself can't give you.
/// Afterburn and the bomb's fire pool tick on their own clock (<see cref="FlameAfterburn"/>), not
/// just while the trigger is held, so the damage genuinely outlives the stream. Both are the
/// igniting player's own game only, like every other weapon here; the bomb's flight is shown to
/// teammates, but its fire pool and afterburn ticks are not (a flame tick twenty times a second is
/// not worth syncing for a cosmetic).
/// </summary>
internal static class FlameThrower
{
    // The stream.
    internal const float Range = 8f;
    internal const float EdgeDamageFraction = 0.15f;
    internal const float ConeDegrees = 12f;
    internal const int ConeRays = 5;
    internal const float StaggerMulti = 1.5f;

    // Afterburn: stacks on every landed tick, each stack worth its own tick damage, refreshed
    // (not stacked in duration) by the next hit.
    internal const float AfterburnTickSeconds = 0.5f;
    internal const float AfterburnDamagePerStack = 1.5f;
    internal const int AfterburnMaxStacks = 5;
    internal const float AfterburnDuration = 3f;

    // Fire spreads: a burning enemy's tick also ignites other living enemies this close to it.
    internal const float SpreadRadius = 3f;

    // The fuel bomb (held Aim).
    internal const float BombCooldownSeconds = 1.25f;
    internal const float BombSpeed = 18f;
    internal const float PoolRadius = 4f;
    internal const float PoolDamagePerTick = 4f;
    internal const float PoolSeconds = 4f;

    private const string BombLaunchEvent = "MindAttic.GTFO.AbsurdWeapons.FlameBombLaunch";

    private static float lastBomb = -10f;

    private sealed record BurnState(EnemyAgent Enemy, int Stacks, float ExpiresAt, PlayerAgent Owner, uint GearCategoryId);
    private sealed record PoolState(Vector3 Position, PlayerAgent Owner, uint GearCategoryId, float ExpiresAt);

    private static readonly Dictionary<IntPtr, BurnState> burning = new();
    private static readonly List<PoolState> pools = new();

    internal static void Init() =>
        NetworkAPI.RegisterEvent<BombMessage>(BombLaunchEvent, (_, message) =>
            FireBombProjectile.Spawn(null, new Vector3(message.X, message.Y, message.Z), new Vector3(message.VelX, message.VelY, message.VelZ), 0));

    // One trigger tick from the local shooter: the cone, or (Aim held) the bomb.
    internal static void Tick(PlayerAgent owner, Vector3 eye, Vector3 aim, float damage, uint gearCategoryId)
    {
        if (InputMapper.GetButtonKeyMouse(InputAction.Aim, eFocusState.FPS))
        {
            FireBomb(owner, eye, aim, gearCategoryId);
            return;
        }
        Stream(owner, eye, aim, damage, gearCategoryId);
    }

    private static void Stream(PlayerAgent owner, Vector3 eye, Vector3 aim, float damage, uint gearCategoryId)
    {
        var right = Vector3.Cross(Vector3.up, aim);
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        var up = Vector3.Cross(aim, right).normalized;

        for (var i = 0; i < ConeRays; i++)
        {
            var t = ConeRays == 1 ? 0f : i / (float)(ConeRays - 1) * 2f - 1f; // -1..1 across the cone
            var direction = (Quaternion.AngleAxis(t * ConeDegrees, up) * aim).normalized;
            if (!Physics.Raycast(eye, direction, out var hit, Range, LayerManager.MASK_BULLETWEAPON_RAY)) continue;

            var limb = hit.collider.GetComponent<Dam_EnemyDamageLimb>();
            var enemy = limb?.m_base?.Owner;
            if (limb == null || enemy == null || !enemy.Alive) continue;

            var falloff = Formulas.AreaFalloff(hit.distance, Range, EdgeDamageFraction);
            limb.BulletDamage(damage * falloff, owner.Cast<Agent>(), hit.point, direction, -direction,
                allowDirectionalBonus: false, StaggerMulti, 1f, gearCategoryId);
            Ignite(enemy, owner, gearCategoryId);
        }
    }

    private static void FireBomb(PlayerAgent owner, Vector3 eye, Vector3 aim, uint gearCategoryId)
    {
        if (Time.time - lastBomb < BombCooldownSeconds) return;
        lastBomb = Time.time;
        var position = eye + aim * 0.5f;
        var velocity = aim * BombSpeed;
        FireBombProjectile.Spawn(owner, position, velocity, gearCategoryId);
        NetworkAPI.InvokeEvent(BombLaunchEvent, new BombMessage(position, velocity));
    }

    private static void Ignite(EnemyAgent enemy, PlayerAgent owner, uint gearCategoryId)
    {
        var stacks = burning.TryGetValue(enemy.Pointer, out var existing) ? existing.Stacks : 0;
        stacks = Formulas.AfterburnStacks(stacks, AfterburnMaxStacks);
        burning[enemy.Pointer] = new BurnState(enemy, stacks, Time.time + AfterburnDuration, owner, gearCategoryId);
    }

    internal static void SpawnPool(Vector3 position, PlayerAgent owner, uint gearCategoryId) =>
        pools.Add(new PoolState(position, owner, gearCategoryId, Time.time + PoolSeconds));

    // Runs off FlameAfterburn's own clock, not the gun's trigger: the damage keeps ticking after
    // the stream (or the bomb's fire) stops.
    internal static void TickBurning()
    {
        if (burning.Count > 0)
        {
            var expired = new List<IntPtr>();
            foreach (var (key, state) in burning)
            {
                if (Time.time >= state.ExpiresAt || !state.Enemy.Alive) { expired.Add(key); continue; }
                if (!state.Owner.IsLocallyOwned) continue; // only the igniter's own game deals the damage

                var limb = ChainLightning.NearestLimb(state.Enemy, state.Enemy.AimTarget.position);
                if (limb != null)
                {
                    limb.BulletDamage(AfterburnDamagePerStack * state.Stacks, state.Owner.Cast<Agent>(),
                        state.Enemy.AimTarget.position, Vector3.up, Vector3.down,
                        allowDirectionalBonus: false, 0.5f, 1f, state.GearCategoryId);
                }
                SpreadTo(state.Enemy, state.Owner, state.GearCategoryId);
            }
            foreach (var key in expired) burning.Remove(key);
        }

        if (pools.Count == 0) return;
        for (var i = pools.Count - 1; i >= 0; i--)
        {
            var pool = pools[i];
            if (Time.time >= pool.ExpiresAt) { pools.RemoveAt(i); continue; }
            if (!pool.Owner.IsLocallyOwned) continue;

            AreaDamage.Apply(pool.Position, PoolRadius, PoolDamagePerTick, 1f, 1f, pool.Owner.Cast<Agent>(), null, pool.GearCategoryId);
            foreach (var collider in Physics.OverlapSphere(pool.Position, PoolRadius, LayerManager.MASK_ENEMY_DAMAGABLE))
            {
                var caught = collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner;
                if (caught != null && caught.Alive) Ignite(caught, pool.Owner, pool.GearCategoryId);
            }
        }
    }

    // Fire catches: a burning enemy's own tick also ignites other living enemies close enough to
    // it, in sight - so a cluster goes up together instead of staying confined to the beam.
    private static void SpreadTo(EnemyAgent source, PlayerAgent owner, uint gearCategoryId)
    {
        var from = source.AimTarget.position;
        foreach (var collider in Physics.OverlapSphere(from, SpreadRadius, LayerManager.MASK_ENEMY_DAMAGABLE))
        {
            var other = collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner;
            if (other == null || !other.Alive || other.Pointer == source.Pointer || burning.ContainsKey(other.Pointer)) continue;
            if (Physics.Linecast(from, other.AimTarget.position, LayerManager.MASK_WORLD)) continue;
            Ignite(other, owner, gearCategoryId);
        }
    }

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct BombMessage
    {
        public readonly float X, Y, Z, VelX, VelY, VelZ;

        public BombMessage(Vector3 position, Vector3 velocity)
        {
            (X, Y, Z) = (position.x, position.y, position.z);
            (VelX, VelY, VelZ) = (velocity.x, velocity.y, velocity.z);
        }
    }
}

/// <summary>Ticks afterburn and fire pools on their own clock - see <see cref="FlameThrower.TickBurning"/>.</summary>
internal sealed class FlameAfterburn : MonoBehaviour
{
    private float nextTickAt;

    public FlameAfterburn(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        if (Time.time < nextTickAt) return;
        nextTickAt = Time.time + FlameThrower.AfterburnTickSeconds;
        try
        {
            FlameThrower.TickBurning();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons flame afterburn tick failed: {e.Message}");
        }
    }
}

/// <summary>
/// The fuel bomb: flies by hand (gravity, a sphere cast each step) and explodes the instant it
/// touches anything, enemy or wall - no fuse, no bounce. The shooter's own copy leaves a lingering
/// fire pool (<see cref="FlameThrower.SpawnPool"/>); a teammate's copy (owner null) just flashes.
/// </summary>
internal sealed class FireBombProjectile : MonoBehaviour
{
    private const float Radius = 0.12f;
    private const float Gravity = 6f;
    private static bool modelFailed;

    private PlayerAgent? owner;
    private Vector3 velocity;
    private uint gearCategoryId;
    private bool exploded;

    public FireBombProjectile(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(PlayerAgent? owner, Vector3 position, Vector3 velocity, uint gearCategoryId)
    {
        var go = new GameObject("GTFO.AbsurdWeapons Fire Bomb");
        go.transform.position = position;
        BuildModel(go.transform);

        var bomb = go.AddComponent<FireBombProjectile>();
        bomb.owner = owner;
        bomb.velocity = velocity;
        bomb.gearCategoryId = gearCategoryId;
    }

    private void Update()
    {
        if (exploded) return;
        try
        {
            velocity += Vector3.down * (Gravity * Time.deltaTime);
            var position = transform.position;
            var step = velocity * Time.deltaTime;
            var distance = step.magnitude;
            if (distance < 0.0001f) return;
            var direction = step / distance;

            if (owner != null && Physics.SphereCast(position, Radius, direction, out var enemyHit, distance, LayerManager.MASK_ENEMY_DAMAGABLE))
            {
                transform.position = enemyHit.point;
                Explode();
                return;
            }
            if (Physics.SphereCast(position, Radius, direction, out var worldHit, distance, LayerManager.MASK_WORLD))
            {
                transform.position = worldHit.point + worldHit.normal * Radius;
                Explode();
                return;
            }
            transform.position = position + step;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons fire bomb failed: {e.Message}");
            Explode();
        }
    }

    [HideFromIl2Cpp]
    private void Explode()
    {
        if (exploded) return;
        exploded = true;
        var position = transform.position;
        var owner = this.owner;
        var gearCategoryId = this.gearCategoryId;
        Destroy(gameObject);
        Explosion.Show(position);
        if (owner != null) FlameThrower.SpawnPool(position, owner, gearCategoryId);
    }

    // A plain tinted sphere - no new assets shipped, same fallback style as the Grenade Launcher.
    [HideFromIl2Cpp]
    private static void BuildModel(Transform parent)
    {
        if (modelFailed) return;
        try
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(parent, false);
            sphere.transform.localScale = Vector3.one * 0.18f;
            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Unlit/Color")) { color = new Color(1f, 0.5f, 0.1f) };
        }
        catch (Exception e)
        {
            modelFailed = true;
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons fire bomb model unavailable: {e.Message}");
        }
    }
}
