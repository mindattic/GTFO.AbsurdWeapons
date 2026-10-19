using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using FX_EffectSystem;
using GTFO.API;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// The Rocket Launcher: Quake 1's own id Software code (<c>W_FireRocket</c> / <c>T_MissileTouch</c> /
/// <c>T_RadiusDamage</c>), with Doom's rocket's straight, gravity-free flight (<c>MT_ROCKET</c>'s
/// <c>MF_NOGRAVITY</c>) - neither game's rocket drops or bounces. Every shot (SeekerGuns' ray hook
/// calls <see cref="Launch"/>) leaves the barrel and flies dead straight, kicking off falling sparks
/// and a trail of dust (the game's own concrete-impact puff) as it goes, to the first enemy or wall
/// in its way, then explodes there - same blast code as the Grenade Launcher (<see cref="AreaDamage"/>,
/// <see cref="Explosion"/>), plus the game's own <c>DamageUtil.DoExplosionDamage</c> so the blast
/// catches nearby players too - shooter, bots and teammates alike, Quake/Doom style. Quake's own
/// numbers: 120-damage splash with a half-distance falloff (<c>T_RadiusDamage</c>),
/// searched out to 160 units - zero past that, a third of the center damage left at the edge (the
/// same 160 u / a-third the Grenade Launcher's blast already uses) - and a 0.8 s refire. The gun's
/// own firing - sound, recoil, ammo (infinite, like every gun here) - stays vanilla. The shooter's
/// game deals the damage, through the bullet path so it syncs; everyone else is sent the launch and
/// the blast and sees the same rocket.
/// It can't aim down sights: right-click instead detonates the shooter's oldest rocket still in
/// flight, right where it is - one per click, so a volley can be set off as a chain
/// (<see cref="RocketProjectile"/>).
/// </summary>
[HarmonyPatch]
internal static class RocketLauncher
{
    internal static ConfigEntry<float> Speed = null!;           // launch speed, m/s
    internal static ConfigEntry<float> Lifetime = null!;        // seconds before an unexploded rocket just disappears
    internal static ConfigEntry<float> BlastRadius = null!;
    internal static ConfigEntry<float> BlastDamage = null!;     // at the blast's center, falling to BlastEdgeFraction at the edge
    internal static ConfigEntry<float> BlastEdgeFraction = null!;
    internal static ConfigEntry<float> BlastStagger = null!;
    internal static ConfigEntry<float> GunShotDelay = null!;
    internal static ConfigEntry<int> GunClipSize = null!;

    private const string LaunchEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.RocketLaunch";
    private const string DetonateEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.RocketDetonate";

    internal static void Bind(ConfigFile config)
    {
        Speed = config.Bind("Rocket Launcher", "LaunchSpeed", 32.7f, "Launch speed along the aim, m/s (Quake 1: 1000 units/s).");
        Lifetime = config.Bind("Rocket Launcher", "Lifetime", 10f, "Seconds a rocket flies before it disappears unexploded if it never hits anything (Quake 1: 10 s auto-remove).");
        BlastRadius = config.Bind("Rocket Launcher", "BlastRadius", 5.2f, "Metres the blast reaches (Quake 1: its 120-damage, half-distance falloff is searched out to 160 units).");
        BlastDamage = config.Bind("Rocket Launcher", "BlastDamage", 110f, "Damage at the blast's center (Quake 1: 110 on a direct hit, 120 at the splash's own center).");
        BlastEdgeFraction = config.Bind("Rocket Launcher", "BlastEdgeFraction", 0.33f, "Share of damage left at the blast's edge (Quake 1's falloff leaves a third at 160 units - same as the Grenade Launcher's).");
        BlastStagger = config.Bind("Rocket Launcher", "BlastStagger", 5f, "Blast stagger multiplier.");
        GunShotDelay = config.Bind("Rocket Launcher", "GunShotDelay", 0.8f, "Seconds between shots (semi-auto; Quake 1: 0.8 s).");
        GunClipSize = config.Bind("Rocket Launcher", "GunClipSize", 5, "Magazine size.");
    }

