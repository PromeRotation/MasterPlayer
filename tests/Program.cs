using System.Numerics;
using System.Text.Json;
using MasterPlayer.Dodge;
using MasterPlayer.Radar;
using MasterPlayer.Settings;
using PromeRotation.Helpers;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

var passed = 0;
if (args.Contains("--local-safezone"))
{
    LocalSafeZoneRegression.Run(Run);
    FollowRegression.Run(Run);
    Console.WriteLine($"PASS: {passed} local safe-zone scenarios");
    return;
}
if (args.Contains("--upstream-sync"))
{
    UpstreamSyncRegression.Run(Run);
    Console.WriteLine($"PASS: {passed} upstream sync scenarios");
    return;
}

for (var flags = 0; flags < 8; flags++)
{
    var radar = (flags & 1) != 0;
    var overlay = (flags & 2) != 0;
    var dodge = (flags & 4) != 0;
    Run($"registered danger: 3D={radar}, radar={overlay}, dodge={dodge}", () =>
    {
        var setting = new SafeZoneSetting();
        using var manager = new SafeZoneManager(setting);
        var registry = new SafeZoneRegistry(manager);
        var handle = registry.AddDelayedHit(new CircleTemplate(13f), new StaticTransform(Vector3.Zero, 0f), 5f);
        Check(manager.ContainsArea(handle), "The danger must be registered even with the host disabled.");
        Check(manager.BuildSnapshot(DateTime.UtcNow).DangerAreas.Count == 0, "Reproduce the empty snapshot before activation.");

        using var activation = new SafeZoneActivation(setting);
        activation.Update(radar, overlay, dodge);
        var snapshot = manager.BuildSnapshot(DateTime.UtcNow);
        Check(snapshot.DangerAreas.Count == (flags == 0 ? 0 : 1), "Enabled features must receive the registered telegraph.");
        Check(setting.EnableAutoMovement == dodge, "Visual features must not turn on automatic movement.");

        activation.Update(false, false, false);
        Check(!setting.EnableSafeZone && !setting.EnableAutoMovement, "Disabling all features must release owned host flags.");
        Check(manager.ContainsArea(handle), "Releasing host flags must not clear registered areas.");
    });
}

Run("turning off dodge keeps radar data", () =>
{
    var setting = new SafeZoneSetting();
    using var activation = new SafeZoneActivation(setting);
    activation.Update(true, true, true);
    activation.Update(true, true, false);
    Check(setting.EnableSafeZone && !setting.EnableAutoMovement, "Drawing must remain available after dodge is disabled.");
    activation.Update(false, true, false);
    Check(setting.EnableSafeZone, "The remaining radar still needs snapshots.");
    activation.Update(false, false, false);
    Check(!setting.EnableSafeZone, "The last visual feature must release the solver.");
    activation.Update(false, false, true);
    Check(setting.EnableSafeZone && setting.EnableAutoMovement, "Dodge must be able to reacquire both flags.");
});

for (var flags = 0; flags < 4; flags++)
{
    var originalSolve = (flags & 1) != 0;
    var originalMove = (flags & 2) != 0;
    Run($"preserve existing host settings: solve={originalSolve}, move={originalMove}", () =>
    {
        var setting = new SafeZoneSetting { EnableSafeZone = originalSolve, EnableAutoMovement = originalMove };
        var activation = new SafeZoneActivation(setting);
        activation.Update(true, true, true);
        Check(setting.EnableSafeZone && setting.EnableAutoMovement, "Dodge must enable both prerequisites.");
        activation.Dispose();
        activation.Dispose();
        activation.Update(true, true, true);
        Check(setting.EnableSafeZone == originalSolve && setting.EnableAutoMovement == originalMove,
            "Disposal must restore the original settings and prevent further activation.");
    });
}

Run("manual host shutdown is respected", () =>
{
    var setting = new SafeZoneSetting();
    using var activation = new SafeZoneActivation(setting);
    activation.Update(true, true, true);
    setting.EnableSafeZone = false;
    setting.EnableAutoMovement = false;
    activation.Update(true, true, true);
    Check(!setting.EnableSafeZone && !setting.EnableAutoMovement, "An update must not undo a manual host shutdown.");
    activation.Update(false, false, false);
    activation.Update(true, true, true);
    Check(setting.EnableSafeZone && setting.EnableAutoMovement, "A new activation must enable prerequisites again.");
});

