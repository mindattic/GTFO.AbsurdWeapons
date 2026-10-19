using System;
using BepInEx.Configuration;
using HarmonyLib;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// Raises how many tentacles the game can draw at once, for every tentacle type (strikers, tanks,
/// scouts, wrap-arounds, the Tentacle Cannon's). Each type's GPU tentacle pool is a fixed size
/// (<c>GPUC_Setup.m_globalLimit</c>) that its compute buffers are built from in
/// <c>GPUC_Setup.Setup</c>, so the limit is multiplied just before that runs. When a pool is full,
/// new tentacles - an enemy's or ours - simply don't appear.
/// </summary>
[HarmonyPatch(typeof(GPUC_Setup), nameof(GPUC_Setup.Setup))]
internal static class TentaclePool
{
    private static ConfigEntry<float> multiplier = null!;
    private static readonly System.Collections.Generic.Dictionary<GPUCurvyType, int> originalLimits = new();

    internal static void Bind(ConfigFile config)
    {
        multiplier = config.Bind("Tentacles", "PoolMultiplier", 4f,
            new ConfigDescription("How many times more tentacles the game can draw at once (every type, enemies' included). Takes effect on game restart.",
                new AcceptableValueRange<float>(1f, 16f)));
    }

    [HarmonyPrefix]
    private static void BeforeSetup(GPUC_Setup __instance)
    {
        try
        {
            // The game sets up the pools again every level on the same objects, so the multiplier
            // is applied to each type's original size, never to an already-enlarged one.
            var before = __instance.m_globalLimit;
            if (!originalLimits.TryGetValue(__instance.m_type, out var original))
                originalLimits[__instance.m_type] = original = before;
            var after = Formulas.EnlargedPoolLimit(original, multiplier.Value);
            if (after <= original || after == before) return;
            __instance.m_globalLimit = after;
            Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons: {__instance.m_type} tentacle pool {before} -> {after}.");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons couldn't enlarge a tentacle pool: {e.Message}");
        }
    }
}
