using System;
using GameData;
using HarmonyLib;
using Localization;
using static GTFO.Weapons.DataBlocks;

namespace GTFO.Weapons;

/// <summary>
/// Adds this mod's melee weapons as new selectable loadout entries. Each one is three new
/// datablocks registered right after the game loads its own: a melee archetype (stats + which
/// animation/sound set to use), a gear category pointing at it, and an offline-gear entry (what
/// appears in the loadout screen, and which model it carries). Nothing vanilla is modified.
/// <list type="bullet">
/// <item>Glaive - the IsoCo Stinger spear model swung overhead like the sledgehammer.</item>
/// <item>Katana - the Mastaba Fixed Blade, blade stretched to ~2.2 m overall, wielded like the bat with spear reach.</item>
/// <item>Mjolnir - the Santonian Mallet hammer model wielded exactly like the bat.</item>
/// <item>Warhammer - the Kovac Sledgehammer with a giant head: enormous hits and knockback.</item>
/// <item>Shadow Dagger - the Mastaba Fixed Blade as a knife; a hit makes you unseen for 6 s.</item>
/// </list>
/// </summary>
[HarmonyPatch(typeof(GameDataInit), nameof(GameDataInit.Initialize))]
internal static class MeleeInjector
{
    // Fixed rather than "max existing + 1" so every player in a lobby agrees on them - gear syncs
    // by category id. Vanilla uses 1-4 for melee archetypes and two-digit ids for the rest. Each
    // weapon uses the same id for all three of its blocks.
    internal const uint GlaiveId = 1001;
    internal const uint KatanaId = 1002;
    internal const uint MjolnirId = 1003;
    // 1004 was the Rapier (removed); left unused so old loadouts never pick up a different weapon.
    internal const uint WarhammerId = 1005;
    internal const uint ShadowDaggerId = 1006;

    // After GTFO.EZ (if installed) has tuned the vanilla weapons, so ours start from its tuned
    // values and EZ - which keys its tuning on vanilla names and ids - never touches ours.
    [HarmonyPostfix]
    [HarmonyAfter(Plugin.GtfoEzGuid)]
    private static void Postfix()
    {
        TryAdd("Glaive", GlaiveId, WeaponSettings.Glaive, AddGlaive);
        TryAdd("Katana", KatanaId, WeaponSettings.Katana, AddKatana);
        TryAdd("Mjolnir", MjolnirId, WeaponSettings.Mjolnir, AddMjolnir);
        TryAdd("Warhammer", WarhammerId, WeaponSettings.Warhammer, AddWarhammer);
        TryAdd("Shadow Dagger", ShadowDaggerId, WeaponSettings.ShadowDagger, AddShadowDagger);
    }

