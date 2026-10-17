using System;
using System.Collections.Generic;
using Agents;
using AK;
using BepInEx.Configuration;
using Enemies;
using Gear;
using GTFO.API;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Player;
using SNetwork;
using UnityEngine;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// The Victory Banner: a plantable tool. Firing it sets it down right in front of you - no throw -
/// and firing again while yours is still up picks it back up instead of planting a second one.
/// Switching your tool slot away from the Victory Banner retires it on every client (checked
/// against whichever gear is currently wielded, every half second).
/// It pulses an expanding green ring every couple of seconds out to its radius, healing every
/// teammate standing inside it (self-applied per client, like Shadow Dagger's hidden table) and
/// giving the local player a damage buff while they're in it - a base bonus that climbs the more
/// enemies they kill while standing there, reset the moment they step out. Every number below is a
/// config multiplier (`[Victory Banner]`).
/// </summary>
[HarmonyPatch]
internal static class VictoryBanner
{
    internal static ConfigEntry<float> Radius = null!;
    internal static ConfigEntry<float> PulseSeconds = null!;
    internal static ConfigEntry<float> HealPerPulse = null!;
    internal static ConfigEntry<float> SafetyLifetimeSeconds = null!;
    internal static ConfigEntry<float> BaseDamageBuff = null!;
    internal static ConfigEntry<float> MaxDamageBuff = null!;
    internal static ConfigEntry<float> DamageBuffPerKill = null!;

    private const string PlantEvent = "MindAttic.GTFO.AbsurdWeapons.BannerPlant";
    private const string PickUpEvent = "MindAttic.GTFO.AbsurdWeapons.BannerPickUp";

    // Set while the launcher itself is firing, so glue from anything else stays glue (see
    // GrenadeLauncher, which guards the same way for the same reason).
    private static int launcherFiring;

    private static bool localInRange;
    private static float killBonus;

    // A kill isn't reflected in EnemyAgent.Alive synchronously for a non-host client - the host has
    // to confirm it - so kills are checked a moment later, same as Shadow Dagger's kill bonus.
    private const float KillCheckDelay = 0.4f;
    private static readonly List<(EnemyAgent Enemy, float CheckAt)> pendingKills = new();

