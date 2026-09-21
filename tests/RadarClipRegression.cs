using System.Numerics;
using MasterPlayer.Radar;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Spatial.Geometry;

internal static class RadarClipRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("radar fits each authored Crucible arena after camera rotation without changing its shape", () =>
        {
            var fields = new[]
            {
                (Center: new WPos(120, -420), Name: 14531u),
                (Center: new WPos(120, 0), Name: 14633u),
                (Center: new WPos(520, 0), Name: 14549u),
                (Center: new WPos(520.1f, -420), Name: 14541u),
                (Center: new WPos(920, -420), Name: 14628u),
                (Center: new WPos(520, -420), Name: 14592u),
                (Center: new WPos(520, -420), Name: 14693u),
            };
            foreach (var screen in new[] { new Vector2(175,175), new Vector2(1950,1080), new Vector2(-850,750) })
            foreach (var field in fields)
            {
                var arena = CrucibleArena.Find(field.Center, field.Name)!;
                var geometry = arena.DisplayBounds.Geometry(arena.Center, 0);
                // Circular drawing uses the host's polygon approximation; compare the same authored contour.
                var contourGeometry = new WorldPolygonGeometry(geometry.GetContours()[0]);
                foreach (var rotation in new[] { MathF.PI, 0.37f, MathF.PI / 4, MathF.PI / 2 })
                {
                    var layout = RadarArenaLayout.Create(geometry, arena.Center.ToVec3(), screen, 150, rotation);
                    Check(layout.Outline.All(p => Math.Abs(p.X - screen.X) <= 146.01f && Math.Abs(p.Y - screen.Y) <= 146.01f),
                        $"arena {field.Name} was clipped when rotated");
                    Check(layout.Outline.Max(p => Math.Max(Math.Abs(p.X - screen.X), Math.Abs(p.Y - screen.Y))) > 145.9f,
                        $"arena {field.Name} still used a circular fit with unnecessary empty corners");
                    var region = new RadarClipRegion(layout.Outline, screen, 150);
                    for (var z = -27.25f; z <= 27; z += 2)
                        for (var x = -27.25f; x <= 27; x += 2)
                        {
                            var world = (arena.Center + new WDir(x, z)).ToVec3();
                            Check(region.Contains(layout.Projection.Project(world)) == contourGeometry.Contains(world),
                                $"arena {field.Name} changed its floor at ({x},{z}), rotation={rotation}, clipped={region.Contains(layout.Projection.Project(world))}, expected={contourGeometry.Contains(world)}");
                        }
                }
            }
        });
        run("radar resizing preserves the complete circular floor at the reported 1.152 scale and all supported sizes", () =>
        {
            var arena = CrucibleArena.Find(new(920, -420), 14628)!;
            var geometry = arena.DisplayBounds.Geometry(arena.Center, 0);
            foreach (var zoom in new[] { 0.3f, 1f, 1.152f, 2f, 3f })
            foreach (var fontSize in new[] { 17f, 28f, 40f })
            {
                var viewport = RadarViewportLayout.Create(zoom, fontSize, new(1920, 1080));
                var center = new Vector2(viewport.FullSize / 2);
                var layout = RadarArenaLayout.Create(geometry, arena.Center.ToVec3(), center, viewport.Radius, 0.63f);
                var clip = new RadarClipRegion(layout.Outline, center, viewport.Radius);
                Check(layout.Outline.All(clip.Contains), $"scale {zoom} cut off the circle");
                Check(Math.Abs(Area(layout.Outline) - clip.ClipPolygon(layout.Outline).Sum(Area)) < 1f,
                    $"scale {zoom} lost floor area");
                Check(viewport.Margin >= 11 + fontSize / 2 + 1, "compass text or its shadow was clipped");
                Check(viewport.FullSize <= 1080, "viewport escaped the display");
            }
        });
        run("radar clamps window size to a small display and recovers invalid saved scale", () =>
        {
            foreach (var scale in new[] { 3f, float.NaN, float.PositiveInfinity })
            {
                var viewport = RadarViewportLayout.Create(scale, 30, new(800, 480));
                Check(float.IsFinite(viewport.FullSize) && viewport.FullSize <= 480 && viewport.Radius >= 24,
                    "invalid or oversized radar window");
            }
            Check(RadarViewportLayout.Create(2, 17, new(1920,1080)).Radius == 300, "size setting no longer changes the viewport");
        });
        run("radar Lauda preserves all six ledges, their fire gaps and the authored arena center", () =>
        {
            var geometry = CrucibleLaudaFloor.Display.Geometry(CrucibleLaudaFloor.Center, 0);
            var layout = RadarArenaLayout.Create(geometry, CrucibleLaudaFloor.Center.ToVec3(), Vector2.Zero, 150, MathF.PI);
            var region = new RadarClipRegion(layout.Outline, Vector2.Zero, 150);
            WPos[] ledges = [new(520,-442.5f), new(520,-397.5f), new(532.5f,-427.5f), new(532.5f,-417.5f), new(507.5f,-422.5f), new(507.5f,-412.5f)];
            WPos[] gaps = [new(532.5f,-437.5f), new(532.5f,-422.5f), new(507.5f,-417.5f), new(507.5f,-402.5f)];
            Check(ledges.All(p => region.Contains(layout.Projection.Project(p.ToVec3()))), "one of Lauda's six platforms disappeared");
            Check(gaps.All(p => !region.Contains(layout.Projection.Project(p.ToVec3()))), "fire gap became floor");
            Check(layout.Projection.Project(new Vector3(520,0,-420)) == Vector2.Zero, "polygon centroid shifted the arena center");
            var worldArea = Area(layout.Outline) / (layout.Projection.Scale * layout.Projection.Scale);
            Check(Math.Abs(worldArea - 950) < 0.1f, $"20x40 main floor plus six 5x5 ledges was distorted: {worldArea}");
            var zoomed = RadarArenaLayout.Create(geometry, CrucibleLaudaFloor.Center.ToVec3(), Vector2.Zero, 300, 0.63f);
            var zoomRegion = new RadarClipRegion(zoomed.Outline, Vector2.Zero, 300);
            Check(!zoomRegion.Contains(new(301,0)) && gaps.All(p => !zoomRegion.Contains(zoomed.Projection.Project(p.ToVec3()))),
                "resizing painted outside the viewport or filled a fire gap");
        });
        run("radar authored display limits include fences but do not pad solid walls or custom floors", () =>
        {
            var square = CrucibleArena.Find(new(120,0),14633)!.DisplayBounds.Geometry(new(120,0),0).GetBounds();
            var solid = CrucibleArena.Find(new(520.1f,-420),14541)!.DisplayBounds.Geometry(new(520.1f,-420),0).GetBounds();
            var dragon = CrucibleArena.Find(new(920,-420),14628)!.DisplayBounds.Geometry(new(920,-420),0);
            var thirdFinal = CrucibleArena.Find(new(520,-420),14592)!.DisplayBounds.Geometry(new(520,-420),0).GetBounds();
            Check(Math.Abs(square.MaxX - square.MinX - 46) < 0.001f, "square lost its three-yard electric fence");
            Check(Math.Abs(solid.MaxX - solid.MinX - 37.4f) < 0.001f && Math.Abs(solid.MaxZ - solid.MinZ - 40.4f) < 0.001f,
                "solid wall gained invented floor padding");
            Check(dragon is CircleGeometry { Radius: 19.75f }, "dragon's circular wall became a square or inflated circle");
            Check(thirdFinal.MaxX - thirdFinal.MinX == 30 && thirdFinal.MaxZ - thirdFinal.MinZ == 50,
                "third-board final's rectangular floor changed");
        });
        run("radar square keeps its corners and clips circles and lines at the actual rim", () =>
        {
            Vector2[] square = [new(-10, -10), new(10, -10), new(10, 10), new(-10, 10)];
            var region = new RadarClipRegion(square, Vector2.Zero, 15);
            Check(region.Contains(new(9, 9)) && !region.Contains(new(11, 0)), "still using circular bounds");
            var line = region.ClipSegment(new(-20, 0), new(20, 0)).Single();
            Check(line.Start == new Vector2(-10, 0) && line.End == new Vector2(10, 0), "wrong square clipping");
            var circle = region.ClipPolygon(RadarClipRegion.Circle(new(9, 0), 4)).ToArray();
            Check(circle.Length > 0 && circle.SelectMany(p => p).All(region.Contains), "circle leaked across arena rim");
        });
        run("radar concave floor preserves gaps for lines and polygon fills in either winding", () =>
        {
            Vector2[] u = [new(-10,-10), new(10,-10), new(10,10), new(4,10), new(4,-4), new(-4,-4), new(-4,10), new(-10,10)];
            foreach (var outline in new[] { u, u.Reverse().ToArray() })
            {
                var region = new RadarClipRegion(outline, Vector2.Zero, 20);
                Check(region.Contains(new(8, 5)) && !region.Contains(new(0, 5)), "concavity filled in");
                var segments = region.ClipSegment(new(-20, 5), new(20, 5)).ToArray();
                Check(segments.Length == 2 && Math.Abs(segments.Sum(s => Vector2.Distance(s.Start, s.End)) - 12) < 0.001, "line bridged the gap");
                Vector2[] cover = [new(-20,0), new(20,0), new(20,10), new(-20,10)];
                var pieces = region.ClipPolygon(cover).ToArray();
                Check(Math.Abs(pieces.Sum(Area) - 120) < 0.001, "clipped fill lost area or double-covered triangle seams");
                Check(pieces.All(p => region.Contains(p.Aggregate(Vector2.Zero, (a,b) => a+b) / p.Count)), "fill leaked into notch");
            }
        });
        run("radar circular fallback and zoomed rotated arena respect the viewport", () =>
        {
            var circle = new RadarClipRegion(RadarClipRegion.Circle(Vector2.Zero, 10), Vector2.Zero, 10);
            Check(circle.Contains(new(0,9)) && !circle.Contains(new(9,9)), "fallback circle changed");
            Vector2[] diamond = [new(0,-30), new(30,0), new(0,30), new(-30,0)];
            var region = new RadarClipRegion(diamond, Vector2.Zero, 10);
            Check(!region.Contains(new(11,0)), "zoom escaped viewport");
            var line = region.ClipSegment(Vector2.Zero, new(50,0)).Single();
            Check(Vector2.Distance(line.End, new(10,0)) < 0.001f, $"compass anchor escaped viewport: {line.End}");
        });
    }
    private static float Area(IReadOnlyList<Vector2> p)
    {
        var result = 0f;
        for (var i = 0; i < p.Count; ++i) result += p[i].X * p[(i+1)%p.Count].Y - p[i].Y * p[(i+1)%p.Count].X;
        return MathF.Abs(result) / 2;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
