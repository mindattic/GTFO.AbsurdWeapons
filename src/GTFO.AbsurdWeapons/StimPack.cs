using System;
using BepInEx.Configuration;
using GameData;
using Agents;
using HarmonyLib;
using Localization;
using Player;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Stim Pack: a consumable of its own - the I2-LP health syringe's item cloned under a new id,
/// so the use animation, stack and sync stay vanilla and the I2-LP itself is untouched - that's an
/// overheal like Doom's: it heals <see cref="HealPercent"/> of max health and can take you past
/// max, up to <see cref="MaxHealthPercent"/>; whatever is above max then drains back down to it at
/// <see cref="DrainPercentPerSecond"/>. Damage eats into the overheal first; once health is back at
/// or below max the overheal is over. Separately, for <see cref="DamageBoostSeconds"/> after a Stim
/// Pack, everything the user hits takes <see cref="DamageBoostPercent"/> more damage.
/// The syringe's heal is hardcoded in native code (vanilla rolls 5-15% of max health). It reaches
/// the player through Dam_SyncedDamageBase.AddHealth, which med kits use too, so the syringe's own
/// apply coroutine flags the call window - for a Stim Pack only - and only AddHealth calls made
/// inside it are rewritten.
/// </summary>
[HarmonyPatch]
internal static class StimPack
{
    // An ItemDataBlock id, next to the Hand Grenade's. Cloned from the I2-LP (140).
    internal const uint ItemId = 1204;
    private const uint SourceItemId = 140;

    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<float> HealPercent = null!;
    internal static ConfigEntry<float> MaxHealthPercent = null!;
    internal static ConfigEntry<float> DrainPercentPerSecond = null!;
    internal static ConfigEntry<float> DamageBoostPercent = null!;
    internal static ConfigEntry<float> DamageBoostSeconds = null!;

    [ThreadStatic] private static bool applying;
    private static float boostUntil = -1f;

