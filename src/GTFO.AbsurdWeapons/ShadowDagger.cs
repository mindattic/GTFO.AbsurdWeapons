using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using Enemies;
using FX_EffectSystem;
using Gear;
using GTFO.API;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
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
/// every game keeps the same list of who is hidden. Hidden players fade to 90% transparent, coming
/// back in as their time runs out (see <see cref="StealthVisuals"/>). Putting the dagger away - wielding anything else - ends
/// it at once.
/// </summary>
[HarmonyPatch]
internal static class Stealth
{
    internal const float StabSpeed = 2.5f; // the fallback for WeaponSettings.ShadowDagger.LightAttackSpeed

    internal static ConfigEntry<float> Seconds = null!;
    internal static ConfigEntry<float> BonusPerKill = null!;
    internal static ConfigEntry<float> MaxBonus = null!;

    // A charged hit counts as full when it deals at least this share of the full charged damage.
    private const float FullChargeShare = 0.95f;
    private const string StealthEvent = "MindAttic.GTFO.AbsurdWeapons.Stealth";

    // Keyed by the player's Steam id (SNet_Player.Lookup): when their stealth ends, in Time.time.
    private static readonly Dictionary<ulong, float> hiddenUntil = new();

    internal static void Bind(ConfigFile config)
    {
        Seconds = config.Bind("Shadow Dagger", "StealthSeconds", 6f, "Seconds a fully charged hit hides you for; a further one restarts the count.");
        BonusPerKill = config.Bind("Shadow Dagger", "StealthBonusPerKill", 0.25f, "Damage bonus (0.25 = +25%) added per kill made while hidden.");
        MaxBonus = config.Bind("Shadow Dagger", "StealthMaxBonus", 1f, "Cap on the stacked kill bonus (1 = +100%).");
    }

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

            damageBonus = Formulas.StealthBonusAfterKill(damageBonus, BonusPerKill.Value, MaxBonus.Value);
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
            var seconds = Mathf.Max(Seconds.Value, Remaining(owner));
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
/// Shows stealth on every game, from the shared hidden list, so everyone sees the same thing: the
/// hidden player fades out to 90% transparent, and how see-through they are is tied straight to
/// how much stealth they have left - it fades back in as the time runs down, and anything that
/// adds time (another full hit, a full-windup kill) makes them more see-through again. Every part
/// of them shown (each renderer) is switched to a see-through copy of its own materials for the
/// duration, and back to its own when it ends. Teammates see the whole body; the hidden player
/// sees their first-person view (arms and the dagger itself).
/// Even out of stealth, the Shadow Dagger in anyone's hand gives off a pulsing violet glow, the
/// Gravity Hammer's old light.
/// </summary>
internal sealed class StealthVisuals : MonoBehaviour
{
    private const float MostTransparent = 0.9f; // fully stealthed: 10% there
    private const float FadeRate = 6f;          // how quickly the shown transparency follows the time left
    private const float RescanSeconds = 0.25f;  // how often to pick up renderers that appeared (a swapped-in weapon)

    // One part of a hidden player: its own materials, and the see-through copies shown meanwhile.
    private sealed class Part
    {
        public Renderer Renderer = null!;
        public Il2CppReferenceArray<Material> Original = null!;
        public Material[] Ghosts = Array.Empty<Material>();
    }

    private sealed class Hidden
    {
        public readonly List<Part> Parts = new();
        public readonly HashSet<IntPtr> Known = new();
        public float NextScan, Transparency;
    }

    private readonly Dictionary<IntPtr, Hidden> hidden = new();

    private static readonly Color GlowColor = new(0.6f, 0.3f, 1f);
    private static Shader? ghostShader;

    // Per player wielding the dagger: its glow.
    private sealed class Held
    {
        public IntPtr Item;
        public EffectLight Glow = null!;
    }

    private readonly Dictionary<IntPtr, Held> held = new();

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
                Wielding(player);

