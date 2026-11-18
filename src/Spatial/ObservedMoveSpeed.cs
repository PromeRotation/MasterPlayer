using System.Numerics;

namespace MasterPlayer.Spatial;

/// <summary>
/// Measures the local player's steady running speed from frame positions.
/// <para>The solver models speed as 6 y/s × PR's speed multiplier, which cannot see
/// game-side slows. 2026-10-29 06:37 High2 ARR: a run-long status cut the player
/// to ~4.9 y/s from ~450s on; uptime waits and escapes timed for 6 y/s left too
/// late for the Gigantis R15 and stopped inside it.</para>
/// <para>2026-10-30 10:00 Low1 ARR: half-second creeps (arrival, short nudges) at
/// 2.2–3.8 y/s were taken as a slow while the player really ran 6–9 y/s; the
/// planner then saw no escape (NoTemporalPath) and stood in 46916. A slow now has
/// to hold for every steady run in the last 15s: the fastest one is the speed.</para>
/// </summary>
internal sealed class ObservedMoveSpeed
{
    // A steady window: long enough to skip start/stop ramps (~0.15s in the ARR) and
    // short nudges, short enough to measure a typical dodge.
    private const double WindowSeconds = .8;
    private const float MaxHalfDifference = .08f;
    private const float MinSpeed = 1.5f;
    private const double MemorySeconds = 15;
    private const double FasterMemorySeconds = 8;

    private readonly List<(DateTime Time, Vector3 Position)> _samples = [];
    private readonly List<(DateTime Time, float Speed)> _runs = [];
    private float _modelAtObservation;
    private float? _confirmedFasterSpeed;
    private DateTime _lastFasterRun;

    internal float? Observed => _runs.Count == 0 ? null : _runs.Max(r => r.Speed);

    internal void Reset()
    {
        _samples.Clear();
        _runs.Clear();
        _confirmedFasterSpeed = null;
    }

    internal void Record(DateTime now, Vector3 position, float modelSpeed, bool allowFaster = false)
    {
        if (MathF.Abs(modelSpeed - _modelAtObservation) > .01f)
        {
            Reset();
            _modelAtObservation = modelSpeed;
        }
        if (!allowFaster || (now - _lastFasterRun).TotalSeconds > FasterMemorySeconds)
            _confirmedFasterSpeed = null;
        _runs.RemoveAll(r => (now - r.Time).TotalSeconds > MemorySeconds);
        if (_samples.Count > 0)
        {
            var (lastTime, lastPosition) = _samples[^1];
            if (now <= lastTime) return;
            // Standing still, a frame gap or a teleport/knockback breaks the window.
            var step = DistanceXZ(lastPosition, position);
            var dt = (now - lastTime).TotalSeconds;
            if (step < .0001f || dt > .25 || step / dt > modelSpeed * 2.5f)
                _samples.Clear();
        }
        _samples.Add((now, position));
        while (_samples.Count > 2 && (now - _samples[1].Time).TotalSeconds >= WindowSeconds)
            _samples.RemoveAt(0);
        if ((now - _samples[0].Time).TotalSeconds < WindowSeconds) return;

        var mid = _samples.Count / 2;
        var first = PathSpeed(0, mid);
        var second = PathSpeed(mid, _samples.Count - 1);
        if (first < MinSpeed || second < MinSpeed) return;
        var steady = MathF.Abs(first - second) <= MaxHalfDifference * MathF.Max(first, second);
        // Both halves must confirm the higher speed. A gradual increase may
        // inform the next solve, using the lower measured half as its bound.
        if (allowFaster && first > modelSpeed * 1.03f && second > modelSpeed * 1.03f
            && (steady || second > first && second <= first * 1.35f))
        {
            _confirmedFasterSpeed = MathF.Max(modelSpeed, (steady ? PathSpeed(0, _samples.Count - 1) : first) * .97f);
            _lastFasterRun = now;
        }
        else if (allowFaster && steady && MathF.Max(first, second) <= modelSpeed * 1.03f)
            _confirmedFasterSpeed = null;

        if (!steady) return;
        var speed = PathSpeed(0, _samples.Count - 1);
        // Below half speed it is a deliberate creep, not a run under a slow.
        if (speed < modelSpeed * .5f) return;
        // Sprint or haste: a full-speed run, which rules out any slow it overlaps.
        _runs.Add((now, MathF.Min(speed, modelSpeed)));
    }

    internal float Effective(float modelSpeed, bool allowFaster = false)
    {
        if (MathF.Abs(modelSpeed - _modelAtObservation) > .01f) return modelSpeed;
        if (allowFaster && _confirmedFasterSpeed is { } faster) return faster;
        return Observed is { } observed && observed < modelSpeed * .97f ? observed : modelSpeed;
    }

    private float PathSpeed(int from, int to)
    {
        if (to <= from) return 0;
        var length = 0f;
        for (var i = from; i < to; i++)
            length += DistanceXZ(_samples[i].Position, _samples[i + 1].Position);
        var seconds = (_samples[to].Time - _samples[from].Time).TotalSeconds;
        return seconds > 0 ? (float)(length / seconds) : 0;
    }

    private static float DistanceXZ(Vector3 a, Vector3 b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
}