    internal static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("Stim Pack", "Stim Pack Enabled", true,
            "Adds the Stim Pack, a consumable of its own (the I2-LP is left as it is). Takes effect on restart.");
        HealPercent = config.Bind("Stim Pack", "HealPercent", 33f, "Health a Stim Pack gives, as a percent of max health.");
        MaxHealthPercent = config.Bind("Stim Pack", "MaxHealthPercent", 200f, "How high a Stim Pack can take your health, as a percent of max health.");
        DrainPercentPerSecond = config.Bind("Stim Pack", "DrainPercentPerSecond", 1f, "How fast health above max drains back down to max, in percent of max health per second.");
        DamageBoostPercent = config.Bind("Stim Pack", "DamageBoostPercent", 33f, "Extra damage dealt (guns and melee) while the Stim Pack's boost runs, in percent.");
        DamageBoostSeconds = config.Bind("Stim Pack", "DamageBoostSeconds", 15f, "How long the damage boost lasts after a Stim Pack.");
    }

    private static bool Boosting(Agent? source)
    {
        if (Time.time >= boostUntil || source == null) return false;
        var local = PlayerManager.GetLocalPlayerAgent();
        return local != null && source.Pointer == local.Pointer;
    }

    private static float Boost => 1f + DamageBoostPercent.Value / 100f;

    [HarmonyPatch(typeof(Dam_EnemyDamageLimb), nameof(Dam_EnemyDamageLimb.BulletDamage))]
    [HarmonyPrefix]
    private static void BoostBullet(ref float dam, Agent sourceAgent)
    {
        if (Boosting(sourceAgent)) dam *= Boost;
    }

    [HarmonyPatch(typeof(Dam_EnemyDamageLimb), nameof(Dam_EnemyDamageLimb.MeleeDamage))]
    [HarmonyPrefix]
    private static void BoostMelee(ref float dam, Agent sourceAgent)
    {
        if (Boosting(sourceAgent)) dam *= Boost;
    }

    internal static bool IsOurs(ItemDataBlock? block) => block != null && block.persistentID == ItemId;

    // After GTFO.EZ, same as the guns. Names are untranslated text with no localization id, or the
    // translation table would put the I2-LP's name straight back.
    [HarmonyPatch(typeof(GameDataInit), nameof(GameDataInit.Initialize))]
    [HarmonyPostfix]
    [HarmonyAfter(Plugin.GtfoEzGuid)]
    private static void AddItem()
    {
        try
        {
            if (!Enabled.Value || GameDataBlockBase<ItemDataBlock>.HasBlock(ItemId)) return;
            var source = GameDataBlockBase<ItemDataBlock>.GetBlock(SourceItemId);
            if (source == null)
            {
                Plugin.Logger.LogError($"GTFO.AbsurdWeapons: couldn't find the I2-LP (item {SourceItemId}) - not adding the Stim Pack.");
                return;
            }
            var item = DataBlocks.Clone(source);
            var name = DataBlocks.DisplayName("Stim Pack");
            item.persistentID = ItemId;
            item.name = "StimPack";
            item.publicName = name;
            item.LocalizedName = new LocalizedText { Id = 0, UntranslatedText = name };
            item.terminalItemLongName = new LocalizedText { Id = 0, UntranslatedText = "Stim Pack" };
            item.terminalItemShortName = "STIM_PACK";
            GameDataBlockBase<ItemDataBlock>.AddBlock(item, -1);
            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons added the Stim Pack (consumable; item id {ItemId}, cloned from the I2-LP).");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons failed to add the Stim Pack: {e}");
        }
    }

    [HarmonyPatch(typeof(SyringeFirstPerson._ApplySyringe_d__18), nameof(SyringeFirstPerson._ApplySyringe_d__18.MoveNext))]
    [HarmonyPrefix]
    private static void BeforeApply(SyringeFirstPerson._ApplySyringe_d__18 __instance)
    {
        var syringe = __instance.__4__this;
        applying = syringe != null && IsOurs(syringe.ItemDataBlock);
    }

    // A finalizer, so the flag clears even if MoveNext throws - otherwise the next med kit heal
    // would be rewritten as a Stim Pack.
    [HarmonyPatch(typeof(SyringeFirstPerson._ApplySyringe_d__18), nameof(SyringeFirstPerson._ApplySyringe_d__18.MoveNext))]
    [HarmonyFinalizer]
    private static void AfterApply() => applying = false;

    [HarmonyPatch(typeof(Dam_SyncedDamageBase), nameof(Dam_SyncedDamageBase.AddHealth))]
    [HarmonyPrefix]
    private static void BeforeAddHealth(Dam_SyncedDamageBase __instance, ref float addHealth)
    {
        if (!applying) return;
        var max = Overheal.BaseMax(__instance);
        addHealth = max * HealPercent.Value / 100f;
        Overheal.Grant(__instance, addHealth);
        boostUntil = Time.time + DamageBoostSeconds.Value;
    }

    [HarmonyPatch(typeof(Dam_PlayerDamageLocal), nameof(Dam_PlayerDamageLocal.Update))]
    [HarmonyPostfix]
    private static void Tick(Dam_PlayerDamageLocal __instance) => Overheal.Tick(__instance);

    // A new level means a new local player; a buff still running from the last one is dropped,
    // and a max raised for it is put back on the object the game reuses. The raise comes off
    // before the game's own Setup, which sets HealthMax afresh - taken off after, it would cut
    // into the new level's max.
    [HarmonyPatch(typeof(Dam_PlayerDamageLocal), nameof(Dam_PlayerDamageLocal.Setup))]
    [HarmonyPrefix]
    private static void BeforeSetup(Dam_PlayerDamageLocal __instance) => Overheal.DropRaise(__instance);

    [HarmonyPatch(typeof(Dam_PlayerDamageLocal), nameof(Dam_PlayerDamageLocal.Setup))]
    [HarmonyPostfix]
    private static void Setup(Dam_PlayerDamageLocal __instance)
    {
        // A bot taking a slot mid-level sets up its own damage too; that mustn't drop the player's
        // overheal (and strand their raised max health).
        if (__instance.Owner?.Owner is { IsBot: true }) return;
        Overheal.Clear(__instance);
        boostUntil = -1f;
    }
}

/// <summary>
/// The local player's overheal. AddHealth caps at max natively, so the overheal is pushed with the
/// synced SendSetHealth afterwards, raising HealthMax for its duration if the game caps that too.
/// Only the overheal's own share of HealthMax (<c>raisedBy</c>) is ever added or taken back, so
/// anything else that changes max health meanwhile (GTFO.EZ's Bold artifact) is left intact; the
/// player's real max is always the live HealthMax minus that share.
/// Health is stepped once a second rather than every frame to keep network traffic down.
/// </summary>
internal static class Overheal
{
    private const float TickSeconds = 1f;
    private const float Epsilon = 0.01f;
    private const int OverhealAttempts = 2;

