using System.Numerics;
using static MasterPlayer.Mechanics.Blunderville.BlundervillePresetGeometry;

namespace MasterPlayer.Mechanics.Blunderville;

public sealed class BlundervilleGraphNode
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
    public string Mechanism { get; set; } = "";
    internal Vector3 Position => new(X, BlundervilleRoutes.HeightHint(BlundervilleCourse.Final, Z), Z);
    internal bool Exit => MathF.Abs(Z - EndZ) < .01f;
    internal BlundervilleGraphNode Copy() => new() { Id = Id, X = X, Z = Z, Mechanism = Mechanism };
}

public sealed class BlundervilleGraphEdge
{
    public int From { get; set; }
    public int To { get; set; }
}

public sealed class BlundervilleRouteGraph
{
    public int Version { get; set; } = 1;
    public int Start { get; set; } = 1;
    public List<BlundervilleGraphNode> Nodes { get; set; } = [];
    public List<BlundervilleGraphEdge> Edges { get; set; } = [];

    internal BlundervilleRouteGraph Copy() => new()
    {
        Version = Version, Start = Start,
        Nodes = (Nodes ?? []).Select(n => n?.Copy() ?? new() { X = float.NaN }).ToList(),
        Edges = (Edges ?? []).Select(e => new BlundervilleGraphEdge { From = e?.From ?? 0, To = e?.To ?? 0 }).ToList()
    };

    internal static BlundervilleRouteGraph FromSections(BlundervilleRouteLibrary sections)
    {
        var graph = new BlundervilleRouteGraph();
        foreach (var section in sections.Routes)
            foreach (var point in section.Points.Skip(graph.Nodes.Count == 0 ? 0 : 1))
            {
                var id = graph.Nodes.Count + 1;
                graph.Nodes.Add(new() { Id = id, X = point.X, Z = point.Z, Mechanism = section.Name });
                if (id > 1) graph.Edges.Add(new() { From = id - 1, To = id });
            }
        return graph;
    }

    internal bool Connect(int from, int to)
    {
        if (from == to || !Nodes.Any(n => n.Id == from) || !Nodes.Any(n => n.Id == to)
            || Edges.Any(e => e.From == from && e.To == to)) return false;
        Edges.Add(new() { From = from, To = to }); return true;
    }

    internal void RemoveNode(int id)
    {
        Nodes.RemoveAll(n => n.Id == id); Edges.RemoveAll(e => e.From == id || e.To == id);
        // Deliberately do not connect former neighbours across the removed node.
    }

    internal string? Validate()
    {
        if (Version != 1) return "不支持的路网版本。";
        if (Nodes is not { Count: >= 2 and <= 256 } || Edges is not { Count: >= 1 and <= 1024 }) return "路网须有2–256个节点、1–1024条连线。";
        var ids = new HashSet<int>();
        foreach (var node in Nodes)
            if (node == null || node.Id <= 0 || !ids.Add(node.Id) || !float.IsFinite(node.X) || !float.IsFinite(node.Z)
                || MathF.Abs(node.X) > 18 || node.Z < EndZ - .01f || node.Z > StartZ + .01f || !Floor.Contains(node.Position, .25f))
                return "节点编号重复、坐标无效或节点超出可走地形。";
        foreach (var node in Nodes.Where(n => n.Exit))
            if (!ValidStairLane(node.X)) return $"入口节点{node.Id}的台阶通道不可走，请调整横向位置。";
        var byId = Nodes.ToDictionary(n => n.Id);
        if (!byId.TryGetValue(Start, out var start) || MathF.Abs(start.Z - StartZ) > .01f) return "请将起跑线Z=323上的一个节点设为起点。";
        var pairs = new HashSet<(int, int)>();
        foreach (var edge in Edges)
        {
            if (edge == null || !byId.TryGetValue(edge.From, out var a) || !byId.TryGetValue(edge.To, out var b)
                || edge.From == edge.To || !pairs.Add((edge.From, edge.To))) return "连线引用无效、重复或连接自身。";
            if (a.Exit) return $"出口节点{a.Id}不能再连出坡道路线。";
            if (Distance(a.Position, b.Position) < .1f || !Link(a.Position, b.Position)) return $"连线{a.Id} → {b.Id}穿过缺口／墙体或太贴边。";
        }
        HashSet<int> Flood(IEnumerable<int> seeds, bool reverse)
        {
            var seen = seeds.ToHashSet(); var queue = new Queue<int>(seen);
            while (queue.TryDequeue(out var id))
                foreach (var e in Edges.Where(e => (reverse ? e.To : e.From) == id))
                    if (seen.Add(reverse ? e.From : e.To)) queue.Enqueue(reverse ? e.From : e.To);
            return seen;
        }
        var reachable = Flood([Start], false);
        if (reachable.Count != Nodes.Count) return "有节点无法从起点沿箭头到达，请补连线或删除孤立节点。";
        var exits = Nodes.Where(n => n.Exit && !Edges.Any(e => e.From == n.Id)).Select(n => n.Id);
        if (Flood(exits, true).Count != Nodes.Count) return "有分支无法到达台阶入口（Z=155.2），请连接后续节点。";
        return null;
    }

    internal static bool ValidStairLane(float x) => float.IsFinite(x) && MathF.Abs(x) is >= 1.5f and <= 14
        // Validate the entrance and first tread. Upper treads may require a
        // lateral step: requiring one X to fit every floor row rejected the
        // usable outer half of the staircase. The hop planner checks each
        // later takeoff/landing against its actual support.
        && new[] { EndZ, 153f, 149 }
            .All(z => Link(new(x - .1f, 0, z), new(x + .1f, 0, z)));

    internal static IReadOnlyList<BlundervilleRouteNode> StairNodes(float x)
    {
        var side = x < 0 ? -1 : 1;
        return BlundervilleRoutes.Nodes(BlundervilleCourse.Final, new(side, 0, 0))
            .Where(n => n.Position.Z < EndZ).Select(n => n.Position.Z >= 136.6f ? n with { Position = n.Position with { X = x } } : n).ToArray();
    }

    internal Compiled Compile()
    {
        var positions = Nodes.ToDictionary(n => n.Id, n => n.Position);
        var links = Nodes.ToDictionary(n => n.Id, _ => new List<int>());
        var id = -1;
        foreach (var edge in Edges)
        {
            var a = positions[edge.From]; var b = positions[edge.To]; var previous = edge.From;
            // Collinear timing stops do not add junctions between different drawn edges.
            foreach (var z in Stops.Where(z => z > Math.Min(a.Z, b.Z) && z < Math.Max(a.Z, b.Z))
                .OrderBy(z => (z - a.Z) / (b.Z - a.Z)))
            {
                positions[id] = Vector3.Lerp(a, b, (z - a.Z) / (b.Z - a.Z)); links[id] = [];
                links[previous].Add(id); previous = id--;
            }
            links[previous].Add(edge.To);
        }
        return new(Start, positions, links.ToDictionary(p => p.Key, p => p.Value.ToArray()), Nodes.Where(n => n.Exit).Select(n => n.Id).ToHashSet());
    }

    internal sealed record Compiled(int Start, Dictionary<int, Vector3> Positions, Dictionary<int, int[]> Next, HashSet<int> Exits);
}
