using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Gear;
using HarmonyLib;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// Every weapon's sounds, picked from the game's own Wwise events to match what the weapon is and
/// what it's doing: no gunshot from a lightning gun, a gauss crack from the Railgun, a thump from
/// the grenade guns, and so on. Each is a config entry holding the event's name
/// (<c>[Sounds]</c>; empty = silent, or the gun's own sound for a shot), so they can be re-picked
/// without a rebuild. Guns with a shot override have their vanilla fire audio suppressed and play
/// the override once per shot instead, on every client (the ray hook runs for teammates' guns too).
/// Events with "2D" in the name are first-person only, so they play for the shooter alone.
/// </summary>
[HarmonyPatch]
internal static class Sounds
{
    internal static ConfigEntry<string> PlasmaShot = null!;
    internal static ConfigEntry<string> RailgunShot = null!;
    internal static ConfigEntry<string> GrenadeShot = null!;
    internal static ConfigEntry<string> TentacleShot = null!;
    internal static ConfigEntry<string> TentacleBite = null!;
    internal static ConfigEntry<string> LaserLoopStart = null!;
    internal static ConfigEntry<string> LaserLoopStop = null!;
    internal static ConfigEntry<string> LightningLoopStart = null!;
    internal static ConfigEntry<string> LightningLoopStop = null!;
    internal static ConfigEntry<string> ShrinkLoopStart = null!;
    internal static ConfigEntry<string> ShrinkLoopStop = null!;
    internal static ConfigEntry<string> ShrinkPop = null!;
    internal static ConfigEntry<string> LightningCrackle = null!;
    internal static ConfigEntry<string> Thunderclap = null!;
    internal static ConfigEntry<string> BannerPlant = null!;

    internal static void Bind(ConfigFile config)
    {
        const string s = "Sounds";
        PlasmaShot = config.Bind(s, "PlasmaShot", "GAUSSPISTOLBODYA3D", "Plasma Pistol/Rifle shot: a punchy energy shot (the gauss gun's body layer). Empty = the gun's own sound.");
        RailgunShot = config.Bind(s, "RailgunShot", "GAUSSPISTOLFIRE2D_RANDOM", "Railgun shot: a gauss (magnetic) gun's crack. Empty = the sniper's own shot.");
        GrenadeShot = config.Bind(s, "GrenadeShot", "FLAREGUNFIRE", "Grenade Pistol/Rifle shot: the flare gun's hollow thump. Empty = the gun's own sound.");
        TentacleShot = config.Bind(s, "TentacleShot", "SNATCHERTENTACLESHOOT", "Tentacle Cannon: each tentacle lashing out (a snatcher's tentacle shot). Empty = the gun's own sound.");
        TentacleBite = config.Bind(s, "TentacleBite", "SNATCHERTENTACLEHIT", "A tentacle landing its bite.");
        LaserLoopStart = config.Bind(s, "LaserLoopStart", "FREEZERLOOP", "Laser beam: a continuous loop at the gun while the trigger is held (the freeze thrower's beam loop).");
        LaserLoopStop = config.Bind(s, "LaserLoopStop", "FREEZERLOOPEND", "Laser beam: stops the loop when the beam goes off.");
        LightningLoopStart = config.Bind(s, "LightningLoopStart", "TERMINAL_CREEPY_STATIC_LOOP", "Chain Lightning: a crackling electric loop while the stream runs.");
        LightningLoopStop = config.Bind(s, "LightningLoopStop", "TERMINAL_CREEPY_STATIC_STOP", "Chain Lightning: stops the loop.");
        ShrinkLoopStart = config.Bind(s, "ShrinkLoopStart", "BIOSCAN_TUBE_EMITTER", "Shrink Beam: a continuous emitter-beam hum while the ray runs (the security scan's beam emitter).");
        ShrinkLoopStop = config.Bind(s, "ShrinkLoopStop", "BIOSCAN_TUBE_EMITTER_STOP", "Shrink Beam: stops the loop.");
        ShrinkPop = config.Bind(s, "ShrinkPop", "ENEMYWEAKSPOTEXPLODE", "Shrink Beam: a shrunk enemy gibbing (shrunk away, or stepped on).");
        LightningCrackle = config.Bind(s, "LightningCrackle", "FLUORESCENT_LIGHT_CRACKLE", "An electric crackle where lightning strikes (chain lightning and Mjolnir's pulses), a few times a second.");
        Thunderclap = config.Bind(s, "Thunderclap", "ELEVATOR_THUNDER", "Mjolnir's thunderclap on a landed hit.");
        BannerPlant = config.Bind(s, "BannerPlant", "SENTRYGUN_DEPLOY", "Planting the Victory Banner.");
    }

    // The shot sound that replaces a gun's own, if it has one.
    private static ConfigEntry<string>? ShotFor(uint id) => id switch
    {
        GunInjector.PlasmaPistolId or GunInjector.PlasmaRifleId => PlasmaShot,
        GunInjector.RailgunId => RailgunShot,
        GunInjector.GrenadePistolId or GunInjector.GrenadeRifleId => GrenadeShot,
        GunInjector.TentacleCannonId => TentacleShot,
        _ => null,
    };

    // The gun's own fire audio is silenced where we have our own shot sound, and always for the
    // trigger-driven guns (lasers, Chain Lightning): they never fire, but the game still starts its
    // full-auto firing loop on the trigger - their sound is the beam / lightning loop instead.
    private static bool Overridden(BulletWeapon weapon)
    {
        var id = weapon.ArchetypeData?.persistentID ?? 0;
        if (LaserGuns.ForGun(id) != null || id == GunInjector.ChainLightningId || id == GunInjector.ShrinkBeamId) return true;
        return ShotFor(id) is { } entry && !string.IsNullOrWhiteSpace(entry.Value);
    }

