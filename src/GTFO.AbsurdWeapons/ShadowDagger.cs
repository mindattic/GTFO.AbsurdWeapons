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

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Shadow Dagger: lightning-fast stabs (its light attacks play <see cref="StabSpeed"/>x as fast),
/// and a fully charged hit that lands on an enemy hides its wielder for <see cref="Seconds"/> (a
/// further one restarts the count). While hidden, enemies can't detect
/// them by sight or noise, can't lock on or be alerted to them, and drop them as a target if they
/// already had them. Enemy AI runs on the host, so the hit is sent to everyone over GTFO-API and
/// every game keeps the same list of who is hidden. Hidden players shimmer, half-there, until it
/// ends (see <see cref="StealthVisuals"/>). Putting the dagger away - wielding anything else - ends
/// it at once.
/// </summary>
[HarmonyPatch]
internal static class Stealth
{
    internal const float Seconds = 6f;
    internal const float StabSpeed = 2.5f;
    // A charged hit counts as full when it deals at least this share of the full charged damage.
    private const float FullChargeShare = 0.95f;
    private const string StealthEvent = "MindAttic.GTFO.AbsurdWeapons.Stealth";

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
        var left = until - Time.time;
        if (left > 0f) return left;
        hiddenUntil.Remove(player.Owner.Lookup); // ran out: off the list
        return 0f;
    }

    private static void Hide(ulong player, float seconds)
    {
        if (seconds > 0f) hiddenUntil[player] = Time.time + seconds;
        else hiddenUntil.Remove(player);
        Plugin.Logger.LogInfo(seconds > 0f
            ? $"GTFO.AbsurdWeapons: player {player} is hidden from enemies for {seconds:0} s."
            : $"GTFO.AbsurdWeapons: player {player} is visible again.");
    }

    // The stealth lasts only while the dagger is in hand: the local player wielding anything else
    // ends it, here and (by message) on every other game.
    internal static void EndIfDaggerPutAway(PlayerAgent player)
    {
        if (!player.IsLocallyOwned || player.Owner == null || Remaining(player) <= 0f) return;
        var wielded = player.Inventory?.WieldedItem;
        if (wielded == null || wielded.GearCategoryData?.persistentID == MeleeInjector.ShadowDaggerId) return;
        Hide(player.Owner.Lookup, 0f);
        NetworkAPI.InvokeEvent(StealthEvent, new StealthMessage(0f));
    }

    // (Fast stabs: the dagger's light attacks are 2.5x as fast - MeleeInjector / SwordTempo.)

    // ---- Kills while hidden: each adds 25% dagger damage (up to +100%) for the rest of the
    // stealth; a full-windup kill also adds the windup's time to the stealth ----

    private const float BonusPerKill = 0.25f;
    private const float MaxBonus = 1f;
    private const float KillCheckDelay = 0.4f; // the kill is confirmed by the host; look a moment later

    private static float damageBonus;      // the local player's, while hidden
    private static float chargeStarted = -1f, lastWindup;
    private static readonly List<(EnemyAgent Enemy, float CheckAt, float Windup)> pendingKills = new();

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.ChangeState))]
    [HarmonyPostfix]
    private static void AfterChangeState(MeleeWeaponFirstPerson __instance, eMeleeWeaponState newState)
    {
        if (__instance.MeleeArchetypeData?.persistentID != MeleeInjector.ShadowDaggerId) return;
        if (__instance.Owner == null || !__instance.Owner.IsLocallyOwned) return;
        if (newState is eMeleeWeaponState.AttackChargeUpLeft or eMeleeWeaponState.AttackChargeUpRight)
            chargeStarted = Time.time;
        else if (newState is eMeleeWeaponState.AttackChargeReleaseLeft or eMeleeWeaponState.AttackChargeReleaseRight)
            lastWindup = chargeStarted >= 0f ? Time.time - chargeStarted : 0f;
    }

    // One check per enemy, however many times it was hit: a kill counts once (keeping a full
    // windup if any of the hits had one).
    private static void QueueKillCheck(EnemyAgent enemy, float windup)
    {
        var index = pendingKills.FindIndex(p => p.Enemy != null && p.Enemy.Pointer == enemy.Pointer);
        if (index >= 0)
        {
            windup = Mathf.Max(windup, pendingKills[index].Windup);
            pendingKills.RemoveAt(index);
        }
        pendingKills.Add((enemy, Time.time + KillCheckDelay, windup));
    }

    // Kills confirmed since they were struck: the damage bonus grows, and a full-windup kill
    // stretches the stealth by the windup. Called every frame (StealthVisuals).
    internal static void ProcessKills(PlayerAgent local)
    {
        if (Remaining(local) <= 0f)
        {
            damageBonus = 0f;
            pendingKills.Clear();
            return;
        }
        for (var i = pendingKills.Count - 1; i >= 0; i--)
        {
            var (enemy, checkAt, windup) = pendingKills[i];
            if (Time.time < checkAt) continue;
            pendingKills.RemoveAt(i);
            if (enemy != null && enemy.Alive) continue;

            damageBonus = Formulas.StealthBonusAfterKill(damageBonus, BonusPerKill, MaxBonus);
            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons Shadow Dagger: kill while hidden - damage +{damageBonus * 100f:0}%.");
            if (windup > 0f && local.Owner != null)
            {
                var stretched = Remaining(local) + windup;
                Hide(local.Owner.Lookup, stretched);
                NetworkAPI.InvokeEvent(StealthEvent, new StealthMessage(stretched));
            }
        }
    }

    // ---- The trigger: a fully charged Shadow Dagger hit that lands on an enemy ----

    // The damage the hit is about to deal, read before the hit too, in case the game spends it.
    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.DoAttackDamage))]
    [HarmonyPrefix]
    private static void BeforeAttackDamage(MeleeWeaponFirstPerson __instance, out float __state)
    {
        __state = __instance.m_damageToDeal;
        // Kills while hidden sharpen the dagger: the bonus applies to every hit until it ends.
        if (damageBonus > 0f && __instance.MeleeArchetypeData?.persistentID == MeleeInjector.ShadowDaggerId
            && __instance.Owner != null && __instance.Owner.IsLocallyOwned && IsHidden(__instance.Owner))
            __instance.m_damageToDeal = __state * (1f + damageBonus);
    }

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.DoAttackDamage))]
    [HarmonyPostfix]
    private static void AfterAttackDamage(MeleeWeaponFirstPerson __instance, MeleeWeaponDamageData data, bool isPush, float __state)
    {
        try
        {
            if (isPush || data == null || __instance.MeleeArchetypeData?.persistentID != MeleeInjector.ShadowDaggerId) return;
            var struck = data.damageGO != null ? data.damageGO.GetComponentInParent<EnemyAgent>() : null;
            if (struck == null) return;
            var attacker = __instance.Owner;
            var charged = ImpactBursts.IsChargedAttack(__instance.CurrentStateName);

            // Full charge: the damage about to be dealt (before any bonus) is (nearly) the
            // archetype's full charged damage.
            var full = __instance.MeleeArchetypeData.ChargedAttackDamage;
            var isFull = charged && __state >= full * FullChargeShare;

            // A hit while hidden: if it kills, the bonus grows (and a full windup stretches the stealth).
            if (attacker != null && attacker.IsLocallyOwned && IsHidden(attacker))
                QueueKillCheck(struck, isFull ? lastWindup : 0f);

            if (!charged) return;
            var dealt = __state;
            if (dealt < full * FullChargeShare)
            {
                Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons Shadow Dagger: charged hit at {dealt:0.##} of {full:0.##} - not a full charge, no stealth.");
                return;
            }

            var owner = __instance.Owner;
            if (owner == null || !owner.IsLocallyOwned || owner.Owner == null) return;

            // Starts (or tops back up to) the full stealth - never cutting a stretched one short.
            var seconds = Mathf.Max(Seconds, Remaining(owner));
            Hide(owner.Owner.Lookup, seconds);
            NetworkAPI.InvokeEvent(StealthEvent, new StealthMessage(seconds));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Shadow Dagger stealth failed: {e}");
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

    private const float RescanSeconds = 0.25f; // how often to pick up renderers that appeared (a swapped-in weapon)

    // Per hidden player: each renderer with its own flicker offset (0..1 of a flicker).
    private sealed class Hidden
    {
        public readonly List<(Renderer Renderer, float Offset)> Renderers = new();
        public readonly HashSet<IntPtr> Known = new();
        public float NextScan;
    }

    private readonly Dictionary<IntPtr, Hidden> hidden = new();

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
                Stealth.EndIfDaggerPutAway(player);
                if (player.IsLocallyOwned) Stealth.ProcessKills(player);
                var remaining = Stealth.Remaining(player);
                var tracked = hidden.TryGetValue(player.Pointer, out var state);

                if (remaining > 0f && !tracked) Vanish(player);
                else if (remaining > 0f)
                {
                    // A weapon swapped in while hidden joins the shimmer too.
                    if (Time.time >= state!.NextScan) Collect(player, state);
                    Shimmer(state.Renderers, remaining);
                }
                else if (tracked) Reappear(player.Pointer, state!.Renderers);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons stealth visuals failed: {e.Message}");
        }
    }

    [HideFromIl2Cpp]
    private void Vanish(PlayerAgent player)
    {
        var state = new Hidden();
        hidden[player.Pointer] = state;
        Collect(player, state);
        SetVisible(state.Renderers, false);
    }

    // Adds the player's showing renderers not tracked yet: your own first-person view (arms and
    // the item in hand), or a teammate's whole body.
    [HideFromIl2Cpp]
    private static void Collect(PlayerAgent player, Hidden state)
    {
        state.NextScan = Time.time + RescanSeconds;
        Component? root = player.IsLocallyOwned ? player.FPItemHolder : player;
        if (root == null) return;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
            if (renderer.enabled && state.Known.Add(renderer.Pointer))
                state.Renderers.Add((renderer, UnityEngine.Random.value));
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
            if (renderer != null && (!visible || renderer.gameObject.activeInHierarchy)) renderer.enabled = visible;
    }
}
