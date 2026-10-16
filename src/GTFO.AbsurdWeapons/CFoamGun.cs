using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using Enemies;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The C-Foam Pistol and Rifle: the C-Foam Launcher's own hardening foam, sprayed full-auto instead
/// of lobbed. Every round that lands stacks foam on that enemy; past a threshold it's fully
/// encased, and <see cref="CFoamTicker"/> (its own clock, not the trigger) holds it there with a
/// heavy stagger pulse every tick until the foam's duration runs out without a fresh hit to refresh
/// it. Barely any direct damage - this is a trap, not a killer. The spraying player's own game only,
/// like every other weapon here.
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
    }

    private sealed record FoamState(EnemyAgent Enemy, int Stacks, float ExpiresAt, PlayerAgent Owner, uint GearCategoryId);
    private static readonly Dictionary<IntPtr, FoamState> foamed = new();

    // One round from the local shooter.
    internal static void Hit(Dam_EnemyDamageLimb limb, EnemyAgent enemy, PlayerAgent owner, Vector3 point, Vector3 direction,
        float damage, uint gearCategoryId)
    {
        limb.BulletDamage(damage, owner.Cast<Agent>(), point, direction, -direction,
            allowDirectionalBonus: false, staggerMulti: 1f, precisionMulti: 1f, gearCategoryId);

        var stacks = foamed.TryGetValue(enemy.Pointer, out var existing) ? existing.Stacks : 0;
        stacks = Math.Min(stacks + 1, MaxStacks.Value);
        foamed[enemy.Pointer] = new FoamState(enemy, stacks, Time.time + Duration.Value, owner, gearCategoryId);
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
        foreach (var key in expired) foamed.Remove(key);
    }
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
