using System;
using System.Collections.Generic;
using Agents;
using AssetShards;
using BepInEx.Configuration;
using Feedback;
using FX_EffectSystem;
using Gear;
using GTFO.API;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>How one laser gun's beam reaches and looks.</summary>
internal sealed record LaserKind(byte Id, uint GunId, float Range, float Width);

/// <summary>
/// The Laser Pistol and Laser Rifle: one continuous beam, drawn with the trip mine's own laser
/// material, from the barrel to whatever the crosshair is on, held for as long as the trigger is.
/// The gun fires full-auto very fast and every shot is a damage tick: a ray from the eye, damage
/// (through the enemy limb's normal bullet path, so it syncs) to whatever enemy it touches. The
/// beam itself is one line that stays up between ticks and follows the gun every frame; the
/// vanilla bullet tracer is muted so the ticks don't streak. Where it lands it burns and sparks.
/// Teammates are sent each tick and see the same beam.
/// </summary>
[HarmonyPatch]
internal static class LaserGuns
{
    internal static ConfigEntry<float> BeamRange = null!;
    internal static ConfigEntry<float> BeamWidth = null!;

    internal static void Bind(ConfigFile config)
    {
        BeamRange = config.Bind("Laser Guns", "BeamRange", 70f, "Metres the beam reaches.");
        BeamWidth = config.Bind("Laser Guns", "BeamWidth", 0.025f, "The beam's visual width.");
    }

    internal static readonly LaserKind Pistol = new(1, GunInjector.LaserPistolId, Range: BeamRange.Value, Width: BeamWidth.Value);
    internal static readonly LaserKind Rifle = new(2, GunInjector.LaserRifleId, Range: BeamRange.Value, Width: BeamWidth.Value);

    internal static LaserKind? ForGun(uint id) => id == Pistol.GunId ? Pistol : id == Rifle.GunId ? Rifle : null;
    private static LaserKind? ForId(byte id) => id == Pistol.Id ? Pistol : id == Rifle.Id ? Rifle : null;

    private const string BeamEvent = "MindAttic.GTFO.AbsurdWeapons.LaserBeam";

    // The bullet tracers of laser guns that have fired: muted, so every tick doesn't also streak a
    // vanilla tracer down the beam (it reads as a new laser spawning 20 times a second).
    // Held by reference (not just pointer) so a tracer can't be freed and its address handed to
    // some other FeedbackPlayer, which would then be muted too.
    private static readonly Dictionary<IntPtr, FeedbackPlayer> mutedTracers = new();

    internal static void Init()
    {
        NetworkAPI.RegisterEvent<BeamMessage>(BeamEvent, (sender, message) =>
        {
            var kind = ForId(message.Kind);
            if (kind != null)
                LaserBeam.Show(sender, kind, null, new Vector3(message.X, message.Y, message.Z), new Vector3(message.EndX, message.EndY, message.EndZ),
                    (BeamHit)message.Hit, new Vector3(message.NormalX, message.NormalY, message.NormalZ));
        });
    }

    // Called as a laser gun fires: its tracer goes on the muted list.
    internal static void MuteTracer(BulletWeapon weapon)
    {
        try
        {
            var tracer = weapon.m_vfxTracer;
            if (tracer != null) mutedTracers[tracer.Pointer] = tracer;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons couldn't mute a laser gun's tracer: {e.Message}");
        }
    }

    [HarmonyPatch(typeof(FeedbackPlayer), nameof(FeedbackPlayer.DoPlay))]
    [HarmonyPrefix]
    private static bool BeforeFeedbackPlay(FeedbackPlayer __instance) => !mutedTracers.ContainsKey(__instance.Pointer);

    // Where the beam visibly starts, relative to the eye: low and to the right, where the gun sits
    // on screen. The first-person gun is drawn with its own camera settings, so its muzzle's real
    // position can sit almost on the eye line - and a beam from there runs straight away from the
    // eye and shrinks to a dot.
    private const float BeamDown = 0.22f, BeamRight = 0.12f, BeamForward = 0.45f;

    // Where a beam fired from `eye` along `aim` appears to leave the gun on screen.
    internal static Vector3 ScreenStart(Vector3 eye, Vector3 aim)
    {
        var right = Vector3.Cross(Vector3.up, aim);
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        right.Normalize();
        var down = -Vector3.Cross(aim, right).normalized;
        return eye + aim * BeamForward + right * BeamRight + down * BeamDown;
    }