    internal static void Init()
    {
        NetworkAPI.RegisterEvent<LaunchMessage>(LaunchEvent, (_, message) =>
            RocketProjectile.Spawn(message.Id, null, new Vector3(message.X, message.Y, message.Z),
                new Vector3(message.DirX, message.DirY, message.DirZ)));
        NetworkAPI.RegisterEvent<DetonateMessage>(DetonateEvent, (_, message) =>
            RocketProjectile.Detonate(message.Id, new Vector3(message.X, message.Y, message.Z)));
    }

    private static uint nextId;

    // One shot from the local shooter's Rocket Launcher: a rocket out of the muzzle, straight along
    // the aim to whatever the crosshair is on - no lob, no gravity - shown to teammates too.
    internal static void Launch(PlayerAgent owner, Vector3 position, Vector3 aim, uint gearCategoryId)
    {
        var direction = aim.normalized;
        var id = ((uint)(owner.Owner?.PlayerSlotIndex() ?? 0) << 24) | (++nextId & 0xFFFFFF);
        RocketProjectile.Spawn(id, owner, position, direction, gearCategoryId);
        NetworkAPI.InvokeEvent(LaunchEvent, new LaunchMessage(id, position, direction));
    }

    // No aiming down sights: right-click is the remote detonator instead. The game asks this of
    // every equippable, including ones still being built (seen at startup) or already destroyed,
    // so it must never throw: an exception here escapes into the il2cpp trampoline.
    [HarmonyPatch(typeof(ItemEquippable), nameof(ItemEquippable.AimingAllowed), MethodType.Getter)]
    [HarmonyPostfix]
    private static void NoAiming(ItemEquippable __instance, ref bool __result)
    {
        if (__result && IsRocketLauncher(__instance)) __result = false;
    }

    // Right-click this frame while the local player holds the Rocket Launcher.
    internal static bool DetonatePressed(PlayerAgent owner) =>
        owner.IsLocallyOwned && FocusStateManager.CurrentState == eFocusState.FPS
        && IsRocketLauncher(owner.Inventory?.WieldedItem)
        && InputMapper.GetButtonDownKeyMouse(InputAction.Aim, eFocusState.FPS);

