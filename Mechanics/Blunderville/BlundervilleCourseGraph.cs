using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

// Editable route graphs for the first and second rounds (FirstA, FirstB,
// Ring, Delivery). The Final keeps its own BlundervilleRouteGraph and timed
// planner. Here a graph only decides which lane sequence the race planner
// follows; dodging, jumps, bridges, ramps and carries stay with their own
// planners, which key off these nodes exactly as they did off the fixed lists.
public sealed class BlundervilleCourseGraphNode
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Radius { get; set; } = 1.2f;
    public bool Jump { get; set; }
    public string Mechanism { get; set; } = "";
    // Laid by the lane grid rather than taken from the verified lanes.
    public bool Generated { get; set; }
    internal Vector3 Position => new(X, Y, Z);
    internal BlundervilleRouteNode Route => new(Position, Radius, Jump);
    internal BlundervilleCourseGraphNode Copy() => new() { Id = Id, X = X, Y = Y, Z = Z, Radius = Radius, Jump = Jump, Mechanism = Mechanism, Generated = Generated };
}

public sealed class BlundervilleCourseGraph
{
    public int Version { get; set; } = 1;
    public string Course { get; set; } = "";
    public int Start { get; set; } = 1;
    public List<BlundervilleCourseGraphNode> Nodes { get; set; } = [];
    public List<BlundervilleGraphEdge> Edges { get; set; } = [];

    internal static readonly BlundervilleCourse[] Editable = [BlundervilleCourse.FirstA, BlundervilleCourse.FirstB, BlundervilleCourse.Ring, BlundervilleCourse.Delivery];
    internal static string Name(BlundervilleCourse course) => course switch
    {
        BlundervilleCourse.FirstA => "第一关A（摆锤／炸弹坡／消失桥）",
        BlundervilleCourse.FirstB => "第一关B（转盘／圆台／黏液坡）",
        BlundervilleCourse.Ring => "第二关（圆环生存）",
        BlundervilleCourse.Delivery => "第二关（搬运水晶）",
        _ => course.ToString()
    };

    internal BlundervilleCourse Kind => Enum.TryParse<BlundervilleCourse>(Course, out var course) ? course : BlundervilleCourse.None;

    internal BlundervilleCourseGraph Copy() => new()
    {
        Version = Version, Course = Course, Start = Start,
        Nodes = (Nodes ?? []).Select(n => n?.Copy() ?? new() { X = float.NaN }).ToList(),
        Edges = (Edges ?? []).Select(e => new BlundervilleGraphEdge { From = e?.From ?? 0, To = e?.To ?? 0 }).ToList()
    };

    internal bool Connect(int from, int to)
    {
        if (from == to || !Nodes.Any(n => n.Id == from) || !Nodes.Any(n => n.Id == to)
            || Edges.Any(e => e.From == from && e.To == to)) return false;
        Edges.Add(new() { From = from, To = to }); return true;
    }