    // ---- the gun's own fire audio is suppressed where there's an override ----
    [HarmonyPatch(typeof(BulletWeapon), nameof(BulletWeapon.TriggerSingleFireAudio))]
    [HarmonyPrefix]
    private static bool SingleFire(BulletWeapon __instance) => !Overridden(__instance);

    [HarmonyPatch(typeof(BulletWeapon), nameof(BulletWeapon.TriggerBurstFireAudio))]
    [HarmonyPrefix]
    private static bool BurstFire(BulletWeapon __instance) => !Overridden(__instance);

    [HarmonyPatch(typeof(BulletWeapon), nameof(BulletWeapon.TriggerBurstFireOneShotAudio))]
    [HarmonyPrefix]
    private static bool BurstOneShot(BulletWeapon __instance) => !Overridden(__instance);

    [HarmonyPatch(typeof(BulletWeapon), nameof(BulletWeapon.TriggerAutoFireStartAudio))]
    [HarmonyPrefix]
    private static bool AutoStart(BulletWeapon __instance) => !Overridden(__instance);

    [HarmonyPatch(typeof(BulletWeapon), nameof(BulletWeapon.TriggerAutoFireEndAudio))]
    [HarmonyPrefix]
    private static bool AutoEnd(BulletWeapon __instance) => !Overridden(__instance);

    [HarmonyPatch(typeof(BulletWeapon), nameof(BulletWeapon.TriggerSyncedBurstFirePerShotAudio))]
    [HarmonyPrefix]
    private static bool SyncedBurst(BulletWeapon __instance) => !Overridden(__instance);

    [HarmonyPatch(typeof(BulletWeapon), nameof(BulletWeapon.TriggerSyncedAutoFirePerShotAudio))]
    [HarmonyPrefix]
    private static bool SyncedAuto(BulletWeapon __instance) => !Overridden(__instance);

    // ---- playing ----
    private static readonly Dictionary<IntPtr, int> lastShotFrame = new();

    // Called from the ray hook for each of our guns' rays: the override, once per shot (a shot's
    // pellets all come in the same frame).
    internal static void Shot(BulletWeapon? weapon, uint id, Vector3 position)
    {
        var entry = ShotFor(id);
        if (weapon == null || entry == null || string.IsNullOrWhiteSpace(entry.Value)) return;
        if (lastShotFrame.TryGetValue(weapon.Pointer, out var frame) && frame == Time.frameCount) return;
        lastShotFrame[weapon.Pointer] = Time.frameCount;
        var local = weapon.Owner != null && weapon.Owner.IsLocallyOwned;
        Play(entry.Value, position, local);
    }

    // `local`: whether the event's source is the local player (a "2D" event only plays for them).
    internal static void Play(string eventName, Vector3 position, bool local = true)
    {
        if (string.IsNullOrWhiteSpace(eventName)) return;
        if (!local && eventName.IndexOf("2D", StringComparison.OrdinalIgnoreCase) >= 0) return;
        try
        {
            CellSound.Post(eventName.Trim(), position);
        }
        catch (Exception e)
        {
            if (failed.Add(eventName)) Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons couldn't play sound '{eventName}': {e.Message}");
        }
    }

    private static readonly HashSet<string> failed = new();
    private static readonly Dictionary<string, float> lastPlayed = new();

    // Play, but no more than once per `interval` seconds for `key` (crackles, splats).
    internal static void PlayLimited(string key, float interval, string eventName, Vector3 position, bool local = true)
    {
        if (lastPlayed.TryGetValue(key, out var at) && Time.time - at < interval) return;
        lastPlayed[key] = Time.time;
        Play(eventName, position, local);
    }
}

/// <summary>
/// Looping sounds that run while something keeps "touching" them (a laser beam's ticks, a
/// lightning stream's bolts) and stop a moment after the touches stop. One per key, each on its
/// own sound player so it can be moved and stopped.
/// </summary>
internal sealed class LoopSounds : MonoBehaviour
{
    private const float StopAfter = 0.25f;

    private sealed class Loop
    {
        public CellSoundPlayer Player = null!;
        public string StopEvent = "";
        public Vector3 Position;
        public float LastTouch;
    }

    private static readonly Dictionary<string, Loop> loops = new();

    public LoopSounds(IntPtr ptr) : base(ptr) { }

    internal static void Touch(string key, string startEvent, string stopEvent, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(startEvent)) return;
        try
        {
            if (!loops.TryGetValue(key, out var loop))
            {
                loop = new Loop { Player = new CellSoundPlayer(position), StopEvent = stopEvent };
                loop.Player.Post(startEvent.Trim(), position);
                loops[key] = loop;
            }
            else
            {
                loop.Player.UpdatePosition(position);
            }
            loop.Position = position;
            loop.LastTouch = Time.time;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons couldn't run loop sound '{startEvent}': {e.Message}");
            loops.Remove(key);
        }
    }

    private void Update()
    {
        if (loops.Count == 0) return;
        List<string>? stale = null;
        foreach (var (key, loop) in loops)
            if (Time.time - loop.LastTouch > StopAfter) (stale ??= new List<string>()).Add(key);
        if (stale == null) return;
        foreach (var key in stale)
        {
            var loop = loops[key];
            loops.Remove(key);
            try
            {
                if (!string.IsNullOrWhiteSpace(loop.StopEvent)) loop.Player.Post(loop.StopEvent.Trim(), loop.Position);
                else loop.Player.Stop();
                loop.Player.Recycle();
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons couldn't stop a loop sound: {e.Message}");
            }
        }
    }
}
