using System;
using System.Collections.Generic;
using System.Numerics;
using PromeRotation.Spatial.Drawing;
using PromeRotation.Spatial.Geometry;

namespace MasterPlayer.Drawing;

internal static class MeshBuilder
{
    internal static void AddGeometry(List<RenderBatch> batches, DrawRenderItem item, Vector4 nearPlane, float heightOffset)
    {
        var contours = item.Geometry.GetContours(64);
        var offset = Vector3.UnitY * heightOffset;
        if (item.Style.FillColor is { } fill && contours.Count != 0)
        {
            var resolved = (item.Geometry as ITriangulatedDrawingGeometry)?.FillPolygons;
            var vertices = resolved != null ? resolved.SelectMany(p => Convex(p, fill, offset)).ToArray()
                : item.Geometry switch
            {
                DonutGeometry when contours.Count >= 2 => Ring(contours[0], contours[1], fill, offset),
                LocalPolygonGeometry or WorldPolygonGeometry or FanGeometry => Polygon(contours[0], fill, offset),
                _ => Convex(contours[0], fill, offset),
            };
            if (vertices.Length != 0)
                batches.Add(new RenderBatch(DrawPipeline.Solid, vertices));
        }
        if (item.Style.StrokeColor is not { } stroke)
            return;
        for (var i = 0; i < contours.Count; i++)
        {
            var start = item.Geometry is FanGeometry { AngleRadians: >= 6.2831755f } && i == 0 ? 1 : 0;
            var vertices = Stroke(contours[i], start, stroke, item.Style.StrokeThickness, nearPlane, offset);
            if (vertices.Length != 0)
                batches.Add(new RenderBatch(DrawPipeline.Stroke, vertices));
        }
    }

    internal static bool IsGroundArea(DrawRenderItem item)
    {
        var contours = item.Geometry.GetContours(64);
        var points = contours.SelectMany(contour => contour).ToArray();
        return points.Length >= 3 && points.Max(point => point.Y) - points.Min(point => point.Y) < 0.02f;
    }

    internal static void AddGroundGeometry(List<RenderBatch> batches, DrawRenderItem item,
        GroundProjection.Mesh mesh, Vector4 nearPlane, float heightOffset)
    {
        // 微小偏移仅用于深度精度；范围自身的起伏来自原始地面三角面。
        var offset = Vector3.UnitY * (0.015f + heightOffset);
        if (item.Style.FillColor is { } fill && mesh.Fill.Length != 0)
            batches.Add(new(DrawPipeline.Solid, mesh.Fill.Select(point => Vertex(point + offset, fill)).ToArray()));
        if (item.Style.StrokeColor is not { } stroke) return;
        var vertices = new List<GpuVertex>();
        for (var i = 0; i < mesh.Lines.Length; i += 2)
            AddStroke(vertices, mesh.Lines[i] + offset, mesh.Lines[i + 1] + offset,
                stroke, item.Style.StrokeThickness, nearPlane);
        if (vertices.Count != 0) batches.Add(new(DrawPipeline.Stroke, vertices.ToArray()));
    }

    private static GpuVertex[] Convex(IReadOnlyList<Vector3> contour, Vector4 color, Vector3 offset)
    {
        if (contour.Count < 3)
        {
            return Array.Empty<GpuVertex>();
        }
        GpuVertex[] array = new GpuVertex[(contour.Count - 2) * 3];
        int num = 0;
        for (int i = 1; i < contour.Count - 1; i++)
        {
            array[num++] = Vertex(contour[0] + offset, color);
            array[num++] = Vertex(contour[i] + offset, color);
            array[num++] = Vertex(contour[i + 1] + offset, color);
        }
        return array;
    }

