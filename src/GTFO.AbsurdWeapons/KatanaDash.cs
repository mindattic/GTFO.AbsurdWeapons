using System;
using System.Collections.Generic;
using Agents;
using Enemies;
using GameData;
using Gear;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Katana's dash-slash: releasing a fully charged swing dashes you forward along your aim
/// (flattened to the ground) and slashes every enemy you pass through. The dash moves you through
/// the game's own player controller, with collision, so walls and props stop it; it's your own
/// movement, so it syncs like any other. The slashes go through the enemy limbs' normal bullet
/// damage path, so they sync too.
/// </summary>
[HarmonyPatch]
internal static class KatanaDash
{
    internal const float Distance = 40f;
    internal const float Seconds = 0.6f; // ~67 m/s
    internal const float SlashRadius = 1.2f;
    internal const float SlashStagger = 3f;
    private const float FullChargeLeeway = 0.95f; // a charge held this share of the full time counts

    // When the local player's Katana started charging (Time.time), or -1.
    private static float chargeStarted = -1f;

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.ChangeState))]
    [HarmonyPostfix]
    private static void AfterChangeState(MeleeWeaponFirstPerson __instance, eMeleeWeaponState newState)
    {
        try
        {
            var archetype = __instance.MeleeArchetypeData;
            if (archetype == null || archetype.persistentID != MeleeInjector.KatanaId) return;
            var owner = __instance.Owner;
            if (owner == null || !owner.IsLocallyOwned) return;

            if (newState is eMeleeWeaponState.AttackChargeUpLeft or eMeleeWeaponState.AttackChargeUpRight)
            {
                chargeStarted = Time.time;
                return;
            }
            if (newState is not (eMeleeWeaponState.AttackChargeReleaseLeft or eMeleeWeaponState.AttackChargeReleaseRight)) return;
            if (chargeStarted < 0f) return;

            var held = Time.time - chargeStarted;
            chargeStarted = -1f;
            var animations = GameDataBlockBase<MeleeAnimationSetDataBlock>.GetBlock(archetype.MeleeAnimationSet);
            var full = animations != null ? animations.MaxDamageChargeTime : 1f;
            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons Katana charge released after {held:0.00} s (full charge {full:0.00} s){(held < full * FullChargeLeeway ? " - not full, no dash" : " - dash")}.");
            if (!Formulas.IsFullCharge(held, full, FullChargeLeeway)) return;

            var aim = owner.FPSCamera != null ? owner.FPSCamera.Forward : owner.transform.forward;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.0001f) return;
            DashRunner.Start(owner, aim.normalized, archetype.ChargedAttackDamage, archetype.persistentID);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Katana dash failed: {e}");
        }
    }
}

/// <summary>Runs the local player's dash, a little each frame, slashing what it passes.</summary>
internal sealed class DashRunner : MonoBehaviour
{
    private static PlayerAgent? player;
    private static Vector3 direction;
    private static float left, damage;
    private static uint gearCategoryId;
    private static readonly HashSet<IntPtr> slashed = new();
    private static Vector3 startPosition;

    public DashRunner(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Start(PlayerAgent owner, Vector3 dir, float slashDamage, uint categoryId)
    {
        player = owner;
        direction = dir;
        left = KatanaDash.Seconds;
        damage = slashDamage;
        gearCategoryId = categoryId;
        slashed.Clear();
        startPosition = owner.Position;
    }

    private void Update()
    {
        if (player == null || left <= 0f) return;
        try
        {
            if (!player.Alive)
            {
                player = null;
                return;
            }
            var dt = Mathf.Min(Time.deltaTime, left);
            left -= dt;
            var controller = player.PlayerCharacterController;
            var from = player.Position;
            controller?.Move(direction * (KatanaDash.Distance / KatanaDash.Seconds) * dt);
            Slash(from);
            if (left <= 0f)
            {
                Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons Katana dash done: moved {Vector3.Distance(startPosition, player.Position):0.0} m of {KatanaDash.Distance} m, slashed {slashed.Count}.");
                player = null;
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Katana dash step failed: {e.Message}");
            player = null;
        }
    }

    // Every enemy within reach of this frame's stretch of the dash (swept from `from` to where the
    // player is now, so a fast dash can't skip anyone between frames), once each per dash, through
    // its nearest limb.
    [HideFromIl2Cpp]
    private static void Slash(Vector3 from)
    {
        var center = player!.Position + Vector3.up;
        foreach (var collider in Physics.OverlapCapsule(from + Vector3.up, center, KatanaDash.SlashRadius, LayerManager.MASK_ENEMY_DAMAGABLE))
        {
            var limb = collider.GetComponent<Dam_EnemyDamageLimb>();
            var enemy = limb?.m_base?.Owner;
            if (limb == null || enemy == null || !enemy.Alive || !slashed.Add(enemy.Pointer)) continue;
            limb.BulletDamage(damage, player.Cast<Agent>(), collider.ClosestPoint(center), direction, -direction,
                allowDirectionalBonus: false, KatanaDash.SlashStagger, 1f, gearCategoryId);
        }
    }
}
