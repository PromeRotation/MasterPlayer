using System.Numerics;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DungeonOutput(ScriptSafeZoneApi safeZone, ScriptDrawApi draw, SafeZoneManager manager,
    Action<IReadOnlyList<SolverTeleporter>>? publishTeleporters = null)
{
    private readonly Dictionary<string, (DungeonPendingCast Cast, SafeZoneAreaHandle Handle)> _areas = [];
    private readonly HashSet<string> _previews = [];
    private readonly Dictionary<string, (DungeonAmbientAoe Aoe, SafeZoneAreaHandle Handle)> ambientAreas = [];
    private IGeometryShape? _boundary;

    public void Apply(DungeonEncounter encounter, DateTime now, bool ripplesEnabled)
    {
        if (!ReferenceEquals(_boundary, encounter.Arena))
        {
            Clear();
            _boundary = encounter.Arena;
            if (_boundary != null) manager.SetArenaBoundary(_boundary);
        }
        var wanted = new HashSet<string>();
        var casts = encounter.OutputCasts.Where(c => c.Value.Rule.Shape != null).ToDictionary();
        foreach (var source in _areas.Keys.Where(k => !casts.ContainsKey(k)).ToArray()) RemoveArea(source);
        foreach (var (source, cast) in casts)
        {
            using var group = safeZone.BeginDrawingGroup(encounter.Active!.Name + " / " + cast.Rule.Name);
            if ((cast.UntilObservedEnd || now <= cast.FinishAt.AddSeconds(0.6))
                && (!_areas.TryGetValue(source, out var previous) || !ReferenceEquals(previous.Cast, cast) || !manager.ContainsArea(previous.Handle)))
            {
                RemoveArea(source);
                var handle = safeZone.AddDangerSnapshot(cast.Rule.Shape!, new StaticTransform(cast.Location, cast.Rotation),
                    [new(cast.FinishAt, cast.UntilObservedEnd ? null : cast.FinishAt.AddSeconds(0.6), cast.StartedAt)]);
                safeZone.SetNavigationOnly(handle, true);
                _areas[source] = (cast, handle);
            }
            var id = source;
            wanted.Add(id);
            draw.AddAoePreview(id, cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null), false, false, 1);
        }
        var ambient = encounter.AmbientAoes().ToArray();
        var ambientIds = ambient.Where(a => a.Navigate).Select(a => a.Id).ToHashSet();
        foreach (var id in ambientAreas.Keys.Where(id => !ambientIds.Contains(id)).ToArray())
        {
            safeZone.Remove(ambientAreas[id].Handle);
            ambientAreas.Remove(id);
        }
        foreach (var aoe in ambient)
        {
            using var group = safeZone.BeginDrawingGroup(encounter.Active!.Name + " / " + aoe.Name);
            if (aoe.Navigate && (!ambientAreas.TryGetValue(aoe.Id, out var previous) || previous.Aoe != aoe || !manager.ContainsArea(previous.Handle)))
            {
                if (ambientAreas.Remove(aoe.Id, out previous)) safeZone.Remove(previous.Handle);
                var binding = new StaticTransform(aoe.Position, aoe.Rotation);
                SafeZoneAreaHandle handle;
                if (aoe.Obstacle)
                    handle = safeZone.AddObstacle(aoe.Shape, binding);
                else if (aoe.Safe)
                {
                    handle = safeZone.AddPersistentSafeArea(aoe.Shape, binding, aoe.GoalPriority);
                    manager.UpdateArea(handle, goalWindows: [new GoalWindow(now, null, aoe.GoalPriority,
                        aoe.PreferredGoalPosition != null ? PositionPreference.Center : PositionPreference.Nearest)
                    {
                        OverrideDangerMargin = aoe.OverrideDangerMargin,
                        PreferredPosition = aoe.PreferredGoalPosition,
                    }]);
                }
                else if (aoe.Activation is { } activation)
                    handle = safeZone.AddDangerSnapshot(aoe.Shape, binding, [new(activation, null, now)]);
                else
                    handle = safeZone.AddPersistentDanger(aoe.Shape, binding);
                safeZone.SetNavigationOnly(handle, true);
                ambientAreas[aoe.Id] = (aoe, handle);
            }
            if (!aoe.Display) continue;
            wanted.Add(aoe.Id);
            draw.AddAoePreview(aoe.Id, aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null), aoe.Safe, aoe.OutlineOnly, 1, aoe.Unclipped, aoe.Color);
        }
        foreach (var gaze in encounter.Gazes)
        {
            if (gaze.Rule.Action is 36297 or 9613 or 16025 or 36624 or 3281) continue;
            using var group = safeZone.BeginDrawingGroup("PetrifyingEye / 背对视线");
            var id = $"gaze/{gaze.Source}";
            wanted.Add(id);
            draw.AddAoePreview(id, new CircleGeometry(gaze.Location, .7f), false, true, 1);
        }
        if (ripplesEnabled)
        {
            using var group = safeZone.BeginDrawingGroup("TrashSpawning");
            foreach (var actor in encounter.ActiveRipples)
            {
                var id = $"ripple/{actor.Id}";
                wanted.Add(id);
            draw.AddAoePreview(id, new RectangleGeometry(actor.Position, 0, 4, 6), true, true, 1,
                color: new Vector4(.7529412f, .7529412f, .7529412f, 1));
            }
        }
        foreach (var id in _previews.Where(id => !wanted.Contains(id))) draw.Remove(id);
        _previews.Clear();
        _previews.UnionWith(wanted);
        publishTeleporters?.Invoke(encounter.NativeTeleporters);
    }

    public void Clear()
    {
        publishTeleporters?.Invoke([]);
        foreach (var source in _areas.Keys.ToArray()) RemoveArea(source);
        foreach (var id in _previews) draw.Remove(id);
        _previews.Clear();
        foreach (var area in ambientAreas.Values) safeZone.Remove(area.Handle);
        ambientAreas.Clear();
        if (_boundary != null && ReferenceEquals(manager.BuildSnapshot(DateTime.UtcNow).ArenaBoundary, _boundary)) manager.ClearArenaBoundary();
        _boundary = null;
    }

    private void RemoveArea(string source)
    {
        if (_areas.Remove(source, out var area)) safeZone.Remove(area.Handle);
    }
}
