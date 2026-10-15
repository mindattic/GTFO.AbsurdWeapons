using System;
using System.Reflection;
using System.Text.RegularExpressions;
using GameData;
using HarmonyLib;
using Localization;

namespace GTFO.Glaive;

/// <summary>
/// Adds the Glaive - the IsoCo Stinger spear, swung overhead like the sledgehammer - as a new
/// selectable melee weapon by registering three new datablocks right after the game loads its own:
/// a melee archetype (stats + which animation/sound set to use), a gear category pointing at it,
/// and an offline-gear entry (what appears in the loadout screen). Nothing vanilla is modified.
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
            Plugin.Logger.LogError($"GTFO.Glaive failed to add the Glaive: {e}");
        }
    }

    private static void Inject()
    {
        if (GameDataBlockBase<MeleeArchetypeDataBlock>.HasBlock(ArchetypeId)
            || GameDataBlockBase<GearCategoryDataBlock>.HasBlock(CategoryId)
            || GameDataBlockBase<PlayerOfflineGearDataBlock>.HasBlock(OfflineGearId))
        {
            Plugin.Logger.LogError($"GTFO.Glaive: id {ArchetypeId} is already taken by another mod's datablock - not adding the Glaive.");
            return;
        }

        var spear = Lookup<MeleeArchetypeDataBlock>("Spear");
        var hammer = Lookup<MeleeArchetypeDataBlock>("Hammer");
        var spearCategory = Lookup<GearCategoryDataBlock>("MeleeSpear");
        var hammerCategory = Lookup<GearCategoryDataBlock>("MeleeSledgehammer");
        var stinger = Lookup<PlayerOfflineGearDataBlock>("Spear_02"); // IsoCo Stinger

        // Stats: the spear's reach and cleave, the hammer's swing, sounds and weight of blow.
        var archetype = Clone(spear);
        archetype.persistentID = ArchetypeId;
        archetype.name = GlaiveName;
        archetype.PublicName = GlaiveName;
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
        category.persistentID = CategoryId;
        category.name = "MeleeGlaive";
        category.MeleeArchetype = ArchetypeId;
        category.ThirdPersonFullbodyMovement = hammerCategory.ThirdPersonFullbodyMovement; // teammates see the overhead swing
        // Fresh instances: the cloned PublicName/Description still point at the Spear's, and
        // editing those would rename the Spear too.
        category.PublicName = new LocalizedText { Id = 0, UntranslatedText = GlaiveName };
        category.Description = new LocalizedText { Id = 0, UntranslatedText = "A long razor-edged blade on a spear shaft, brought down overhead like a splitting maul. Reaches far and cleaves through a crowd." };

        var gearJson = RewriteGearJson(stinger.GearJSON, spearCategory.persistentID, CategoryId, GlaiveName);
        if (gearJson == null)
        {
            Plugin.Logger.LogError("GTFO.Glaive: couldn't find the Spear category in the IsoCo Stinger's gear JSON - not adding the Glaive.");
            return;
        }

        var gear = Clone(stinger);
        gear.persistentID = OfflineGearId;
        gear.name = GlaiveName;
        gear.GearJSON = gearJson;

        GameDataBlockBase<MeleeArchetypeDataBlock>.AddBlock(archetype, -1);
        GameDataBlockBase<GearCategoryDataBlock>.AddBlock(category, -1);
        GameDataBlockBase<PlayerOfflineGearDataBlock>.AddBlock(gear, -1);

        Plugin.Logger.LogInfo($"GTFO.Glaive added the Glaive (archetype {ArchetypeId}, category {CategoryId}, gear {OfflineGearId}).");
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