    // Lays a lane grid over every flat stretch between two route nodes, the way
    // the Final mechanism graph covers its slope: rows across the stretch every
    // RowStep, lanes every LaneStep out to MaxOffset either side, links to the
    // next row's lanes within LaneReach and sideways within a row. Jump nodes,
    // ramps (height change) and short links keep their single drawn edge.
    internal const float RowStep = 3, LaneStep = 2, MaxOffset = 8, LaneReach = 4, FloorMargin = .6f;
    internal void Densify()
    {
        var course = Kind;
        var byId = Nodes.ToDictionary(n => n.Id);
        var original = Edges.ToList();
        var done = new HashSet<(int, int)>();
        var nextId = Nodes.Max(n => n.Id) + 1;
        var existing = Edges.Select(e => (e.From, e.To)).ToHashSet();
        bool Floor(Vector3 p) => BlundervilleRoutes.SignedDistanceToCorridor(course, p) <= -FloorMargin;
        foreach (var edge in original)
        {
            var a = byId[edge.From]; var b = byId[edge.To];
            if (done.Contains((b.Id, a.Id)) || done.Contains((a.Id, b.Id))) continue;
            done.Add((a.Id, b.Id));
            var both = original.Any(e => e.From == b.Id && e.To == a.Id);
            var axis = new Vector2(b.X - a.X, b.Z - a.Z); var length = axis.Length();
            if (a.Jump || b.Jump || MathF.Abs(a.Y - b.Y) >= .5f || length < 2 * RowStep) continue;
            // FirstA past z=-250 is the arrow (misdirection) walkway and the
            // vanishing bridge: the arrow planner steers a straight lane there
            // and a grid multiplied its queue changes (58 against 10 dispatches).
            if (course == BlundervilleCourse.FirstA && MathF.Max(a.Z, b.Z) < -250) continue;
            var dir = axis / length; var normal = new Vector2(-dir.Y, dir.X);
            var rowCount = (int)MathF.Floor(length / RowStep);
            var rows = new List<List<(BlundervilleCourseGraphNode Node, float Offset)>>();
            for (var r = 1; r < rowCount; ++r)
            {
                var t = r / (float)rowCount; var centre = Vector2.Lerp(new(a.X, a.Z), new(b.X, b.Z), t); var y = a.Y + (b.Y - a.Y) * t;
                var row = new List<(BlundervilleCourseGraphNode, float)>();
                for (var offset = -MaxOffset; offset <= MaxOffset + .01f; offset += LaneStep)
                {
                    var q = centre + normal * offset; var position = new Vector3(q.X, y, q.Y);
                    if (!Floor(position)) continue;
                    row.Add((new BlundervilleCourseGraphNode { Id = nextId++, X = MathF.Round(q.X * 100) / 100, Y = y, Z = MathF.Round(q.Y * 100) / 100,
                        Radius = 1, Mechanism = a.Mechanism, Generated = true }, offset));
                }
                if (row.Count > 0) rows.Add(row);
            }
            if (rows.Count == 0) continue;
            var added = new List<BlundervilleGraphEdge>();
            void Link(BlundervilleCourseGraphNode from, BlundervilleCourseGraphNode to)
            {
                if (!Walkable(course, from.Position, to.Position)) return;
                added.Add(new() { From = from.Id, To = to.Id });
                if (both) added.Add(new() { From = to.Id, To = from.Id });
            }
            foreach (var (node, _) in rows[0]) Link(a, node);
            for (var r = 0; r < rows.Count; ++r)
            {
                var row = rows[r];
                for (var i = 1; i < row.Count; ++i)
                    if (row[i].Offset - row[i - 1].Offset <= LaneStep + .01f)
                    {
                        if (Walkable(course, row[i - 1].Node.Position, row[i].Node.Position))
                        { added.Add(new() { From = row[i - 1].Node.Id, To = row[i].Node.Id }); added.Add(new() { From = row[i].Node.Id, To = row[i - 1].Node.Id }); }
                    }
                if (r + 1 < rows.Count)
                    foreach (var (from, fo) in row)
                        foreach (var (to, to2) in rows[r + 1])
                            if (MathF.Abs(fo - to2) <= LaneReach + .01f) Link(from, to);
            }
            foreach (var (node, _) in rows[^1]) Link(node, b);
            // Keep the stretch only if the grid actually joins both ends.
            var nodes = rows.SelectMany(r => r.Select(x => x.Node)).ToList();
            var adjacency = added.ToLookup(e => e.From, e => e.To);
            var seen = new HashSet<int> { a.Id }; var queue = new Queue<int>(seen);
            while (queue.TryDequeue(out var id)) foreach (var n in adjacency[id]) if (seen.Add(n)) queue.Enqueue(n);
            if (!seen.Contains(b.Id)) continue;
            // Drop grid nodes that cannot both be reached and lead on to b.
            var reverse = added.ToLookup(e => e.To, e => e.From);
            var back = new HashSet<int> { b.Id }; queue = new Queue<int>(back);
            while (queue.TryDequeue(out var id)) foreach (var n in reverse[id]) if (back.Add(n)) queue.Enqueue(n);
            var keep = nodes.Where(n => seen.Contains(n.Id) && back.Contains(n.Id)).Select(n => n.Id).ToHashSet();
            keep.Add(a.Id); keep.Add(b.Id);
            Nodes.AddRange(nodes.Where(n => keep.Contains(n.Id)));
            foreach (var e in added.Where(e => keep.Contains(e.From) && keep.Contains(e.To)))
                if (existing.Add((e.From, e.To))) Edges.Add(e);
        }
    }

