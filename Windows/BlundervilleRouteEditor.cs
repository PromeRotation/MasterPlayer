using System.Numerics;
using Dalamud.Bindings.ImGui;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Settings;
using static MasterPlayer.Mechanics.Blunderville.BlundervillePresetGeometry;

namespace MasterPlayer.Windows;

internal sealed class BlundervilleRouteEditor
{
    private bool _visible, _dirty;
    private BlundervilleRouteGraph? _draft;
    private int _selected = -1, _drag = -1, _mode;
    private float _zoom = 1;
    private string _message = "", _mechanism = "新机制", _filter = "";
    private string? _error;
    private uint _revision, _validated = uint.MaxValue;
    private readonly Stack<BlundervilleRouteGraph> _undo = new();
    private readonly List<(Vector3 A, Vector3 B)> _floorRows = [];
    private BlundervilleGraphNode? Selected => _draft?.Nodes.FirstOrDefault(n => n.Id == _selected);
    // 0 = Final; otherwise BlundervilleCourseGraph.Editable[_course - 1].
    private int _course;
    private BlundervilleCourseGraph? _courseDraft;
    private readonly Stack<BlundervilleCourseGraph> _courseUndo = new();
    private BlundervilleCourse CourseKind => _course == 0 ? BlundervilleCourse.Final : BlundervilleCourseGraph.Editable[_course - 1];
    private BlundervilleCourseGraphNode? CourseSelected => _courseDraft?.Nodes.FirstOrDefault(n => n.Id == _selected);

    internal void Open()
    {
        _draft ??= MasterPlayerSettings.Instance.BlundervilleFinalRoute.EditGraph();
        _visible = true;
        if (_floorRows.Count != 0) return;
        for (var z = 122f; z < 325; z += .5f)
        {
            float? begin = null;
            for (var x = -19f; x <= 19; x += .5f)
            {
                var inside = x < 19 && Floor.Contains(new(x, 0, z), 0);
                if (inside && begin == null) begin = x;
                if (!inside && begin is { } start) { _floorRows.Add((new(start, 0, z), new(x, 0, z + .5f))); begin = null; }
            }
        }
    }

    private void BeforeEdit()
    {
        if (_draft == null) return;
        if (_undo.Count >= 60) _undo.Clear();
        _undo.Push(_draft.Copy()); Changed();
    }
    private void Changed() { _dirty = true; ++_revision; _message = ""; }
    private void Select(int id) { _selected = id; if (Selected is { } node) _mechanism = node.Mechanism; }
    private void Reload()
    {
        _draft = MasterPlayerSettings.Instance.BlundervilleFinalRoute.EditGraph();
        _undo.Clear(); _dirty = false; ++_revision; _selected = _drag = -1; _message = "已重新读取路网。";
    }

