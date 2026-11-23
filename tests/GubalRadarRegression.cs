using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;

internal static class GubalRadarRegression
{
    public static void Run(Action<string, Action> run)
    {
        var center = new Vector3(100, 7, -50);
        static Vector2[] Rect(float x0, float y0, float x1, float y1)
            => [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];
        static Vector2[][] Triangles(IEnumerable<Vector2[]> rectangles)
            => rectangles.SelectMany(r => new Vector2[][] { [r[0], r[1], r[2]], [r[0], r[2], r[3]] }).ToArray();
        var islands = new[] { Rect(-12, -4, -4, 4), Rect(4, -4, 12, 4) };
        var separated = new PolygonRegionGeometry(center, islands, [], Triangles(islands));
        var ring = new PolygonRegionGeometry(center, [Rect(-10, -10, 10, 10)], [Rect(-3, -3, 3, 3)],
            Triangles([Rect(-10, -10, 10, -3), Rect(-10, 3, 10, 10), Rect(-10, -3, -3, 3), Rect(3, -3, 10, 3)]));
        foreach (var (shape, area, lineLength) in new[] { (separated, 128f, 16f), (ring, 364f, 14f) })
            run($"Gubal radar region area {area} preserves holes islands fill and true rims", () =>
            {
                foreach (var screen in new[] { Vector2.Zero, new Vector2(1950, 1080) })
                foreach (var rotation in new[] { 0, .37f, MathF.PI / 2 })
                {
                    var layout = RadarArenaLayout.Create(shape, center, screen, 150, rotation);
                    var clip = new RadarClipRegion(layout.Outline, screen, 150, layout.Outlines, layout.FillPolygons);
                    var scale = layout.Projection.Scale;
                    Check(!clip.Contains(layout.Projection.Project(center)), "Gap became floor.");
                    var pieces = clip.ClipPolygon(clip.Outline).ToArray();
                    Check(Math.Abs(pieces.Sum(Area) / (scale * scale) - area) < .01f, "Background lost floor or filled a hole.");
                    var line = clip.ClipSegment(layout.Projection.Project(center - Vector3.UnitX * 20),
                        layout.Projection.Project(center + Vector3.UnitX * 20)).ToArray();
                    Check(line.Length == 2 && Math.Abs(line.Sum(s => Vector2.Distance(s.Start, s.End)) / scale - lineLength) < .001f,
                        "Path bridged gap or lost a floor.");
                    foreach (var contour in layout.Outlines!)
                        for (var i = 0; i < contour.Count; ++i)
                        {
                            var a = contour[i]; var b = contour[(i + 1) % contour.Count];
                            var length = clip.ClipSegment(a, b).Sum(s => Vector2.Distance(s.Start, s.End));
                            Check(Math.Abs(length - Vector2.Distance(a, b)) < .02f, "Rotated rim was truncated.");
                        }
                }
            });
        run("Gubal radar triangle seams and every hole edge preserve membership", () =>
        {
            foreach (var rotation in new[] { 0, .37f, MathF.PI / 2 })
            {
                var layout = RadarArenaLayout.Create(separated, center, Vector2.Zero, 150, rotation);
                var clip = new RadarClipRegion(layout.Outline, Vector2.Zero, 150, layout.Outlines, layout.FillPolygons);
                var point = new Vector3(105.25f, 7, -52.75f);
                Check(separated.Contains(point) && clip.Contains(layout.Projection.Project(point)), "Triangle seam rejected interior.");
                layout = RadarArenaLayout.Create(ring, center, Vector2.Zero, 150, rotation);
                clip = new RadarClipRegion(layout.Outline, Vector2.Zero, 150, layout.Outlines, layout.FillPolygons);
                foreach (var offset in new[] { new Vector3(-3, 0, 0), new Vector3(3, 0, 0), new Vector3(0, 0, -3), new Vector3(0, 0, 3) })
                    Check(ring.Contains(center + offset) && clip.Contains(layout.Projection.Project(center + offset)),
                        "Hole boundary membership is asymmetric.");
            }
        });
        run("Gubal radar exported arenas preserve runtime containment and world contours", () =>
        {
            foreach (var (shape, runtime) in new[]
            {
                (GubalArenas.DemonTome(0), BossMod.Heavensward.Dungeon.D05GreatGubalLibrary.D051DemonTome.D051DemonTome.DefaultArena),
                (GubalArenas.DemonTomeSpin(0), BossMod.Heavensward.Dungeon.D05GreatGubalLibrary.D051DemonTome.D051DemonTome.SpinArena),
            })
            foreach (var rotation in new[] { 0, .37f, MathF.PI / 2 })
            {
                var layout = RadarArenaLayout.Create(shape, shape.Position, new(1950, 1080), 150, rotation);
                var clip = new RadarClipRegion(layout.Outline, new(1950, 1080), 150, layout.Outlines, layout.FillPolygons);
                var host = HostDrawingGeometry.Convert(shape);
                Check(host.GetContours().Count == 2, "World contour omitted island or hole.");
                for (var x = -20.13f; x < 20; x += .37f)
                    for (var z = -10.17f; z < 10; z += .37f)
                    {
                        var point = new Vector3(x, 0, z);
                        var expected = runtime.Contains(new(x - runtime.Center.X, z - runtime.Center.Z));
                        Check(shape.Contains(point) == expected && host.Contains(point) == expected
                            && clip.Contains(layout.Projection.Project(point)) == expected, "Runtime, radar and world floors differ.");
                    }
            }
        });
        run("Gubal radar ordinary single polygon retains original clipping route", () =>
        {
            var shape = new WorldPolygonGeometry(Rect(-10, -10, 10, 10).Select(p => new Vector3(p.X, 0, p.Y)));
            var layout = RadarArenaLayout.Create(shape, Vector3.Zero, Vector2.Zero, 150, .37f);
            Check(layout.FillPolygons == null && layout.Outlines!.Count == 1, "Single polygon changed route.");
            var clip = new RadarClipRegion(layout.Outline, Vector2.Zero, 150);
            Check(Math.Abs(clip.ClipPolygon(clip.Outline).Sum(Area) / MathF.Pow(layout.Projection.Scale, 2) - 400) < .01f,
                "Single polygon floor changed.");
        });
    }

    private static float Area(IReadOnlyList<Vector2> polygon)
    {
        var area = 0f;
        for (var i = 1; i + 1 < polygon.Count; ++i)
        {
            var a = polygon[i] - polygon[0]; var b = polygon[i + 1] - polygon[0];
            area += a.X * b.Y - a.Y * b.X;
        }
        return Math.Abs(area) * .5f;
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
