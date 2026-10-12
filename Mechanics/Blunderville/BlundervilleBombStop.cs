using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

// The forced 1.2s stop can land inside the FirstA ramp sweeps (01:01 and 02:33
// ARRs both stopped near the z=-219.9 row). Within the last 2.5s, stop at the
// first position that stays clear of every forecast hazard until just after
// expiry; otherwise keep the route until the forced stop.
internal static class BlundervilleBombStop
{
    internal const double ExpiryGrace = .4;
    internal const float Clearance = .5f;

    public static bool StopEarly(BlundervilleRaceState state, IEnumerable<BlundervilleHazard> hazards, Vector3 player, DateTime now)
    {
        if (state.AccelerationBombExpiresAt is not { } expires || expires == DateTime.MaxValue) return false;
        var remaining = (expires - now).TotalSeconds;
        if (remaining > BlundervilleRaceState.AccelerationBombSafeStopWindow || remaining < 0) return false;
        if (state.MustStopForAccelerationBomb(now)) return true;
        var until = expires.AddSeconds(ExpiryGrace);
        return !hazards.Any(h => h.Until >= now && h.From <= until && h.Geometry.Contains(player, Clearance));
    }
}
