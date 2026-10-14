using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// Both completed and failed carries return on the lower floor. These links come from
// the 19:30/19:38 manual ARRs, including a yellow loss at (-203,6,217).
// A descent is one-way: it must never be offered as a way back up a bridge.
internal sealed class BlundervilleDeliveryRecovery
{
    private enum Stage { None, Approach, Drop, Lower, Ascent }
    private Stage _stage;
    private long _round = -1;
    private Vector3 _gate, _foot;
    private Vector3[] _drop = [], _ascent = [];
    private IGeometryShape? _dropFloor, _ascentFloor;
    private bool _sideRamp;

    public void Reset() { _stage = Stage.None; _round = -1; _dropFloor = _ascentFloor = null; }

    public BlundervilleRaceDecision? Apply(BlundervilleRaceState state, Vector3 player, DateTime now, bool grounded, bool jumping)
    {
        if (!state.NeedsPickup || state.LastCrystalParam is not (35 or 36 or 37))
        { Reset(); return null; }
        if (_round != state.RoundVersion) { Reset(); _round = state.RoundVersion; }
        if (_stage != Stage.Ascent && player.Y >= 4.8f && Distance(player, BlundervilleRaceState.PickupCenter) <= 7)
        {
            _stage = Stage.None;
            return Decision(BlundervilleRaceState.PickupCenter, BlundervilleRoutes.NavigationBoundary(state.Course, player),
                "搬运返程：回中央领取", Distance(player, BlundervilleRaceState.PickupCenter) <= BlundervilleRacePlanner.PickupRadius);
        }
        if (_stage == Stage.None)
        {
            var west = player.X <= -200;
            _sideRamp = false;
            // Completed carries leave from the deposit platform itself. Going
            // back to the failed-carry exit crosses the last device again.
            // These three platform descents are recorded in manual ARR returns.
            if (state.LastCrystalParam == 37 && player.Z < 212)
            {
                west = player.X < -202;
                _gate = Mirror(new(-205.9f, 6, 209.2f), west);
                _drop = [Mirror(new(-207.446f, 6, 209.735f), west), Mirror(new(-207.9f, 6, 213.9f), west),
                    Mirror(new(-208.1f, 4.87f, 215.47f), west), Mirror(new(-208.21f, 3.01f, 217.18f), west)];
            }
            else if (state.LastCrystalParam == 35 && player.X < -235)
            {
                _gate = new(-237.4f, 6, 267.2f);
                _drop = [new(-236.1f, 5.66f, 265.03f), new(-234.85f, 3.01f, 263.26f)];
                _sideRamp = true;
            }
            else if (state.LastCrystalParam == 36 && player.X > -165)
            {
                _gate = new(-163.2f, 6, 266.93f);
                _drop = [new(-165.91f, 3.01f, 262.9f)];
                _sideRamp = true;
            }
            else if (state.LastCrystalParam == 37)
            {
                _gate = Mirror(new(-204.1f, 6, 220.1f), west);
                _drop = [Mirror(new(-205.3f, 6, 221.4f), west), Mirror(new(-208.12f, 3.01f, 226.31f), west)];
            }
            else if (state.LastCrystalParam == 35)
            {
                _gate = new(-223.56f, 6, 270.86f);
                _drop = [new(-221, 5, 271.5f), new(-218.49f, 3.01f, 272.11f)];
            }
            else
            {
                _gate = new(-168.2f, 6, 274.74f);
                _drop = [new(-170.76f, 3.65f, 275.96f), new(-174.85f, 3.01f, 275.26f)];
            }
            _dropFloor = new BlundervilleRecoveryCorridor([_gate, .. _drop], .65f);
            _stage = Stage.Approach;
        }
        // Knockback can already have put the player downstairs. Discard the
        // upper approach immediately; never try to reach a high gate from below.
        if ((_stage == Stage.Approach || _stage == Stage.Drop && (Distance(player, _drop[^1]) <= .55f
                || !_dropFloor!.Contains(player, 1))) && grounded && !jumping && player.Y < 4.2f)
        {
            ConfigureAscent(player);
            _stage = Stage.Lower;
        }
        // Solver goal radius + GreenMove arrival tolerance can finish 0.4m
        // short. The descent corridor includes that approach error.
        if (_stage == Stage.Approach && Distance(player, _gate) <= .55f && grounded && !jumping)
            _stage = Stage.Drop;
        if (_stage == Stage.Lower && Distance(player, _foot) <= .55f && grounded && !jumping)
            _stage = Stage.Ascent;

        return _stage switch
        {
            Stage.Approach => Decision(_gate, BlundervilleRoutes.NavigationBoundary(state.Course, player), "搬运返程：到侧面出口下桥"),
            Stage.Drop => Decision(_drop[^1], _dropFloor!, "搬运返程：保持方向下到低层", path: _drop),
            Stage.Lower => Decision(_foot, BlundervilleRoutes.DeliveryLowerFloor, "搬运返程：沿下层回中央坡口",
                below: player.Y < 4.2f),
            Stage.Ascent => Decision(BlundervilleRaceState.PickupCenter, _ascentFloor!, "搬运返程：沿入口坡返回中央", path: _ascent),
            _ => null,
        };

        BlundervilleRaceDecision Decision(Vector3 target, IGeometryShape floor, string hint, bool hold = false,
            Vector3[]? path = null, bool below = false)
            => new(target, hold || !state.CanMove(now), false, null, hint, path != null)
            { RecoveryFloor = floor, ContinuousPath = path, BelowDeliveryMechanisms = below };
    }

    private void ConfigureAscent(Vector3 player)
    {
        if (_sideRamp || player.Z < 250)
        {
            var west = player.X <= -200;
            _foot = Mirror(new(-211.3f, 3.01f, 241.2f), west);
            _ascent = [Mirror(new(-211.5f, 3.43f, 242.8f), west), Mirror(new(-208.15f, 5.36f, 245.17f), west),
                Mirror(new(-204.67f, 6.03f, 247.46f), west), BlundervilleRaceState.PickupCenter];
        }
        else
        {
            _foot = new(-200, 3.01f, 270);
            _ascent = [new(-200, 4.5f, 265), new(-200, 6.03f, 260), BlundervilleRaceState.PickupCenter];
        }
        _ascentFloor = new BlundervilleRecoveryCorridor([_foot, .. _ascent], .65f);
    }

    private static Vector3 Mirror(Vector3 p, bool west) => west ? p : p with { X = -400 - p.X };
    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
}