Run("release does not resurrect pre-enabled flags closed by the user", () =>
{
    var setting = new SafeZoneSetting { EnableSafeZone = true, EnableAutoMovement = true };
    var activation = new SafeZoneActivation(setting);
    activation.Update(true, true, true);
    setting.EnableSafeZone = false;
    setting.EnableAutoMovement = false;
    activation.Dispose();
    Check(!setting.EnableSafeZone && !setting.EnableAutoMovement, "Disposal must preserve a later manual shutdown.");
});

Run("release preserves flags manually re-enabled after an observed shutdown", () =>
{
    var setting = new SafeZoneSetting();
    var activation = new SafeZoneActivation(setting);
    activation.Update(true, true, true);
    setting.EnableSafeZone = false;
    setting.EnableAutoMovement = false;
    activation.Update(true, true, true);
    setting.EnableSafeZone = true;
    setting.EnableAutoMovement = true;
    activation.Update(false, false, false);
    activation.Dispose();
    Check(setting.EnableSafeZone && setting.EnableAutoMovement, "Externally re-enabled flags must no longer belong to the plugin.");
});

Run("hot reload reapplies persisted feature choices", () =>
{
    var setting = new SafeZoneSetting();
    using (var first = new SafeZoneActivation(setting))
        first.Update(true, true, true);
    Check(!setting.EnableSafeZone && !setting.EnableAutoMovement, "The previous plugin instance must release its flags.");
    using var second = new SafeZoneActivation(setting);
    second.Update(true, true, true);
    Check(setting.EnableSafeZone && setting.EnableAutoMovement, "The next instance must reapply the enabled features.");
});

Run("AOE colors preserve custom RGB and alpha in both views", () =>
{
    var now = DateTime.UtcNow;
    var danger = new DangerAreaSnapshot(default, new CircleGeometry(Vector3.Zero, 3f),
        [new DangerWindow(now.AddSeconds(10), now.AddSeconds(11), now)]);
    var world = new AoeViewSettings { DangerFill = new Vector4(0.2f, 0.4f, 0.6f, 0.3f), FadeDanger = false };
    var radar = AoeViewSettings.CreateRadarDefaults();
    Check(world.GetDangerStyle(danger, now).FillColor == world.DangerFill, "A constant appearance must use the selected RGBA unchanged.");
    Check(radar.GetDangerStyle(danger, now.AddSeconds(10)).FillColor == radar.DangerFill, "Changing world colors must not affect radar colors.");
    world.SafeFill = new Vector4(0.1f, 0.3f, 0.8f, 0.15f);
    world.SafeOutline = new Vector4(0.2f, 0.5f, 1f, 0.7f);
    var safe = world.GetSafeStyle();
    Check(safe.FillColor == world.SafeFill && safe.StrokeColor == world.SafeOutline, "Safe fill and outline must keep their independent RGBA values.");
});

Run("AOE opacity supports fully transparent and animated limits", () =>
{
    var now = DateTime.UtcNow;
    var danger = new DangerAreaSnapshot(default, new CircleGeometry(Vector3.Zero, 3f),
        [new DangerWindow(now.AddSeconds(10), now.AddSeconds(11), now)]);
    var appearance = new AoeViewSettings { DangerFill = new Vector4(1f, 0f, 0f, 0f), DangerOutline = Vector4.Zero };
    Check(appearance.GetDangerStyle(danger, now.AddSeconds(5)).FillColor!.Value.W == 0f, "Transparent fill must remain transparent during the telegraph.");
    Check(appearance.GetDangerStyle(danger, now.AddSeconds(10)).StrokeColor!.Value.W == 0f, "Transparent outlines must not be forced opaque.");
    appearance.DangerFill.W = 0.4f;
    var start = appearance.GetDangerStyle(danger, now).FillColor!.Value.W;
    var middle = appearance.GetDangerStyle(danger, now.AddSeconds(5)).FillColor!.Value.W;
    var end = appearance.GetDangerStyle(danger, now.AddSeconds(10)).FillColor!.Value.W;
    Check(0f < start && start < middle && middle < end && MathF.Abs(end - 0.4f) < 0.00001f,
        "Telegraph opacity must grow to, and never exceed, the configured maximum.");
});

Run("AOE appearance survives JSON round trips and defaults", () =>
{
    var options = new JsonSerializerOptions { IncludeFields = true };
    var appearance = new AoeViewSettings { DangerFill = new Vector4(0.1f, 0.2f, 0.3f, 0.4f), FadeDanger = false };
    var restored = JsonSerializer.Deserialize<AoeViewSettings>(JsonSerializer.Serialize(appearance, options), options)!;
    Check(restored.DangerFill == appearance.DangerFill && !restored.FadeDanger, "Saved RGBA and fade choices must survive reload.");
    var defaults = JsonSerializer.Deserialize<AoeViewSettings>("{}", options)!;
    Check(defaults.DangerFill.W > 0f && defaults.SafeFill.W > 0f, "Missing settings from older configurations must retain visible defaults.");
});

