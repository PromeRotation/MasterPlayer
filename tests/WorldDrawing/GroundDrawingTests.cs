using System.Numerics;
using MasterPlayer.Drawing;
using MasterPlayer.Spatial.Terrain;
using PromeRotation.Spatial.Drawing;
using PromeRotation.Spatial.Geometry;

internal static class GroundDrawingTests
{
    internal static void Run()
    {
        var slope = Build(Grid([-4, 0, 4], [-4, 0, 4], (x, _) => x * 0.2f));
        var circle = Item(new CircleGeometry(Vector3.Zero, 3));
        var projected = GroundProjection.Project(circle, slope);
        Require(projected.Fill.Length > 0 && projected.Lines.Length > 0, "坡面填充/边线缺失");
        foreach (var point in projected.Fill.Concat(projected.Lines))
        {
            Require(MathF.Abs(point.Y - point.X * 0.2f) < 0.0001f, "顶点未落在真实坡面");
            Require(new Vector2(point.X, point.Z).Length() <= 3.001f, "裁剪超出原始范围");
        }
        Require(MathF.Abs(Area(projected.Fill) - PolygonArea(circle.Geometry.GetContours(64)[0])) < 0.001f,
            "平面投影面积改变或存在重复填充");
        Console.WriteLine("PASS ground: slope fill, contour, footprint and area");

        var bump = Build(Grid([-4, -1, 0, 1, 4], [-4, 0, 4], (x, _) => MathF.Max(0, 1 - MathF.Abs(x))));
        var raised = GroundProjection.Project(Item(new CircleGeometry(Vector3.UnitY, 3)), bump);
        Require(raised.Fill.Any(point => point.Y > 0.99f) && raised.Fill.Any(point => point.Y < 0.01f),
            "范围内部隆起没有被保留");
        var ring = Item(new DonutGeometry(Vector3.UnitY, 1, 3));
        var ringMesh = GroundProjection.Project(ring, bump);
        Require(ringMesh.Fill.Length > 0, "圆环贴地为空");
        for (var i = 0; i < ringMesh.Fill.Length; i += 3)
        {
            var center = (ringMesh.Fill[i] + ringMesh.Fill[i + 1] + ringMesh.Fill[i + 2]) / 3;
            Require(new Vector2(center.X, center.Z).Length() > 0.995f, "圆环内孔被填充");
        }
        var fan = GroundProjection.Project(Item(new FanGeometry(Vector3.UnitY, 0, 3, MathF.PI * 1.5f)), bump);
        Require(fan.Fill.Length > 0, "大角度扇形贴地失败");
        Console.WriteLine("PASS ground: interior bump, donut hole and reflex fan");

        var lower = Grid([-4, 0, 4], [-4, 0, 4], (_, _) => 0);
        var upper = Grid([-4, 0, 4], [-4, 0, 4], (_, _) => 4);
        var stacked = Build(lower, upper);
        var lowerMesh = GroundProjection.Project(circle, stacked);
        var upperMesh = GroundProjection.Project(Item(new CircleGeometry(new(0, 4, 0), 3)), stacked);
        Require(lowerMesh.Fill.Length > 0 && lowerMesh.Fill.All(point => MathF.Abs(point.Y) < 0.001f),
            "下层范围画到了上层");
        Require(upperMesh.Fill.Length > 0 && upperMesh.Fill.All(point => MathF.Abs(point.Y - 4) < 0.001f),
            "上层范围画到了下层");
        var gap = Build(Grid([-4, -1], [-4, 4], (_, _) => 0), Grid([1, 4], [-4, 4], (_, _) => 0));
        var gapMesh = GroundProjection.Project(Item(new CircleGeometry(new(-2, 0, 0), 5)), gap);
        Require(gapMesh.Fill.Length > 0 && gapMesh.Fill.All(point => point.X <= -0.999f),
            "范围跨过断崖连接了另一块平台");
        Require(GroundProjection.Project(Item(new CircleGeometry(new(0, 10, 0), 3)), stacked).Fill.Length == 0,
            "远离支撑面的空中范围被吸附到了其他楼层");
        Console.WriteLine("PASS ground: stacked floors, cliffs and missing support");

        var stairs = Build(Grid([0, 1], [-3, 3], (_, _) => 0),
            Grid([1, 2], [-3, 3], (_, _) => 0.4f), Grid([2, 3], [-3, 3], (_, _) => 0.8f));
        var steps = GroundProjection.Project(Item(new CircleGeometry(new(0.5f, 0, 0), 4)), stairs);
        Require(steps.Fill.Any(point => MathF.Abs(point.Y - 0.8f) < 0.001f), "台阶没有连接到支撑层");
        for (var i = 0; i < steps.Fill.Length; i += 3)
            Require(MathF.Abs(steps.Fill[i].Y - steps.Fill[i + 1].Y) < 0.001f
                && MathF.Abs(steps.Fill[i].Y - steps.Fill[i + 2].Y) < 0.001f, "台阶之间生成了虚构斜面");
        for (var i = 0; i < steps.Lines.Length; i += 2)
            Require(MathF.Abs(steps.Lines[i].Y - steps.Lines[i + 1].Y) < 0.001f, "边线跨台阶拉直");
        Console.WriteLine("PASS ground: stepped floors without fabricated fill or stroke bridges");

        var seams = Build(Grid([-4, 0], [-4, 4], (_, _) => 0), Grid([0, 4], [-2, 0, 2], (_, _) => 0));
        var joined = GroundProjection.Project(Item(new CircleGeometry(new(-1, 0, 0), 3)), seams);
        Require(joined.Fill.Any(point => point.X > 1), "不同细分模型的 T 接缝没有连接");
        var hole = Build(Grid([-4, -1], [-4, 4], (_, _) => 0));
        var holeRing = GroundProjection.Project(Item(new DonutGeometry(Vector3.Zero, 1.5f, 3)), hole);
        Require(holeRing.Fill.Length > 0, "锚点在真实洞口时丢失了有支撑的圆环");
        Console.WriteLine("PASS ground: T-junction seams and anchors over real holes");

        var blocked = new TerrainMesh(lower.Vertices, lower.Triangles.Select(t => t with { Material = 0x2000000 }).ToArray());
        Require(Build(blocked).Count == 0, "禁行洞口材质被当作地面");
        var mirroredScene = Scene(lower, Matrix4x4.CreateScale(-1, 1, 1));
        Require(GroundSurface.Build(mirroredScene, Vector3.Zero, 8, default).Count > 0, "镜像模型法线处理错误");
        var vertical = new TerrainMesh([new(0, 0, 0), new(0, 3, 0), new(0, 3, 3)], [new(0, 1, 2, 0)]);
        Require(Build(vertical).Count == 0, "竖直墙面被当作地面");
        var huge = Grid([-10000, 10000], [-10000, 10000], (_, _) => 0);
        var cropped = Build(huge);
        var croppedMesh = GroundProjection.Project(circle, cropped);
        Require(croppedMesh.Fill.Length > 0 && cropped.Count <= 8, "巨大场景三角面没有裁入局部窗口");
        Console.WriteLine("PASS ground: materials, walls, mirrored models and capture-window clipping");

        var cache = new GroundMeshCache();
        var first = cache.Get(circle, slope);
        Require(ReferenceEquals(first, cache.Get(circle with { Style = new DrawStyle(Vector4.One, null) }, slope)),
            "颜色改变导致地面裁剪缓存失效");
        var shifted = circle with { Geometry = new CircleGeometry(new(1, 0, 0), 3) };
        Require(!ReferenceEquals(first, cache.Get(shifted, slope)), "实体移动没有更新贴地几何");
        Require(!ReferenceEquals(first, cache.Get(circle, stacked)), "地表更新没有失效缓存");
        var batches = new List<RenderBatch>();
        MeshBuilder.AddGroundGeometry(batches, circle, projected, new(0, 0, 0, -1), 0);
        Require(batches.Count == 2 && batches[0].Vertices.All(v => MathF.Abs(v.Position.Y - v.Position.X * 0.2f - 0.015f) < 0.0001f),
            "贴地高度偏移或绘制批次错误");
        Require(!MeshBuilder.IsGroundArea(Item(new WorldPolygonGeometry([new(0, 0, 0), new(1, 1, 0), new(0, 0, 1)]))),
            "显式三维图形不应强制贴地");
        Console.WriteLine("PASS ground: geometry cache, style independence, depth offset and explicit 3D geometry");
    }

