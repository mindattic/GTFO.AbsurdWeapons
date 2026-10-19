using System;
using System.Collections.Generic;
using Agents;
using LevelGeneration;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// Door locks (padlocks): vanilla bullets and swings break them through <see cref="LG_WeakLockDamage"/>,
/// but this mod's weapons only look for enemy limbs, so without this they pass a lock by. Call it
/// only on the attacker's own game, same as the enemy damage.
/// </summary>
internal static class LockDamage
{
    // The lock `collider` belongs to, if any, takes `damage`. Returns whether it was a lock.
    internal static bool TryHit(Collider? collider, float damage, Agent source, Vector3 point, Vector3 direction, Vector3 normal)
    {
        var target = collider != null ? collider.GetComponentInParent<LG_WeakLockDamage>() : null;
        if (target == null) return false;
        Hit(target, damage, source, point, direction, normal);
        return true;
    }

    // Every lock on the line from `from` along `direction`, out to `range`, once each - for a
    // shot that goes through walls.
    internal static void Line(Vector3 from, Vector3 direction, float range, float damage, Agent source)
    {
        var struck = new HashSet<IntPtr>();
        foreach (var hit in Physics.RaycastAll(from, direction, range, ~0, QueryTriggerInteraction.Collide))
        {
            var target = hit.collider.GetComponentInParent<LG_WeakLockDamage>();
            if (target != null && struck.Add(target.Pointer)) Hit(target, damage, source, hit.point, direction, hit.normal);
        }
    }

    // Every lock within `radius` of `center` that it can see, once each - for a blast.
    internal static void Radius(Vector3 center, float radius, float damage, Agent source)
    {
        var struck = new HashSet<IntPtr>();
        foreach (var collider in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide))
        {
            var target = collider.GetComponentInParent<LG_WeakLockDamage>();
            if (target == null || !struck.Add(target.Pointer)) continue;
            var point = collider.ClosestPoint(center);
            // The lock's own door is world geometry, so only a wall well short of the lock blocks it.
            var line = point - center;
            if (line.magnitude > 0.5f && Physics.Raycast(center, line.normalized, line.magnitude - 0.4f, LayerManager.MASK_WORLD)) continue;
            var direction = line.sqrMagnitude > 0.0001f ? line.normalized : Vector3.forward;
            Hit(target, damage, source, point, direction, -direction);
        }
    }

    internal static void Hit(LG_WeakLockDamage target, float damage, Agent source, Vector3 point, Vector3 direction, Vector3 normal)
    {
        try
        {
            target.BulletDamage(damage, source, point, direction, normal);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons lock damage failed: {e.Message}");
        }
    }
}
