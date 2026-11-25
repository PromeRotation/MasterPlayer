using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntHeshualaState(ulong owner)
{
    private static readonly GeometryTemplate cone = new FanTemplate(50, MathF.PI / 2);
    private static readonly GeometryTemplate crossShape = new LocalPolygonTemplate([
        new(-5, -50), new(5, -50), new(5, -5), new(50, -5), new(50, 5), new(5, 5),
        new(5, 50), new(-5, 50), new(-5, 5), new(-50, 5), new(-50, -5), new(-5, -5)]);
    private static readonly uint[] watchedStatuses = [3977, 3978, 3979];
    private readonly List<(HuntPendingCast Cast, int Remaining, float Increment)> sequences = [];
    private readonly List<HuntPendingCast> aoes = [];
    private readonly Dictionary<(ulong Actor, uint Status), ReplayStatus> statuses = [];
    private readonly Dictionary<(ulong Actor, uint Status), ReplayStatus?> pending = [];
    private readonly Dictionary<(ulong Actor, uint Status), DateTime> watermarks = [];
    private HuntPendingCast? cross;
    private bool cardinal;
    private int version;
    private int lastVersion = -1;
    private int lastCount = -1;
    internal int Spins { get; private set; }
    internal IEnumerable<HuntPendingCast> Areas => aoes.Concat(cross != null && Spins < 3 ? [cross] : [])
        .Select(cast => cast with { Source = owner });

    internal void Status(ulong actor, uint id, ReplayStatus? value, DateTime at)
    {
        if (id is not (3977 or 3978 or 3979)) return;
        var key = (actor, id);
        if (watermarks.TryGetValue(key, out var previous) && at < previous) return;
        watermarks[key] = at;
        pending[key] = value;
        Apply(key, value);
    }

    internal void Observe(IReadOnlyDictionary<ulong, ReplayActorState> actors, DateTime now)
    {
        foreach (var key in statuses.Keys.Concat(pending.Keys).Distinct().Where(k => !actors.ContainsKey(k.Actor)).ToArray())
        {
            statuses.Remove(key);
            pending.Remove(key);
            watermarks.Remove(key);
        }
        foreach (var actor in actors.Values)
        foreach (var id in actor.Statuses.Where(s => watchedStatuses.Contains(s.Id)).Select(s => s.Id)
            .Concat(watchedStatuses).Distinct())
        {
            var key = (actor.Id, id);
            var current = actor.Statuses.FirstOrDefault(s => s.Id == id);
            if (pending.TryGetValue(key, out var value))
            {
                if (value == null && current == null || value != null && current != null
                    && value.SourceId == current.SourceId && value.Param == current.Param
                    && current.ExpiresAt >= value.ExpiresAt.AddSeconds(-1)
                    || value != null && current == null && value.ExpiresAt < now)
                    pending.Remove(key);
                else current = value;
            }
            statuses.TryGetValue(key, out var old);
            if (current != null && (old == null || current.SourceId != old.SourceId || current.Param != old.Param
                || (current.ExpiresAt - old.ExpiresAt).TotalSeconds > 1))
                Gain(id);
            if (current == null) statuses.Remove(key);
            else statuses[key] = current;
        }
    }

    private void Apply((ulong Actor, uint Status) key, ReplayStatus? value)
    {
        if (value == null) statuses.Remove(key);
        else
        {
            statuses[key] = value;
            Gain(key.Status);
        }
    }

    private void Gain(uint id)
    {
        if (id == 3977) cardinal = true;
        else if (id == 3978) cardinal = false;
    }

    internal void Start(HuntPendingCast cast)
    {
        if (cast.Rule.Action is >= 39095 and <= 39098) Spins = (int)cast.Rule.Action - 39092;
        if (cast.Rule.Action is not (39099 or 39100)) return;
        if (Spins == 0 && statuses.TryGetValue((owner, 3979), out var charge)) Spins = charge.Param;
        var increment = cast.Rule.Action == 39100 ? MathF.PI / 2 : -MathF.PI / 2;
        sequences.Add((cast with { Shape = cone, Stage = 0 }, Spins, increment));
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        for (var i = 0; i < sequences.Count; ++i)
        {
            var (cast, remaining, increment) = sequences[i];
            if (cast.Order != previous.Order) continue;
            var corrected = cast with
            {
                Location = updated.Location, Rotation = cast.Rotation + updated.Rotation - previous.Rotation,
                Activation = cast.Stage == 0 ? updated.Activation : cast.Activation, Input = updated.Input
            };
            sequences[i] = (corrected, remaining, increment);
            ++version;
        }
    }

    internal void RetractUnobserved(HuntPendingCast cast)
    {
        if (sequences.RemoveAll(s => s.Cast.Order == cast.Order && s.Cast.Source == cast.Source) != 0) ++version;
        if (cross?.Order == cast.Order && cross.Source == cast.Source) cross = null;
    }

    internal void Finish(uint action, ReplayActorState caster, float? rotation, DateTime at)
    {
        if (action is 39102 or 39103) cross = null;
        if (action is not (39099 or 39100 or 39101)) return;
        --Spins;
        if (rotation == null) return;
        var index = sequences.FindIndex(s => MathF.Abs(s.Cast.Location.X - caster.Position.X) <= 1
            && MathF.Abs(s.Cast.Location.Z - caster.Position.Z) <= 1
            && MathF.Abs(MathF.IEEERemainder(s.Cast.Rotation - rotation.Value, 2 * MathF.PI)) <= .05f);
        if (index < 0) return;
        var (cast, remaining, increment) = sequences[index];
        ++version;
        if (--remaining <= 0) sequences.RemoveAt(index);
        else sequences[index] = (cast with
        {
            Rotation = cast.Rotation + increment, Activation = at.AddSeconds(2.7d), Stage = cast.Stage + 1
        }, remaining, increment);
    }

    internal void Update(DateTime now)
    {
        if (lastCount != sequences.Count || lastVersion != version)
        {
            lastCount = sequences.Count;
            lastVersion = version;
            aoes.Clear();
            foreach (var (cast, remaining, increment) in sequences)
            {
                if (remaining > 1)
                    aoes.Add(cast with
                    {
                        Rotation = cast.Rotation + increment, Activation = (cast.Activation > now ? cast.Activation : now).AddSeconds(2.7d),
                        Color = null, SequenceId = $"{owner}/heshuala/{cast.Order}/next"
                    });
                if (remaining != 0)
                    aoes.Add(cast with
                    {
                        Color = remaining > 1 ? new Vector4(1, 1, 0, 1) : null,
                        SequenceId = $"{owner}/heshuala/{cast.Order}/current"
                    });
            }
        }
        if (cross != null || sequences.Count == 0) return;
        var first = sequences[0].Cast;
        cross = first with
        {
            Shape = crossShape, Rotation = first.Rotation + (cardinal ? 0 : MathF.PI / 4),
            Activation = now.AddSeconds(11.5d + Spins * 2d), SequenceId = $"{owner}/heshuala/cross"
        };
    }
}
