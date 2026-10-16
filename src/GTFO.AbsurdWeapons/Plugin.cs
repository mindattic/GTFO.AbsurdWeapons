using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace GTFO.AbsurdWeapons;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInProcess("GTFO.exe")]
[BepInDependency("dev.gtfomodding.gtfo-api")]
[BepInDependency(GtfoEzGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BasePlugin
{
    public const string GUID = "MindAttic.GTFO.AbsurdWeapons";
    public const string NAME = "GTFO.AbsurdWeapons";
    public const string VERSION = "1.0.0";

    // Optional: only used to order our datablock injection after it.
    internal const string GtfoEzGuid = "MindAttic.GTFO.EZ";

    internal static ManualLogSource Logger = null!;

    public override void Load()
    {
        Logger = Log;
        WeaponSettings.Bind(Config);
        TentaclePool.Bind(Config);
        GearCycler.Bind(Config);
        Stealth.Bind(Config);
        KatanaDash.Bind(Config);
        GravityHammer.Bind(Config);
        ImpactBurst.Bind(Config);
        Knockback.Bind(Config);
        MjolnirThrow.Bind(Config);
        FlameThrower.Bind(Config);
        FlakCannon.Bind(Config);
        VictoryBanner.Bind(Config);
        ClassInjector.RegisterTypeInIl2Cpp<SeekerProjectile>();
        ClassInjector.RegisterTypeInIl2Cpp<BurstFlash>();
        ClassInjector.RegisterTypeInIl2Cpp<TentacleLash>();
        ClassInjector.RegisterTypeInIl2Cpp<LaserBeam>();
        ClassInjector.RegisterTypeInIl2Cpp<LauncherGrenade>();
        ClassInjector.RegisterTypeInIl2Cpp<RailBeam>();
        ClassInjector.RegisterTypeInIl2Cpp<LightningBolt>();
        ClassInjector.RegisterTypeInIl2Cpp<ThrownHammer>();
        ClassInjector.RegisterTypeInIl2Cpp<FireBombProjectile>();
        ClassInjector.RegisterTypeInIl2Cpp<ShrapnelShard>();
        ClassInjector.RegisterTypeInIl2Cpp<FlakBall>();
        ClassInjector.RegisterTypeInIl2Cpp<BannerPylon>();
        SeekerGuns.Init();
        ImpactBursts.Init();
        Stealth.Init();
        TentacleCannon.Init();
        LaserGuns.Init();
        GrenadeLauncher.Init();
        Railgun.Init();
        ChainLightning.Init();
        FlameThrower.Init();
        FlakCannon.Init();
        VictoryBanner.Init();
        Knockback.Init();
        new Harmony(GUID).PatchAll();
        AddComponent<StealthVisuals>();
        AddComponent<KnockbackSlides>();
        AddComponent<DashRunner>();
        AddComponent<GravityPull>();
        AddComponent<GearCycler>();
        AddComponent<InfiniteAmmo>();
        AddComponent<FlameAfterburn>();
        AddComponent<VictoryBannerBuff>();
        MjolnirThrow.Init();
        Logger.LogInfo($"{NAME} v{VERSION} loaded.");
    }
}
