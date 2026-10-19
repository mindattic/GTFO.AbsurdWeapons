using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using Enemies;
using FX_EffectSystem;
using GTFO.API;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Shrink Beam: a continuous ray (<see cref="ShrinkBeamDriver"/>; the gun itself never fires)
/// that holds on whatever's under the crosshair and steadily shrinks it - smaller and slower by the
/// same share of its own full size - until it shrinks to <see cref="MinScale"/> and gibs. Once
/// it's at <see cref="StepOnScale"/> or smaller, getting within <see cref="StepOnRadius"/> of it and
/// looking down at it gibs it too - you've stepped on it. Stop holding the beam on it
/// and, after <see cref="RegrowDelaySeconds"/> off it, it grows back to full size at its own pace: a
/// shrink has to be finished in one sustained hold, not nibbled away at across several bursts. The
/// shrink and regrow are cosmetic (scale and move speed aren't
/// networked, so every client that sees the beam's line - the shooter's own tick, or the message
/// everyone else gets - runs the same shrink math on its own copy of whatever that line is on);
/// the kill itself goes through the shooter's own bullet path, so it syncs like any other hit.
/// </summary>
internal static class ShrinkBeam
{
    private static ConfigEntry<float> Range = null!;
    internal static ConfigEntry<float> ShrinkPerSecond = null!;
    internal static ConfigEntry<float> RegrowDelaySeconds = null!;
    internal static ConfigEntry<float> RegrowPerSecond = null!;
    internal static ConfigEntry<float> MinScale = null!;
    internal static ConfigEntry<float> PopDamage = null!;
    internal static ConfigEntry<float> StepOnScale = null!;
    internal static ConfigEntry<float> StepOnRadius = null!;
    internal static ConfigEntry<float> StepOnLookConeDegrees = null!;
    internal static ConfigEntry<float> GunShotDelay = null!;
    internal static ConfigEntry<int> GunClipSize = null!;

    private const string BeamEvent = "MindAttic.GTFO.AbsurdWeapons.ShrinkBeam";
    // How far from the received beam's end point a receiving client looks for the enemy it's on.
    private const float RemoteFindRadius = 0.6f;
    private static readonly Color PopColor = new(0.3f, 1f, 0.4f);

    internal static void Bind(ConfigFile config)
    {
        Range = config.Bind("Shrink Beam", "Range", 40f, "Metres the beam reaches.");
        ShrinkPerSecond = config.Bind("Shrink Beam", "ShrinkPerSecond", 0.25f, "Share of full size (and speed) removed per second the beam holds on an enemy.");
        RegrowDelaySeconds = config.Bind("Shrink Beam", "RegrowDelaySeconds", 1f, "Seconds off the beam before a shrunk enemy starts growing back.");
        RegrowPerSecond = config.Bind("Shrink Beam", "RegrowPerSecond", 0.15f, "Share of full size regained per second once it starts growing back.");
        MinScale = config.Bind("Shrink Beam", "MinScale", 0.33f, "Share of full size at which it gibs.");
        PopDamage = config.Bind("Shrink Beam", "PopDamage", 99999f, "Damage dealt on the pop (enough to kill anything outright).");
        StepOnScale = config.Bind("Shrink Beam", "StepOnScale", 0.5f, "At or below this share of full size you can step on it: get within StepOnRadius and look down at it and it gibs.");
        StepOnRadius = config.Bind("Shrink Beam", "StepOnRadius", 1f, "Metres from a small-enough enemy within which looking down at it steps on it.");
        StepOnLookConeDegrees = config.Bind("Shrink Beam", "StepOnLookConeDegrees", 35f, "How close to your crosshair a small-enough enemy has to be for you to count as looking down at it.");
        GunShotDelay = config.Bind("Shrink Beam", "GunShotDelay", 0.08f, "Seconds between beam ticks (it never actually fires - the stream is trigger-driven).");
        GunClipSize = config.Bind("Shrink Beam", "GunClipSize", 100, "Magazine size.");
    }

    internal static void Init()
    {
        NetworkAPI.RegisterEvent<BeamMessage>(BeamEvent, (sender, message) =>
        {
            var start = new Vector3(message.X0, message.Y0, message.Z0);
            var end = new Vector3(message.X1, message.Y1, message.Z1);
            ShrinkStream.Spawn(start, end, sender);
            var enemy = NearestEnemy(end, RemoteFindRadius);
            if (enemy != null && enemy.Alive) Shrink(enemy, GunShotDelay.Value);
        });
    }

