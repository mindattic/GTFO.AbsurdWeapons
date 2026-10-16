using System;
using System.Collections.Generic;
using AK;
using AssetShards;
using FX_EffectSystem;
using GTFO.API;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Grenade Launcher: a C-Foam launcher whose globs are swapped for lock melters rigged to
/// blow. The launcher's own firing (pressure, ammo, sound) is untouched; each glob it would launch
/// becomes a grenade instead. A grenade that hits an enemy explodes at once. One that misses
/// bounces off whatever it hits, flashing red faster and faster, and explodes 3 seconds after it
/// was fired. The blast damages and staggers enemies only, through the bullet path, on the
/// shooter's game; teammates are sent each launch and detonation and see the same grenades.
/// </summary>
[HarmonyPatch]
internal static class GrenadeLauncher
{
    internal const float Speed = 24f;           // launch speed, m/s
    internal const float FuseSeconds = 3f;
    internal const float BlastRadius = 5f;
    internal const float BlastDamage = 40f;     // at the center, falling to 25% at the edge
    internal const float BlastEdgeFraction = 0.25f;
    internal const float BlastStagger = 5f;
    private const float MinSecondsBetween = 0.5f; // one grenade per pull: the launcher's burst extras are dropped

    private const string LaunchEvent = "MindAttic.GTFO.AbsurdWeapons.GrenadeLaunch";
    private const string DetonateEvent = "MindAttic.GTFO.AbsurdWeapons.GrenadeDetonate";

    private static float lastLaunch = -10f;
    private static uint nextId;

