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
        InfiniteAmmo.Bind(Config);
        StimPack.Bind(Config);
        Stealth.Bind(Config);
        KatanaDash.Bind(Config);
        WarhammerSlam.Bind(Config);
        ImpactBurst.Bind(Config);
        Knockback.Bind(Config);
        MjolnirThrow.Bind(Config);
        GunInjector.Bind(Config);
        SeekerKind.Bind(Config);
        LaserGuns.Bind(Config);
        Railgun.Bind(Config);
        ChainLightning.Bind(Config);
        TentacleCannon.Bind(Config);
        GrenadeLauncher.Bind(Config);
        RocketLauncher.Bind(Config);
        ShrinkBeam.Bind(Config);
        HandGrenade.Bind(Config);
        LootMix.Bind(Config);
        Sounds.Bind(Config);
        HiveLocks.Bind(Config);
        VictoryBanner.Bind(Config);
        ConfigMigrations.Apply(Config); // after every Bind: stale old defaults move to the new ones
        ClassInjector.RegisterTypeInIl2Cpp<SeekerProjectile>();
        ClassInjector.RegisterTypeInIl2Cpp<BurstFlash>();
        ClassInjector.RegisterTypeInIl2Cpp<TentacleLash>();
        ClassInjector.RegisterTypeInIl2Cpp<LaserBeam>();
        ClassInjector.RegisterTypeInIl2Cpp<LauncherGrenade>();
        ClassInjector.RegisterTypeInIl2Cpp<RocketProjectile>();
        ClassInjector.RegisterTypeInIl2Cpp<FallingSpark>();
        ClassInjector.RegisterTypeInIl2Cpp<ShrinkStream>();
        ClassInjector.RegisterTypeInIl2Cpp<RailBeam>();
        ClassInjector.RegisterTypeInIl2Cpp<LightningBolt>();
        ClassInjector.RegisterTypeInIl2Cpp<ThrownHammer>();
        ClassInjector.RegisterTypeInIl2Cpp<BannerPylon>();
        ClassInjector.RegisterTypeInIl2Cpp<HandGrenadeFuse>();
        SeekerGuns.Init();
        ImpactBursts.Init();
        Stealth.Init();
        TentacleCannon.Init();
        LaserGuns.Init();
        GrenadeLauncher.Init();
        RocketLauncher.Init();
        ShrinkBeam.Init();
        Railgun.Init();
        ChainLightning.Init();
        VictoryBanner.Init();
        Knockback.Init();
        new Harmony(GUID).PatchAll();
        AddComponent<StealthVisuals>();
        AddComponent<KnockbackSlides>();
        AddComponent<DashRunner>();
        AddComponent<WarhammerSlam>();
        AddComponent<GearCycler>();
        AddComponent<InfiniteAmmo>();
        AddComponent<HandGrenadeSupply>();
        AddComponent<LaserDriver>();
        AddComponent<LoopSounds>();
        AddComponent<HiveLocks>();
        AddComponent<ChainLightningDriver>();
        AddComponent<ShrinkBeamDriver>();
        AddComponent<ShrinkRegrowth>();
        MjolnirThrow.Init();
        Logger.LogInfo($"{NAME} v{VERSION} loaded.");
    }
}