    // The beam from `eye` along `aim`: where it starts on screen, where it lands, what it hit.
    internal static (Vector3 start, Vector3 end, BeamHit hit, Vector3 normal, RaycastHit ray) Trace(LaserKind kind, Vector3 eye, Vector3 aim)
    {
        var start = ScreenStart(eye, aim);
        if (!Physics.Raycast(eye, aim, out var hit, kind.Range, LayerManager.MASK_BULLETWEAPON_RAY))
            return (start, eye + aim * kind.Range, BeamHit.None, -aim, hit);
        var enemy = hit.collider.GetComponent<Dam_EnemyDamageLimb>() != null;
        return (start, hit.point, enemy ? BeamHit.Enemy : BeamHit.Surface, hit.normal, hit);
    }

    // One beam tick from the local shooter: damage to whatever enemy the beam is on, and the beam
    // kept lit (it follows the gun every frame by itself while ticks keep coming).
    internal static void Tick(LaserKind kind, PlayerAgent owner, Transform? muzzle, Vector3 eye, Vector3 aim,
        float damage, float staggerMulti, float precisionMulti, uint gearCategoryId)
    {
        var (start, end, hitKind, normal, hit) = Trace(kind, eye, aim);
        if (hitKind == BeamHit.Enemy)
        {
            var limb = hit.collider.GetComponent<Dam_EnemyDamageLimb>();
            if (limb != null && limb.m_base?.Owner != null && limb.m_base.Owner.Alive)
            {
                limb.BulletDamage(damage, owner.Cast<Agent>(), hit.point, aim, hit.normal,
                    allowDirectionalBonus: true, staggerMulti, precisionMulti, gearCategoryId);
            }
        }

        var lookup = owner.Owner != null ? owner.Owner.Lookup : 0UL;
        LaserBeam.Show(lookup, kind, owner, start, end, hitKind, normal);
        NetworkAPI.InvokeEvent(BeamEvent, new BeamMessage(kind.Id, start, end, hitKind, normal));
    }

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct BeamMessage
    {
        public readonly byte Kind, Hit;
        public readonly float X, Y, Z, EndX, EndY, EndZ, NormalX, NormalY, NormalZ;

        public BeamMessage(byte kind, Vector3 start, Vector3 end, BeamHit hit, Vector3 normal)
        {
            Kind = kind;
            Hit = (byte)hit;
            (X, Y, Z) = (start.x, start.y, start.z);
            (EndX, EndY, EndZ) = (end.x, end.y, end.z);
            (NormalX, NormalY, NormalZ) = (normal.x, normal.y, normal.z);
        }
    }
}

internal enum BeamHit : byte { None, Surface, Enemy }

/// <summary>
/// One player's laser beam: a single line renderer with the trip mine's laser material, lit while
/// ticks keep coming (the trigger is held) and hidden a moment after they stop. The shooter's own
/// beam is re-aimed every frame so it stays locked to the gun and crosshair. Where it lands it
/// burns: a white-hot spot, a flickering orange glow and a steady spray of the game's own sparks.
/// </summary>
internal sealed class LaserBeam : MonoBehaviour
{
    private const float HoldSeconds = 0.15f; // a few ticks' grace, so the beam never blinks out mid-burn
    private const float SparkSeconds = 0.06f;
    private const string TripMinePrefab = "Assets/AssetPrefabs/Items/Tools/MineDeployer/TripMine_1.prefab";
    private static readonly Color BeamColor = new(1f, 0.15f, 0.1f, 1f);
    private static readonly Color BurnColor = new(1f, 0.45f, 0.1f, 1f);
    private static readonly Color HotColor = new(1f, 0.85f, 0.6f, 1f);

    private static readonly Dictionary<ulong, LaserBeam> beams = new();
    private static Material? material;
    private static bool materialLogged, sparkFailed;

    private LineRenderer line = null!;
    private LineRenderer hotSpot = null!;
    private EffectLight glow = null!;
    private LaserKind kind = null!;
    private PlayerAgent? owner; // set for the local shooter's own beam: re-aimed every frame
    private BeamHit hit;
    private Vector3 start, end, normal;
    private float lastTick, nextSpark;
    private int sparkCount;

