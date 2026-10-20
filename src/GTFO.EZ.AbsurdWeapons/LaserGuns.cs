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
using MindAttic.GTFO.Shared;
using Player;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>How one laser gun's beam reaches and looks.</summary>
internal sealed record LaserKind(byte Id, uint GunId, float Range, float Width);

/// <summary>
/// The Laser Pistol and Laser Rifle: one continuous beam, drawn with the trip mine's own laser
/// material, from the barrel to whatever the crosshair is on, held for as long as the trigger is.
/// They don't shoot: the gun never fires (no bullets, muzzle flash, brass or kick); a steady hold that
/// never relaxes keeps it raised at hip height (GunInjector.AddSteadyHold). <see cref="LaserDriver"/> reads the trigger instead and, while it's held,
/// burns whatever enemy the beam's end is on every ShotDelay seconds (through the enemy limb's
/// normal bullet path, so it syncs). The beam follows the gun every frame; where it lands it burns
/// and sparks. Teammates are sent each tick and see the same beam.
/// </summary>
[HarmonyPatch]
internal static class LaserGuns
{
    // Built inside Bind(), not as static field initializers: those would run (as part of this
    // type's static constructor) the moment Bind() is first called, before Bind()'s own body had
    // assigned the config entries they'd read.
    internal static LaserKind Pistol = null!;
    internal static LaserKind Rifle = null!;

    internal static void Bind(ConfigFile config)
    {
        var beamRange = config.Bind("Laser Guns", "BeamRange", 70f, "Metres the beam reaches.");
        var beamWidth = config.Bind("Laser Guns", "BeamWidth", 0.025f, "The beam's visual width.");
        Pistol = new(1, GunInjector.LaserPistolId, Range: beamRange.Value, Width: beamWidth.Value);
        Rifle = new(2, GunInjector.LaserRifleId, Range: beamRange.Value, Width: beamWidth.Value);
    }

    internal static LaserKind? ForGun(uint id) => id == Pistol.GunId ? Pistol : id == Rifle.GunId ? Rifle : null;
    private static LaserKind? ForId(byte id) => id == Pistol.Id ? Pistol : id == Rifle.Id ? Rifle : null;

    private const string BeamEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.LaserBeam";

    // The bullet tracers of laser guns that have fired: muted, so every tick doesn't also streak a
    // vanilla tracer down the beam (it reads as a new laser spawning 20 times a second).
    // Held by reference (not just pointer) so a tracer can't be freed and its address handed to
    // some other FeedbackPlayer, which would then be muted too.
    private static readonly Dictionary<IntPtr, FeedbackPlayer> mutedTracers = new();

