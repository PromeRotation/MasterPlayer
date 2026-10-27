using static MasterPlayer.Mechanics.Blunderville.BlundervillePresetGeometry;

namespace MasterPlayer.Mechanics.Blunderville;

// Authored against the measured floor and Final mechanism rows, not a runtime grid.
internal static class BlundervilleMechanismGraph
{
    internal static BlundervilleRouteGraph Create()
    {
        var graph = new BlundervilleRouteGraph();
        int Node(float x, float z, string group)
        {
            var id = graph.Nodes.Count + 1;
            graph.Nodes.Add(new() { Id = id, X = x, Z = z, Mechanism = group }); return id;
        }
        void Edge(int a, int b)
        {
            if (!Link(graph.Nodes[a - 1].Position, graph.Nodes[b - 1].Position))
                throw new InvalidOperationException($"Authored mechanism edge {a}->{b} crosses terrain.");
            graph.Connect(a, b);
        }
        int[] Row(float z, float[] xs, string group) => xs.Select(x => Node(x, z, group)).ToArray();
        void Straight(int[] from, int[] to) { for (var i = 0; i < from.Length; ++i) Edge(from[i], to[i]); }
        void Across(int[] row)
        {
            for (var i = 1; i < row.Length; ++i) { Edge(row[i - 1], row[i]); Edge(row[i], row[i - 1]); }
        }
        const string approach = "起跑与首组机关", first = "第一窄道机关", second = "第二窄道机关", wave = "长坡与波浪", final = "台阶前机关";
        var start = Node(0, StartZ, approach); var lead = Node(0, 290, approach); Edge(start, lead);
        var bends = Row(278, [-3.5f, 3.5f], approach); foreach (var b in bends) Edge(lead, b);
        var entry = Row(275, [-13.5f, -6, 6, 13.5f], first);
        Edge(bends[0], entry[1]); Edge(bends[1], entry[2]);
        Edge(entry[1], entry[0]); Edge(entry[2], entry[3]);
        // x=6 shortens exposure to the R5 circles at x=10; outer lanes are alternatives.
        var firstEnd = Row(260, [-13.5f, -6, 6, 13.5f], first); Straight(entry, firstEnd); Across(firstEnd);
        var secondEntry = Row(260, [-3.5f, 3.5f], second);
        Edge(firstEnd[1], secondEntry[0]); Edge(firstEnd[2], secondEntry[1]);
        Edge(secondEntry[0], firstEnd[1]); Edge(secondEntry[1], firstEnd[2]);
        var secondEnd = Row(236, [-13.5f, -3.5f, 3.5f, 13.5f], second);
        Edge(firstEnd[0], secondEnd[0]); Edge(secondEntry[0], secondEnd[1]);
        Edge(secondEntry[1], secondEnd[2]); Edge(firstEnd[3], secondEnd[3]); Across(secondEnd);
        // Circle rows at Z229/221, short-period circles at Z225: the ±5
        // seams reduce exposure. Wave columns (X=0/±6/±12) follow; place
        // bends at their row boundaries and keep both sides of the central pit.
        var previous = secondEnd;
        foreach (var z in new[] { 232f, 228, 224, 220, 216, 212, 208, 204, 200, 196, 192, 188, 184 })
        {
            float[] lanes = [-13.5f,-10,-6.5f,-5,-3.5f,0,3.5f,5,6.5f,10,13.5f];
            var row = lanes.Where(x => Floor.Contains(new(x,0,z), .3f)).Select(x => Node(x,z,wave)).ToArray();
            foreach (var from in previous)
                foreach (var to in row)
                {
                    var a = graph.Nodes[from-1].Position; var b = graph.Nodes[to-1].Position;
                    // Neighbouring channels only; no diagonal across the whole slope.
                    if (Math.Abs(a.X-b.X) <= 4
                        && Link(a,b)) Edge(from,to);
                }
            previous = row;
        }
        var rest = previous; Across(rest);
        // Alternating circles occupy different endpoints at Z176/170/162/156.
        // Put choices before each row; weave only one neighbouring lane per row.
        previous = rest;
        foreach (var z in new[] { 180f, 174, 168, 162, 158, EndZ })
        {
            float[] lanes = z == EndZ ? [-7,-5.5f,-4.5f,-4,4,4.5f,5.5f,7]
                : z == 158 ? [-12,-10,-7,-4,-1,1,4,7,10,12] : [-11,-6,-3,-1,1,3,6,11];
            var row = lanes.Where(x => Floor.Contains(new(x,0,z),.3f) && (z != EndZ || BlundervilleRouteGraph.ValidStairLane(x))).Select(x => Node(x,z,final)).ToArray();
            // Cross the alternating end-circle row while its corridor is
            // clear; do not strand an outer approach behind a single diagonal.
            if (z == 162) Across(row);
            foreach (var from in previous)
                foreach (var to in row)
                    if (Math.Abs(graph.Nodes[from-1].X-graph.Nodes[to-1].X) <= (z == EndZ ? 5.5f : 5f)
                        && Link(graph.Nodes[from-1].Position,graph.Nodes[to-1].Position)) Edge(from,to);
            previous = row;
        }
        // Discard terrain-cut candidate lanes from this authored layout only.
        // This construction runs in the editor/export, never during navigation.
        bool changed;
        do
        {
            var dead = graph.Nodes.Where(n => n.Id != graph.Start && !graph.Edges.Any(e => e.To == n.Id)
                || !n.Exit && !graph.Edges.Any(e => e.From == n.Id)).Select(n => n.Id).ToArray();
            changed = dead.Length > 0; foreach (var id in dead) graph.RemoveNode(id);
        } while (changed);
        if (graph.Validate() is { } error) throw new InvalidOperationException(error);
        return graph;
    }
}