    internal static TerrainMesh Grid(float[] xs, float[] zs, Func<float, float, float> height)
    {
        var vertices = (from z in zs from x in xs select new Vector3(x, height(x, z), z)).ToArray();
        var triangles = new List<TerrainMesh.Triangle>();
        for (var z = 0; z + 1 < zs.Length; ++z)
            for (var x = 0; x + 1 < xs.Length; ++x)
            {
                var i = z * xs.Length + x;
                triangles.Add(new(i, i + xs.Length, i + xs.Length + 1, 0));
                triangles.Add(new(i, i + xs.Length + 1, i + 1, 0));
            }
        return new(vertices, triangles.ToArray());
    }

    internal static TerrainSceneSnapshot Scene(TerrainMesh mesh, Matrix4x4? transform = null)
        => new([new(1, new TerrainMeshAsset("test", mesh), transform ?? Matrix4x4.Identity, 0, 0)]);

    private static GroundSurface Build(params TerrainMesh[] meshes)
        => GroundSurface.Build(new(meshes.Select((mesh, i) =>
            new TerrainSceneSnapshot.Instance((ulong)i, new("test" + i, mesh), Matrix4x4.Identity, 0, 0)).ToArray()),
            Vector3.Zero, 8, default);

    private static DrawRenderItem Item(IGeometryShape geometry) => new("test", geometry, new DrawStyle(new(1, 0, 0, 0.5f), Vector4.One));

    private static float Area(Vector3[] vertices)
    {
        var area = 0f;
        for (var i = 0; i < vertices.Length; i += 3)
        {
            var ab = vertices[i + 1] - vertices[i];
            var ac = vertices[i + 2] - vertices[i];
            area += MathF.Abs(ab.X * ac.Z - ab.Z * ac.X) / 2;
        }
        return area;
    }

    private static float PolygonArea(IReadOnlyList<Vector3> points)
    {
        var area = 0f;
        for (var i = 0; i < points.Count; ++i)
        {
            var a = points[i]; var b = points[(i + 1) % points.Count];
            area += a.X * b.Z - a.Z * b.X;
        }
        return MathF.Abs(area) / 2;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