    internal static void Bind(ConfigFile config)
    {
        Radius = config.Bind("Victory Banner", "Radius", 8f, "Metres out to which its ring heals and buffs.");
        PulseSeconds = config.Bind("Victory Banner", "PulseSeconds", 2f, "Seconds between heal pulses.");
        HealPerPulse = config.Bind("Victory Banner", "HealPercentPerPulse", 8f, "Percent of max health restored per pulse to each teammate inside the aura.");
        SafetyLifetimeSeconds = config.Bind("Victory Banner", "SafetyLifetimeSeconds", 600f,
            "A safety-net despawn in case it's never picked back up and its owner's tool never changes.");
        BaseDamageBuff = config.Bind("Victory Banner", "BaseDamageBuff", 0.15f,
            "Damage multiplier bonus (0.15 = +15%) for the local player while standing in the ring.");
        MaxDamageBuff = config.Bind("Victory Banner", "MaxDamageBuff", 0.25f, "The damage bonus's cap once kills have built it up.");
        DamageBuffPerKill = config.Bind("Victory Banner", "DamageBuffPerKill", 0.02f,
            "How much the bonus climbs per enemy killed while standing in the ring; resets to the base when you step out.");
    }

    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.FireBurst))]
    [HarmonyPrefix]
    private static void BeforeFireBurst() => launcherFiring++;

    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.FireBurst))]
    [HarmonyFinalizer]
    private static void AfterFireBurst() => launcherFiring = Math.Max(0, launcherFiring - 1);

    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.FireSingle))]
    [HarmonyPrefix]
    private static void BeforeFireSingle() => launcherFiring++;

    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.FireSingle))]
    [HarmonyFinalizer]
    private static void AfterFireSingle() => launcherFiring = Math.Max(0, launcherFiring - 1);

    internal static void Init()
    {
        NetworkAPI.RegisterEvent<PlantMessage>(PlantEvent, (sender, message) =>
            BannerPylon.Spawn(sender, new Vector3(message.X, message.Y, message.Z), new Vector3(message.NormalX, message.NormalY, message.NormalZ)));
        NetworkAPI.RegisterEvent<PickUpMessage>(PickUpEvent, (sender, _) => BannerPylon.Retire(sender));
    }

    // Called by the owner's own game (BannerPylon.StillInToolSlot) when their tool slot - PageUp/
    // PageDown, Home's restore, or just re-equipping something else - no longer holds the Victory
    // Banner: tells every client (itself included) to drop their copy, same as picking it up by hand.
    internal static void BroadcastPickUp() => NetworkAPI.InvokeEvent(PickUpEvent, new PickUpMessage());

    // Every glob the C-Foam launcher would fire goes through here (the local player's only). Ours
    // toggles: plant if you don't have one up, pick up if you do.
    [HarmonyPatch(typeof(ProjectileManager), nameof(ProjectileManager.WantToFireGlue))]
    [HarmonyPrefix]
    private static bool BeforeFireGlue(PlayerAgent owner, Vector3 pos, Vector3 velocity)
    {
        try
        {
            if (owner == null || !owner.IsLocallyOwned) return true;
            var wielded = owner.Inventory?.WieldedItem;
            if (wielded == null || wielded.GearCategoryData?.persistentID != GunInjector.VictoryBannerId) return true;
            if (launcherFiring == 0) return true;

            Toggle(owner, velocity);
            return false;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Victory Banner failed: {e}");
            return false;
        }
    }

    // No charge-up: while the Victory Banner is in hand the launcher's own update (pressure, bursts)
    // doesn't run at all - a press of fire plants it, or picks it back up, right away.
    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.Update))]
    [HarmonyPrefix]
    private static bool InsteadOfCharging(GlueGun __instance)
    {
        try
        {
            if (__instance.GearCategoryData?.persistentID != GunInjector.VictoryBannerId) return true;
            var owner = __instance.Owner;
            if (owner != null && owner.IsLocallyOwned && owner.Inventory?.WieldedItem?.Pointer == __instance.Pointer
                && InputMapper.GetButtonDownKeyMouse(InputAction.Fire, eFocusState.FPS))
                Toggle(owner, owner.FPSCamera != null ? owner.FPSCamera.Forward : owner.transform.forward);
            return false;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Victory Banner input failed: {e.Message}");
            return true;
        }
    }

    // In hand it's the flag itself: the launcher's model hidden, a smaller copy of the flag in its place.
    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.OnWield))]
    [HarmonyPostfix]
    private static void HoldTheFlag(GlueGun __instance)
    {
        try
        {
            if (__instance.GearCategoryData?.persistentID != GunInjector.VictoryBannerId) return;
            BannerPylon.HoldModel(__instance.gameObject);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons couldn't show the held Victory Banner: {e.Message}");
        }
    }

    // Puts your banner down in front of you, like a sentry, and switches you to your main weapon.
    // Already down: nothing - it's picked back up by walking to it (BannerPylon.UpdatePickup).
    private static void Toggle(PlayerAgent owner, Vector3 forward)
    {
        var lookup = owner.Owner != null ? owner.Owner.Lookup : 0UL;
        if (BannerPylon.HasActive(lookup)) return;

        var aim = forward.sqrMagnitude > 0.0001f ? forward.normalized : owner.transform.forward;
        var origin = owner.Position + aim * 1.5f + Vector3.up * 1f;
        var position = origin + Vector3.down;
        var normal = Vector3.up;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 3f, LayerManager.MASK_WORLD))
        {
            position = hit.point;
            normal = hit.normal;
        }

        BannerPylon.Spawn(lookup, position, normal);
        Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons Victory Banner: planted at {position}.");
        NetworkAPI.InvokeEvent(PlantEvent, new PlantMessage(position, normal));
        owner.Sync?.WantsToWieldSlot(InventorySlot.GearStandard); // hands free of it, like a placed sentry
    }

    // Picking it back up (the owner, at the banner): gone on every game, and back in hand.
    internal static void PickUp(PlayerAgent owner)
    {
        var lookup = owner.Owner != null ? owner.Owner.Lookup : 0UL;
        BannerPylon.Retire(lookup);
        NetworkAPI.InvokeEvent(PickUpEvent, new PickUpMessage());
        Plugin.Logger.LogInfo("GTFO.AbsurdWeapons Victory Banner: picked back up.");
        owner.Sync?.WantsToWieldSlot(InventorySlot.GearClass);
    }

    // Infinite ammo: while the local player holds the banner, the tool ammo is kept full.
    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.Update))]
    [HarmonyPostfix]
    private static void KeepToolAmmoFull(GlueGun __instance)
    {
        try
        {
            if (__instance.GearCategoryData?.persistentID != GunInjector.VictoryBannerId) return;
            if (__instance.Owner == null || !__instance.Owner.IsLocallyOwned) return;
            var storage = PlayerBackpackManager.LocalBackpack?.AmmoStorage;
            var ammo = storage?.ClassAmmo;
            if (storage == null || ammo == null || ammo.IsFull) return;
            storage.SetAmmo(AmmoType.Class, ammo.AmmoMaxCap);
            storage.UpdateAllAmmoUI();
            storage.NeedsSync = true;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons couldn't refill the Victory Banner: {e.Message}");
        }
    }

    // Universal damage buff: every hit the local player deals while standing in a banner's ring is
    // multiplied, whatever weapon dealt it (this mod's or vanilla) - BulletDamage is the one choke
    // point every instant-damage path in the game runs through.
    [HarmonyPatch(typeof(Dam_EnemyDamageLimb), nameof(Dam_EnemyDamageLimb.BulletDamage))]
    [HarmonyPrefix]
    private static void BeforeBulletDamage(Dam_EnemyDamageLimb __instance, ref float dam, Agent sourceAgent, out bool __state)
    {
        __state = false;
        // TryCast, not `is`: the interop wraps the argument as a plain Agent, so `is PlayerAgent` never matches.
        if (!localInRange) return;
        var player = sourceAgent?.TryCast<PlayerAgent>();
        if (player == null || !player.IsLocallyOwned) return;
        dam *= 1f + CurrentDamageBuff();
        __state = __instance.m_base?.Owner != null && __instance.m_base.Owner.Alive;
    }

    [HarmonyPatch(typeof(Dam_EnemyDamageLimb), nameof(Dam_EnemyDamageLimb.BulletDamage))]
    [HarmonyPostfix]
    private static void AfterBulletDamage(Dam_EnemyDamageLimb __instance, bool __state)
    {
        if (!__state) return;
        var enemy = __instance.m_base?.Owner;
        if (enemy != null) QueueKillCheck(enemy);
    }

    private static float CurrentDamageBuff() => Math.Min(MaxDamageBuff.Value, BaseDamageBuff.Value + killBonus);

    // One check per enemy, however many buffed hits struck it (a kill counts once).
    private static void QueueKillCheck(EnemyAgent enemy)
    {
        if (pendingKills.Exists(p => p.Enemy != null && p.Enemy.Pointer == enemy.Pointer)) return;
        pendingKills.Add((enemy, Time.time + KillCheckDelay));
    }

    // Kills confirmed since they were struck. Called every frame (VictoryBannerBuff). The bonus is
    // earned by being in range when the killing blow landed, not by still being in range now.
    internal static void ProcessPendingKills()
    {
        if (pendingKills.Count == 0) return;
        for (var i = pendingKills.Count - 1; i >= 0; i--)
        {
            var (enemy, checkAt) = pendingKills[i];
            if (Time.time < checkAt) continue;
            pendingKills.RemoveAt(i);
            if (enemy == null || enemy.Alive) continue;
            killBonus = Formulas.StealthBonusAfterKill(killBonus, DamageBuffPerKill.Value, MaxDamageBuff.Value - BaseDamageBuff.Value);
        }
    }

    // Run every frame from VictoryBannerBuff: is the local player inside any active banner's ring.
    // Leaving clears the kill bonus (the next visit, even to the same banner, starts at base) and
    // drops any kill still waiting on host confirmation, so it can't retroactively bump a reset bonus.
    internal static void UpdateLocalRange(PlayerAgent local)
    {
        var inRange = BannerPylon.IsPositionBuffed(local.Position);
        if (!inRange)
        {
            killBonus = 0f;
            pendingKills.Clear();
        }
        localInRange = inRange;
    }

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct PlantMessage
    {
        public readonly float X, Y, Z, NormalX, NormalY, NormalZ;

        public PlantMessage(Vector3 position, Vector3 normal)
        {
            (X, Y, Z) = (position.x, position.y, position.z);
            (NormalX, NormalY, NormalZ) = (normal.x, normal.y, normal.z);
        }
    }

    // A zero-field struct would throw from GTFO-API's Marshal.SizeOf<T>() (no meaningful size can be
    // computed for a type with no fields), so this carries one dummy byte just to have a layout.
    private readonly struct PickUpMessage
    {
        public readonly byte Dummy;
        public PickUpMessage() => Dummy = 0;
    }
}

