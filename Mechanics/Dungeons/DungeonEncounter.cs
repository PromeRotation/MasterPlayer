using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record DungeonCastObservation(uint Action, Vector3 Location, float? Rotation, DateTime StartedAt, DateTime FinishAt, ulong TargetId = 0);
internal sealed record DungeonActor(ulong Id, uint Oid, Vector3 Position, bool Dead = false, bool Targetable = true, bool InCombat = false,
    DungeonCastObservation? Cast = null, ulong TargetId = 0, string Name = "", int PartySlot = -1, string PartyRole = "");
internal sealed record DungeonPendingCast(ulong Source, DungeonCastRule Rule, Vector3 Location, float Rotation, DateTime StartedAt, DateTime FinishAt, ulong TargetId = 0)
{
    public bool EffectSeen { get; set; }
}

internal sealed record DungeonDamagePrediction(ulong TargetId, int PartySlot, string Role, string Name, DateTime Activation, string DamageType);

internal sealed class DungeonEncounter(uint territory, Action<string>? warning = null)
{
    public DungeonDefinition[] Definitions { get; } = ArrDungeonCatalog.Create(territory);
    public DungeonDefinition? Active { get; private set; }
    public ulong PrimaryId { get; private set; }
    public IGeometryShape? Arena { get; private set; }
    public IReadOnlyDictionary<ulong, DungeonPendingCast> Casts => _casts;
    public IReadOnlyDictionary<ulong, DungeonActor> Actors => _actors;
    public IEnumerable<DungeonActor> ActiveRipples => _ripples.Where(r => (r.Value & 0x40000) != 0)
        .Select(r => _actors.GetValueOrDefault(r.Key)).OfType<DungeonActor>().Where(a => !a.Dead);
    public string CoralColor { get; private set; } = "Unknown";
    public IEnumerable<DungeonDamagePrediction> PredictedDamage
    {
        get
        {
            foreach (var cast in _casts.Values.Where(c => c.Rule.Hint != null))
            {
                var target = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId;
                if (_actors.TryGetValue(target, out var actor) && actor.PartySlot >= 0)
                    yield return new(target, actor.PartySlot, actor.PartyRole, actor.Name, cast.FinishAt, "Tankbuster");
            }
        }
    }
    private readonly Dictionary<ulong, DungeonActor> _actors = [];
    private readonly Dictionary<ulong, DungeonPendingCast> _casts = [];
    private readonly Dictionary<ulong, (uint Action, DateTime EndedAt)> _finished = [];
    private readonly Dictionary<(ulong Source, uint Action, uint Sequence), DateTime> _effects = [];
    private readonly Dictionary<ulong, uint> _ripples = [];
    private readonly HashSet<(ulong Source, uint Action)> _missingRotation = [];
    private DateTime _now;

    public void Synchronize(IEnumerable<DungeonActor> actors, Vector3? player, DateTime now, Func<string, bool>? enabled = null)
    {
        _now = now;
        _actors.Clear();
        foreach (var actor in actors) _actors[actor.Id] = actor;
        if (territory == 1036) UpdateCoral();
        if (player == null) { ResetFight(); return; }
        var primary = _actors.GetValueOrDefault(PrimaryId);
        if (Active == null || primary == null || primary.Dead || !primary.InCombat || enabled?.Invoke(Active.Name) == false)
        {
            ResetFight();
            if (player is { } position)
            {
                primary = _actors.Values.Where(a => !a.Dead && a.Targetable && a.InCombat
                    && Definitions.Any(d => d.BossOid == a.Oid && enabled?.Invoke(d.Name) != false))
                    .OrderBy(a => Vector3.DistanceSquared(a.Position, position)).FirstOrDefault();
                if (primary != null)
                {
                    Active = Definitions.First(d => d.BossOid == primary.Oid);
                    PrimaryId = primary.Id;
                    Arena = Active.Arena(primary.Position);
                }
            }
        }
        if (Active == null) return;
        foreach (var actor in _actors.Values)
        {
            if (actor.Dead) { RemoveActor(actor.Id); continue; }
            if (actor.Cast is { } cast)
            {
                StartCast(actor.Id, cast, now, fromPacket: false);
            }
            else
            {
                _missingRotation.RemoveWhere(k => k.Source == actor.Id);
                if (_casts.TryGetValue(actor.Id, out var old) && now >= old.StartedAt.AddSeconds(0.2)
                    && (old.EffectSeen || now >= old.FinishAt || now < old.FinishAt.AddSeconds(-0.5)))
                    Finish(actor.Id, old.Rule.Action);
            }
        }
        foreach (var source in _casts.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) RemoveActor(source);
        foreach (var source in _ripples.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) _ripples.Remove(source);
        Tick(now);
    }

