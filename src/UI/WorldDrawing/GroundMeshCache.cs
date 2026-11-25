using System.Numerics;
using PromeRotation.Spatial.Drawing;

namespace MasterPlayer.Drawing;

internal sealed class GroundMeshCache
{
    private sealed record Entry(Vector3 Anchor, Vector3[][] Contours, GroundProjection.Mesh Mesh, long Used);
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private GroundSurface? surface;
    private long clock;
    private int vertices;

    internal GroundProjection.Mesh Get(DrawRenderItem item, GroundSurface current)
    {
        if (!ReferenceEquals(surface, current))
        {
            Clear();
            surface = current;
        }
        ++clock;
        var contours = item.Geometry.GetContours(64);
        if (entries.TryGetValue(item.Id, out var entry))
        {
            if (entry.Anchor == item.Geometry.Position && entry.Contours.Length == contours.Count
                && entry.Contours.Select((points, i) => points.SequenceEqual(contours[i])).All(same => same))
            {
                entries[item.Id] = entry with { Used = clock };
                return entry.Mesh;
            }
            vertices -= entry.Mesh.Fill.Length + entry.Mesh.Lines.Length;
            entries.Remove(item.Id);
        }
        var mesh = GroundProjection.Project(item, current);
        var count = mesh.Fill.Length + mesh.Lines.Length;
        if (count > 200_000) return mesh;
        while (entries.Count >= 64 || vertices + count > 200_000)
        {
            var oldest = entries.MinBy(pair => pair.Value.Used);
            vertices -= oldest.Value.Mesh.Fill.Length + oldest.Value.Mesh.Lines.Length;
            entries.Remove(oldest.Key);
        }
        entries.Add(item.Id, new(item.Geometry.Position, contours.Select(points => points.ToArray()).ToArray(), mesh, clock));
        vertices += count;
        return mesh;
    }

    internal void Clear()
    {
        entries.Clear();
        surface = null;
        vertices = 0;
    }
}
