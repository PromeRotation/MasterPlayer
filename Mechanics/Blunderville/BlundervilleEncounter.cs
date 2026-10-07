using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed record BlundervilleCast(uint Action, Vector3 Location, float? Rotation, DateTime StartedAt, DateTime FinishAt, Vector3? SourcePosition = null);
internal sealed record BlundervilleActor(ulong Id, uint Oid, Vector3 Position, bool Dead = false, BlundervilleCast? Cast = null, float Rotation = 0);
internal sealed record BlundervillePreview(string Id, string Group, IGeometryShape Geometry, bool Outline = false, float Progress = 1);
internal sealed record BlundervilleHazard(string Id, IGeometryShape Geometry, DateTime From, DateTime Until, DateTime Telegraph);

internal sealed class BlundervilleEncounter(Action<string> diagnostic)
{
    private sealed record Pending(ulong Source, BlundervilleCast Cast, IGeometryShape Geometry, string Group);
    private sealed record Pulse(ulong Source, BlundervillePreview Preview, DateTime Expires);
    private sealed record Wave(ulong Source, Vector3 Origin, float Rotation, DateTime StartedAt, DateTime Impact, int Consumed = -1);
    private sealed record Route(Vector3 A, Vector3 B, DateTime LastSeen);
    private sealed record LastCast(BlundervilleCast Cast, bool FromPacket);
    private readonly Dictionary<ulong, BlundervilleActor> _actors = [];
    private readonly Dictionary<ulong, Pending> _casts = [];
    private readonly Dictionary<ulong, LastCast> _lastCasts = [];
    private readonly Dictionary<string, Pulse> _pulses = [];
    private readonly Dictionary<ulong, Wave> _waves = [];
    private readonly Dictionary<ulong, Route> _routes = [];
    private readonly Dictionary<ulong, Pulse> _beams = [];
    private readonly HashSet<(ulong, uint, uint)> _sequences = [];
    private readonly Queue<(ulong, uint, uint)> _sequenceOrder = [];
    private readonly HashSet<uint> _missingFacing = [];
    private DateTime _lastTime;
    private readonly BlundervilleAnimationTracker _animations = new();
    private const double StepSeconds = 0.45;
    private const float StepHeight = 2.21464f;

    public void Synchronize(IEnumerable<BlundervilleActor> actors, DateTime now)
    {
        Tick(now);
        var current = actors.Where(a => BlundervilleCatalog.IsActor(a.Oid) && Finite(a.Position)).ToDictionary(a => a.Id);
        foreach (var id in _actors.Keys.Where(id => !current.ContainsKey(id)).ToArray()) Remove(id);
        foreach (var (id, actor) in current)
        {
            if (actor.Dead) { Remove(id); continue; }
            if (_actors.TryGetValue(id, out var old) && old.Oid != actor.Oid) Remove(id);
            _actors[id] = actor;
            _animations.Observe(actor, now);
            if (actor.Cast is { } cast) StartCast(id, cast, now, fromPacket: false);
            // Normal completion keeps the preview for its effect; disappearance well before finish means cancellation.
            else if (_casts.TryGetValue(id, out var pending) && now < pending.Cast.FinishAt.AddSeconds(-0.35))
                Cancel(id, pending.Cast.Action);
        }
    }

    public void Map(byte index, uint state, DateTime now)
    {
        Tick(now);
        _animations.Map(index, state, now);
    }