/// <summary>
/// One banner, set down (not thrown) and stuck where it landed. Pulses an expanding ring every
/// <see cref="VictoryBanner.PulseSeconds"/> out to <see cref="VictoryBanner.Radius"/>; every client
/// checks its own local player against the ring for both the heal and the damage buff, so neither
/// ever has to cross the network. Picking it back up, planting a new one for the same owner, or the
/// owner switching away from the Victory Banner in their tool slot all retire it, on every client.
/// </summary>
internal sealed class BannerPylon : MonoBehaviour
{
    private const float RingSeconds = 0.6f;
    private const float ToolCheckSeconds = 0.5f;
    private static readonly Color BannerColor = new(0.25f, 0.95f, 0.35f, 1f);

    private static readonly Dictionary<ulong, BannerPylon> activeByOwner = new();

    private ulong ownerLookup;
    private bool retired;
    private float placedAt, nextPulseAt, nextToolCheckAt, ringProgress = -1f;
    private LineRenderer ring = null!, aura = null!;
    private EffectLight glow = null!;
    private Transform? flag;
    private float flash; // 1 right after a heal pulse, fading

    public BannerPylon(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static bool HasActive(ulong ownerLookup) => activeByOwner.TryGetValue(ownerLookup, out var pylon) && pylon != null && !pylon.retired;

    [HideFromIl2Cpp]
    internal static void Spawn(ulong ownerLookup, Vector3 position, Vector3 normal)
    {
        if (activeByOwner.TryGetValue(ownerLookup, out var old) && old != null) old.Retire();

        var go = new GameObject("GTFO.AbsurdWeapons Victory Banner");
        go.transform.position = position;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
        var flag = BuildModel(go.transform);

        var pylon = go.AddComponent<BannerPylon>();
        pylon.flag = flag;
        pylon.ownerLookup = ownerLookup;
        pylon.placedAt = Time.time;
        pylon.nextPulseAt = Time.time; // heals right away

        var ringObject = new GameObject("Ring");
        ringObject.transform.SetParent(go.transform, false);
        pylon.ring = ringObject.AddComponent<LineRenderer>();
        pylon.ring.loop = true;
        pylon.ring.useWorldSpace = true;
        pylon.ring.positionCount = 48;
        pylon.ring.material = RingMaterial();
        pylon.ring.enabled = false;

        // The aura: a glowing ring on the ground at the healing radius, always there while it's up.
        var auraObject = new GameObject("Aura");
        auraObject.transform.SetParent(go.transform, false);
        pylon.aura = auraObject.AddComponent<LineRenderer>();
        pylon.aura.loop = true;
        pylon.aura.useWorldSpace = true;
        pylon.aura.positionCount = 64;
        pylon.aura.material = RingMaterial();

        // A green glow filling the aura, brightening with each heal.
        var glowObject = new GameObject("Glow");
        glowObject.transform.SetParent(go.transform, false);
        glowObject.transform.localPosition = Vector3.up * 1.6f;
        pylon.glow = glowObject.AddComponent<EffectLight>();
        pylon.glow.Color = BannerColor;
        pylon.glow.Range = VictoryBanner.Radius.Value * 1.2f;
        pylon.glow.Intensity = 2f;
        pylon.glow.enabled = true;

        activeByOwner[ownerLookup] = pylon;
        Sounds.Play(Sounds.BannerPlant.Value, position); // every game plants its copy, so every game hears it
    }

    [HideFromIl2Cpp]
    internal static void Retire(ulong ownerLookup)
    {
        if (activeByOwner.TryGetValue(ownerLookup, out var pylon) && pylon != null) pylon.Retire();
    }

    [HideFromIl2Cpp]
    private void Retire()
    {
        if (retired) return;
        retired = true;
        var local = PlayerManager.GetLocalPlayerAgent();
        if (local?.Owner != null && local.Owner.Lookup == ownerLookup) HidePrompt();
        if (activeByOwner.TryGetValue(ownerLookup, out var current) && current == this) activeByOwner.Remove(ownerLookup);
        Destroy(gameObject);
    }

    [HideFromIl2Cpp]
    internal static bool IsPositionBuffed(Vector3 position)
    {
        foreach (var pylon in activeByOwner.Values)
            if (pylon != null && !pylon.retired && Vector3.Distance(position, pylon.transform.position) <= VictoryBanner.Radius.Value)
                return true;
        return false;
    }

    private void Update()
    {
        if (retired) return;
        try
        {
            if (Time.time - placedAt >= VictoryBanner.SafetyLifetimeSeconds.Value)
            {
                Retire();
                return;
            }

            if (Time.time >= nextToolCheckAt)
            {
                nextToolCheckAt = Time.time + ToolCheckSeconds;
                var local = PlayerManager.GetLocalPlayerAgent();
                if (local != null && local.Owner != null && local.Owner.Lookup == ownerLookup && !StillInToolSlot())
                {
                    Retire();
                    VictoryBanner.BroadcastPickUp();
                    return;
                }
            }

            UpdatePickup();
            if (retired) return;

            if (Time.time >= nextPulseAt)
            {
                nextPulseAt = Time.time + VictoryBanner.PulseSeconds.Value;
                Pulse();
            }
            AnimateRing(Time.deltaTime);
            AnimateAura(Time.deltaTime);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons Victory Banner failed: {e.Message}");
            Retire();
        }
    }

    private const float PickupRange = 2.5f, PickupConeDegrees = 40f;
    private static bool promptShown;

    // Like a sentry: the owner walks up to their banner, looks at it, and presses Use to pick it up.
    // A prompt shows while they can.
    [HideFromIl2Cpp]
    private void UpdatePickup()
    {
        var local = PlayerManager.GetLocalPlayerAgent();
        if (local == null || local.Owner == null || local.Owner.Lookup != ownerLookup) return;
        var camera = local.FPSCamera;
        var pole = transform.position + transform.up * 1.1f;
        var canPick = local.Alive && camera != null && FocusStateManager.CurrentState == eFocusState.FPS
            && Vector3.Distance(local.Position, transform.position) <= PickupRange
            && Vector3.Angle(camera.Forward, pole - camera.Position) <= PickupConeDegrees;
        var layer = GuiManager.InteractionLayer;
        if (canPick)
        {
            if (layer != null)
            {
                layer.SetInteractPrompt("Pick up Victory Banner", "Use");
                layer.InteractPromptVisible = true;
                promptShown = true;
            }
            if (InputMapper.GetButtonDownKeyMouse(InputAction.Use, eFocusState.FPS))
            {
                HidePrompt();
                VictoryBanner.PickUp(local);
            }
        }
        else if (promptShown)
        {
            HidePrompt();
        }
    }

    private static void HidePrompt()
    {
        if (!promptShown) return;
        promptShown = false;
        var layer = GuiManager.InteractionLayer;
        if (layer != null) layer.InteractPromptVisible = false;
    }

    // Whether the Victory Banner is still what's actually equipped in the owner's tool slot - not
    // just "currently drawn" (drawing your gun to fight shouldn't retire the banner you just
    // planted). Only checkable on the owner's own game, the same way GearCycler reads a slot's
    // gear; PageUp/PageDown, re-equipping another tool, or Home's restore all show up here.
    [HideFromIl2Cpp]
    private static bool StillInToolSlot()
    {
        var backpack = PlayerBackpackManager.LocalBackpack;
        if (backpack == null || !backpack.TryGetBackpackItem(InventorySlot.GearClass, out var item) || item?.GearIDRange == null) return true;
        return item.GearIDRange.GetCompID(eGearComponent.Category) == GunInjector.VictoryBannerId;
    }

    // Heals the local player if they're standing in the ring, and lights the ring up to show it.
    [HideFromIl2Cpp]
    private void Pulse()
    {
        ringProgress = 0f;
        flash = 1f;
        var local = PlayerManager.GetLocalPlayerAgent();
        if (local == null || !local.Alive) return;
        if (Vector3.Distance(local.Position, transform.position) > VictoryBanner.Radius.Value) return;

        try
        {
            var damage = local.Damage;
            if (damage == null) return;
            // The game's own heal (synced), a share of max health.
            damage.AddHealth(damage.HealthMax * VictoryBanner.HealPerPulse.Value / 100f, local.Cast<Agent>());
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons Victory Banner couldn't heal: {e.Message}");
        }
    }

    [HideFromIl2Cpp]
    private void AnimateRing(float dt)
    {
        if (ringProgress < 0f)
        {
            ring.enabled = false;
            return;
        }

        ringProgress += dt / RingSeconds;
        if (ringProgress >= 1f)
        {
            ringProgress = -1f;
            ring.enabled = false;
            return;
        }

        var radius = VictoryBanner.Radius.Value * ringProgress;
        var fade = 1f - ringProgress;
        var center = transform.position;
        var up = transform.up;
        var right = Vector3.Cross(up, Vector3.forward).sqrMagnitude > 0.001f ? Vector3.Cross(up, Vector3.forward).normalized : Vector3.right;
        var forward = Vector3.Cross(right, up).normalized;

        ring.startWidth = ring.endWidth = 0.08f;
        ring.startColor = ring.endColor = new Color(BannerColor.r, BannerColor.g, BannerColor.b, fade);
        ring.enabled = true;
        for (var i = 0; i < ring.positionCount; i++)
        {
            var angle = i / (float)ring.positionCount * Mathf.PI * 2f;
            var point = center + up * 0.05f + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
            ring.SetPosition(i, point);
        }
    }

    // The aura ring stays at the healing radius, breathing, and flares on each heal; the flag waves.
    [HideFromIl2Cpp]
    private void AnimateAura(float dt)
    {
        flash = Mathf.Max(0f, flash - dt * 1.5f);
        var breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f);
        aura.startWidth = aura.endWidth = 0.1f + 0.06f * flash;
        aura.startColor = aura.endColor = new Color(BannerColor.r, BannerColor.g, BannerColor.b, 0.45f + 0.25f * breathe + 0.3f * flash);
        Circle(aura, VictoryBanner.Radius.Value, 0.04f);
        glow.Range = VictoryBanner.Radius.Value * 1.2f;
        glow.Intensity = 1.5f + 0.8f * breathe + 5f * flash;
        if (flag != null) flag.localRotation = Quaternion.Euler(0f, 12f * Mathf.Sin(Time.time * 3.1f), 4f * Mathf.Sin(Time.time * 5.3f));
    }