                if (remaining > 0f)
                {
                    if (!tracked) hidden[player.Pointer] = state = new Hidden();
                    // A weapon swapped in while hidden fades with the rest.
                    if (Time.time >= state!.NextScan) Collect(player, state);
                    Fade(state, remaining);
                }
                else if (tracked) Reappear(player.Pointer, state!);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons stealth visuals failed: {e.Message}");
        }
    }

    // The dagger's glow while it's in `player`'s hand.
    [HideFromIl2Cpp]
    private void Wielding(PlayerAgent player)
    {
        var item = player.Inventory?.WieldedItem;
        var isDagger = item != null && item.GearCategoryData?.persistentID == MeleeInjector.ShadowDaggerId;
        held.TryGetValue(player.Pointer, out var state);
        if (state != null && (!isDagger || state.Item != item!.Pointer))
        {
            if (state.Glow != null) Destroy(state.Glow.gameObject);
            held.Remove(player.Pointer);
            state = null;
        }
        if (!isDagger) return;

        if (state == null)
        {
            state = new Held { Item = item!.Pointer };
            var go = new GameObject("GTFO.AbsurdWeapons Shadow Dagger Glow");
            state.Glow = go.AddComponent<EffectLight>();
            state.Glow.Color = GlowColor;
            state.Glow.Range = 5f;
            state.Glow.enabled = true;
            held[player.Pointer] = state;
        }

        state.Glow.transform.position = item!.transform.position;
        state.Glow.Intensity = 2f * (0.75f + 0.25f * Mathf.Sin(Time.time * 12f));
    }

    // Switches the player's showing renderers not tracked yet to see-through copies of their
    // materials: your own first-person view (arms and the item in hand), or a teammate's whole body.
    [HideFromIl2Cpp]
    private static void Collect(PlayerAgent player, Hidden state)
    {
        state.NextScan = Time.time + RescanSeconds;
        Component? root = player.IsLocallyOwned ? player.FPItemHolder : player;
        if (root == null) return;
        ghostShader ??= Shader.Find("Sprites/Default");
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
        {
            if (!renderer.enabled || !state.Known.Add(renderer.Pointer)) continue;
            var original = renderer.sharedMaterials;
            if (original == null || original.Length == 0) continue;
            var ghosts = new Material[original.Length];
            for (var i = 0; i < original.Length; i++)
            {
                ghosts[i] = new Material(ghostShader);
                var source = original[i];
                if (source != null && source.HasProperty("_MainTex")) ghosts[i].mainTexture = source.mainTexture;
            }
            renderer.materials = new Il2CppReferenceArray<Material>(ghosts);
            state.Parts.Add(new Part { Renderer = renderer, Original = original, Ghosts = ghosts });
        }
    }

    // Transparency follows the stealth time left: 90% at a full stealth (or more), back to solid
    // as it runs out - eased, so it fades rather than snaps when time is added.
    [HideFromIl2Cpp]
    private static void Fade(Hidden state, float remaining)
    {
        var target = MostTransparent * Mathf.Clamp01(remaining / Mathf.Max(0.1f, Stealth.Seconds.Value));
        state.Transparency = Mathf.Lerp(state.Transparency, target, 1f - Mathf.Exp(-FadeRate * Time.deltaTime));
        var tint = new Color(1f, 1f, 1f, 1f - state.Transparency);
        foreach (var part in state.Parts)
            foreach (var ghost in part.Ghosts)
                if (ghost != null) ghost.color = tint;
    }

    // Every part back to its own materials.
    [HideFromIl2Cpp]
    private void Reappear(IntPtr player, Hidden state)
    {
        foreach (var part in state.Parts)
        {
            if (part.Renderer != null) part.Renderer.sharedMaterials = part.Original;
            foreach (var ghost in part.Ghosts)
                if (ghost != null) Destroy(ghost);
        }
        hidden.Remove(player);
    }
}
