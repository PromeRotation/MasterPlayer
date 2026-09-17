using System.Numerics;
using Dalamud.Bindings.ImGui;
using MasterPlayer.Radar;
using MasterPlayer.Scripting.Visual;
using PromeRotation.Helpers;

namespace MasterPlayer.Windows;

internal static class VisualAoePreview
{
    internal static bool Draw(VisualMechanicRule rule, float simulatedDegrees, float zoom)
    {
        var size = new Vector2(MathF.Max(1f, ImGui.GetContentRegionAvail().X), 240f);
        var origin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##aoe_preview", size);
        var center = origin + size * 0.5f;
        var radius = MathF.Max(1f, MathF.Min(size.X, size.Y) * 0.5f - 18f);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin, origin + size, ImGui.GetColorU32(new Vector4(0.055f, 0.07f, 0.10f, 1f)), 5f);
        var canvas = new RadarCanvas(draw, center, radius);
        var extent = rule.Shape switch
        {
            VisualAoeShape.Rectangle => MathF.Sqrt(rule.Width * rule.Width + rule.Length * rule.Length) * 0.5f,
            VisualAoeShape.RayRectangle => MathF.Sqrt(rule.Width * rule.Width * 0.25f + rule.Length * rule.Length),
            _ => rule.Radius,
        };
        if (rule.ValidateGeometry().Count > 0 || !float.IsFinite(extent) || extent <= 0f
            || !float.IsFinite(rule.Offset.X) || !float.IsFinite(rule.Offset.Y))
        {
            draw.AddText(origin + new Vector2(12f), ImGui.GetColorU32(new Vector4(1f, 0.6f, 0.4f, 1f)), "修正尺寸后显示预览");
            return false;
        }
        var range = MathF.Max(10f, extent * 1.25f);
        var scale = radius / range * zoom;
        var projection = new RadarProjection(Vector3.Zero, center, scale, RadarProjection.NorthUpRotation);
        var grid = ImGui.GetColorU32(new Vector4(0.20f, 0.27f, 0.34f, 1f));
        var step = range > 40f ? 10f : range > 20f ? 5f : 2f;
        for (var i = -10; i <= 10; i++)
        {
            var d = i * step * scale;
            canvas.StrokeSegment(center + new Vector2(d, -radius), center + new Vector2(d, radius), grid, 1f);
            canvas.StrokeSegment(center + new Vector2(-radius, d), center + new Vector2(radius, d), grid, 1f);
        }

        var followsRotation = rule.Tracking == VisualAoeTracking.FollowPositionAndRotation;
        var degrees = !followsRotation && rule.Direction == VisualAoeDirection.Fixed ? rule.RotationDegrees : simulatedDegrees;
        var rotation = degrees * (MathF.PI / 180f);
        if (!float.IsFinite(rotation)) return false;
        var position = followsRotation
            ? GeometryHelper.LocalToWorld(rule.Offset, Vector3.Zero, rotation)
            : new Vector3(rule.Offset.X, 0f, rule.Offset.Y);
        var contours = rule.CreateTemplate().Resolve(position, rotation, null).GetContours()
            .Select(c => c.Select(projection.Project).ToArray()).ToArray();
        var fill = ImGui.GetColorU32(new Vector4(1f, 0.36f, 0.20f, 0.30f));
        var stroke = ImGui.GetColorU32(new Vector4(1f, 0.55f, 0.30f, 1f));
        if (rule.Shape == VisualAoeShape.Donut && contours.Length == 2)
        {
            for (var i = 0; i < contours[0].Length; i++)
            {
                var next = (i + 1) % contours[0].Length;
                canvas.FillPolygon([contours[0][i], contours[0][next], contours[1][next], contours[1][i]], fill);
            }
        }
        else foreach (var contour in contours) canvas.FillPolygon(contour, fill);
        foreach (var contour in contours) canvas.StrokeContour(contour, stroke, 1.5f);
        var blue = ImGui.GetColorU32(new Vector4(0.40f, 0.80f, 1f, 1f));
        draw.AddCircleFilled(center, 4f, blue);
        var arrow = projection.Project(GeometryHelper.PointFromRotationDistanceXZ(Vector3.Zero, rotation, 3f));
        canvas.StrokeSegment(center, arrow, blue, 2f);
        draw.AddText(origin + new Vector2(10f, 8f), blue, $"北 ↑   网格 {step:0} 码");
        draw.AddText(origin + new Vector2(10f, size.Y - 24f), blue, "拖动调整偏移");
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            var delta = ImGui.GetIO().MouseDelta / scale;
            rule.Offset += followsRotation
                ? GeometryHelper.WorldToLocal(new Vector3(delta.X, 0f, delta.Y), Vector3.Zero, rotation)
                : delta;
            return delta != Vector2.Zero;
        }
        return false;
    }
}