foreach (var rotation in new[] { 0f, MathF.PI / 2f, MathF.PI, -MathF.PI / 2f, 0.731f })
{
    Run($"radar follows camera axes at rotation {rotation}", () =>
    {
        var center = new Vector3(120f, 17f, -420f);
        var screen = new Vector2(310f, 240f);
        var projection = new RadarProjection(center, screen, 4f, rotation);
        var forward = GeometryHelper.PointFromRotationDistanceXZ(center, rotation, 5f);
        var right = GeometryHelper.RightFromRotation(rotation);
        var rightWorld = center + new Vector3(right.X, 0f, right.Y) * 5f;
        Check(Vector2.Distance(projection.Project(forward), screen + new Vector2(0f, -20f)) < 0.001f,
            "Camera forward must appear above the center, not below it.");
        Check(Vector2.Distance(projection.Project(rightWorld), screen + new Vector2(20f, 0f)) < 0.001f,
            "Camera right must appear to the right, not to the left.");
        Check(Vector2.Distance(projection.Project(center + Vector3.UnitY * 100f), screen) < 0.001f,
            "World elevation must not alter the XZ map position.");
    });
}

Run("fixed radar keeps north up and east right", () =>
{
    var center = new Vector3(520f, 10f, 200f);
    var projection = new RadarProjection(center, Vector2.Zero, 2f, RadarProjection.NorthUpRotation);
    Check(Vector2.Distance(projection.Project(center - Vector3.UnitZ * 5f), new Vector2(0f, -10f)) < 0.001f,
        "World north (-Z) must be at the top in fixed mode.");
    Check(Vector2.Distance(projection.Project(center + Vector3.UnitX * 5f), new Vector2(10f, 0f)) < 0.001f,
        "World east (+X) must be at the right in fixed mode.");
});

Run("asymmetric PR contours preserve their pose in radar", () =>
{
    var center = new Vector3(37f, -5f, -83f);
    const float rotation = 0.73f;
    var geometry = new RayRectangleTemplate(1f, 3f, 12f).Resolve(center, rotation, null);
    var projection = new RadarProjection(center, new Vector2(400f, 200f), 2f, rotation);
    var points = geometry.GetContours()[0].Select(p => (projection.Project(p) - projection.ScreenCenter) / projection.Scale).ToArray();
    Check(MathF.Abs(points.Min(p => p.X) - 1f) < 0.001f && MathF.Abs(points.Max(p => p.X) - 3f) < 0.001f,
        "An asymmetric right-hand rectangle must not be mirrored or translated twice.");
    Check(MathF.Abs(points.Min(p => p.Y) + 12f) < 0.001f && MathF.Abs(points.Max(p => p.Y)) < 0.001f,
        "The resolved world contour must retain its forward extent.");
});

Run("line casts begin at the caster instead of half a length behind", () =>
{
    var template = AoeShapeDatabase.CreateTemplate(new AoeShapeDescriptor(AoeShapeKind.RayRectangle, 6f, 40f, true, 0f))!;
    var source = new Vector3(11f, 3f, -9f);
    const float rotation = 1.2f;
    var local = template.Resolve(source, rotation, null).GetContours()[0]
        .Select(p => GeometryHelper.WorldToLocal(p, source, rotation)).ToArray();
    Check(MathF.Abs(local.Min(p => p.Y) + 40f) < 0.001f && MathF.Abs(local.Max(p => p.Y)) < 0.001f,
        "A forward line must extend from the caster to its full range.");
    var centered = AoeShapeDatabase.CreateTemplate(new AoeShapeDescriptor(AoeShapeKind.Rectangle, 6f, 40f, false, 0f))!;
    var centeredLocal = centered.Resolve(source, rotation, null).GetContours()[0]
        .Select(p => GeometryHelper.WorldToLocal(p, source, rotation)).ToArray();
    Check(MathF.Abs(centeredLocal.Min(p => p.Y) + 20f) < 0.001f && MathF.Abs(centeredLocal.Max(p => p.Y) - 20f) < 0.001f,
        "Explicit centered rectangles must retain their separate geometry semantics.");
});