    // Set while the launcher itself is firing, so glue from anything else (a C-foam mine, say)
    // stays glue even with the launcher in hand.
    private static int launcherFiring;
    private static bool strayGlueLogged;

    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.FireBurst))]
    [HarmonyPrefix]
    private static void BeforeFireBurst() => launcherFiring++;

    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.FireBurst))]
    [HarmonyFinalizer]
    private static void AfterFireBurst() => launcherFiring = Math.Max(0, launcherFiring - 1);

    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.FireSingle))]
    [HarmonyPrefix]
    private static void BeforeFireSingle() => launcherFiring++;

    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.FireSingle))]
    [HarmonyFinalizer]
    private static void AfterFireSingle() => launcherFiring = Math.Max(0, launcherFiring - 1);

    internal static void Init()
    {
        NetworkAPI.RegisterEvent<LaunchMessage>(LaunchEvent, (_, message) =>
            LauncherGrenade.Spawn(message.Id, null, new Vector3(message.X, message.Y, message.Z),
                new Vector3(message.VelX, message.VelY, message.VelZ)));
        NetworkAPI.RegisterEvent<DetonateMessage>(DetonateEvent, (_, message) =>
            LauncherGrenade.Detonate(message.Id, new Vector3(message.X, message.Y, message.Z)));
    }

    // Every glob the C-Foam launcher would fire goes through here (the local player's only). Ours
    // become grenades.
    [HarmonyPatch(typeof(ProjectileManager), nameof(ProjectileManager.WantToFireGlue))]
    [HarmonyPrefix]
    private static bool BeforeFireGlue(PlayerAgent owner, Vector3 pos, Vector3 velocity)
    {
        try
        {
            if (owner == null || !owner.IsLocallyOwned) return true;
            var wielded = owner.Inventory?.WieldedItem;
            if (wielded == null || wielded.GearCategoryData?.persistentID != GunInjector.GrenadeLauncherId) return true;
            if (launcherFiring == 0)
            {
                if (!strayGlueLogged)
                {
                    strayGlueLogged = true;
                    Plugin.Logger.LogInfo("GTFO.AbsurdWeapons Grenade Launcher: glue fired by something other than the launcher stays glue.");
                }
                return true;
            }
            if (Time.time - lastLaunch < MinSecondsBetween) return false;
            lastLaunch = Time.time;

            var aim = owner.FPSCamera != null ? owner.FPSCamera.Forward : velocity.normalized;
            var launch = aim * Speed;
            var id = ((uint)(owner.Owner?.PlayerSlotIndex() ?? 0) << 24) | (++nextId & 0xFFFFFF);
            LauncherGrenade.Spawn(id, owner, pos, launch);
            NetworkAPI.InvokeEvent(LaunchEvent, new LaunchMessage(id, pos, launch));
            CellSound.Post(EVENTS.FRAGGRENADETHROW, pos);
            return false;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Grenade Launcher failed to fire: {e}");
            return false;
        }
    }

    // Infinite ammo: while the local player holds the launcher, the tool ammo is kept full. (On
    // Update - Unity calls it; UpdateLocal is inlined into it, so a patch there never runs.)
    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.Update))]
    [HarmonyPostfix]
    private static void KeepToolAmmoFull(GlueGun __instance)
    {
        try
        {
            if (__instance.GearCategoryData?.persistentID != GunInjector.GrenadeLauncherId) return;
            if (__instance.Owner == null || !__instance.Owner.IsLocallyOwned) return;
            var storage = PlayerBackpackManager.LocalBackpack?.AmmoStorage;
            var ammo = storage?.ClassAmmo;
            if (storage == null || ammo == null || ammo.IsFull) return;
            storage.SetAmmo(AmmoType.Class, ammo.AmmoMaxCap);
            storage.UpdateAllAmmoUI();
            storage.NeedsSync = true;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons couldn't refill the Grenade Launcher: {e.Message}");
        }
    }

    internal static void SendDetonate(uint id, Vector3 position) =>
        NetworkAPI.InvokeEvent(DetonateEvent, new DetonateMessage(id, position));

    // Plain fields so GTFO-API can copy them as raw bytes.
    private readonly struct LaunchMessage
    {
        public readonly uint Id;
        public readonly float X, Y, Z, VelX, VelY, VelZ;

        public LaunchMessage(uint id, Vector3 position, Vector3 velocity)
        {
            Id = id;
            (X, Y, Z) = (position.x, position.y, position.z);
            (VelX, VelY, VelZ) = (velocity.x, velocity.y, velocity.z);
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
/// One grenade in flight: a lock melter's meshes, flown by hand (gravity, sphere casts) so it
/// bounces off the level and never touches players. It blinks a red light (and beeps) faster and
/// faster until its fuse runs out, or until it hits an enemy (on the shooter's game). Teammates'
/// copies only bounce; they explode when the shooter's game says so.
/// </summary>
internal sealed class LauncherGrenade : MonoBehaviour
{
    private const float Radius = 0.12f;
    private const float Gravity = 9.81f;
    private const float Bounciness = 0.55f;
    private const float RestSpeed = 1.2f;
    private const float RemoteGraceSeconds = 0.75f; // a teammate's copy waits this long past the fuse for the detonation message
    private static readonly Color FlashColor = new(1f, 0.05f, 0.05f, 1f);

    private static readonly string[] LockMelterPrefabs =
    {
        "Assets/AssetPrefabs/Items/Consumables/LockMelter/Consumable_LockMelter_Pickup.prefab",
        "Assets/AssetPrefabs/Items/Consumables/LockMelter/Consumable_LockMelter.prefab",
        "Assets/AssetPrefabs/Items/Consumables/LockMelter/Consumable_LockMelter_FirstPerson.prefab",
    };

    private static readonly Dictionary<uint, LauncherGrenade> live = new();
    // Teammates' copies that blew on their own fuse before the shooter's detonation arrived.
    private static readonly HashSet<uint> selfDetonated = new();
    private static bool modelLogged;
    private static Material? ledMaterial;

    private uint id;
    private PlayerAgent? owner; // null on a teammate's (visual-only) copy
    private Vector3 velocity, spinAxis;
    private float age, spinSpeed, nextFlash, lastBounceSound;
    private bool resting, exploded, lit;
    private EffectLight light = null!;
    private LineRenderer led = null!;

    public LauncherGrenade(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(uint id, PlayerAgent? owner, Vector3 position, Vector3 velocity)
    {
        var go = new GameObject("GTFO.AbsurdWeapons Launcher Grenade");
        go.transform.position = position;
        BuildModel(go.transform);

        var grenade = go.AddComponent<LauncherGrenade>();
        grenade.id = id;
        grenade.owner = owner;
        grenade.velocity = velocity;
        grenade.spinAxis = UnityEngine.Random.onUnitSphere;
        grenade.spinSpeed = UnityEngine.Random.Range(360f, 900f);

        var lightObject = new GameObject("Flash");
        lightObject.transform.SetParent(go.transform, false);
        grenade.light = lightObject.AddComponent<EffectLight>();
        grenade.light.Color = FlashColor;
        grenade.light.Range = 2.5f;
        grenade.light.Intensity = 0f;
        grenade.light.enabled = true;

        grenade.led = lightObject.AddComponent<LineRenderer>();
        grenade.led.positionCount = 2;
        grenade.led.useWorldSpace = true;
        grenade.led.numCapVertices = 4;
        grenade.led.startWidth = grenade.led.endWidth = 0.09f;
        grenade.led.startColor = grenade.led.endColor = FlashColor;
        grenade.led.material = LedMaterial();
        grenade.led.enabled = false;

        live[id] = grenade;
    }

    // A teammate's detonation message: blow that grenade up where their game says it went off.
    [HideFromIl2Cpp]
    internal static void Detonate(uint id, Vector3 position)
    {
        if (live.TryGetValue(id, out var grenade) && grenade != null)
        {
            grenade.transform.position = position;
            grenade.Explode(fromMessage: true);
        }
        else if (!selfDetonated.Remove(id))
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
            Flash();

            if (owner == null ? age >= GrenadeLauncher.FuseSeconds + RemoteGraceSeconds : age >= GrenadeLauncher.FuseSeconds)
            {
                Explode();
                return;
            }

            if (!resting) Fly(dt);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons launcher grenade failed: {e.Message}");
            exploded = true;
            live.Remove(id);
            Destroy(gameObject);
        }
    }

    [HideFromIl2Cpp]
    private void Fly(float dt)
    {
        velocity += Vector3.down * (Gravity * dt);
        var position = transform.position;
        var step = velocity * dt;
        var distance = step.magnitude;
        if (distance < 0.0001f) return;
        var direction = step / distance;

        // An enemy in the way: it goes off on contact (the shooter's game decides).
        if (owner != null && Physics.SphereCast(position, Radius, direction, out var enemyHit, distance, LayerManager.MASK_ENEMY_DAMAGABLE))
        {
            transform.position = enemyHit.point;
            Explode();
            return;
        }

        if (Physics.SphereCast(position, Radius, direction, out var hit, distance, LayerManager.MASK_WORLD))
        {
            transform.position = hit.point + hit.normal * (Radius + 0.01f);
            velocity = Vector3.Reflect(velocity, hit.normal) * Bounciness;
            spinSpeed *= 0.7f;
            if (velocity.magnitude > 2f && age - lastBounceSound > 0.1f)
            {
                lastBounceSound = age;
                CellSound.Post(EVENTS.FRAGGRENADEBOUNCE, hit.point);
            }
            // Settled on a floor: it stops and waits out its fuse.
            if (hit.normal.y > 0.7f && velocity.magnitude < RestSpeed)
            {
                resting = true;
                velocity = Vector3.zero;
            }
            return;
        }

        transform.position = position + step;
        transform.Rotate(spinAxis, spinSpeed * dt, Space.World);
    }

    // A red blink that speeds up from ~2 a second to ~16 as the fuse burns down, with a beep on
    // each flash while they're still far enough apart to hear.
    [HideFromIl2Cpp]
    private void Flash()
    {
        var progress = Mathf.Clamp01(age / GrenadeLauncher.FuseSeconds);
        var interval = Formulas.GrenadeFlashInterval(age, GrenadeLauncher.FuseSeconds);
        if (age >= nextFlash)
        {
            nextFlash = age + interval;
            lit = true;
            if (interval > 0.12f) CellSound.Post(EVENTS.DEPLOYER_BEEP_TARGET_ACQUIRED, transform.position);
        }
        else if (lit && age >= nextFlash - interval * 0.55f)
        {
            lit = false;
        }

        var position = transform.position;
        light.transform.position = position + Vector3.up * 0.1f;
        light.Intensity = lit ? 3f + 3f * progress : 0f;
        led.enabled = lit;
        if (lit)
        {
            led.SetPosition(0, position - Vector3.up * 0.02f);
            led.SetPosition(1, position + Vector3.up * 0.02f);
        }
    }

    [HideFromIl2Cpp]
    private void Explode(bool fromMessage = false)
    {
        if (exploded) return;
        exploded = true;
        live.Remove(id);
        var position = transform.position;
        Destroy(gameObject);

        if (owner == null)
        {
            // A teammate's copy: if this was its own fuse, the shooter's message is still coming.
            if (!fromMessage) selfDetonated.Add(id);
            Explosion.Show(position);
            return;
        }
        GrenadeLauncher.SendDetonate(id, position);
        Explosion.Show(position);
        AreaDamage.Apply(position, GrenadeLauncher.BlastRadius, GrenadeLauncher.BlastDamage, GrenadeLauncher.BlastEdgeFraction,
            GrenadeLauncher.BlastStagger, owner.Cast<Agents.Agent>(), null, GunInjector.GrenadeLauncherId);
    }

    // The lock melter's meshes, copied onto the grenade - just the look, none of the prefab's
    // pickup or interaction scripts. Falls back to a plain capsule if the prefab isn't loaded.
    [HideFromIl2Cpp]
    private static void BuildModel(Transform parent)
    {
        string source = "fallback capsule";
        try
        {
            foreach (var path in LockMelterPrefabs)
            {
                var prefab = AssetShardManager.GetLoadedAsset<GameObject>(path);
                if (prefab == null) continue;
                var root = prefab.transform;
                var copied = 0;
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || renderer == null) continue;
                    var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var part = new GameObject("Part");
                    part.transform.SetParent(parent, false);
                    part.transform.localPosition = matrix.GetColumn(3);
                    part.transform.localRotation = matrix.rotation;
                    part.transform.localScale = matrix.lossyScale;
                    part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                    copied++;
                }
                if (copied == 0) continue;
                source = $"{path} ({copied} meshes)";
                return;
            }

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            UnityEngine.Object.Destroy(capsule.GetComponent<Collider>());
            capsule.transform.SetParent(parent, false);
            capsule.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
        }
        finally
        {
            if (!modelLogged)
            {
                modelLogged = true;
                Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons launcher grenade model: {source}.");
            }
        }
    }

    [HideFromIl2Cpp]
    private static Material LedMaterial() =>
        ledMaterial ??= new Material(Shader.Find("Sprites/Default")) { color = FlashColor };
}

