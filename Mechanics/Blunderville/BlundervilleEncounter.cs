using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed record BlundervilleCast(uint Action, Vector3 Location, float? Rotation, DateTime StartedAt, DateTime FinishAt, Vector3? SourcePosition = null);
internal sealed record BlundervilleActor(ulong Id, uint Oid, Vector3 Position, bool Dead = false, BlundervilleCast? Cast = null, float Rotation = 0, float HitboxRadius = 0);
internal sealed record BlundervillePreview(string Id, string Group, IGeometryShape Geometry, bool Outline = false, float Progress = 1);
internal sealed record BlundervilleHazard(string Id, IGeometryShape Geometry, DateTime From, DateTime Until, DateTime Telegraph);

internal sealed class BlundervilleEncounter(Action<string> diagnostic)
{
    private sealed record Pending(ulong Source, BlundervilleCast Cast, IGeometryShape Geometry, string Group);
    private sealed record Pulse(ulong Source, BlundervillePreview Preview, DateTime Expires);
    private sealed record Wave(ulong Source, Vector3 Origin, float Rotation, DateTime StartedAt, DateTime Impact, int Consumed = -1);
    private sealed record Route(Vector3 A, Vector3 B, DateTime LastSeen);
    private sealed record LastCast(BlundervilleCast Cast, bool FromPacket);
    private sealed record Motion(Vector3 Position, Vector3 Velocity, DateTime At);
    private readonly Dictionary<ulong, Motion> _ballMotion = [];
    private readonly Dictionary<ulong, BlundervilleHazard> _charges = [];
    private readonly Dictionary<ulong, BlundervilleActor> _actors = [];
    private readonly Dictionary<ulong, Pending> _casts = [];
    private readonly Dictionary<ulong, LastCast> _lastCasts = [];
    private readonly Dictionary<string, Pulse> _pulses = [];
    private readonly Dictionary<ulong, Wave> _waves = [];
    // Each Final wave column re-casts every 5.91–6.04s in six recordings; a
    // wave cast is visible only ~1s before its origin hits. Remember the last
    // cast per column to forecast the next cycles for route planning.
    private sealed record WaveCycle(Vector3 Origin, float Rotation, DateTime LastStart, double CastSeconds);
    private readonly Dictionary<ulong, WaveCycle> _waveCycles = [];
    internal const double WaveCyclePeriod = 5.98;
    private readonly Dictionary<ulong, Route> _routes = [];
    private readonly Dictionary<ulong, Pulse> _beams = [];
    private readonly HashSet<(ulong, uint, uint)> _sequences = [];
    private readonly Queue<(ulong, uint, uint)> _sequenceOrder = [];
    private readonly HashSet<uint> _missingFacing = [];
    private DateTime _lastTime;
    private readonly BlundervilleAnimationTracker _animations = new();
    private readonly BlundervilleAlternatingPulseTracker _alternatingPulses = new();
    private readonly BlundervilleRhythmTracker _rhythms = new();
    private readonly BlundervilleBumperTracker _bumpers = new();
    private readonly BlundervilleChargeTracker _deliveryCharges = new();
    private readonly BlundervilleRampPushers _rampPushers = new();
    public BlundervilleDeliverySchedule DeliverySchedule { get; } = new();
    public BlundervilleRingBeam RingBeam { get; } = new();
    private readonly BlundervilleRingCycles _ringCycles = new();
    private readonly BlundervilleCylinderSchedule _cylinders = new();
    // Cylinders whose learned clocks have both been ready once: from then on
    // they behave exactly as before the schedule existed.
    private readonly HashSet<ulong> _learnedCylinders = [];
    private const double StepSeconds = 0.45;
    private const float StepHeight = 2.21464f;

    public bool BumpersReady(Vector3 player)
    {
        // Both bridges in the entry lane must have confirmed clocks. Other
        // unlearned lanes remain blocked by TraversalHazards instead.
        var bands = _actors.Values.Where(a => a.Oid == 16339 && MathF.Abs(a.Position.X - player.X) < 8
            && (a.Position.Z is > -180 and < -164 || a.Position.Z is > -205 and < -185)).ToArray();
        return bands.Any(a => a.Position.Z is > -205 and < -185)
            && (player.Z <= -181 || bands.Any(a => a.Position.Z is > -180 and < -164))
            && bands.Where(a => player.Z > -181 || a.Position.Z < -185)
                .All(a => Learned(a.Id, _lastTime) ? _bumpers.Ready(a.Id) && _rhythms.Ready(a.Id, 34717, _lastTime) : _cylinders.Ready(a.Id, _lastTime));
    }

