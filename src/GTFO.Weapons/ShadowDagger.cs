using System;
using System.Collections.Generic;
using Agents;
using Enemies;
using Gear;
using GTFO.API;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.Weapons;

/// <summary>
/// The Shadow Dagger: lightning-fast stabs (its light attacks play <see cref="StabSpeed"/>x as fast),
/// and a fully charged hit that lands on an enemy hides its wielder for <see cref="Seconds"/> (a
/// further one restarts the count). While hidden, enemies can't detect
/// them by sight or noise, can't lock on or be alerted to them, and drop them as a target if they
/// already had them. Enemy AI runs on the host, so the hit is sent to everyone over GTFO-API and
/// every game keeps the same list of who is hidden. Hidden players shimmer, half-there, until it
/// ends (see <see cref="StealthVisuals"/>).
/// </summary>
[HarmonyPatch]
internal static class Stealth
{
    internal const float Seconds = 6f;
    internal const float StabSpeed = 2.5f;
    // A charged hit counts as full when it deals at least this share of the full charged damage.
    private const float FullChargeShare = 0.95f;
    private const string StealthEvent = "MindAttic.GTFO.Weapons.Stealth";

    // Keyed by the player's Steam id (SNet_Player.Lookup): when their stealth ends, in Time.time.
    private static readonly Dictionary<ulong, float> hiddenUntil = new();

    internal static void Init()
    {
        NetworkAPI.RegisterEvent<StealthMessage>(StealthEvent, (sender, message) => Hide(sender, message.Seconds));
    }

    internal static bool IsHidden(Agent? agent) => Remaining(agent) > 0f;

    // Seconds of stealth left (0 when not hidden).
    internal static float Remaining(Agent? agent)
    {
        if (agent == null || hiddenUntil.Count == 0) return 0f;
        var player = agent.TryCast<PlayerAgent>();
        if (player == null || player.Owner == null || !hiddenUntil.TryGetValue(player.Owner.Lookup, out var until)) return 0f;
        return Mathf.Max(0f, until - Time.time);
    }

    private static void Hide(ulong player, float seconds)
    {
        hiddenUntil[player] = Time.time + seconds;
        Plugin.Logger.LogInfo($"GTFO.Weapons: player {player} is hidden from enemies for {seconds:0} s.");
    }

    // ---- Fast stabs: light attacks play faster; the charge-up keeps its normal pace ----

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.MeleeSpeedModifier), MethodType.Getter)]
    [HarmonyPostfix]
    private static void StabSpeedUp(MeleeWeaponFirstPerson __instance, ref float __result)
    {
        if (__instance.MeleeArchetypeData?.persistentID != MeleeInjector.ShadowDaggerId) return;
        if (__instance.CurrentStateName is eMeleeWeaponState.AttackMissLeft or eMeleeWeaponState.AttackMissRight
            or eMeleeWeaponState.AttackHitLeft or eMeleeWeaponState.AttackHitRight)
        {
            __result *= StabSpeed;
        }
    }

    // ---- The trigger: a fully charged Shadow Dagger hit that lands on an enemy ----

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.DoAttackDamage))]
    [HarmonyPostfix]
    private static void AfterAttackDamage(MeleeWeaponFirstPerson __instance, MeleeWeaponDamageData data, bool isPush)
    {
        try
        {
            if (isPush || data == null || __instance.MeleeArchetypeData?.persistentID != MeleeInjector.ShadowDaggerId) return;
            if (data.damageGO == null || data.damageGO.GetComponentInParent<EnemyAgent>() == null) return;
            if (!ImpactBursts.IsChargedAttack(__instance.CurrentStateName)) return;

            // Full charge: the damage about to be dealt is (nearly) the archetype's full charged damage.
            var full = __instance.MeleeArchetypeData.ChargedAttackDamage;
            var dealt = __instance.m_damageToDeal;
            if (dealt < full * FullChargeShare)
            {
                Plugin.Logger.LogInfo($"GTFO.Weapons Shadow Dagger: charged hit at {dealt:0.##} of {full:0.##} - not a full charge, no stealth.");
                return;
            }

            var owner = __instance.Owner;
            if (owner == null || !owner.IsLocallyOwned || owner.Owner == null) return;

            Hide(owner.Owner.Lookup, Seconds);
            NetworkAPI.InvokeEvent(StealthEvent, new StealthMessage(Seconds));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.Weapons Shadow Dagger stealth failed: {e}");
        }
    }

    // ---- Enemies ignore hidden players (these decide on the host) ----

    // A target that's hidden no longer counts - enemies already chasing drop it.
    [HarmonyPatch(typeof(EnemyDetection), nameof(EnemyDetection.IsTargetValid))]
    [HarmonyPostfix]
    private static void TargetValid(AgentTarget agentTarget, ref bool __result)
    {
        if (__result && IsHidden(agentTarget?.m_agent)) __result = false;
    }

    // Noticing them by sight or noise.
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.SetDetectedAgent))]
    [HarmonyPrefix]
    private static bool DetectedAgent(Agent agent) => !IsHidden(agent);

    // Locking on.
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.SetTarget))]
    [HarmonyPrefix]
    private static bool Target(Agent agent) => !IsHidden(agent);

    // Being alerted to them by other enemies and noise propagation.
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.InjectPropagatedTarget))]
    [HarmonyPrefix]
    private static bool PropagatedTarget(Agent agent) => !IsHidden(agent);

    // Plain fields so GTFO-API can copy it as raw bytes. The sender is the hidden player.
    private readonly struct StealthMessage
    {
        public readonly float Seconds;
        public StealthMessage(float seconds) => Seconds = seconds;
    }
}