/// <summary>The blast's look and sound: the trip mine's explosion effect, a flash and the frag grenade bang.</summary>
internal static class Explosion
{
    private const string TripMineExplosion = "Assets/AssetPrefabs/FX_Effects/FX_Tripmine.prefab";
    private static FX_Pool? pool;
    private static bool effectFailed, effectLogged;

    internal static void Show(Vector3 position)
    {
        CellSound.Post(EVENTS.FRAGGRENADEEXPLODE, position);
        BurstFlash.Spawn(position + Vector3.up * 0.3f, new Color(1f, 0.55f, 0.2f), 10f, 10f, 0.35f);
        if (effectFailed) return;
        try
        {
            if (pool == null) pool = MineDeployerInstance_Detonate_Explosive.s_explosionPool; // Unity null: destroyed at level cleanup
            if (pool == null)
            {
                var prefab = AssetShardManager.GetLoadedAsset<GameObject>(TripMineExplosion);
                if (prefab != null) pool = FX_Manager.GetEffectPool(prefab);
            }
            if (!effectLogged)
            {
                effectLogged = true;
                Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons grenade explosion effect: {(pool != null ? "trip mine explosion" : "none loaded (flash only)")}.");
            }
            pool?.AquireEffect()?.Play(null, position, Quaternion.identity);
        }
        catch (Exception e)
        {
            effectFailed = true;
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons grenade explosion effect unavailable: {e.Message}");
        }
    }
}
