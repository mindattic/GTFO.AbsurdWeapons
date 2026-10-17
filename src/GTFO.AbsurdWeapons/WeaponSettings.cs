using BepInEx.Configuration;

namespace GTFO.AbsurdWeapons;

internal enum BladeAxis
{
    Auto,
    X,
    Y,
    Z,
    All,
}

/// <summary>
/// Per-weapon tuning from BepInEx/config/MindAttic.GTFO.AbsurdWeapons.cfg. Blade length is re-read
/// every time a weapon model is built (loadout screen, re-equip, drop-in), so it can be tuned
/// without restarting; reach is baked into the weapon's datablock at startup.
/// </summary>
internal sealed class WeaponSettings
{
    internal static WeaponSettings Katana = null!;
    internal static WeaponSettings Mjolnir = null!;
    internal static WeaponSettings Warhammer = null!;
    internal static WeaponSettings ShadowDagger = null!;

    private static ConfigFile config = null!;

    internal string Weapon { get; }
    internal ConfigEntry<float> BladeLength { get; }
    internal ConfigEntry<BladeAxis> BladeLengthAxis { get; }
    internal ConfigEntry<float> Reach { get; }

    // The weapon's own numbers (0 keeps the built-in value baked into MeleeInjector's Add* methods;
    // takes effect on restart, same as Reach). Not every weapon uses every entry - Mjolnir
    // keeps its source archetype's stats untouched, for instance - so 0 there just does nothing.
    internal ConfigEntry<float> LightAttackDamage { get; }
    internal ConfigEntry<float> ChargedAttackDamage { get; }
    internal ConfigEntry<float> LightStaggerMulti { get; }
    internal ConfigEntry<float> ChargedStaggerMulti { get; }
    internal ConfigEntry<float> AttackSphereRadius { get; }
    internal ConfigEntry<float> ChargeSpeedMulti { get; } // PlayerRunSpeedMultiWhileCharging
    internal ConfigEntry<float> LightAttackSpeed { get; } // SwordTempo's per-weapon multiplier

    private WeaponSettings(ConfigFile file, string weapon, float bladeLength, string builtInReach, BladeAxis axis = BladeAxis.Y)
    {
        Weapon = weapon;
        BladeLength = file.Bind(weapon, "BladeLength", bladeLength,
            new ConfigDescription("Stretches the blade along its length: 1 is the model's own size, 2 twice as long. Visual only - raise Reach to match.",
                new AcceptableValueRange<float>(0.25f, 20f)));
        BladeLengthAxis = file.Bind(weapon, "BladeLengthAxis", axis,
            "Which of the blade model's own axes is its length. GTFO's melee parts run along Y. Auto picks the part's longest dimension instead (on a hammer head that's its width). All scales it evenly - a bigger head rather than a longer one.");
        Reach = file.Bind(weapon, "Reach", 0f,
            new ConfigDescription($"How far hits land, in meters. 0 keeps the built-in reach ({builtInReach}). Takes effect on game restart.",
                new AcceptableValueRange<float>(0f, 10f)));
        LightAttackDamage = file.Bind(weapon, "LightAttackDamage", 0f, "Light-attack damage. 0 keeps the built-in value. Takes effect on restart.");
        ChargedAttackDamage = file.Bind(weapon, "ChargedAttackDamage", 0f, "Fully-charged attack damage. 0 keeps the built-in value. Takes effect on restart.");
        LightStaggerMulti = file.Bind(weapon, "LightStaggerMulti", 0f, "Light-attack stagger multiplier. 0 keeps the built-in value. Takes effect on restart.");
        ChargedStaggerMulti = file.Bind(weapon, "ChargedStaggerMulti", 0f, "Charged-attack stagger multiplier. 0 keeps the built-in value. Takes effect on restart.");
        AttackSphereRadius = file.Bind(weapon, "AttackSphereRadius", 0f, "How wide a swing's hit sphere is, in meters. 0 keeps the built-in value. Takes effect on restart.");
        ChargeSpeedMulti = file.Bind(weapon, "ChargeSpeedMulti", 0f, "Move-speed multiplier while charging (1 = normal speed). 0 keeps the built-in value. Takes effect on restart.");
        LightAttackSpeed = file.Bind(weapon, "LightAttackSpeed", 0f, "Light-attack pace multiplier (SwordTempo). 0 keeps the built-in value. Takes effect on restart.");
    }

    internal static void Bind(ConfigFile file)
    {
        config = file;
        // The Mastaba Fixed Blade's blade is 0.268 m: x6.9 = ~1.85 m, over two and a half times a real katana's 70 cm.
        Katana = new WeaponSettings(file, "Katana", 6.9f, "3.5");
        Mjolnir = new WeaponSettings(file, "Mjolnir", 1f, "1.75, the bat's"); // its "blade" is the hammer head
        Warhammer = new WeaponSettings(file, "Warhammer", 3f, "2.4", BladeAxis.All); // a giant head, not a longer one
        ShadowDagger = new WeaponSettings(file, "Shadow Dagger", 1f, "1.75, the knife's");
    }

    // `entry` wins when it's set (non-zero); otherwise the built-in `builtIn` value.
    internal static float Or(ConfigEntry<float> entry, float builtIn) => entry.Value != 0f ? entry.Value : builtIn;

    internal static WeaponSettings? ForCategory(uint categoryId) => categoryId switch
    {
        MeleeInjector.KatanaId => Katana,
        MeleeInjector.MjolnirId => Mjolnir,
        MeleeInjector.WarhammerId => Warhammer,
        MeleeInjector.ShadowDaggerId => ShadowDagger,
        _ => null,
    };

    // Picks up edits made to the .cfg while the game is running.
    internal static void Reload() => config.Reload();
}
