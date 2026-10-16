using BepInEx.Configuration;

namespace GTFO.Weapons;

internal enum BladeAxis
{
    Auto,
    X,
    Y,
    Z,
    All,
}

/// <summary>
/// Per-weapon tuning from BepInEx/config/MindAttic.GTFO.Weapons.cfg. Blade length is re-read
/// every time a weapon model is built (loadout screen, re-equip, drop-in), so it can be tuned
/// without restarting; reach is baked into the weapon's datablock at startup.
/// </summary>
internal sealed class WeaponSettings
{
    internal static WeaponSettings Glaive = null!;
    internal static WeaponSettings Katana = null!;
    internal static WeaponSettings Mjolnir = null!;
    internal static WeaponSettings Warhammer = null!;
    internal static WeaponSettings ShadowDagger = null!;

    private static ConfigFile config = null!;

    internal string Weapon { get; }
    internal ConfigEntry<float> BladeLength { get; }
    internal ConfigEntry<BladeAxis> BladeLengthAxis { get; }
    internal ConfigEntry<float> Reach { get; }

    private WeaponSettings(ConfigFile file, string weapon, float bladeLength, string builtInReach, BladeAxis axis = BladeAxis.Y)
    {
        Weapon = weapon;
        BladeLength = file.Bind(weapon, "BladeLength", bladeLength,
            new ConfigDescription("Stretches the blade along its length: 1 is the model's own size, 2 twice as long. Visual only - raise Reach to match.",
                new AcceptableValueRange<float>(0.25f, 10f)));
        BladeLengthAxis = file.Bind(weapon, "BladeLengthAxis", axis,
            "Which of the blade model's own axes is its length. GTFO's melee parts run along Y. Auto picks the part's longest dimension instead (on a hammer head that's its width). All scales it evenly - a bigger head rather than a longer one.");
        Reach = file.Bind(weapon, "Reach", 0f,
            new ConfigDescription($"How far hits land, in meters. 0 keeps the built-in reach ({builtInReach}). Takes effect on game restart.",
                new AcceptableValueRange<float>(0f, 10f)));
    }

    internal static void Bind(ConfigFile file)
    {
        config = file;
        Glaive = new WeaponSettings(file, "Glaive", 1f, "3.2, the spear's");
        Katana = new WeaponSettings(file, "Katana", 7.5f, "3.2"); // Mastaba blade ~2 m: ~2.2 m overall
        Mjolnir = new WeaponSettings(file, "Mjolnir", 1f, "1.75, the bat's"); // its "blade" is the hammer head
        Warhammer = new WeaponSettings(file, "Warhammer", 3f, "2.4", BladeAxis.All); // a giant head, not a longer one
        ShadowDagger = new WeaponSettings(file, "Shadow Dagger", 1f, "1.75, the knife's");
    }

    internal static WeaponSettings? ForCategory(uint categoryId) => categoryId switch
    {
        MeleeInjector.GlaiveId => Glaive,
        MeleeInjector.KatanaId => Katana,
        MeleeInjector.MjolnirId => Mjolnir,
        MeleeInjector.WarhammerId => Warhammer,
        MeleeInjector.ShadowDaggerId => ShadowDagger,
        _ => null,
    };

    // Picks up edits made to the .cfg while the game is running.
    internal static void Reload() => config.Reload();
}
