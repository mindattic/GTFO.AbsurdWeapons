using System;
using System.Reflection;
using System.Text.RegularExpressions;
using GameData;
using HarmonyLib;
using Localization;

namespace GTFO.Weapons;

/// <summary>
/// Adds the Glaive - the knife's fixed-blade model, swung overhead with the sledgehammer's full
/// weight and reach (a hammer with a blade) - as a new selectable melee weapon by registering
/// three new datablocks right after the game loads its own: a melee archetype (stats + which
/// animation/sound set to use), a gear category pointing at it, and an offline-gear entry (what
/// appears in the loadout screen). Nothing vanilla is modified.
/// </summary>
[HarmonyPatch(typeof(GameDataInit), nameof(GameDataInit.Initialize))]
internal static class GlaiveInjector
{
    // Fixed rather than "max existing + 1" so every player in a lobby agrees on them - gear syncs
    // by category id. Vanilla uses 1-4 for melee archetypes and two-digit ids for the rest.
    internal const uint ArchetypeId = 1001;
    internal const uint CategoryId = 1001;
    internal const uint OfflineGearId = 1001;

    private const string GlaiveName = "Glaive";

    [HarmonyPostfix]
    private static void Postfix()
    {
        try
        {
            Inject();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.Weapons failed to add the Glaive: {e}");
        }
    }

    private static void Inject()
    {
        if (GameDataBlockBase<MeleeArchetypeDataBlock>.HasBlock(ArchetypeId)
            || GameDataBlockBase<GearCategoryDataBlock>.HasBlock(CategoryId)
            || GameDataBlockBase<PlayerOfflineGearDataBlock>.HasBlock(OfflineGearId))
        {
            Plugin.Logger.LogError($"GTFO.Weapons: id {ArchetypeId} is already taken by another mod's datablock - not adding the Glaive.");
            return;
        }

        var hammer = Lookup<MeleeArchetypeDataBlock>("Hammer");
        var hammerCategory = Lookup<GearCategoryDataBlock>("MeleeSledgehammer");
        var knifeCategory = Lookup<GearCategoryDataBlock>("MeleeKnife");
        var knifeGear = Lookup<PlayerOfflineGearDataBlock>("Knife"); // Mastaba Fixed Blade

        // Stats and swing: the hammer's, wholesale (damage, stagger, reach, overhead swing
        // animation/SFX) - the knife model is just a reskin onto the hammer's archetype/category.
        var archetype = Clone(hammer);
        archetype.persistentID = ArchetypeId;
        archetype.name = GlaiveName;
        archetype.PublicName = GlaiveName;
        archetype.CanHitMultipleEnemies = true; // the hammer's own is single-target; the Glaive cleaves

        var category = Clone(hammerCategory);
        category.persistentID = CategoryId;
        category.name = "MeleeGlaive";
        category.MeleeArchetype = ArchetypeId;
        // Fresh instances: the cloned PublicName/Description still point at the Sledgehammer's, and
        // editing those would rename the Sledgehammer too.
        category.PublicName = new LocalizedText { Id = 0, UntranslatedText = GlaiveName };
        category.Description = new LocalizedText { Id = 0, UntranslatedText = "A fixed blade swung overhead with a hammer's full weight and reach. Hits like a sledgehammer and cleaves through a crowd." };

        var gearJson = RewriteGearJson(knifeGear.GearJSON, knifeCategory.persistentID, CategoryId, GlaiveName);
        if (gearJson == null)
        {
            Plugin.Logger.LogError("GTFO.Weapons: couldn't find the Knife category in the Mastaba Fixed Blade's gear JSON - not adding the Glaive.");
            return;
        }

        var gear = Clone(knifeGear);
        gear.persistentID = OfflineGearId;
        gear.name = GlaiveName;
        gear.GearJSON = gearJson;

        GameDataBlockBase<MeleeArchetypeDataBlock>.AddBlock(archetype, -1);
        GameDataBlockBase<GearCategoryDataBlock>.AddBlock(category, -1);
        GameDataBlockBase<PlayerOfflineGearDataBlock>.AddBlock(gear, -1);

        Plugin.Logger.LogInfo($"GTFO.Weapons added the Glaive (archetype {ArchetypeId}, category {CategoryId}, gear {OfflineGearId}).");
    }

    private static T Lookup<T>(string name) where T : GameDataBlockBase<T>
    {
        var id = GameDataBlockBase<T>.GetBlockID(name);
        return GameDataBlockBase<T>.GetBlock(id) ?? throw new InvalidOperationException($"vanilla {typeof(T).Name} '{name}' not found");
    }

    // Copies every writable instance property onto a new block, so fields this mod doesn't
    // explicitly set (stamina costs, hit timing, etc.) start from the source weapon's values.
    private static T Clone<T>(T source) where T : GameDataBlockBase<T>, new()
    {
        var copy = new T();
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0) continue;
            property.SetValue(copy, property.GetValue(source));
        }
        return copy;
    }

    // Points the gear entry at the Glaive's category and renames it. Returns null if the category
    // component isn't there, rather than registering gear that would silently stay a spear.
    internal static string? RewriteGearJson(string json, uint fromCategory, uint toCategory, string name)
    {
        var categoryComp = new Regex($"\"c\"\\s*:\\s*2\\s*,\\s*\"v\"\\s*:\\s*{fromCategory}\\b");
        if (!categoryComp.IsMatch(json)) return null;

        json = categoryComp.Replace(json, $"\"c\":2,\"v\":{toCategory}", 1);
        json = Regex.Replace(json, "\"Name\"\\s*:\\s*\"[^\"]*\"", $"\"Name\":\"{name}\"");
        json = Regex.Replace(json, "(\"publicName\"\\s*:\\s*\\{\\s*\"data\"\\s*:\\s*)\"[^\"]*\"", $"$1\"{name}\"");
        return json;
    }
}
