using System;
using Agents;
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
    // Shrapnel shard (primary fire): falls off with distance travelled, dead by ShardRange.
    internal const float ShardSpeed = 45f;
    internal const float ShardRange = 10f;
    internal const float ShardEdgeFraction = 0f;
    internal const float ShardBounciness = 0.5f;
    internal const int ShardMaxBounces = 2;
    internal const float ShardLifetime = 1.2f;

    // The ball (secondary fire, held Aim): one shot, slow and a little lobbed, explodes on contact.
    internal const float BallSpeed = 22f;
    internal const float BallGravity = 4f;
    internal const float BallBlastRadius = 4f;
    internal const float BallBlastDamage = 25f;
    internal const float BallBlastEdgeFraction = 0.3f;
    internal const float BallBlastStagger = 6f;
    internal const int BallShrapnelRays = 10;
    internal const float BallShrapnelRange = 6f;
    internal const float BallShrapnelDamage = 10f;

    private const string BallLaunchEvent = "MindAttic.GTFO.AbsurdWeapons.FlakBallLaunch";

    // One shot's worth of pellets all land here; only the first (while Aim is held) becomes the ball.
    private static bool ballFiredThisShot;

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
            var velocity = direction * BallSpeed;
            FlakBall.Spawn(owner, position, velocity, gearCategoryId);
            NetworkAPI.InvokeEvent(BallLaunchEvent, new BallMessage(position, velocity));
            return;
        }

        ShrapnelShard.Spawn(owner, position, direction * ShardSpeed, damage, gearCategoryId);
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
            if (age >= FlakCannon.ShardLifetime || owner == null)
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
                velocity = Vector3.Reflect(velocity, worldHit.normal) * FlakCannon.ShardBounciness;
                if (++bounces > FlakCannon.ShardMaxBounces) Destroy(gameObject);
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
            var falloff = Formulas.AreaFalloff(traveled, FlakCannon.ShardRange, FlakCannon.ShardEdgeFraction);
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
            velocity += Vector3.down * (FlakCannon.BallGravity * Time.deltaTime);
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
        AreaDamage.Apply(position, FlakCannon.BallBlastRadius, FlakCannon.BallBlastDamage, FlakCannon.BallBlastEdgeFraction,
            FlakCannon.BallBlastStagger, owner.Cast<Agent>(), null, gearCategoryId);

        // The UT touch: a radial burst of secondary shrapnel rays layered over the main blast,
        // catching things just past its edge.
        for (var i = 0; i < FlakCannon.BallShrapnelRays; i++)
        {
            var direction = UnityEngine.Random.onUnitSphere;
            if (!Physics.Raycast(position, direction, out var hit, FlakCannon.BallShrapnelRange, LayerManager.MASK_ENEMY_DAMAGABLE)) continue;
            var limb = hit.collider.GetComponent<Dam_EnemyDamageLimb>();
            if (limb == null || limb.m_base?.Owner == null || !limb.m_base.Owner.Alive) continue;
            limb.BulletDamage(FlakCannon.BallShrapnelDamage, owner.Cast<Agent>(), hit.point, direction, -direction,
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
