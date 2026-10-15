using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace GTFO.Weapons;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInProcess("GTFO.exe")]
public class Plugin : BasePlugin
{
    public const string GUID = "MindAttic.GTFO.Weapons";
    public const string NAME = "GTFO.Weapons";
    public const string VERSION = "1.0.0";

    internal static ManualLogSource Logger = null!;

    public override void Load()
    {
        Logger = Log;
        new Harmony(GUID).PatchAll();
        Logger.LogInfo($"{NAME} v{VERSION} loaded.");
    }
}
