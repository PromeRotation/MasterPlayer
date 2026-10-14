using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using static MasterPlayer.Mechanics.Blunderville.BlundervilleFinalPlanner;

namespace MasterPlayer.Mechanics.Blunderville;

// A carry bridge is not a one-way sequence of rows: moving sideways or back
// to a pocket can be necessary before the next swing. Geometry is cached;
// only the arrival times and animation windows change between searches.
internal static class BlundervilleDeliverySearch
{
    private sealed record Vertex(Vector3 Position, float Progress, int Row);
    private sealed record Graph(Vertex[] Vertices, int[][] Edges, int Goal);
    private sealed record Visit(int Vertex, double Time, double Departure, Visit? Parent);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Profile, Lazy<Graph>> Graphs = new();
    private static readonly Lazy<Task> Warmup = new(() => Task.Run(() =>
        Parallel.ForEach(new[] { DeliveryWest, DeliveryEast, DeliveryNorth }, profile =>
            GetGraph(profile, BlundervilleRoutes.NavigationBoundary(BlundervilleCourse.Delivery, profile.Spoke![0])))));
    internal static void Prepare() => _ = Warmup.Value;
    private static Graph GetGraph(Profile profile, IGeometryShape floor)
        => Graphs.GetOrAdd(profile, p => new(() => Build(p, floor))).Value;
    private const double Limit = BlundervilleDeliverySchedule.Horizon - StandAfterGoal - Lag;

    internal static List<Waypoint>? Find(Profile profile, HazardIndex hazards, IGeometryShape floor,
        Vector3 player, DateTime now, float speed, CancellationToken cancellation = default)
    {
        var graph = GetGraph(profile, floor);
        var vertices = graph.Vertices;
        var start = vertices.Length;
        var progress = Along(profile.Spoke!, player).S;
        var first = Enumerable.Range(0, vertices.Length)
            .Where(i => Vector3.Distance(player, vertices[i].Position) <= 4 && FloorLink(floor, player, vertices[i].Position, true)).ToArray();
        if (first.Length == 0) return null;
        var queue = new PriorityQueue<Visit, double>();
        var reached = new Dictionary<(int, double), double>();
        // Leave time for the worker result and the first input frame. This
        // planned stand is revalidated by the caller when the task completes.
        queue.Enqueue(new(start, .2, .2, null), .2);
        Visit? refuge = null;
        while (queue.TryDequeue(out var node, out _))
        {
            if (cancellation.IsCancellationRequested) return null;
            var from = node.Vertex == start ? player : vertices[node.Vertex].Position;
            var key = (node.Vertex, hazards.SafeInterval(from, node.Time));
            if (reached.TryGetValue(key, out var earlier) && earlier < node.Time - .001) continue;
            if (node.Vertex == graph.Goal) return Plan(node, vertices, player, now);
            if (node.Vertex != start && vertices[node.Vertex].Progress > progress + 1
                && (profile.Colour != 37 || from.Z is < 212 or > 243 || BlundervilleDeliveryWind.Protected(from))
                && (refuge == null || vertices[node.Vertex].Progress > vertices[refuge.Vertex].Progress + .1f)
                && hazards.Stand(from, node.Time, node.Time + StandAfterGoal)) refuge = node;
            foreach (var next in node.Vertex == start ? first : graph.Edges[node.Vertex])
            {
                var to = vertices[next].Position;
                var travel = Vector3.Distance(from, to) / speed;
                var earliest = node.Time;
                // Keep separate arrivals into later safe intervals. A first
                // arrival just before a closing gap can have no way onward.
                for (var window = 0; window < 4 && earliest + travel <= Limit; ++window)
                {
                    var departure = hazards.Departure(from, to, earliest, travel, Limit - travel);
                    if (!double.IsFinite(departure)) break;
                    if (departure > node.Time + .001 && !hazards.Stand(from, node.Time, departure + profile.WaitSlip)) break;
                    var arrival = departure + travel;
                    var interval = hazards.SafeInterval(to, arrival);
                    var targetKey = (next, interval);
                    if (!reached.TryGetValue(targetKey, out var old) || arrival < old - .001)
                    {
                        reached[targetKey] = arrival;
                        queue.Enqueue(new(next, arrival, departure, node), arrival + Vector3.Distance(to, profile.Spoke![^1]) / speed);
                    }
                    if (!double.IsFinite(interval)) break;
                    earliest = Math.Max(departure + .05, interval - travel + .01);
                }
            }
        }
        return refuge == null ? null : Plan(refuge, vertices, player, now);
    }

    private static Graph Build(Profile profile, IGeometryShape floor)
    {
        var vertices = new List<Vertex>();
        var stations = Stations(profile.Spoke!, true);
        var width = (int)MathF.Round(profile.Lanes / profile.LaneWidth);
        for (var row = 0; row < stations.Count; ++row)
        {
            var (progress, point, lateral) = stations[row];
            var lanes = row == stations.Count - 1 ? 0 : width;
            for (var lane = -lanes; lane <= lanes; ++lane)
            {
                var p = point + lateral * (lane * profile.LaneWidth);
                if (ArenaBoundaryTraversal.ContainsTube(floor, p, p, FloorClearance)) vertices.Add(new(p, progress, row));
            }
        }
        var edges = new int[vertices.Count][];
        for (var i = 0; i < vertices.Count; ++i)
        {
            var source = vertices[i];
            edges[i] = Enumerable.Range(0, vertices.Count).Where(j => j != i && Math.Abs(vertices[j].Row - source.Row) <= 2
                && Vector3.Distance(source.Position, vertices[j].Position) is > .1f and <= 3.5f
                && FloorLink(floor, source.Position, vertices[j].Position, false)).ToArray();
        }
        return new(vertices.ToArray(), edges, vertices.FindIndex(v => v.Position == profile.Spoke![^1]));
    }

    // A wind push can leave the centre against a barrier. Permit only the
    // initial half-metre to recover clearance, still wholly on the floor.
    internal static bool FloorLink(IGeometryShape floor, Vector3 from, Vector3 to, bool escape)
    {
        if (ArenaBoundaryTraversal.ContainsTube(floor, from, to, FloorClearance)) return true;
        if (!escape || !ArenaBoundaryTraversal.ContainsSegment(floor, from, to)) return false;
        var distance = Vector3.Distance(from, to);
        if (distance < .5f) return ArenaBoundaryTraversal.ContainsTube(floor, to, to, FloorClearance);
        return ArenaBoundaryTraversal.ContainsTube(floor, Vector3.Lerp(from, to, .5f / distance), to, FloorClearance);
    }

    private static List<Waypoint> Plan(Visit end, Vertex[] vertices, Vector3 player, DateTime now)
    {
        var nodes = new List<Visit>();
        for (var n = end; n != null; n = n.Parent) nodes.Add(n);
        nodes.Reverse();
        var plan = nodes.Select((n, i) => new Waypoint(n.Vertex == vertices.Length ? player : vertices[n.Vertex].Position,
            now.AddSeconds(n.Time), now.AddSeconds(i + 1 < nodes.Count ? nodes[i + 1].Departure : n.Time))).ToList();
        for (var i = plan.Count - 2; i > 0; --i)
        {
            var a = plan[i].Position - plan[i - 1].Position; var b = plan[i + 1].Position - plan[i].Position;
            if (plan[i].Departure == plan[i].Arrival && a.LengthSquared() > .0001 && b.LengthSquared() > .0001
                && Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b)) > .9999f) plan.RemoveAt(i);
        }
        return plan;
    }
}
