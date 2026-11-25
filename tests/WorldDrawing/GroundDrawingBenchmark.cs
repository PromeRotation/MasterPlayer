using System.Diagnostics;
using System.Numerics;
using MasterPlayer.Drawing;
using PromeRotation.Spatial.Drawing;
using PromeRotation.Spatial.Geometry;

internal static class GroundDrawingBenchmark
{
    internal static void Run()
    {
        var axis = Enumerable.Range(-40, 81).Select(i => (float)i).ToArray();
        var scene = GroundDrawingTests.Scene(GroundDrawingTests.Grid(axis, axis, (x, z) => MathF.Sin(x * 0.2f) * 0.5f));
        var clock = Stopwatch.StartNew();
        var surface = GroundSurface.Build(scene, Vector3.Zero, 40, default);
        var build = clock.Elapsed.TotalMilliseconds;
        var item = new DrawRenderItem("benchmark", new CircleGeometry(Vector3.Zero, 12), new DrawStyle(Vector4.One, Vector4.One));
        var cache = new GroundMeshCache();
        clock.Restart();
        var mesh = cache.Get(item, surface);
        var project = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        for (var i = 0; i < 1000; ++i)
            if (!ReferenceEquals(mesh, cache.Get(item, surface)))
                throw new InvalidOperationException("重复范围未复用裁剪结果");
        Console.WriteLine($"INFO ground synthetic: faces={surface.Count}, background build={build:F2}ms, first projection={project:F2}ms, cached lookup={clock.Elapsed.TotalMilliseconds / 1000:F4}ms");
    }
}