Run("per-AOE routing is independent of view and does not remove solver areas", () =>
{
    var rules = new Dictionary<string, AoeVisibility>();
    var visibility = new AoeVisibilityRegistry(rules);
    using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
    var registry = new SafeZoneRegistry(manager);
    var first = registry.AddPersistentDanger(new CircleTemplate(5f), new StaticTransform(Vector3.Zero, 0f));
    var second = registry.AddPersistentDanger(new CircleTemplate(5f), new StaticTransform(Vector3.Zero, 0f));
    visibility.Track(first, "action/100", "第一种机制");
    visibility.Track(second, "action/200", "第二种机制");
    rules["action/100"].World = false;
    rules["action/200"].Radar = false;
    Check(!visibility.GetVisibility(first).World && visibility.GetVisibility(first).Radar, "The first AOE must remain visible only on radar.");
    Check(visibility.GetVisibility(second).World && !visibility.GetVisibility(second).Radar, "The second same-shaped AOE must remain visible only in the world.");
    Check(manager.BuildSnapshot(DateTime.UtcNow).DangerAreas.Count == 2, "Display preferences must not change the solver snapshot.");

    registry.Remove(first);
    visibility.Update(manager);
    var next = registry.AddPersistentDanger(new CircleTemplate(5f), new StaticTransform(Vector3.Zero, 0f));
    var reloaded = new AoeVisibilityRegistry(rules);
    reloaded.Track(next, "action/100", "第一种机制");
    Check(!reloaded.GetVisibility(next).World && reloaded.GetVisibility(next).Radar, "Preferences must follow stable AOE identity across new handles and plugin reloads.");
});

Run("shape filters cover centered and forward rectangles without leaking between views", () =>
{
    var world = new AoeViewSettings { ShowRectangles = false, ShowDonuts = false };
    var radar = AoeViewSettings.CreateRadarDefaults();
    IGeometryShape[] hidden = [new RectangleTemplate(6f, 20f).Resolve(Vector3.Zero, 0f, null),
        RayRectangleTemplate.Symmetric(6f, 20f).Resolve(Vector3.Zero, 0f, null),
        new DonutTemplate(3f, 8f).Resolve(Vector3.Zero, 0f, null)];
    Check(hidden.All(g => !world.ShowsShape(g) && radar.ShowsShape(g)), "Shape choices must be independent per view.");
    Check(world.ShowsShape(new CircleGeometry(Vector3.Zero, 3f)), "Hiding one category must preserve other shapes.");
    var options = new JsonSerializerOptions { IncludeFields = true };
    var copy = JsonSerializer.Deserialize<AoeViewSettings>(JsonSerializer.Serialize(world, options), options)!;
    Check(hidden.All(g => !copy.ShowsShape(g)), "Shape choices must survive settings reload.");
});

Run("persistent obstacles obey source, category, shape and danger colors", () =>
{
    var rules = new Dictionary<string, AoeVisibility>();
    var visibility = new AoeVisibilityRegistry(rules);
    using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
    var handle = new SafeZoneRegistry(manager).AddObstacle(new CircleTemplate(2f), new StaticTransform(Vector3.Zero, 0f));
    visibility.Track(handle, "script/dragon/cloud", "毒气团");
    var geometry = manager.BuildSnapshot(DateTime.UtcNow).HardObstacles.Single().Geometry;
    var world = new AoeViewSettings { DangerFill = new Vector4(0.2f, 0.3f, 0.4f, 0.15f), DangerOutline = Vector4.Zero };
    var radar = AoeViewSettings.CreateRadarDefaults();
    rules["script/dragon/cloud"].World = false;
    Check(!world.IsVisible(geometry, false, visibility.GetVisibility(handle).World) &&
        radar.IsVisible(geometry, false, visibility.GetVisibility(handle).Radar), "An obstacle must support independent source routing.");
    Check(world.GetDangerStyle().FillColor == world.DangerFill && world.GetDangerStyle().StrokeColor == Vector4.Zero,
        "Persistent hazards must honor danger RGBA, including a transparent outline.");
    radar.ShowDanger = false;
    Check(!radar.IsVisible(geometry, false), "Hiding dangerous areas must hide damaging obstacles too.");
    radar.ShowDanger = true;
    radar.ShowCircles = false;
    Check(!radar.IsVisible(geometry, false), "Obstacle geometry must obey shape filters.");
    Check(manager.BuildSnapshot(DateTime.UtcNow).HardObstacles.Count == 1, "Hiding an obstacle must preserve solver collision data.");
});

