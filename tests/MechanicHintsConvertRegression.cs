using System.Numerics;
using BossMod;
using MasterPlayer.MechanicHost;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

internal static class MechanicHintsConvertRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("mechanic hints convert maps circle and donut directly", () =>
        {
            var circle = MechanicHintsConvert.ConvertForbiddenZone(new SDCircle(new WPos(10, 20), 5f), 7f, Vector3.Zero, 0f, out var circleKind);
            Check(circleKind == MechanicNavZoneKind.Direct && circle.Count == 1, "circle not converted directly");
            var circlePrim = circle[0];
            Check(circlePrim.Template is CircleTemplate { Radius: 5f }, "circle radius lost");
            Check(circlePrim.Position == new Vector3(10, 7, 20) && circlePrim.Rotation == 0f, "circle origin lost");

            var donut = MechanicHintsConvert.ConvertForbiddenZone(new SDDonut(new WPos(1, 2), 3f, 8f), 0f, Vector3.Zero, 0f, out var donutKind);
            Check(donutKind == MechanicNavZoneKind.Direct && donut.Count == 1, "donut not converted directly");
            Check(donut[0].Template is DonutTemplate { InnerRadius: 3f, OuterRadius: 8f }, "donut radii lost");
        });

        run("mechanic hints convert recovers cone direction and half angle from stored normals", () =>
        {
            var narrow = MechanicHintsConvert.ConvertForbiddenZone(
                new SDCone(new WPos(5, 5), 10f, new Angle(0f), new Angle(30f * Angle.DegToRad)), 0f, Vector3.Zero, 0f, out var narrowKind);
            Check(narrowKind == MechanicNavZoneKind.Direct && narrow.Count == 1, "narrow cone not converted directly");
            var fan = (FanTemplate)narrow[0].Template;
            Check(fan.Radius == 10f && MathF.Abs(fan.AngleRadians - 60f * Angle.DegToRad) < 1e-3f, "narrow cone opening lost");
            Check(MathF.Abs(NormalizePi(narrow[0].Rotation)) < 1e-3f, "narrow cone direction lost");
            var fanShape = (FanGeometry)narrow[0].Template.Resolve(narrow[0].Position, narrow[0].Rotation, null);
            Check(fanShape.Contains(new Vector3(5, 0, 14)) && !fanShape.Contains(new Vector3(14, 0, 5)), "narrow cone covers wrong sector");

            var wide = MechanicHintsConvert.ConvertForbiddenZone(
                new SDCone(new WPos(0, 0), 10f, new Angle(0f), new Angle(120f * Angle.DegToRad)), 0f, Vector3.Zero, 0f, out _);
            var wideFan = (FanTemplate)wide[0].Template;
            Check(MathF.Abs(wideFan.AngleRadians - 240f * Angle.DegToRad) < 1e-2f, "wide cone (>180°) opening misrecovered");
            var wideShape = (FanGeometry)wide[0].Template.Resolve(wide[0].Position, wide[0].Rotation, null);
            Check(wideShape.Contains(new Vector3(5, 0, 0)) && !wideShape.Contains(new Vector3(0, 0, -5)), "wide cone covers wrong sector");
        });

        run("mechanic hints convert maps rect and splits cross into two rectangles", () =>
        {
            var rect = MechanicHintsConvert.ConvertForbiddenZone(
                new SDRect(new WPos(0, 100), new Angle(0f), 10f, 4f, 3f), 2f, Vector3.Zero, 0f, out _);
            Check(rect.Count == 1 && rect[0].Template is RectangleTemplate { Width: 6f, Length: 14f }, "rect width/length lost");
            Check(Vector3.Distance(rect[0].Position, new Vector3(0, 2, 103)) < 1e-4f && MathF.Abs(rect[0].Rotation) < 1e-6f,
                "rect center/rotation wrong");

            var cross = MechanicHintsConvert.ConvertForbiddenZone(
                new SDCross(new WPos(50, 50), new Angle(MathF.PI / 4f), 10f, 2f), 0f, Vector3.Zero, 0f, out var crossKind);
            Check(crossKind == MechanicNavZoneKind.Split && cross.Count == 2, "cross not split into two arms");
            Check(cross.All(p => p.Template is RectangleTemplate { Width: 4f, Length: 20f }), "cross arm dimensions wrong");
        });

        run("mechanic hints convert approximates donut sector, capsule and arc capsule as polygons", () =>
        {
            var sector = MechanicHintsConvert.ConvertForbiddenZone(
                new SDDonutSector(new WPos(0, 0), 3f, 8f, new Angle(MathF.PI / 2f), new Angle(45f * Angle.DegToRad)), 0f, Vector3.Zero, 0f, out _);
            Check(sector.Count == 1, "donut sector not converted");
            var sectorShape = Resolve(sector[0]);
            Check(sectorShape.Contains(new Vector3(5.5f, 0, 0)), "donut sector misses mid-sector point");
            Check(!sectorShape.Contains(new Vector3(0, 0, 5.5f)) && !sectorShape.Contains(new Vector3(1, 0, 0)), "donut sector leaks");

            var capsule = MechanicHintsConvert.ConvertForbiddenZone(
                new SDCapsule(new WPos(0, 0), new Angle(0f), 6f, 2f), 0f, Vector3.Zero, 0f, out _);
            var capsuleShape = Resolve(capsule[0]);
            Check(capsuleShape.Contains(new Vector3(0, 0, 3)) && !capsuleShape.Contains(new Vector3(2.5f, 0, 3)), "capsule coverage wrong");

            var arc = MechanicHintsConvert.ConvertForbiddenZone(
                new SDArcCapsule(new WPos(10, 0), new WPos(0, 0), new Angle(MathF.PI / 2f), 1f), 0f, Vector3.Zero, 0f, out _);
            var arcShape = Resolve(arc[0]);
            Check(arcShape.Contains(new Vector3(7.07f, 0, -7.07f)), "arc capsule misses mid-arc point");
            Check(!arcShape.Contains(new Vector3(10, 0, 10)), "arc capsule leaks outside the sweep");
        });

        run("mechanic hints convert splits unions, samples intersections and skips complements", () =>
        {
            var union = MechanicHintsConvert.ConvertForbiddenZone(
                new SDUnion([new SDCircle(new WPos(0, 0), 5f), new SDCircle(new WPos(20, 0), 5f)]), 0f, Vector3.Zero, 0f, out var unionKind);
            Check(unionKind == MechanicNavZoneKind.Split && union.Count == 2, "union of circles not split");

            var intersection = MechanicHintsConvert.ConvertForbiddenZone(
                new SDIntersection([new SDCircle(new WPos(-3, 0), 5f), new SDCircle(new WPos(3, 0), 5f)]), 0f, Vector3.Zero, 25f, out var intersectionKind);
            Check(intersectionKind == MechanicNavZoneKind.Sampled && intersection.Count >= 1, $"intersection not sampled (kind={intersectionKind}, count={intersection.Count})");
            var lens = Resolve(intersection[0]);
            Check(lens.Contains(Vector3.Zero), "sampled intersection misses the overlap center");
            Check(!lens.Contains(new Vector3(8, 0, 0)), "sampled intersection exceeds the overlap");

            var complement = MechanicHintsConvert.ConvertForbiddenZone(new SDInvertedCircle(new WPos(0, 0), 5f), 0f, Vector3.Zero, 25f, out var complementKind);
            Check(complementKind == MechanicNavZoneKind.ComplementSkipped && complement.Count == 0, "inverted circle must be skipped, not misdrawn");
        });

        run("mechanic hints convert samples unrecognized binary fields with hit semantics", () =>
        {
            var weird = MechanicHintsConvert.ConvertForbiddenZone(new WeirdCircle(new WPos(0, 0), 5f), 0f, Vector3.Zero, 20f, out var kind);
            Check(kind == MechanicNavZoneKind.Sampled && weird.Count >= 1, "unrecognized shape not sampled");
            var polygon = Resolve(weird[0]);
            Check(polygon is WorldPolygonGeometry { WorldPoints.Count: > 0 }, "sampled polygon has no vertices");
            Check(polygon.Contains(Vector3.Zero), "sampled polygon misses the shape center");
            Check(!polygon.Contains(new Vector3(15, 0, 0)), "sampled polygon covers far point");
        });

        run("mechanic hints convert maps activation to delay or persistent", () =>
        {
            var now = DateTime.UtcNow;
            Check(MechanicHintsConvert.MapActivation(default, now) == (0f, true), "default activation must be persistent");
            Check(MechanicHintsConvert.MapActivation(now.AddSeconds(-1), now) == (0f, true), "past activation must be persistent");
            var future = MechanicHintsConvert.MapActivation(now.AddSeconds(3), now);
            Check(!future.Persistent && Math.Abs(future.DelaySeconds - 3f) < 0.05f, "future activation must become a delayed hit");
        });

        run("mechanic hints convert maps arena bounds to MP boundary geometry", () =>
        {
            var circle = MechanicHintsConvert.ConvertBounds(new ArenaBoundsCircle(20f), new WPos(100, 200), 3f);
            Check(circle is CircleGeometry { Radius: 20f } && circle.Position == new Vector3(100, 3, 200), "circle bounds lost");

            var rect = MechanicHintsConvert.ConvertBounds(new ArenaBoundsRect(10f, 15f, new Angle(MathF.PI / 2f)), new WPos(0, 0), 0f);
            Check(rect is RectangleGeometry { Width: 20f, Length: 30f }, "rect bounds dimensions lost");
            Check(MathF.Abs(rect!.Rotation - MathF.PI / 2f) < 1e-6f, "rect bounds rotation lost");

            var custom = MechanicHintsConvert.ConvertBounds(new ArenaBoundsCustom([new Polygon(default, 10f, 4)]), new WPos(0, 0), 0f);
            Check(custom is WorldPolygonGeometry { WorldPoints.Count: >= 4 }, "custom bounds polygon lost");
            Check(custom!.Contains(Vector3.Zero) && !custom.Contains(new Vector3(20, 0, 0)), "custom bounds coverage wrong");
        });

        run("mechanic nav differ keeps identical zones, updates changed and removes vanished", () =>
        {
            var setting = new SafeZoneSetting { EnableSafeZone = true };
            using var manager = new SafeZoneManager(setting);
            var registry = new SafeZoneRegistry(manager);
            var differ = new MechanicNavAreaDiffer();
            var now = DateTime.UtcNow;

            var primA = new MechanicNavPrimitive(new CircleTemplate(5f), Vector3.Zero, 0f, "sigA");
            var requestA = new MechanicNavZoneRequest("k1", [primA], 0f, true, "sigA");
            differ.Sync(registry, manager, [requestA], [], 0.5f, null);
            Check(manager.BuildSnapshot(now).DangerAreas.Count == 1, "danger zone not registered");
            Check(manager.BuildSnapshot(now).DangerAreas[0].Geometry is CircleGeometry { Radius: 5.5f }, "danger margin not applied");

            differ.Sync(registry, manager, [requestA], [], 0.5f, null);
            Check(manager.BuildSnapshot(now).DangerAreas.Count == 1 && differ.ZoneCount == 1, "identical frame rebuilt the zone");

            var requestB = requestA with { Primitives = [new MechanicNavPrimitive(new CircleTemplate(7f), Vector3.Zero, 0f, "sigB")], Signature = "sigB" };
            differ.Sync(registry, manager, [requestB], [], 0.5f, null);
            Check(manager.BuildSnapshot(now).DangerAreas[0].Geometry is CircleGeometry { Radius: 7.5f }, "changed zone not updated in place");

            var delayed = new MechanicNavZoneRequest("k2", [primA], 3f, false, "sigC");
            differ.Sync(registry, manager, [requestB, delayed], [], 0.5f, null);
            var snapshot = manager.BuildSnapshot(now);
            Check(snapshot.DangerAreas.Count == 2, "delayed zone not registered");
            var delayedWindow = snapshot.DangerAreas.SelectMany(a => a.DangerWindows).Single(w => w.TelegraphFrom != null);
            Check(delayedWindow.DangerFrom > now, "delayed zone lost its telegraph window");

            differ.Sync(registry, manager, [requestB], [], 0.5f, null);
            Check(manager.BuildSnapshot(now).DangerAreas.Count == 1, "vanished zone not removed");

            differ.Sync(registry, manager, [], [], 0.5f, null);
            Check(manager.BuildSnapshot(now).DangerAreas.Count == 0 && differ.ZoneCount == 0, "clear left stale zones");
        });
    }

    private static IGeometryShape Resolve(MechanicNavPrimitive prim)
        => prim.Template.Resolve(prim.Position, prim.Rotation, null);

    private static float NormalizePi(float angle)
    {
        angle %= 2f * MathF.PI;
        if (angle > MathF.PI) angle -= 2f * MathF.PI;
        if (angle < -MathF.PI) angle += 2f * MathF.PI;
        return angle;
    }

    private sealed class WeirdCircle(WPos center, float radius) : ShapeDistance
    {
        public override float Distance(in WPos p) => (p - center).Length() - radius;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