    internal void RemoveNode(int id)
    {
        Nodes.RemoveAll(n => n.Id == id); Edges.RemoveAll(e => e.From == id || e.To == id);
    }

    // Goals: the finish of a race course, the pads of Delivery, the start for
    // the Ring loop (every lane must come back round).
    internal IEnumerable<int> Goals()
    {
        var kind = Kind;
        if (kind == BlundervilleCourse.Ring) return [Start];
        // Delivery targets are chosen per carry (the node nearest the pad or
        // the pickup); every node is a possible end.
        if (kind == BlundervilleCourse.Delivery) return Nodes.Select(n => n.Id);
        var sources = Edges.Select(e => e.From).ToHashSet();
        return Nodes.Where(n => !sources.Contains(n.Id)).Select(n => n.Id);
    }

    internal const int MaxNodes = 4096, MaxEdges = 20000;

    internal static bool OnFloor(BlundervilleCourse course, Vector3 p, float tolerance)
        => BlundervilleRoutes.Boundary(course).Contains(p, tolerance);

    internal static bool Walkable(BlundervilleCourse course, Vector3 a, Vector3 b)
    {
        var length = Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
        var samples = Math.Max(2, (int)MathF.Ceiling(length / .5f) + 1);
        for (var i = 0; i < samples; ++i)
            if (!OnFloor(course, Vector3.Lerp(a, b, i / (float)(samples - 1)), .6f)) return false;
        return true;
    }

    internal string? Validate()
    {
        var course = Kind;
        if (Version != 1 || !Editable.Contains(course)) return "不支持的路网版本或关卡。";
        if (Nodes is not { Count: >= 2 and <= MaxNodes } || Edges is not { Count: >= 1 and <= MaxEdges }) return $"路网须有2–{MaxNodes}个节点、1–{MaxEdges}条连线。";
        var ids = new HashSet<int>();
        foreach (var node in Nodes)
            if (node == null || node.Id <= 0 || !ids.Add(node.Id) || !float.IsFinite(node.X) || !float.IsFinite(node.Y) || !float.IsFinite(node.Z)
                || node.Radius is < .2f or > 4 || !OnFloor(course, node.Position, 1))
                return $"节点{node?.Id}编号重复、坐标无效、到达半径不在0.2–4m或超出可走地形。";
        var byId = Nodes.ToDictionary(n => n.Id);
        if (!byId.ContainsKey(Start)) return "请设定起点节点。";
        var pairs = new HashSet<(int, int)>();
        foreach (var edge in Edges)
        {
            if (edge == null || !byId.TryGetValue(edge.From, out var a) || !byId.TryGetValue(edge.To, out var b)
                || edge.From == edge.To || !pairs.Add((edge.From, edge.To))) return "连线引用无效、重复或连接自身。";
            if (MathF.Abs(a.Y - b.Y) > 6) return $"连线{a.Id} → {b.Id}高差超过6m。";
            // Between nodes the race planner's solver walks around pillars and
            // the jump/ramp planners own their lanes, so a link is not required
            // to be a straight walk; both ends must be on the course.
        }
        var forward = Edges.ToLookup(e => e.From, e => e.To); var backward = Edges.ToLookup(e => e.To, e => e.From);
        HashSet<int> Flood(IEnumerable<int> seeds, bool reverse)
        {
            var seen = seeds.ToHashSet(); var queue = new Queue<int>(seen);
            while (queue.TryDequeue(out var id))
                foreach (var next in (reverse ? backward : forward)[id])
                    if (seen.Add(next)) queue.Enqueue(next);
            return seen;
        }
        if (Flood([Start], false).Count != Nodes.Count) return "有节点无法从起点沿箭头到达，请补连线或删除孤立节点。";
        var goals = Goals().ToArray();
        if (goals.Length == 0) return "没有终点（没有出口的节点）。";
        if (course == BlundervilleCourse.Delivery && Flood([Start], true).Count != Nodes.Count)
            return "搬运路网的每个节点都须能连回中央起点。";
        if (course != BlundervilleCourse.Delivery && Flood(goals, true).Count != Nodes.Count)
            return course == BlundervilleCourse.Ring ? "圆环每个分支都须连回起点。" : "有分支无法到达终点，请连接后续节点。";
        return null;
    }

