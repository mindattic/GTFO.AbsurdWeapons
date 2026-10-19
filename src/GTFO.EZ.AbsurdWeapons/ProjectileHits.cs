using Player;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// What a hand-flown projectile (rocket, launcher grenade) can touch besides enemies and the world:
/// any player or bot except the one who fired it. Players sit on two layers - the local player's
/// own mover and every synced (remote or bot) player - neither of which is in the enemy or world masks.
/// </summary>
internal static class ProjectileHits
{
    private static int PlayerMask => (1 << LayerManager.LAYER_PLAYER_MOVER) | (1 << LayerManager.LAYER_PLAYER_SYNCED);

    // The nearest player or bot other than `owner` along this sweep. The shooter's own collider
    // (the projectile starts at their muzzle) is skipped, so it never goes off in their face.
    internal static bool OtherPlayer(Vector3 from, float radius, Vector3 direction, float distance, PlayerAgent owner, out RaycastHit hit)
    {
        hit = default;
        var found = false;
        var best = float.MaxValue;
        foreach (var candidate in Physics.SphereCastAll(from, radius, direction, distance, PlayerMask))
        {
            if (candidate.distance >= best || candidate.collider == null) continue;
            var agent = candidate.collider.GetComponentInParent<PlayerAgent>();
            if (agent == null || agent.Pointer == owner.Pointer) continue;
            best = candidate.distance;
            hit = candidate;
            found = true;
        }
        return found;
    }
}
