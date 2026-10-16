using System;
using GameData;
using HarmonyLib;
using Localization;
using UnityEngine;
using static GTFO.Weapons.DataBlocks;

namespace GTFO.Weapons;

/// <summary>
/// Adds this mod's guns. Each is a cloned archetype (stats), gear category (every fire mode
/// pointed at that archetype) and offline-gear entry (the model, which also decides the slot).
/// What they shoot is <see cref="SeekerGuns"/>' job; the datablocks only set ammo, rate of fire
/// and pellet count/spread.
/// <list type="bullet">
/// <item>Plasma Pistol (main) - Raptus Steigro, Doom-style blue plasma balls.</item>
/// <item>Plasma Rifle (special) - Omneco LRG, the same plasma, faster.</item>
/// <item>Hive Pistol (main) - Shelling S49, a Titanfall-style smart pistol.</item>
/// <item>Hive Rifle (special) - Malatack HXC, a smart rifle.</item>
/// <item>Laser Pistol (main) - Raptus Treffen 2, a continuous trip-mine laser beam.</item>
/// <item>Laser Rifle (special) - Drekker DEL P1, a heavier, longer beam.</item>
/// <item>Tentacle Cannon (special) - Buckland XDist2 (iron sights); every round is a striker tentacle.</item>
/// </list>
/// </summary>
[HarmonyPatch(typeof(GameDataInit), nameof(GameDataInit.Initialize))]
internal static class GunInjector
{
    // Vanilla archetypes run to the low hundreds at most; melee weapons here use 1001+.
    internal const uint PlasmaPistolId = 1101;
    internal const uint HivePistolId = 1102;
    internal const uint TentacleCannonId = 1103;
    internal const uint HiveRifleId = 1104;
    internal const uint PlasmaRifleId = 1105;
    internal const uint LaserPistolId = 1106;
    internal const uint LaserRifleId = 1107;

    // Every gun this mod adds (infinite ammo applies to all of them).
    internal static bool IsOurs(uint id) => id is >= PlasmaPistolId and <= LaserRifleId;

    // After GTFO.EZ (if installed) has tuned the vanilla weapons, so ours start from its tuned
    // values and EZ - which keys its tuning on vanilla names and ids - never touches ours.
    [HarmonyPostfix]
    [HarmonyAfter(Plugin.GtfoEzGuid)]
    private static void Postfix()
    {
        TryAdd("Plasma Pistol", PlasmaPistolId, (name, id) => AddPlasma(name, id,
            "Autopistol staggering", 3f / 35f, // Raptus Steigro: the high-tech HEL pistol, main slot
            "Rapid-fire blue plasma, straight out of Doom: no recoil, no homing, just a stream of energy that shocks whatever it hits."));
        TryAdd("Plasma Rifle", PlasmaRifleId, (name, id) => AddPlasma(name, id,
            "HELRifleSemi", 2f / 35f, // Omneco LRG: the sci-fi gauss rifle, special slot
            "Doom's plasma at rifle pace: an even faster stream of blue plasma, no recoil, shocking everything it hits."));
        TryAdd("Hive Pistol", HivePistolId, (name, id) => AddHive(name, id,
            "PistolSemi", 1f, 0.12f, // Shelling S49: the plain pistol, main slot
            "A smart pistol: locks onto the enemies around your crosshair, and every round curves out of the barrel onto one of them."));
        TryAdd("Hive Rifle", HiveRifleId, (name, id) => AddHive(name, id,
            "RifleHeavyAuto", 1f, 0.09f, // Malatack HXC: the heavy auto rifle, special slot
            "A smart rifle: locks onto the enemies around your crosshair and curves a fast stream of rounds onto them."));
        TryAdd("Tentacle Cannon", TentacleCannonId, AddTentacleCannon);
        TryAdd("Laser Pistol", LaserPistolId, (name, id) => AddLaser(name, id,
            "Pistolauto", 1.2f, // Raptus Treffen 2: full-auto pistol, no sight, main slot
            48, // held low (ItemFPSSettings "Pistol_Chunky", 10 cm down) so the beam is visible
            "A continuous laser beam, like a trip mine's: it burns whatever it's held on, for as long as you hold it there."));
        TryAdd("Laser Rifle", LaserRifleId, (name, id) => AddLaser(name, id,
            "Precision_Rifle", 2f, // Drekker DEL P1: precision rifle, special slot
            null,
            "A heavier, longer laser beam: it burns whatever it's held on, out to 70 metres."));
    }

