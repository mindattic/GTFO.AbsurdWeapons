using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using Enemies;
using GameData;
using Gear;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Katana's dash-slash: release a fully charged swing and, instead of swinging at once, you
/// dash forward along your aim (flattened to the ground), up to 10 m, with the swing held. The
/// moment you make contact - an enemy within reach, or a wall or prop that stops you - or the dash
/// runs out, the held swing is released, so the charged cut lands right there. The dash moves you
/// through the game's own player controller, with collision, so it syncs like any movement; the
/// cut is the Katana's own charged attack.
/// </summary>
[HarmonyPatch]
internal static class KatanaDash
{
    internal static ConfigEntry<float> Distance = null!;
    internal static ConfigEntry<float> Seconds = null!;
    internal static ConfigEntry<float> SlashRadius = null!;
    internal static ConfigEntry<float> SlashStagger = null!;
    private const float FullChargeLeeway = 0.95f; // a charge held this share of the full time counts

    internal static void Bind(ConfigFile config)
    {
        Distance = config.Bind("Katana", "DashDistance", 10f, "Metres the full-charge dash moves you (it stops sooner on contact with an enemy or a wall).");
        Seconds = config.Bind("Katana", "DashSeconds", 0.6f, "Seconds the full dash takes (so ~17 m/s at the defaults).");
        SlashRadius = config.Bind("Katana", "DashSlashRadius", 1.2f, "How close an enemy has to be to count as contact (the held swing releases).");
        SlashStagger = config.Bind("Katana", "DashSlashStagger", 3f, "Stagger multiplier of the fallback cut, if the held swing can't be released.");
    }

    // When the local player's Katana started charging (Time.time), or -1.
    private static float chargeStarted = -1f;

    // Set while DashRunner releases the held swing itself, so the prefix lets that one through.
    internal static bool Releasing;

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.ChangeState))]
    [HarmonyPostfix]
    private static void AfterChangeState(MeleeWeaponFirstPerson __instance, eMeleeWeaponState newState)
    {
        if (newState is not (eMeleeWeaponState.AttackChargeUpLeft or eMeleeWeaponState.AttackChargeUpRight)) return;
        if (__instance.MeleeArchetypeData?.persistentID != MeleeInjector.KatanaId) return;
        if (__instance.Owner == null || !__instance.Owner.IsLocallyOwned || DashRunner.Active) return;
        chargeStarted = Time.time;
    }

    // A full charge's release is held back (the swing waits) and becomes a dash; the swing goes
    // when DashRunner says so. While the dash runs, the weapon keeps asking to release - held too.
    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.ChangeState))]
    [HarmonyPrefix]
    private static bool BeforeChangeState(MeleeWeaponFirstPerson __instance, eMeleeWeaponState newState)
    {
        try
        {
            if (Releasing || newState is not (eMeleeWeaponState.AttackChargeReleaseLeft or eMeleeWeaponState.AttackChargeReleaseRight)) return true;
            var archetype = __instance.MeleeArchetypeData;
            if (archetype == null || archetype.persistentID != MeleeInjector.KatanaId) return true;
            var owner = __instance.Owner;
            if (owner == null || !owner.IsLocallyOwned) return true;
            if (DashRunner.Active) return false;
            if (chargeStarted < 0f) return true;

            var held = Time.time - chargeStarted;
            chargeStarted = -1f;
            var animations = GameDataBlockBase<MeleeAnimationSetDataBlock>.GetBlock(archetype.MeleeAnimationSet);
            var full = animations != null ? animations.MaxDamageChargeTime : 1f;
            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons Katana charge released after {held:0.00} s (full charge {full:0.00} s){(held < full * FullChargeLeeway ? " - not full, a normal swing" : " - dash, swing held")}.");
            if (!Formulas.IsFullCharge(held, full, FullChargeLeeway)) return true;

            var aim = owner.FPSCamera != null ? owner.FPSCamera.Forward : owner.transform.forward;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.0001f) return true;
            DashRunner.Start(owner, __instance, newState, aim.normalized, archetype.ChargedAttackDamage, archetype.persistentID);
            return false;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Katana dash failed: {e}");
            return true;
        }
    }
}

