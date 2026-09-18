using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace MasterPlayer.Radar;

/// <summary>Radar drawing clipped to the actual arena, including concave floors and circular markers.</summary>
internal sealed class RadarCanvas
{
    private readonly ImDrawListPtr _drawList;
    private readonly RadarClipRegion _clip;
    public Vector2 Center { get; }
    public float Radius { get; }

    public RadarCanvas(ImDrawListPtr drawList, Vector2 center, float radius, IReadOnlyList<Vector2>? outline = null)
    {
        _drawList = drawList; Center = center; Radius = radius;
        _clip = new(outline ?? RadarClipRegion.Circle(center, radius), center, radius);
    }

    public bool Contains(Vector2 point) => _clip.Contains(point);
    public Vector2 RimPoint(Vector2 direction)
        => _clip.ClipSegment(Center, Center + direction * Radius * 2).LastOrDefault((Start: Center, End: Center)).End;
    public void FillBackground(uint color) => FillPolygon(_clip.Outline, color);
    public void StrokeRim(uint color, float thickness) => StrokeContour(_clip.Outline, color, thickness);

    public void FillPolygon(IReadOnlyList<Vector2> polygon, uint color)
    {
        var flags = _drawList.Flags;
        _drawList.Flags &= ~ImDrawListFlags.AntiAliasedFill;
        try
        {
            foreach (var part in _clip.ClipPolygon(polygon))
                for (var i = 1; i + 1 < part.Count; ++i)
                    _drawList.AddTriangleFilled(part[0], part[i], part[i + 1], color);
        }
        finally { _drawList.Flags = flags; }
    }

    public void StrokeSegment(Vector2 a, Vector2 b, uint color, float thickness)
    {
        foreach (var (start, end) in _clip.ClipSegment(a, b)) _drawList.AddLine(start, end, color, thickness);
    }
    public void StrokeContour(IReadOnlyList<Vector2> contour, uint color, float thickness)
    {
        for (var i = 0; i < contour.Count; ++i) StrokeSegment(contour[i], contour[(i + 1) % contour.Count], color, thickness);
    }
    public void StrokeContourGlow(IReadOnlyList<Vector2> contour, Vector4 color, float thickness)
    {
        var glow = color; glow.W *= 0.22f;
        StrokeContour(contour, ImGui.GetColorU32(glow), thickness * 3f);
        StrokeContour(contour, ImGui.GetColorU32(color), thickness);
    }
    public void FillCircle(Vector2 center, float radius, uint color, int segments = 0)
        => FillPolygon(RadarClipRegion.Circle(center, radius, segments == 0 ? 24 : segments), color);
    public void StrokeCircle(Vector2 center, float radius, uint color, float thickness, int segments = 0)
        => StrokeContour(RadarClipRegion.Circle(center, radius, segments == 0 ? 24 : segments), color, thickness);
}
