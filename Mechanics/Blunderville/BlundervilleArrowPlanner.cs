using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

// Misdirection locks the heading while input is held. A bend/wait inside a
// hammer row must reacquire the rotating arrow, so ordinary timed A* is invalid.
internal sealed class BlundervilleArrowPlanner
{
    // The second hammer row covers Z=-286..-276. Directly south of both side
    // walkways are real exit holes (Z<-289.5); only X=-203..-197 continues.
    // Stop the straight run between the hammers and those holes, then hand the
    // bend into the central exit to ordinary navigation. ARR 2026-10-12 00:17
    // pinned the old -289 endpoint against the hole edge and ping-ponged with
    // ordinary navigation across the -288.5 hand-off for ten seconds.
    internal const float SecondRowExit = -287.5f;
    internal const float SecondRowRelease = -286.8f;
    // A small drift after the hand-off must not re-enter the arrow controller.
    // Only a real return toward the hammers (e.g. knockback) re-arms it.
    internal const float SecondRowRearm = -284.5f;
    // Any start between the rows is outside both hammer rows. A staging point a
    // few decimetres away needs another exact arrow alignment and cost 4-5s.
    internal const float StagingSlack = 2.5f;

    private Vector3? _target;
    private Vector3 _origin;
    private DateTime _started;
    private bool _aligned;
    private float? _lastArrow;
    private DateTime _lastSample;
    private float _angularSpeed;
    private bool _pastSecondRow;

    public BlundervilleRaceDecision Apply(BlundervilleRaceDecision normal, BlundervilleRaceState state,
        Vector3 player, float? arrow, DateTime now, bool inputRequested = false)
    {
        if (now < _lastSample) Reset();
        if (arrow is { } sample && float.IsFinite(sample))
        {
            var dt = (now - _lastSample).TotalSeconds;
            if (_lastArrow is { } previous && dt is > 0 and < .2)
            {
                var speed = MathF.IEEERemainder(sample - previous, MathF.Tau) / (float)dt;
                if (MathF.Abs(speed) is > .5f and < 8) _angularSpeed = speed;
            }
            _lastArrow = sample; _lastSample = now;
        }
        var pastSecondRow = player.Z < SecondRowRelease || _pastSecondRow && player.Z < SecondRowRearm;
        if (!state.IsRacing || state.Qualified || state.Course != BlundervilleCourse.FirstA || !state.HasMisdirection
            || player.Z > -247 || pastSecondRow || player.Y < 9 || !state.CanMove(now))
        {
            var keepReleased = pastSecondRow && state.IsRacing && !state.Qualified
                && state.Course == BlundervilleCourse.FirstA && state.HasMisdirection;
            Reset(); _pastSecondRow = keepReleased;
            return normal with { DirectTraversal = false };
        }
        _pastSecondRow = false;
        if (_target is { } active)
        {
            if (!_aligned && Distance(player, _origin) < .01f && (!inputRequested || now - _started > TimeSpan.FromSeconds(.06))) ClearTarget();
            else
            {
                if (!_aligned && Distance(player, _origin) > .05f && arrow is { } locked && float.IsFinite(locked) && MathF.Cos(locked) < -.7f)
                {
                    // Native input may start a frame after the sample. Once motion
                    // confirms the locked arrow, follow that ray instead of steering
                    // back to the old endpoint and releasing input inside the row.
                    active = player + new Vector3(MathF.Sin(locked), 0, MathF.Cos(locked)) * ((active.Z - player.Z) / MathF.Cos(locked));
                    _target = active; _aligned = true;
                }
                if (player.Z <= active.Z + .35f || Vector3.DistanceSquared(player, _origin) > 25 * 25
                    || now < _started || now - _started > TimeSpan.FromSeconds(5)) ClearTarget();
                else if (Distance(player, _origin) < .2f && (now - _started > TimeSpan.FromSeconds(.3)
                    || arrow is not { } current || MathF.Cos(current - MathF.Atan2(active.X - player.X, active.Z - player.Z)) < .9659f)) ClearTarget();
                else return new(active, false, false, null, "箭头桥：保持方向，连续穿过整排机关", true) { HeadingTolerance = _aligned ? 0 : AcquisitionTolerance };
            }
        }
        var row = player.Z > -268.5f ? -261f : -281f;
        var staging = row + 7.5f;
        var approach = player.Z > staging + StagingSlack;
        var end = approach ? staging : row == -261f ? row - 8 : SecondRowExit;
        // The middle island at z=-270 and the central opening in the second
        // row are real floor exclusions. Exit beside them, not into their edge.
        var side = player.X < -200 ? -1 : 1;
        var lane = row == -261 ? (approach ? -200 : -200 + side * 3.5f) : -200 + side * 6;
        var desired = MathF.Atan2(lane - player.X, end - player.Z);
        var heading = arrow.GetValueOrDefault() + _angularSpeed * .016f;
        if (arrow == null || _angularSpeed == 0 || !float.IsFinite(heading) || MathF.Cos(heading - desired) < (approach ? .9955f : .985f))
            return new(new(player.X, player.Y, end), true, false, null, "箭头桥：安全区等待箭头朝前", true);
        var distance = (end - player.Z) / MathF.Cos(heading);
        var target = player + new Vector3(MathF.Sin(heading), 0, MathF.Cos(heading)) * distance;
        var floor = BlundervilleRoutes.Boundary(state.Course);
        for (var i = 0; i <= 30; ++i)
        {
            var p = Vector3.Lerp(player, target, i / 30f);
            var clearance = MathF.Min(.6f, Distance(player, p) * .5f);
            if (!floor.Contains(p + Vector3.UnitX * clearance) || !floor.Contains(p - Vector3.UnitX * clearance))
                return new(target, true, false, null, "箭头桥：等待指向桥面内侧的箭头", true);
        }
        _target = target; _origin = player; _started = now; _aligned = false;
        return new(target, false, false, null, "箭头桥：等待整排通行窗口", true) { HeadingTolerance = AcquisitionTolerance };
    }

    private void ClearTarget() { _target = null; _started = default; _aligned = false; }
    public void Reset() { ClearTarget(); _lastArrow = null; _lastSample = default; _angularSpeed = 0; _pastSecondRow = false; }
    // One 30fps frame of acquisition, centered at 16ms, plus sample error.
    private float AcquisitionTolerance => MathF.Max(.06f, MathF.Abs(_angularSpeed) * .017f + .025f);
    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
}