    // Keyed by the enemy's own Il2Cpp pointer (its GameObject never changes while it's alive).
    private sealed class Shrunk
    {
        public EnemyAgent Enemy = null!;
        public float Scale = 1f;
        public Vector3 BaseScale = Vector3.one; // its own size before the beam touched it
        public float BaseRunSpeed;
        public bool CapturedBase;
        public float LastHit;
    }

    private static readonly Dictionary<IntPtr, Shrunk> shrunk = new();

    // One beam update from the local shooter, called every frame so the beam's start always sits
    // right at the gun's own barrel on screen, not just wherever it was at the last shrink tick.
    // `applyShrink` false: just the beam kept lit and following the gun (every frame between
    // ticks) - no shrink math, no network message (same split as the laser beam's).
    internal static void Tick(PlayerAgent owner, Vector3 eye, Vector3 aim, Transform? muzzle, float dt, uint gearCategoryId, bool applyShrink = true)
    {
        var start = muzzle != null ? Barrel.Start(owner, muzzle, eye, aim) : eye + aim * 0.5f;
        var end = eye + aim * Range.Value;
        EnemyAgent? hitEnemy = null;
        if (Physics.Raycast(eye, aim, out var hit, Range.Value, LayerManager.MASK_BULLETWEAPON_RAY))
        {
            end = hit.point;
            hitEnemy = hit.collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner;
        }

        if (!applyShrink)
        {
            ShrinkStream.Spawn(start, end, owner.Owner != null ? owner.Owner.Lookup : 0UL);
            return;
        }

        if (hitEnemy != null && hitEnemy.Alive)
        {
            var state = Shrink(hitEnemy, dt);
            if (state.Scale <= MinScale.Value) Pop(owner, hitEnemy, gearCategoryId);
        }

        ShrinkStream.Spawn(start, end, owner.Owner != null ? owner.Owner.Lookup : 0UL);
        NetworkAPI.InvokeEvent(BeamEvent, new BeamMessage(start, end));
    }

    // Shrinks `enemy` by one tick's worth and applies it (scale and, scaled the same, its own top
    // speed). The base speed is read once, the first time this enemy is ever touched, so repeated
    // ticks scale from the same baseline instead of compounding.
    [HideFromIl2Cpp]
    private static Shrunk Shrink(EnemyAgent enemy, float dt)
    {
        if (!shrunk.TryGetValue(enemy.Pointer, out var state))
        {
            state = new Shrunk { Enemy = enemy, BaseScale = enemy.transform.localScale };
            shrunk[enemy.Pointer] = state;
        }
        state.LastHit = Time.time;
        if (!state.CapturedBase)
        {
            try
            {
                var locomotion = enemy.Locomotion;
                state.BaseRunSpeed = locomotion != null ? locomotion.m_maxMovementSpeed : 0f;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons shrink beam couldn't read {enemy.name}'s base speed: {e.Message}");
            }
            state.CapturedBase = true;
        }

        state.Scale = Mathf.Max(MinScale.Value, state.Scale - ShrinkPerSecond.Value * dt);
        Apply(state);
        return state;
    }

    [HideFromIl2Cpp]
    private static void Apply(Shrunk state)
    {
        var enemy = state.Enemy;
        if (enemy == null) return;
        try
        {
            enemy.transform.localScale = state.BaseScale * state.Scale;
            var locomotion = enemy.Locomotion;
            if (locomotion != null && state.BaseRunSpeed > 0f) locomotion.m_maxMovementSpeed = state.BaseRunSpeed * state.Scale;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons shrink beam couldn't resize {enemy.name}: {e.Message}");
        }
    }

    // Shrunk past MinScale: the shooter's own game finishes it off (through the bullet path, so it
    // syncs) and a green pop marks it; the shrink itself was already cosmetic and clears here.
    [HideFromIl2Cpp]
    private static void Pop(PlayerAgent owner, EnemyAgent enemy, uint gearCategoryId)
    {
        shrunk.Remove(enemy.Pointer);
        try
        {
            var point = enemy.AimTarget.position;
            BurstFlash.Spawn(point, PopColor, 5f, 8f, 0.3f);
            Sounds.Play(Sounds.ShrinkPop.Value, point);
            var limb = ChainLightning.NearestLimb(enemy, point);
            limb?.BulletDamage(PopDamage.Value, owner.Cast<Agent>(), point, Vector3.up, Vector3.down,
                allowDirectionalBonus: false, 10f, 1f, gearCategoryId);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons shrink beam pop failed: {e.Message}");
        }
    }

