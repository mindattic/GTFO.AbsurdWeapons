using System;
using BepInEx.Configuration;

namespace GTFO.AbsurdWeapons;

/// <summary>
/// BepInEx keeps whatever value is already in an existing .cfg, so when this mod changes a default,
/// installs that already have the setting never pick up the new one. Each entry here moves a
/// setting still sitting on its old default over to its new one (read off the Bind call itself, so
/// the new value lives in one place), once - only for configs that haven't reached that entry's
/// version yet - so a value a player set deliberately is never overwritten. (The same scheme as
/// GTFO.EZ's.) Changing a default: add a line with the next version number and the old default.
/// </summary>
internal static class ConfigMigrations
{
    // Versions 7-10, 13 and 14 were for the Loot, Hand Grenade and Stim Pack settings, which moved to
    // GTFO.AbsurdItems (with a config file of its own); this mod no longer binds them, so those lines
    // find nothing and do nothing. They stay so the next migration takes version 15, never one an
    // install has already reached.
    private static readonly (int Version, string Section, string Key, object OldDefault)[] DefaultMigrations =
    {
        (1, "Tentacle Guns", "Damage", 22f),
        (1, "Tentacle Guns", "EngageConeDegrees", 15f),
        (1, "Plasma Guns", "BlastRadius", 1.25f),
        (2, "Chain Lightning Gun", "Damage", 10f),
        (2, "Tentacle Guns", "Damage", 35f),
        (2, "Tentacle Guns", "StaggerMulti", 5f),
        (3, "Tentacle Guns", "Damage", 100f),
        (4, "Shrink Beam", "MinScale", 0.25f),
        (4, "Shrink Beam", "StepOnScale", 0.4f),
        (4, "Shrink Beam", "StepOnRadius", 0.6f),
        (4, "Sounds", "ShrinkLoopStart", "BIOTRACKER_TOOL_LOOP"),
        (4, "Sounds", "ShrinkLoopStop", "BIOTRACKER_TOOL_LOOP_STOP"),
        (5, "Shadow Dagger", "StealthKillTimeBonus", 0.25f),
        (6, "Shadow Dagger", "StealthSeconds", 2f),
        (7, "Loot", "KitSize", 100),
        (8, "Hand Grenade", "StartingCount", 3),
        (9, "Loot", "HandGrenadeWeight", 2f),
        (10, "Stim Pack", "Stim Pack Enabled", true),
        (11, "Ammo", "StreamPercentPerSecond", 1f),
        (12, "Railgun", "Damage", 150f),
        (12, "Railgun", "PenetrationRange", 200f),
        (12, "Shrink Beam", "MinScale", 0.33f),
        (12, "Shrink Beam", "StepOnScale", 0.5f),
        (12, "Shrink Beam", "StepOnRadius", 1f),
        (13, "Stim Pack", "Stim Pack Enabled", false),
        (14, "Stim Pack", "HealPercent", 50f),
    };

    // After every setting is bound.
    internal static void Apply(ConfigFile config)
    {
        var version = config.Bind("Internal", "Config Migration Version", 0,
            "Internal bookkeeping - do not edit. Tracks which one-time default migrations have already run.");
        var reached = version.Value;
        var latest = reached;
        foreach (var (target, section, key, oldDefault) in DefaultMigrations)
        {
            latest = Math.Max(latest, target);
            if (reached >= target) continue;
            var definition = new ConfigDefinition(section, key);
            if (!config.ContainsKey(definition)) continue;
            var entry = config[definition];
            if (!Equals(entry.BoxedValue, oldDefault)) continue;
            entry.BoxedValue = entry.DefaultValue;
            Plugin.Logger.LogInfo($"GTFO.AbsurdWeapons config: [{section}] {key} moved from its old default {oldDefault} to {entry.DefaultValue}.");
        }
        if (latest != reached) version.Value = latest;
    }
}
