using System;
using Agents;
using BepInEx.Configuration;
using GTFO.API;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Flak Cannon, Unreal Tournament style. Every pellet the shotgun fires becomes a physical
/// shard of shrapnel that bounces off the level - a wall of metal point-blank, nearly harmless
/// across a room. Hold Aim and pull the trigger instead to compress the whole shot into one slow
/// ball that explodes into a radial burst of shrapnel the instant it touches anything.
/// Shrapnel shards are the shooter's own game only - not networked to teammates (a shotgun's worth
/// of bouncing debris, every shot, would be a lot of traffic for a cosmetic); the ball is, since
/// there's only ever one of it.
/// </summary>
internal static class FlakCannon
{
    // The gun's own archetype stats (read by GunInjector.AddFlakCannon).
    internal static ConfigEntry<float> GunDamagePerShard = null!;
    internal static ConfigEntry<int> GunPelletCount = null!;
    internal static ConfigEntry<int> GunConeAndSpread = null!;
    internal static ConfigEntry<float> GunShotDelay = null!;
    internal static ConfigEntry<int> GunClipSize = null!;

    // Shrapnel shard (primary fire): falls off with distance travelled, dead by ShardRange.
    internal static ConfigEntry<float> ShardSpeed = null!;
    internal static ConfigEntry<float> ShardRange = null!;
    internal static ConfigEntry<float> ShardEdgeFraction = null!;
    internal static ConfigEntry<float> ShardBounciness = null!;
    internal static ConfigEntry<int> ShardMaxBounces = null!;
    internal static ConfigEntry<float> ShardLifetime = null!;

    // The ball (secondary fire, held Aim): one shot, slow and a little lobbed, explodes on contact.
    internal static ConfigEntry<float> BallSpeed = null!;
    internal static ConfigEntry<float> BallGravity = null!;
    internal static ConfigEntry<float> BallBlastRadius = null!;
    internal static ConfigEntry<float> BallBlastDamage = null!;
    internal static ConfigEntry<float> BallBlastEdgeFraction = null!;
    internal static ConfigEntry<float> BallBlastStagger = null!;
    internal static ConfigEntry<int> BallShrapnelRays = null!;
    internal static ConfigEntry<float> BallShrapnelRange = null!;
    internal static ConfigEntry<float> BallShrapnelDamage = null!;

    private const string BallLaunchEvent = "MindAttic.GTFO.AbsurdWeapons.FlakBallLaunch";

    // One shot's worth of pellets all land here; only the first (while Aim is held) becomes the ball.
    private static bool ballFiredThisShot;

    internal static void Bind(ConfigFile config)
    {
        GunDamagePerShard = config.Bind("Flak Cannon", "GunDamagePerShard", 7f, "Damage per shard, before range falloff.");
        GunPelletCount = config.Bind("Flak Cannon", "GunPelletCount", 9, "Pellets (shards) per shot.");
        GunConeAndSpread = config.Bind("Flak Cannon", "GunConeAndSpread", 5, "The shotgun's cone size and bullet spread.");
        GunShotDelay = config.Bind("Flak Cannon", "GunShotDelay", 0.85f, "Seconds between shots.");
        GunClipSize = config.Bind("Flak Cannon", "GunClipSize", 6, "Clip size (infinite ammo refills it anyway).");
        ShardSpeed = config.Bind("Flak Cannon", "ShardSpeed", 45f, "Shrapnel shard speed, m/s.");
        ShardRange = config.Bind("Flak Cannon", "ShardRange", 10f, "Metres of travel before a shard's damage falls to nothing.");
        ShardEdgeFraction = config.Bind("Flak Cannon", "ShardEdgeFraction", 0f, "Share of damage left at ShardRange.");
        ShardBounciness = config.Bind("Flak Cannon", "ShardBounciness", 0.5f, "Speed kept per bounce off the world.");
        ShardMaxBounces = config.Bind("Flak Cannon", "ShardMaxBounces", 2, "Bounces before a shard burns out.");
        ShardLifetime = config.Bind("Flak Cannon", "ShardLifetime", 1.2f, "Seconds before a shard expires outright.");
        BallSpeed = config.Bind("Flak Cannon", "BallSpeed", 22f, "The compressed ball's launch speed, m/s.");
        BallGravity = config.Bind("Flak Cannon", "BallGravity", 4f, "Gravity applied to the ball in flight.");
        BallBlastRadius = config.Bind("Flak Cannon", "BallBlastRadius", 4f, "Metres of the ball's central blast.");
        BallBlastDamage = config.Bind("Flak Cannon", "BallBlastDamage", 25f, "Damage at the center of the ball's blast.");
        BallBlastEdgeFraction = config.Bind("Flak Cannon", "BallBlastEdgeFraction", 0.3f, "Share of blast damage left at its edge.");
        BallBlastStagger = config.Bind("Flak Cannon", "BallBlastStagger", 6f, "Stagger multiplier of the ball's blast.");
        BallShrapnelRays = config.Bind("Flak Cannon", "BallShrapnelRays", 10, "Radial secondary-shrapnel rays on the ball's detonation.");
        BallShrapnelRange = config.Bind("Flak Cannon", "BallShrapnelRange", 6f, "Metres each secondary shrapnel ray reaches.");
        BallShrapnelDamage = config.Bind("Flak Cannon", "BallShrapnelDamage", 10f, "Damage of each secondary shrapnel ray that hits.");
    }