    public LaserBeam(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Show(ulong player, LaserKind kind, PlayerAgent? owner, Vector3 start, Vector3 end, BeamHit hit, Vector3 normal)
    {
        if (!beams.TryGetValue(player, out var beam) || beam == null)
        {
            var go = new GameObject("GTFO.AbsurdWeapons Laser Beam");
            beam = go.AddComponent<LaserBeam>();
            beam.line = NewLine(go);

            var spot = new GameObject("GTFO.AbsurdWeapons Laser Burn");
            spot.transform.SetParent(go.transform, false);
            beam.hotSpot = NewLine(spot);
            beam.glow = spot.AddComponent<EffectLight>();
            beam.glow.Color = BurnColor;
            beam.glow.Range = 3f;
            beam.glow.Intensity = 0f;
            beam.glow.enabled = true;
            beams[player] = beam;
        }

        beam.kind = kind;
        beam.owner = owner;
        beam.start = start;
        beam.end = end;
        beam.hit = hit;
        beam.normal = normal;
        beam.line.startWidth = kind.Width;
        beam.line.endWidth = kind.Width;
        beam.line.startColor = BeamColor;
        beam.line.endColor = BeamColor;
        beam.line.enabled = true;
        beam.lastTick = Time.time;
        beam.Draw();
    }

    [HideFromIl2Cpp]
    private static LineRenderer NewLine(GameObject go)
    {
        var line = go.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.numCapVertices = 4;
        line.material = BeamMaterial();
        line.enabled = false;
        return line;
    }

    private void Update()
    {
        try
        {
            if (line == null || !line.enabled) return;
            if (Time.time - lastTick > HoldSeconds)
            {
                line.enabled = false;
                hotSpot.enabled = false;
                glow.Intensity = 0f;
                return;
            }

            // Our own beam follows the gun and crosshair every frame, not just every tick.
            var camera = owner?.FPSCamera;
            if (camera != null)
                (start, end, hit, normal, _) = LaserGuns.Trace(kind, camera.Position, camera.Forward);
            Draw();
            Burn();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons laser beam failed: {e.Message}");
            if (line != null) line.enabled = false;
        }
    }

    [HideFromIl2Cpp]
    private void Draw()
    {
        line.SetPosition(0, start);
        line.SetPosition(1, end);

        if (hit == BeamHit.None)
        {
            hotSpot.enabled = false;
            glow.Intensity = 0f;
            return;
        }

        // A white-hot spot where it lands, flickering.
        var size = kind.Width * UnityEngine.Random.Range(5f, 9f);
        var into = (end - start).normalized;
        hotSpot.startWidth = size;
        hotSpot.endWidth = size * 0.6f;
        hotSpot.startColor = HotColor;
        hotSpot.endColor = BurnColor;
        hotSpot.SetPosition(0, end - into * 0.02f);
        hotSpot.SetPosition(1, end + normal * 0.03f);
        hotSpot.enabled = true;

        glow.transform.position = end + normal * 0.15f;
        glow.Intensity = UnityEngine.Random.Range(2.5f, 5f);
    }

    // The game's own impact effects at the burn point: sparks off anything, and on an enemy its
    // body hit too. Every few, a scorch decal.
    [HideFromIl2Cpp]
    private void Burn()
    {
        if (hit == BeamHit.None || sparkFailed || Time.time < nextSpark) return;
        nextSpark = Time.time + SparkSeconds;
        try
        {
            var rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.01f ? normal : Vector3.up);
            var decal = sparkCount++ % 6 == 0;
            FX_Manager.PlayEffect(false, FX_GroupName.Impact_Metal, end, rotation, decal);
            if (hit == BeamHit.Enemy && sparkCount % 3 == 0)
                FX_Manager.PlayEffect(false, FX_GroupName.Impact_EnemyBody, end, rotation, false);
        }
        catch (Exception e)
        {
            sparkFailed = true;
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons laser sparks unavailable: {e.Message}");
        }
    }

    // The trip mine's own laser material, so the beam renders like the game's lasers: from the
    // loaded trip mine prefab, else a trip mine already placed in the level, else a plain fallback.
    [HideFromIl2Cpp]
    private static Material BeamMaterial()
    {
        if (material != null) return material;

        string source;
        try
        {
            var prefab = AssetShardManager.GetLoadedAsset<GameObject>(TripMinePrefab);
            material = prefab?.GetComponentInChildren<MineDeployerInstance_Detect_Laser>(true)?.m_lineRenderer?.sharedMaterial;
            source = "trip mine prefab";
        }
        catch (Exception)
        {
            source = "";
        }

        if (material == null)
        {
            var placed = UnityEngine.Object.FindObjectOfType<MineDeployerInstance_Detect_Laser>();
            material = placed?.m_lineRenderer?.sharedMaterial;
            source = "placed trip mine";
        }
        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default")) { color = BeamColor };
            source = "fallback (Sprites/Default)";
        }

        if (!materialLogged)
        {
            materialLogged = true;
            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons laser beam material: {source}, shader '{material.shader?.name}'.");
        }
        return material;
    }
}
