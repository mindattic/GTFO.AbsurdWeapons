using System;
using BepInEx.Configuration;
using HarmonyLib;

namespace GTFO.Weapons;

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
            var before = __instance.m_globalLimit;
            var after = (int)Math.Ceiling(before * multiplier.Value);
            if (after <= before) return;
            __instance.m_globalLimit = after;
            Plugin.Logger.LogInfo($"GTFO.Weapons: {__instance.m_type} tentacle pool {before} -> {after}.");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.Weapons couldn't enlarge a tentacle pool: {e.Message}");
        }
    }
}