    internal Compiled Compile()
    {
        var next = Edges.ToLookup(e => e.From, e => e.To);
        return new(Kind, Start, Nodes.ToDictionary(n => n.Id, n => n.Route), Nodes.ToDictionary(n => n.Id, n => next[n.Id].ToArray()), Goals().ToHashSet(),
            Nodes.Where(n => n.Generated).Select(n => n.Id).ToHashSet());
    }

    internal sealed record Compiled(BlundervilleCourse Course, int Start, Dictionary<int, BlundervilleRouteNode> Nodes,
        Dictionary<int, int[]> Next, HashSet<int> Goals, HashSet<int> Generated)
    {
        // Grid lanes cost a fifth more: with no forecast (or no time to gain)
        // the verified lane is kept; the grid is taken when it saves time.
        internal const float GridPenalty = 1.2f;
        internal float Cost(int a, int b) => Vector2.Distance(new(Nodes[a].Position.X, Nodes[a].Position.Z), new(Nodes[b].Position.X, Nodes[b].Position.Z))
            * (Generated.Contains(a) || Generated.Contains(b) ? GridPenalty : 1);
        internal bool Fork(int id) => Next.TryGetValue(id, out var next) && next.Length > 1;
    }

    // Built-in graphs: the verified lanes MP already drove, plus the lane
    // switches the floor allows. A course with no saved graph uses these, so
    // their single-branch parts reproduce the fixed node lists exactly.
    internal static readonly Vector3 WestPad = new(-240.2991f, 6, 275.4989f), EastPad = new(-159.4339f, 6, 275.2527f), NorthPad = new(-200.1303f, 6, 205.1951f);