    public void StartCast(ulong source, BlundervilleCast cast, DateTime now, bool fromPacket = true)
    {
        Tick(now);
        if (!BlundervilleCatalog.TryGet(cast.Action, out var rule) || (rule.Shape == null && !rule.Charge) || !Finite(cast.Location)
            || cast.FinishAt < cast.StartedAt || cast.FinishAt - cast.StartedAt > TimeSpan.FromHours(1) || now > cast.FinishAt.AddSeconds(0.6)) return;
        if (_lastCasts.TryGetValue(source, out var last) && last.Cast.Action == cast.Action)
        {
            var tolerance = fromPacket && (last.FromPacket || !_casts.ContainsKey(source)) ? 0.01 : 0.5;
            if (Math.Abs((last.Cast.StartedAt - cast.StartedAt).TotalSeconds) < tolerance)
            {
                if (!_casts.ContainsKey(source) || !fromPacket) return;
                // Packet correction can replace a scanned snapshot; scanning never moves a locked cast.
            }
            else _waves.Remove(source);
        }
        else _waves.Remove(source);
        var origin = cast.SourcePosition ?? (_actors.TryGetValue(source, out var actor) ? actor.Position : (Vector3?)null);
        if (origin is not { } pos || !Finite(pos)) return;
        var facing = cast.Rotation is { } r && float.IsFinite(r) ? r : (float?)null;
        IGeometryShape geometry;
        if (rule.Charge)
        {
            var delta = cast.Location - pos;
            var length = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            if (!float.IsFinite(length) || length <= 0.5f) return;
            geometry = new RayRectangleGeometry(pos, MathF.Atan2(delta.X, delta.Z), 3, length);
        }
        else if (rule.Shape is not CircleTemplate && facing == null)
        {
            if (_missingFacing.Add(cast.Action)) diagnostic($"金碟巨豆中心 AID {cast.Action} 缺少锁定读条朝向，跳过矩形预警。");
            return;
        }
        else geometry = rule.Shape!.Resolve(pos, facing ?? 0, null);
        _lastCasts[source] = new(cast, fromPacket);
        _casts[source] = new(source, cast, geometry, rule.Group);
        if (cast.Action == BlundervilleCatalog.WaveStart && IsWaveOrigin(pos, facing ?? 0))
            _waves[source] = new(source, pos, facing!.Value, cast.StartedAt, cast.FinishAt);
    }

    public void Effect(ulong source, uint action, uint sequence, Vector3? position, float? rotation, DateTime now)
    {
        Tick(now);
        if (!BlundervilleCatalog.TryGet(action, out var rule)) return;
        if (sequence != 0)
        {
            var key = (source, action, sequence);
            if (!_sequences.Add(key)) return;
            _sequenceOrder.Enqueue(key);
            while (_sequenceOrder.Count > 512) _sequences.Remove(_sequenceOrder.Dequeue());
        }
        var cast = _casts.TryGetValue(source, out var pending) && pending.Cast.Action == action ? pending : null;
        if (cast != null) _casts.Remove(source);
        // These fans have 0/PI actor/effect facing but +/-PI/2 cast facing in the ARR.
        if (rule.Shape is FanTemplate)
        {
            if (cast != null)
            {
                var fanId = $"hit/{source}/{action}";
                _pulses[fanId] = new(source, new(fanId, rule.Group, cast.Geometry), now.AddSeconds(0.55));
            }
            return;
        }
        if (position is not { } pos || !Finite(pos)) return;
        if (rule.Charge) { ObserveRoute(source, pos, now); return; }
        // Ball snapshots would leave circles behind a moving obstacle; the object scan draws its current position.
        if (action == 34776 || rule.Shape == null) return;
        if (rule.Shape is not CircleTemplate && (rotation is not { } facing || !float.IsFinite(facing))) return;
        if (rotation is { } animationFacing) _animations.Effect(source, action, pos, animationFacing, now);
        if (action == BlundervilleCatalog.BeamAction)
        {
            var beamId = $"beam/{source}";
            _beams[source] = new(source, new(beamId, rule.Group, rule.Shape.Resolve(pos, rotation!.Value, null)), now.AddSeconds(1.3));
            return;
        }
        if (_waves.TryGetValue(source, out var wave))
        {
            var stage = action == BlundervilleCatalog.WaveStart ? 0 : action == BlundervilleCatalog.WaveStep
                ? (int)MathF.Round((pos.Z - wave.Origin.Z) / 6) : -1;
            if (stage >= 0 && stage <= 4 && MathF.Abs(pos.X - wave.Origin.X) < 0.2f
                && MathF.Abs(pos.Z - wave.Origin.Z - stage * 6) < 0.2f && stage > wave.Consumed)
            {
                _waves[source] = wave with { Impact = now.AddSeconds(-stage * StepSeconds), Consumed = stage };
                if (stage == 4) _waves.Remove(source);
            }
        }
        var id = FormattableString.Invariant($"hit/{source}/{action}/{pos.X:F1}/{pos.Z:F1}");
        _pulses[id] = new(source, new(id, rule.Group, rule.Shape.Resolve(pos, rotation ?? 0, null)), now.AddSeconds(0.55));
    }

