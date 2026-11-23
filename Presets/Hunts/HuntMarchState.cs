using System.Numerics;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntMarchState
{
    private readonly Dictionary<(ulong Actor, uint Status), ReplayStatus> known = [];
    private readonly Dictionary<(ulong Actor, uint Status), ReplayStatus?> unconfirmed = [];
    private readonly Dictionary<(ulong Actor, uint Status), DateTime> watermarks = [];
    private readonly Dictionary<ulong, List<(uint Status, DateTime At)>> pending = [];
    private readonly Dictionary<ulong, DateTime> forcedEnds = [];

    internal static bool Matches(uint status) => status is >= 1958 and <= 1961 or 1257 or 3629;

    internal void Status(ulong actor, uint status, DateTime at, ReplayStatus? value)
    {
        var key = (actor, status);
        if (!Matches(status) || watermarks.TryGetValue(key, out var previous) && at <= previous) return;
        watermarks[key] = at;
        unconfirmed[key] = value;
        var observed = known.GetValueOrDefault(key);
        if (value == null ? observed == null : observed != null
            && Math.Abs((observed.ExpiresAt - value.ExpiresAt).TotalSeconds) <= .1) return;
        Apply(actor, status, value);
    }

    internal void Synchronize(IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        foreach (var id in known.Keys.Select(k => k.Actor).Concat(pending.Keys).Concat(forcedEnds.Keys)
            .Concat(unconfirmed.Keys.Select(k => k.Actor)).Distinct().Where(id => !actors.ContainsKey(id)).ToArray())
            Remove(id);
        foreach (var actor in actors.Values)
        foreach (var status in new uint[] { 1958, 1959, 1960, 1961, 1257, 3629 })
        {
            var key = (actor.Id, status);
            var observed = actor.Statuses.FirstOrDefault(s => s.Id == status);
            if (unconfirmed.TryGetValue(key, out var expected))
            {
                if (expected == null ? observed != null : observed == null
                    || Math.Abs((observed.ExpiresAt - expected.ExpiresAt).TotalSeconds) > .1) continue;
                unconfirmed.Remove(key);
                // Acknowledgement of the event is not a second status gain.
                if (observed == null) known.Remove(key);
                else known[key] = observed;
                continue;
            }
            var previous = known.GetValueOrDefault(key);
            if (previous == null && observed == null) continue;
            if (previous != null && observed != null
                && Math.Abs((previous.ExpiresAt - observed.ExpiresAt).TotalSeconds) <= .1) continue;
            Apply(actor.Id, status, observed);
        }
    }

    private void Apply(ulong actor, uint status, ReplayStatus? value)
    {
        var key = (actor, status);
        if (value == null) known.Remove(key);
        else known[key] = value;
        if (status is 1257 or 3629)
        {
            forcedEnds[actor] = value?.ExpiresAt ?? default;
            return;
        }
        if (!pending.TryGetValue(actor, out var moves)) pending[actor] = moves = [];
        if (value != null)
        {
            moves.Add((status, value.ExpiresAt));
            moves.Sort((a, b) => a.At.CompareTo(b.At));
        }
        else
        {
            var index = moves.FindIndex(m => m.Status == status);
            if (index >= 0) moves.RemoveAt(index);
        }
    }

    internal IEnumerable<(Vector3 From, Vector3 To, float Rotation)> Movements(ReplayActorState actor, DateTime now)
    {
        var from = actor.Position;
        var rotation = actor.Rotation;
        if (forcedEnds.GetValueOrDefault(actor.Id) > now)
        {
            var to = from + Direction(rotation) * (6.6f * (float)(forcedEnds[actor.Id] - now).TotalSeconds);
            yield return (from, to, rotation);
            from = to;
        }
        foreach (var move in pending.GetValueOrDefault(actor.Id) ?? [])
        {
            rotation += move.Status switch { 1959 => MathF.PI, 1960 => MathF.PI / 2, 1961 => 3 * MathF.PI / 2, _ => 0 };
            var to = from + Direction(rotation) * 13.2f;
            yield return (from, to, rotation);
            from = to;
        }
    }

    private static Vector3 Direction(float rotation) => new(MathF.Sin(rotation), 0, MathF.Cos(rotation));

    internal void Remove(ulong actor)
    {
        foreach (var key in known.Keys.Concat(unconfirmed.Keys).Concat(watermarks.Keys).Where(k => k.Actor == actor).Distinct().ToArray())
        {
            known.Remove(key);
            unconfirmed.Remove(key);
            watermarks.Remove(key);
        }
        pending.Remove(actor);
        forcedEnds.Remove(actor);
    }

    internal void Clear()
    {
        known.Clear();
        unconfirmed.Clear();
        watermarks.Clear();
        pending.Clear();
        forcedEnds.Clear();
    }
}
