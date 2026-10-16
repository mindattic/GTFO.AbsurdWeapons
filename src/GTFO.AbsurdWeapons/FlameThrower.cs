using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
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
    // The gun's own archetype stats (read by GunInjector.AddFlamethrower).
    internal static ConfigEntry<float> GunDamagePerTick = null!;
    internal static ConfigEntry<float> GunTicksPerSecond = null!;
    internal static ConfigEntry<int> GunClipSize = null!;

    // The stream.
    internal static ConfigEntry<float> Range = null!;
    internal static ConfigEntry<float> EdgeDamageFraction = null!;
    internal static ConfigEntry<float> ConeDegrees = null!;
    internal const int ConeRays = 5;
    internal static ConfigEntry<float> StaggerMulti = null!;

    // Afterburn: stacks on every landed tick, each stack worth its own tick damage, refreshed
    // (not stacked in duration) by the next hit.
    internal static ConfigEntry<float> AfterburnTickSeconds = null!;
    internal static ConfigEntry<float> AfterburnDamagePerStack = null!;
    internal static ConfigEntry<int> AfterburnMaxStacks = null!;
    internal static ConfigEntry<float> AfterburnDuration = null!;

    // Fire spreads: a burning enemy's tick also ignites other living enemies this close to it.
    internal static ConfigEntry<float> SpreadRadius = null!;

    // The fuel bomb (held Aim).
    internal static ConfigEntry<float> BombCooldownSeconds = null!;
    internal static ConfigEntry<float> BombSpeed = null!;
    internal static ConfigEntry<float> PoolRadius = null!;
    internal static ConfigEntry<float> PoolDamagePerTick = null!;
    internal static ConfigEntry<float> PoolSeconds = null!;

    private const string BombLaunchEvent = "MindAttic.GTFO.AbsurdWeapons.FlameBombLaunch";

    private static float lastBomb = -10f;

    internal static void Bind(ConfigFile config)
    {
        GunDamagePerTick = config.Bind("Flamethrower", "GunDamagePerTick", 6f, "Damage dealt per tick, before range falloff.");
        GunTicksPerSecond = config.Bind("Flamethrower", "GunTicksPerSecond", 15f, "How many stream ticks the gun fires a second.");
        GunClipSize = config.Bind("Flamethrower", "GunClipSize", 150, "Clip size (infinite ammo refills it anyway).");
        Range = config.Bind("Flamethrower", "Range", 8f, "Metres the cone reaches before falling off to nothing.");
        EdgeDamageFraction = config.Bind("Flamethrower", "EdgeDamageFraction", 0.15f, "Share of full damage left at the edge of Range.");
        ConeDegrees = config.Bind("Flamethrower", "ConeDegrees", 12f, "Half-angle of the cone, either side of where you're aiming.");
        StaggerMulti = config.Bind("Flamethrower", "StaggerMulti", 1.5f, "Stagger multiplier per stream tick.");
        AfterburnTickSeconds = config.Bind("Flamethrower", "AfterburnTickSeconds", 0.5f, "Seconds between afterburn/fire-pool ticks.");
        AfterburnDamagePerStack = config.Bind("Flamethrower", "AfterburnDamagePerStack", 1.5f, "Damage per afterburn stack, per tick.");
        AfterburnMaxStacks = config.Bind("Flamethrower", "AfterburnMaxStacks", 5, "Cap on afterburn stacks per enemy.");
        AfterburnDuration = config.Bind("Flamethrower", "AfterburnDuration", 3f, "Seconds afterburn keeps ticking after the last hit.");
        SpreadRadius = config.Bind("Flamethrower", "SpreadRadius", 3f, "Metres a burning enemy's tick can ignite another enemy in.");
        BombCooldownSeconds = config.Bind("Flamethrower", "BombCooldownSeconds", 1.25f, "Minimum seconds between fuel bombs.");
        BombSpeed = config.Bind("Flamethrower", "BombSpeed", 18f, "Fuel bomb launch speed, m/s.");
        PoolRadius = config.Bind("Flamethrower", "PoolRadius", 4f, "Metres the fuel bomb's fire pool reaches.");
        PoolDamagePerTick = config.Bind("Flamethrower", "PoolDamagePerTick", 4f, "Fire pool damage per tick.");
        PoolSeconds = config.Bind("Flamethrower", "PoolSeconds", 4f, "Seconds the fire pool lasts.");
    }

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

        // At point-blank range the cone's rays are only centimetres apart, so several of them often
        // land on the same enemy - fine for damage (more of the flame touching them, same as a
        // shotgun's pellets), but afterburn must stack once per tick, not once per ray that connects.
        var ignitedThisTick = new HashSet<IntPtr>();

        for (var i = 0; i < ConeRays; i++)
        {
            var t = ConeRays == 1 ? 0f : i / (float)(ConeRays - 1) * 2f - 1f; // -1..1 across the cone
            var direction = (Quaternion.AngleAxis(t * ConeDegrees.Value, up) * aim).normalized;
            if (!Physics.Raycast(eye, direction, out var hit, Range.Value, LayerManager.MASK_BULLETWEAPON_RAY)) continue;

            var limb = hit.collider.GetComponent<Dam_EnemyDamageLimb>();
            var enemy = limb?.m_base?.Owner;
            if (limb == null || enemy == null || !enemy.Alive) continue;

            var falloff = Formulas.AreaFalloff(hit.distance, Range.Value, EdgeDamageFraction.Value);
            limb.BulletDamage(damage * falloff, owner.Cast<Agent>(), hit.point, direction, -direction,
                allowDirectionalBonus: false, StaggerMulti.Value, 1f, gearCategoryId);
            if (ignitedThisTick.Add(enemy.Pointer)) Ignite(enemy, owner, gearCategoryId);
        }
    }

    private static void FireBomb(PlayerAgent owner, Vector3 eye, Vector3 aim, uint gearCategoryId)
    {
        if (Time.time - lastBomb < BombCooldownSeconds.Value) return;
        lastBomb = Time.time;
        var position = eye + aim * 0.5f;
        var velocity = aim * BombSpeed.Value;
        FireBombProjectile.Spawn(owner, position, velocity, gearCategoryId);
        NetworkAPI.InvokeEvent(BombLaunchEvent, new BombMessage(position, velocity));
    }

    private static void Ignite(EnemyAgent enemy, PlayerAgent owner, uint gearCategoryId)
    {
        var stacks = burning.TryGetValue(enemy.Pointer, out var existing) ? existing.Stacks : 0;
        stacks = Formulas.AfterburnStacks(stacks, AfterburnMaxStacks.Value);
        burning[enemy.Pointer] = new BurnState(enemy, stacks, Time.time + AfterburnDuration.Value, owner, gearCategoryId);
    }

    internal static void SpawnPool(Vector3 position, PlayerAgent owner, uint gearCategoryId) =>
        pools.Add(new PoolState(position, owner, gearCategoryId, Time.time + PoolSeconds.Value));

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
                    limb.BulletDamage(AfterburnDamagePerStack.Value * state.Stacks, state.Owner.Cast<Agent>(),
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

            AreaDamage.Apply(pool.Position, PoolRadius.Value, PoolDamagePerTick.Value, 1f, 1f, pool.Owner.Cast<Agent>(), null, pool.GearCategoryId);
            foreach (var collider in Physics.OverlapSphere(pool.Position, PoolRadius.Value, LayerManager.MASK_ENEMY_DAMAGABLE))
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
        foreach (var collider in Physics.OverlapSphere(from, SpreadRadius.Value, LayerManager.MASK_ENEMY_DAMAGABLE))
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
        nextTickAt = Time.time + FlameThrower.AfterburnTickSeconds.Value;
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