    internal static void Init()
    {
        RemoteEvents.Register<BeamMessage>(BeamEvent, (sender, message) =>
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
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons couldn't mute a laser gun's tracer: {e.Message}");
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

    // Where a beam fired from `eye` along `aim` appears to leave the gun on screen. `forward`
    // overrides how far out it starts (Chain Lightning's bolt pushes further than the lasers' beam).
    internal static Vector3 ScreenStart(Vector3 eye, Vector3 aim, float forward = BeamForward)
    {
        var right = Vector3.Cross(Vector3.up, aim);
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        right.Normalize();
        var down = -Vector3.Cross(aim, right).normalized;
        return eye + aim * forward + right * BeamRight + down * BeamDown;
    }

    // The beam from `eye` along `aim`: where it starts on screen, where it lands, what it hit.
    // `muzzle`: the gun in hand, so the beam leaves that model's own barrel (else a fixed spot
    // low and right of the eye).
    internal static (Vector3 start, Vector3 end, BeamHit hit, Vector3 normal, RaycastHit ray) Trace(LaserKind kind, Vector3 eye, Vector3 aim,
        PlayerAgent? owner = null, Transform? muzzle = null)
    {
        var start = muzzle != null ? Barrel.Start(owner, muzzle, eye, aim) : ScreenStart(eye, aim);
        if (!Physics.Raycast(eye, aim, out var hit, kind.Range, LayerManager.MASK_BULLETWEAPON_RAY))
            return (start, eye + aim * kind.Range, BeamHit.None, -aim, hit);
        var enemy = hit.collider.GetComponent<Dam_EnemyDamageLimb>() != null;
        return (start, hit.point, enemy ? BeamHit.Enemy : BeamHit.Surface, hit.normal, hit);
    }

    // One beam tick from the local shooter: damage to whatever enemy the beam is on, and the beam
    // kept lit (it follows the gun every frame by itself while ticks keep coming).
    // `dealDamage` false: just the beam kept lit and following the gun (every frame between ticks).
    internal static void Tick(LaserKind kind, PlayerAgent owner, Transform? muzzle, Vector3 eye, Vector3 aim,
        float damage, float staggerMulti, float precisionMulti, uint gearCategoryId, bool dealDamage = true)
    {
        var (start, end, hitKind, normal, hit) = Trace(kind, eye, aim, owner, muzzle);
        if (!dealDamage)
        {
            LaserBeam.Show(owner.Owner != null ? owner.Owner.Lookup : 0UL, kind, owner, start, end, hitKind, normal);
            return;
        }
        if (hitKind == BeamHit.Enemy)
        {
            var limb = hit.collider.GetComponent<Dam_EnemyDamageLimb>();
            if (limb != null && limb.m_base?.Owner != null && limb.m_base.Owner.Alive)
            {
                limb.BulletDamage(damage, owner.Cast<Agent>(), hit.point, aim, hit.normal,
                    allowDirectionalBonus: true, staggerMulti, precisionMulti, gearCategoryId);
            }
        }
        else if (hitKind == BeamHit.Surface)
        {
            LockDamage.TryHit(hit.collider, damage, owner.Cast<Agent>(), hit.point, aim, hit.normal);
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
    private const float HoldSeconds = 0.25f; // a teammate's copy hears only the ticks: a few ticks' grace, so it never blinks out mid-burn
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
        // A continuous beam loop at the gun (where the player hears it) while ticks keep coming.
        LoopSounds.Touch($"laser:{player}", Sounds.LaserLoopStart.Value, Sounds.LaserLoopStop.Value, start);
        if (!beams.TryGetValue(player, out var beam) || beam == null)
        {
            var go = new GameObject("GTFO.EZ.AbsurdWeapons Laser Beam");
            beam = go.AddComponent<LaserBeam>();
            beam.line = NewLine(go);

            var spot = new GameObject("GTFO.EZ.AbsurdWeapons Laser Burn");
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
            {
                var muzzle = owner!.Inventory?.WieldedItem?.TryCast<BulletWeapon>()?.MuzzleAlign;
                (start, end, hit, normal, _) = LaserGuns.Trace(kind, camera.Position, camera.Forward, owner, muzzle);
            }
            Draw();
            Burn();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons laser beam failed: {e.Message}");
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
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons laser sparks unavailable: {e.Message}");
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
            Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons laser beam material: {source}, shader '{material.shader?.name}'.");
        }
        return material;
    }
}

/// <summary>
/// Shared by every trigger-driven stream (lasers, Chain Lightning, Shrink Beam): whether the local
/// player's currently wielded item has been up long enough to count as settled into its aim
/// position, rather than still mid-raise from just being swapped to or coming back from a sprint.
/// </summary>
internal static class StreamReadiness
{
    private const float SettleSeconds = 0.3f;
    private static IntPtr wieldedItem;
    private static float wieldedSince;
    private static int lastAskedFrame = -1;

    // The drivers ask every frame the gun could fire; a frame they skipped (a sprint) means it was
    // lowered, so coming back up restarts the settle just like a swap does.
    internal static bool IsSettled(PlayerAgent player)
    {
        var item = player.Inventory?.WieldedItem;
        var pointer = item != null ? item.Pointer : IntPtr.Zero;
        var frame = Time.frameCount;
        if (pointer != wieldedItem || frame - lastAskedFrame > 1)
        {
            wieldedItem = pointer;
            wieldedSince = Time.time;
        }
        lastAskedFrame = frame;
        return Time.time - wieldedSince >= SettleSeconds;
    }

    // The stream holds off while the gun is lowered or hidden (a mantle, a ladder, an interaction -
    // the game's own guns can't fire then either), and while sprinting unless GTFO.EZ.AbsurdMoves is
    // installed with [Fire On The Move] Fire While Running Enabled on.
    internal static bool Blocked(PlayerAgent player)
    {
        var holder = player.FPItemHolder;
        if (holder != null && (holder.m_itemDownCounter > 0
            || holder.m_currentState is FPItemStateName.Down or FPItemStateName.Hidden))
            return true;
        return player.Locomotion != null && player.Locomotion.IsRunning && !(FireWhileRunning()?.Value ?? false);
    }

    private static ConfigEntry<bool>? fireWhileRunning;
    private static bool lookedUp;

    private static ConfigEntry<bool>? FireWhileRunning()
    {
        if (lookedUp) return fireWhileRunning;
        lookedUp = true;
        // Not installed, or switched off in its config ([0. Mod] Enabled): no setting, so no firing while running.
        SharedOwner.TryGetSetting("MindAttic.GTFO.EZ.AbsurdMoves", "Fire On The Move", "Fire While Running Enabled", out fireWhileRunning);
        return fireWhileRunning;
    }
}

/// <summary>
/// Drives the local player's laser beam from the trigger: while a laser gun is in hand and Fire is
/// held, a damage tick at the beam's end every <c>ShotDelay</c> seconds (the archetype's), at the
/// archetype's damage. The gun's own firing never runs (see <see cref="LaserGuns"/>).
/// </summary>
internal sealed class LaserDriver : MonoBehaviour
{
    private float nextTick;

    public LaserDriver(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        try
        {
            var player = PlayerManager.GetLocalPlayerAgent();
            if (player == null || !player.Alive || FocusStateManager.CurrentState != eFocusState.FPS) return;
            if (StreamReadiness.Blocked(player)) return;
            if (!StreamReadiness.IsSettled(player)) return; // not yet raised into its aim position
            var gun = player.Inventory?.WieldedItem?.TryCast<BulletWeapon>();
            var archetype = gun?.ArchetypeData;
            var kind = LaserGuns.ForGun(archetype?.persistentID ?? 0);
            if (gun == null || archetype == null || kind == null) return;
            if (!InputMapper.GetButtonKeyMouse(InputAction.Fire, eFocusState.FPS)) return;
            var camera = player.FPSCamera;
            if (camera == null) return;

            // Lit every frame the trigger is held, so it's one constant beam until it's released;
            // ammo goes by the time it's held, the damage comes on the ticks.
            if (!InfiniteAmmo.HasStreamAmmo(player) || !InfiniteAmmo.Drain(player, Time.deltaTime)) return; // out of ammo: no beam
            var damageTick = Time.time >= nextTick;
            if (damageTick) nextTick = Time.time + Mathf.Max(0.02f, archetype.ShotDelay);
            LaserGuns.Tick(kind, player, gun.MuzzleAlign, camera.Position, camera.Forward, archetype.Damage, 1f, 1f, kind.GunId, damageTick);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons laser driver failed: {e.Message}");
            nextTick = Time.time + 1f;
        }
    }
}
