using System.Numerics;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DungeonOutput(ScriptSafeZoneApi safeZone, ScriptDrawApi draw, SafeZoneManager manager)
{
    private readonly Dictionary<ulong, (DungeonPendingCast Cast, SafeZoneAreaHandle Handle)> _areas = [];
    private readonly HashSet<string> _previews = [];
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
        var casts = encounter.Casts.Values.Where(c => c.Rule.Shape != null).ToDictionary(c => c.Source);
        foreach (var source in _areas.Keys.Where(k => !casts.ContainsKey(k)).ToArray()) RemoveArea(source);
        foreach (var (source, cast) in casts)
        {
            using var group = safeZone.BeginDrawingGroup(encounter.Active!.Name + " / " + cast.Rule.Name);
            if (now <= cast.FinishAt.AddSeconds(0.6)
                && (!_areas.TryGetValue(source, out var previous) || !ReferenceEquals(previous.Cast, cast) || !manager.ContainsArea(previous.Handle)))
            {
                RemoveArea(source);
                var handle = safeZone.AddDangerSnapshot(cast.Rule.Shape!, new StaticTransform(cast.Location, cast.Rotation),
                    [new(cast.FinishAt, cast.FinishAt.AddSeconds(0.6), cast.StartedAt)]);
                safeZone.SetNavigationOnly(handle, true);
                _areas[source] = (cast, handle);
            }
            var id = $"cast/{source}";
            wanted.Add(id);
            draw.AddAoePreview(id, cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null), false, false, 1);
        }
        if (ripplesEnabled)
        {
            using var group = safeZone.BeginDrawingGroup("TrashSpawning");
            foreach (var actor in encounter.ActiveRipples)
            {
                var id = $"ripple/{actor.Id}";
                wanted.Add(id);
                draw.AddAoePreview(id, new RectangleGeometry(actor.Position, 0, 4, 6), true, true, 1);
            }
        }
        foreach (var id in _previews.Where(id => !wanted.Contains(id))) draw.Remove(id);
        _previews.Clear();
        _previews.UnionWith(wanted);
    }

    public void Clear()
    {
        foreach (var source in _areas.Keys.ToArray()) RemoveArea(source);
        foreach (var id in _previews) draw.Remove(id);
        _previews.Clear();
        if (_boundary != null && ReferenceEquals(manager.BuildSnapshot(DateTime.UtcNow).ArenaBoundary, _boundary)) manager.ClearArenaBoundary();
        _boundary = null;
    }

    private void RemoveArea(ulong source)
    {
        if (_areas.Remove(source, out var area)) safeZone.Remove(area.Handle);
    }
}