    private static void TryAdd(string name, uint id, Action<string, uint> add)
    {
        try
        {
            if (GameDataBlockBase<ArchetypeDataBlock>.HasBlock(id)
                || GameDataBlockBase<GearCategoryDataBlock>.HasBlock(id)
                || GameDataBlockBase<PlayerOfflineGearDataBlock>.HasBlock(id))
            {
                Plugin.Logger.LogError($"GTFO.Weapons: id {id} is already taken by another mod's datablock - not adding the {name}.");
                return;
            }

            add(name, id);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.Weapons failed to add the {name}: {e}");
        }
    }

    // A Doom plasma gun on the given model (gear block name): forced full-auto at `shotDelay`, no
    // recoil. Damage is rolled per ball (SeekerKind.Plasma, 5 x 1d8); 22.5 is that roll's average,
    // for the loadout screen. Infinite ammo is SeekerGuns' clip refill.
    private static void AddPlasma(string name, uint id, string model, float shotDelay, string description)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>(model);
        var (category, archetype) = ModelSource(gear);
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = 22.5f;
        archetype.ShotDelay = shotDelay;
        archetype.DefaultClipSize = 100;
        archetype.ShellCasingSize = 0f; // plasma, not bullets: no brass ejected
        archetype.RecoilDataID = AddKicklessRecoil(name, id);
        Register(name, id, archetype, category, gear, description);
    }

    // A Titanfall-style smart gun on the given model: forced full-auto, no recoil; each round
    // curves onto an enemy locked near the crosshair (SeekerKind.Hive). Both Hive guns do 1 damage
    // a round: they never miss, so it takes a stream of them to kill anything.
    private static void AddHive(string name, uint id, string model, float damage, float shotDelay, string description)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>(model);
        var (category, archetype) = ModelSource(gear);
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = damage;
        archetype.ShotDelay = shotDelay;
        archetype.DefaultClipSize = 30;
        archetype.RecoilDataID = AddKicklessRecoil(name, id);
        Register(name, id, archetype, category, gear, description);
    }

    // A laser beam gun (LaserGuns) on the given model: forced full-auto at 20 ticks a second - every
    // tick burns `damagePerTick` into whatever the beam is on - with no spread, no recoil and
    // infinite ammo.
    // `hold`, if given, replaces the model's first-person hold (ItemFPSSettings).
    private static void AddLaser(string name, uint id, string model, float damagePerTick, uint? hold, string description)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>(model);
        if (hold.HasValue)
        {
            var held = Clone(gear);
            held.GearJSON = SetGearComponent(gear.GearJSON, 4, hold.Value);
            gear = held;
        }
        var (category, archetype) = ModelSource(gear);
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = damagePerTick;
        archetype.ShotDelay = 0.05f;
        archetype.DefaultClipSize = 100;
        archetype.HipFireSpread = 0f;
        archetype.AimSpread = 0f;
        archetype.ShellCasingSize = 0f; // a beam, not bullets: no brass ejected
        archetype.SpecialChargetupTime = 0f;
        archetype.SpecialCooldownTime = 0f;
        archetype.RecoilDataID = AddKicklessRecoil(name, id);
        Register(name, id, archetype, category, gear, description);
    }

    private static void AddTentacleCannon(string name, uint id)
    {
        // Buckland XDist2: the biggest special-slot gun with no optic or holosight - a long-range
        // shotgun with plain iron sights. Full-auto: a tentacle (TentacleCannon) for every round
        // while the trigger is held - one "pellet" per shot, no cone. (Its pellets carry no owner
        // in the hit data; SeekerGuns falls back to the gun's owner.)
        var model = Lookup<PlayerOfflineGearDataBlock>("ShotgunRanged");
        var (category, archetype) = ModelSource(model);
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.ShotgunConeSize = 0;
        archetype.ShotgunBulletSpread = 0;
        archetype.HipFireSpread = 0f; // the machine gun's own spread off: the tentacles bring their own
        archetype.AimSpread = 0f;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = 10f;       // per tentacle
        archetype.ShotDelay = 0.235f; // ~4.3 tentacles a second, so each has time to live
        archetype.ShellCasingSize = 0f; // tentacles, not bullets: no brass ejected
        archetype.DefaultClipSize = 60;
        archetype.SpecialChargetupTime = 0f;
        archetype.SpecialCooldownTime = 0f;
        archetype.RecoilDataID = AddKicklessRecoil(name, id);

        // Its own first-person hold, held out away from the face and low, so the tentacles have
        // room: see AddFarLowHold.
        var held = Clone(model);
        held.GearJSON = SetGearComponent(model.GearJSON, 4, AddFarLowHold(name, id));

        Register(name, id, archetype, category, held,
            "Fires the strikers' own tentacles, full-auto: every round is a tentacle that lashes out at an enemy near your crosshair and bites hard.");
    }

    // The Tentacle Cannon's first-person hold (ItemFPSSettingsDataBlock): the XDist2's own
    // "Shotgun" pose - so the hands and stock sit where a shotgun's do - pushed a little lower and
    // further out, tipped slightly up, with a lighter aim zoom so aiming doesn't pull it into your
    // face. Returns the id to use.
    private static uint AddFarLowHold(string name, uint id)
    {
        if (GameDataBlockBase<ItemFPSSettingsDataBlock>.HasBlock(id))
        {
            Plugin.Logger.LogWarning($"GTFO.Weapons: first-person hold id {id} is taken by another mod - the {name} keeps its model's hold.");
            return Lookup<ItemFPSSettingsDataBlock>("Shotgun").persistentID;
        }

        var hold = Clone(Lookup<ItemFPSSettingsDataBlock>("Shotgun"));
        hold.persistentID = id;
        hold.name = name.Replace(" ", "") + "_Hold";
        hold.localPosHip = new Vector3(0.08f, -0.105f, 0.46f);  // shotgun (0.076, -0.085, 0.38): lower, further out
        hold.localPosZoom = new Vector3(0f, -0.175f, 0.45f);    // shotgun (0, -0.154, 0.35)
        hold.localRotHip = new Vector3(-3f, 0f, 0f);            // tipped slightly up
        hold.localRotZoom = new Vector3(-3f, 0f, 0f);
        hold.ItemCameraFOVZoom = 30;                            // shotgun 23: a lighter aim zoom
        GameDataBlockBase<ItemFPSSettingsDataBlock>.AddBlock(hold, -1);
        return id;
    }

    // No recoil at all: the auto pistol's profile (for its crosshair) with every kick, view-push
    // and shake weight zeroed. (The Arid 5's own, High_Cal_Pistol, is one of the game's heaviest -
    // a big rotational kick, double camera rotation and a hard screen shake every shot.)
    // Registered at the gun's id; returns the id to use.
    private static uint AddKicklessRecoil(string name, uint id)
    {
        if (GameDataBlockBase<RecoilDataBlock>.HasBlock(id))
        {
            Plugin.Logger.LogWarning($"GTFO.Weapons: recoil id {id} is taken by another mod - the {name} keeps its model's recoil.");
            return Lookup<RecoilDataBlock>("High_Cal_Pistol").persistentID;
        }

        var recoil = Clone(Lookup<RecoilDataBlock>("PistolAuto"));
        recoil.persistentID = id;
        recoil.name = name.Replace(" ", "") + "_NoKick";
        // MinMaxValue is a shared reference type: fresh instances, so the auto pistol keeps its own.
        recoil.power = new MinMaxValue { Min = 0f, Max = 0f };
        recoil.horizontalScale = new MinMaxValue { Min = 0f, Max = 0f };
        recoil.verticalScale = new MinMaxValue { Min = 0f, Max = 0f };
        recoil.recoilRotImpulseWeight = 0f;
        recoil.recoilPosImpulseWeight = 0f;
        recoil.recoilPosShiftWeight = 0f;
        recoil.recoilCameraRotWeight = 0f;
        recoil.recoilCameraPosWeight = 0f;
        recoil.recoilAimingWeight = 0f;
        recoil.hipFireCrosshairRecoilPop = 0f;
        recoil.concussionIntensity = 0f;
        recoil.concussionDuration = 0f;

        GameDataBlockBase<RecoilDataBlock>.AddBlock(recoil, -1);
        return id;
    }

    // The gear's own category and a clone of the archetype its own fire mode uses.
    private static (GearCategoryDataBlock Category, ArchetypeDataBlock Archetype) ModelSource(PlayerOfflineGearDataBlock gear)
    {
        var categoryId = CategoryIdOf(gear) ?? throw new InvalidOperationException($"no category in '{gear.name}' gear JSON");
        var category = GameDataBlockBase<GearCategoryDataBlock>.GetBlock(categoryId)
            ?? throw new InvalidOperationException($"gear category {categoryId} not found");
        var archetypeId = FireModeOf(gear) switch
        {
            eWeaponFireMode.Burst => category.BurstArchetype,
            eWeaponFireMode.Auto => category.AutoArchetype,
            eWeaponFireMode.SemiBurst => category.SemiBurstArchetype,
            _ => category.SemiArchetype,
        };
        var archetype = GameDataBlockBase<ArchetypeDataBlock>.GetBlock(archetypeId)
            ?? throw new InvalidOperationException($"archetype {archetypeId} not found");
        return (category, Clone(archetype));
    }

    private static void Register(string name, uint id, ArchetypeDataBlock archetype, GearCategoryDataBlock sourceCategory,
        PlayerOfflineGearDataBlock modelGear, string description)
    {
        var gearJson = RewriteGearJson(modelGear.GearJSON, sourceCategory.persistentID, id, name);
        if (gearJson == null)
        {
            Plugin.Logger.LogError($"GTFO.Weapons: couldn't find category {sourceCategory.persistentID} in '{modelGear.name}' gear JSON - not adding the {name}.");
            return;
        }

        // Block names are lookup keys; the display name (which may have spaces) goes in PublicName.
        var blockName = name.Replace(" ", "");
        archetype.persistentID = id;
        archetype.name = blockName;
        archetype.PublicName = new LocalizedText { Id = 0, UntranslatedText = name };

        var category = Clone(sourceCategory);
        category.persistentID = id;
        category.name = blockName;
        // Whichever fire mode the model's gear asks for, it gets this archetype.
        category.SemiArchetype = id;
        category.BurstArchetype = id;
        category.AutoArchetype = id;
        category.SemiBurstArchetype = id;
        category.PublicName = new LocalizedText { Id = 0, UntranslatedText = name };
        category.Description = new LocalizedText { Id = 0, UntranslatedText = description };

        var gear = Clone(modelGear);
        gear.persistentID = id;
        gear.name = blockName;
        gear.GearJSON = gearJson;

        GameDataBlockBase<ArchetypeDataBlock>.AddBlock(archetype, -1);
        GameDataBlockBase<GearCategoryDataBlock>.AddBlock(category, -1);
        GameDataBlockBase<PlayerOfflineGearDataBlock>.AddBlock(gear, -1);

        Plugin.Logger.LogInfo($"GTFO.Weapons added the {name} (archetype, category and gear id {id}).");
    }
}