    // Lays a ground-level circle of `radius` around the banner.
    [HideFromIl2Cpp]
    private void Circle(LineRenderer line, float radius, float lift)
    {
        var center = transform.position;
        var up = transform.up;
        var right = Vector3.Cross(up, Vector3.forward).sqrMagnitude > 0.001f ? Vector3.Cross(up, Vector3.forward).normalized : Vector3.right;
        var forward = Vector3.Cross(right, up).normalized;
        for (var i = 0; i < line.positionCount; i++)
        {
            var angle = i / (float)line.positionCount * Mathf.PI * 2f;
            line.SetPosition(i, center + up * lift + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius);
        }
    }

    // A flag: a dark pole on a small base with a green banner flying from its top - primitives,
    // no new assets shipped (same style as the grenade's fallback). Returns the banner, which waves.
    [HideFromIl2Cpp]
    private static Transform BuildModel(Transform parent)
    {
        var dark = new Color(0.15f, 0.15f, 0.17f);
        Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 0.04f, 0f), new Vector3(0.3f, 0.04f, 0.3f), dark);   // base
        Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 1.05f, 0f), new Vector3(0.05f, 1.05f, 0.05f), dark); // 2.1 m pole
        Part(PrimitiveType.Sphere, parent, new Vector3(0f, 2.12f, 0f), Vector3.one * 0.09f, BannerColor);         // finial

        // The banner hangs off a pivot at the pole, so it swings from there.
        var pivot = new GameObject("Flag").transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = new Vector3(0f, 1.7f, 0f);
        Part(PrimitiveType.Cube, pivot, new Vector3(0.38f, 0f, 0f), new Vector3(0.72f, 0.5f, 0.02f), BannerColor);
        Part(PrimitiveType.Cube, pivot, new Vector3(0.38f, 0f, 0f), new Vector3(0.2f, 0.2f, 0.03f), Color.white);  // emblem
        return pivot;
    }

    private const string HeldModelName = "GTFO.AbsurdWeapons Held Flag";

    // The held Victory Banner: the launcher's renderers hidden and a third-size flag in their place,
    // on the item's own layer (the first-person item draws with its own camera).
    [HideFromIl2Cpp]
    internal static void HoldModel(GameObject item)
    {
        if (item.transform.Find(HeldModelName) != null) return;
        foreach (var renderer in item.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        var holder = new GameObject(HeldModelName).transform;
        holder.SetParent(item.transform, false);
        holder.localPosition = new Vector3(0f, -0.25f, 0.15f);
        holder.localScale = Vector3.one * 0.35f;
        BuildModel(holder);
        foreach (var child in holder.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = item.layer;
    }

    [HideFromIl2Cpp]
    private static void Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        var part = GameObject.CreatePrimitive(type);
        UnityEngine.Object.Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        var renderer = part.GetComponent<Renderer>();
        if (renderer != null) renderer.material = new Material(Shader.Find("Unlit/Color")) { color = color };
    }

    private static Material? ringMaterial;

    [HideFromIl2Cpp]
    private static Material RingMaterial() => ringMaterial ??= new Material(Shader.Find("Sprites/Default")) { color = BannerColor };
}

/// <summary>Always-on: feeds <see cref="VictoryBanner.UpdateLocalRange"/> every frame, independent of
/// whether any banner currently exists, so the buff clears even after the last banner is gone.</summary>
internal sealed class VictoryBannerBuff : MonoBehaviour
{
    public VictoryBannerBuff(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        var local = PlayerManager.GetLocalPlayerAgent();
        if (local != null) VictoryBanner.UpdateLocalRange(local);
        VictoryBanner.ProcessPendingKills();
    }
}
