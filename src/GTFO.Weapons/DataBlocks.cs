using System;
using System.Reflection;
using System.Text.RegularExpressions;
using GameData;

namespace GTFO.Weapons;

/// <summary>Helpers for building new weapons out of cloned vanilla datablocks.</summary>
internal static class DataBlocks
{
    internal static T Lookup<T>(string name) where T : GameDataBlockBase<T>
    {
        var id = GameDataBlockBase<T>.GetBlockID(name);
        return GameDataBlockBase<T>.GetBlock(id) ?? throw new InvalidOperationException($"vanilla {typeof(T).Name} '{name}' not found");
    }

    // Copies every writable instance property onto a new block, so fields this mod doesn't
    // explicitly set (stamina costs, hit timing, etc.) start from the source weapon's values.
    internal static T Clone<T>(T source) where T : GameDataBlockBase<T>, new()
    {
        var copy = new T();
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0) continue;
            property.SetValue(copy, property.GetValue(source));
        }
        return copy;
    }

    // The gear category a loadout entry belongs to: component 2 in the gear JSON's packet.
    internal static uint? CategoryIdOf(PlayerOfflineGearDataBlock gear)
    {
        var match = Regex.Match(gear.GearJSON, "\"c\"\\s*:\\s*2\\s*,\\s*\"v\"\\s*:\\s*(\\d+)");
        return match.Success ? uint.Parse(match.Groups[1].Value) : null;
    }

    // The fire mode a loadout entry asks for: component 1 in the gear JSON's packet (absent = semi).
    internal static eWeaponFireMode FireModeOf(PlayerOfflineGearDataBlock gear)
    {
        var match = Regex.Match(gear.GearJSON, "\"c\"\\s*:\\s*1\\s*,\\s*\"v\"\\s*:\\s*(\\d+)");
        return match.Success ? (eWeaponFireMode)int.Parse(match.Groups[1].Value) : eWeaponFireMode.Semi;
    }

    // Sets one component of the gear JSON's packet (e.g. 4 = ItemFPSSettings, how the gun is held
    // in first person) to `value`. Returns the JSON unchanged if the component isn't there.
    internal static string SetGearComponent(string json, int component, uint value) =>
        Regex.Replace(json, $"(\"c\"\\s*:\\s*{component}\\s*,\\s*\"v\"\\s*:\\s*)\\d+", $"${{1}}{value}", RegexOptions.None, TimeSpan.FromSeconds(1));

    // Points the gear entry at the new weapon's category and renames it. Returns null if the
    // category component isn't there, rather than registering gear that would silently stay the
    // model's original weapon.
    internal static string? RewriteGearJson(string json, uint fromCategory, uint toCategory, string name)
    {
        var categoryComp = new Regex($"\"c\"\\s*:\\s*2\\s*,\\s*\"v\"\\s*:\\s*{fromCategory}\\b");
        if (!categoryComp.IsMatch(json)) return null;

        json = categoryComp.Replace(json, $"\"c\":2,\"v\":{toCategory}", 1);
        json = Regex.Replace(json, "\"Name\"\\s*:\\s*\"[^\"]*\"", $"\"Name\":\"{name}\"");
        json = Regex.Replace(json, "(\"publicName\"\\s*:\\s*\\{\\s*\"data\"\\s*:\\s*)\"[^\"]*\"", $"$1\"{name}\"");
        return json;
    }
}
