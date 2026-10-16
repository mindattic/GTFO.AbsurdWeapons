using System;
using System.Collections.Generic;
using Agents;
using Enemies;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// A player-fired seeker. It borrows a real enemy projectile for its looks (model, glow, trail)
/// but switches that projectile's own logic off - left on, it would home in on and damage
/// players. This component flies it instead: it leaves the muzzle in a spread, weaves if its kind
/// does, then locks onto the enemy nearest its line of flight and turns toward it at a capped rate.
/// On contact it deals damage through the enemy limb's normal bullet path, blasts the area around
/// it and throws off shards, as its <see cref="SeekerKind"/> says. Only the shooter's copy deals
/// damage; teammates' copies (and their shards) are visual.
/// </summary>
internal sealed class SeekerProjectile : MonoBehaviour
{
    private const float HitRadius = 0.15f;
    private const float RetargetInterval = 0.2f;
    private const float FadeOutSeconds = 1f;
    private const float FlashSeconds = 0.12f;
    private const float FlashIntensity = 3f;

    // The first tinted projectile's renderers, shaders and color slots go to the log once, to
    // show where a borrowed projectile keeps its color if the tint doesn't take.
    private static bool tintDumped;

    private SeekerKind kind = null!;
    private PlayerAgent? owner; // null on a teammate's (visual-only) copy
    private float damage, staggerMulti, precisionMulti;
    private uint gearCategoryId;

    private Vector3 direction, wobbleAxis;
    private float age, retargetIn, fadeLeft, wobblePhase;
    private EnemyAgent? target;
    private IntPtr ignoredEnemy; // a shard won't chase the enemy its parent just hit
    private bool dead;

    // Smart-lock curve (Titanfall's Smart Pistol): a quadratic Bezier from the muzzle, pulled
    // out along the aim, onto the locked enemy's chest - re-aimed at it every frame as it moves.
    private bool curving;
    private Vector3 curveStart, curveControl, curveEnd;
    private float curveProgress; // 0..1 along the curve
    private EnemyAgent? curveTarget; // null on a teammate's copy: it curves to the sent point

    public SeekerProjectile(IntPtr ptr) : base(ptr) { }

    private static int HitMask => LayerManager.MASK_ENEMY_DAMAGABLE | LayerManager.MASK_PROJECTILE_BLOCKERS;

    [HideFromIl2Cpp]
    internal static void Spawn(SeekerKind kind, Vector3 position, Vector3 direction, PlayerAgent? owner,
        float damage, float staggerMulti, float precisionMulti, uint gearCategoryId, IntPtr ignoredEnemy = default,
        EnemyAgent? lockTarget = null, Vector3? lockPoint = null)
    {
        direction = Spread(direction.normalized, kind.SpreadDegrees);
        var go = ProjectileManager.SpawnProjectileType(kind.Projectile, position, Quaternion.LookRotation(direction));
        if (go == null) return;

        // Silence the enemy logic before it runs a frame: no player homing, damage or self-destruct.
        var vanilla = go.GetComponent<ProjectileBase>();
        if (vanilla != null)
        {
            vanilla.enabled = false;
            try
            {
                vanilla.OnFire(null);
                vanilla.m_soundPlayer?.Stop();
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons couldn't fully silence a borrowed projectile: {e.Message}");
            }
            vanilla.enabled = false;
        }

        if (kind.Tint.a > 0f) Tint(go, kind.Tint);

        var seeker = go.AddComponent<SeekerProjectile>();
        seeker.kind = kind;
        seeker.owner = owner;
        seeker.damage = kind.OwnDamage > 0f ? kind.OwnDamage : damage;
        seeker.staggerMulti = staggerMulti > 0f ? staggerMulti : 1f;
        seeker.precisionMulti = precisionMulti > 0f ? precisionMulti : 1f;
        seeker.gearCategoryId = gearCategoryId;
        seeker.direction = direction;
        seeker.ignoredEnemy = ignoredEnemy;
        seeker.wobbleAxis = Vector3.Cross(direction, UnityEngine.Random.onUnitSphere).normalized;
        seeker.wobblePhase = UnityEngine.Random.value * Mathf.PI * 2f;
        seeker.retargetIn = kind.LaunchSeconds;

        var end = lockTarget != null ? lockTarget.AimTarget.position : lockPoint;
        if (end.HasValue)
        {
            seeker.curving = true;
            seeker.curveTarget = lockTarget;
            seeker.curveStart = position;
            seeker.curveEnd = end.Value;
            // The bend: leave along the aim for a share of the distance, then sweep onto the target.
            seeker.curveControl = position + direction * (Vector3.Distance(position, end.Value) * kind.CurveBend);
        }
    }

