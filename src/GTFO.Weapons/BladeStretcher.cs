using System;
using System.Collections.Generic;
using Gear;
using HarmonyLib;
using UnityEngine;

namespace GTFO.Weapons;

/// <summary>
/// Lengthens this mod's melee blades by scaling the weapon model's head part (the blade) along
/// its length once the game has assembled the model from its parts. Every model goes through
/// this - loadout screen, first person, the third-person model teammates see - so the longer
/// blade shows everywhere. The blade grows away from the grip: whichever end of it sits nearer
/// the handle stays where it was. A one-piece model (no separate blade part, like the Wox Compact knife)
/// is stretched whole instead, pinned at the hand so the grip stays in it. BladeLengthAxis All
/// scales the part evenly instead (a bigger hammer head), growing away from the shaft. Visual only; hit reach is the archetype's camera ray.
/// Every build logs the blade's length in meters before and after, for tuning BladeLength.
/// </summary>
[HarmonyPatch(typeof(GearPartHolder), nameof(GearPartHolder.OnAllPartsSpawned))]
internal static class BladeStretcher
{
    // Each blade's unstretched transform, so re-applying (a second spawn callback, or a changed
    // config) always starts from the original instead of compounding.
    private static readonly Dictionary<int, (Vector3 Scale, Vector3 Position)> originals = new();

    [HarmonyPostfix]
    private static void Postfix(GearPartHolder __instance)
    {
        try
        {
            var settings = WeaponSettings.ForCategory(__instance.CategoryData?.persistentID ?? 0);
            if (settings == null) return;

            var blade = __instance.MeleeHeadPart;
            var grip = __instance.MeleeHandlePart ?? __instance.MeleeNeckPart;
            var onePiece = blade == null;
            if (onePiece)
            {
                blade = grip;
                grip = null;
            }
            if (blade == null) return;

            WeaponSettings.Reload();
            var pin = grip != null ? grip.transform : __instance.m_aRightHand ?? __instance.transform;
            Stretch(settings.Weapon, __instance.transform, blade.transform, pin, onePiece,
                settings.BladeLength.Value, settings.BladeLengthAxis.Value);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.Weapons failed to stretch a blade: {e}");
        }
    }

    // `pin` is the grip part (the blade's grip-side end stays put) or, for a one-piece model, the
    // hand (the point of the model under it stays put).
    private static void Stretch(string weapon, Transform holder, Transform blade, Transform pin, bool onePiece, float factor, BladeAxis axisSetting)
    {
        var id = blade.GetInstanceID();
        if (!originals.TryGetValue(id, out var original))
        {
            original = (blade.localScale, blade.localPosition);
            originals[id] = original;
        }

        blade.localScale = original.Scale;
        blade.localPosition = original.Position;

        if (!TryGetLocalBounds(blade, out var bounds))
        {
            Plugin.Logger.LogWarning($"GTFO.Weapons: blade '{blade.name}' has no mesh to measure - leaving it unstretched.");
            return;
        }

        var uniform = axisSetting == BladeAxis.All;
        var axis = axisSetting switch
        {
            BladeAxis.X => 0,
            BladeAxis.Y => 1,
            BladeAxis.Z => 2,
            _ => LongestAxis(bounds.size),
        };
        var axisName = uniform ? "all axes" : "XYZ"[axis].ToString();
        var length = LengthInMeters(holder, blade, bounds.size, axis);

        if (Mathf.Approximately(factor, 1f))
        {
            Plugin.Logger.LogInfo($"GTFO.Weapons {weapon} ({holder.name}): {(onePiece ? "one-piece model" : "blade")} '{blade.name}' is {length:0.000} m along {axisName}, unstretched. Model size {Describe(bounds.size)}.");
            return;
        }

        // The point that must not move, in the blade's own space. Scaling happens around the
        // blade's pivot, which may be anywhere, so this point is put back in place afterwards.
        var pinLocal = blade.InverseTransformPoint(pin.position);
        Vector3 anchor;
        if (uniform)
        {
            // The point of the part nearest the shaft (or hand), so it grows outward from there.
            anchor = new Vector3(
                Mathf.Clamp(pinLocal.x, bounds.min.x, bounds.max.x),
                Mathf.Clamp(pinLocal.y, bounds.min.y, bounds.max.y),
                Mathf.Clamp(pinLocal.z, bounds.min.z, bounds.max.z));
        }
        else
        {
            anchor = bounds.center;
            anchor[axis] = onePiece
            ? Mathf.Clamp(pinLocal[axis], bounds.min[axis], bounds.max[axis])
            : Mathf.Abs(pinLocal[axis] - bounds.min[axis]) <= Mathf.Abs(pinLocal[axis] - bounds.max[axis])
                ? bounds.min[axis]
                : bounds.max[axis];
        }
        var anchorBefore = blade.TransformPoint(anchor);

        var scale = original.Scale;
        if (uniform) scale *= factor;
        else scale[axis] *= factor;
        blade.localScale = scale;
        blade.position += anchorBefore - blade.TransformPoint(anchor);

        Plugin.Logger.LogInfo($"GTFO.Weapons {weapon} ({holder.name}): {(onePiece ? "one-piece model" : "blade")} '{blade.name}' {length:0.000} m -> {length * factor:0.000} m (x{factor} along {axisName}). Model size {Describe(bounds.size)}.");
    }

    // Bounds of every mesh under the blade, in the blade's own (unscaled) space.
    private static bool TryGetLocalBounds(Transform blade, out Bounds bounds)
    {
        bounds = default;
        var found = false;
        foreach (var filter in blade.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            if (mesh == null) continue;

            var meshBounds = mesh.bounds;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                var local = blade.InverseTransformPoint(filter.transform.TransformPoint(point));
                if (!found) bounds = new Bounds(local, Vector3.zero);
                else bounds.Encapsulate(local);
                found = true;
            }
        }
        return found;
    }

    // How long the blade's extent along `axis` is in the weapon's own space - meters, as long as
    // the holder itself isn't scaled (the loadout screen's preview may be).
    private static float LengthInMeters(Transform holder, Transform blade, Vector3 size, int axis)
    {
        var extent = Vector3.zero;
        extent[axis] = size[axis];
        return holder.InverseTransformVector(blade.TransformVector(extent)).magnitude;
    }

    private static string Describe(Vector3 size) => $"X {size.x:0.000} / Y {size.y:0.000} / Z {size.z:0.000}";

    private static int LongestAxis(Vector3 size) =>
        size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
}