    private static void TryAdd(string name, uint id, WeaponSettings settings, Action<string, uint> add)
    {
        try
        {
            if (GameDataBlockBase<MeleeArchetypeDataBlock>.HasBlock(id)
                || GameDataBlockBase<GearCategoryDataBlock>.HasBlock(id)
                || GameDataBlockBase<PlayerOfflineGearDataBlock>.HasBlock(id))
            {
                Plugin.Logger.LogError($"GTFO.Weapons: id {id} is already taken by another mod's datablock - not adding the {name}.");
                return;
            }

            add(name, id);

            // Reach override from the config, applied to the archetype just registered.
            var archetype = GameDataBlockBase<MeleeArchetypeDataBlock>.GetBlock(id);
            if (archetype != null && settings.Reach.Value > 0f)
            {
                archetype.CameraDamageRayLength = settings.Reach.Value;
                Plugin.Logger.LogInfo($"GTFO.Weapons: {name} reach set to {settings.Reach.Value} from the config.");
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.Weapons failed to add the {name}: {e}");
        }
    }

    private static void AddGlaive(string name, uint id)
    {
        var spear = Lookup<MeleeArchetypeDataBlock>("Spear");
        var hammer = Lookup<MeleeArchetypeDataBlock>("Hammer");
        var spearCategory = Lookup<GearCategoryDataBlock>("MeleeSpear");
        var hammerCategory = Lookup<GearCategoryDataBlock>("MeleeSledgehammer");
        var stinger = Lookup<PlayerOfflineGearDataBlock>("Spear_02"); // IsoCo Stinger

        // Stats: the spear's reach and cleave, the hammer's swing, sounds and weight of blow.
        var archetype = Clone(spear);
        archetype.MeleeAnimationSet = hammer.MeleeAnimationSet;
        archetype.MeleeSFXSet = hammer.MeleeSFXSet;
        archetype.LightAttackDamage = hammer.LightAttackDamage;
        archetype.ChargedAttackDamage = hammer.ChargedAttackDamage;
        archetype.ChargedStaggerMulti = hammer.ChargedStaggerMulti;
        archetype.LightEnvironmentMulti = hammer.LightEnvironmentMulti;
        archetype.AttackSphereRadius = hammer.AttackSphereRadius;
        archetype.PlayerRunSpeedMultiWhileCharging = hammer.PlayerRunSpeedMultiWhileCharging;
        archetype.CameraDamageRayLength = spear.CameraDamageRayLength;
        archetype.CanHitMultipleEnemies = true;
        // The spear's hold-to-thrust charge has no matching motion in the hammer's animation set.
        archetype.EvaluateHoldBeforeAttack = false;

        var category = Clone(spearCategory);
        category.ThirdPersonFullbodyMovement = hammerCategory.ThirdPersonFullbodyMovement; // teammates see the overhead swing

        Register(name, id, archetype, category, spearCategory.persistentID, stinger,
            "A long razor-edged blade on a spear shaft, brought down overhead like a splitting maul. Reaches far and cleaves through a crowd.");
    }

    private static void AddKatana(string name, uint id)
    {
        var bat = Lookup<MeleeArchetypeDataBlock>("Bat");
        var batCategory = Lookup<GearCategoryDataBlock>("MeleeBat");
        var knifeCategory = Lookup<GearCategoryDataBlock>("MeleeKnife");
        // Mastaba Fixed Blade: its blade is a separate part, so BladeStretcher
        // lengthens just the blade and the grip stays knife-sized.
        var knife = Lookup<PlayerOfflineGearDataBlock>("Knife");

        // Everything but the model and reach is the bat's: stats, swing, sounds, third-person
        // movement. With the blade stretched 7.5x it's ~2.2 m, about a spear's length, so it gets the
        // spear's 3.2 m reach (the bat's is 1.75 m).
        var archetype = Clone(bat);
        archetype.CameraDamageRayLength = 3.2f;

        Register(name, id, archetype, Clone(batCategory), knifeCategory.persistentID, knife,
            "A long blade swung in wide two-handed arcs. Handles like a bat, cuts like a blade, and reaches further than either.");
    }

    private static void AddMjolnir(string name, uint id)
    {
        var bat = Lookup<MeleeArchetypeDataBlock>("Bat");
        var batCategory = Lookup<GearCategoryDataBlock>("MeleeBat");
        var hammerCategory = Lookup<GearCategoryDataBlock>("MeleeSledgehammer");
        var mallet = Lookup<PlayerOfflineGearDataBlock>("Mallet"); // Santonian Mallet - short-hafted, like the real thing

        // Everything but the model is the bat's: stats, swing, sounds, third-person movement.
        Register(name, id, Clone(bat), Clone(batCategory), hammerCategory.persistentID, mallet,
            "A short-hafted war hammer swung in fast, wide arcs. Handles like a bat, lands like thunder.");
    }

    private static void AddWarhammer(string name, uint id)
    {
        var hammer = Lookup<MeleeArchetypeDataBlock>("Hammer");
        var hammerCategory = Lookup<GearCategoryDataBlock>("MeleeSledgehammer");
        var sledgehammer = Lookup<PlayerOfflineGearDataBlock>("Sledgehammer"); // Kovac Sledgehammer; BladeStretcher gives it the giant head

        // The hammer's own swing and charge (full damage at 1.8 s). A fully charged blow kills
        // almost anything short of a tank; taps stay hammer-light.
        var archetype = Clone(hammer);
        archetype.ChargedAttackDamage = 500f; // vanilla hammer 20
        archetype.LightAttackDamage = 6f; // vanilla hammer 3
        // Stagger makes an enemy reel: x55 charged (vanilla 1.3), x11 light (vanilla 1). The actual
        // shove - sliding enemies back - is runtime (ImpactBursts.Shockwave / Knockback).
        archetype.ChargedStaggerMulti = 55f;
        archetype.LightStaggerMulti = 11f;
        archetype.ChargedEnvironmentMulti = 10f; // doors and locks too
        archetype.CanHitMultipleEnemies = true; // the head is big enough to catch a crowd
        archetype.AttackSphereRadius = 0.6f; // vanilla hammer 0.3
        archetype.CameraDamageRayLength = 2.4f; // vanilla hammer 1.8 - the giant head reaches further
        archetype.PlayerRunSpeedMultiWhileCharging = 0.5f; // lugging it at the ready slows you down

        Register(name, id, archetype, Clone(hammerCategory), hammerCategory.persistentID, sledgehammer,
            "A sledgehammer with a head the size of a crate. Every hit sends enemies reeling, and a full charge leaves nothing standing.");
    }

    private static void AddShadowDagger(string name, uint id)
    {
        var knife = Lookup<MeleeArchetypeDataBlock>("Knife");
        var knifeCategory = Lookup<GearCategoryDataBlock>("MeleeKnife");
        var mastaba = Lookup<PlayerOfflineGearDataBlock>("Knife"); // Mastaba Fixed Blade, normal size

        // Exactly the knife; what it does on a hit is runtime (Stealth).
        Register(name, id, Clone(knife), Clone(knifeCategory), knifeCategory.persistentID, mastaba,
            "Strike from the shadows. Every enemy it cuts makes you vanish for six seconds: no enemy can see, hear or chase you.");
    }

    // Gives the cloned archetype and category the weapon's id and name, builds its loadout entry
    // from the model's gear (re-pointed at the new category), and registers all three.
    private static void Register(string name, uint id, MeleeArchetypeDataBlock archetype, GearCategoryDataBlock category,
        uint modelCategoryId, PlayerOfflineGearDataBlock modelGear, string description)
    {
        var gearJson = RewriteGearJson(modelGear.GearJSON, modelCategoryId, id, name);
        if (gearJson == null)
        {
            Plugin.Logger.LogError($"GTFO.Weapons: couldn't find category {modelCategoryId} in '{modelGear.name}' gear JSON - not adding the {name}.");
            return;
        }

        // Block names are lookup keys; the display name (which may have spaces) goes in PublicName.
        var blockName = name.Replace(" ", "");
        archetype.persistentID = id;
        archetype.name = blockName;
        archetype.PublicName = name;

        category.persistentID = id;
        category.name = "Melee" + blockName;
        category.MeleeArchetype = id;
        // Fresh instances: the cloned PublicName/Description still point at the source weapon's,
        // and editing those would rename it too.
        category.PublicName = new LocalizedText { Id = 0, UntranslatedText = name };
        category.Description = new LocalizedText { Id = 0, UntranslatedText = description };

        var gear = Clone(modelGear);
        gear.persistentID = id;
        gear.name = blockName;
        gear.GearJSON = gearJson;

        GameDataBlockBase<MeleeArchetypeDataBlock>.AddBlock(archetype, -1);
        GameDataBlockBase<GearCategoryDataBlock>.AddBlock(category, -1);
        GameDataBlockBase<PlayerOfflineGearDataBlock>.AddBlock(gear, -1);

        Plugin.Logger.LogInfo($"GTFO.Weapons added the {name} (archetype, category and gear id {id}).");
    }
}
