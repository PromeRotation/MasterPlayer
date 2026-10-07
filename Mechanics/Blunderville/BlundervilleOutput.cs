using System.Numerics;
using MasterPlayer.Scripting;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed class BlundervilleOutput(ScriptSafeZoneApi safeZone, ScriptDrawApi draw)
{
    private readonly HashSet<string> _shown = [];

    public void Apply(BlundervilleEncounter encounter, DateTime now, Vector3? player, Func<string, bool> enabled)
    {
        var wanted = new HashSet<string>();
        if (player is { } position && BlundervilleEncounter.Finite(position))
            foreach (var preview in encounter.Previews(now))
            {
                if (!enabled(preview.Group) || Vector3.DistanceSquared(position, preview.Geometry.Position) > 100 * 100) continue;
                using var group = safeZone.BeginDrawingGroup(preview.Group);
                wanted.Add(preview.Id);
                draw.AddAoePreview(preview.Id, preview.Geometry, false, preview.Outline, preview.Progress);
            }
        else encounter.Tick(now);
        foreach (var id in _shown.Where(id => !wanted.Contains(id))) draw.Remove(id);
        _shown.Clear(); _shown.UnionWith(wanted);
    }

    public void Clear()
    {
        foreach (var id in _shown) draw.Remove(id);
        _shown.Clear();
    }
}