/// <summary>
/// Shows stealth on every game, from the shared hidden list, so everyone sees the same thing: a
/// shimmer. Each part of a hidden player's body (every renderer) flickers 10 times a second on its
/// own timing, showing for 35% of each flicker - so at any moment some pieces are there and others
/// aren't, and the figure reads as broken up and see-through rather than blinking as a whole.
/// Over the last second it fills back in to solid as a warning. Teammates see the whole body; the
/// hidden player sees their first-person view (arms and the dagger itself). Only renderers that
/// were on when the stealth began are touched, and all of them are turned back on.
/// </summary>
internal sealed class StealthVisuals : MonoBehaviour
{
    private const float FlickersPerSecond = 10f;
    private const float ShimmerVisible = 0.35f; // share of each flicker a body part shows
    private const float WarningSeconds = 1f;   // the shimmer fills back in over this last stretch

    // Per hidden player: each renderer with its own flicker offset (0..1 of a flicker).
    private readonly Dictionary<IntPtr, List<(Renderer Renderer, float Offset)>> hidden = new();

    public StealthVisuals(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        try
        {
            var players = PlayerManager.PlayerAgentsInLevel;
            if (players == null) return;
            foreach (var player in players)
            {
                if (player == null) continue;
                var remaining = Stealth.Remaining(player);
                var tracked = hidden.TryGetValue(player.Pointer, out var renderers);

                if (remaining > 0f && !tracked) Vanish(player);
                else if (remaining > 0f) Shimmer(renderers!, remaining);
                else if (tracked) Reappear(player.Pointer, renderers!);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.Weapons stealth visuals failed: {e.Message}");
        }
    }

    [HideFromIl2Cpp]
    private void Vanish(PlayerAgent player)
    {
        var renderers = new List<(Renderer, float)>();
        // Your own first-person view (arms and the item in hand), or a teammate's whole body.
        Component? root = player.IsLocallyOwned ? player.FPItemHolder : player;
        if (root != null)
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.enabled) renderers.Add((renderer, UnityEngine.Random.value));
        hidden[player.Pointer] = renderers;
        SetVisible(renderers, false);
    }

    [HideFromIl2Cpp]
    private static void Shimmer(List<(Renderer Renderer, float Offset)> renderers, float remaining)
    {
        // Over the last second the share of each flicker spent visible climbs to all of it.
        var visibleShare = remaining > WarningSeconds
            ? ShimmerVisible
            : Mathf.Lerp(1f, ShimmerVisible, remaining / WarningSeconds);
        var flicker = Time.time * FlickersPerSecond;
        foreach (var (renderer, offset) in renderers)
            if (renderer != null) renderer.enabled = (flicker + offset) % 1f < visibleShare;
    }

    [HideFromIl2Cpp]
    private void Reappear(IntPtr player, List<(Renderer Renderer, float Offset)> renderers)
    {
        SetVisible(renderers, true);
        hidden.Remove(player);
    }

    [HideFromIl2Cpp]
    private static void SetVisible(List<(Renderer Renderer, float Offset)> renderers, bool visible)
    {
        foreach (var (renderer, _) in renderers)
            if (renderer != null) renderer.enabled = visible;
    }
}
