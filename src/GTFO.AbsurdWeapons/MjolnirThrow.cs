using System;
using System.Collections.Generic;
using Agents;
using GameData;
using Gear;
using GTFO.API;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Player;
using SNetwork;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// Mjolnir's throw: release a full charge and the hammer flies from your hand, tumbling end over
/// end, out to 25 m (or the first wall), then comes back to you. Along the way it hits every
/// enemy it passes through and throws lightning pulses - chain lightning from the hammer to the
/// nearest enemy, jumping on to two more. Damage happens on the thrower's game only, through the
/// bullet path; teammates are sent the throw and see the same hammer (and the bolts, which
/// ChainLightning sends).
/// </summary>
[HarmonyPatch]
internal static class MjolnirThrow
{
    internal const float Speed = 26f;
    internal const float ReturnSpeed = 32f;
    internal const float Reach = 25f;
    internal const float SpinDegreesPerSecond = 1080f;
    internal const float HitRadius = 0.6f;
    internal const float HitDamage = 20f;
    internal const float HitStagger = 4f;
    internal const float PulseSeconds = 0.12f;
    internal const float PulseRange = 6f;
    internal const float PulseDamage = 6f;
    internal const int PulseJumps = 2;
    private const float FullChargeLeeway = 0.95f;
    private const string ThrowEvent = "MindAttic.GTFO.AbsurdWeapons.MjolnirThrow";

    private static float chargeStarted = -1f;

    internal static void Init()
    {
        NetworkAPI.RegisterEvent<ThrowMessage>(ThrowEvent, (sender, message) =>
            ThrownHammer.Spawn(null, PlayerFor(sender), new Vector3(message.X, message.Y, message.Z),
                new Vector3(message.DirX, message.DirY, message.DirZ), null));
    }

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.ChangeState))]
    [HarmonyPostfix]
    private static void AfterChangeState(MeleeWeaponFirstPerson __instance, eMeleeWeaponState newState)
    {
        try
        {
            var archetype = __instance.MeleeArchetypeData;
            if (archetype == null || archetype.persistentID != MeleeInjector.MjolnirId) return;
            var owner = __instance.Owner;
            if (owner == null || !owner.IsLocallyOwned) return;

            if (newState is eMeleeWeaponState.AttackChargeUpLeft or eMeleeWeaponState.AttackChargeUpRight)
            {
                chargeStarted = Time.time;
                return;
            }
            if (newState is not (eMeleeWeaponState.AttackChargeReleaseLeft or eMeleeWeaponState.AttackChargeReleaseRight)) return;
            if (chargeStarted < 0f) return;
            var held = Time.time - chargeStarted;
            chargeStarted = -1f;

            var animations = GameDataBlockBase<MeleeAnimationSetDataBlock>.GetBlock(archetype.MeleeAnimationSet);
            var full = animations != null ? animations.MaxDamageChargeTime : 1f;
            if (!Formulas.IsFullCharge(held, full, FullChargeLeeway)) return;

            var camera = owner.FPSCamera;
            if (camera == null || ThrownHammer.LocalInFlight) return; // one throw at a time
            var direction = camera.Forward;
            var start = camera.Position + direction * 0.8f + Vector3.down * 0.15f;
            ThrownHammer.Spawn(owner, owner, start, direction, __instance.gameObject);
            NetworkAPI.InvokeEvent(ThrowEvent, new ThrowMessage(start, direction));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Mjolnir throw failed: {e}");
        }
    }

    // The player agent of the teammate who sent a message, so their hammer flies back to them.
    private static PlayerAgent? PlayerFor(ulong lookup)
    {
        foreach (var player in PlayerManager.PlayerAgentsInLevel)
            if (player != null && player.Owner != null && player.Owner.Lookup == lookup) return player;
        return null;
    }

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct ThrowMessage
    {
        public readonly float X, Y, Z, DirX, DirY, DirZ;

        public ThrowMessage(Vector3 start, Vector3 direction)
        {
            (X, Y, Z) = (start.x, start.y, start.z);
            (DirX, DirY, DirZ) = (direction.x, direction.y, direction.z);
        }
    }
}

/// <summary>
/// One thrown Mjolnir: the hammer's meshes, copied from the held weapon (or a stand-in), flown out
/// and back by hand, spinning end over end with a blue glow.
/// </summary>
internal sealed class ThrownHammer : MonoBehaviour
{
    private static readonly Color GlowColor = new(0.45f, 0.75f, 1f);

    private PlayerAgent? owner;   // the thrower on their own game (deals damage); null on teammates' copies
    private PlayerAgent? thrower; // whose hand it returns to
    private Vector3 origin, direction, spinAxis;
    private bool returning;
    private float travelled, nextPulse, age;
    private readonly HashSet<IntPtr> hitOut = new(), hitBack = new();
    private EffectLight glow = null!;

    // The local player's own throw is out (the next can't start until it's back).
    internal static bool LocalInFlight => localThrow != null;
    private static ThrownHammer? localThrow;