    internal void Draw()
    {
        if (!_visible || _draft == null) return;
        ImGui.SetNextWindowSize(new(1180, 720), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new(800, 550), new(2200, 1500));
        if (!ImGui.Begin("糖豆人 · 路网节点与连线", ref _visible)) { ImGui.End(); return; }
        try
        {
            string[] courses = ["第三关（决赛）", .. BlundervilleCourseGraph.Editable.Select(BlundervilleCourseGraph.Name)];
            for (var i = 0; i < courses.Length; ++i)
            {
                if (i != 0) ImGui.SameLine();
                if (ImGui.Button((_course == i ? "● " : "") + courses[i]) && _course != i)
                {
                    if (_dirty) _message = "上一张路网有未保存修改，已放弃。";
                    _course = i; _selected = _drag = -1; _dirty = false; _courseUndo.Clear(); _undo.Clear();
                    _courseDraft = _course == 0 ? null : BlundervilleCourseGraphs.Editing(MasterPlayerSettings.Instance.BlundervilleCourseGraphs, CourseKind);
                    ++_revision;
                }
            }
            if (_course != 0) { DrawCourse(); return; }
            var live = MasterPlayerSettings.Instance.BlundervilleFinalRoute;
            ImGui.TextDisabled(_dirty ? "有未保存修改" : live.Graph == null && live.Sections == null ? "编辑底稿：保存前不执行" : "当前保存的路网（原有分段路线已保留为连线）");
            ImGui.TextWrapped("每个节点可连向多个后继，也可汇合。运行到分叉节点后，只从你画出的箭头中择路；交叉线不会自动连通。台阶入口可有多个：在Z=155.2放节点，自定X。虚线仅示意入口方向，上台阶时可在同侧平台横移、斜跳并逐阶选择落点。");
            if (ImGui.Button("撤销") && _undo.TryPop(out var previous)) { _draft = previous; _selected = _drag = -1; Changed(); }
            ImGui.SameLine(); if (ImGui.Button("保存路网")) Save();
            ImGui.SameLine(); if (ImGui.Button("放弃未保存修改")) Reload();
            ImGui.SameLine(); if (ImGui.Button("载入按机制铺设路网"))
            {
                BeforeEdit(); _draft = BlundervilleMechanismGraph.Create(); _selected = _drag = -1;
                _message = "已载入机制路网草稿，可继续修改；点击保存路网后生效。";
            }
            string[] modes = ["选择／拖动节点", "画连线／分支", "在线上插入节点", "删除连线"];
            for (var i = 0; i < modes.Length; ++i)
            {
                if (i != 0) ImGui.SameLine();
                if (ImGui.Button((_mode == i ? "● " : "") + modes[i])) { _mode = i; _drag = -1; }
            }
            ImGui.TextWrapped(_mode switch
            {
                1 => "右键节点设为连线起点；左键空白处新增并连接，左键已有节点连接到它。可从同一节点反复画出不同分支。箭头方向就是行进方向。",
                2 => "点击已有连线，在该线上插入共享节点。相交但没有共用节点的线保持不连通。",
                3 => "点击连线删除；不会补连邻居。孤立节点需重新连接或删除后才能保存。",
                _ => "左键选择、拖动节点；右键删除节点及它的连线。空白处点击只取消选择，不自动加线。"
            });
            if (ImGui.BeginChild("##mechanisms", new(190, -1), true))
            {
                ImGui.TextUnformatted("机制分组（仅高亮）");
                if (ImGui.Selectable("全部", _filter == "")) _filter = "";
                foreach (var name in _draft.Nodes.Select(n => n.Mechanism).Distinct().ToArray())
                    if (ImGui.Selectable(string.IsNullOrWhiteSpace(name) ? "未命名" : name, _filter == name)) _filter = name;
                ImGui.Spacing();
                ImGui.TextWrapped("分组不限制连线。允许分叉、汇合及显式回头连线；每个分支必须最终能到达一个台阶入口。删除节点不会偷偷补线。");
            }
            ImGui.EndChild(); ImGui.SameLine();
            if (ImGui.BeginChild("##editing", new(-1, -1), false))
            {
                ImGui.SetNextItemWidth(200);
                var name = _mechanism;
                if (ImGui.InputText("机制名称／新节点分组", ref name, 61))
                {
                    if (Selected is { } selected) { BeforeEdit(); selected.Mechanism = name; }
                    _mechanism = name;
                }
                ImGui.SetNextItemWidth(160); ImGui.SliderFloat("地图缩放", ref _zoom, 1, 4, "%.1fx");
                DrawMap();
                if (Selected is { } node)
                {
                    var xy = new Vector2(node.X, node.Z);
                    ImGui.SetNextItemWidth(240);
                    if (ImGui.InputFloat2($"节点{node.Id}（X／Z）", ref xy)) { BeforeEdit(); node.X = xy.X; node.Z = xy.Y; }
                    ImGui.SameLine();
                    if (ImGui.Button("设为起跑节点")) { BeforeEdit(); _draft.Start = node.Id; }
                    ImGui.TextWrapped($"后继：{string.Join("、", _draft.Edges.Where(e => e.From == node.Id).Select(e => e.To))}" + (node.Exit ? "；台阶入口" : ""));
                }
                if (_validated != _revision) { _error = _draft.Validate(); _validated = _revision; }
                ImGui.TextWrapped(_error ?? $"{_draft.Nodes.Count}个节点，{_draft.Edges.Count}条连线，连通性及地形检查通过。");
                if (_message.Length != 0) ImGui.TextWrapped(_message);
            }
            ImGui.EndChild();
        }
        finally { ImGui.End(); }
    }

