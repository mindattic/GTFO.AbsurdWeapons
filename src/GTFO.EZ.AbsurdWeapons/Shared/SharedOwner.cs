// Generated from GTFO.EZ src/GTFO.EZ/Shared/SharedOwner.cs (948c927) - don't edit here.
// Change GTFO.EZ's copy and run GTFO.EZ/tools/publish-shared.ps1.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace MindAttic.GTFO.Shared;

// Code every GTFO.* mod carries its own copy of (published from GTFO.EZ by tools/publish-shared.ps1).
// With several of the mods installed, several copies run, so anything that must happen once - a
// key press, a warden message - only happens in the first installed mod in Order. State that has
// to be the same in every copy lives once for the whole process (Get/Set), not in a copy's statics.
internal static class SharedOwner
{
    internal static readonly ManualLogSource Log = Logger.CreateLogSource("MindAttic.GTFO.Shared");

    // Every GTFO.* mod's master switch: the first entry in its .cfg. Off, the mod returns from Load
    // before patching anything, so it counts as not installed.
    internal const string MasterSection = "0. Mod";
    internal const string MasterKey = "Enabled";

    private static readonly string[] Order =
    {
        "MindAttic.GTFO.EZ",
        "MindAttic.GTFO.EZ.AbsurdWeapons",
        "MindAttic.GTFO.EZ.AbsurdMoves",
        "MindAttic.GTFO.EZ.AbsurdItems",
        "MindAttic.GTFO.CrashTestDummies",
    };

    private static bool? _isOwner;

    // True in exactly one loaded copy: the first mod in Order that's installed, switched on and
    // carries this code - a mod version from before the shared code (an older GTFO.EZ), or one whose
    // master switch is off, is passed over rather than left owning keys it never reads.
    internal static bool IsOwner => _isOwner ??= FindOwner();

    private static bool FindOwner()
    {
        try
        {
            var mine = Assembly.GetExecutingAssembly();
            foreach (var guid in Order)
            {
                if (!TryGetActive(guid, out var plugin)) continue;
                var assembly = plugin.GetType().Assembly;
                if (assembly.GetType(typeof(SharedOwner).FullName!) == null) continue;
                return assembly == mine;
            }
        }
        catch (Exception e)
        {
            Log.LogWarning($"Couldn't tell which GTFO.* mod runs the shared keys. {e.Message}");
        }
        return false;
    }

    // Whether the mod with this plugin GUID is installed, loaded and switched on ([0. Mod] Enabled
    // not false; a version without the switch counts as on). For checks on another GTFO.* mod.
    internal static bool IsActive(string guid)
    {
        try
        {
            return TryGetActive(guid, out _);
        }
        catch (Exception e)
        {
            Log.LogWarning($"Couldn't check whether {guid} is installed. {e.Message}");
            return false;
        }
    }

    // Another mod's setting, read off its own config: false when that mod isn't active (see
    // IsActive) or hasn't bound the entry, so the caller picks what a missing setting means.
    internal static bool TryGetSetting<T>(string guid, string section, string key, [NotNullWhen(true)] out ConfigEntry<T>? entry)
    {
        entry = null;
        try
        {
            return TryGetActive(guid, out var plugin) && plugin.Config.TryGetEntry(section, key, out entry);
        }
        catch (Exception e)
        {
            Log.LogWarning($"Couldn't read [{section}] {key} from {guid}. {e.Message}");
            entry = null;
            return false;
        }
    }

    private static bool TryGetActive(string guid, [NotNullWhen(true)] out BasePlugin? plugin)
    {
        plugin = null;
        if (!IL2CPPChainloader.Instance.Plugins.TryGetValue(guid, out var info) || info.Instance is not BasePlugin loaded) return false;
        if (loaded.Config.TryGetEntry<bool>(MasterSection, MasterKey, out var enabled) && !enabled.Value) return false;
        plugin = loaded;
        return true;
    }

    internal static bool Get(string key) => AppDomain.CurrentDomain.GetData("MindAttic.GTFO." + key) is true;

    internal static void Set(string key, bool value) => AppDomain.CurrentDomain.SetData("MindAttic.GTFO." + key, value);
}