Run("display-only AOE previews route immediately and never enter the solver", () =>
{
    var rules = new Dictionary<string, AoeVisibility>();
    var visibility = new AoeVisibilityRegistry(rules);
    using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
    var geometry = new RayRectangleGeometry(new Vector3(20f, 4f, 30f), 0.7f, 4f, 3f);
    visibility.AddPreview("script1_cloud", geometry, false, 250f, "script/dragon/preview", "毒气团移动预览");
    var preview = visibility.GetPreviews(DateTime.UtcNow).Single();
    var appearance = new AoeViewSettings();
    rules["script/dragon/preview"].World = false;
    Check(!appearance.IsVisible(preview.Geometry, preview.Safe, preview.Visibility.World) &&
        appearance.IsVisible(preview.Geometry, preview.Safe, preview.Visibility.Radar), "Changing a live preview's view rule must take effect immediately.");
    Check(ReferenceEquals(preview.Geometry, geometry), "Both renderers must receive the same PR world geometry, without a second transform.");
    var snapshot = manager.BuildSnapshot(DateTime.UtcNow);
    Check(snapshot.DangerAreas.Count == 0 && snapshot.HardObstacles.Count == 0 && snapshot.GoalAreas.Count == 0,
        "A speculative drawing must not alter solver regions.");
    Check(!visibility.GetPreviews(preview.ExpiresUtc).Any(), "A preview must stop drawing at its expiry, even before bookkeeping cleanup.");
});

Run("AOE preview replacement and script cleanup preserve other owners", () =>
{
    var rules = new Dictionary<string, AoeVisibility>();
    var visibility = new AoeVisibilityRegistry(rules);
    var first = new CircleGeometry(Vector3.Zero, 3f);
    var replacement = new CircleGeometry(Vector3.One, 4f);
    visibility.AddPreview("script1_a", first, false, 0f, "script/one/a", "第一种 AOE");
    visibility.AddPreview("script1_a", replacement, true, 0f, "script/one/a", "第一种 AOE");
    visibility.AddPreview("script1_b", first, false, 0f, "script/one/b", "第二种 AOE");
    visibility.AddPreview("script2_a", first, false, 0f, "script/two/a", "其他脚本");
    var current = visibility.GetPreviews(DateTime.UtcNow).ToArray();
    Check(current.Length == 3 && current.Single(p => p.Id == "script1_a") is { Safe: true } updated &&
        ReferenceEquals(updated.Geometry, replacement), "Updating an ID must replace, rather than accumulate, preview geometry.");
    rules["script/one/a"].Radar = false;
    Check(current.Single(p => p.Id == "script1_b").Visibility.Radar, "Different same-shaped AOEs within a script must retain independent choices.");
    Check(visibility.RemovePreview("script1_b") && !visibility.RemovePreview("script1_b"), "Explicit removal must be idempotent.");
    visibility.RemovePreviews("script1_");
    Check(visibility.GetPreviews(DateTime.UtcNow).Single().Id == "script2_a", "Script cleanup must preserve previews owned by another script.");
    Check(!rules["script/one/a"].Radar, "Cleanup must preserve saved display choices.");
});

CrucibleMechanicRegression.Run(Run);
ArrDungeonRegression.Run(Run);
UpstreamSyncRegression.Run(Run);
CrucibleAllRegression.Run(Run);
CrucibleTimingRegression.Run(Run);
CrucibleArrRegression.Run(Run);
LateDodgeRegression.Run(Run);
MindflayerLiveRegression.Run(Run);
High2LiveMovementRegression.Run(Run);
MovingHazardDispatchRegression.Run(Run);
Low2LiveRegression.Run(Run);
Low2MissingMechanicsRegression.Run(Run);
MindflayerWaterRegression.Run(Run);
RadarPathDashesRegression.Run(Run);
High2MigrationRegression.Run(Run);
High2FeedbackRegression.Run(Run);
RecordedLaudaRegression.Run(Run);
CastRotationRegression.Run(Run);
RadarClipRegression.Run(Run);
SafeZoneTemporalRegression.RunPrecision(Run);
SafeZoneTemporalRegression.Run(Run);
LocalSafeZoneRegression.Run(Run);
FollowRegression.Run(Run);
DodgeUptimeRegression.Run(Run);
RecordedDodgeRefinementRegression.Run(Run);
VisualScriptRegression.Run(Run);
ReplayRecordingRegression.Run(Run);
ReplayWorldRegression.Run(Run);
ReplayTimelineRegression.Run(Run);
MechanicDrawConvertRegression.Run(Run);
MechanicHintsConvertRegression.Run(Run);

Console.WriteLine($"PASS: {passed} regression scenarios");

void Run(string name, Action test)
{
    if (args.FirstOrDefault(a => a.StartsWith("--filter=")) is { } filter && !name.Contains(filter[9..], StringComparison.OrdinalIgnoreCase)) return;
    test();
    passed++;
    Console.WriteLine($"PASS {name}");
}

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
