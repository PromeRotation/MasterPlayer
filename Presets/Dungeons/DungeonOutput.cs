using System.Numerics;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DungeonOutput(ScriptSafeZoneApi safeZone, ScriptDrawApi draw, SafeZoneManager manager,
    Action<IReadOnlyList<SolverTeleporter>>? publishTeleporters = null)
{
    private readonly Dictionary<string, (DungeonPendingCast Cast, SafeZoneAreaHandle Handle)> _areas = [];
    private readonly HashSet<string> _previews = [];
    private readonly Dictionary<string, (DungeonAmbientAoe Aoe, SafeZoneAreaHandle Handle)> ambientAreas = [];
    private readonly Dictionary<string, (Vector4[] Inputs, WorldPolygonTemplate[] Shapes)> adaptedGeometry = [];
    private readonly HashSet<string> activeAdaptations = [];
    private IGeometryShape? _boundary;

    public void Apply(DungeonEncounter encounter, DateTime now, bool ripplesEnabled)
    {
        var navigationBoundary = encounter.Arena;
        if (encounter.IsQuantum && (!encounter.Quantum.Active || encounter.Quantum.CurrentSlot < 0)
            || encounter.IsAlteRoite && (!encounter.AlteRoite.Active || encounter.AlteRoite.Slot < 0
                || encounter.AlteRoite.SourcePlayer == null || !encounter.AlteRoite.Actors.ContainsKey(encounter.PlayerId))
            || encounter.IsHalicarnassus && (!encounter.Halicarnassus.Active || encounter.Halicarnassus.Slot < 0
                || encounter.Halicarnassus.SourcePlayer == null || !encounter.Halicarnassus.Actors.ContainsKey(encounter.PlayerId))
            || encounter.IsWickedThunder && (!encounter.WickedThunder.Active || encounter.WickedThunder.Slot < 0)
            || encounter.IsKefka && (!encounter.Kefka.Active || encounter.Kefka.Slot < 0)
            || encounter.IsThordan && (!encounter.Thordan.Active || encounter.Thordan.Slot < 0))
            navigationBoundary = null;
        if (!ReferenceEquals(_boundary, navigationBoundary))
        {
            Clear();
            _boundary = navigationBoundary;
            if (_boundary != null) manager.SetArenaBoundary(_boundary);
        }
        var wanted = new HashSet<string>();
        var casts = encounter.OutputCasts.Where(c => c.Value.Rule.Shape != null).ToDictionary();
        foreach (var source in _areas.Keys.Where(k => !casts.ContainsKey(k)).ToArray()) RemoveArea(source);
        foreach (var (source, cast) in casts)
        {
            using var group = safeZone.BeginDrawingGroup(encounter.Active!.Name + " / " + cast.Rule.Name);
            if ((cast.UntilObservedEnd || now <= cast.FinishAt.AddSeconds(0.6))
                && (!_areas.TryGetValue(source, out var previous) || !ReferenceEquals(previous.Cast, cast) || !manager.ContainsArea(previous.Handle)))
            {
                RemoveArea(source);
                var handle = safeZone.AddDangerSnapshot(cast.Rule.Shape!, new StaticTransform(cast.Location, cast.Rotation),
                    [new(cast.FinishAt, cast.UntilObservedEnd ? null : cast.FinishAt.AddSeconds(0.6), cast.StartedAt)]);
                safeZone.SetNavigationOnly(handle, true);
                _areas[source] = (cast, handle);
            }
            var id = source;
            wanted.Add(id);
            draw.AddAoePreview(id, cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null), false, false, 1);
        }
        activeAdaptations.Clear();
        var ambient = AdaptAmbient(encounter).Where(a => a.ExpiresAt == null || a.ExpiresAt >= now).ToArray();
        foreach (var key in adaptedGeometry.Keys.Where(key => !activeAdaptations.Contains(key)).ToArray())
            adaptedGeometry.Remove(key);
        var ambientIds = ambient.Where(a => a.Navigate).Select(a => a.Id).ToHashSet();
        foreach (var id in ambientAreas.Keys.Where(id => !ambientIds.Contains(id)).ToArray())
        {
            safeZone.Remove(ambientAreas[id].Handle);
            ambientAreas.Remove(id);
        }
        foreach (var aoe in ambient)
        {
            using var group = safeZone.BeginDrawingGroup((encounter.Active?.Name ?? encounter.Definitions[0].Name) + " / " + aoe.Name);
            if (aoe.Navigate && (!ambientAreas.TryGetValue(aoe.Id, out var previous) || previous.Aoe != aoe || !manager.ContainsArea(previous.Handle)))
            {
                if (ambientAreas.Remove(aoe.Id, out previous)) safeZone.Remove(previous.Handle);
                var binding = new StaticTransform(aoe.Position, aoe.Rotation);
                SafeZoneAreaHandle handle;
                if (aoe.Obstacle)
                    handle = safeZone.AddObstacle(aoe.Shape, binding);
                else if (aoe.Safe)
                {
                    handle = safeZone.AddPersistentSafeArea(aoe.Shape, binding, aoe.GoalPriority);
                    var goalFrom = aoe.ExpiresAt != null ? aoe.Activation ?? now : now;
                    manager.UpdateArea(handle, goalWindows: [new GoalWindow(goalFrom, aoe.ExpiresAt, aoe.GoalPriority,
                        aoe.PreferredGoalPosition != null ? PositionPreference.Center : PositionPreference.Nearest)
                    {
                        OverrideDangerMargin = aoe.OverrideDangerMargin,
                        PreferredPosition = aoe.PreferredGoalPosition,
                    }]);
                }
                // Upstream default(DateTime) denotes an immediate constraint, not a year-one hit.
                else if (aoe.Activation is { } activation && activation != default)
                    handle = safeZone.AddDangerSnapshot(aoe.Shape, binding, [new(activation, aoe.ExpiresAt, now)
                    {
                        HasMotionEnvelope = aoe.HasMotionEnvelope,
                        HasExplicitTiming = aoe.ExpiresAt != null,
                        RequireOutwardExit = aoe.RequireOutwardExit,
                    }]);
                else
                    handle = safeZone.AddPersistentDanger(aoe.Shape, binding);
                safeZone.SetNavigationOnly(handle, true);
                ambientAreas[aoe.Id] = (aoe, handle);
            }
            if (!aoe.Display) continue;
            wanted.Add(aoe.Id);
            draw.AddAoePreview(aoe.Id, aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null), aoe.Safe, aoe.OutlineOnly, 1, aoe.Unclipped, aoe.Color);
        }
        foreach (var line in encounter.ThordanLines())
        {
            using var group = safeZone.BeginDrawingGroup(line.Id.StartsWith("thordan/chain/")
                ? "圆桌骑士（极） / Burning Chains" : "圆桌骑士（极） / Spiral Pierce");
            wanted.Add(line.Id);
            draw.AddLinePreview(line.Id, line.Start, line.End, false);
        }
        foreach (var line in encounter.SleepBeamLines())
        {
            using var group = safeZone.BeginDrawingGroup(encounter.Active!.Name + " / Burning Beam");
            wanted.Add(line.Id);
            draw.AddLinePreview(line.Id, line.Start, line.End, false);
        }
        foreach (var line in encounter.AlphaLines())
        {
            using var group = safeZone.BeginDrawingGroup(encounter.Active!.Name + (encounter.IsBarPassageP1 ? " / Toxic Spit" : " / Feeding Time"));
            wanted.Add(line.Id);
            draw.AddLinePreview(line.Id, line.Start, line.End, line.Safe);
        }
        foreach (var line in encounter.EurekaLines())
        {
            using var group = safeZone.BeginDrawingGroup(encounter.Active!.Name + " / Whei Morn");
            wanted.Add(line.Id);
            draw.AddLinePreview(line.Id, line.Start, line.End, false);
        }
        foreach (var line in encounter.EurekaChimeraLines())
        {
            using var group = safeZone.BeginDrawingGroup(encounter.Active!.Name + " / Stretch tether");
            wanted.Add(line.Id);
            draw.AddLinePreview(line.Id, line.Start, line.End, line.Safe);
        }
        foreach (var gaze in encounter.Gazes)
        {
            if (gaze.Rule.Action is 11900 or 11922 or 14691 or 14951 or 14875 or 36297 or 9613 or 16025 or 36624 or 3281 or 6403 or 37523 or 37537 or 17198 or 21090 or 40743 or 40757 || NativeTreasureCatalog.GazeAction(gaze.Rule.Action)) continue;
            using var group = safeZone.BeginDrawingGroup("PetrifyingEye / 背对视线");
            var id = $"gaze/{gaze.Source}";
            wanted.Add(id);
            draw.AddAoePreview(id, new CircleGeometry(gaze.Location, .7f), false, true, 1);
        }
        if (ripplesEnabled)
        {
            using var group = safeZone.BeginDrawingGroup("TrashSpawning");
            foreach (var actor in encounter.ActiveRipples)
            {
                var id = $"ripple/{actor.Id}";
                wanted.Add(id);
            draw.AddAoePreview(id, new RectangleGeometry(actor.Position, 0, 4, 6), true, true, 1,
                color: new Vector4(.7529412f, .7529412f, .7529412f, 1));
            }
        }
        foreach (var id in _previews.Where(id => !wanted.Contains(id))) draw.Remove(id);
        _previews.Clear();
        _previews.UnionWith(wanted);
        publishTeleporters?.Invoke(encounter.NativeTeleporters);
    }

    private IEnumerable<DungeonAmbientAoe> AdaptAmbient(DungeonEncounter encounter)
    {
        if (encounter.EurekaThermalSides() is { } thermal && encounter.Arena is { } thermalArena)
        {
            var polygons = AdaptPolygons("eureka/excalibur/thermal",
                [new(thermalArena.Position, 19.5f), new(thermal.Safe, thermal.SafeRotation)],
                () => GeometryGridSampler.Sample(thermalArena.Position, 19.5f,
                    (x, z) => EurekaThermalDistance(thermal.Safe, thermal.SafeRotation, x, z)).Select(p => p.WorldPoints));
            for (var i = 0; i < polygons.Length; ++i)
                yield return new($"eureka/excalibur/thermal/inverse/{i}", "Thermal Divide safe side",
                    polygons[i], polygons[i].Resolve(default, 0, null).Position,
                    Display: false, Activation: thermal.Activation);
        }
        var swords = encounter.EurekaSteelSafeSwords();
        if (swords.Length == 2 && encounter.Arena is { } swordArena)
        {
            var polygons = AdaptPolygons("eureka/excalibur/steel",
                [new(swordArena.Position, 19.5f), new(swords[0].Origin, swords[0].Rotation),
                    new(swords[1].Origin, swords[1].Rotation)],
                () => EurekaSteelForbiddenPolygons(swords[0], swords[1], swordArena.Position, 19.5f));
            for (var i = 0; i < polygons.Length; ++i)
                yield return new($"eureka/excalibur/steel/safe-union/{i}", "Get hit by a blade",
                    polygons[i], swordArena.Position,
                    Display: false, Activation: swords[0].Activation);
        }
        if (encounter.EurekaKaliyaMagnetism() is { } magnetism && encounter.Arena is { } kaliyaArena)
        {
            var drones = encounter.EurekaKaliyaDrones();
            var center = kaliyaArena.Position;
            var polygons = AdaptPolygons("eureka/kaliya/magnetism",
                [new(center, 20), new(magnetism.Origin, magnetism.Pull ? 1 : 0),
                    .. drones.Select(d => new Vector4(d.Position, d.Rotation))],
                () => GeometryGridSampler.Sample(center, 20,
                    (x, z) => EurekaMagnetismDistance(center, magnetism.Origin, magnetism.Pull, drones, x, z)).Select(p => p.WorldPoints));
            for (var i = 0; i < polygons.Length; ++i)
                yield return new($"eureka/kaliya/magnetism/navigation/{i}", "Aetheromagnetism",
                    polygons[i], polygons[i].Resolve(default, 0, null).Position,
                    Display: false, Activation: magnetism.Activation);
        }
        if (encounter.EurekaTurbineNavigation() is { } turbine)
        {
            // With no wind the intersection is exactly the outside of R5.
            // A complement must not be dropped by the finite sampler's fill-ratio guard.
            if (turbine.Winds.Length == 0)
                yield return new("eureka/turbine/navigation", "Turbine",
                    new DonutTemplate(5, 100), turbine.Origin,
                    Display: false, Activation: turbine.Activation);
            else
            {
                var polygons = AdaptPolygons("eureka/turbine",
                    [new(turbine.Origin, 20), .. turbine.Winds.Select(p => new Vector4(p, 0))],
                    () => GeometryGridSampler.Sample(turbine.Origin, 20,
                        (x, z) => EurekaTurbineDistance(turbine.Origin, turbine.Winds, x, z)).Select(p => p.WorldPoints));
                for (var i = 0; i < polygons.Length; ++i)
                    yield return new($"eureka/turbine/navigation/{i}", "Turbine",
                        polygons[i], polygons[i].Resolve(default, 0, null).Position,
                        Display: false, Activation: turbine.Activation);
            }
        }
        foreach (var aoe in encounter.AmbientAoes())
        {
            if (aoe.Shape is not DungeonStackBuddyTemplate buddy)
            {
                yield return aoe;
                continue;
            }
            // An intersection of inverted buddy circles was sampled by the old consumer.
            // Preserve its coverage rejection and contours, independently of the source model.
            var bounds = buddy.Arena.GetBounds();
            var center = buddy.Arena.Position;
            if (encounter.Actors.GetValueOrDefault(encounter.PlayerId) is { } player) center.Y = player.Position.Y;
            var radius = MathF.Max(bounds.Width, bounds.Height) * .5f;
            var polygons = AdaptPolygons(aoe.Id,
                [new(center, radius), new(Vector3.Zero, buddy.Radius),
                    .. buddy.Buddies.Select(p => new Vector4(p, 0))],
                () => GeometryGridSampler.Sample(center, radius, buddy.Distance).Select(p => p.WorldPoints));
            for (var i = 0; i < polygons.Length; ++i)
                yield return aoe with
                {
                    Id = aoe.Id + "/" + i,
                    Shape = polygons[i],
                    Position = polygons[i].Resolve(default, 0, null).Position,
                };
        }
    }

    private WorldPolygonTemplate[] AdaptPolygons(string key, Vector4[] inputs,
        Func<IEnumerable<IReadOnlyList<Vector3>>> build)
    {
        // Inputs contain only geometry: XYZ positions and their radius/rotation.
        // Timing stays on each output so a new hit window still updates its area.
        activeAdaptations.Add(key);
        if (adaptedGeometry.TryGetValue(key, out var cached) && inputs.AsSpan().SequenceEqual(cached.Inputs))
            return cached.Shapes;
        var shapes = build().Select(points => new WorldPolygonTemplate(points)).ToArray();
        adaptedGeometry[key] = (inputs, shapes);
        return shapes;
    }

    internal static float EurekaTurbineDistance(Vector3 origin, Vector3[] winds, float x, float z)
    {
        x -= origin.X;
        z -= origin.Z;
        var length = MathF.Sqrt(x * x + z * z);
        var distance = 5 - length;
        const float halfAngle = 20 * (MathF.PI / 180f);
        foreach (var wind in winds)
        {
            var angle = MathF.Atan2(wind.X - origin.X, wind.Z - origin.Z);
            var (leftSin, leftCos) = Math.SinCos(angle + halfAngle);
            var (rightSin, rightCos) = Math.SinCos(angle - halfAngle);
            var left = x * (float)leftCos - z * (float)leftSin;
            var right = -x * (float)rightCos + z * (float)rightSin;
            distance = MathF.Max(distance, MathF.Max(length - 20, MathF.Max(left, right)));
        }
        return distance;
    }

    internal static float EurekaMagnetismDistance(Vector3 center, Vector3 origin, bool pull,
        (Vector3 Position, float Rotation)[] drones, float x, float z)
    {
        var direction = new Vector2(x - origin.X, z - origin.Z);
        var length = direction.Length();
        direction = length > 0 ? direction * (1f / length) : Vector2.Zero;
        if (pull) direction = -direction;
        var projected = new Vector2(x, z) + 10 * direction;
        var offset = new Vector2(x - center.X, z - center.Z);
        var closestTime = Math.Clamp(-Vector2.Dot(offset, direction), 0, 10);
        var closest = offset + direction * closestTime;
        var relative = projected - new Vector2(center.X, center.Z);
        // The source Distance deliberately differs from Contains (OR versus AND).
        // The bridge consumed Distance<=0, including this zero-valued forbidden region.
        if (relative.LengthSquared() > 19 * 19 || closest.LengthSquared() <= 5.5f * 5.5f)
            return 0;
        var radius = relative.Length();
        var distance = MathF.Max(8 - radius, radius - 50);
        foreach (var drone in drones)
        {
            var local = projected - new Vector2(drone.Position.X, drone.Position.Z);
            var (sin, cos) = Math.SinCos(drone.Rotation);
            var along = local.X * (float)sin + local.Y * (float)cos;
            var side = local.X * (float)cos - local.Y * (float)sin;
            distance = MathF.Min(distance, MathF.Max(MathF.Max(along - 43, -along), MathF.Abs(side) - 2.5f));
        }
        return distance;
    }

    internal static float EurekaThermalDistance(Vector3 origin, float rotation, float x, float z)
    {
        var dx = x - origin.X;
        var dz = z - origin.Z;
        var (leftSin, leftCos) = Math.SinCos(rotation + MathF.PI / 2);
        var (rightSin, rightCos) = Math.SinCos(rotation - MathF.PI / 2);
        var left = dx * (float)leftCos - dz * (float)leftSin;
        var right = -dx * (float)rightCos + dz * (float)rightSin;
        return -MathF.Max(MathF.Sqrt(dx * dx + dz * dz) - 40, MathF.Max(left, right));
    }

    internal static IReadOnlyList<Vector3[]> EurekaSteelForbiddenPolygons(EurekaExcaliburAoe first,
        EurekaExcaliburAoe last, Vector3 center, float arenaRadius)
    {
        // Both full strips are centered on this circular arena and extend 20 in
        // either direction, beyond its R19.5 bounds. Their complement inside the
        // arena is exactly four intersections of two outside half-planes.
        // Sampling that complement would exceed the shared sampler's fill guard.
        var result = new List<Vector3[]>();
        foreach (var firstSide in new[] { -1f, 1f })
            foreach (var lastSide in new[] { -1f, 1f })
            {
                List<Vector2> polygon = [new(-arenaRadius, -arenaRadius), new(arenaRadius, -arenaRadius),
                    new(arenaRadius, arenaRadius), new(-arenaRadius, arenaRadius)];
                foreach (var (sword, side) in new[] { (first, firstSide), (last, lastSide) })
                {
                    if (polygon.Count == 0) break;
                    var (sin, cos) = Math.SinCos(sword.Rotation);
                    var normal = new Vector2((float)cos * side, -(float)sin * side);
                    var relativeOrigin = new Vector2(sword.Origin.X - center.X, sword.Origin.Z - center.Z);
                    var threshold = 2 + Vector2.Dot(relativeOrigin, normal);
                    var clipped = new List<Vector2>();
                    var previous = polygon[^1];
                    var previousDistance = Vector2.Dot(previous, normal) - threshold;
                    foreach (var current in polygon)
                    {
                        var distance = Vector2.Dot(current, normal) - threshold;
                        if ((distance >= 0) != (previousDistance >= 0))
                            clipped.Add(previous + (current - previous) * (previousDistance / (previousDistance - distance)));
                        if (distance >= 0) clipped.Add(current);
                        previous = current;
                        previousDistance = distance;
                    }
                    polygon = clipped;
                }
                // Clipping a line through an AABB corner can emit two distinct
                // local floats that round to the same world point. Normalize
                // after translation, before publishing geometry to either consumer.
                var world = new List<Vector3>();
                const float epsilon = GeometryHelper.DefaultEpsilon;
                foreach (var point in polygon)
                {
                    var translated = new Vector3(center.X + point.X, center.Y, center.Z + point.Y);
                    if (world.Count == 0 || Vector3.DistanceSquared(world[^1], translated) > epsilon * epsilon)
                        world.Add(translated);
                }
                if (world.Count > 1 && Vector3.DistanceSquared(world[0], world[^1]) <= epsilon * epsilon)
                    world.RemoveAt(world.Count - 1);
                if (world.Count >= 3 && MathF.Abs(PolygonGeometryHelpers.SignedArea(
                    world.Select(p => new Vector2(p.X, p.Z)).ToArray())) > epsilon)
                    result.Add(world.ToArray());
            }
        return result;
    }

    public void Clear()
    {
        publishTeleporters?.Invoke([]);
        foreach (var source in _areas.Keys.ToArray()) RemoveArea(source);
        foreach (var id in _previews) draw.Remove(id);
        _previews.Clear();
        foreach (var area in ambientAreas.Values) safeZone.Remove(area.Handle);
        ambientAreas.Clear();
        adaptedGeometry.Clear();
        activeAdaptations.Clear();
        if (_boundary != null && ReferenceEquals(manager.BuildSnapshot(DateTime.UtcNow).ArenaBoundary, _boundary)) manager.ClearArenaBoundary();
        _boundary = null;
    }

    private void RemoveArea(string source)
    {
        if (_areas.Remove(source, out var area)) safeZone.Remove(area.Handle);
    }
}