    public void StartCast(ulong source, DungeonCastObservation cast, DateTime now, bool fromPacket = true)
    {
        _now = now;
        if (Active == null || !_actors.TryGetValue(source, out var actor) || actor.Dead || cast.FinishAt.AddSeconds(2) <= now) return;
        if (_finished.TryGetValue(source, out var finished) && finished.Action == cast.Action && cast.StartedAt <= finished.EndedAt) return;
        if (_casts.TryGetValue(source, out var existing))
        {
            var tolerance = existing.Rule.Action == cast.Action ? 0.25 : 0;
            if (cast.StartedAt < existing.StartedAt.AddSeconds(-tolerance)) return;
            if (existing.Rule.Action != cast.Action) Finish(source, existing.Rule.Action);
        }
        var rule = Active.Casts.FirstOrDefault(r => r.Action == cast.Action && r.CasterOid == actor.Oid);
        if (rule == null) return;
        if (!float.IsFinite(cast.Location.X) || !float.IsFinite(cast.Location.Y) || !float.IsFinite(cast.Location.Z))
        {
            warning?.Invoke($"{rule.Name}: 无有效读条落点，跳过预测。");
            return;
        }
        if (rule.Shape is FanTemplate && (cast.Rotation is not { } r || !float.IsFinite(r)))
        {
            if (_missingRotation.Add((source, cast.Action))) warning?.Invoke($"{rule.Name}: 缺少锁定施法朝向，跳过预测。");
            return;
        }
        var rotation = rule.Shape is FanTemplate ? cast.Rotation!.Value : 0;
        var start = cast.StartedAt;
        var finish = cast.FinishAt;
        if (_casts.TryGetValue(source, out var previous) && previous.Rule.Action == cast.Action
            && Math.Abs((previous.FinishAt - finish).TotalSeconds) < 0.75 && Math.Abs((previous.StartedAt - start).TotalSeconds) < 0.25)
        {
            // The scan restores missing casts; only a packet corrects an existing locked snapshot.
            if (!fromPacket || (previous.Location == cast.Location && previous.Rotation == rotation && previous.TargetId == cast.TargetId)) return;
            start = previous.StartedAt;
            finish = previous.FinishAt;
        }
        _casts[source] = new(source, rule, cast.Location, rotation, start, finish, cast.TargetId);
    }

    public void Finish(ulong source, uint action = 0, DateTime? endedAt = null)
    {
        if (_casts.TryGetValue(source, out var cast) && (action == 0 || action == cast.Rule.Action))
        {
            var predictedEnd = cast.FinishAt.AddSeconds(-0.3);
            var time = endedAt ?? (_now < predictedEnd ? _now : predictedEnd);
            if (time < cast.StartedAt) return;
            _casts.Remove(source);
            _finished[source] = (cast.Rule.Action, time);
        }
    }

    public void Effect(ulong source, uint action, uint sequence, DateTime now)
    {
        _now = now;
        if (!_effects.TryAdd((source, action, sequence), now)) return;
        if (_casts.TryGetValue(source, out var cast) && cast.Rule.Action == action) cast.EffectSeen = true;
    }

    public void Animation(ulong source, uint state)
    {
        if (Active?.BossOid == 0x1A1 && _actors.TryGetValue(source, out var actor) && actor.Oid is >= 0x1E8615 and <= 0x1E8618)
            _ripples[source] = state;
    }

    public void RemoveActor(ulong source)
    {
        _casts.Remove(source);
        _finished.Remove(source);
        _ripples.Remove(source);
        _missingRotation.RemoveWhere(k => k.Source == source);
        if (source == PrimaryId) ResetFight();
    }

    public void Tick(DateTime now)
    {
        _now = now;
        foreach (var cast in _casts.Values.Where(c => now >= c.FinishAt.AddSeconds(2)).ToArray()) Finish(cast.Source, cast.Rule.Action);
        foreach (var id in _finished.Where(c => now >= c.Value.EndedAt.AddSeconds(10) || !_actors.ContainsKey(c.Key)).Select(c => c.Key).ToArray()) _finished.Remove(id);
        foreach (var key in _effects.Where(e => now - e.Value > TimeSpan.FromSeconds(10)).Select(e => e.Key).ToArray()) _effects.Remove(key);
    }

    public IEnumerable<string> Hints(Vector3? player)
    {
        if (territory == 1036 && CoralColor != "Resolved") yield return "Correct switch: " + CoralColor;
        foreach (var hint in _casts.Values.Select(c => c.Rule.Hint).OfType<string>().Distinct()) yield return hint;
        foreach (var prediction in PredictedDamage) yield return $"{prediction.DamageType}: {prediction.Role} {prediction.Name}";
        if (ActiveRipples.Any()) yield return "Adds spawning soon!";
        if (Active?.BossOid == 0x4C && _actors.Values.Any(a => a.Oid == 0x7D && !a.Dead)) yield return "Kill the imps!";
        if (player is { } p && _casts.Values.Any(c => c.Rule.Shape?.Resolve(c.Location, c.Rotation, null).Contains(p) == true)) yield return "GTFO from aoe!";
    }

    public void ResetFight()
    {
        Active = null; PrimaryId = 0; Arena = null;
        _casts.Clear(); _finished.Clear(); _effects.Clear(); _ripples.Clear(); _missingRotation.Clear();
    }

    private void UpdateCoral()
    {
        if (CoralColor == "Unknown")
            CoralColor = _actors.Values.FirstOrDefault(a => a.Targetable && a.Oid is 0x1E8554 or 0x1E8A8C or 0x1E8A8D)?.Oid switch
            {
                0x1E8554 => "Blue", 0x1E8A8C => "Red", 0x1E8A8D => "Green", _ => "Unknown"
            };
        else if (CoralColor != "Resolved" && _actors.Values.Any(a => a.Targetable && a.Oid == 0x1E8558)) CoralColor = "Resolved";
    }
}