    private void Save()
    {
        if (_draft == null) return;
        if (_draft.Validate() is { } error) { _message = "未保存：" + error; return; }
        var settings = MasterPlayerSettings.Instance; var previous = settings.BlundervilleFinalRoute;
        settings.BlundervilleFinalRoute = new() { Graph = _draft.Copy(), Sections = previous.Sections, Left = previous.Left, Right = previous.Right };
        if (!settings.TrySave()) { settings.BlundervilleFinalRoute = previous; _message = "保存失败，运行路网未改变。"; return; }
        _dirty = false; _message = "路网已保存：到节点后只从已连接后继中选择。";
    }

    private static Vector3 SnapGraph(Vector3 p)
    {
        p.X = MathF.Round(Math.Clamp(p.X, -18, 18) * 10) / 10;
        p.Z = MathF.Round(Math.Clamp(p.Z, EndZ, StartZ) * 10) / 10;
        if (MathF.Abs(p.Z - StartZ) < 1) p.Z = StartZ;
        if (MathF.Abs(p.Z - EndZ) < 1) p.Z = EndZ;
        return p;
    }

    private BlundervilleGraphNode Add(Vector3 p)
    {
        var id = _draft!.Nodes.Count == 0 ? 1 : _draft.Nodes.Max(n => n.Id) + 1;
        var node = new BlundervilleGraphNode { Id = id, X = p.X, Z = p.Z, Mechanism = _mechanism };
        _draft.Nodes.Add(node); return node;
    }