    // Every tracked enemy not touched in a while grows back toward full size on its own - each
    // client runs this independently off its own shrunk list, so there's nothing to sync.
    internal static void Regrow(float dt)
    {
        if (shrunk.Count == 0) return;
        List<IntPtr>? done = null;
        foreach (var (pointer, state) in shrunk)
        {
            if (state.Enemy == null || !state.Enemy.Alive)
            {
                (done ??= new List<IntPtr>()).Add(pointer);
                continue;
            }
            if (Time.time - state.LastHit < RegrowDelaySeconds.Value) continue;

            state.Scale = Mathf.Min(1f, state.Scale + RegrowPerSecond.Value * dt);
            Apply(state);
            if (state.Scale >= 1f) (done ??= new List<IntPtr>()).Add(pointer);
        }
        if (done == null) return;
        foreach (var pointer in done) shrunk.Remove(pointer);
    }

    // Once an enemy's shrunk to StepOnScale, getting within StepOnRadius and looking down at it gibs
    // it - the local player stepped on it. Each client checks this for its own local player only, same
    // as the regrow above: no network message needed, the kill still goes through the bullet path.
    internal static void CheckStepOn()
    {
        if (shrunk.Count == 0) return;
        var local = PlayerManager.GetLocalPlayerAgent();
        if (local == null || !local.Alive || FocusStateManager.CurrentState != eFocusState.FPS) return;
        var camera = local.FPSCamera;
        if (camera == null) return;

        List<EnemyAgent>? stepped = null;
        foreach (var state in shrunk.Values)
        {
            if (state.Scale > StepOnScale.Value || state.Enemy == null || !state.Enemy.Alive) continue;
            var feet = state.Enemy.transform.position;
            if (Vector3.Distance(local.Position, feet) > StepOnRadius.Value) continue;
            if (Vector3.Angle(camera.Forward, feet - camera.Position) > StepOnLookConeDegrees.Value) continue;
            (stepped ??= new List<EnemyAgent>()).Add(state.Enemy);
        }
        if (stepped == null) return;
        foreach (var enemy in stepped) Pop(local, enemy, GunInjector.ShrinkBeamId);
    }

    // The living enemy nearest `point` within `radius` - how a receiving client finds the same
    // enemy the shooter's beam is on, since scale isn't networked but world position is.
    [HideFromIl2Cpp]
    private static EnemyAgent? NearestEnemy(Vector3 point, float radius)
    {
        EnemyAgent? best = null;
        var bestDistance = float.MaxValue;
        foreach (var collider in Physics.OverlapSphere(point, radius, LayerManager.MASK_ENEMY_DAMAGABLE))
        {
            var enemy = collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner;
            if (enemy == null || !enemy.Alive) continue;
            var distance = (enemy.AimTarget.position - point).sqrMagnitude;
            if (distance >= bestDistance) continue;
            best = enemy;
            bestDistance = distance;
        }
        return best;
    }

    // Plain fields so GTFO-API can copy them as raw bytes.
    private readonly struct BeamMessage
    {
        public readonly float X0, Y0, Z0, X1, Y1, Z1;

        public BeamMessage(Vector3 start, Vector3 end)
        {
            (X0, Y0, Z0) = (start.x, start.y, start.z);
            (X1, Y1, Z1) = (end.x, end.y, end.z);
        }
    }
}

/// <summary>
/// The beam's look and sound: Chain Lightning's old persistent-stream tether, recolored green and
/// drawn as a single swaying line (no jumps - the Shrink Beam only ever holds on one target) - one
/// unbroken ray per shooter for as long as it's fed, re-swayed every frame so it crackles, lingering
/// only a moment past the last tick so it ends when the trigger is released. A continuous hum plays
/// at the gun for as long as it's fed, same as the laser beams' and Chain Lightning's own loops.
/// </summary>
internal sealed class ShrinkStream : MonoBehaviour
{
    private const float LingerSeconds = 0.2f;
    private const int PointsPerLink = 12;
    private const float Sway = 0.06f;
    private static readonly Color BeamColor = new(0.35f, 1f, 0.3f, 0.85f);
    private static Material? material;

