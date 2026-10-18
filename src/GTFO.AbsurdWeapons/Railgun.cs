using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using FX_EffectSystem;
using GTFO.API;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Railgun: one hitscan slug with infinite penetration. It goes through every enemy and every
/// wall along its line, out to 200 m, hitting each enemy once (through the limb it meets first,
/// so a head is still a head) via the normal bullet path, so it syncs. It's drawn as a white-hot
/// beam wrapped in a double helix that spins, widens and fades, with sparks wherever it punches
/// through a wall. Teammates are sent each shot and see the same beam.
/// </summary>
internal static class Railgun
{
    internal static ConfigEntry<float> Range = null!;
    private const string ShotEvent = "MindAttic.GTFO.AbsurdWeapons.RailShot";
    private const float BeamStartAhead = 1.2f; // metres past where the gun sits on screen

    internal static void Bind(ConfigFile config) =>
        Range = config.Bind("Railgun", "PenetrationRange", 200f, "Metres the piercing slug reaches.");

    internal static void Init()
    {
        NetworkAPI.RegisterEvent<ShotMessage>(ShotEvent, (_, message) =>
        {
            var start = new Vector3(message.X, message.Y, message.Z);
            var end = new Vector3(message.EndX, message.EndY, message.EndZ);
            RailBeam.Spawn(start, end);
            WallSparks(new Vector3(message.EyeX, message.EyeY, message.EyeZ), end);
        });
    }

    // One shot from the local shooter.
    internal static void Fire(PlayerAgent owner, Vector3 eye, Vector3 aim, float damage, float staggerMulti,
        float precisionMulti, uint gearCategoryId)
    {
        // Every enemy on the line, nearest limb first - walls don't stop it.
        var hits = new List<RaycastHit>(Physics.RaycastAll(eye, aim, Range.Value, LayerManager.MASK_ENEMY_DAMAGABLE));
        hits.Sort((a, b) => a.distance.CompareTo(b.distance));
        var struck = new HashSet<IntPtr>();
        foreach (var hit in hits)
        {
            var limb = hit.collider.GetComponent<Dam_EnemyDamageLimb>();
            var enemy = limb?.m_base?.Owner;
            if (limb == null || enemy == null || !enemy.Alive || !struck.Add(enemy.Pointer)) continue;
            limb.BulletDamage(damage, owner.Cast<Agent>(), hit.point, aim, hit.normal,
                allowDirectionalBonus: true, staggerMulti, precisionMulti, gearCategoryId);
        }
        LockDamage.Line(eye, aim, Range.Value, damage, owner.Cast<Agent>());

        // Pushed out ahead of the gun so the beam doesn't start in the player's face.
        var start = LaserGuns.ScreenStart(eye, aim) + aim * BeamStartAhead;
        var end = eye + aim * Range.Value;
        RailBeam.Spawn(start, end);
        WallSparks(eye, end);
        NetworkAPI.InvokeEvent(ShotEvent, new ShotMessage(start, end, eye));
    }

    // Sparks at every wall the slug punches through.
    private static void WallSparks(Vector3 from, Vector3 to)
    {
        try
        {
            var line = to - from;
            foreach (var hit in Physics.RaycastAll(from, line.normalized, line.magnitude, LayerManager.MASK_WORLD))
                FX_Manager.PlayEffect(false, FX_GroupName.Impact_Metal, hit.point, Quaternion.LookRotation(hit.normal), true);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons railgun sparks failed: {e.Message}");
        }
    }

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct ShotMessage
    {
        public readonly float X, Y, Z, EndX, EndY, EndZ, EyeX, EyeY, EyeZ;

        public ShotMessage(Vector3 start, Vector3 end, Vector3 eye)
        {
            (X, Y, Z) = (start.x, start.y, start.z);
            (EndX, EndY, EndZ) = (end.x, end.y, end.z);
            (EyeX, EyeY, EyeZ) = (eye.x, eye.y, eye.z);
        }
    }
}

