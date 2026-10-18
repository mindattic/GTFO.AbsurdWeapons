// Generated from GTFO.EZ src/GTFO.EZ/Shared/SharedOwner.cs (39a419b) - don't edit here.
// Change GTFO.EZ's copy and run GTFO.EZ/tools/publish-shared.ps1.
using System;
using System.Reflection;
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

    private static readonly string[] Order =
    {
        "MindAttic.GTFO.EZ",
        "MindAttic.GTFO.AbsurdWeapons",
        "MindAttic.GTFO.AbsurdMoves",
        "MindAttic.GTFO.CrashTestDummies",
    };

    private static bool? _isOwner;

    // True in exactly one loaded copy: the first mod in Order that's installed.
    internal static bool IsOwner => _isOwner ??= FindOwner();

    private static bool FindOwner()
    {
        try
        {
            var mine = Assembly.GetExecutingAssembly();
            foreach (var guid in Order)
            {
                if (!IL2CPPChainloader.Instance.Plugins.TryGetValue(guid, out var info) || info.Instance == null) continue;
                return info.Instance.GetType().Assembly == mine;
            }
        }
        catch (Exception e)
        {
            Log.LogWarning($"Couldn't tell which GTFO.* mod runs the shared keys. {e.Message}");
        }
        return false;
    }

    internal static bool Get(string key) => AppDomain.CurrentDomain.GetData("MindAttic.GTFO." + key) is true;

    internal static void Set(string key, bool value) => AppDomain.CurrentDomain.SetData("MindAttic.GTFO." + key, value);
}
