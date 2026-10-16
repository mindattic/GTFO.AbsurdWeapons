using System;
using GameData;
using HarmonyLib;
using Localization;
using UnityEngine;
using static GTFO.AbsurdWeapons.DataBlocks;

namespace GTFO.AbsurdWeapons;

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
/// <item>Tentacle Pistol (main) - Bataldo 3RB; a slower stream of striker tentacles.</item>
/// <item>Tentacle Rifle (special) - Buckland XDist2 (iron sights); every round is a striker tentacle.</item>
/// <item>Railgun (special) - Koning PR 11; a hitscan slug with infinite penetration (<see cref="Railgun"/>).</item>
/// <item>Chain Lightning Gun (special) - Omneco exp1; lightning that jumps enemy to enemy (<see cref="ChainLightning"/>).</item>
/// <item>Grenade Launcher (tool) - the C-Foam launcher firing bouncing lock-melter grenades (<see cref="GrenadeLauncher"/>).</item>
/// </list>
/// </summary>
[HarmonyPatch(typeof(GameDataInit), nameof(GameDataInit.Initialize))]
internal static class GunInjector
{
    // Vanilla archetypes run to the low hundreds at most; melee weapons here use 1001+.
    internal const uint PlasmaPistolId = 1101;
    internal const uint HivePistolId = 1102;
    internal const uint TentacleRifleId = 1103; // was the Tentacle Cannon
    internal const uint HiveRifleId = 1104;
    internal const uint PlasmaRifleId = 1105;
    internal const uint LaserPistolId = 1106;
    internal const uint LaserRifleId = 1107;
    internal const uint RailgunId = 1108;
    internal const uint ChainLightningId = 1109;
    internal const uint TentaclePistolId = 1110;
    internal const uint FlamethrowerId = 1111;
    internal const uint FlakCannonId = 1112;
    internal const uint GrenadeLauncherId = 1201; // a tool: uses the tool ammo, so not "ours" for infinite ammo

    // Every gun this mod adds (infinite ammo applies to all of them).
    internal static bool IsOurs(uint id) => GunArchetypes.Contains(id);

    // The gun archetypes this mod registered (melee and the tool have none of these).
    private static readonly System.Collections.Generic.HashSet<uint> GunArchetypes = new();

    // After GTFO.EZ (if installed) has tuned the vanilla weapons, so ours start from its tuned
    // values and EZ - which keys its tuning on vanilla names and ids - never touches ours.
    [HarmonyPostfix]
    [HarmonyAfter(Plugin.GtfoEzGuid)]
    private static void Postfix()
    {
        // Each pistol/rifle pair shares one set of stats (built from the same source archetype), so
        // the pistol and the rifle behave identically; the model only decides the look and hold.
        const string plasma = "Rapid-fire blue plasma, straight out of Doom: no recoil, no homing, just a stream of energy that shocks whatever it hits.";
        TryAdd("Plasma Pistol", PlasmaPistolId, (name, id) => AddPlasma(name, id, "Autopistol staggering", plasma)); // Raptus Steigro, main slot
        TryAdd("Plasma Rifle", PlasmaRifleId, (name, id) => AddPlasma(name, id, "HELRifleSemi", plasma));           // Omneco LRG, special slot
        const string hive = "A smart gun: locks onto the enemies around your crosshair and curves a fast stream of rounds onto them.";
        TryAdd("Hive Pistol", HivePistolId, (name, id) => AddHive(name, id, "PistolSemi", hive));    // Shelling S49, main slot
        TryAdd("Hive Rifle", HiveRifleId, (name, id) => AddHive(name, id, "RifleHeavyAuto", hive)); // Malatack HXC, special slot
        const string tentacle = "Fires the strikers' own tentacles, full-auto: every round is a tentacle that lashes out at an enemy near your crosshair and bites hard.";
        TryAdd("Tentacle Pistol", TentaclePistolId, (name, id) => AddTentacle(name, id, "RevolverHEL", null, tentacle)); // Bataldo 3RB, main slot
        // Buckland XDist2: the biggest special-slot gun with no optic, held out low (AddFarLowHold) so the tentacles have room.
        TryAdd("Tentacle Rifle", TentacleRifleId, (name, id) => AddTentacle(name, id, "ShotgunRanged", AddFarLowHold(name, id), tentacle));
        const string laser = "A continuous laser beam, like a trip mine's: it burns whatever it's held on, out to 70 metres, for as long as you hold it there.";
        TryAdd("Laser Pistol", LaserPistolId, (name, id) => AddLaser(name, id,
            "Pistolauto", // Raptus Treffen 2: full-auto pistol, no sight, main slot
            48, // held low (ItemFPSSettings "Pistol_Chunky", 10 cm down) so the beam is visible
            laser));
        TryAdd("Laser Rifle", LaserRifleId, (name, id) => AddLaser(name, id, "Precision_Rifle", null, laser)); // Drekker DEL P1, special slot
        TryAdd("Railgun", RailgunId, AddRailgun);
        TryAdd("Chain Lightning Gun", ChainLightningId, AddChainLightning);
        TryAdd("Flamethrower", FlamethrowerId, AddFlamethrower);
        TryAdd("Flak Cannon", FlakCannonId, AddFlakCannon);
        TryAdd("Grenade Launcher", GrenadeLauncherId, AddGrenadeLauncher);
    }

