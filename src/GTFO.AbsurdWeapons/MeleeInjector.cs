using System;
using GameData;
using HarmonyLib;
using Localization;
using static GTFO.AbsurdWeapons.DataBlocks;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// Adds this mod's melee weapons as new selectable loadout entries. Each one is three new
/// datablocks registered right after the game loads its own: a melee archetype (stats + which
/// animation/sound set to use), a gear category pointing at it, and an offline-gear entry (what
/// appears in the loadout screen, and which model it carries). Nothing vanilla is modified.
/// <list type="bullet">
/// <item>Yari - the IsoCo Stinger spear model swung overhead like the sledgehammer.</item>
/// <item>Tanto - the Mastaba Fixed Blade at a real tanto's size (~25 cm blade): the fastest, shortest sword.</item>
/// <item>Wakizashi - the Mastaba Fixed Blade at a real wakizashi's size (~50 cm blade), wielded like the knife.</item>
/// <item>Katana - the Mastaba Fixed Blade at a real katana's size (~70 cm blade), wielded like the bat; full-charge dash.</item>
/// <item>Odachi - the Mastaba Fixed Blade at the Norimitsu Odachi's size (2.26 m blade), swung like the sledgehammer.</item>
/// <item>Mjolnir - the Santonian Mallet hammer model wielded exactly like the bat.</item>
/// <item>Warhammer - the Kovac Sledgehammer with a giant head: enormous hits and knockback.</item>
/// <item>Shadow Dagger - the Mastaba Fixed Blade as a knife; a fully charged hit makes you unseen for 6 s.</item>
/// <item>Gravity Hammer - the Omneco Maul; a charge held forever pulls enemies in, released it blasts them out.</item>
/// </list>
/// </summary>
[HarmonyPatch(typeof(GameDataInit), nameof(GameDataInit.Initialize))]
internal static class MeleeInjector
{
    // Fixed rather than "max existing + 1" so every player in a lobby agrees on them - gear syncs
    // by category id. Vanilla uses 1-4 for melee archetypes and two-digit ids for the rest. Each
    // weapon uses the same id for all three of its blocks.
    internal const uint YariId = 1001; // was the Glaive
    internal const uint KatanaId = 1002;
    internal const uint MjolnirId = 1003;
    // 1004 was the Rapier (removed); left unused so old loadouts never pick up a different weapon.
    internal const uint WarhammerId = 1005;
    internal const uint ShadowDaggerId = 1006;
    internal const uint WakizashiId = 1007;
    internal const uint OdachiId = 1008;
    internal const uint TantoId = 1009;
    internal const uint GravityHammerId = 1010; // also its own melee animation set's id

    // After GTFO.EZ (if installed) has tuned the vanilla weapons, so ours start from its tuned
    // values and EZ - which keys its tuning on vanilla names and ids - never touches ours.
    [HarmonyPostfix]
    [HarmonyAfter(Plugin.GtfoEzGuid)]
    private static void Postfix()
    {
        TryAdd("Yari", YariId, WeaponSettings.Yari, AddYari);
        TryAdd("Tanto", TantoId, WeaponSettings.Tanto, AddTanto);
        TryAdd("Wakizashi", WakizashiId, WeaponSettings.Wakizashi, AddWakizashi);
        TryAdd("Katana", KatanaId, WeaponSettings.Katana, AddKatana);
        TryAdd("Odachi", OdachiId, WeaponSettings.Odachi, AddOdachi);
        TryAdd("Mjolnir", MjolnirId, WeaponSettings.Mjolnir, AddMjolnir);
        TryAdd("Warhammer", WarhammerId, WeaponSettings.Warhammer, AddWarhammer);
        TryAdd("Shadow Dagger", ShadowDaggerId, WeaponSettings.ShadowDagger, AddShadowDagger);
        TryAdd("Gravity Hammer", GravityHammerId, WeaponSettings.GravityHammer, AddGravityHammer);
    }

