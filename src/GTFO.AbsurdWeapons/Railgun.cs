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
/// One railgun shot's look: no core, just two strands spiralling around the line of fire in a
/// double helix, built to Quake 3's own rail spiral (ioquake3 cg_weapons.c, CG_RailTrail):
/// RADIUS 4 units, a puff every SPACING 5 units turning 10 degrees each (36 to a turn, so 180
/// units a turn), every puff drifting outward at 6 units/s, no spin, and each lasting
/// 600 ms + 0.5 ms per unit along the trail - so it fades from the gun outward. Converted at this
/// mod's id-game scale (56 units = 1.83 m, as for the Doom plasma).
/// </summary>
internal sealed class RailBeam : MonoBehaviour
{
    private const float MetresPerUnit = 1.83f / 56f;
    private const float StartRadius = 4f * MetresPerUnit;    // RADIUS 4
    private const float DriftPerSecond = 6f * MetresPerUnit; // trDelta = axis * 6
    private const float TurnLength = 180f * MetresPerUnit;   // SPACING 5 x 36 steps of 10 deg
    private const int PointsPerTurn = 36;                    // one per Quake puff
    private const float BaseSeconds = 0.6f;                  // the + 600 ms
    private const float SecondsPerMetre = 0.0005f / MetresPerUnit; // the (i >> 1) ms per unit along
    private const int FadeKeys = 8;                          // a Gradient takes at most 8
    // Green-tinted and semi-transparent: alpha below 1 here, not just the time fade, so the beam
    // reads as translucent the instant it fires.
    private static readonly Color StrandA = new(0.2f, 1f, 0.35f, 0.6f);
    private static readonly Color StrandB = new(0.1f, 0.65f, 0.2f, 0.6f);
    // Quake's rail leaves a ragged spiral of separate puffs, not a clean ribbon: each point keeps its
    // own scatter, and a faint haze of smoke drifts off the line and spreads faster than the spiral.
    private static readonly Color Smoke = new(0.6f, 0.75f, 0.62f, 0.16f);
    private const float Scatter = 0.35f;          // each point's radius, up to this share either way
    private const float SmokeDriftPerSecond = 0.6f;
    private static Material? material;

    private LineRenderer strandA = null!, strandB = null!, smoke = null!;
    private float[] scatterA = Array.Empty<float>(), scatterB = Array.Empty<float>();
    private Vector3[] smokeDrift = Array.Empty<Vector3>();
    private EffectLight glow = null!;
    private Vector3 start, end;
    private float age, length;

    public RailBeam(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(Vector3 start, Vector3 end)
    {
        var go = new GameObject("GTFO.AbsurdWeapons Rail Beam");
        var beam = go.AddComponent<RailBeam>();
        beam.start = start;
        beam.end = end;
        beam.length = Vector3.Distance(start, end);

        var helixPoints = Mathf.Max(2, Mathf.CeilToInt(beam.length / TurnLength * PointsPerTurn) + 1);
        beam.strandA = NewLine(new GameObject("Strand A") { transform = { parent = go.transform } }, helixPoints);
        beam.strandB = NewLine(new GameObject("Strand B") { transform = { parent = go.transform } }, helixPoints);
        beam.smoke = NewLine(new GameObject("Smoke") { transform = { parent = go.transform } }, helixPoints);
        beam.scatterA = new float[helixPoints];
        beam.scatterB = new float[helixPoints];
        beam.smokeDrift = new Vector3[helixPoints];
        for (var i = 0; i < helixPoints; i++)
        {
            beam.scatterA[i] = 1f + UnityEngine.Random.Range(-Scatter, Scatter);
            beam.scatterB[i] = 1f + UnityEngine.Random.Range(-Scatter, Scatter);
            beam.smokeDrift[i] = UnityEngine.Random.insideUnitSphere;
        }

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
        if (age >= BaseSeconds + length * SecondsPerMetre) // the farthest puff gone
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
        glow.Intensity = 6f * Mathf.Clamp01(1f - age / BaseSeconds);

        var line = end - start;
        var forward = line.normalized;
        var side = Vector3.Cross(Vector3.up, forward);
        if (side.sqrMagnitude < 0.001f) side = Vector3.right;
        side.Normalize();
        var up = Vector3.Cross(forward, side);

        // Every puff drifts straight out from the line at the same rate: the spiral just widens.
        var radius = StartRadius + DriftPerSecond * age;
        var count = strandA.positionCount;
        for (var i = 0; i < count; i++)
        {
            var along = Mathf.Min(length, i * (TurnLength / PointsPerTurn));
            var angle = along / TurnLength * Mathf.PI * 2f;
            var center = start + forward * along;
            var offset = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
            strandA.SetPosition(i, center + offset * (radius * scatterA[i]));
            strandB.SetPosition(i, center - offset * (radius * scatterB[i]));
            smoke.SetPosition(i, center + smokeDrift[i] * (StartRadius + SmokeDriftPerSecond * age));
        }

        var width = 2.2f * MetresPerUnit; // the puffs' radius 1.1
        strandA.startWidth = strandA.endWidth = width;
        strandB.startWidth = strandB.endWidth = width;
        smoke.startWidth = smoke.endWidth = 0.12f + 0.25f * age;
        smoke.colorGradient = FadeAlong(Smoke);
        strandA.colorGradient = FadeAlong(StrandA);
        strandB.colorGradient = FadeAlong(StrandB);
    }

    // Each point fades linearly over its own lifetime (Quake's LE_FADE_RGB), longer the farther
    // along the trail it is - so the fade sweeps out from the gun.
    [HideFromIl2Cpp]
    private Gradient FadeAlong(Color color)
    {
        var colors = new GradientColorKey[2] { new(color, 0f), new(color, 1f) };
        var alphas = new GradientAlphaKey[FadeKeys];
        for (var k = 0; k < FadeKeys; k++)
        {
            var t = k / (float)(FadeKeys - 1);
            var life = BaseSeconds + t * length * SecondsPerMetre;
            alphas[k] = new GradientAlphaKey(color.a * Mathf.Clamp01(1f - age / life), t);
        }
        var gradient = new Gradient();
        gradient.SetKeys(colors, alphas);
        return gradient;
    }

    private static Color Fade(Color color, float alpha) => new(color.r, color.g, color.b, color.a * alpha);
}
