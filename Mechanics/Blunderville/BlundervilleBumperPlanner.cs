using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// Search to the next wide platform, waiting only outside the cylinder lane.
// A short safe step alone can lead into a pocket with no safe onward window.
internal sealed class BlundervilleBumperPlanner(Func<Func<BlundervilleBumperPlanner.Step[]>, Task<BlundervilleBumperPlanner.Step[]>>? schedule = null, double publicationDelay = .2)
{
    internal sealed record Step(Vector3 Position, double Wait);
    private sealed record Node(Vector3 Position, double Time, double WaitBefore, Node? Parent);
    private readonly List<Step> _path = [];
    private DateTime _retry, _requestedAt, _waitUntil;
    private Task<Step[]>? _pending;
    private Vector3 _requestedPosition;

    public void Reset() { _path.Clear(); _retry = _waitUntil = default; _pending = null; }

    public BlundervilleRaceDecision Apply(BlundervilleRaceDecision normal, BlundervilleRaceState state,
        BlundervilleEncounter encounter, Vector3 player, DateTime now)
    {
        if (!state.IsRacing || state.Qualified || state.Course != BlundervilleCourse.FirstA || player.Y < 16
            || player.Z is > -158 or < -207 || !state.CanMove(now))
        { Reset(); return normal; }
        if (player.Z is < -181 and > -185 && player.X < -209.2f)
        {
            Reset();
            return new(new(-207, player.Y, -184), false, false, null, "圆圈机关：从宽平台进入湿滑通道", true);
        }
        var hazards = encounter.Hazards(now).Where(h => Radar.RadarLocalGeometry.Intersects(h.Geometry.GetBounds(), player, 30)).ToArray();
        var floor = BlundervilleRoutes.Boundary(state.Course);
        var speed = state.NavigationBaseSpeed(now);
        if (_pending is { IsCompleted: true } completed && (now - _requestedAt).TotalSeconds + .00001 >= publicationDelay)
        {
            _pending = null;
            if (completed.IsCompletedSuccessfully && now >= _requestedAt && now - _requestedAt < TimeSpan.FromSeconds(.5)
                && Distance(player, _requestedPosition) < .25f) _path.AddRange(completed.Result);
        }
        if (_pending != null && (now < _requestedAt || now - _requestedAt > TimeSpan.FromSeconds(.5))) _pending = null;
        while (_path.Count > 0 && (Distance(player, _path[0].Position) < .06f
            || _path[0].Wait == 0 && player.Z < _path[0].Position.Z - .1f))
        {
            if (_path[0].Wait > 0)
            {
                if (_waitUntil == default) _waitUntil = now.AddSeconds(_path[0].Wait);
                if (now < _waitUntil) break;
            }
            _path.RemoveAt(0); _waitUntil = default;
        }
        if (_path.Count > 0)
        {
            var from = player; var time = 0.0; var valid = true;
            for (var i = 0; i < _path.Count; ++i)
            {
                var step = _path[i]; var arrival = time + Distance(from, step.Position) / speed;
                var wait = i == 0 && _waitUntil != default ? Math.Max(0, (_waitUntil - now).TotalSeconds) : step.Wait;
                if (!Safe(floor, hazards, now, from, step.Position, time, arrival, wait + (i == _path.Count - 1 ? .4 : 0), 0))
                { valid = false; break; }
                from = step.Position; time = arrival + wait + (wait > 0 ? .1 : 0);
            }
            if (valid)
            {
                if (_waitUntil > now) return new(player, true, false, null, "圆圈机关：在安全间隙等待下一拍", true);
                var run = _path.TakeWhile(s => s.Wait == 0).Select(s => s.Position).ToList();
                if (_path.FirstOrDefault(s => s.Wait > 0) is { } stop) run.Add(stop.Position);
                return new(run[^1], false, false, null, "圆圈机关：沿动画通路连续前进", true) { ContinuousPath = run.ToArray() };
            }
            _path.Clear(); _waitUntil = default;
        }
        if (_pending == null && now >= _retry && encounter.BumpersReady(player))
        {
            _retry = now.AddSeconds(.15); _requestedAt = now; _requestedPosition = player;
            // Captured geometry is immutable. No native state, registrations or
            // movement-controller access is permitted on this worker.
            var departure = now.AddSeconds(publicationDelay);
            _pending = (schedule ?? (work => Task.Run(work)))(() => Search(floor, hazards, player, speed, departure));
        }
        return new(player, true, false, null, "圆圈机关：等待通路开放", true);
    }