    private static void TryAdd(string name, uint id, WeaponSettings settings, Action<string, uint> add)
    {
        try
        {
            // Already added by this mod (the game initialized its data again): nothing to do.
            if (AddedCategories.Contains(id) && GameDataBlockBase<GearCategoryDataBlock>.HasBlock(id)) return;
            if (GameDataBlockBase<MeleeArchetypeDataBlock>.HasBlock(id)
                || GameDataBlockBase<GearCategoryDataBlock>.HasBlock(id)
                || GameDataBlockBase<PlayerOfflineGearDataBlock>.HasBlock(id))
            {
                Plugin.Logger.LogError($"GTFO.AbsurdWeapons: id {id} is already taken by another mod's datablock - not adding the {name}.");
                return;
            }

            add(name, id);

            // Reach override from the config, applied to the archetype just registered.
            var archetype = GameDataBlockBase<MeleeArchetypeDataBlock>.GetBlock(id);
            if (archetype != null && settings.Reach.Value > 0f)
            {
                archetype.CameraDamageRayLength = settings.Reach.Value;
                Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons: {name} reach set to {settings.Reach.Value} from the config.");
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons failed to add the {name}: {e}");
        }
    }

    private static void AddYari(string name, uint id)
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
            "A Japanese spear's long blade, brought down overhead like a splitting maul. Reaches far and cleaves through a crowd.");
    }

    // The four Japanese swords are all the Mastaba Fixed Blade: its blade is a separate part, so
    // BladeStretcher sizes just the blade (see WeaponSettings) and the grip stays knife-sized.
    // They're a ladder - each step up is slower, hits harder and reaches further:
    //
    //            moves   light-attack speed   light / charged   reach   hits
    //   Tanto    knife   x1.6 (SwordTempo)    2 / 6             1.6 m   one
    //   Wakizashi knife  x1.25                3 / 9             2.0 m   one
    //   Katana   bat     x1                   4 / 14            2.3 m   all in the arc (+ dash)
    //   Odachi   hammer  x0.85                6 / 9999 (kills)  3.5 m   all in the arc

    // A real tanto: ~25 cm blade. The fastest and shortest.
    private static void AddTanto(string name, uint id)
    {
        var knife = Lookup<MeleeArchetypeDataBlock>("Knife");
        var knifeCategory = Lookup<GearCategoryDataBlock>("MeleeKnife");
        var mastaba = Lookup<PlayerOfflineGearDataBlock>("Knife");

        var archetype = Clone(knife);
        archetype.LightAttackDamage = 2f;
        archetype.ChargedAttackDamage = 6f;
        archetype.CameraDamageRayLength = 1.6f; // knife 1.75
        archetype.name = "Tanto";
        WithLightAttackTempo(archetype, id, SwordTempo.SpeedFor(id));

        Register(name, id, archetype, Clone(knifeCategory), knifeCategory.persistentID, mastaba,
            "A short Japanese blade: the fastest cuts of any sword, at arm's length.");
    }

    // A real wakizashi: ~50 cm blade, the samurai's short companion sword.
    private static void AddWakizashi(string name, uint id)
    {
        var knife = Lookup<MeleeArchetypeDataBlock>("Knife");
        var knifeCategory = Lookup<GearCategoryDataBlock>("MeleeKnife");
        var mastaba = Lookup<PlayerOfflineGearDataBlock>("Knife");

        var archetype = Clone(knife);
        archetype.LightAttackDamage = 3f;
        archetype.ChargedAttackDamage = 9f;
        archetype.CameraDamageRayLength = 2.0f;
        archetype.name = "Wakizashi";
        WithLightAttackTempo(archetype, id, SwordTempo.SpeedFor(id));

        Register(name, id, archetype, Clone(knifeCategory), knifeCategory.persistentID, mastaba,
            "The samurai's short companion sword: quick cuts with a little more reach and weight than a tanto.");
    }

    // A real katana: ~70 cm blade, ~1 m overall. The bat's wide swings, and it cuts through every
    // enemy in the arc. The full-charge dash is runtime (KatanaDash).
    private static void AddKatana(string name, uint id)
    {
        var bat = Lookup<MeleeArchetypeDataBlock>("Bat");
        var batCategory = Lookup<GearCategoryDataBlock>("MeleeBat");
        var knifeCategory = Lookup<GearCategoryDataBlock>("MeleeKnife");
        var mastaba = Lookup<PlayerOfflineGearDataBlock>("Knife");

        var archetype = Clone(bat);
        archetype.LightAttackDamage = 4f;       // bat 3
        archetype.ChargedAttackDamage = 14f;    // bat 12
        archetype.CameraDamageRayLength = 2.3f; // bat 1.75
        archetype.CanHitMultipleEnemies = true;

        Register(name, id, archetype, Clone(batCategory), knifeCategory.persistentID, mastaba,
            "A Japanese long sword swung in wide two-handed arcs, cutting through everything in its path. Release a full charge to dash forward, slashing all you pass.");
    }

    // The Norimitsu Odachi, the largest known: a 2.26 m blade. Brought down overhead with the
    // sledgehammer's swing, charge and weight, reaching 3.5 m and cleaving through a crowd.
    private static void AddOdachi(string name, uint id)
    {
        var hammer = Lookup<MeleeArchetypeDataBlock>("Hammer");
        var hammerCategory = Lookup<GearCategoryDataBlock>("MeleeSledgehammer");
        var knifeCategory = Lookup<GearCategoryDataBlock>("MeleeKnife");
        var mastaba = Lookup<PlayerOfflineGearDataBlock>("Knife");

        var archetype = Clone(hammer);
        archetype.LightAttackDamage = 6f;       // hammer 3
        archetype.ChargedAttackDamage = 9999f;  // a full charge kills anything it reaches (hammer 20)
        archetype.CanHitMultipleEnemies = true;
        archetype.AttackSphereRadius = 0.4f;    // hammer 0.3
        archetype.CameraDamageRayLength = 3.5f; // hammer 1.8
        archetype.PlayerRunSpeedMultiWhileCharging = 0.7f;
        archetype.name = "Odachi";
        WithLightAttackTempo(archetype, id, SwordTempo.SpeedFor(id));

        Register(name, id, archetype, Clone(hammerCategory), knifeCategory.persistentID, mastaba,
            "A colossal Japanese field sword, taller than two men. Slow, heavy overhead cuts that reach far and cleave everything beneath them.");
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

        // The knife, with stabs 2.5x as fast; what it does on a hit is runtime (Stealth).
        var archetype = Clone(knife);
        archetype.name = "ShadowDagger";
        WithLightAttackTempo(archetype, id, SwordTempo.SpeedFor(id));
        Register(name, id, archetype, Clone(knifeCategory), knifeCategory.persistentID, mastaba,
            "Strike from the shadows. A fully charged hit makes you vanish for six seconds: no enemy can see, hear or chase you. Kills while hidden sharpen the blade.");
    }

    // The Omneco Maul with the sledgehammer's swing and charge - but a charge that never swings by
    // itself: a copy of the hammer's animation set with the auto-attack (4.2 s) pushed out of
    // reach. Holding it pulls enemies in; releasing a full charge blasts them out (GravityHammer).
    private static void AddGravityHammer(string name, uint id)
    {
        var hammer = Lookup<MeleeArchetypeDataBlock>("Hammer");
        var hammerCategory = Lookup<GearCategoryDataBlock>("MeleeSledgehammer");
        var maul = Lookup<PlayerOfflineGearDataBlock>("Maul"); // Omneco Maul

        var animations = GameDataBlockBase<MeleeAnimationSetDataBlock>.GetBlock(hammer.MeleeAnimationSet)
            ?? throw new InvalidOperationException("the hammer's melee animation set wasn't found");
        if (GameDataBlockBase<MeleeAnimationSetDataBlock>.HasBlock(id))
            throw new InvalidOperationException($"melee animation set id {id} is taken by another mod");
        var held = Clone(animations);
        held.persistentID = id;
        held.name = name.Replace(" ", "") + "_Animations";
        held.AutoAttackTime = 1000000f;
        held.AutoAttackWarningTime = 999999f;
        GameDataBlockBase<MeleeAnimationSetDataBlock>.AddBlock(held, -1);

        var archetype = Clone(hammer);
        archetype.MeleeAnimationSet = id;
        archetype.LightAttackDamage = 4f;         // hammer 3
        archetype.ChargedAttackDamage = 30f;      // hammer 20
        archetype.ChargedStaggerMulti = 20f;      // hammer 1.3
        archetype.CanHitMultipleEnemies = true;
        archetype.AttackSphereRadius = 0.5f;      // hammer 0.3
        archetype.CameraDamageRayLength = 2.2f;   // hammer 1.8
        archetype.PlayerRunSpeedMultiWhileCharging = 0.6f; // anchored while it pulls

        Register(name, id, archetype, Clone(hammerCategory), hammerCategory.persistentID, maul,
            "Hold the charge as long as you like and it drags every enemy in sight toward you. Let go of a full charge and it throws them all back out.");
    }

    // Light attacks `speed` times as fast: the weapon gets its own copy of its animation set (id =
    // the weapon's id) with the light attacks' timings - hit, damage window, combo, length -
    // divided by `speed`. SwordTempo speeds the animation itself up to match.
    private static void WithLightAttackTempo(MeleeArchetypeDataBlock archetype, uint id, float speed)
    {
        var source = GameDataBlockBase<MeleeAnimationSetDataBlock>.GetBlock(archetype.MeleeAnimationSet)
            ?? throw new InvalidOperationException($"melee animation set {archetype.MeleeAnimationSet} wasn't found");
        if (GameDataBlockBase<MeleeAnimationSetDataBlock>.HasBlock(id))
            throw new InvalidOperationException($"melee animation set id {id} is taken by another mod");
        var set = Clone(source);
        set.persistentID = id;
        set.name = archetype.name + "_Tempo";
        set.FPAttackMissRight = Faster(set.FPAttackMissRight, speed);
        set.FPAttackMissLeft = Faster(set.FPAttackMissLeft, speed);
        set.FPAttackHitRight = Faster(set.FPAttackHitRight, speed);
        set.FPAttackHitLeft = Faster(set.FPAttackHitLeft, speed);
        GameDataBlockBase<MeleeAnimationSetDataBlock>.AddBlock(set, -1);
        archetype.MeleeAnimationSet = id;
    }

    private static MeleeAnimationSetDataBlock.MeleeAttackData Faster(MeleeAnimationSetDataBlock.MeleeAttackData data, float speed)
    {
        // Each timing is stored twice - in seconds (AttackHitTime, ...) and in frames
        // (m_attackHitFrameTime, ...) - and the native AttackHitFrameTime-style properties read and
        // write those same frame fields. So: scale the seconds and m_ frame fields, each once, from
        // the original values, and leave the native properties alone.
        var copy = CopyObject(data);
        var timings = new System.Collections.Generic.List<(System.Reflection.PropertyInfo Property, float Value)>();
        foreach (var property in typeof(MeleeAnimationSetDataBlock.MeleeAttackData).GetProperties())
        {
            if (property.PropertyType != typeof(float) || !property.CanWrite || !property.Name.EndsWith("Time")) continue;
            if (property.Name.EndsWith("FrameTime") && !property.Name.StartsWith("m_")) continue;
            timings.Add((property, (float)property.GetValue(copy)!));
        }
        foreach (var (property, value) in timings) property.SetValue(copy, Formulas.ScaledTiming(value, speed));
        return copy;
    }

    // Gives the cloned archetype and category the weapon's id and name, builds its loadout entry
    // from the model's gear (re-pointed at the new category), and registers all three.
    private static void Register(string name, uint id, MeleeArchetypeDataBlock archetype, GearCategoryDataBlock category,
        uint modelCategoryId, PlayerOfflineGearDataBlock modelGear, string description)
    {
        var gearJson = RewriteGearJson(modelGear.GearJSON, modelCategoryId, id, DisplayName(name));
        if (gearJson == null)
        {
            Plugin.Logger.LogError($"GTFO.AbsurdWeapons: couldn't find category {modelCategoryId} in '{modelGear.name}' gear JSON - not adding the {name}.");
            return;
        }

        // Block names are lookup keys; the display name (which may have spaces) goes in PublicName.
        var blockName = name.Replace(" ", "");
        archetype.persistentID = id;
        archetype.name = blockName;
        archetype.PublicName = DisplayName(name);

        category.persistentID = id;
        category.name = "Melee" + blockName;
        category.MeleeArchetype = id;
        // Fresh instances: the cloned PublicName/Description still point at the source weapon's,
        // and editing those would rename it too.
        category.PublicName = new LocalizedText { Id = 0, UntranslatedText = DisplayName(name) };
        category.Description = new LocalizedText { Id = 0, UntranslatedText = description };

        var gear = Clone(modelGear);
        gear.persistentID = id;
        gear.name = blockName;
        gear.GearJSON = gearJson;

        GameDataBlockBase<MeleeArchetypeDataBlock>.AddBlock(archetype, -1);
        GameDataBlockBase<GearCategoryDataBlock>.AddBlock(category, -1);
        AddedCategories.Add(id);
        GameDataBlockBase<PlayerOfflineGearDataBlock>.AddBlock(gear, -1);

        Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons added the {name} (archetype, category and gear id {id}).");
    }
}