/// <summary>
/// One railgun shot's look: a straight white-hot core and two strands spiralling around it in a
/// double helix. The helix spins, widens and the whole thing fades out over a moment.
/// </summary>
internal sealed class RailBeam : MonoBehaviour
{
    private const float Seconds = 0.8f;
    private const float HelixLength = 80f;     // the helix wraps the first stretch; the core runs the full range
    private const float TurnLength = 2.4f;     // metres per full turn (half as many spirals again)
    private const int PointsPerTurn = 12;
    private const float StartRadius = 0.04f, EndRadius = 0.3f;
    private const float SpinPerSecond = 720f;
    // Green-tinted and semi-transparent: alpha below 1 here, not just the time fade, so the beam
    // reads as translucent the instant it fires.
    private static readonly Color CoreColor = new(0.8f, 1f, 0.85f, 0.65f);
    private static readonly Color StrandA = new(0.2f, 1f, 0.35f, 0.6f);
    private static readonly Color StrandB = new(0.1f, 0.65f, 0.2f, 0.6f);
    private static Material? material;

    private LineRenderer core = null!, strandA = null!, strandB = null!;
    private EffectLight glow = null!;
    private Vector3 start, end;
    private float age;

    public RailBeam(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(Vector3 start, Vector3 end)
    {
        var go = new GameObject("GTFO.AbsurdWeapons Rail Beam");
        var beam = go.AddComponent<RailBeam>();
        beam.start = start;
        beam.end = end;
        beam.core = NewLine(go, 2);
        beam.core.SetPosition(0, start);
        beam.core.SetPosition(1, end);

        var helixPoints = Mathf.Max(2, Mathf.CeilToInt(Mathf.Min(HelixLength, Vector3.Distance(start, end)) / TurnLength * PointsPerTurn) + 1);
        beam.strandA = NewLine(new GameObject("Strand A") { transform = { parent = go.transform } }, helixPoints);
        beam.strandB = NewLine(new GameObject("Strand B") { transform = { parent = go.transform } }, helixPoints);

        var lightObject = new GameObject("Glow");
        lightObject.transform.SetParent(go.transform, false);
        lightObject.transform.position = start + (end - start).normalized * 1.5f;
        beam.glow = lightObject.AddComponent<EffectLight>();
        beam.glow.Color = StrandA;
        beam.glow.Range = 6f;
        beam.glow.Intensity = 6f;
        beam.glow.enabled = true;

        beam.Draw();
    }

    [HideFromIl2Cpp]
    private static LineRenderer NewLine(GameObject go, int points)
    {
        var line = go.AddComponent<LineRenderer>();
        line.positionCount = points;
        line.useWorldSpace = true;
        line.numCapVertices = 2;
        line.material = material ??= new Material(Shader.Find("Sprites/Default"));
        return line;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Seconds)
        {
            Destroy(gameObject);
            return;
        }
        try
        {
            Draw();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons rail beam failed: {e.Message}");
            Destroy(gameObject);
        }
    }

    [HideFromIl2Cpp]
    private void Draw()
    {
        var t = age / Seconds;
        var fade = 1f - t * t;

        core.startWidth = core.endWidth = Mathf.Lerp(0.05f, 0.01f, t);
        core.startColor = core.endColor = Fade(CoreColor, fade);
        glow.Intensity = 6f * fade;

        var line = end - start;
        var forward = line.normalized;
        var side = Vector3.Cross(Vector3.up, forward);
        if (side.sqrMagnitude < 0.001f) side = Vector3.right;
        side.Normalize();
        var up = Vector3.Cross(forward, side);

        var radius = Mathf.Lerp(StartRadius, EndRadius, Mathf.Sqrt(t));
        var spin = age * SpinPerSecond * Mathf.Deg2Rad;
        var count = strandA.positionCount;
        for (var i = 0; i < count; i++)
        {
            var along = i * (TurnLength / PointsPerTurn);
            var angle = along / TurnLength * Mathf.PI * 2f + spin;
            var center = start + forward * along;
            var offset = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
            strandA.SetPosition(i, center + offset * radius);
            strandB.SetPosition(i, center - offset * radius);
        }

        var width = Mathf.Lerp(0.03f, 0.012f, t);
        strandA.startWidth = strandA.endWidth = width;
        strandB.startWidth = strandB.endWidth = width;
        strandA.startColor = Fade(StrandA, fade);
        strandA.endColor = Fade(StrandA, 0f);
        strandB.startColor = Fade(StrandB, fade);
        strandB.endColor = Fade(StrandB, 0f);
    }

    private static Color Fade(Color color, float alpha) => new(color.r, color.g, color.b, color.a * alpha);
}