    private static IntPtr owner;
    private static bool active;
    private static float target, raisedBy, lastSent = float.NaN, untilTick;
    private static int attempt;

    // Max health without the overheal's own raise.
    internal static float BaseMax(Dam_SyncedDamageBase damage) =>
        damage.Pointer == owner ? damage.HealthMax - raisedBy : damage.HealthMax;

    // Raises or lowers the overheal's share of HealthMax to `share`, leaving the rest of max alone.
    private static void SetRaise(Dam_SyncedDamageBase damage, float share)
    {
        share = Mathf.Max(0f, share);
        damage.HealthMax += share - raisedBy;
        raisedBy = share;
    }

    internal static void Grant(Dam_SyncedDamageBase damage, float amount)
    {
        if (!active || owner != damage.Pointer)
        {
            Clear();
            owner = damage.Pointer;
        }
        var max = BaseMax(damage);
        var cap = max * Mathf.Max(100f, StimPack.MaxHealthPercent.Value) / 100f;
        target = Mathf.Min(cap, Current(damage.Health) + amount);
        if (target <= max + Epsilon)
        {
            End(damage, "plain heal, nothing over max");
            return;
        }
        active = true;
        attempt = 0;
        lastSent = float.NaN;
        untilTick = TickSeconds;
        Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons Stim Pack: overhealing to {target:0.##} HP (max {max:0.##}).");
    }

    // Takes the overheal's raise back off `damage`'s max, if it's the overhealed one (a bot's own
    // damage object never is).
    internal static void DropRaise(Dam_SyncedDamageBase damage)
    {
        if (raisedBy > 0f && damage.Pointer == owner) SetRaise(damage, 0f);
    }

    internal static void Clear(Dam_SyncedDamageBase? sameInstance = null)
    {
        if (sameInstance != null) DropRaise(sameInstance);
        owner = IntPtr.Zero;
        active = false;
        raisedBy = 0f;
        lastSent = float.NaN;
    }

    internal static void Tick(Dam_PlayerDamageLocal local)
    {
        if (!active || local.Pointer != owner) return;
        untilTick -= Time.deltaTime;
        if (untilTick > 0f) return;
        var elapsed = TickSeconds - untilTick;
        untilTick = TickSeconds;

        // First the overheal is pushed past the native cap (raising max if a plain push doesn't
        // stick); once it has stuck - or damage has taken health back under max - it only drains.
        var health = local.Health;
        var max = BaseMax(local);
        if (attempt <= OverhealAttempts)
        {
            if (health >= target - Epsilon || health < max - Epsilon)
            {
                attempt = OverhealAttempts + 1;
            }
            else if (attempt < OverhealAttempts)
            {
                if (attempt == 1 && target > local.HealthMax + Epsilon) SetRaise(local, target - max);
                local.SendSetHealth(target);
                lastSent = target;
                attempt++;
                return;
            }
            else
            {
                Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons Stim Pack: overheal didn't stick (health {health:0.##}, wanted {target:0.##}).");
                attempt = OverhealAttempts + 1;
            }
        }

        var now = Current(health);
        if (now <= max + Epsilon)
        {
            End(local, "back at max health");
            return;
        }
        var next = Mathf.Max(max, now - max * StimPack.DrainPercentPerSecond.Value / 100f * elapsed);
        if (raisedBy > 0f) SetRaise(local, next - max);
        local.SendSetHealth(next);
        lastSent = next;
        if (next <= max + Epsilon) End(local, "drained back to max health");
    }

    // On a client, Health can briefly read the pre-SendSetHealth value; trusting the last value
    // sent stops a stale read from undoing a drain step.
    private static float Current(float health) => float.IsNaN(lastSent) ? health : Mathf.Min(health, lastSent);

    private static void End(Dam_SyncedDamageBase damage, string reason)
    {
        if (raisedBy > 0f && damage.Pointer == owner)
        {
            SetRaise(damage, 0f);
            if (damage.Health > damage.HealthMax) damage.SendSetHealth(damage.HealthMax);
        }
        if (active) Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons Stim Pack overheal ended: {reason}.");
        Clear();
    }
}
