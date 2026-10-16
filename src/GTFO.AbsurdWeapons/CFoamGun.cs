using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using Enemies;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>Which color the foam blob is built in - a fixed one, or a fresh random one each time
/// an enemy starts getting foamed.</summary>
internal enum CFoamColor
{
    White,
    Tan,
    Orange,
    Blue,
    Green,
    Purple,
    Random,
}

/// <summary>
/// The C-Foam Pistol and Rifle: the C-Foam Launcher's own hardening foam, sprayed full-auto instead
/// of lobbed. Every round that lands stacks foam on that enemy (growing a visible blob stuck to
/// them); past a threshold it's fully encased, and <see cref="CFoamTicker"/> (its own clock, not the
/// trigger) holds it there with a heavy stagger pulse every tick until the foam's duration runs out
/// without a fresh hit to refresh it. Barely any direct damage - this is a trap, not a killer. The
/// spraying player's own game only, like every other weapon here (the blob itself is purely local
/// too - not sent to teammates, same simplification as the Flak Cannon's shrapnel).
/// </summary>
internal static class CFoamGun
{
    internal static ConfigEntry<float> GunDamage = null!;
    internal static ConfigEntry<float> GunShotDelay = null!;
    internal static ConfigEntry<int> GunClipSize = null!;

    internal static ConfigEntry<int> StackThreshold = null!;
    internal static ConfigEntry<int> MaxStacks = null!;
    internal static ConfigEntry<float> Duration = null!;
    internal static ConfigEntry<float> TickSeconds = null!;
    internal static ConfigEntry<float> TickStaggerMulti = null!;
    internal static ConfigEntry<CFoamColor> FoamColor = null!;

    internal static void Bind(ConfigFile config)
    {
        GunDamage = config.Bind("C-Foam Guns", "GunDamage", 1f, "Direct damage per round - this is a trap, not a killer.");
        GunShotDelay = config.Bind("C-Foam Guns", "GunShotDelay", 0.08f, "Seconds between rounds.");
        GunClipSize = config.Bind("C-Foam Guns", "GunClipSize", 80, "Clip size (infinite ammo refills it anyway).");
        StackThreshold = config.Bind("C-Foam Guns", "StackThreshold", 5, "Hits needed before an enemy counts as fully encased.");
        MaxStacks = config.Bind("C-Foam Guns", "MaxStacks", 8, "Cap on foam stacks per enemy.");
        Duration = config.Bind("C-Foam Guns", "Duration", 4f, "Seconds the foam holds without a fresh hit; each hit refreshes it.");
        TickSeconds = config.Bind("C-Foam Guns", "TickSeconds", 0.5f, "Seconds between stagger pulses on a fully encased enemy.");
        TickStaggerMulti = config.Bind("C-Foam Guns", "TickStaggerMulti", 80f, "Stagger multiplier of each pulse - high enough to hold almost anything in place.");
        FoamColor = config.Bind("C-Foam Guns", "FoamColor", CFoamColor.White,
            "The foam blob's color. Random picks a fresh one (any hue) each time an enemy starts getting foamed, so a crowd of them comes out in different colors.");
    }

    private sealed record FoamState(EnemyAgent Enemy, int Stacks, float ExpiresAt, PlayerAgent Owner, uint GearCategoryId, GameObject Blob);
    private static readonly Dictionary<IntPtr, FoamState> foamed = new();
    private const float MinBlobScale = 0.15f, MaxBlobScale = 0.7f;

    // One round from the local shooter.
    internal static void Hit(Dam_EnemyDamageLimb limb, EnemyAgent enemy, PlayerAgent owner, Vector3 point, Vector3 direction,
        float damage, uint gearCategoryId)
    {
        limb.BulletDamage(damage, owner.Cast<Agent>(), point, direction, -direction,
            allowDirectionalBonus: false, staggerMulti: 1f, precisionMulti: 1f, gearCategoryId);

        var hadExisting = foamed.TryGetValue(enemy.Pointer, out var existing);
        var stacks = Math.Min((hadExisting ? existing!.Stacks : 0) + 1, MaxStacks.Value);
        var blob = hadExisting ? existing!.Blob : SpawnBlob(enemy, point);
        GrowBlob(blob, stacks);
        foamed[enemy.Pointer] = new FoamState(enemy, stacks, Time.time + Duration.Value, owner, gearCategoryId, blob);
    }

    // Runs off CFoamTicker's own clock, not the trigger: a fully-encased enemy stays held even if
    // you stop shooting it, until the foam's duration runs out.
    internal static void Tick()
    {
        if (foamed.Count == 0) return;
        var expired = new List<IntPtr>();
        foreach (var (key, state) in foamed)
        {
            if (Time.time >= state.ExpiresAt || !state.Enemy.Alive) { expired.Add(key); continue; }
            if (state.Stacks < StackThreshold.Value) continue;
            if (!state.Owner.IsLocallyOwned) continue; // only the shooter's own game holds it in place

            var limb = ChainLightning.NearestLimb(state.Enemy, state.Enemy.AimTarget.position);
            if (limb == null) continue;
            limb.BulletDamage(0f, state.Owner.Cast<Agent>(), state.Enemy.AimTarget.position, Vector3.up, Vector3.down,
                allowDirectionalBonus: false, TickStaggerMulti.Value, 1f, state.GearCategoryId);
        }
        foreach (var key in expired)
        {
            if (foamed.TryGetValue(key, out var gone) && gone.Blob != null) UnityEngine.Object.Destroy(gone.Blob);
            foamed.Remove(key);
        }
    }

    // A tinted blob stuck to the enemy where the foam first landed - no new assets shipped, same
    // primitive-fallback style as the Flak Cannon's ball and the Victory Banner's pole.
    private static GameObject SpawnBlob(EnemyAgent enemy, Vector3 point)
    {
        var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        UnityEngine.Object.Destroy(blob.GetComponent<Collider>());
        blob.transform.SetParent(enemy.transform, true);
        blob.transform.position = point;
        blob.transform.localScale = Vector3.one * MinBlobScale;
        var renderer = blob.GetComponent<Renderer>();
        if (renderer != null) renderer.material = new Material(Shader.Find("Unlit/Color")) { color = ResolveColor() };
        return blob;
    }

    private static void GrowBlob(GameObject blob, int stacks) =>
        blob.transform.localScale = Vector3.one * Mathf.Lerp(MinBlobScale, MaxBlobScale, stacks / (float)MaxStacks.Value);

    private static Color ResolveColor() => FoamColor.Value switch
    {
        CFoamColor.Tan => new Color(0.82f, 0.72f, 0.55f),
        CFoamColor.Orange => new Color(1f, 0.55f, 0.15f),
        CFoamColor.Blue => new Color(0.25f, 0.55f, 1f),
        CFoamColor.Green => new Color(0.25f, 0.85f, 0.35f),
        CFoamColor.Purple => new Color(0.65f, 0.3f, 0.9f),
        CFoamColor.Random => UnityEngine.Random.ColorHSV(0f, 1f, 0.55f, 0.9f, 0.8f, 1f),
        _ => new Color(0.92f, 0.90f, 0.85f), // White
    };
}

/// <summary>Always-on: ticks C-Foam's hold on encased enemies - see <see cref="CFoamGun.Tick"/>.</summary>
internal sealed class CFoamTicker : MonoBehaviour
{
    private float nextTickAt;

    public CFoamTicker(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        if (Time.time < nextTickAt) return;
        nextTickAt = Time.time + CFoamGun.TickSeconds.Value;
        try
        {
            CFoamGun.Tick();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons C-Foam tick failed: {e.Message}");
        }
    }
}
