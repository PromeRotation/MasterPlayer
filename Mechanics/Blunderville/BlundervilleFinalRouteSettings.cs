using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

public sealed class BlundervilleFinalRouteSettings
{
    // Old alternatives remain as data, never selected for execution.
    public List<BlundervilleRoutePoint>? Left { get; set; }
    public List<BlundervilleRoutePoint>? Right { get; set; }
    public List<BlundervilleRoutePreset>? Sections { get; set; }
    public BlundervilleRouteGraph? Graph { get; set; }
    internal float? TailLane { get; init; }

    internal BlundervilleRouteGraph EditGraph() => Graph?.Copy() ?? BlundervilleRouteGraph.FromSections(Edit());

    internal BlundervilleRouteGraph? AssignedGraph()
    {
        if (Graph != null) return Graph.Validate() == null ? Graph.Copy() : null;
        if (AssignedRoute() == null) return null;
        var migrated = BlundervilleRouteGraph.FromSections(Edit());
        return migrated.Validate() == null ? migrated : null;
    }

    internal BlundervilleRouteLibrary Edit() => new()
    {
        UsePresets = true,
        Routes = Sections?.Select(s => s?.Copy() ?? new() { Name = "", Points = [] }).ToList() ?? DraftSections()
    };

    internal static BlundervilleFinalRouteSettings FromEdit(BlundervilleRouteLibrary draft) => new()
    {
        Sections = draft.Routes.Select(s => s.Copy()).ToList()
    };

    internal static string? ValidateSections(BlundervilleRouteLibrary draft)
    {
        if (draft.Routes is not { Count: > 0 and <= 24 }) return "须指定 1–24 个机制段。";
        for (var i = 0; i < draft.Routes.Count; ++i)
        {
            var section = draft.Routes[i];
            if (section == null || string.IsNullOrWhiteSpace(section.Name) || section.Name.Length > 60)
                return $"第 {i + 1} 段须有 1–60 字的名称。";
            if (section.Points is not { Count: >= 2 } || section.Points.Any(p => p == null))
                return $"{section.Name}：至少需要两个路点。";
            if (i > 0 && BlundervillePresetGeometry.Distance(draft.Routes[i - 1].Points[^1].Position, section.Points[0].Position) > .01f)
                return $"第 {i}、{i + 1} 段没有接在同一个路点；不会自动补路。";
        }
        return BlundervillePresetGeometry.Validate(Join(draft));
    }

    internal static BlundervilleRoutePreset Join(BlundervilleRouteLibrary draft) => new()
    {
        Name = "指定的整关路线",
        Points = draft.Routes.SelectMany((s, i) => i == 0 ? s.Points : s.Points.Skip(1)).ToList()
    };

    internal BlundervilleRoutePreset? AssignedRoute()
    {
        if (Sections == null) return null;
        var draft = Edit();
        return ValidateSections(draft) == null ? Join(draft) : null;
    }

    internal IReadOnlyList<BlundervilleRouteNode> Nodes(Vector3 position)
    {
        if (TailLane is { } x) return BlundervilleRouteGraph.StairNodes(x);
        if (Graph != null) return []; // The graph planner owns all branch decisions.
        var route = AssignedRoute();
        if (route == null) return [];
        // The authored endpoint fixes the stair side; position cannot mirror it.
        var side = route.Points[^1].X < 0 ? -1 : 1;
        var original = BlundervilleRoutes.Nodes(BlundervilleCourse.Final, new(side, 0, 0));
        return route.Points.Select(p => new BlundervilleRouteNode(p.Position, .2f))
            .Concat(original.Where(n => n.Position.Z < BlundervillePresetGeometry.EndZ)).ToArray();
    }

    internal static void SetPoint(BlundervilleRouteLibrary draft, int section, int index, BlundervilleRoutePoint point)
    {
        var points = draft.Routes[section].Points;
        points[index] = point;
        if (index == 0 && section > 0) draft.Routes[section - 1].Points[^1] = BlundervilleRoutePoint.From(point.Position);
        if (index == points.Count - 1 && section + 1 < draft.Routes.Count)
            draft.Routes[section + 1].Points[0] = BlundervilleRoutePoint.From(point.Position);
    }

    internal static bool Split(BlundervilleRouteLibrary draft, int section, int point)
    {
        var current = draft.Routes[section];
        if (draft.Routes.Count >= 24 || point <= 0 || point >= current.Points.Count - 1) return false;
        var next = current.Copy(); next.Name = "新机制段"; next.Points = next.Points.Skip(point).ToList();
        current.Points = current.Points.Take(point + 1).ToList();
        draft.Routes.Insert(section + 1, next); return true;
    }

    internal static bool MergeNext(BlundervilleRouteLibrary draft, int section)
    {
        if (section + 1 >= draft.Routes.Count) return false;
        draft.Routes[section].Points.AddRange(draft.Routes[section + 1].Points.Skip(1));
        draft.Routes.RemoveAt(section + 1); return true;
    }

    private static List<BlundervilleRoutePreset> DraftSections()
    {
        // Editing scaffold only; AssignedRoute never executes this before save.
        var points = BlundervillePresetGeometry.Compile(BlundervillePresetGeometry.Reference(-1));
        string[] names = ["起跑与首组机关", "第一窄道机关", "第二窄道机关", "长坡与波浪", "台阶前机关"];
        List<BlundervilleRoutePreset> result = [];
        var from = 0;
        for (var i = 1; i < points.Length; ++i)
        {
            if (i != points.Length - 1 && !BlundervillePresetGeometry.Stops.Contains(points[i].Z)) continue;
            if (i != points.Length - 1 && result.Count > 0 && MathF.Abs(result[^1].Points[^1].Z - points[i].Z) < .01f) continue;
            result.Add(new() { Name = names[Math.Min(result.Count, names.Length - 1)],
                Points = points[from..(i + 1)].Select(BlundervilleRoutePoint.From).ToList() });
            from = i;
        }
        return result;
    }
}
