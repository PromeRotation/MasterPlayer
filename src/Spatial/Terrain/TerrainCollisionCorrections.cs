using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal sealed class TerrainCollisionCorrections
{
    private sealed record Entry(TerrainObstruction Obstruction, DateTime NextCheck);
    private readonly List<Entry> entries = [];
    private LocalTerrainBoundary? source;
    private LocalTerrainBoundary? published;
    private bool changed;
    internal int Count => entries.Count;
    internal TerrainObstruction[] Snapshot() => entries.Select(e => e.Obstruction).ToArray();

    internal void Record(TerrainObstruction obstruction, DateTime now)
    {
        var index = entries.FindIndex(e => Vector3.DistanceSquared(e.Obstruction.Position, obstruction.Position) < .01f);
        if (index >= 0)
        {
            changed |= entries[index].Obstruction.Kind != obstruction.Kind;
            // Keep the published location stable when repeated queries differ only by roundoff.
            obstruction = obstruction with { Position = entries[index].Obstruction.Position };
            entries[index] = new(obstruction, now.AddMilliseconds(500));
        }
        else
        {
            entries.Add(new(obstruction, now.AddMilliseconds(500)));
            changed = true;
        }
    }

    internal void Refresh(ITerrainProbe probe, Vector3 origin, float radius, DateTime now)
    {
        // A cached room must retain learned collisions when revisited. Only probe nearby, loaded space.
        var index = entries.FindIndex(e => e.NextCheck <= now
            && Vector2.DistanceSquared(new(e.Obstruction.Position.X, e.Obstruction.Position.Z), new(origin.X, origin.Z))
                <= (radius + 4) * (radius + 4));
        if (index < 0) return;
        var entry = entries[index];
        if (probe.CanTraverse(entry.Obstruction.From, entry.Obstruction.To, out var obstruction))
        {
            entries.RemoveAt(index);
            changed = true;
        }
        else
        {
            var next = obstruction ?? entry.Obstruction;
            changed |= next.Kind != entry.Obstruction.Kind || Vector3.DistanceSquared(next.Position, entry.Obstruction.Position) >= .0001f;
            entries[index] = new(next, now.AddMilliseconds(500));
        }
    }

    internal LocalTerrainBoundary? Apply(LocalTerrainBoundary? boundary)
    {
        if (!changed && ReferenceEquals(source, boundary)) return published;
        source = boundary;
        published = boundary == null || entries.Count == 0 ? boundary : boundary.WithObstructions(entries.Select(e => e.Obstruction));
        changed = false;
        return published;
    }

    internal void Clear()
    {
        entries.Clear();
        source = published = null;
        changed = false;
    }
}