    // The Bataldo J 300 shotgun: primary fire scatters bouncing shrapnel shards (FlakCannon); hold
    // Aim to compress the shot into one ball that explodes into a shrapnel burst on contact.
    private static void AddFlakCannon(string name, uint id)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>("ShotgunHEL");
        var (category, archetype) = ModelSource(gear);
        archetype.FireMode = eWeaponFireMode.Semi;
        archetype.ShotgunBulletCount = 9;
        archetype.ShotgunConeSize = 5;
        archetype.ShotgunBulletSpread = 5;
        archetype.HipFireSpread = 0f;
        archetype.AimSpread = 0f;
        archetype.Damage = 7f;        // per shard, before FlakCannon's own range falloff
        archetype.ShotDelay = 0.85f;
        archetype.DefaultClipSize = 6;
        archetype.ShellCasingSize = 0f; // shrapnel, not shot: no brass ejected
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        Register(name, id, archetype, category, gear,
            "A classic flak cannon: a cone of bouncing shrapnel shards, devastating point-blank. Hold Aim to fire a single compressed ball instead - it explodes into a radial burst of shrapnel the instant it touches anything.");
    }

    // The TechMan Veruta XII machine gun, full-auto, firing FlameThrower's cone instead of bullets:
    // short range, falls off hard with distance, every tick that lands stacks afterburn. Hold Aim
    // to lob a fuel bomb instead (FlameThrower.FireBomb).
    private static void AddFlamethrower(string name, uint id)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>("MachinegunAuto");
        var (category, archetype) = ModelSource(gear);
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = 6f;        // per tick, before FlameThrower's own range falloff
        archetype.ShotDelay = 1f / 15f; // 15 ticks a second
        archetype.DefaultClipSize = 150;
        archetype.HipFireSpread = 0f;
        archetype.AimSpread = 0f;
        archetype.ShellCasingSize = 0f; // fire, not bullets: no brass ejected
        archetype.SpecialChargetupTime = 0f;
        archetype.SpecialCooldownTime = 0f;
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        Register(name, id, archetype, category, gear,
            "A short, brutal cone of fire: devastating up close, nearly useless across a room. Anything it touches keeps burning, and fire catches between enemies standing close together. Hold Aim to lob a fuel bomb instead, for reach.");
    }

    // A tool: the C-Foam launcher's category and gear (Stalwart Flow G2) cloned under a new id. Tools
    // have no archetype - the launcher's own prefab drives its pressure and bursts - so only those
    // two blocks are added. What it fires is GrenadeLauncher's job.
    private static void AddGrenadeLauncher(string name, uint id)
    {
        var sourceCategory = Lookup<GearCategoryDataBlock>("GlueGun");
        var sourceGear = Lookup<PlayerOfflineGearDataBlock>("Glue_distance");
        var gearJson = RewriteGearJson(sourceGear.GearJSON, sourceCategory.persistentID, id, DisplayName(name));
        if (gearJson == null)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons: couldn't find category {sourceCategory.persistentID} in '{sourceGear.name}' gear JSON - not adding the {name}.");
            return;
        }

        var blockName = name.Replace(" ", "");
        var category = Clone(sourceCategory);
        category.persistentID = id;
        category.name = blockName;
        category.PublicName = new LocalizedText { Id = 0, UntranslatedText = DisplayName(name) };
        category.Description = new LocalizedText { Id = 0, UntranslatedText =
            "Lobs lock melters rigged to blow: one that hits an enemy goes off at once; one that misses bounces around, flashing red faster and faster, and blows after 3 seconds." };

        var gear = Clone(sourceGear);
        gear.persistentID = id;
        gear.name = blockName;
        gear.GearJSON = gearJson;

        GameDataBlockBase<GearCategoryDataBlock>.AddBlock(category, -1);
        AddedCategories.Add(id);
        GameDataBlockBase<PlayerOfflineGearDataBlock>.AddBlock(gear, -1);
        Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons added the {name} (tool; category and gear id {id}).");
    }

    private static void TryAdd(string name, uint id, Action<string, uint> add)
    {
        try
        {
            // Already added by this mod (the game initialized its data again): nothing to do.
            if (AddedCategories.Contains(id) && GameDataBlockBase<GearCategoryDataBlock>.HasBlock(id)) return;
            if (GameDataBlockBase<ArchetypeDataBlock>.HasBlock(id)
                || GameDataBlockBase<GearCategoryDataBlock>.HasBlock(id)
                || GameDataBlockBase<PlayerOfflineGearDataBlock>.HasBlock(id))
            {
                Plugin.Logger.LogError($"GTFO.AbsurdWeapons: id {id} is already taken by another mod's datablock - not adding the {name}.");
                return;
            }

            add(name, id);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons failed to add the {name}: {e}");
        }
    }

    // A Doom plasma gun on the given model (gear block name): forced full-auto, no recoil. Damage is
    // rolled per ball (SeekerKind.Plasma, 5 x 1d8); 22.5 is that roll's average, for the loadout
    // screen. Stats come from the Omneco LRG for both plasma guns.
    private static void AddPlasma(string name, uint id, string model, string description)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>(model);
        var archetype = StatsFrom("HELRifleSemi");
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = 22.5f;
        archetype.ShotDelay = 2f / 35f;
        archetype.DefaultClipSize = 100;
        archetype.ShellCasingSize = 0f; // plasma, not bullets: no brass ejected
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        Register(name, id, archetype, CategoryOf(gear), gear, description);
    }

    // A Titanfall-style smart gun on the given model: forced full-auto, no recoil; each round
    // curves onto an enemy locked near the crosshair (SeekerKind.Hive). 1 damage a round: they
    // never miss, so it takes a stream of them to kill anything. Stats come from the Malatack HXC.
    private static void AddHive(string name, uint id, string model, string description)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>(model);
        var archetype = StatsFrom("RifleHeavyAuto");
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = 1f;
        archetype.ShotDelay = 0.09f;
        archetype.DefaultClipSize = 30;
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        Register(name, id, archetype, CategoryOf(gear), gear, description);
    }

    // A laser beam gun (LaserGuns) on the given model: forced full-auto at 20 ticks a second - every
    // tick burns 2 into whatever the beam is on - with no spread, no recoil and infinite ammo.
    // Stats come from the Drekker DEL P1. `hold`, if given, replaces the model's first-person hold.
    private static void AddLaser(string name, uint id, string model, uint? hold, string description)
    {
        var gear = WithHold(Lookup<PlayerOfflineGearDataBlock>(model), hold);
        var archetype = StatsFrom("Precision_Rifle");
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = 2f;
        archetype.ShotDelay = 0.05f;
        archetype.DefaultClipSize = 100;
        archetype.HipFireSpread = 0f;
        archetype.AimSpread = 0f;
        archetype.ShellCasingSize = 0f; // a beam, not bullets: no brass ejected
        archetype.SpecialChargetupTime = 0f;
        archetype.SpecialCooldownTime = 0f;
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        Register(name, id, archetype, CategoryOf(gear), gear, description);
    }

    // The Koning PR 11 sniper, semi-auto: every shot is one Railgun slug through everything in its
    // line. It keeps the sniper's own kick and scope.
    private static void AddRailgun(string name, uint id)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>("SniperSemi");
        var (category, archetype) = ModelSource(gear);
        archetype.FireMode = eWeaponFireMode.Semi;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = 60f;          // to every enemy on the line (sniper 40)
        archetype.ShotDelay = 1.0f;      // sniper 0.8
        archetype.DefaultClipSize = 5;
        archetype.HipFireSpread = 0f;
        archetype.AimSpread = 0f;
        archetype.ShellCasingSize = 0f;  // a slug thrown by magnets: no brass
        Register(name, id, archetype, category, gear,
            "A hitscan slug with infinite penetration: it goes through every enemy and every wall in its path.");
    }

    // The Omneco exp1 HEL machine gun, full-auto at ~6.7 bolts a second, no recoil, no spread, no
    // brass, infinite ammo. Each bolt does 10 to its first enemy, then 75% of the last on each of
    // up to five jumps (ChainLightning).
    private static void AddChainLightning(string name, uint id)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>("HELGun");
        var (category, archetype) = ModelSource(gear);
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = 10f;
        archetype.ShotDelay = 0.15f;
        archetype.DefaultClipSize = 60;
        archetype.HipFireSpread = 0f;
        archetype.AimSpread = 0f;
        archetype.ShellCasingSize = 0f;
        archetype.SpecialChargetupTime = 0f;
        archetype.SpecialCooldownTime = 0f;
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        Register(name, id, archetype, category, gear,
            "Every shot is a bolt of lightning that leaps from enemy to enemy, up to five jumps, weaker with each.");
    }

    // A tentacle gun on the given model, forced full-auto: a tentacle (TentacleCannon) for every
    // round while the trigger is held - one "pellet" per shot, no cone. (Shotgun pellets carry no
    // owner in the hit data; SeekerGuns falls back to the gun's owner.) Stats come from the
    // Buckland XDist2. `hold`, if given, replaces the model's first-person hold.
    private static void AddTentacle(string name, uint id, string model, uint? hold, string description)
    {
        var gear = WithHold(Lookup<PlayerOfflineGearDataBlock>(model), hold);
        var archetype = StatsFrom("ShotgunRanged");
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.ShotgunConeSize = 0;
        archetype.ShotgunBulletSpread = 0;
        archetype.HipFireSpread = 0f; // the tentacles bring their own spread
        archetype.AimSpread = 0f;
        archetype.Damage = 11f;       // per tentacle
        archetype.ShotDelay = 0.235f; // ~4.3 tentacles a second, so each has time to live
        archetype.ShellCasingSize = 0f; // tentacles, not bullets: no brass ejected
        archetype.DefaultClipSize = 60;
        archetype.SpecialChargetupTime = 0f;
        archetype.SpecialCooldownTime = 0f;
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        Register(name, id, archetype, CategoryOf(gear), gear, description);
    }

    private static PlayerOfflineGearDataBlock WithHold(PlayerOfflineGearDataBlock gear, uint? hold)
    {
        if (!hold.HasValue) return gear;
        var held = Clone(gear);
        held.GearJSON = SetGearComponent(gear.GearJSON, 4, hold.Value);
        return held;
    }

    // The Tentacle Rifle's first-person hold (ItemFPSSettingsDataBlock): the XDist2's own
    // "Shotgun" pose - so the hands and stock sit where a shotgun's do - pushed a little lower and
    // further out, tipped slightly up, with a lighter aim zoom so aiming doesn't pull it into your
    // face. Returns the id to use.
    private static uint AddFarLowHold(string name, uint id)
    {
        if (GameDataBlockBase<ItemFPSSettingsDataBlock>.HasBlock(id))
        {
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons: first-person hold id {id} is taken by another mod - the {name} keeps its model's hold.");
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
    private static uint AddKicklessRecoil(string name, uint id, uint modelRecoil)
    {
        if (GameDataBlockBase<RecoilDataBlock>.HasBlock(id))
        {
            Plugin.Logger.LogWarning($"GTFO.AbsurdWeapons: recoil id {id} is taken by another mod - the {name} keeps its model's recoil.");
            return modelRecoil;
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

    private static (GearCategoryDataBlock Category, ArchetypeDataBlock Archetype) ModelSource(PlayerOfflineGearDataBlock gear)
    {
        var category = CategoryOf(gear);
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

    // A clone of the archetype the named gear fires with: a pair's shared stat source.
    private static ArchetypeDataBlock StatsFrom(string gearName) => ModelSource(Lookup<PlayerOfflineGearDataBlock>(gearName)).Archetype;

    private static GearCategoryDataBlock CategoryOf(PlayerOfflineGearDataBlock gear)
    {
        var categoryId = CategoryIdOf(gear) ?? throw new InvalidOperationException($"no category in '{gear.name}' gear JSON");
        return GameDataBlockBase<GearCategoryDataBlock>.GetBlock(categoryId)
            ?? throw new InvalidOperationException($"gear category {categoryId} not found");
    }

    private static void Register(string name, uint id, ArchetypeDataBlock archetype, GearCategoryDataBlock sourceCategory,
        PlayerOfflineGearDataBlock modelGear, string description)
    {
        var gearJson = RewriteGearJson(modelGear.GearJSON, sourceCategory.persistentID, id, DisplayName(name));
        if (gearJson == null)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons: couldn't find category {sourceCategory.persistentID} in '{modelGear.name}' gear JSON - not adding the {name}.");
            return;
        }

        // Block names are lookup keys; the display name (which may have spaces) goes in PublicName.
        var blockName = name.Replace(" ", "");
        archetype.persistentID = id;
        archetype.name = blockName;
        archetype.PublicName = new LocalizedText { Id = 0, UntranslatedText = DisplayName(name) };

        var category = Clone(sourceCategory);
        category.persistentID = id;
        category.name = blockName;
        // Whichever fire mode the model's gear asks for, it gets this archetype.
        category.SemiArchetype = id;
        category.BurstArchetype = id;
        category.AutoArchetype = id;
        category.SemiBurstArchetype = id;
        category.PublicName = new LocalizedText { Id = 0, UntranslatedText = DisplayName(name) };
        category.Description = new LocalizedText { Id = 0, UntranslatedText = description };

        var gear = Clone(modelGear);
        gear.persistentID = id;
        gear.name = blockName;
        gear.GearJSON = gearJson;

        GameDataBlockBase<ArchetypeDataBlock>.AddBlock(archetype, -1);
        GameDataBlockBase<GearCategoryDataBlock>.AddBlock(category, -1);
        AddedCategories.Add(id);
        GunArchetypes.Add(id);
        GameDataBlockBase<PlayerOfflineGearDataBlock>.AddBlock(gear, -1);

        Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons added the {name} (archetype, category and gear id {id}).");
    }
}