    private void Update()
    {
        var dt = Time.deltaTime;
        if (dead)
        {
            fadeLeft -= dt;
            if (fadeLeft <= 0f) Destroy(gameObject);
            return;
        }

        age += dt;
        if (age >= kind.Lifetime)
        {
            Die();
            return;
        }

        if (curving)
        {
            FlyCurve(dt);
            return;
        }

        if (kind.Homing && age >= kind.LaunchSeconds)
        {
            if (target == null || !target.Alive)
            {
                target = null;
                retargetIn -= dt;
                if (retargetIn <= 0f)
                {
                    retargetIn = RetargetInterval;
                    target = FindTarget();
                }
            }

            if (target != null)
            {
                var toTarget = (target.AimTarget.position - transform.position).normalized;
                direction = Vector3.RotateTowards(direction, toTarget, kind.TurnDegreesPerSecond * Mathf.Deg2Rad * dt, 0f).normalized;
            }
        }

        // The weave bends the flight path, not the heading, and stops once locked on - at these
        // turn rates it would only swerve the seeker past its target.
        var heading = direction;
        if (kind.WobbleDegrees > 0f && target == null)
            heading = Quaternion.AngleAxis(Mathf.Sin(age * kind.WobbleHz * Mathf.PI * 2f + wobblePhase) * kind.WobbleDegrees, wobbleAxis) * direction;

        var position = transform.position;
        var step = kind.Speed * dt;
        if (Physics.SphereCast(position, HitRadius, heading, out var hit, step, HitMask))
        {
            transform.position = hit.point;
            Impact(hit, heading);
            Die();
            return;
        }

        transform.SetPositionAndRotation(position + heading * step, Quaternion.LookRotation(heading));
    }

    [HideFromIl2Cpp]
    private void FlyCurve(float dt)
    {
        if (curveTarget != null)
        {
            if (curveTarget.Alive)
            {
                curveEnd = curveTarget.AimTarget.position;
            }
            else if (kind.Homing)
            {
                // Its target died: hunt the next one instead of flying on to where it was.
                curving = false;
                curveTarget = null;
                target = null;
                retargetIn = 0f;
                return;
            }
        }

        // Advance by distance along the (roughly measured) curve, so speed stays constant.
        var length = Mathf.Max(0.5f, Vector3.Distance(curveStart, curveControl) + Vector3.Distance(curveControl, curveEnd));
        curveProgress = Mathf.Min(1f, curveProgress + kind.Speed * dt / length);
        var u = 1f - curveProgress;
        var next = u * u * curveStart + 2f * u * curveProgress * curveControl + curveProgress * curveProgress * curveEnd;

        var position = transform.position;
        var step = next - position;
        if (step.sqrMagnitude < 0.000001f) step = direction * 0.001f;
        var heading = step.normalized;

        if (Physics.SphereCast(position, HitRadius, heading, out var hit, step.magnitude, HitMask))
        {
            transform.position = hit.point;
            Impact(hit, heading);
            Die();
            return;
        }

        direction = heading;
        transform.SetPositionAndRotation(next, Quaternion.LookRotation(heading));
        // Reached the end without touching anything (target moved or died): carry on straight.
        if (curveProgress >= 1f) curving = false;
    }

    [HideFromIl2Cpp]
    private void Impact(RaycastHit hit, Vector3 heading)
    {
        var limb = hit.collider.GetComponent<Dam_EnemyDamageLimb>();
        var enemy = limb?.m_base?.Owner;

        if (kind.FlashRange > 0f)
            BurstFlash.Spawn(hit.point - heading * 0.3f, kind.FlashColor, kind.FlashRange, FlashIntensity, FlashSeconds);

        // The shooter's game deals all the damage; a teammate's copy only shows it.
        if (owner != null)
        {
            var source = owner.Cast<Agent>();
            if (limb != null && enemy != null && enemy.Alive)
            {
                var dealt = kind.DamageDie > 0 ? kind.DamageScale * UnityEngine.Random.Range(1, kind.DamageDie + 1) : damage;
                limb.BulletDamage(dealt, source, hit.point, heading, hit.normal,
                    allowDirectionalBonus: true, staggerMulti, precisionMulti, gearCategoryId);
            }
            if (kind.BlastRadius > 0f)
            {
                AreaDamage.Apply(hit.point, kind.BlastRadius, kind.BlastDamage, 0.4f, kind.BlastStagger,
                    source, spared: enemy, gearCategoryId);
            }
        }

        // Shards only come off enemies, and fly back out the way this one came in.
        if (kind.SplitInto != null && enemy != null)
        {
            for (var i = 0; i < kind.SplitCount; i++)
            {
                Spawn(kind.SplitInto, hit.point - heading * 0.4f, -heading, owner, 0f, staggerMulti, precisionMulti,
                    gearCategoryId, enemy.Pointer);
            }
        }
    }