    private void DrawMap()
    {
        var width = MathF.Max(500, ImGui.GetContentRegionAvail().X - 20) * _zoom;
        var scale = width / 208; var size = new Vector2(width, 42 * scale);
        if (ImGui.BeginChild("##map_scroll", new(-1, MathF.Min(350, size.Y + 28)), true, ImGuiWindowFlags.HorizontalScrollbar))
        {
            var origin = ImGui.GetCursorScreenPos();
            Vector2 Project(Vector3 p) => origin + new Vector2(325 - p.Z, p.X + 21) * scale;
            Vector3 World(Vector2 p) => new((p.Y - origin.Y) / scale - 21, 0, 325 - (p.X - origin.X) / scale);
            ImGui.InvisibleButton("##map", size); var hovered = ImGui.IsItemHovered();
            var mouse = ImGui.GetIO().MousePos; var draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(origin, origin + size, 0xff201b18);
            foreach (var (a, b) in _floorRows) draw.AddRectFilled(Project(a with { Z = b.Z }), Project(b with { Z = a.Z }), 0xff56514a);
            foreach (var strip in BlundervilleFinalPlanner.Final.Strips!)
                draw.AddRectFilled(Project(new(-18, 0, Math.Min(StartZ, strip.To))), Project(new(18, 0, strip.From)), 0x3348cc6c);
            foreach (var contour in Floor.GetContours())
                for (var i = 0; i < contour.Count; ++i) draw.AddLine(Project(contour[i]), Project(contour[(i + 1) % contour.Count]), 0xffc3b7a3);
            for (var z = 160; z <= 320; z += 20)
            {
                draw.AddLine(Project(new(-18, 0, z)), Project(new(18, 0, z)), 0x337f7f7f);
                draw.AddText(Project(new(-20, 0, z)), 0xffd8d8d8, z.ToString());
            }
            draw.AddLine(Project(new(-18, 0, StartZ)), Project(new(18, 0, StartZ)), 0xff7adade, 2);
            draw.AddLine(Project(new(-14, 0, EndZ)), Project(new(14, 0, EndZ)), 0xff7adade, 1);
            draw.AddText(origin + new Vector2(8, size.Y - 18), 0xffdddddd, "北向右；箭头为连通方向；黄色节点为自定台阶入口");
            var nodes = _draft!.Nodes.Where(n => float.IsFinite(n.X) && float.IsFinite(n.Z)).ToDictionary(n => n.Id);
            BlundervilleGraphEdge? nearestEdge = null; var edgeDistance = 8f; var edgePoint = Vector3.Zero;
            foreach (var edge in _draft.Edges)
            {
                if (!nodes.TryGetValue(edge.From, out var a) || !nodes.TryGetValue(edge.To, out var b)) continue;
                var from = Project(a.Position); var to = Project(b.Position); var delta = to - from;
                var color = _filter == "" || a.Mechanism == _filter || b.Mechanism == _filter ? 0xfff3ce63u : 0xff777777u;
                if (edge.From == _selected) color = 0xff73eaff;
                draw.AddLine(from, to, color, 2);
                if (delta.LengthSquared() > 1)
                {
                    var direction = Vector2.Normalize(delta); var tip = Vector2.Lerp(from, to, .65f);
                    var perpendicular = new Vector2(-direction.Y, direction.X);
                    draw.AddLine(tip, tip - direction * 7 + perpendicular * 4, color, 2);
                    draw.AddLine(tip, tip - direction * 7 - perpendicular * 4, color, 2);
                }
                var t = Math.Clamp(Vector2.Dot(mouse - from, delta) / MathF.Max(.001f, delta.LengthSquared()), 0, 1);
                var distance = Vector2.Distance(mouse, Vector2.Lerp(from, to, t));
                if (distance < edgeDistance) { edgeDistance = distance; nearestEdge = edge; edgePoint = Vector3.Lerp(a.Position, b.Position, t); }
            }
            var nearest = -1; var nodeDistance = 10f;
            foreach (var node in nodes.Values)
            {
                var p = Project(node.Position); var color = node.Id == _selected ? 0xff73eaffu : node.Exit ? 0xff66dddd : 0xfff3ce63;
                draw.AddCircleFilled(p, node.Id == _selected ? 5 : 3.5f, color);
                draw.AddText(p + new Vector2(5, -12), 0xffffffff, node.Id.ToString());
                if (node.Id == _draft.Start) draw.AddCircle(p, 8, 0xff7adade, 16, 2);
                if (node.Exit)
                    for (var z = EndZ; z > 137; z -= 2) draw.AddLine(Project(new(node.X, 0, z)), Project(new(node.X, 0, Math.Max(137, z - 1))), color, 1);
                var distance = Vector2.Distance(mouse, p);
                if (distance < nodeDistance) { nodeDistance = distance; nearest = node.Id; }
            }
            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                if (_mode == 0) { Select(nearest); if (nearest >= 0) { _drag = nearest; BeforeEdit(); } }
                else if (_mode == 1)
                {
                    if (nearest >= 0) { if (_selected >= 0 && _selected != nearest) { BeforeEdit(); _draft.Connect(_selected, nearest); } Select(nearest); }
                    else if (_draft.Nodes.Count < 256)
                    {
                        var p = SnapGraph(World(mouse));
                        if (Floor.Contains(p, 0)) { BeforeEdit(); var node = Add(p); if (_selected >= 0) _draft.Connect(_selected, node.Id); Select(node.Id); }
                    }
                }
                else if (nearestEdge != null && _mode == 2 && _draft.Nodes.Count < 256
                    && Distance(edgePoint, nodes[nearestEdge.From].Position) > .1f && Distance(edgePoint, nodes[nearestEdge.To].Position) > .1f)
                {
                    BeforeEdit(); var node = Add(edgePoint); _draft.Edges.Remove(nearestEdge);
                    _draft.Connect(nearestEdge.From, node.Id); _draft.Connect(node.Id, nearestEdge.To); Select(node.Id);
                }
                else if (nearestEdge != null && _mode == 3) { BeforeEdit(); _draft.Edges.Remove(nearestEdge); }
            }
            if (hovered && nearest >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                if (_mode == 1) Select(nearest);
                else if (_mode == 0) { BeforeEdit(); _draft.RemoveNode(nearest); _selected = _drag = -1; }
            }
            if (_mode == 0 && _drag >= 0 && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && nodes.TryGetValue(_drag, out var dragged))
            { var p = SnapGraph(World(mouse)); dragged.X = p.X; dragged.Z = p.Z; Changed(); }
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) _drag = -1;
            if (hovered) { var p = World(mouse); ImGui.SetTooltip($"X {p.X:F1} / Z {p.Z:F1}"); }
        }
        ImGui.EndChild();
    }

    private void CourseBeforeEdit()
    {
        if (_courseDraft == null) return;
        if (_courseUndo.Count >= 60) _courseUndo.Clear();
        _courseUndo.Push(_courseDraft.Copy()); Changed();
    }

    private void DrawCourse()
    {
        if (_courseDraft == null) return;
        var course = CourseKind;
        var saved = MasterPlayerSettings.Instance.BlundervilleCourseGraphs.ContainsKey(course.ToString());
        ImGui.TextDisabled(_dirty ? "有未保存修改" : saved ? "当前保存的路网" : "内置路网（与原固定路线一致，未保存也会执行）");
        ImGui.TextWrapped("一二关的路网决定按哪条车道走：到分叉节点时按当前机关预测选最早能到终点的分支。节点之间的躲避、跳跃、消失桥、黏液坡和搬运仍由各自的规划器处理。"
            + "标记“跳跃”的节点交给跳跃规划器；Y 是该节点所在平台高度。");
        if (ImGui.Button("撤销") && _courseUndo.TryPop(out var previous)) { _courseDraft = previous; _selected = _drag = -1; Changed(); }
        ImGui.SameLine(); if (ImGui.Button("保存路网")) SaveCourse();
        ImGui.SameLine();
        if (ImGui.Button("放弃未保存修改"))
        {
            _courseDraft = BlundervilleCourseGraphs.Editing(MasterPlayerSettings.Instance.BlundervilleCourseGraphs, course);
            _courseUndo.Clear(); _dirty = false; ++_revision; _selected = _drag = -1; _message = "已重新读取路网。";
        }
        ImGui.SameLine();
        if (ImGui.Button("载入内置路网")) { CourseBeforeEdit(); _courseDraft = BlundervilleCourseGraph.Default(course); _selected = _drag = -1; _message = "已载入内置路网草稿；点击保存路网后生效。"; }
        if (saved) { ImGui.SameLine(); if (ImGui.Button("删除已保存路网（改用内置）")) DeleteCourse(); }
        string[] modes = ["选择／拖动节点", "画连线／分支", "在线上插入节点", "删除连线"];
        for (var i = 0; i < modes.Length; ++i)
        {
            if (i != 0) ImGui.SameLine();
            if (ImGui.Button((_mode == i ? "● " : "") + modes[i])) { _mode = i; _drag = -1; }
        }
        ImGui.SetNextItemWidth(200);
        var name = _mechanism;
        if (ImGui.InputText("机制名称／新节点分组", ref name, 61))
        {
            if (CourseSelected is { } selected) { CourseBeforeEdit(); selected.Mechanism = name; }
            _mechanism = name;
        }
        ImGui.SameLine(); ImGui.SetNextItemWidth(160); ImGui.SliderFloat("地图缩放", ref _zoom, 1, 4, "%.1fx");
        DrawCourseMap(course);
        if (CourseSelected is { } node)
        {
            var xyz = new Vector3(node.X, node.Y, node.Z);
            ImGui.SetNextItemWidth(300);
            if (ImGui.InputFloat3($"节点{node.Id}（X／Y／Z）", ref xyz)) { CourseBeforeEdit(); node.X = xyz.X; node.Y = xyz.Y; node.Z = xyz.Z; }
            var radius = node.Radius; ImGui.SameLine(); ImGui.SetNextItemWidth(120);
            if (ImGui.SliderFloat("到达半径", ref radius, .2f, 4, "%.1fm")) { CourseBeforeEdit(); node.Radius = radius; }
            var jump = node.Jump; ImGui.SameLine();
            if (ImGui.Checkbox("跳跃节点", ref jump)) { CourseBeforeEdit(); node.Jump = jump; }
            ImGui.SameLine();
            if (ImGui.Button("设为起点")) { CourseBeforeEdit(); _courseDraft.Start = node.Id; }
            ImGui.TextWrapped($"后继：{string.Join("、", _courseDraft.Edges.Where(e => e.From == node.Id).Select(e => e.To))}");
        }
        if (_validated != _revision) { _error = _courseDraft.Validate(); _validated = _revision; }
        ImGui.TextWrapped(_error ?? $"{_courseDraft.Nodes.Count}个节点，{_courseDraft.Edges.Count}条连线，检查通过。");
        if (_message.Length != 0) ImGui.TextWrapped(_message);
    }

    private void SaveCourse()
    {
        if (_courseDraft == null) return;
        if (_courseDraft.Validate() is { } error) { _message = "未保存：" + error; return; }
        var settings = MasterPlayerSettings.Instance; var key = CourseKind.ToString();
        settings.BlundervilleCourseGraphs.TryGetValue(key, out var previous);
        settings.BlundervilleCourseGraphs[key] = _courseDraft.Copy();
        if (!settings.TrySave())
        {
            if (previous != null) settings.BlundervilleCourseGraphs[key] = previous; else settings.BlundervilleCourseGraphs.Remove(key);
            _message = "保存失败，运行路网未改变。"; return;
        }
        _dirty = false; _message = "路网已保存，下一次进入该关卡时生效。";
    }

    private void DeleteCourse()
    {
        var settings = MasterPlayerSettings.Instance; var key = CourseKind.ToString();
        if (!settings.BlundervilleCourseGraphs.Remove(key, out var previous)) return;
        if (!settings.TrySave()) { settings.BlundervilleCourseGraphs[key] = previous; _message = "删除失败。"; return; }
        _courseDraft = BlundervilleCourseGraph.Default(CourseKind); _courseUndo.Clear(); _dirty = false; ++_revision;
        _message = "已删除保存的路网，改用内置路网。";
    }

    private void DrawCourseMap(BlundervilleCourse course)
    {
        var boundary = BlundervilleRoutes.Boundary(course);
        var contours = boundary.GetContours();
        var points = contours.SelectMany(c => c).Concat(_courseDraft!.Nodes.Select(n => n.Position)).ToArray();
        float minX = points.Min(p => p.X) - 3, maxX = points.Max(p => p.X) + 3, minZ = points.Min(p => p.Z) - 3, maxZ = points.Max(p => p.Z) + 3;
        // Long courses run left to right along Z; round ones keep their aspect.
        var alongZ = maxZ - minZ >= maxX - minX;
        float spanU = alongZ ? maxZ - minZ : maxX - minX, spanV = alongZ ? maxX - minX : maxZ - minZ;
        var width = MathF.Max(500, ImGui.GetContentRegionAvail().X - 20) * _zoom;
        var scale = width / spanU; var size = new Vector2(width, spanV * scale);
        if (ImGui.BeginChild("##course_map", new(-1, MathF.Min(420, size.Y + 28)), true, ImGuiWindowFlags.HorizontalScrollbar))
        {
            var origin = ImGui.GetCursorScreenPos();
            Vector2 Project(Vector3 p) => origin + (alongZ ? new Vector2(maxZ - p.Z, p.X - minX) : new Vector2(p.X - minX, maxZ - p.Z)) * scale;
            Vector3 World(Vector2 p)
            {
                var q = (p - origin) / scale;
                return alongZ ? new(q.Y + minX, 0, maxZ - q.X) : new(q.X + minX, 0, maxZ - q.Y);
            }
            ImGui.InvisibleButton("##course_canvas", size); var hovered = ImGui.IsItemHovered();
            var mouse = ImGui.GetIO().MousePos; var draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(origin, origin + size, 0xff201b18);
            foreach (var contour in contours)
                for (var i = 0; i < contour.Count; ++i) draw.AddLine(Project(contour[i]), Project(contour[(i + 1) % contour.Count]), 0xffc3b7a3);
            draw.AddText(origin + new Vector2(8, size.Y - 18), 0xffdddddd, "箭头为连通方向；橙色为跳跃节点；圆圈为起点");
            var nodes = _courseDraft.Nodes.Where(n => float.IsFinite(n.X) && float.IsFinite(n.Z)).ToDictionary(n => n.Id);
            BlundervilleGraphEdge? nearestEdge = null; var edgeDistance = 8f; var edgePoint = Vector3.Zero; var edgeY = 0f;
            foreach (var edge in _courseDraft.Edges)
            {
                if (!nodes.TryGetValue(edge.From, out var a) || !nodes.TryGetValue(edge.To, out var b)) continue;
                var from = Project(a.Position); var to = Project(b.Position); var delta = to - from;
                var color = _filter == "" || a.Mechanism == _filter || b.Mechanism == _filter ? 0xfff3ce63u : 0xff777777u;
                if (edge.From == _selected) color = 0xff73eaff;
                draw.AddLine(from, to, color, 2);
                if (delta.LengthSquared() > 1)
                {
                    var direction = Vector2.Normalize(delta); var tip = Vector2.Lerp(from, to, .65f);
                    var perpendicular = new Vector2(-direction.Y, direction.X);
                    draw.AddLine(tip, tip - direction * 7 + perpendicular * 4, color, 2);
                    draw.AddLine(tip, tip - direction * 7 - perpendicular * 4, color, 2);
                }
                var t = Math.Clamp(Vector2.Dot(mouse - from, delta) / MathF.Max(.001f, delta.LengthSquared()), 0, 1);
                var distance = Vector2.Distance(mouse, Vector2.Lerp(from, to, t));
                if (distance < edgeDistance) { edgeDistance = distance; nearestEdge = edge; edgePoint = Vector3.Lerp(a.Position, b.Position, t); edgeY = a.Y + (b.Y - a.Y) * t; }
            }
            var nearest = -1; var nodeDistance = 10f;
            foreach (var node in nodes.Values)
            {
                var p = Project(node.Position); var color = node.Id == _selected ? 0xff73eaffu : node.Jump ? 0xff3399ff : 0xfff3ce63;
                draw.AddCircleFilled(p, node.Id == _selected ? 5 : 3.5f, color);
                draw.AddText(p + new Vector2(5, -12), 0xffffffff, node.Id.ToString());
                if (node.Id == _courseDraft.Start) draw.AddCircle(p, 8, 0xff7adade, 16, 2);
                var distance = Vector2.Distance(mouse, p);
                if (distance < nodeDistance) { nodeDistance = distance; nearest = node.Id; }
            }
            BlundervilleCourseGraphNode Add(Vector3 p, float y)
            {
                var id = _courseDraft.Nodes.Count == 0 ? 1 : _courseDraft.Nodes.Max(n => n.Id) + 1;
                var node = new BlundervilleCourseGraphNode { Id = id, X = MathF.Round(p.X * 10) / 10, Y = y, Z = MathF.Round(p.Z * 10) / 10, Mechanism = _mechanism };
                _courseDraft.Nodes.Add(node); return node;
            }
            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                if (_mode == 0) { _selected = nearest; if (CourseSelected is { } picked) _mechanism = picked.Mechanism; if (nearest >= 0) { _drag = nearest; CourseBeforeEdit(); } }
                else if (_mode == 1)
                {
                    if (nearest >= 0) { if (_selected >= 0 && _selected != nearest) { CourseBeforeEdit(); _courseDraft.Connect(_selected, nearest); } _selected = nearest; }
                    else if (_courseDraft.Nodes.Count < 256)
                    {
                        var p = World(mouse);
                        // A new node takes the platform height of the node it continues from.
                        var y = CourseSelected?.Y ?? BlundervilleRoutes.HeightHint(course, p.Z);
                        if (BlundervilleCourseGraph.OnFloor(course, p with { Y = y }, 1))
                        { CourseBeforeEdit(); var node = Add(p, y); if (_selected >= 0) _courseDraft.Connect(_selected, node.Id); _selected = node.Id; }
                    }
                }
                else if (nearestEdge != null && _mode == 2 && _courseDraft.Nodes.Count < 256
                    && Distance(edgePoint, nodes[nearestEdge.From].Position) > .1f && Distance(edgePoint, nodes[nearestEdge.To].Position) > .1f)
                {
                    CourseBeforeEdit(); var node = Add(edgePoint, edgeY); _courseDraft.Edges.Remove(nearestEdge);
                    _courseDraft.Connect(nearestEdge.From, node.Id); _courseDraft.Connect(node.Id, nearestEdge.To); _selected = node.Id;
                }
                else if (nearestEdge != null && _mode == 3) { CourseBeforeEdit(); _courseDraft.Edges.Remove(nearestEdge); }
            }
            if (hovered && nearest >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                if (_mode == 1) _selected = nearest;
                else if (_mode == 0) { CourseBeforeEdit(); _courseDraft.RemoveNode(nearest); _selected = _drag = -1; }
            }
            if (_mode == 0 && _drag >= 0 && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && nodes.TryGetValue(_drag, out var dragged))
            { var p = World(mouse); dragged.X = MathF.Round(p.X * 10) / 10; dragged.Z = MathF.Round(p.Z * 10) / 10; Changed(); }
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) _drag = -1;
            if (hovered) { var p = World(mouse); ImGui.SetTooltip($"X {p.X:F1} / Z {p.Z:F1}"); }
        }
        ImGui.EndChild();
    }
}
