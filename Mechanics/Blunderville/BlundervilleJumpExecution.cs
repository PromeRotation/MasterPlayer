using System.Numerics;
using PromeRotation.GreenMoveSystem;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed class BlundervilleJumpExecution
{
    // A queued route is not evidence that the game has started running. The
    // 01:42 ARR has failed standing launches at ~2.5m/s across this same gap.
    private BlundervilleJumpCorridor? _link;
    private bool _running, _airborne, _completed;
    private DateTime _launched, _lastSample, _nextLaunch;
    private readonly Queue<(DateTime Time, Vector3 Position)> _samples = new();
    internal const float LaneSwitchLateral = 1.5f;
    public bool InFlight => _launched != default;
    public bool Landed => _completed && _airborne;
    public bool NeedsRunupRetry { get; private set; }
    public float MeasuredSpeed { get; private set; }
    public BlundervilleJumpCorridor? Corridor => _link;
    public Vector3? Landing => _link?.MovementTarget;

    public BlundervilleJumpDecision? Prepare(BlundervilleJumpCorridor? link, Vector3 player)
    {
        if (InFlight && _link is { } flight)
            return new(flight.MovementTarget, false, false, flight, "保持空中前进，等待实际着地");
        if (link == null) { Clear(); return null; }
        if (_link?.Takeoff != link.Takeoff || _link?.Landing != link.Landing)
        {
            var nextLaunch = _nextLaunch;
            var continuing = link.Stairs && _link is { Stairs: true } previous
                && previous.MovementTarget == link.MovementTarget && _completed;
            var samples = continuing ? _samples.ToArray() : [];
            var lastSample = _lastSample;
            Clear(); _nextLaunch = nextLaunch; _link = link;
            if (continuing) { foreach (var sample in samples) _samples.Enqueue(sample); _lastSample = lastSample; }
        }
        if (_completed) return null;
        var direction = Direction(link);
        var progress = Vector3.Dot(Flat(player - link.Takeoff), direction);
        var lateral = Flat(player - link.Takeoff) - direction * progress;
        // Publish the landing while a metre of run-up floor still remains.
        var tolerance = link.LaneSwitch ? LaneSwitchLateral : .3f;
        if (!_running && progress >= -6f && progress <= (link.Stairs ? 1.2f : -.65f) && lateral.LengthSquared() <= tolerance * tolerance)
        { _running = true; NeedsRunupRetry = false; }
        return new(_running ? link.MovementTarget : link.Takeoff - direction * 1.15f, false, false, link,
            _running ? "助跑中：确认移动速度后起跳" : "对齐跳跃助跑位置");
    }

    public bool Sample(DateTime now, Vector3 player, bool grounded, bool jumping, bool hasControl, GreenMoveStatus movement)
    {
        NeedsRunupRetry = false;
        if (_lastSample != default && now < _lastSample) { Clear(); return false; }
        if (_lastSample != default && now - _lastSample > TimeSpan.FromSeconds(.3)) _samples.Clear();
        _lastSample = now;
        if (_link is not { } link || _completed) return false;
        if (InFlight)
        {
            if (link.Stairs) RecordSample(now, player, hasControl && movement.IsRunning);
            _airborne |= jumping || !grounded;
            if (now - _launched > TimeSpan.FromSeconds(1.2) || player.Y < link.Takeoff.Y - 1
                || _airborne && grounded && !jumping && now - _launched > TimeSpan.FromSeconds(.15))
            { _launched = default; _completed = true; }
            else if (!_airborne && now - _launched > TimeSpan.FromSeconds(.25))
                Retry();
            return false;
        }
        if (!_running || !grounded || jumping) { _samples.Clear(); return false; }
        var ready = Ready(link.MovementTarget, hasControl, movement);
        // Route publication can lag the run-up. Measure the actual owned run
        // across that hand-over, but still require the landing route to launch.
        // Clearing these samples on each old takeoff route caused a late
        // 0.075s speed warm-up and repeated runbacks in the 21:31 Ring ARR.
        RecordSample(now, player, hasControl && movement.IsRunning);
        var direction = Direction(link);
        var offset = Flat(player - link.Takeoff);
        var progress = Vector3.Dot(offset, direction);
        var lateral = (offset - direction * progress).Length();
        var velocity = Vector3.Zero;
        var duration = _samples.Count > 0 ? (now - _samples.Peek().Time).TotalSeconds : 0;
        if (duration >= .075) velocity = Flat(player - _samples.Peek().Position) / (float)duration;
        MeasuredSpeed = Vector3.Dot(velocity, direction);
        // A lane switch reacts while the player is still walking down the lane
        // (one 100ms scan plus route latency); its run-up line is then offset
        // from the planned axis. The takeoff band and run-up speed still apply.
        var aligned = (velocity - direction * MeasuredSpeed).Length() < (link.LaneSwitch ? 2f : 1.2f);
        if (progress >= (link.Stairs ? -.35f : -.18f) && progress <= (link.Stairs ? 1.2f : .3f) && lateral <= (link.LaneSwitch ? LaneSwitchLateral : .4f) && ready
            && now >= _nextLaunch
            && MeasuredSpeed is >= 4.8f and <= 8.5f && aligned)
            return true;
        // A missed launch must not turn the flight corridor into walkable floor.
        if (progress >= (link.Stairs ? 1.2f : .05f)) Retry();
        return false;
    }

    public void ConfirmLaunch(DateTime now)
    { _launched = now; _nextLaunch = now.AddSeconds(.7); _airborne = false; if (_link?.Stairs != true) _samples.Clear(); }
    public void RejectLaunch() => Retry();
    private void Retry()
    { _launched = default; _running = false; _samples.Clear(); NeedsRunupRetry = true; }
    public void Clear()
    {
        _link = null; _running = _airborne = _completed = NeedsRunupRetry = false;
        _launched = _lastSample = _nextLaunch = default; MeasuredSpeed = 0; _samples.Clear();
    }

    private void RecordSample(DateTime now, Vector3 player, bool moving)
    {
        if (!moving) { _samples.Clear(); return; }
        _samples.Enqueue((now, player));
        while (_samples.Count > 1 && now - _samples.Peek().Time > TimeSpan.FromSeconds(.16)) _samples.Dequeue();
    }

    public static bool Ready(Vector3 landing, bool hasControl, GreenMoveStatus movement)
        => hasControl && movement.IsRunning && movement.CurrentPoint is { } current && movement.FinalPoint is { } final
            && Near(current.Position, landing) && Near(final.Position, landing);

    private static bool Near(Vector3 a, Vector3 b)
        => Vector2.DistanceSquared(new(a.X, a.Z), new(b.X, b.Z)) <= .35f * .35f;
    private static Vector3 Flat(Vector3 v) => v with { Y = 0 };
    private static Vector3 Direction(BlundervilleJumpCorridor link) => Vector3.Normalize(Flat(link.Landing - link.Takeoff));
}
