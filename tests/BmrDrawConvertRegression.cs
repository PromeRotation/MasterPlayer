using System.Numerics;
using BossMod;
using MasterPlayer.BmrBridge;
using MasterPlayer.BmrRuntime;
using MasterPlayer.Spatial.Geometry;

internal static class BmrDrawConvertRegression
{
    private const uint ColorAoe = 0x80008080;
    private const uint ColorDanger = 0xff00ffff;
    private const uint ColorSafeFromAoe = 0x80008000;
    private const uint ColorSafe = 0xff00ff00;
    private const uint ColorEnemy = 0xff0000ff;

    public static void Run(Action<string, Action> run)
    {
        run("bmr draw convert maps circle, donut and fan parameters", () =>
        {
            var (shapes, lines, extras, skipped) = Convert([
                new BmrDrawZoneCircle(new WPos(10, 20), 5f, ColorAoe),
                new BmrDrawZoneDonut(new WPos(0, 0), 3f, 8f, ColorSafeFromAoe),
                new BmrDrawZoneCone(new WPos(0, 0), 0f, 12f, new Angle(0f), new Angle(30f * Angle.DegToRad), ColorDanger),
            ], 7f);
            Check(skipped == 0, "valid zone commands were skipped");
            Check(shapes.Count == 3 && lines.Count == 0 && extras.Count == 0, "commands landed in wrong buckets");
            var circle = (CircleGeometry)shapes[0].Shape;
            Check(circle.Radius == 5f && circle.Position == new Vector3(10, 7, 20), "circle center/radius/fallback-Y lost");
            Check(!shapes[0].Safe && shapes[1].Safe && !shapes[2].Safe, "AOE/safe color classification wrong");
            var donut = (DonutGeometry)shapes[1].Shape;
            Check(donut.InnerRadius == 3f && donut.OuterRadius == 8f, "donut radii lost");
            var fan = (FanGeometry)shapes[2].Shape;
            Check(MathF.Abs(fan.Rotation) < 1e-6f && MathF.Abs(fan.AngleRadians - 60f * Angle.DegToRad) < 1e-4f, "fan direction/opening lost");
            Check(fan.Contains(new Vector3(0, 7, 10)) && !fan.Contains(new Vector3(10, 7, 0)), "fan covers the wrong sector");
        });

        run("bmr draw convert rect uses front/back lengths and center offset", () =>
        {
            var (shapes, _, _, skipped) = Convert([
                new BmrDrawZoneRect(new WPos(0, 100), new WDir(0, 1), 10f, 4f, 3f, ColorAoe),
            ], 2f);
            Check(skipped == 0 && shapes.Count == 1, "rect command not converted");
            var rect = (RectangleGeometry)shapes[0].Shape;
            Check(rect.Width == 6f && rect.Length == 14f, "rect width/length conversion wrong");
            Check(Vector3.Distance(rect.Position, new Vector3(0, 2, 103)) < 1e-4f, "rect center not offset along direction");
            Check(MathF.Abs(rect.Rotation) < 1e-6f, "rect rotation not aligned to +Z");
            Check(rect.Contains(new Vector3(0, 2, 109.9f)) && rect.Contains(new Vector3(0, 2, 96.1f)), "rect front/back coverage wrong");
            Check(!rect.Contains(new Vector3(0, 2, 111.1f)) && !rect.Contains(new Vector3(3.1f, 2, 100)), "rect bounds exceeded");
        });

        run("bmr draw convert cross splits into two perpendicular rectangles", () =>
        {
            var (shapes, _, _, skipped) = Convert([
                new BmrDrawZoneCross(new WPos(50, 50), new Angle(MathF.PI / 4f), 10f, 2f, ColorAoe),
            ]);
            Check(skipped == 0 && shapes.Count == 2, "cross did not produce two arms");
            var armA = (RectangleGeometry)shapes[0].Shape;
            var armB = (RectangleGeometry)shapes[1].Shape;
            Check(armA.Width == 4f && armA.Length == 20f && armB.Width == 4f && armB.Length == 20f, "cross arm dimensions wrong");
            Check(armA.Position == armB.Position && armA.Position == new Vector3(50, 0, 50), "cross center moved");
            Check(MathF.Abs(armA.Rotation - MathF.PI / 4f) < 1e-6f && MathF.Abs(armB.Rotation - 3f * MathF.PI / 4f) < 1e-6f, "cross arms not perpendicular");
        });

        run("bmr draw convert ring fan falls back to polygon", () =>
        {
            var (shapes, _, _, skipped) = Convert([
                new BmrDrawZoneCone(new WPos(0, 0), 4f, 10f, new Angle(MathF.PI / 2f), new Angle(45f * Angle.DegToRad), ColorAoe),
            ]);
            Check(skipped == 0 && shapes.Count == 1, "ring fan not converted");
            var polygon = (WorldPolygonGeometry)shapes[0].Shape;
            Check(polygon.WorldPoints.Count > 6, "ring fan polygon has too few vertices");
            Check(polygon.Contains(new Vector3(7, 0, 0)), "ring fan misses mid-sector point");
            Check(!polygon.Contains(new Vector3(1, 0, 0)), "ring fan leaks into inner radius");
            Check(!polygon.Contains(new Vector3(0, 0, 7)), "ring fan leaks outside the sector");
        });

        run("bmr draw convert capsule and arc capsule approximate without throwing", () =>
        {
            var (shapes, _, _, skipped) = Convert([
                new BmrDrawZoneCapsule(new WPos(0, 0), new WDir(0, 1), 2f, 6f, ColorAoe),
                new BmrDrawZoneArcCapsule(new WPos(10, 0), new WPos(0, 0), new Angle(MathF.PI / 2f), 1f, ColorAoe),
            ]);
            Check(skipped == 0 && shapes.Count == 2, "capsule commands not converted");
            var capsule = shapes[0].Shape;
            Check(capsule.Contains(new Vector3(0, 0, 3)) && capsule.Contains(new Vector3(0, 0, 7.9f)) && capsule.Contains(new Vector3(1.9f, 0, 3)), "capsule coverage wrong");
            Check(!capsule.Contains(new Vector3(0, 0, 8.5f)) && !capsule.Contains(new Vector3(2.5f, 0, 3)), "capsule exceeds its radius");
            var arc = shapes[1].Shape;
            Check(arc.Contains(new Vector3(7.07f, 0, -7.07f)), "arc capsule misses mid-arc point");
            Check(!arc.Contains(new Vector3(10, 0, 10)), "arc capsule leaks outside the sweep");
        });

        run("bmr draw convert triangle soup and complex polygons become world polygons", () =>
        {
            var (shapes, _, _, skipped) = Convert([
                new BmrDrawZoneTriangles([
                    new WPos(0, 0), new WPos(4, 0), new WPos(0, 4),
                    new WPos(4, 0), new WPos(4, 4), new WPos(0, 4),
                ], ColorAoe),
                new BmrDrawComplexPoly(1, [
                    new BmrDrawPolygonPart([new WPos(20, 20), new WPos(24, 20), new WPos(24, 24), new WPos(20, 24)],
                        [[new WPos(21, 21), new WPos(22, 21), new WPos(22, 22)]]),
                ], ColorAoe),
                new BmrDrawComplexPoly(0, [new BmrDrawPolygonPart([new WPos(0, 0), new WPos(1, 0), new WPos(0, 1)], [])], 0xffffffffu),
            ]);
            Check(skipped == 1, "white arena border must be skipped");
            Check(shapes.Count == 3, "triangle soup or complex poly parts missing");
            Check(shapes[0].Shape.Contains(new Vector3(1, 0, 1)) && shapes[1].Shape.Contains(new Vector3(3, 0, 3)), "triangle soup coverage wrong");
            Check(shapes[2].Shape.Contains(new Vector3(21.5f, 0, 21.5f)), "complex poly exterior lost (holes intentionally ignored)");
        });

        run("bmr draw convert maps lines, polylines, triangles, quads and extras", () =>
        {
            var (shapes, lines, extras, skipped) = Convert([
                new BmrDrawLine(new WPos(0, 0), new WPos(1, 1), ColorDanger),
                new BmrDrawPolyline([new WPos(0, 0), new WPos(2, 0), new WPos(2, 2)], true, ColorSafe),
                new BmrDrawTriangle(new WPos(0, 0), new WPos(4, 0), new WPos(0, 4), ColorDanger, false),
                new BmrDrawTriangle(new WPos(0, 0), new WPos(4, 0), new WPos(0, 4), ColorDanger, true),
                new BmrDrawQuad(new WPos(0, 0), new WPos(4, 0), new WPos(4, 4), new WPos(0, 4), ColorDanger),
                new BmrDrawEye(new WPos(5, 5), true, ColorEnemy),
                new BmrDrawTextWorld(new WPos(6, 6), "躲开", ColorDanger, 17f),
                new BmrDrawTextWorld(new WPos(7, 7), "提示", 0xffffffffu, 17f),
                new BmrDrawSignMarker(new WPos(8, 8), 61301, Sign.Attack1),
                new BmrDrawIconWorld(new WPos(9, 9), 0xE000, ColorDanger, 17f),
                new BmrDrawActorMarker(new WPos(10, 10), new Angle(0f), ColorSafe, true),
                new BmrDrawCardinalNames(0xffffffffu, 0xffffffffu, 0xffffffffu, 0xffffffffu, 17f),
            ]);
            Check(skipped == 3, "icon/actor-marker/cardinal commands must be skipped");
            Check(shapes.Count == 1 && shapes[0].Shape is WorldPolygonGeometry, "filled triangle did not become a polygon");
            Check(lines.Count == 1 + 3 + 3 + 4, "line/polyline/triangle/quad segment counts wrong");
            Check(lines[0].Start == new Vector3(0, 0, 0) && lines[0].End == new Vector3(1, 0, 1), "line endpoints lost fallback Y mapping");
            Check(lines[1].Safe && !lines[0].Safe, "line safe classification wrong");
            Check(extras.Count == 4, "eye/text/sign extras missing");
            Check(extras[0].Kind == BmrRadarExtraKind.Eye && extras[0].Danger, "eye extra lost its danger flag");
            Check(extras[1].Kind == BmrRadarExtraKind.Text && extras[1].Danger && extras[1].Text == "躲开", "danger text extra wrong");
            Check(extras[2].Kind == BmrRadarExtraKind.Text && !extras[2].Danger, "neutral text misclassified as danger");
            Check(extras[3].Kind == BmrRadarExtraKind.Sign && extras[3].Text == "1", "sign label mapping wrong");
            Check(BmrDrawConvert.SignLabel(Sign.Bind2) == "B2" && BmrDrawConvert.SignLabel(Sign.Cross) == "×"
                && BmrDrawConvert.SignLabel(Sign.Attack8) == "8" && BmrDrawConvert.SignLabel(Sign.Ignore1) == "I1", "sign labels wrong");
        });

        run("bmr draw convert rejects degenerate commands without throwing", () =>
        {
            var (shapes, lines, extras, skipped) = Convert([
                new BmrDrawZoneCircle(new WPos(0, 0), 0f, ColorAoe),
                new BmrDrawZoneDonut(new WPos(0, 0), 8f, 3f, ColorAoe),
                new BmrDrawZoneCone(new WPos(0, 0), 0f, float.NaN, new Angle(0f), new Angle(1f), ColorAoe),
                new BmrDrawZoneRect(new WPos(0, 0), new WDir(0, 0), 5f, 0f, 2f, ColorAoe),
                new BmrDrawZoneTriangles([new WPos(0, 0), new WPos(1, 0)], ColorAoe),
                new BmrDrawPolyline([new WPos(0, 0)], false, ColorDanger),
            ]);
            Check(skipped == 6 && shapes.Count == 0 && lines.Count == 0 && extras.Count == 0, "degenerate commands leaked into output");
        });
    }

    private static (List<BmrPreviewShape> Shapes, List<BmrPreviewLine> Lines, List<BmrRadarExtra> Extras, int Skipped) Convert(
        BmrDrawCommand[] commands, float fallbackY = 0f)
    {
        var shapes = new List<BmrPreviewShape>();
        var lines = new List<BmrPreviewLine>();
        var extras = new List<BmrRadarExtra>();
        var skipped = BmrDrawConvert.Convert(commands, fallbackY, shapes, lines, extras);
        return (shapes, lines, extras, skipped);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