    private static readonly Dictionary<ulong, ShrinkStream> streams = new();

    private LineRenderer line = null!;
    private Vector3 start, end;
    private ulong source;
    private float lastFed;

    public ShrinkStream(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(Vector3 start, Vector3 end, ulong source = 0)
    {
        // A continuous hum at the gun (where the player hears it) while the beam keeps coming.
        LoopSounds.Touch($"shrink:{source}", Sounds.ShrinkLoopStart.Value, Sounds.ShrinkLoopStop.Value, start);

        if (!streams.TryGetValue(source, out var stream) || stream == null)
        {
            stream = new GameObject("GTFO.AbsurdWeapons Shrink Beam").AddComponent<ShrinkStream>();
            stream.source = source;
            stream.line = stream.gameObject.AddComponent<LineRenderer>();
            stream.line.useWorldSpace = true;
            stream.line.positionCount = PointsPerLink;
            stream.line.material = material ??= new Material(Shader.Find("Sprites/Default"));
            streams[source] = stream;
        }
        stream.start = start;
        stream.end = end;
        stream.lastFed = Time.time;
        stream.Draw();
    }

    private void Update()
    {
        if (Time.time - lastFed > LingerSeconds)
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
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons shrink beam stream failed: {e.Message}");
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (streams.TryGetValue(source, out var current) && current == this) streams.Remove(source);
    }

    [HideFromIl2Cpp]
    private void Draw()
    {
        for (var i = 0; i < PointsPerLink; i++)
        {
            var t = i / (float)(PointsPerLink - 1);
            var sway = Mathf.Sin(t * Mathf.PI) * Sway;
            line.SetPosition(i, Vector3.Lerp(start, end, t) + UnityEngine.Random.insideUnitSphere * sway);
        }
        line.startWidth = 0.05f;
        line.endWidth = 0.08f;
        line.startColor = line.endColor = BeamColor;
    }
}

/// <summary>
/// Drives the local player's Shrink Beam from the trigger: while the gun is in hand and Fire is
/// held, a tick every <see cref="ShrinkBeam.GunShotDelay"/>.
/// </summary>
internal sealed class ShrinkBeamDriver : MonoBehaviour
{
    private float nextTick;

    public ShrinkBeamDriver(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        try
        {
            var player = PlayerManager.GetLocalPlayerAgent();
            if (player == null || !player.Alive || FocusStateManager.CurrentState != eFocusState.FPS) return;
            if (StreamReadiness.Blocked(player)) return;
            if (!StreamReadiness.IsSettled(player)) return; // not yet raised into its aim position
            var gun = player.Inventory?.WieldedItem?.TryCast<Gear.BulletWeapon>();
            var archetype = gun?.ArchetypeData;
            if (gun == null || archetype == null || archetype.persistentID != GunInjector.ShrinkBeamId) return;
            if (!InputMapper.GetButtonKeyMouse(InputAction.Fire, eFocusState.FPS)) return;
            var camera = player.FPSCamera;
            if (camera == null) return;

            // Lit (and locked to the barrel) every frame the trigger is held; the shrink itself
            // only lands on the throttled ticks.
            var tick = Mathf.Max(0.02f, ShrinkBeam.GunShotDelay.Value);
            var applyShrink = Time.time >= nextTick;
            if (applyShrink)
            {
                if (!InfiniteAmmo.TrySpend(player, tick)) return; // out of ammo: no beam
                nextTick = Time.time + tick;
            }
            else if (!InfiniteAmmo.HasStreamAmmo(player)) return;
            ShrinkBeam.Tick(player, camera.Position, camera.Forward, gun.MuzzleAlign, tick, GunInjector.ShrinkBeamId, applyShrink);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons shrink beam driver failed: {e.Message}");
            nextTick = Time.time + 1f;
        }
    }
}

/// <summary>
/// Always-on: regrows every tracked shrunk enemy that's fallen off the beam, and pops any that are
/// small enough for the local player to have just stepped on.
/// </summary>
internal sealed class ShrinkRegrowth : MonoBehaviour
{
    public ShrinkRegrowth(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        try
        {
            ShrinkBeam.Regrow(Time.deltaTime);
            ShrinkBeam.CheckStepOn();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons shrink regrowth failed: {e.Message}");
        }
    }
}
