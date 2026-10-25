using System.Numerics;
using MasterPlayer.Follow;
using MasterPlayer.Scripting;
using PromeRotation.Spatial.Drawing;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Crucible;

internal sealed class CrucibleOutput(ScriptSafeZoneApi safeZone, ScriptDrawApi draw, SafeZoneManager manager, Action<string> log, Func<float>? dangerMargin = null)
{
    private readonly Dictionary<string, (string Signature, SafeZoneAreaHandle Handle)> _areas = [];
    private readonly HashSet<string> _drawIds = [];
    private readonly Dictionary<string, DateTime> _messages = [];
    private readonly Dictionary<string, IGeometryShape> _shapeGeometryCache = new(StringComparer.Ordinal);
    private IGeometryShape? _boundary;
    private IGeometryShape? _displayBoundary;
    private ArenaBounds? _bounds;
    private WPos _center;
    private int _generation = -1;
    private ArenaBounds? _goalBounds;
    private WPos _goalCenter;
    private readonly Dictionary<WPos, bool> _goalFloor = [];

    internal (IGeometryShape Boundary, Vector3 Center)? GetRadarArena(IGeometryShape? navigationBoundary)
        => _boundary != null && _displayBoundary != null && ReferenceEquals(navigationBoundary, _boundary)
            ? (_displayBoundary, _center.ToVec3(_displayBoundary.Position.Y)) : null;