    internal static BlundervilleCourseGraph Default(BlundervilleCourse course)
    {
        var graph = new BlundervilleCourseGraph { Course = course.ToString() };
        var byPosition = new Dictionary<Vector3, int>();
        int Node(BlundervilleRouteNode n, string mechanism)
        {
            if (byPosition.TryGetValue(n.Position, out var existing)) return existing;
            var id = graph.Nodes.Count + 1;
            graph.Nodes.Add(new() { Id = id, X = n.Position.X, Y = n.Position.Y, Z = n.Position.Z, Radius = n.ArrivalRadius, Jump = n.Jump, Mechanism = mechanism });
            byPosition[n.Position] = id; return id;
        }
        int[] Chain(IReadOnlyList<BlundervilleRouteNode> nodes, Func<int, string> mechanism)
        {
            var ids = nodes.Select((n, i) => Node(n, mechanism(i))).ToArray();
            for (var i = 1; i < ids.Length; ++i) graph.Connect(ids[i - 1], ids[i]);
            return ids;
        }
        switch (course)
        {
            case BlundervilleCourse.FirstA:
                // All recorded runs use the left approach; the right one is not
                // verified against the obstacle gaps and is not offered.
                Chain(BlundervilleRoutes.Nodes(course, new(-201, 0, 0)), i => i < 8 ? "摆锤坡道" : i < 13 ? "炸弹坡" : "消失桥");
                graph.Start = 1;
                break;
            case BlundervilleCourse.FirstB:
            {
                string Section(BlundervilleRouteNode n) => n.Position.Z > -160 ? "转盘与弹板" : n.Position.Z > -194 ? "直道" : n.Position.Z > -271 ? "圆台" : "黏液坡";
                // The start line holds both lanes; a spawn joins its nearer lane.
                var start = Node(new(BlundervilleRoutes.VerifiedStart(course), 1.2f), "起跑");
                var left = BlundervilleRoutes.Nodes(course, new(-1, 0, 0)); var right = BlundervilleRoutes.Nodes(course, new(1, 0, 0));
                var l = Chain(left, i => Section(left[i])); var r = Chain(right, i => Section(right[i]));
                // The floor spans |x|<=4.7 at z=-189.5: either straight lane can
                // switch sides before the circle rows.
                var lb = l.First(id => graph.Nodes[id - 1].Z == -189); var rb = r.First(id => graph.Nodes[id - 1].Z == -189);
                var lc = l.First(id => graph.Nodes[id - 1].Z == -197); var rc = r.First(id => graph.Nodes[id - 1].Z == -197);
                foreach (var (a, b) in new[] { (lb, rc), (rb, lc) })
                    if (Walkable(course, graph.Nodes[a - 1].Position, graph.Nodes[b - 1].Position)) graph.Connect(a, b);
                graph.Connect(start, l[0]); graph.Connect(start, r[0]);
                graph.Start = start;
                break;
            }
            case BlundervilleCourse.Ring:
            {
                var loop = BlundervilleRoutes.Nodes(course, default);
                var ids = Chain(loop, i => loop[i].Position.Z > -135 ? "北侧" : "南侧");
                graph.Connect(ids[^1], ids[0]); graph.Start = ids[0];
                break;
            }
            case BlundervilleCourse.Delivery:
            {
                var hub = BlundervilleRoutes.DeliveryNodes(default, WestPad)[0];
                var hubId = Node(hub, "中央");
                foreach (var (pad, name) in new[] { (WestPad, "西线"), (EastPad, "东线"), (NorthPad, "北线") })
                {
                    var spoke = BlundervilleRoutes.DeliveryNodes(hub.Position, pad);
                    var ids = spoke.Select((n, i) => i == 0 ? hubId : Node(n, name)).ToArray();
                    for (var i = 1; i < ids.Length; ++i) { graph.Connect(ids[i - 1], ids[i]); graph.Connect(ids[i], ids[i - 1]); }
                }
                graph.Start = hubId;
                break;
            }
        }
        // The Ring is outrun around a fixed loop: waits or grid detours let the
        // beam catch up (22 recorded rounds: 10 clean with timed grid lanes
        // against 17 on the verified loop). It keeps the verified loop.
        if (course != BlundervilleCourse.Ring) graph.Densify();
        return graph;
    }
}

internal static class BlundervilleCourseGraphs
{
    private static readonly Dictionary<BlundervilleCourse, BlundervilleCourseGraph.Compiled?> Defaults = [];
    private static readonly object Gate = new();

    // The saved graph when valid, else the built-in one.
    internal static BlundervilleCourseGraph.Compiled? Assigned(IReadOnlyDictionary<string, BlundervilleCourseGraph>? saved, BlundervilleCourse course)
    {
        if (!BlundervilleCourseGraph.Editable.Contains(course)) return null;
        if (saved != null && saved.TryGetValue(course.ToString(), out var graph) && graph != null && graph.Validate() == null) return graph.Compile();
        lock (Gate)
        {
            if (!Defaults.TryGetValue(course, out var compiled))
            {
                var built = BlundervilleCourseGraph.Default(course);
                Defaults[course] = compiled = built.Validate() == null ? built.Compile() : null;
            }
            return compiled;
        }
    }