    // False for anything missing, half-built or destroyed rather than throwing.
    private static bool IsRocketLauncher(ItemEquippable? item)
    {
        if (item == null || item.Pointer == IntPtr.Zero) return false;
        try
        {
            return item.GearCategoryData?.persistentID == GunInjector.RocketLauncherId;
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static void SendDetonate(uint id, Vector3 position) =>
        NetworkAPI.InvokeEvent(DetonateEvent, new DetonateMessage(id, position));

    // Plain fields so GTFO-API can copy them as raw bytes.
    private readonly struct LaunchMessage
    {
        public readonly uint Id;
        public readonly float X, Y, Z, DirX, DirY, DirZ;

        public LaunchMessage(uint id, Vector3 position, Vector3 direction)
        {
            Id = id;
            (X, Y, Z) = (position.x, position.y, position.z);
            (DirX, DirY, DirZ) = (direction.x, direction.y, direction.z);
        }
    }

    private readonly struct DetonateMessage
    {
        public readonly uint Id;
        public readonly float X, Y, Z;

        public DetonateMessage(uint id, Vector3 position)
        {
            Id = id;
            (X, Y, Z) = (position.x, position.y, position.z);
        }
    }
}

/// <summary>
/// One rocket in flight: a small hand-built missile, flown by hand in a dead straight line (no
/// gravity, no bounce - unlike <see cref="LauncherGrenade"/>) until it touches an enemy or the
/// world, kicking off a falling spark every few centimetres. Only the shooter's own copy decides when it
/// hits something (through the same enemy/world sphere cast its blast then uses); a teammate's copy
/// is purely visual and flies on - rockets never bounce, so there's nothing for it to simulate -
/// until the shooter's detonation message (<see cref="RocketLauncher.SendDetonate"/>) snaps it to
/// the real impact point and shows the same blast.
/// </summary>
internal sealed class RocketProjectile : MonoBehaviour
{
    private const float Radius = 0.15f;
    private const float SparkInterval = 0.05f;
    private const float DustInterval = 0.08f;
    private static int HitMask => LayerManager.MASK_ENEMY_DAMAGABLE | LayerManager.MASK_WORLD;

    private static readonly Dictionary<uint, RocketProjectile> live = new();

    private uint id, gearCategoryId;
    private PlayerAgent? owner; // null on a teammate's (visual-only) copy
    private Vector3 direction;
    private float age, nextSpark, nextDust, launchedAt;
    private static int detonatedFrame = -1; // a right-click detonates one rocket, whichever runs first that frame
    private bool exploded;

    public RocketProjectile(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(uint id, PlayerAgent? owner, Vector3 position, Vector3 direction, uint gearCategoryId = 0)
    {
        var go = new GameObject("GTFO.EZ.AbsurdWeapons Rocket");
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        BuildModel(go.transform);

        var rocket = go.AddComponent<RocketProjectile>();
        rocket.id = id;
        rocket.gearCategoryId = gearCategoryId;
        rocket.owner = owner;
        rocket.direction = direction.normalized;
        rocket.launchedAt = Time.time;

        live[id] = rocket;
    }

    // A teammate's detonation message: blow that rocket up where the shooter's game says it hit.
    [HideFromIl2Cpp]
    internal static void Detonate(uint id, Vector3 position)
    {
        if (live.TryGetValue(id, out var rocket) && rocket != null)
        {
            rocket.transform.position = position;
            rocket.Explode();
        }
        else
        {
            Explosion.Show(position); // it never arrived here; still show the blast
        }
    }

    private void Update()
    {
        if (exploded) return;
        try
        {
            var dt = Time.deltaTime;
            age += dt;
            if (age >= RocketLauncher.Lifetime.Value) { Remove(); return; } // Quake 1: flies 10 s, then just disappears

            Spark();
            Dust();

            var position = transform.position;

            // Right-click: the shooter's oldest rocket in flight stops dead and blows up right
            // where it is - one per click.
            if (owner != null && detonatedFrame != Time.frameCount && RocketLauncher.DetonatePressed(owner))
            {
                detonatedFrame = Time.frameCount;
                var oldest = Oldest(owner);
                if (oldest != null)
                {
                    RocketLauncher.SendDetonate(oldest.id, oldest.transform.position);
                    oldest.Explode();
                    if (oldest == this) return;
                }
            }

            var step = RocketLauncher.Speed.Value * dt;

            // Only the shooter's own copy ever decides it hit something; a teammate's copy has
            // nothing to simulate (no bounce, no gravity) and just waits for Detonate above.
            if (owner != null && Physics.SphereCast(position, Radius, direction, out var hit, step, HitMask))
            {
                transform.position = hit.point;
                RocketLauncher.SendDetonate(id, hit.point);
                Explode();
                return;
            }

            transform.position = position + direction * step;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons rocket failed: {e.Message}");
            Remove();
        }
    }

    // The shooter's rocket launched longest ago that's still flying.
    [HideFromIl2Cpp]
    private static RocketProjectile? Oldest(PlayerAgent owner)
    {
        RocketProjectile? oldest = null;
        foreach (var rocket in live.Values)
        {
            if (rocket == null || rocket.exploded || rocket.owner == null || rocket.owner.Pointer != owner.Pointer) continue;
            if (oldest == null || rocket.launchedAt < oldest.launchedAt) oldest = rocket;
        }
        return oldest;
    }

    // No smoke trail - just a spray of sparks every few centimetres of flight, kicked out behind
    // and to the sides, that fall away under gravity as the rocket keeps going.
    [HideFromIl2Cpp]
    private void Spark()
    {
        if (age < nextSpark) return;
        nextSpark = age + SparkInterval;
        var position = transform.position;
        var count = UnityEngine.Random.Range(1, 3);
        for (var i = 0; i < count; i++)
        {
            var scatter = UnityEngine.Random.insideUnitSphere * 2f;
            var velocity = -direction * UnityEngine.Random.Range(1f, 3f) + scatter;
            FallingSpark.Spawn(position, velocity);
        }
    }

    // Dust behind it - the game's own concrete-impact puff, no decal (there's nothing to mark, it's
    // not hitting anything), just the cloud of debris that effect already throws up.
    [HideFromIl2Cpp]
    private void Dust()
    {
        if (age < nextDust) return;
        nextDust = age + DustInterval;
        try
        {
            FX_Manager.PlayEffect(false, FX_GroupName.Impact_Concrete, transform.position - direction * 0.25f,
                Quaternion.LookRotation(-direction), false);
        }
        catch (Exception)
        {
            // Dust is a nicety; the sparks still show.
        }
    }

    [HideFromIl2Cpp]
    private void Explode()
    {
        if (exploded) return;
        exploded = true;
        live.Remove(id);
        var position = transform.position;
        Destroy(gameObject);

        Explosion.Show(position);
        if (owner == null) return; // a teammate's copy: visual only, no damage
        AreaDamage.Apply(position, RocketLauncher.BlastRadius.Value, RocketLauncher.BlastDamage.Value, RocketLauncher.BlastEdgeFraction.Value,
            RocketLauncher.BlastStagger.Value, owner.Cast<Agents.Agent>(), null, gearCategoryId);

        // Quake/Doom rockets hurt whoever's standing in the blast - shooter, bots and teammates
        // alike - not just enemies: the game's own explosion-damage sweep (MASK_EXPLOSION_TARGETS is
        // players and destructibles, not enemies - those are the AreaDamage.Apply above, with their
        // own falloff), blocked by world geometry.
        try
        {
            DamageUtil.DoExplosionDamage(position, RocketLauncher.BlastRadius.Value, RocketLauncher.BlastDamage.Value,
                LayerManager.MASK_EXPLOSION_TARGETS, LayerManager.MASK_EXPLOSION_BLOCKERS, false, 0f);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons rocket couldn't damage nearby players: {e.Message}");
        }
    }

    [HideFromIl2Cpp]
    private void Remove()
    {
        live.Remove(id);
        Destroy(gameObject);
    }

    // A small hand-built missile: a dark, elongated body with a bright tip, long axis along local
    // forward (the object's own rotation already points along the flight direction).
    [HideFromIl2Cpp]
    private static void BuildModel(Transform parent)
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        UnityEngine.Object.Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(parent, false);
        // Scale is applied in the capsule's own unrotated space (long axis Y), then the rotation
        // below turns that Y onto local forward (Z) - so the length goes on Y, not Z.
        body.transform.localScale = new Vector3(0.1f, 0.22f, 0.1f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // capsule's long axis (Y) onto forward (Z)
        var bodyRenderer = body.GetComponent<Renderer>();
        if (bodyRenderer != null) bodyRenderer.material.color = new Color(0.12f, 0.12f, 0.12f);

        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        UnityEngine.Object.Destroy(tip.GetComponent<Collider>());
        tip.transform.SetParent(parent, false);
        tip.transform.localPosition = Vector3.forward * 0.22f;
        tip.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
        var tipRenderer = tip.GetComponent<Renderer>();
        if (tipRenderer != null) tipRenderer.material.color = new Color(0.6f, 0.08f, 0.05f);
    }
}

/// <summary>
/// One ember kicked off a flying rocket: a tiny bright point that falls under gravity, dimming out
/// over its short life - no trail, just a shower of sparks peeling away behind the rocket.
/// </summary>
internal sealed class FallingSpark : MonoBehaviour
{
    private const float Lifetime = 0.5f;
    private const float Gravity = 9.8f;
    private static readonly Color SparkColor = new(1f, 0.6f, 0.2f);

    private Vector3 velocity;
    private float age;
    private EffectLight light = null!;

    public FallingSpark(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(Vector3 position, Vector3 velocity)
    {
        var go = new GameObject("GTFO.EZ.AbsurdWeapons Rocket Spark");
        go.transform.position = position;
        var spark = go.AddComponent<FallingSpark>();
        spark.velocity = velocity;
        spark.light = go.AddComponent<EffectLight>();
        spark.light.Color = SparkColor;
        spark.light.Range = 1.5f;
        spark.light.Intensity = 2f;
        spark.light.enabled = true;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }
        velocity += Vector3.down * (Gravity * Time.deltaTime);
        transform.position += velocity * Time.deltaTime;
        light.Intensity = 2f * (1f - age / Lifetime);
    }
}
