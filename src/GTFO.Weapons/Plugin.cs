using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace GTFO.Weapons;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInProcess("GTFO.exe")]
[BepInDependency("dev.gtfomodding.gtfo-api")]
[BepInDependency(GtfoEzGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BasePlugin
{
    public const string GUID = "MindAttic.GTFO.Weapons";
    public const string NAME = "GTFO.Weapons";
    public const string VERSION = "1.0.0";

    // Optional: only used to order our datablock injection after it.
    internal const string GtfoEzGuid = "MindAttic.GTFO.EZ";

    internal static ManualLogSource Logger = null!;

    public override void Load()
    {
        Logger = Log;
        WeaponSettings.Bind(Config);
        TentaclePool.Bind(Config);
        ClassInjector.RegisterTypeInIl2Cpp<SeekerProjectile>();
        ClassInjector.RegisterTypeInIl2Cpp<BurstFlash>();
        ClassInjector.RegisterTypeInIl2Cpp<TentacleLash>();
        ClassInjector.RegisterTypeInIl2Cpp<LaserBeam>();
        SeekerGuns.Init();
        ImpactBursts.Init();
        Stealth.Init();
        TentacleCannon.Init();
        LaserGuns.Init();
        Knockback.Init();
        new Harmony(GUID).PatchAll();
        AddComponent<StealthVisuals>();
        AddComponent<KnockbackSlides>();
        AddComponent<DashRunner>();
        Logger.LogInfo($"{NAME} v{VERSION} loaded.");
    }
}
