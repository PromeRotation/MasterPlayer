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
    internal Vector3 Position => new(X, Y, Z);
    internal BlundervilleRouteNode Route => new(Position, Radius, Jump);
    internal BlundervilleCourseGraphNode Copy() => new() { Id = Id, X = X, Y = Y, Z = Z, Radius = Radius, Jump = Jump, Mechanism = Mechanism };
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
        return Nodes.Where(n => !Edges.Any(e => e.From == n.Id)).Select(n => n.Id);
    }

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
        if (Nodes is not { Count: >= 2 and <= 256 } || Edges is not { Count: >= 1 and <= 1024 }) return "路网须有2–256个节点、1–1024条连线。";
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
        HashSet<int> Flood(IEnumerable<int> seeds, bool reverse)
        {
            var seen = seeds.ToHashSet(); var queue = new Queue<int>(seen);
            while (queue.TryDequeue(out var id))
                foreach (var e in Edges.Where(e => (reverse ? e.To : e.From) == id))
                    if (seen.Add(reverse ? e.From : e.To)) queue.Enqueue(reverse ? e.From : e.To);
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

    internal Compiled Compile() => new(Kind, Start, Nodes.ToDictionary(n => n.Id, n => n.Route),
        Nodes.ToDictionary(n => n.Id, n => Edges.Where(e => e.From == n.Id).Select(e => e.To).ToArray()), Goals().ToHashSet());

    internal sealed record Compiled(BlundervilleCourse Course, int Start, Dictionary<int, BlundervilleRouteNode> Nodes,
        Dictionary<int, int[]> Next, HashSet<int> Goals)
    {
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
    // Lane choice: earliest arrival at a goal over the drawn arrows, with node
    // waits, against the forecast windows. Without forecasts (or when nothing
    // is clear inside the window) the shortest drawn path is used.
    private const double Window = 30, WaitStep = .25, MaxWait = 2;
    internal static BlundervilleFinalPlanner.Profile Generic(BlundervilleCourse course)
        => new(course, 0, 0, 0, 1, 0, null, false, "路网选道");

    internal static int Nearest(BlundervilleCourseGraph.Compiled graph, Vector3 player)
        => graph.Nodes.Where(n => MathF.Abs(n.Value.Position.Y - player.Y) <= 3).DefaultIfEmpty(graph.Nodes.First())
            .MinBy(n => Vector2.DistanceSquared(new(n.Value.Position.X, n.Value.Position.Z), new(player.X, player.Z))).Key;

    // The node sequence from `from` (inclusive) to a goal. For the Ring the
    // goal is the start again; for Delivery the goal is the node nearest the
    // destination.
    internal static List<int> Route(BlundervilleCourseGraph.Compiled graph, int from, IReadOnlyList<BlundervilleHazard> hazards,
        DateTime now, float speed, Vector3? destination = null)
    {
        var goals = destination is { } d
            ? [graph.Nodes.MinBy(n => Vector2.DistanceSquared(new(n.Value.Position.X, n.Value.Position.Z), new(d.X, d.Z))).Key]
            : graph.Goals.ToHashSet();
        var loop = graph.Course == BlundervilleCourse.Ring;
        return Timed(graph, from, goals, hazards, now, speed, loop) ?? Shortest(graph, from, goals, loop) ?? [from];
    }

    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));

    private static List<int>? Shortest(BlundervilleCourseGraph.Compiled graph, int from, HashSet<int> goals, bool loop)
    {
        if (!loop && goals.Contains(from)) return [from];
        var best = new Dictionary<int, double>(); var parent = new Dictionary<int, int>();
        var open = new PriorityQueue<int, double>();
        foreach (var next in graph.Next[from])
        {
            var d = Distance(graph.Nodes[from].Position, graph.Nodes[next].Position);
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
                var d = cost + Distance(graph.Nodes[id].Position, graph.Nodes[next].Position);
                if (best.TryGetValue(next, out var known) && known <= d) continue;
                best[next] = d; parent[next] = id; open.Enqueue(next, d);
            }
        }
        return null;
    }

    private static List<int> Path(Dictionary<int, int> parent, int from, int to)
    {
        var path = new List<int> { to };
        for (var id = to; path.Count < 512; )
        {
            var p = parent[id]; path.Add(p); if (p == from) break; id = p;
        }
        path.Reverse(); return path;
    }

    private sealed record Label(int Node, double Time, Label? Previous);

    private static List<int>? Timed(BlundervilleCourseGraph.Compiled graph, int from, HashSet<int> goals,
        IReadOnlyList<BlundervilleHazard> hazards, DateTime now, float speed, bool loop)
    {
        if (hazards.Count == 0 || !float.IsFinite(speed) || speed <= 0) return null;
        var index = new BlundervilleFinalPlanner.HazardIndex(hazards, now, Generic(graph.Course));
        var queue = new PriorityQueue<Label, double>(); var seen = new HashSet<(int, int)>();
        queue.Enqueue(new(from, 0, null), 0);
        var expanded = 0;
        while (queue.TryDequeue(out var current, out _) && expanded++ < 20000)
        {
            if (current.Previous != null && goals.Contains(current.Node))
            {
                var path = new List<int>();
                for (var label = current; label != null; label = label.Previous) path.Add(label.Node);
                path.Reverse(); return path;
            }
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
                    if (depart + duration > Window) break;
                    if (wait > 0 && !index.Stand(p, current.Time, depart)) break;
                    // Ramps and jumps are timed by their own planners.
                    if (graph.Nodes[next].Jump || MathF.Abs(p.Y - b.Y) > .5f || index.CachedSegment(p, b, depart, duration))
                    {
                        var arrival = depart + duration;
                        queue.Enqueue(new(next, arrival, current), arrival);
                        break;
                    }
                }
            }
        }
        return null;
    }
}