    public void Cancel(ulong source, uint action, DateTime? occurredAt = null)
    {
        if (_casts.TryGetValue(source, out var cast) && (action == 0 || cast.Cast.Action == action)
            && (occurredAt == null || occurredAt >= cast.Cast.StartedAt))
        {
            _casts.Remove(source);
            _waves.Remove(source);
        }
    }

    public void Remove(ulong source)
    {
        _animations.Remove(source);
        _actors.Remove(source); _casts.Remove(source); _lastCasts.Remove(source); _waves.Remove(source); _routes.Remove(source); _beams.Remove(source);
        foreach (var id in _pulses.Where(p => p.Value.Source == source).Select(p => p.Key).ToArray()) _pulses.Remove(id);
    }

    public void Tick(DateTime now)
    {
        if (now < _lastTime) Reset();
        _lastTime = now;
        foreach (var id in _casts.Where(p => now > p.Value.Cast.FinishAt.AddSeconds(0.6)).Select(p => p.Key).ToArray()) _casts.Remove(id);
        foreach (var id in _pulses.Where(p => now >= p.Value.Expires).Select(p => p.Key).ToArray()) _pulses.Remove(id);
        foreach (var id in _waves.Where(p => now > p.Value.Impact.AddSeconds(4 * StepSeconds + 0.55)).Select(p => p.Key).ToArray()) _waves.Remove(id);
        foreach (var id in _routes.Where(p => now > p.Value.LastSeen.AddSeconds(8)).Select(p => p.Key).ToArray()) _routes.Remove(id);
        foreach (var id in _beams.Where(p => now >= p.Value.Expires).Select(p => p.Key).ToArray()) _beams.Remove(id);
    }

    public IEnumerable<BlundervillePreview> Previews(DateTime now)
    {
        Tick(now);
        foreach (var hazard in _animations.Hazards(now).Where(h => h.From <= now.AddSeconds(2)))
            yield return new(hazard.Id, BlundervilleCatalog.Devices, hazard.Geometry, now < hazard.From,
                (float)Math.Clamp(1 - (hazard.From - now).TotalSeconds / 2, .25, 1));
        foreach (var cast in _casts.Values)
            yield return new($"cast/{cast.Source}", cast.Group, cast.Geometry);
        foreach (var pulse in _pulses.Values) yield return pulse.Preview;
        foreach (var actor in _actors.Values.Where(a => a.Oid == BlundervilleCatalog.Ball))
            yield return new($"ball/{actor.Id}", BlundervilleCatalog.Balls, new CircleGeometry(actor.Position, 2));
        foreach (var (source, beam) in _beams)
            yield return _actors.TryGetValue(source, out var actor) && actor.Oid == BlundervilleCatalog.BeamActor && float.IsFinite(actor.Rotation)
                ? beam.Preview with { Geometry = new RayRectangleGeometry(actor.Position, actor.Rotation, 8, 100) } : beam.Preview;
        foreach (var (source, route) in _routes)
        {
            var delta = route.B - route.A;
            var length = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            if (length > 0.5f)
                yield return new($"route/{source}", BlundervilleCatalog.Routes,
                    new RayRectangleGeometry(route.A, MathF.Atan2(delta.X, delta.Z), 3, length), true);
        }
        foreach (var wave in _waves.Values)
            for (var i = Math.Max(1, wave.Consumed + 1); i <= 4; i++)
            {
                var impact = wave.Impact.AddSeconds(i * StepSeconds);
                if (now > impact.AddSeconds(0.25)) continue;
                var pos = wave.Origin + new Vector3(0, -StepHeight * i, 6 * i);
                yield return new($"wave/{wave.Source}/{i}", BlundervilleCatalog.Waves,
                    new RayRectangleGeometry(pos, wave.Rotation, 6, 6), now < impact.AddSeconds(-0.2),
                    (float)Math.Clamp(1 - (impact - now).TotalSeconds / 3, 0.25, 1));
            }
    }