    private static GpuVertex[] Polygon(IReadOnlyList<Vector3> contour, Vector4 color, Vector3 offset)
    {
        if (!PolygonTriangulator.TryTriangulate(contour, out int[] triangleIndices))
        {
            return Array.Empty<GpuVertex>();
        }
        GpuVertex[] array = new GpuVertex[triangleIndices.Length];
        for (int i = 0; i < triangleIndices.Length; i++)
        {
            array[i] = Vertex(contour[triangleIndices[i]] + offset, color);
        }
        return array;
    }

    private static GpuVertex[] Ring(IReadOnlyList<Vector3> outer, IReadOnlyList<Vector3> inner, Vector4 color, Vector3 offset)
    {
        if (outer.Count < 3 || outer.Count != inner.Count)
        {
            return Array.Empty<GpuVertex>();
        }
        GpuVertex[] array = new GpuVertex[outer.Count * 6];
        int num = 0;
        for (int i = 0; i < outer.Count; i++)
        {
            int index = (i + 1) % outer.Count;
            array[num++] = Vertex(outer[i] + offset, color);
            array[num++] = Vertex(outer[index] + offset, color);
            array[num++] = Vertex(inner[index] + offset, color);
            array[num++] = Vertex(outer[i] + offset, color);
            array[num++] = Vertex(inner[index] + offset, color);
            array[num++] = Vertex(inner[i] + offset, color);
        }
        return array;
    }

    private static GpuVertex[] Stroke(IReadOnlyList<Vector3> contour, int start, Vector4 color, float thickness, Vector4 nearPlane, Vector3 offset)
    {
        int num = contour.Count - start;
        if (num < 2)
        {
            return Array.Empty<GpuVertex>();
        }
        List<GpuVertex> list = new List<GpuVertex>(num * 6);
        for (int i = 0; i < num; i++)
        {
            AddStroke(list, contour[start + i] + offset, contour[start + (i + 1) % num] + offset,
                color, thickness, nearPlane);
        }
        return list.ToArray();
    }

    private static void AddStroke(List<GpuVertex> vertices, Vector3 start, Vector3 end, Vector4 color, float thickness, Vector4 nearPlane)
    {
        if (!ClipLine(nearPlane, ref start, ref end)) return;
        vertices.Add(StrokeVertex(start, end, 0f, -1f, color, thickness));
        vertices.Add(StrokeVertex(start, end, 1f, -1f, color, thickness));
        vertices.Add(StrokeVertex(start, end, 1f, 1f, color, thickness));
        vertices.Add(StrokeVertex(start, end, 0f, -1f, color, thickness));
        vertices.Add(StrokeVertex(start, end, 1f, 1f, color, thickness));
        vertices.Add(StrokeVertex(start, end, 0f, 1f, color, thickness));
    }

    private static bool ClipLine(Vector4 plane, ref Vector3 start, ref Vector3 end)
    {
        float num = Vector4.Dot(new Vector4(start, 1f), plane);
        float num2 = Vector4.Dot(new Vector4(end, 1f), plane);
        bool flag = num < 0f;
        bool flag2 = num2 < 0f;
        if (flag & flag2)
        {
            return true;
        }
        if (!flag && !flag2)
        {
            return false;
        }
        Vector3 vector = end - start;
        float num3 = Vector3.Dot(vector, new Vector3(plane.X, plane.Y, plane.Z));
        if (MathF.Abs(num3) <= float.Epsilon)
        {
            return false;
        }
        Vector3 vector2 = start + vector * ((0f - num) / num3);
        if (flag)
        {
            end = vector2;
        }
        else
        {
            start = vector2;
        }
        return true;
    }

    private static GpuVertex Vertex(Vector3 position, Vector4 color, float u = 0f, float v = 0f)
    {
        return new GpuVertex(position, Vector3.Zero, color, new Vector4(u, v, 0f, 0f));
    }

    private static GpuVertex StrokeVertex(Vector3 start, Vector3 end, float along, float side, Vector4 color, float thickness)
    {
        return new GpuVertex(start, end, color, new Vector4(along, side, thickness, 0f));
    }



}