    public ThrownHammer(IntPtr ptr) : base(ptr) { }

    private void OnDestroy()
    {
        if (localThrow == this) localThrow = null;
    }

    [HideFromIl2Cpp]
    internal static void Spawn(PlayerAgent? owner, PlayerAgent? thrower, Vector3 start, Vector3 direction, GameObject? heldModel)
    {
        var go = new GameObject("GTFO.AbsurdWeapons Thrown Mjolnir");
        go.transform.position = start;
        go.transform.rotation = Quaternion.LookRotation(direction);
        CopyModel(heldModel, go.transform);

        var hammer = go.AddComponent<ThrownHammer>();
        hammer.owner = owner;
        hammer.thrower = thrower;
        if (owner != null) localThrow = hammer;
        hammer.origin = start;
        hammer.direction = direction.normalized;
        var right = Vector3.Cross(Vector3.up, hammer.direction);
        hammer.spinAxis = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right; // end over end

        var lightObject = new GameObject("Glow");
        lightObject.transform.SetParent(go.transform, false);
        hammer.glow = lightObject.AddComponent<EffectLight>();
        hammer.glow.Color = GlowColor;
        hammer.glow.Range = 4f;
        hammer.glow.Intensity = 4f;
        hammer.glow.enabled = true;
    }

    private void Update()
    {
        try
        {
            var dt = Time.deltaTime;
            age += dt;
            var position = transform.position;

            Vector3 next;
            if (!returning)
            {
                var step = MjolnirThrow.Speed * dt;
                next = position + direction * step;
                travelled += step;
                if (Physics.Linecast(position, next, out var wall, LayerManager.MASK_WORLD))
                {
                    next = wall.point - direction * 0.2f;
                    returning = true;
                }
                else if (travelled >= MjolnirThrow.Reach) returning = true;
            }
            else
            {
                // Home back to the thrower's hand (or where it was thrown from).
                var hand = thrower != null && thrower.Alive ? thrower.Position + Vector3.up * 1.3f : origin;
                var toHand = hand - position;
                var step = MjolnirThrow.ReturnSpeed * dt;
                if (toHand.magnitude <= Mathf.Max(step, 1f) || age > 6f)
                {
                    Destroy(gameObject);
                    return;
                }
                next = position + toHand.normalized * step;
            }

            if (owner != null) Strike(position, next);
            transform.position = next;
            transform.Rotate(spinAxis, MjolnirThrow.SpinDegreesPerSecond * dt, Space.World);
            glow.Intensity = 3f + Mathf.PerlinNoise(age * 20f, 0f) * 4f;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons thrown Mjolnir failed: {e.Message}");
            Destroy(gameObject);
        }
    }

    // On the thrower's game: every enemy the hammer passes through (once each way), and a
    // lightning pulse every so often.
    [HideFromIl2Cpp]
    private void Strike(Vector3 from, Vector3 to)
    {
        var hitSet = returning ? hitBack : hitOut;
        var line = to - from;
        var length = line.magnitude;
        if (length > 0.0001f)
        {
            foreach (var hit in Physics.SphereCastAll(from, MjolnirThrow.HitRadius, line / length, length, LayerManager.MASK_ENEMY_DAMAGABLE))
            {
                var limb = hit.collider.GetComponent<Dam_EnemyDamageLimb>();
                var enemy = limb?.m_base?.Owner;
                if (limb == null || enemy == null || !enemy.Alive || !hitSet.Add(enemy.Pointer)) continue;
                var point = hit.point == Vector3.zero ? limb.transform.position : hit.point;
                limb.BulletDamage(MjolnirThrow.HitDamage, owner!.Cast<Agent>(), point, line / length, -line / length,
                    allowDirectionalBonus: false, MjolnirThrow.HitStagger, 1f, MeleeInjector.MjolnirId);
            }
        }

        if (age < nextPulse) return;
        nextPulse = age + MjolnirThrow.PulseSeconds;
        ChainLightning.Pulse(owner!, to, MjolnirThrow.PulseRange, MjolnirThrow.PulseDamage, MjolnirThrow.PulseJumps, MeleeInjector.MjolnirId);
    }

    // The held hammer's meshes (positioned relative to the weapon's root), drawn on the default
    // layer; a stand-in block if there's nothing to copy (a teammate's game).
    [HideFromIl2Cpp]
    private static void CopyModel(GameObject? source, Transform parent)
    {
        var copied = 0;
        if (source != null)
        {
            var root = source.transform;
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled) continue;
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
        }
        if (copied > 0) return;

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        UnityEngine.Object.Destroy(head.GetComponent<Collider>());
        head.transform.SetParent(parent, false);
        head.transform.localScale = new Vector3(0.18f, 0.18f, 0.32f);
        var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        UnityEngine.Object.Destroy(handle.GetComponent<Collider>());
        handle.transform.SetParent(parent, false);
        handle.transform.localPosition = new Vector3(0f, -0.25f, 0f);
        handle.transform.localScale = new Vector3(0.05f, 0.2f, 0.05f);
    }
}
