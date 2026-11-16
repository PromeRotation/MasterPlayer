using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Enuo;

internal sealed class EnuoOutput(ScriptSafeZoneApi safe, ScriptDrawApi draw, SafeZoneManager manager)
{
    private readonly Dictionary<string, (EnuoHazard Hazard, SafeZoneAreaHandle Handle)> _areas = [];
    private SafeZoneAreaHandle _goal;
    private EnuoGoal? _lastGoal;
    private CircleGeometry? _boundary;

    public void Apply(EnuoPlan plan, DateTime now)
    {
        if (_boundary?.Radius != plan.ArenaRadius)
        {
            _boundary = new(EnuoStrategy.Center, plan.ArenaRadius);
            manager.SetArenaBoundary(_boundary);
        }
        var wanted = plan.Hazards.Where(h => h.Until >= now).ToDictionary(h => h.Key);
        foreach (var key in _areas.Keys.Where(k => !wanted.ContainsKey(k)).ToArray()) Remove(key);
        foreach (var (key, hazard) in wanted)
        {
            using var group = safe.BeginDrawingGroup("恩欧 / 危险范围");
            if (!_areas.TryGetValue(key, out var old) || old.Hazard != hazard || !manager.ContainsArea(old.Handle))
            {
                Remove(key);
                var handle = safe.AddDangerSnapshot(hazard.Shape, new StaticTransform(hazard.Position, hazard.Rotation),
                    [new(hazard.From, hazard.Until, hazard.Telegraph)]);
                safe.SetNavigationOnly(handle, true);
                _areas[key] = (hazard, handle);
            }
            draw.AddAoePreview(key, hazard.Shape.Resolve(hazard.Position, hazard.Rotation, null), false, false, 1);
        }
        if (plan.Goal is { } goal && goal.Until >= now)
        {
            using var group = safe.BeginDrawingGroup("恩欧 / 视频打法");
            if (!manager.ContainsArea(_goal))
                _goal = safe.AddPersistentSafeArea(new CircleTemplate(goal.Radius), new StaticTransform(goal.Position, 0), 100, PositionPreference.Center);
            if (_lastGoal != goal)
                manager.UpdateArea(_goal, new CircleTemplate(goal.Radius), new StaticTransform(goal.Position, 0),
                    goalWindows: [new(now, goal.Until, 100, PositionPreference.Center)]);
            safe.SetNavigationOnly(_goal, true);
            draw.AddAoePreview("assignment", new CircleGeometry(goal.Position, goal.Radius), true, true, 1);
            _lastGoal = goal;
        }
        else ClearGoal();
    }

    public void Clear(DateTime now)
    {
        foreach (var key in _areas.Keys.ToArray()) Remove(key);
        ClearGoal();
        if (_boundary != null && ReferenceEquals(manager.BuildSnapshot(now).ArenaBoundary, _boundary)) manager.ClearArenaBoundary();
        _boundary = null;
    }
    private void ClearGoal() { if (_goal.IsValid) safe.Remove(_goal); _goal = default; _lastGoal = null; draw.Remove("assignment"); }
    private void Remove(string key) { if (_areas.Remove(key, out var old)) safe.Remove(old.Handle); draw.Remove(key); }
}