    internal static BlundervilleCourseGraph Editing(IReadOnlyDictionary<string, BlundervilleCourseGraph>? saved, BlundervilleCourse course)
        => saved != null && saved.TryGetValue(course.ToString(), out var graph) && graph != null ? graph.Copy() : BlundervilleCourseGraph.Default(course);
}

internal static class BlundervilleCourseRouter
{
    // Lane choice over the drawn arrows: earliest arrival at a goal, with node
    // waits, against the forecast windows. A goal beyond the window takes the
    // label that got furthest (least remaining distance) within it. Each first
    // branch is searched on its own worker; without forecasts the shortest
    // drawn path is used.
    private const double Window = 20, WaitStep = .25, MaxWait = 2;
    private const int ExpansionLimit = 60000;
    internal static BlundervilleFinalPlanner.Profile Generic(BlundervilleCourse course)
        => new(course, 0, 0, 0, 1, 0, null, false, "路网选道");

    internal static int Nearest(BlundervilleCourseGraph.Compiled graph, Vector3 player)
        => graph.Nodes.Where(n => MathF.Abs(n.Value.Position.Y - player.Y) <= 3).DefaultIfEmpty(graph.Nodes.First())
            .MinBy(n => Vector2.DistanceSquared(new(n.Value.Position.X, n.Value.Position.Z), new(player.X, player.Z))).Key;

    internal static List<int> Route(BlundervilleCourseGraph.Compiled graph, int from, IReadOnlyList<BlundervilleHazard> hazards,
        DateTime now, float speed, Vector3? destination = null) => Scored(graph, from, hazards, now, speed, destination).Path;

    // Expected time (with waits, penalties and the remaining walk past the
    // window) of following a given node sequence; used to keep the current
    // route unless a re-plan is clearly better.
    internal static double Evaluate(BlundervilleCourseGraph.Compiled graph, IReadOnlyList<int> path, IReadOnlyList<BlundervilleHazard> hazards,
        DateTime now, float speed, Vector3? destination = null)
    {
        if (path.Count < 2 || !float.IsFinite(speed) || speed <= 0) return double.PositiveInfinity;
        var goals = Goals(graph, destination); var loop = graph.Course == BlundervilleCourse.Ring;
        var remaining = Remaining(graph, goals, path[0], loop);
        var index = new BlundervilleFinalPlanner.HazardIndex(hazards, now, Generic(graph.Course));
        var time = 0.0; var penalty = 0.0;
        for (var i = 1; i < path.Count; ++i)
        {
            var a = graph.Nodes[path[i - 1]].Position; var b = graph.Nodes[path[i]].Position; var duration = Distance(a, b) / speed;
            penalty += (graph.Cost(path[i - 1], path[i]) - Distance(a, b)) / speed;
            var moved = false;
            for (var wait = 0.0; wait <= MaxWait; wait += WaitStep)
            {
                if (wait > 0 && !index.Stand(a, time, time + wait)) break;
                if (graph.Nodes[path[i]].Jump || MathF.Abs(a.Y - b.Y) > .5f || index.CachedSegment(a, b, time + wait, duration))
                { time += wait + duration; moved = true; break; }
            }
            if (!moved) return double.PositiveInfinity;
            if (time > Window) return time + penalty + (remaining.TryGetValue(path[i], out var r) ? r / speed : double.PositiveInfinity);
        }
        return time + penalty + (remaining.TryGetValue(path[^1], out var rest) ? rest / speed : double.PositiveInfinity);
    }

    private static HashSet<int> Goals(BlundervilleCourseGraph.Compiled graph, Vector3? destination) => destination is { } d
        ? [graph.Nodes.MinBy(n => Vector2.DistanceSquared(new(n.Value.Position.X, n.Value.Position.Z), new(d.X, d.Z))).Key]
        : graph.Goals.ToHashSet();

