using System;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// The numbers behind the weapons' behaviour, kept free of game and Unity types so they're unit
/// tested directly (tests/GTFO.EZ.AbsurdWeapons.Tests). The weapon code calls these.
/// </summary>
internal static class Formulas
{
    // A charge counts as full when it was held at least `leeway` (0..1) of the full charge time.
    internal static bool IsFullCharge(float heldSeconds, float fullChargeSeconds, float leeway) =>
        heldSeconds >= fullChargeSeconds * leeway;

    // Area damage at `distance` from the center: full at the center, falling linearly to
    // `edgeFraction` of it at `radius`.
    internal static float AreaFalloff(float distance, float radius, float edgeFraction)
    {
        if (radius <= 0f) return 1f;
        var t = Math.Clamp(distance / radius, 0f, 1f);
        return 1f + (edgeFraction - 1f) * t;
    }

    // Chain lightning: the damage of strike `index` (0 = the first target), each jump `falloff` of
    // the one before.
    internal static float ChainStrikeDamage(float firstDamage, float falloff, int index) =>
        firstDamage * MathF.Pow(falloff, index);

    // A knockback slide that hits a wall with `unspentMeters` of its distance still to go: the
    // first `freeMeters` are free, the rest hurt `damagePerMeter` each. 0 when it doesn't slam.
    internal static float SlamDamage(float unspentMeters, float damagePerMeter, float freeMeters) =>
        damagePerMeter <= 0f || unspentMeters <= freeMeters ? 0f : (unspentMeters - freeMeters) * damagePerMeter;

    // The gear cycler: the entry `step` (+1 / -1) away from `current` in a list of `count`,
    // wrapping at both ends. With no current entry (-1) it starts at the first (next) or last
    // (previous).
    internal static int CycleIndex(int current, int step, int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (current < 0 || current >= count) return step > 0 ? 0 : count - 1;
        return ((current + step) % count + count) % count;
    }

    // A tentacle pool's enlarged size: always from the type's original size, so setting the pools
    // up again (every level) never compounds the multiplier.
    internal static int EnlargedPoolLimit(int originalLimit, float multiplier) =>
        Math.Max(originalLimit, (int)Math.Ceiling(originalLimit * multiplier));

    // The launcher grenade's blink interval `age` seconds into its fuse: from 0.5 s down to 0.06 s,
    // speeding up toward the end.
    internal static float GrenadeFlashInterval(float age, float fuseSeconds)
    {
        var progress = fuseSeconds > 0f ? Math.Clamp(age / fuseSeconds, 0f, 1f) : 1f;
        return 0.5f + (0.06f - 0.5f) * progress * progress;
    }

    // The Shadow Dagger's damage bonus after one more kill while hidden.
    internal static float StealthBonusAfterKill(float bonus, float perKill, float max) => Math.Min(max, bonus + perKill);

    // A light attack's timing (seconds or frames) at `speed` times the pace.
    internal static float ScaledTiming(float value, float speed) => speed > 0f ? value / speed : value;

    // The Flamethrower's afterburn: one more stack on an enemy already alight, capped so repeated
    // hits can't stack damage forever.
    internal static int AfterburnStacks(int current, int max) => Math.Min(current + 1, max);
}