    public bool DeliveryReady(Vector3 destination, DateTime now)
    {
        if (DeliverySchedule.Active) return true;
        if (destination.X is > -225 and < -175) return true;
        var sources = _actors.Values.Where(a => a.Oid == 16350 && a.Position.Z is > 245 and < 280
            && (destination.X < -225 ? a.Position.X < -200 : a.Position.X > -200)).ToArray();
        return sources.Length > 0 && sources.All(a => _deliveryCharges.Ready(a.Id, now));
    }

    public void Synchronize(IEnumerable<BlundervilleActor> actors, DateTime now)
    {
        Tick(now);
        var current = actors.Where(a => BlundervilleCatalog.IsActor(a.Oid) && Finite(a.Position)).ToDictionary(a => a.Id);
        foreach (var id in _actors.Keys.Where(id => !current.ContainsKey(id)).ToArray()) Remove(id);
        foreach (var (id, actor) in current)
        {
            if (actor.Dead) { Remove(id); continue; }
            if (_actors.TryGetValue(id, out var old) && old.Oid != actor.Oid) Remove(id);
            if (actor.Oid == BlundervilleCatalog.Ball)
            {
                var velocity = Vector3.Zero;
                if (_ballMotion.TryGetValue(id, out var motion) && now > motion.At && now - motion.At < TimeSpan.FromSeconds(.5))
                {
                    var delta = actor.Position - motion.Position; delta.Y = 0;
                    velocity = delta.LengthSquared() < .0009f && now - motion.At < TimeSpan.FromSeconds(.25)
                        ? motion.Velocity : delta / (float)(now - motion.At).TotalSeconds;
                    if (velocity.LengthSquared() > 225) velocity = Vector3.Zero;
                    if (delta.LengthSquared() < .0009f && now - motion.At < TimeSpan.FromSeconds(.25))
                    { _actors[id] = actor; continue; }
                }
                _ballMotion[id] = new(actor.Position, velocity, now);
            }
            _actors[id] = actor;
            _bumpers.Observe(actor, now);
            _animations.Observe(actor, now);
            _alternatingPulses.Observe(actor, now);
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
        _alternatingPulses.Map(index, state);
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
        if (fromPacket && cast.Action is 34797 or 34798 or 34805) _ringCycles.Cast(cast.Action, pos, cast.StartedAt);
        if (fromPacket && cast.Action == 34717 && _actors.TryGetValue(source, out var cylinder) && cylinder.Oid == 16339)
            _cylinders.Cast(source, pos, cast.StartedAt);
        if (cast.Action == BlundervilleCatalog.WaveStart && IsWaveOrigin(pos, facing ?? 0))
        {
            _waves[source] = new(source, pos, facing!.Value, cast.StartedAt, cast.FinishAt);
            _waveCycles[source] = new(pos, facing.Value, cast.StartedAt, (cast.FinishAt - cast.StartedAt).TotalSeconds);
        }
    }

    public void Effect(ulong source, uint action, uint sequence, Vector3? position, float? rotation, DateTime now, Vector3? destination = null)
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
        DeliverySchedule.Observe(action, pos, now);
        if (action == 34717) _bumpers.Effect(source, pos, now);
        if (rule.Charge)
        {
            ObserveRoute(source, pos, now);
            if (action == 34774) _deliveryCharges.Effect(source, pos, destination, now, rotation);
            if (action == 34774) _rampPushers.Effect(source, pos, destination, now);
            if (destination is { } end && Finite(end))
            {
                var delta = end - pos; delta.Y = 0;
                var length = delta.Length();
                // ARR charge packets precede the visible traversal. Keep the
                // swept body occupied through that animation, including its endpoints.
                // A FirstB ramp step has already pushed with its packet.
                var occupied = action == 34774 && BlundervilleRampPushers.OnRamp(pos, end) ? BlundervilleRampPushers.Occupied : 1.8;
                if (length is > .5f and < 8)
                    _charges[source] = new($"charge/{source}", new RectangleGeometry((pos + end) / 2,
                        MathF.Atan2(delta.X, delta.Z), 3, length + 3), now, now.AddSeconds(occupied), now);
            }
            return;
        }
        // Ball snapshots would leave circles behind a moving obstacle; the object scan draws its current position.
        if (action == 34776 || rule.Shape == null) return;
        if (rule.Shape is not CircleTemplate && (rotation is not { } facing || !float.IsFinite(facing))) return;
        if (rotation is { } animationFacing) _animations.Effect(source, action, pos, animationFacing, now);
        _alternatingPulses.Effect(source, action, pos, now);
        if (_actors.TryGetValue(source, out var rhythmActor)) _rhythms.Effect(rhythmActor, action, pos, rotation ?? 0, now);
        if (action == BlundervilleCatalog.BeamAction)
        {
            RingBeam.Observe(rotation!.Value, now);
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
        _alternatingPulses.Remove(source);
        _rhythms.Remove(source);
        _bumpers.Remove(source);
        _deliveryCharges.Remove(source);
        _ballMotion.Remove(source); _charges.Remove(source); _rampPushers.Remove(source); _cylinders.Remove(source); _learnedCylinders.Remove(source);
        _actors.Remove(source); _casts.Remove(source); _lastCasts.Remove(source); _waves.Remove(source); _routes.Remove(source); _beams.Remove(source);
        _waveCycles.Remove(source);
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
        foreach (var id in _charges.Where(p => now >= p.Value.Until).Select(p => p.Key).ToArray()) _charges.Remove(id);
    }

    public IEnumerable<BlundervillePreview> Previews(DateTime now)
    {
        Tick(now);
        foreach (var hazard in _animations.Hazards(now).Concat(_alternatingPulses.Hazards(now)).Concat(_rhythms.Hazards(now))
            .Concat(_deliveryCharges.Hazards(now)).Concat(_rampPushers.Hazards(now)).Where(h => h.From <= now.AddSeconds(2)))
            yield return new(hazard.Id, BlundervilleCatalog.Devices, hazard.Geometry, now < hazard.From,
                (float)Math.Clamp(1 - (hazard.From - now).TotalSeconds / 2, .25, 1));
        foreach (var cast in _casts.Values)
            yield return new($"cast/{cast.Source}", cast.Group, cast.Geometry);
        foreach (var body in _bumpers.Hazards.Where(h => h.From <= now && h.Until >= now))
            yield return new(body.Id, BlundervilleCatalog.Devices, body.Geometry, true);
        foreach (var pulse in _pulses.Values) yield return pulse.Preview;
        foreach (var charge in _charges.Values) yield return new(charge.Id, BlundervilleCatalog.Routes, charge.Geometry);
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
                    new RectangleGeometry(pos, wave.Rotation, 6, 6), now < impact.AddSeconds(-0.2),
                    (float)Math.Clamp(1 - (impact - now).TotalSeconds / 3, 0.25, 1));
            }
    }

    public void Reset()
    {
        _actors.Clear(); _casts.Clear(); _lastCasts.Clear(); _pulses.Clear(); _waves.Clear(); _routes.Clear(); _beams.Clear();
        _waveCycles.Clear();
        _sequences.Clear(); _sequenceOrder.Clear(); _missingFacing.Clear(); _lastTime = default;
        _animations.Reset();
        _alternatingPulses.Reset();
        _rhythms.Reset();
        _bumpers.Reset(); _cylinders.Reset(); _learnedCylinders.Clear();
        _deliveryCharges.Reset();
        _ballMotion.Clear(); _charges.Clear(); _rampPushers.Reset(); DeliverySchedule.SetStart(null); RingBeam.SetStart(null); _ringCycles.Reset();
    }

    public IEnumerable<BlundervilleHazard> Hazards(DateTime now)
    {
        Tick(now);
        // On the Delivery clock the calibrated schedule is the only source
        // for its devices; the live learners add wider, later windows.
        if (DeliverySchedule.Active)
        {
            foreach (var hazard in DeliverySchedule.Hazards(now)) yield return hazard;
            foreach (var cast in _casts.Values)
                if (cast.Cast.Action is not (BlundervilleCatalog.VisualWave or 34800 or 34716 or 34774 or 34799))
                    yield return new($"cast/{cast.Source}", cast.Geometry, cast.Cast.FinishAt, cast.Cast.FinishAt.AddSeconds(0.2), cast.Cast.StartedAt);
            yield break;
        }
        foreach (var hazard in _animations.Hazards(now)) yield return hazard;
        foreach (var hazard in _alternatingPulses.Hazards(now)) yield return hazard;
        // Until a cylinder's trackers have learned, its cast-anchored schedule
        // replaces their cold body sweep and pulse rhythm.
        foreach (var hazard in _rhythms.Hazards(now)) if (!Scheduled(hazard.Id, "rhythm/", now)) yield return hazard;
        foreach (var hazard in _bumpers.Hazards) if (!Scheduled(hazard.Id, "bumper/", now)) yield return hazard;
        foreach (var hazard in _cylinders.Hazards(now))
            if (Scheduled(hazard.Id, hazard.Id.StartsWith("bumper/", StringComparison.Ordinal) ? "bumper/" : "rhythm/", now)) yield return hazard;
        foreach (var hazard in _deliveryCharges.Hazards(now)) yield return hazard;
        foreach (var hazard in _charges.Values) yield return hazard;
        foreach (var hazard in _rampPushers.Hazards(now)) yield return hazard;
        foreach (var cast in _casts.Values)
            if (cast.Cast.Action != BlundervilleCatalog.VisualWave)
                yield return new($"cast/{cast.Source}", cast.Geometry, cast.Cast.FinishAt.AddSeconds(-RingLead(cast.Cast.Action, cast.Geometry.Position)),
                    cast.Cast.FinishAt.AddSeconds(0.2), cast.Cast.StartedAt);
        foreach (var wave in _waves.Values)
            for (var i = Math.Max(1, wave.Consumed + 1); i <= 4; i++)
            {
                var impact = wave.Impact.AddSeconds(i * StepSeconds);
                if (now > impact.AddSeconds(0.2)) continue;
                var pos = wave.Origin + new Vector3(0, -StepHeight * i, 6 * i);
                yield return new($"wave/{wave.Source}/{i}", new RectangleGeometry(pos, wave.Rotation, 6, 6),
                    impact, impact.AddSeconds(0.2), wave.StartedAt);
            }
        foreach (var actor in _actors.Values.Where(a => a.Oid == BlundervilleCatalog.Ball))
        {
            yield return new($"ball/{actor.Id}", new CircleGeometry(actor.Position, 2), now, now.AddSeconds(0.3), now);
            if (!_ballMotion.TryGetValue(actor.Id, out var motion) || motion.Velocity.LengthSquared() < .1f) continue;
            // Each interval covers the whole swept ball, not just a future center.
            for (var i = 0; i < 4; ++i)
                yield return new($"ball/{actor.Id}/future/{i}", new CircleGeometry(actor.Position + motion.Velocity * (.1f + i * .2f),
                    2 + motion.Velocity.Length() * .1f), now.AddSeconds(i * .2), now.AddSeconds((i + 1) * .2), now);
        }
        foreach (var (source, beam) in _beams)
        {
            var geometry = _actors.TryGetValue(source, out var actor) && actor.Oid == BlundervilleCatalog.BeamActor && float.IsFinite(actor.Rotation)
                ? new RayRectangleGeometry(actor.Position, actor.Rotation, 8, 100) : beam.Preview.Geometry;
            yield return new($"beam/{source}", geometry, now, now.AddSeconds(0.15), now);
        }
        foreach (var hazard in RingBeam.Hazards(now)) yield return hazard;
        // Ring fan and circle cycles beyond the casts in flight (Ring clock only).
        if (RingBeam.Active) foreach (var hazard in _ringCycles.Hazards(now)) yield return hazard;
    }

    public IEnumerable<BlundervilleHazard> TraversalHazards(DateTime now)
    {
        foreach (var hazard in Hazards(now)) yield return hazard;
        foreach (var actor in _actors.Values.Where(a => a.Oid == 16339))
            if (Learned(actor.Id, now) ? !_bumpers.Ready(actor.Id) || !_rhythms.Ready(actor.Id, 34717, now) : !_cylinders.Ready(actor.Id, now))
                yield return _bumpers.UnknownTraversalHazard(actor, now);
        foreach (var hazard in WaveCycleForecasts(now)) yield return hazard;
    }

    // Next cycles of each observed wave column, beyond the cast in flight.
    // Windows widen with the 0.38–0.56s step spread recorded per segment.
    private IEnumerable<BlundervilleHazard> WaveCycleForecasts(DateTime now)
    {
        if (!BlundervilleCatalog.TryGet(BlundervilleCatalog.WaveStart, out var origin) || origin.Shape == null) yield break;
        foreach (var (source, cycle) in _waveCycles)
        {
            if (!_actors.ContainsKey(source) || (now - cycle.LastStart).TotalSeconds > 3 * WaveCyclePeriod) continue;
            for (var k = 1; k <= 2; ++k)
            {
                var impact = cycle.LastStart.AddSeconds(k * WaveCyclePeriod + cycle.CastSeconds);
                if (now > impact.AddSeconds(4 * StepSeconds + .5)) continue;
                yield return new($"wave/cycle/{source}/{k}/0", origin.Shape.Resolve(cycle.Origin, cycle.Rotation, null),
                    impact.AddSeconds(-.3), impact.AddSeconds(.3), impact.AddSeconds(-cycle.CastSeconds));
                for (var i = 1; i <= 4; ++i)
                {
                    var step = impact.AddSeconds(i * StepSeconds);
                    var spread = .1 * i;
                    yield return new($"wave/cycle/{source}/{k}/{i}", new RectangleGeometry(cycle.Origin + new Vector3(0, -StepHeight * i, 6 * i), cycle.Rotation, 6, 6),
                        step.AddSeconds(-.2 - spread), step.AddSeconds(.3 + spread), impact.AddSeconds(-cycle.CastSeconds));
                }
            }
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

    // Ring 2026-10-13 11:19: MP entered the NE inner circle 1.9s before it
    // resolved, was blocked ahead by two charges and waited inside it; the hit
    // knocked it off the ring. The inner circle covers the line MP runs and its
    // edge is ~4m (0.7s) away, so it counts from 0.75s earlier; the outer half
    // stays open meanwhile. Longer leads (or all circles) block whole corners
    // until the beam arrives (22 replays: 1.0s inner 15/22 clean, 0.75s 17/22).
    internal const double InnerCircleLead = .75;
    internal static double RingLead(uint action, Vector3 source)
        => action == 34805 && BlundervilleRingCycles.Inner(source) ? InnerCircleLead : 0;

    // Whether the schedule supplies this cylinder's body sweep ("bumper/") or
    // endpoint pulse ("rhythm/.../34717/") instead of the learned trackers.
    private bool Scheduled(string id, string prefix, DateTime now)
    {
        if (!id.StartsWith(prefix, StringComparison.Ordinal) || prefix == "rhythm/" && !id.Contains("/34717/", StringComparison.Ordinal)) return false;
        var end = id.IndexOf('/', prefix.Length);
        return end > prefix.Length && ulong.TryParse(id.AsSpan(prefix.Length, end - prefix.Length), out var source)
            && !Learned(source, now) && _cylinders.Ready(source, now);
    }

    // The bumper planner's ice search reacts to the slightest input change:
    // swapping equivalent clocks under a learned lane changed which slides it
    // found (04:45/17:44 replays), so the schedule only covers the cold start.
    private bool Learned(ulong id, DateTime now)
    {
        if (_learnedCylinders.Contains(id)) return true;
        if (!_bumpers.Ready(id) || !_rhythms.Ready(id, 34717, now)) return false;
        _learnedCylinders.Add(id);
        return true;
    }

    private static float DistanceXZ(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
    internal static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
    private static bool IsWaveOrigin(Vector3 p, float r) => MathF.Abs(p.Z - 190.4f) < 0.2f && MathF.Abs(p.Y - 25.59f) < 0.5f
        && MathF.Abs(p.X) <= 12.1f && MathF.Abs(p.X / 6 - MathF.Round(p.X / 6)) < 0.02f && MathF.Abs(r) < 0.02f;
}