    private static Step[] Search(IGeometryShape floor, BlundervilleHazard[] hazards, Vector3 player, float speed, DateTime now)
    {
        var ground = new Dictionary<(Vector3 From, Vector3 To), bool>();
        var obstacles = hazards.Select(h => new Obstacle(h.Geometry, h.Geometry.GetBounds(),
            (h.From - now).TotalSeconds - (h.Id.StartsWith("bumper/", StringComparison.Ordinal) ? .05 : .25),
            (h.Until - now).TotalSeconds + (h.Id.StartsWith("bumper/", StringComparison.Ordinal) ? .05 : .35))).ToArray();
        bool CanGo(Vector3 from, Vector3 to, double start, double end, double settle)
        {
            if (!ground.TryGetValue((from, to), out var traversable)) ground[(from, to)] = traversable = OnFloor(floor, from, to);
            return traversable && Avoids(obstacles, from, to, start, end, settle, .15);
        }
        var first = player.Z > -181; var finish = first ? -182.5f : -207.5f; var center = first ? -211f : -206f;
        List<Node> layer = [new(player, 0, 0, null)];
        for (var z = player.Z - 2; layer.Count != 0; z -= 2)
        {
            z = MathF.Max(finish, z);
            var next = new Dictionary<(int X, int Time), Node>();
            foreach (var previous in layer)
                for (var wait = 0.0; wait <= (previous.Parent == null && player.Z > (first ? -163 : -185) ? 3 : 0); wait += .25)
                {
                    if (wait > 0 && !CanGo(previous.Position, previous.Position, previous.Time, previous.Time + wait + .1, 0)) continue;
                    for (var x = -5; x <= 5; ++x)
                    {
                        var p = new Vector3(center + x * .5f, player.Y, z);
                        var start = previous.Time + wait + (wait > 0 ? .1 : 0); var time = start + Distance(previous.Position, p) / speed;
                        if (time > 10.7 || !CanGo(previous.Position, p, start, time, z == finish ? .5 : 0)) continue;
                        var key = (x, (int)(time / .16));
                        if (!next.TryGetValue(key, out var old) || old.Time > time) next[key] = new(p, time, wait, previous);
                    }
                }
            layer = next.Values.OrderBy(n => n.Time).Take(100).ToList();
            if (z != finish) continue;
            if (layer.FirstOrDefault() is not { } last) break;
            var nodes = new List<Node>();
            for (var n = last; n.Parent != null; n = n.Parent) nodes.Add(n);
            nodes.Reverse(); var path = new List<Step>();
            foreach (var n in nodes)
            {
                if (n.WaitBefore > 0)
                {
                    if (path.Count == 0) path.Add(new(player, n.WaitBefore));
                    else path[^1] = path[^1] with { Wait = n.WaitBefore };
                }
                path.Add(new(n.Position, 0));
            }
            for (var i = path.Count - 2; i > 0; --i)
                if (path[i].Wait == 0 && Vector3.Dot(Vector3.Normalize(path[i].Position - path[i - 1].Position), Vector3.Normalize(path[i + 1].Position - path[i].Position)) > .9999f)
                    path.RemoveAt(i);
            return path.ToArray();
        }
        return [];
    }

    private static bool Safe(IGeometryShape floor, BlundervilleHazard[] hazards, DateTime now, Vector3 from, Vector3 to, double start, double end, double settle, double delay)
    {
        return OnFloor(floor, from, to) && Avoids(hazards.Select(h => new Obstacle(h.Geometry, h.Geometry.GetBounds(),
            (h.From - now).TotalSeconds - (h.Id.StartsWith("bumper/", StringComparison.Ordinal) ? .05 : .2),
            (h.Until - now).TotalSeconds + (h.Id.StartsWith("bumper/", StringComparison.Ordinal) ? .05 : .35))), from, to, start, end, settle, delay);
    }

    private static bool OnFloor(IGeometryShape floor, Vector3 from, Vector3 to)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(Distance(from, to) / .2f));
        for (var i = 0; i <= steps; ++i)
        {
            var p = Vector3.Lerp(from, to, (float)i / steps);
            if (!floor.Contains(p + Vector3.UnitX * .2f) || !floor.Contains(p - Vector3.UnitX * .2f)) return false;
        }
        return true;
    }

    private readonly record struct Obstacle(IGeometryShape Geometry, GeometryBounds Bounds, double From, double Until);
    private static bool Avoids(IEnumerable<Obstacle> hazards, Vector3 from, Vector3 to, double start, double end, double settle, double delay)
    {
        var minX = MathF.Min(from.X, to.X) - .1f; var maxX = MathF.Max(from.X, to.X) + .1f;
        var minZ = MathF.Min(from.Z, to.Z) - .1f; var maxZ = MathF.Max(from.Z, to.Z) + .1f;
        foreach (var h in hazards)
        {
            if (h.Bounds.MinX > maxX || h.Bounds.MaxX < minX || h.Bounds.MinZ > maxZ || h.Bounds.MaxZ < minZ) continue;
            var activeFrom = h.From;
            var activeUntil = h.Until;
            if (activeUntil < start || activeFrom > end + delay + settle) continue;
            var duration = Math.Max(.001, end - start);
            var a = Vector3.Lerp(from, to, (float)Math.Clamp((activeFrom - delay - start) / duration, 0, 1));
            var b = Vector3.Lerp(from, to, (float)Math.Clamp((activeUntil - start) / duration, 0, 1));
            if (h.Geometry.Contains(a, .1f) || h.Geometry.Contains(b, .1f) || h.Geometry.IntersectsSegment(a, b, .1f)) return false;
        }
        return true;
    }
    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
}