    // The living enemy closest to the line of flight, within range and cone, that it can see.
    [HideFromIl2Cpp]
    private EnemyAgent? FindTarget()
    {
        var position = transform.position;
        EnemyAgent? best = null;
        var bestAngle = kind.SearchConeDegrees;
        foreach (var collider in Physics.OverlapSphere(position, kind.SearchRange, LayerManager.MASK_ENEMY_DAMAGABLE))
        {
            var limb = collider.GetComponent<Dam_EnemyDamageLimb>();
            var enemy = limb?.m_base?.Owner;
            if (enemy == null || !enemy.Alive || enemy == best || enemy.Pointer == ignoredEnemy) continue;

            var aim = enemy.AimTarget.position;
            var angle = Vector3.Angle(direction, aim - position);
            if (angle >= bestAngle) continue;
            if (Physics.Linecast(position, aim, LayerManager.MASK_WORLD)) continue; // behind a wall

            best = enemy;
            bestAngle = angle;
        }
        return best;
    }

    // Glow, trail, particles and body in one color, so a borrowed yellow seeker reads as blue plasma.
    private static void Tint(GameObject go, Color color)
    {
        // Each step on its own, so one that fails (and says which) doesn't stop the rest.
        TintStep("light", () =>
        {
            var targeting = go.GetComponent<ProjectileTargeting>();
            if (targeting != null && targeting.m_light != null)
                targeting.m_light.Color = color;
        });
        TintStep("trails", () =>
        {
            foreach (var trail in go.GetComponentsInChildren<TrailRenderer>(true))
            {
                trail.startColor = color;
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
        });
        // Every color slot of every material, particle renderers included - whatever the game's
        // shaders call them. Slots that sound like glow get a brighter value so the core still
        // reads as lit.
        TintStep("materials", () =>
        {
            var dump = tintDumped ? null : new System.Text.StringBuilder("GTFO.AbsurdWeapons projectile tint: ");
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.TryCast<TrailRenderer>() != null) continue;
                var material = renderer.material; // per-instance copy: other projectiles keep theirs
                if (material == null) continue;
                var shader = material.shader;
                dump?.Append($"[{renderer.name} shader '{(shader != null ? shader.name : "?")}':");
                var count = shader != null ? shader.GetPropertyCount() : 0;
                for (var i = 0; i < count; i++)
                {
                    if (shader!.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;
                    var property = shader.GetPropertyName(i);
                    var original = material.GetColor(property);
                    dump?.Append($" {property}={original}");
                    var glow = property.IndexOf("emis", StringComparison.OrdinalIgnoreCase) >= 0
                        || property.IndexOf("glow", StringComparison.OrdinalIgnoreCase) >= 0;
                    material.SetColor(property, new Color(color.r, color.g, color.b, original.a) * (glow ? 2f : 1f));
                }
                dump?.Append("] ");
            }
            if (dump != null)
            {
                tintDumped = true;
                Plugin.Logger.LogInfo(dump.ToString());
            }
        });
    }

    private static readonly HashSet<string> tintFailuresLogged = new();

    private static void TintStep(string step, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            if (tintFailuresLogged.Add(step))
                Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons couldn't tint a projectile's {step} (further failures not logged): {e}");
        }
    }

    // A random direction within `degrees` of `forward`.
    private static Vector3 Spread(Vector3 forward, float degrees)
    {
        if (degrees <= 0f) return forward;
        var offset = UnityEngine.Random.insideUnitCircle * degrees;
        var right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        right.Normalize();
        var up = Vector3.Cross(forward, right);
        return (Quaternion.AngleAxis(offset.x, up) * Quaternion.AngleAxis(offset.y, right) * forward).normalized;
    }

    // Stops the glow and particles but lets the trail fade out before the object goes.
    [HideFromIl2Cpp]
    private void Die()
    {
        dead = true;
        fadeLeft = FadeOutSeconds;
        foreach (var particles in GetComponentsInChildren<ParticleSystem>())
            particles.Stop();
        foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            renderer.enabled = false;
        foreach (var light in GetComponentsInChildren<Light>())
            light.enabled = false;
        var targeting = GetComponent<ProjectileTargeting>();
        if (targeting != null && targeting.m_light != null)
            targeting.m_light.enabled = false;
    }
}
