using System.Numerics;
using PromeRotation.Spatial.Drawing;
using PromeRotation.Spatial.Geometry;
using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Settings;

public sealed class AoeViewSettings
{
    public Vector4 DangerFill = new(1f, 0f, 0f, 0.55f);
    public Vector4 DangerOutline = new(1f, 0f, 0f, 1f);
    public Vector4 SafeFill = new(0f, 1f, 0f, 0.25f);
    public Vector4 SafeOutline = new(0f, 1f, 0f, 1f);
    public bool FadeDanger = true;
    public bool ShowDanger = true;
    public bool ShowSafe = true;
    public bool ShowCircles = true;
    public bool ShowDonuts = true;
    public bool ShowFans = true;
    public bool ShowRectangles = true;
    public bool ShowPolygons = true;
    public bool ShowOtherShapes = true;

    public bool IsVisible(IGeometryShape geometry, bool safe, bool sourceVisible = true)
        => sourceVisible && (safe ? ShowSafe : ShowDanger) && ShowsShape(geometry);

    public bool ShowsShape(IGeometryShape geometry) => geometry switch
    {
        CircleGeometry => ShowCircles,
        DonutGeometry => ShowDonuts,
        FanGeometry => ShowFans,
        RectangleGeometry or RayRectangleGeometry => ShowRectangles,
        LocalPolygonGeometry or WorldPolygonGeometry => ShowPolygons,
        _ => ShowOtherShapes,
    };

    public static AoeViewSettings CreateRadarDefaults() => new()
    {
        DangerOutline = new Vector4(1f, 0f, 0f, 0.9f),
        SafeFill = new Vector4(0f, 1f, 0f, 0.22f),
        SafeOutline = new Vector4(0f, 1f, 0f, 0.8f),
    };

    public DrawStyle GetDangerStyle(DangerAreaSnapshot danger, DateTime now)
    {
        var progress = 1f;
        if (danger.DangerWindows.Count > 0)
        {
            progress = 0f;
            foreach (var window in danger.DangerWindows)
                progress = MathF.Max(progress, window.TelegraphProgressAt(now));
        }
        return GetDangerStyle(progress);
    }

    public DrawStyle GetDangerStyle(float progress = 1f)
    {
        var fill = Vector4.Clamp(DangerFill, Vector4.Zero, Vector4.One);
        if (FadeDanger)
            fill.W *= (0.12f + 0.43f * Math.Clamp(progress, 0f, 1f)) / 0.55f;
        return new DrawStyle(fill, Vector4.Clamp(DangerOutline, Vector4.Zero, Vector4.One));
    }

    public DrawStyle GetSafeStyle() => new(
        Vector4.Clamp(SafeFill, Vector4.Zero, Vector4.One),
        Vector4.Clamp(SafeOutline, Vector4.Zero, Vector4.One));
}

public sealed class AoeVisibility
{
    public string Name = "";
    public bool World = true;
    public bool Radar = true;
}