    internal static void Init() =>
        NetworkAPI.RegisterEvent<BallMessage>(BallLaunchEvent, (_, message) =>
            FlakBall.Spawn(null, new Vector3(message.X, message.Y, message.Z), new Vector3(message.VelX, message.VelY, message.VelZ), 0));

    // Reset at the start of every Fire() call for this gun (see SeekerGuns.BeginFire), so a fresh
    // trigger pull can fire a fresh ball.
    internal static void ResetShot() => ballFiredThisShot = false;

    // One pellet from the local shooter's shotgun blast.
    internal static void FirePellet(PlayerAgent owner, Transform? muzzle, Vector3 origin, Vector3 direction, float damage, uint gearCategoryId)
    {
        var position = muzzle != null ? muzzle.position : origin;
        if (InputMapper.GetButtonKeyMouse(InputAction.Aim, eFocusState.FPS))
        {
            if (ballFiredThisShot) return; // the rest of this shot's pellets: already spent on the ball
            ballFiredThisShot = true;
            var velocity = direction * BallSpeed.Value;
            FlakBall.Spawn(owner, position, velocity, gearCategoryId);
            NetworkAPI.InvokeEvent(BallLaunchEvent, new BallMessage(position, velocity));
            return;
        }

        ShrapnelShard.Spawn(owner, position, direction * ShardSpeed.Value, damage, gearCategoryId);
    }

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct BallMessage
    {
        public readonly float X, Y, Z, VelX, VelY, VelZ;

        public BallMessage(Vector3 position, Vector3 velocity)
        {
            (X, Y, Z) = (position.x, position.y, position.z);
            (VelX, VelY, VelZ) = (velocity.x, velocity.y, velocity.z);
        }
    }
}

/// <summary>
/// One bouncing shard of shrapnel: flies by hand, sphere-casting each step for an enemy (damage,
/// falling off with distance travelled) or the world (bounce, losing speed, up to
/// <see cref="FlakCannon.ShardMaxBounces"/> times). The shooter's own game only.
/// </summary>
internal sealed class ShrapnelShard : MonoBehaviour
{
    private const float Radius = 0.03f;

    private PlayerAgent owner = null!;
    private Vector3 velocity;
    private float damage;
    private uint gearCategoryId;
    private float traveled, age;
    private int bounces;