/// <summary>
/// Runs the local player's dash, a little each frame, until contact - an enemy within reach, or a
/// wall that stops the movement - or the end of its distance; then releases the held swing.
/// </summary>
internal sealed class DashRunner : MonoBehaviour
{
    private static PlayerAgent? player;
    private static MeleeWeaponFirstPerson? weapon;
    private static eMeleeWeaponState releaseState;
    private static Vector3 direction;
    private static float left, damage;
    private static uint gearCategoryId;
    private static Vector3 startPosition;

    private const float WallProbeRadius = 0.3f, WallSkin = 0.15f; // a body-height probe, clear of floor bumps and stairs

    internal static bool Active => player != null;

    public DashRunner(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Start(PlayerAgent owner, MeleeWeaponFirstPerson katana, eMeleeWeaponState release, Vector3 dir, float slashDamage, uint categoryId)
    {
        player = owner;
        weapon = katana;
        releaseState = release;
        direction = dir;
        left = KatanaDash.Seconds.Value;
        damage = slashDamage;
        gearCategoryId = categoryId;
        startPosition = owner.Position;
    }

    private void Update()
    {
        if (player == null) return;
        try
        {
            if (!player.Alive)
            {
                Finish("you died");
                return;
            }
            var dt = Mathf.Min(Time.deltaTime, left);
            left -= dt;
            var from = player.Position;

            // An enemy in the way: move only up to it, and that's contact.
            var step = KatanaDash.Distance.Value / KatanaDash.Seconds.Value * dt;
            var reached = Physics.SphereCast(from + Vector3.up, KatanaDash.SlashRadius.Value, direction, out var hit, step, LayerManager.MASK_ENEMY_DAMAGABLE)
                && hit.collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner is { Alive: true };
            if (reached) step = hit.distance;

            // A wall or prop ahead within this step is contact too: move up to it. (Looked ahead
            // rather than measured after moving - the agent's position only catches up later.)
            var wall = default(RaycastHit);
            var blocked = !reached && Physics.SphereCast(from + Vector3.up, WallProbeRadius, direction, out wall, step + WallSkin, LayerManager.MASK_WORLD);
            if (blocked) step = Mathf.Max(0f, wall.distance - WallSkin);
            player.PlayerCharacterController?.Move(direction * step);

            if (reached || EnemyTouching(from)) Finish("an enemy");
            else if (blocked) Finish("a wall");
            else if (left <= 0f) Finish("its full distance");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Katana dash step failed: {e.Message}");
            Finish("an error");
        }
    }

    // Contact: the held swing goes now. If the game won't take the release, a cut through the
    // nearest limb of each enemy in reach stands in for it.
    [HideFromIl2Cpp]
    private static void Finish(string contact)
    {
        var owner = player;
        var katana = weapon;
        player = null;
        weapon = null;
        if (owner == null) return;
        Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons Katana dash: contact with {contact} after {Vector3.Distance(startPosition, owner.Position):0.0} m - swing released.");
        try
        {
            KatanaDash.Releasing = true;
            katana?.ChangeState(releaseState);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons Katana couldn't release its held swing ({e.Message}) - cutting instead.");
            if (owner.Alive) FallbackCut(owner);
        }
        finally
        {
            KatanaDash.Releasing = false;
        }
    }

    // Any living enemy within reach of this frame's stretch of the dash.
    [HideFromIl2Cpp]
    private static bool EnemyTouching(Vector3 from)
    {
        var center = player!.Position + Vector3.up;
        foreach (var collider in Physics.OverlapCapsule(from + Vector3.up, center, KatanaDash.SlashRadius.Value, LayerManager.MASK_ENEMY_DAMAGABLE))
            if (collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner is { Alive: true }) return true;
        return false;
    }

    [HideFromIl2Cpp]
    private static void FallbackCut(PlayerAgent owner)
    {
        var center = owner.Position + Vector3.up;
        var cut = new HashSet<IntPtr>();
        foreach (var collider in Physics.OverlapSphere(center, KatanaDash.SlashRadius.Value + 0.5f, LayerManager.MASK_ENEMY_DAMAGABLE))
        {
            var limb = collider.GetComponent<Dam_EnemyDamageLimb>();
            var enemy = limb?.m_base?.Owner;
            if (limb == null || enemy == null || !enemy.Alive || !cut.Add(enemy.Pointer)) continue;
            limb.BulletDamage(damage, owner.Cast<Agent>(), collider.ClosestPoint(center), direction, -direction,
                allowDirectionalBonus: false, KatanaDash.SlashStagger.Value, 1f, gearCategoryId);
        }
    }
}