    public void Clear()
    {
        foreach (var area in _areas.Values) safeZone.Remove(area.Handle);
        _areas.Clear();
        foreach (var id in _drawIds) draw.Remove(id);
        _drawIds.Clear();
        _shapeGeometryCache.Clear();
        if (_boundary != null && ReferenceEquals(manager.BuildSnapshot(DateTime.UtcNow).ArenaBoundary, _boundary)) manager.ClearArenaBoundary();
        _boundary = null; _displayBoundary = null; _bounds = null; _messages.Clear();
        _goalBounds = null; _goalFloor.Clear();
    }
    public void Apply(CrucibleEngine engine, CrucibleFrame frame, float y, bool dangerEnabled, bool goalsEnabled, bool boundaryEnabled)
    {
        if (engine.Generation != _generation) { Clear(); _generation = engine.Generation; }
        if (engine.Module is not { } module || engine.World.Player is not { } player) { Clear(); return; }
        var now = engine.World.CurrentTime;
        if (boundaryEnabled && frame.Hints.PathfindMapBounds is { } bounds)
        {
            if (_bounds != bounds || _center != module.Center || _boundary == null)
            {
                _bounds = bounds; _center = module.Center; _boundary = bounds.Geometry(module.Center, y);
                _displayBoundary = (module.Field?.DisplayBounds ?? module.Arena.Bounds).Geometry(module.Center, y);
                manager.SetArenaBoundary(_boundary);
            }
        }
        else if (_boundary != null)
        {
            if (ReferenceEquals(manager.BuildSnapshot(now).ArenaBoundary, _boundary)) manager.ClearArenaBoundary();
            _boundary = null; _displayBoundary = null; _bounds = null;
        }
        var desired = new Dictionary<string, Area>();
        var previews = new HashSet<string>();
        var previewGeometries = new HashSet<int>();
        var zoneGeometries = new Dictionary<AIHints.ForbiddenZone, IGeometryShape>();
        IGeometryShape Geometry(AIHints.ForbiddenZone zone)
        {
            if (!zoneGeometries.TryGetValue(zone, out var geometry))
            {
                var key = zone.Shape.ContentKey();
                if (key != null && _shapeGeometryCache.TryGetValue(key, out geometry))
                {
                    zoneGeometries[zone] = geometry;
                    return geometry;
                }
                zoneGeometries[zone] = geometry = ZoneGeometry(zone, module, y);
                if (key != null)
                {
                    if (_shapeGeometryCache.Count >= 512)
                        _shapeGeometryCache.Clear();
                    _shapeGeometryCache[key] = geometry;
                }
            }
            return geometry;
        }
        IGeometryShape ShapeGeometry(ShapeDistance shape)
        {
            var key = shape.ContentKey();
            if (key != null && _shapeGeometryCache.TryGetValue(key, out var cached))
                return cached;
            var geometry = shape.Geometry(y) ?? FrozenConstraintGeometry.Create(p => shape.Distance(p), module.Center, y);
            if (key != null)
            {
                if (_shapeGeometryCache.Count >= 512)
                    _shapeGeometryCache.Clear();
                _shapeGeometryCache[key] = geometry;
            }
            return geometry;
        }
        if (dangerEnabled)
        {
            var i = 0;
            foreach (var zone in frame.Hints.ForbiddenZones)
            {
                if (zone.Activation == DateTime.MaxValue) continue;
                var geometry = Geometry(zone);
                desired[$"danger/{i++}"] = new(geometry, zone.Activation, AreaKind.Danger, ExpiresAt: zone.ExpiresAt, ExactGeometry: zone.ExactGeometry);
            }
            i = 0;
            foreach (var shape in frame.Hints.TemporaryObstacles)
            {
                var geometry = ShapeGeometry(shape);
                desired[$"obstacle/{i++}"] = new(geometry, default, AreaKind.Obstacle);
            }
            i = 0;
            foreach (var (component, aoe) in frame.Aoes)
            {
                using var group = safeZone.BeginDrawingGroup(component);
                var id = $"aoe/{i++}";
                var progress = aoe.Activation == default ? 1 : Math.Clamp(1 - (float)(aoe.Activation - now).TotalSeconds / 8, 0, 1);
                var geometry = aoe.Shape.Geometry(aoe.Origin, aoe.Rotation, y);
                previewGeometries.Add(Area.GeometrySignature(geometry));
                draw.AddAoePreview(id, geometry, aoe.Color == Colors.SafeFromAOE, !aoe.Risky && aoe.Color != Colors.SafeFromAOE, progress);
                previews.Add(id);
            }
        }
        if (goalsEnabled)
        {
            var i = 0;
            foreach (var point in frame.Hints.GoalPoints)
                desired[$"goal/{i++}"] = new(new CircleGeometry(point.Position.ToVec3(y), point.Radius), default, AreaKind.Goal, point.Priority, PositionPreference.Center);
            if (frame.Hints.GoalPoints.Count == 0 && frame.Hints.GoalZones.Count > 0 && player.CastInfo == null)
            {
                var exclusions = frame.Hints.ForbiddenZones
                    .Where(zone => OptionalGoalSafety.Pending(zone.Activation, zone.ExpiresAt, now))
                    .Select(zone => (Zone: zone, Mask: new GoalExclusion(new ResolvedGeometryTemplate(Geometry(zone))
                        .Inflate(zone.ExactGeometry ? 0 : dangerMargin?.Invoke() ?? 0).Shape))).ToArray();
                var urgent = exclusions.Any(e => OptionalGoalSafety.Urgent(e.Zone.Activation, e.Zone.ExpiresAt, now) && e.Mask.Contains(player.Position.ToVec3(y)));
                if (!urgent)
                {
                    if (_goalBounds != module.Arena.Bounds || _goalCenter != module.Center)
                    {
                        _goalBounds = module.Arena.Bounds; _goalCenter = module.Center; _goalFloor.Clear();
                    }
                    // Floor topology is immutable until the bounds change. Cache only
                    // that geometry; dynamic goals and danger masks are evaluated afresh.
                    bool OnFloor(WPos p)
                    {
                        if (!_goalFloor.TryGetValue(p, out var inside)) _goalFloor[p] = inside = module.Arena.InBounds(p);
                        return inside;
                    }
                    var extent = module.Arena.Bounds.Geometry(module.Center, y).GetBounds();
                    var radius = MathF.Ceiling(Math.Max(Math.Max(module.Center.X - extent.MinX, extent.MaxX - module.Center.X),
                        Math.Max(module.Center.Z - extent.MinZ, extent.MaxZ - module.Center.Z)));
                    var goals = frame.Hints.GoalZones;
                    var totals = new int[goals.Count];
                    var safeCounts = new int[goals.Count];
                    bool Safe(WPos p) => !exclusions.Any(e => e.Mask.Contains(p.ToVec3(y)))
                        && !frame.Hints.TemporaryObstacles.Any(o => o.Distance(p) <= 0);
                    // Measure each original preference before clipping, as BMR does.
                    for (var z = -radius; z <= radius; z += OptionalGoalSafety.SampleStep)
                        for (var x = -radius; x <= radius; x += OptionalGoalSafety.SampleStep)
                        {
                            var p = module.Center + new WDir(x,z);
                            if (!OnFloor(p)) continue;
                            bool? safe = null;
                            for (var g = 0; g < goals.Count; ++g)
                                if (goals[g](p) > 0)
                                {
                                    ++totals[g];
                                    if (safe ??= Safe(p)) ++safeCounts[g];
                                }
                        }
                    var accepted = Enumerable.Range(0,goals.Count)
                        .Where(g => totals[g] > 0 && safeCounts[g] >= totals[g] * OptionalGoalSafety.MinSafeFraction).ToArray();
                    float Score(WPos p)
                    {
                        var score = 0f;
                        foreach (var g in accepted) score += Math.Max(0,goals[g](p));
                        return score > 0 && Safe(p) ? score : 0;
                    }
                    var max = 0f;
                    for (var z = -radius; z <= radius; z += OptionalGoalSafety.SampleStep)
                        for (var x = -radius; x <= radius; x += OptionalGoalSafety.SampleStep)
                        {
                            var p = module.Center + new WDir(x,z);
                            if (OnFloor(p)) max = Math.Max(max,Score(p));
                        }
                    if (max > 0)
                    {
                        var threshold = max * 0.9f;
                        var geometry = FrozenConstraintGeometry.Create(p => OnFloor(p) ? threshold - Score(p) : 1, module.Center, y, radius: radius, visible: true, safeRegion: true);
                        desired[$"goal/{i}"] = new(geometry, default, AreaKind.Goal, 1, PositionPreference.Nearest);
                    }
                }
            }
        }

        // Remove every replaced/expired registration before adding its replacement, including bait points.
        var signatures = desired.ToDictionary(p => p.Key, p => p.Value.Signature());
        foreach (var (id, old) in _areas.ToArray())
            if (!signatures.TryGetValue(id, out var signature) || old.Signature != signature || !manager.ContainsArea(old.Handle))
            {
                safeZone.Remove(old.Handle); _areas.Remove(id);
            }
        using (safeZone.BeginDrawingGroup("斗兽奇弈走位"))
            foreach (var (id, area) in desired)
            {
                if (_areas.ContainsKey(id)) continue;
                var template = new ResolvedGeometryTemplate(area.Geometry);
                var transform = new StaticTransform(area.Geometry.Position, 0);
                SafeZoneAreaHandle handle;
                if (area.Kind == AreaKind.Obstacle) handle = safeZone.AddObstacle(template, transform);
                else if (area.Kind == AreaKind.Goal) handle = safeZone.AddPersistentSafeArea(template, transform, area.Priority, area.Preference);
                else
                {
                    var start = area.Activation == default ? now : area.Activation;
                    // The producer distinguishes a finite hit from a positioning deadline.
                    var end = area.ExpiresAt;
                    if (end < now) continue;
                    handle = area.ExactGeometry
                        ? safeZone.AddExactDangerSnapshot(template, transform, [new(start, end, now)])
                        : safeZone.AddDangerSnapshot(template, transform, [new(start, end, now)]);
                }
                _areas[id] = (signatures[id], handle);
            }

        // A precise script preview owns its appearance; do not paint the inflated
        // navigation copy over it. Constraints without a preview remain visible.
        foreach (var (id, area) in desired)
            if (_areas.TryGetValue(id, out var registered))
                safeZone.SetNavigationOnly(registered.Handle, area.Kind == AreaKind.Danger && previewGeometries.Contains(area.GeometryHash));

        var lineIndex = 0;
        foreach (var line in module.Arena.Lines)
        {
            var id = $"line/{lineIndex++}";
            draw.AddLinePreview(id, line.Start.ToVec3(y), line.End.ToVec3(y), line.Color is Colors.Safe or Colors.SafeFromAOE);
            previews.Add(id);
        }
        var shapeIndex = 0;
        foreach (var shape in module.Arena.Shapes)
        {
            if (frame.Aoes.Any(a => a.Aoe.Shape == shape.Shape && a.Aoe.Origin == shape.Origin && a.Aoe.Rotation == shape.Rotation)) continue;
            var id = $"marker/{shapeIndex++}";
            draw.AddAoePreview(id, shape.Shape.Geometry(shape.Origin, shape.Rotation, y), shape.Color == Colors.Safe || shape.Color == Colors.SafeFromAOE, true, 1);
            previews.Add(id);
        }
        foreach (var message in frame.Text.Select(t => t.Text).Concat(frame.Global).Concat(frame.Hints.ActionHints)) Hint(message, now);
        if (frame.Hints.ForcedTarget is { } target) Hint($"优先攻击：{ActorName(target)}", now);
        foreach (var enemy in frame.Hints.PotentialTargets.Where(e => e.Priority != 0 || e.ForbidDOTs || e.Spikes || e.ShouldBeDispelled))
        {
            if (enemy.Priority > 0) Hint($"优先攻击：{ActorName(enemy.Actor)}", now);
            if (enemy.Priority < 0 || enemy.Spikes) Hint($"暂缓攻击：{ActorName(enemy.Actor)}", now);
            if (enemy.ForbidDOTs) Hint($"停止对 {ActorName(enemy.Actor)} 施加持续伤害", now);
            if (enemy.ShouldBeDispelled) Hint($"驱散 {ActorName(enemy.Actor)} 的强化状态", now);
        }
        if (frame.Hints.ForceCancelCast) Hint("机制即将判定：停止读条，按指引调整朝向", now);
        foreach (var label in module.Arena.Texts)
        {
            var offset = label.Position - module.Center;
            var direction = (offset.Z < -2 ? "北" : offset.Z > 2 ? "南" : "") + (offset.X < -2 ? "西" : offset.X > 2 ? "东" : "");
            Hint($"{label.Text}：{(direction.Length == 0 ? "中央" : direction)}，距场心 {offset.Length():F0} 米", now);
        }
        if (frame.Hints.InteractWithTarget is { } interact) Hint($"记忆题答案已就位：请交互 {interact.Name}", now);
        if (frame.Hints.DesiredFacing is { } facing)
        {
            Hint($"强制位移即将判定：按指引面向调整朝向（{facing.Deg:F0}°）", now);
            var direction = facing.ToDirection();
            var end = player.Position + direction * 8;
            draw.AddLinePreview("facing", player.Position.ToVec3(y), end.ToVec3(y), true);
            draw.AddLinePreview("facing/left", end.ToVec3(y), (end - direction * 1.5f + direction.OrthoR() * 0.8f).ToVec3(y), true);
            draw.AddLinePreview("facing/right", end.ToVec3(y), (end - direction * 1.5f - direction.OrthoR() * 0.8f).ToVec3(y), true);
            previews.UnionWith(["facing", "facing/left", "facing/right"]);
        }
        foreach (var id in _drawIds.Except(previews)) draw.Remove(id);
        _drawIds.Clear(); _drawIds.UnionWith(previews);
        foreach (var key in _messages.Where(p => p.Value < now.AddMinutes(-1)).Select(p => p.Key).ToArray()) _messages.Remove(key);
    }
    private static IGeometryShape ZoneGeometry(AIHints.ForbiddenZone zone, BossModule module, float y)
        => zone.Shape is SDInvertedCircle circle
            ? new CircleComplementGeometry(circle.Origin.ToVec3(y), circle.Radius, module.Arena.Bounds.Geometry(module.Center, y).GetBounds())
            : zone.Shape.Geometry(y) ?? FrozenConstraintGeometry.Create(p => zone.Shape.Distance(p), module.Center, y);
    private static string ActorName(Actor actor) => string.IsNullOrWhiteSpace(actor.Name) ? $"目标 {actor.NameID}" : actor.Name;
    private void Hint(string text, DateTime now)
    {
        if (_messages.TryGetValue(text, out var expiry) && expiry > now) return;
        _messages[text] = now.AddSeconds(8);
        log(text);
    }
    private sealed record Area(IGeometryShape Geometry, DateTime Activation, AreaKind Kind, int Priority = 0, PositionPreference Preference = PositionPreference.Nearest, DateTime? ExpiresAt = null, bool ExactGeometry = false)
    {
        public int GeometryHash { get; } = GeometrySignature(Geometry);
        public string Signature()
            => $"{GeometryHash}:{Activation.Ticks}:{ExpiresAt?.Ticks}:{Kind}:{Priority}:{Preference}:{ExactGeometry}";
        public static int GeometrySignature(IGeometryShape geometry)
        {
            var hash = new HashCode();
            hash.Add(geometry.Kind); hash.Add(geometry.Position.X); hash.Add(geometry.Position.Z); hash.Add(geometry.Rotation); hash.Add(geometry.GetBounds());
            if (geometry is CircleComplementGeometry circle) hash.Add(circle.SafeRadius);
            else if (geometry is FrozenConstraintGeometry grid) hash.Add(grid.ContentHash);
            else foreach (var contour in geometry.GetContours(24)) foreach (var p in contour) { hash.Add(p.X); hash.Add(p.Z); }
            return hash.ToHashCode();
        }
    }
}