    public ShrapnelShard(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(PlayerAgent owner, Vector3 position, Vector3 velocity, float damage, uint gearCategoryId)
    {
        var go = new GameObject("GTFO.AbsurdWeapons Shrapnel Shard");
        go.transform.position = position;
        var shard = go.AddComponent<ShrapnelShard>();
        shard.owner = owner;
        shard.velocity = velocity;
        shard.damage = damage;
        shard.gearCategoryId = gearCategoryId;
    }

    private void Update()
    {
        try
        {
            age += Time.deltaTime;
            if (age >= FlakCannon.ShardLifetime.Value || owner == null)
            {
                Destroy(gameObject);
                return;
            }

            var position = transform.position;
            var step = velocity * Time.deltaTime;
            var distance = step.magnitude;
            if (distance < 0.0001f) return;
            var direction = step / distance;

            if (Physics.SphereCast(position, Radius, direction, out var enemyHit, distance, LayerManager.MASK_ENEMY_DAMAGABLE))
            {
                Hit(enemyHit);
                return;
            }

            if (Physics.SphereCast(position, Radius, direction, out var worldHit, distance, LayerManager.MASK_WORLD))
            {
                traveled += Vector3.Distance(position, worldHit.point);
                transform.position = worldHit.point + worldHit.normal * (Radius + 0.01f);
                velocity = Vector3.Reflect(velocity, worldHit.normal) * FlakCannon.ShardBounciness.Value;
                if (++bounces > FlakCannon.ShardMaxBounces.Value) Destroy(gameObject);
                return;
            }

            transform.position = position + step;
            traveled += distance;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons shrapnel shard failed: {e.Message}");
            Destroy(gameObject);
        }
    }

    [HideFromIl2Cpp]
    private void Hit(RaycastHit hit)
    {
        var limb = hit.collider.GetComponent<Dam_EnemyDamageLimb>();
        if (limb != null && limb.m_base?.Owner != null && limb.m_base.Owner.Alive)
        {
            var falloff = Formulas.AreaFalloff(traveled, FlakCannon.ShardRange.Value, FlakCannon.ShardEdgeFraction.Value);
            var direction = velocity.normalized;
            limb.BulletDamage(damage * falloff, owner.Cast<Agent>(), hit.point, direction, -direction,
                allowDirectionalBonus: false, 1f, 1f, gearCategoryId);
        }
        Destroy(gameObject);
    }
}

/// <summary>
/// The ball: a dense little orb lobbed with a little gravity, exploding on the first thing it
/// touches - enemy or wall, no fuse. The shooter's own copy damages and showers shrapnel; a
/// teammate's copy (owner null) just shows the flash.
/// </summary>
internal sealed class FlakBall : MonoBehaviour
{
    private const float Radius = 0.12f;
    private static bool modelFailed;

    private PlayerAgent? owner;
    private Vector3 velocity;
    private uint gearCategoryId;
    private bool exploded;

    public FlakBall(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(PlayerAgent? owner, Vector3 position, Vector3 velocity, uint gearCategoryId)
    {
        var go = new GameObject("GTFO.AbsurdWeapons Flak Ball");
        go.transform.position = position;
        BuildModel(go.transform);

        var ball = go.AddComponent<FlakBall>();
        ball.owner = owner;
        ball.velocity = velocity;
        ball.gearCategoryId = gearCategoryId;
    }

    private void Update()
    {
        if (exploded) return;
        try
        {
            velocity += Vector3.down * (FlakCannon.BallGravity.Value * Time.deltaTime);
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
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons flak ball failed: {e.Message}");
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

        if (owner == null) return; // a teammate's copy: flash and gone
        AreaDamage.Apply(position, FlakCannon.BallBlastRadius.Value, FlakCannon.BallBlastDamage.Value, FlakCannon.BallBlastEdgeFraction.Value,
            FlakCannon.BallBlastStagger.Value, owner.Cast<Agent>(), null, gearCategoryId);

        // The UT touch: a radial burst of secondary shrapnel rays layered over the main blast,
        // catching things just past its edge.
        for (var i = 0; i < FlakCannon.BallShrapnelRays.Value; i++)
        {
            var direction = UnityEngine.Random.onUnitSphere;
            if (!Physics.Raycast(position, direction, out var hit, FlakCannon.BallShrapnelRange.Value, LayerManager.MASK_ENEMY_DAMAGABLE)) continue;
            var limb = hit.collider.GetComponent<Dam_EnemyDamageLimb>();
            if (limb == null || limb.m_base?.Owner == null || !limb.m_base.Owner.Alive) continue;
            limb.BulletDamage(FlakCannon.BallShrapnelDamage.Value, owner.Cast<Agent>(), hit.point, direction, -direction,
                allowDirectionalBonus: false, 1f, 1f, gearCategoryId);
        }
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
            sphere.transform.localScale = Vector3.one * 0.22f;
            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.55f, 0.55f, 0.6f) };
        }
        catch (Exception e)
        {
            modelFailed = true;
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons flak ball model unavailable: {e.Message}");
        }
    }
}
