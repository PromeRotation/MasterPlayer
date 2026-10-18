using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed class BlundervilleRingColumns
{
    internal const string Prefix = "ring/column/";
    internal const float Radius = 1.5f, FanRadius = 1.1f;
    internal const double TransitSeconds = .85;
    private sealed record Column(BlundervilleHazard Body, BlundervilleHazard? Source = null,
        BlundervilleHazard? Sweep = null, DateTime? MovingUntil = null, bool AwaitingEffect = false);
    private readonly Dictionary<ulong, Column> columns = [];
    private BlundervilleHazard[] fixedBodies = [];

    private static bool OnRing(Vector3 point) => BlundervilleRoutes.FromPosition(point) == BlundervilleCourse.Ring;
    private static float DistanceSquared(Vector3 a, Vector3 b) => Vector2.DistanceSquared(new(a.X, a.Z), new(b.X, b.Z));

    internal void Synchronize(IEnumerable<BlundervilleActor> actors, DateTime now)
    {
        var visible = actors.Where(a => !a.Dead && OnRing(a.Position)).ToArray();
        if (visible.Length == 0) { Reset(); return; }
        EnsureFixed(now);
        var seen = new HashSet<ulong>();
        foreach (var actor in visible.Where(a => a.Oid == 16350))
        {
            seen.Add(actor.Id);
            if (columns.TryGetValue(actor.Id, out var old))
            {
                // ARR poses remain at the origin for up to .205s after the
                // effect, and reach the destination only .610-.799s later.
                if (old.MovingUntil is { } until && now <= until.AddSeconds(.2)) continue;
                if (DistanceSquared(old.Body.Geometry.Position, actor.Position) < .2f * .2f) continue;
            }
            columns[actor.Id] = new(Body(actor.Id, actor.Position, now));
        }
        foreach (var id in columns.Keys.Where(id => !seen.Contains(id)).ToArray()) columns.Remove(id);
    }

    internal void Cast(ulong source, BlundervilleCast cast, Vector3 from, DateTime now)
    {
        if (cast.Action == 35409)
            Move(source, from, cast.Location, cast.FinishAt, now, true);
    }

    internal bool Effect(ulong source, Vector3 from, Vector3? destination, DateTime now)
        => destination is { } to && Move(source, from, to, now, now, false);

    private bool Move(ulong source, Vector3 from, Vector3 to, DateTime activation, DateTime observed, bool pending)
    {
        if (!OnRing(from) || !OnRing(to)) return false;
        var length = MathF.Sqrt(DistanceSquared(from, to));
        if (length is <= .5f or > 40) return false;
        EnsureFixed(observed);
        var until = activation.AddSeconds(TransitSeconds);
        var sourceFrom = columns.TryGetValue(source, out var old) ? old.Source?.From ?? old.Body.From : observed;
        if (sourceFrom > observed) sourceFrom = observed;
        var origin = new BlundervilleHazard($"{Prefix}{source}/source", new CircleGeometry(from, Radius), sourceFrom, until, observed)
            { HasMotionEnvelope = !pending };
        // The destination is also the round cap of the swept charge. It is
        // dangerous during transit, then remains occupied by the parked body.
        var body = Body(source, to, activation) with { HasMotionEnvelope = !pending };
        var sweep = new BlundervilleHazard($"{Prefix}{source}/sweep",
            new RectangleGeometry((from + to) / 2, MathF.Atan2(to.X - from.X, to.Z - from.Z), 2 * Radius, length),
            activation, until, observed) { HasMotionEnvelope = !pending };
        columns[source] = new(body, origin, sweep, until, pending);
        return true;
    }

    private static BlundervilleHazard Body(ulong source, Vector3 position, DateTime from)
        => new($"{Prefix}{source}/body", new CircleGeometry(position, Radius), from, DateTime.MaxValue, from);

    private void EnsureFixed(DateTime now)
    {
        if (fixedBodies.Length != 0) return;
        // Only the two fan centres have a permanent physical base. The four
        // 34805 helper positions must not become invented permanent pillars.
        fixedBodies = [
            new(Prefix + "fan/west", new CircleGeometry(new(180, 10, -125), FanRadius), now, DateTime.MaxValue, now),
            new(Prefix + "fan/east", new CircleGeometry(new(220, 10, -125), FanRadius), now, DateTime.MaxValue, now),
        ];
    }

    internal IEnumerable<BlundervilleHazard> Hazards(DateTime now)
    {
        foreach (var body in fixedBodies) yield return body;
        foreach (var column in columns.Values)
        {
            yield return column.Body;
            if (column.Source is { } origin && now <= origin.Until) yield return origin;
            if (column.Sweep is { } sweep && now <= sweep.Until) yield return sweep;
        }
    }

    internal void Cancel(ulong source)
    {
        if (columns.TryGetValue(source, out var column) && column.AwaitingEffect && column.Source is { } origin)
            columns[source] = new(Body(source, origin.Geometry.Position, origin.From));
    }

    internal void Remove(ulong source) => columns.Remove(source);
    internal void Reset() { columns.Clear(); fixedBodies = []; }
}