    internal static (List<int> Path, double Score) Scored(BlundervilleCourseGraph.Compiled graph, int from, IReadOnlyList<BlundervilleHazard> hazards,
        DateTime now, float speed, Vector3? destination = null)
    {
        var goals = Goals(graph, destination);
        var loop = graph.Course == BlundervilleCourse.Ring;
        var remaining = Remaining(graph, goals, from, loop);
        var shortest = Shortest(graph, from, goals, loop, remaining);
        // The Ring loop is not re-timed (see Default).
        if (hazards.Count == 0 || loop || !float.IsFinite(speed) || speed <= 0) return (shortest ?? [from], double.NaN);
        var firsts = graph.Next[from];
        var results = new (List<int>? Path, double Score)[firsts.Length];
        // HazardIndex caches per query and is not thread-safe: one per worker.
        Parallel.For(0, firsts.Length, i => results[i] = Timed(graph, from, firsts[i], goals,
            new BlundervilleFinalPlanner.HazardIndex(hazards, now, Generic(graph.Course)), speed, loop, remaining));
        var best = results.Where(r => r.Path != null).OrderBy(r => r.Score).FirstOrDefault();
        if (best.Path is not { Count: > 0 } path) return (shortest ?? [from], double.PositiveInfinity);
        // Beyond the forecast window the drawn shortest path finishes the route;
        // the next fork re-plans it with fresh forecasts.
        if (!goals.Contains(path[^1]) && Shortest(graph, path[^1], goals, loop, remaining) is { Count: > 1 } rest)
            path.AddRange(rest.Skip(1));
        return (path, best.Score);
    }

    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));

    // Walking distance from every node to the nearest goal (reverse Dijkstra).
    private static Dictionary<int, double> Remaining(BlundervilleCourseGraph.Compiled graph, HashSet<int> goals, int from, bool loop)
    {
        var reverse = graph.Next.SelectMany(p => p.Value.Select(to => (From: p.Key, To: to))).ToLookup(e => e.To, e => e.From);
        var distance = new Dictionary<int, double>(); var open = new PriorityQueue<int, double>();
        foreach (var goal in goals) { distance[goal] = 0; open.Enqueue(goal, 0); }
        while (open.TryDequeue(out var id, out var cost))
        {
            if (cost > distance[id]) continue;
            foreach (var prev in reverse[id])
            {
                if (loop && id == from && prev != from) { }
                var d = cost + graph.Cost(prev, id);
                if (distance.TryGetValue(prev, out var known) && known <= d) continue;
                distance[prev] = d; open.Enqueue(prev, d);
            }
        }
        return distance;
    }

    private static List<int>? Shortest(BlundervilleCourseGraph.Compiled graph, int from, HashSet<int> goals, bool loop, Dictionary<int, double> remaining)
    {
        if (!loop && goals.Contains(from)) return [from];
        var best = new Dictionary<int, double>(); var parent = new Dictionary<int, int>();
        var open = new PriorityQueue<int, double>();
        foreach (var next in graph.Next[from])
        {
            var d = (double)graph.Cost(from, next);
            if (best.TryGetValue(next, out var known) && known <= d) continue;
            best[next] = d; parent[next] = from; open.Enqueue(next, d);
        }
        while (open.TryDequeue(out var id, out var cost))
        {
            if (cost > best[id]) continue;
            if (goals.Contains(id)) return Path(parent, from, id);
            if (id == from) continue;
            foreach (var next in graph.Next[id])
            {
                var d = cost + graph.Cost(id, next);
                if (best.TryGetValue(next, out var known) && known <= d) continue;
                best[next] = d; parent[next] = id; open.Enqueue(next, d);
            }
        }
        return null;
    }

    private static List<int> Path(Dictionary<int, int> parent, int from, int to)
    {
        var path = new List<int> { to };
        for (var id = to; path.Count < 16384; )
        {
            var p = parent[id]; path.Add(p); if (p == from) break; id = p;
        }
        path.Reverse(); return path;
    }

    private sealed record Label(int Node, double Time, Label? Previous, int Depth, double Penalty = 0);

    // Time-expanded A* from `from` whose first step is `first`. Score: arrival
    // at a goal, or for a goal beyond the window the time plus the remaining
    // walk from the furthest label reached.
    private static (List<int>? Path, double Score) Timed(BlundervilleCourseGraph.Compiled graph, int from, int first, HashSet<int> goals,
        BlundervilleFinalPlanner.HazardIndex index, float speed, bool loop, Dictionary<int, double> remaining)
    {
        double Rest(int id) => remaining.TryGetValue(id, out var r) ? r / speed : double.PositiveInfinity;
        var queue = new PriorityQueue<Label, double>(); var seen = new HashSet<(int, int)>();
        Label? furthest = null; var furthestScore = double.PositiveInfinity;
        void Push(int node, double time, Label? previous)
        {
            var penalty = (previous?.Penalty ?? 0) + (previous == null ? 0
                : (graph.Cost(previous.Node, node) - Distance(graph.Nodes[previous.Node].Position, graph.Nodes[node].Position)) / speed);
            var label = new Label(node, time, previous, (previous?.Depth ?? 0) + 1, penalty);
            var h = Rest(node); if (!double.IsFinite(h)) return;
            queue.Enqueue(label, time + penalty + h);
            if (time + penalty + h < furthestScore && previous != null) { furthestScore = time + penalty + h; furthest = label; }
        }
        var start = new Label(from, 0, null, 0);
        var p0 = graph.Nodes[from].Position; var b0 = graph.Nodes[first].Position; var d0 = Distance(p0, b0) / speed;
        for (var wait = 0.0; wait <= MaxWait; wait += WaitStep)
        {
            if (wait > 0 && !index.Stand(p0, 0, wait)) break;
            if (graph.Nodes[first].Jump || MathF.Abs(p0.Y - b0.Y) > .5f || index.CachedSegment(p0, b0, wait, d0)) { Push(first, wait + d0, start); break; }
        }
        var expanded = 0;
        while (queue.TryDequeue(out var current, out _) && expanded++ < ExpansionLimit)
        {
            if (goals.Contains(current.Node) && (!loop || current.Node == from)) return (Chain(current), current.Time + current.Penalty);
            if (current.Time > Window) continue;
            if (!seen.Add((current.Node, (int)Math.Round(current.Time / WaitStep)))) continue;
            var p = graph.Nodes[current.Node].Position;
            foreach (var next in graph.Next[current.Node])
            {
                if (next == from && !loop) continue;
                var revisited = false;
                for (var prior = current.Previous; !revisited && prior != null; prior = prior.Previous) revisited = prior.Node == next && !(loop && next == from);
                if (revisited) continue;
                var b = graph.Nodes[next].Position; var duration = Distance(p, b) / speed;
                for (var wait = 0.0; wait <= MaxWait; wait += WaitStep)
                {
                    var depart = current.Time + wait;
                    if (wait > 0 && !index.Stand(p, current.Time, depart)) break;
                    // Ramps and jumps are timed by their own planners.
                    if (graph.Nodes[next].Jump || MathF.Abs(p.Y - b.Y) > .5f || index.CachedSegment(p, b, depart, duration))
                    { Push(next, depart + duration, current); break; }
                }
            }
        }
        return furthest == null ? (null, double.PositiveInfinity) : (Chain(furthest), furthestScore);
    }

    private static List<int> Chain(Label end)
    {
        var path = new List<int>();
        for (var label = end; label != null; label = label.Previous) path.Add(label.Node);
        path.Reverse(); return path;
    }
}