    public void Reset()
    {
        _actors.Clear(); _casts.Clear(); _lastCasts.Clear(); _pulses.Clear(); _waves.Clear(); _routes.Clear(); _beams.Clear();
        _sequences.Clear(); _sequenceOrder.Clear(); _missingFacing.Clear(); _lastTime = default;
        _animations.Reset();
    }

    public IEnumerable<BlundervilleHazard> Hazards(DateTime now)
    {
        Tick(now);
        foreach (var hazard in _animations.Hazards(now)) yield return hazard;
        foreach (var cast in _casts.Values)
            if (cast.Cast.Action != BlundervilleCatalog.VisualWave)
                yield return new($"cast/{cast.Source}", cast.Geometry, cast.Cast.FinishAt,
                    cast.Cast.FinishAt.AddSeconds(0.2), cast.Cast.StartedAt);
        foreach (var wave in _waves.Values)
            for (var i = Math.Max(1, wave.Consumed + 1); i <= 4; i++)
            {
                var impact = wave.Impact.AddSeconds(i * StepSeconds);
                if (now > impact.AddSeconds(0.2)) continue;
                var pos = wave.Origin + new Vector3(0, -StepHeight * i, 6 * i);
                yield return new($"wave/{wave.Source}/{i}", new RayRectangleGeometry(pos, wave.Rotation, 6, 6),
                    impact, impact.AddSeconds(0.2), wave.StartedAt);
            }
        foreach (var actor in _actors.Values.Where(a => a.Oid == BlundervilleCatalog.Ball))
            yield return new($"ball/{actor.Id}", new CircleGeometry(actor.Position, 2), now, now.AddSeconds(0.3), now);
        foreach (var (source, beam) in _beams)
        {
            var geometry = _actors.TryGetValue(source, out var actor) && actor.Oid == BlundervilleCatalog.BeamActor && float.IsFinite(actor.Rotation)
                ? new RayRectangleGeometry(actor.Position, actor.Rotation, 8, 100) : beam.Preview.Geometry;
            yield return new($"beam/{source}", geometry, now, now.AddSeconds(0.15), now);
        }
    }

    private void ObserveRoute(ulong source, Vector3 position, DateTime now)
    {
        if (!_routes.TryGetValue(source, out var route)) { _routes[source] = new(position, position, now); return; }
        var ab = DistanceXZ(route.A, route.B);
        var ap = DistanceXZ(route.A, position); var bp = DistanceXZ(route.B, position);
        // Ignore unrelated teleports instead of stretching a route across courses.
        if (Math.Max(ap, bp) > 40) { _routes[source] = new(position, position, now); return; }
        _routes[source] = ap > ab && ap >= bp ? new(route.A, position, now) : bp > ab ? new(position, route.B, now) : route with { LastSeen = now };
    }

    private static float DistanceXZ(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
    internal static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
    private static bool IsWaveOrigin(Vector3 p, float r) => MathF.Abs(p.Z - 190.4f) < 0.2f && MathF.Abs(p.Y - 25.59f) < 0.5f
        && MathF.Abs(p.X) <= 12.1f && MathF.Abs(p.X / 6 - MathF.Round(p.X / 6)) < 0.02f && MathF.Abs(r) < 0.02f;
}
