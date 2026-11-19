using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using PromeRotation.LogSystem;

internal static class PilgrimsFollowUpRegression
{
    public static void Run(Assembly assembly, dynamic runtime, string mechanic = "shockwave")
    {
        var dive = mechanic == "dive";
        var water = mechanic == "water";
        var (fixtureName, territory, countExpected) = mechanic switch
        {
            "dive" => ("dive-live61.json", 1287u, 3),
            "water" => ("water-live71.json", 1288u, 13),
            _ => ("shockwave-183552.json", 1287u, 4),
        };
        var type = assembly.GetType("MasterPlayer.Dodge.DodgeController")!;
        var controller = RuntimeHelpers.GetUninitializedObject(type);
        foreach (var name in new[] { "_registeredHandles", "_pilgrimsFollowUps" })
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) field.SetValue(controller, Activator.CreateInstance(field.FieldType));
        }
        var track = type.GetMethod("TrackPilgrimsFollowUpCast", BindingFlags.Instance | BindingFlags.NonPublic);
        var effect = type.GetMethod("HandlePilgrimsFollowUpEvent", BindingFlags.Instance | BindingFlags.NonPublic);
        var clear = type.GetMethod("ClearPilgrimsFollowUps", BindingFlags.Instance | BindingFlags.NonPublic);
        var update = type.GetMethod("UpdatePilgrimsFollowUps", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var database = assembly.GetType("MasterPlayer.Dodge.AoeShapeDatabase")!;
        var descriptorType = assembly.GetType("MasterPlayer.Dodge.AoeShapeDescriptor")!;
        var lookup = database.GetMethod("TryGetShape", [typeof(uint), typeof(float), descriptorType.MakeByRefType()])!;
        var bind = database.GetMethod("TryCreateBinding", BindingFlags.Static | BindingFlags.NonPublic)!;
        var replayCastType = assembly.GetType("MasterPlayer.Replay.ReplayCastData")!;
        var handleType = assembly.GetType("MasterPlayer.Spatial.SafeZone.SafeZoneAreaHandle")!;
        var emptyHandle = Activator.CreateInstance(handleType);
        dynamic settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var previousMargin = (float)settings.DodgeDangerMargin;
        var clientProperty = typeof(Svc).GetProperty(nameof(Svc.ClientState))!;
        var previousClient = Svc.ClientState;
        var activeTerritory = territory;
        clientProperty.SetValue(null, CatalogServiceProxy.Create<IClientState>((name, _) => name == "get_TerritoryType"
            ? activeTerritory : throw new NotSupportedException(name)));
        settings.DodgeDangerMargin = 0f;
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", fixtureName)));
        dynamic[] Dangers() => ((IEnumerable)runtime.Manager.BuildSnapshot(DateTime.UtcNow).DangerAreas).Cast<dynamic>().ToArray();
        var count = 0;
        try
        {
            foreach (var cycle in fixture.RootElement.GetProperty("cycles").EnumerateArray())
            {
                var p = cycle.GetProperty("position").EnumerateArray().Select(v => v.GetSingle()).ToArray();
                var origin = new Vector3(p[0], p[1], p[2]);
                var rotation = cycle.GetProperty("castRotation").GetSingle();
                var oldPosition = Vector3.Zero;
                if (dive)
                {
                    var old = cycle.GetProperty("firstSourcePosition").EnumerateArray().Select(v => v.GetSingle()).ToArray();
                    oldPosition = new Vector3(old[0], old[1], old[2]);
                }
                var cast = new LogSystemCastStartEvent { SourceId = (ulong)++count,
                    ActionId = cycle.GetProperty("firstAction").GetUInt16(), CastTime = cycle.GetProperty("castSeconds").GetSingle(),
                    EffectPosition = origin, SourcePosition = oldPosition, SourceRotation = 2f,
                    Raw = Activator.CreateInstance(replayCastType, [rotation]) };
                if (water)
                {
                    var lockedPose = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZone.StaticTransform")!, [origin, rotation]);
                    track!.Invoke(controller, [cast, emptyHandle, lockedPose, territory]);
                    effect!.Invoke(controller, [new LogSystemActionEffectEvent { SourceId = cast.SourceId,
                        ActionId = cast.ActionId, GlobalSequence = 1 }]);
                    Check(Dangers().Length == 1, "Twin water's first effect must create its missing untelegraphed second hit.");
                    clear!.Invoke(controller, null);
                }
                object?[] query = [(uint)cast.ActionId, 20f, null];
                Check((bool)lookup.Invoke(null, query)!, "Recorded first hits require exact entries independent of generic inference.");
                dynamic descriptor = query[2]!;
                var firstRadius = mechanic switch { "water" => 10f, "dive" => 6f, _ => 30f };
                Check((float)descriptor.Param1 == firstRadius && (float)descriptor.ExtraDelaySeconds == .3f,
                    "The first hit must preserve its raw radius and recorded cast-to-effect offset, without adding a hitbox.");
                if (water)
                {
                    dynamic firstTemplate = database.GetMethod("CreateTemplate", BindingFlags.Static | BindingFlags.NonPublic,
                        [descriptorType])!.Invoke(null, [query[2]])!;
                    dynamic firstShape = firstTemplate.Resolve(origin, 0f, null);
                    var growing = cast.ActionId == 42578;
                    Check((bool)firstShape.Contains(origin + new Vector3(0, 0, 9.9f), 0f) == growing
                        && (bool)firstShape.Contains(origin + new Vector3(0, 0, 10.1f), 0f) == !growing
                        && !(bool)firstShape.Contains(origin + new Vector3(0, 0, 40.1f), 0f),
                        "Twin water must begin with the right R10 circle or R10-40 donut.");
                }
                object?[] bindingArgs = [cast, query[2], null, territory];
                Check((bool)bind.Invoke(null, bindingArgs)!, "The recorded first hit must bind at its locked landing point.");
                var pose = bindingArgs[2];
                track?.Invoke(controller, [cast, emptyHandle, pose, territory]);
                var first = new LogSystemActionEffectEvent { SourceId = cast.SourceId, ActionId = cast.ActionId,
                    GlobalSequence = 1, SourceRotation = 2f, SourcePosition = oldPosition, EffectPosition = Vector3.Zero };
                var before = DateTime.UtcNow;
                effect?.Invoke(controller, [first]);
                update.Invoke(controller, null);
                var areas = Dangers();
                Check(areas.Length == 1, "The first effect must register its untelegraphed follow-up.");
                dynamic area = areas.Single();
                dynamic shape = area.Geometry;
                Vector3 Point(float angle, float radius) => origin + new Vector3(MathF.Sin(angle) * radius, 0, MathF.Cos(angle) * radius);
                if (water)
                {
                    var growing = cast.ActionId == 42578;
                    Check((bool)shape.Contains(Point(0f, 9.9f), 0f) == !growing
                        && (bool)shape.Contains(Point(0f, 10.1f), 0f) == growing
                        && (bool)shape.Contains(Point(0f, 39.9f), 0f) == growing
                        && !(bool)shape.Contains(Point(0f, 40.1f), 0f),
                        "Water must alternate the locked R10 circle and R10-40 donut in the correct order.");
                }
                else if (dive)
                {
                    Check(!(bool)shape.Contains(Point(0f, 4.9f), 0f) && (bool)shape.Contains(Point(0f, 5.1f), 0f)
                        && (bool)shape.Contains(Point(0f, 9.9f), 0f) && !(bool)shape.Contains(Point(0f, 10.1f), 0f),
                        "Subduction must preserve the locked landing point and actual R5-10 donut, including its safe inner hole.");
                }
                else
                    Check((bool)shape.Contains(Point(rotation + MathF.PI, 29.9f), 0f)
                        && !(bool)shape.Contains(Point(rotation, 10f), 0f)
                        && !(bool)shape.Contains(Point(rotation + MathF.PI, 30.1f), 0f),
                        "The follow-up must reverse the locked cast side at the original position; zero effect locations and ordinary facing cannot replace it.");
                var windows = ((IEnumerable)area.DangerWindows).Cast<dynamic>().ToArray();
                var delay = dive || water ? 3.1 : 2.6;
                Check(windows.Length == 1 && Math.Abs(((DateTime)windows[0].DangerFrom - before).TotalSeconds - delay) < .1,
                    "The follow-up must activate at the calibrated delay after the actual first effect.");
                foreach (var latency in new[] { .08, .3 })
                    CheckEscape(assembly, runtime, origin, Point(dive || water ? 0f : rotation + MathF.PI,
                        water && cast.ActionId == 42578 ? 12f : 8f), shape,
                        (DateTime)windows[0].DangerFrom, before.AddSeconds(latency + .08),
                        cycle.GetProperty("interval").GetDouble(), delay);
                runtime.Manager.ClearArenaBoundary();
                effect!.Invoke(controller, [first with { TargetIndex = 1 }]);
                Check(Dangers().Length == 1, "Multi-target first effects must not duplicate the follow-up.");
                var follow = first with { ActionId = cycle.GetProperty("secondAction").GetUInt32(), GlobalSequence = 2 };
                effect.Invoke(controller, [follow with { SourceId = 99 }]);
                Check(Dangers().Length == 1, "Another caster cannot consume this sequence.");
                effect.Invoke(controller, [follow]);
                effect.Invoke(controller, [first]);
                Check(Dangers().Length == 0, "The second effect must remove its own area; late duplicate first effects cannot recreate it.");
                track!.Invoke(controller, [cast, emptyHandle, pose, territory]);
                effect.Invoke(controller, [new LogSystemCancelCastEvent { SourceId = cast.SourceId, ActionId = cast.ActionId }]);
                effect.Invoke(controller, [first]);
                Check(Dangers().Length == 0, "Cancelled first casts must not produce a follow-up.");
                track.Invoke(controller, [cast, emptyHandle, pose, 1286u]);
                effect.Invoke(controller, [first]);
                Check(Dangers().Length == 0, "The recording-backed fix must be isolated to its floor range.");
                track.Invoke(controller, [cast, emptyHandle, pose, territory]);
                effect.Invoke(controller, [first]);
                clear!.Invoke(controller, null);
                Check(Dangers().Length == 0, "Territory changes and disposal must clear pending follow-ups.");
                track.Invoke(controller, [cast, emptyHandle, pose, territory]);
                effect.Invoke(controller, [new LogSystemInterruptCastEvent { SourceId = cast.SourceId, ActionId = cast.ActionId }]);
                effect.Invoke(controller, [first]);
                Check(Dangers().Length == 0, "Interrupted casts must not produce a follow-up.");
                track.Invoke(controller, [cast, emptyHandle, pose, territory]);
                effect.Invoke(controller, [first]);
                activeTerritory = 1200;
                update.Invoke(controller, null);
                Check(Dangers().Length == 0, "The frame update must discard sequences belonging to a previous territory.");
                activeTerritory = territory;
            }
            Check(count == countExpected, "Exercise every recorded sequence.");
        }
        finally
        {
            settings.DodgeDangerMargin = previousMargin;
            clear?.Invoke(controller, null);
            clientProperty.SetValue(null, previousClient);
            runtime.Manager.ClearAllAreas();
        }
        Console.WriteLine(mechanic switch
        {
            "water" => "PASS Pilgrims twin water: thirteen live ARR sequences, locked R10 circle / R10-40 donut in both orders, 3.1s timing, 80/300ms solver escape and lifecycle cleanup",
            "dive" => "PASS Pilgrims Execute/Subduction: three live ARR sequences, locked landing R5-10 donut, 3.1s timing, 80/300ms solver escape, effect deduplication, source isolation, cancellation, interruption and cleanup",
            _ => "PASS Pilgrims shockwaves: four ARR sequences, opposite locked R30 half-plane, 2.6s timing, 80/300ms solver escape, effect deduplication, source isolation, cancellation, interruption and cleanup",
        });
    }

    private static void CheckEscape(Assembly assembly, dynamic runtime, Vector3 origin, Vector3 start,
        dynamic actualShape, DateTime predictedHit, DateTime ready, double actualInterval, double delay)
    {
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        dynamic circle = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZone.CircleTemplate")!, [15f])!;
        runtime.Manager.SetArenaBoundary(circle.Resolve(origin, 0f, null));
        dynamic snapshot = runtime.Manager.BuildSnapshot(ready);
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [start, 0f, 6f, false, false, 0f, true, (int)snapshot.Version])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        options.GetType().GetProperty("HardSafetyMargin")!.SetValue((object)options, .5f);
        options.GetType().GetProperty("ArenaSafetyMargin")!.SetValue((object)options, 0f);
        dynamic solution = solver.Solve(snapshot, player, capability, options, ready, null);
        var fallback = (bool)solution.GetType().GetProperty("IsLeastDangerFallback", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue((object)solution)!;
        Check((bool)solution.IsSuccess && !fallback, "The real solver must find an opposite-side escape within the recorded interval.");
        dynamic[] path = ((IEnumerable)solution.ExecutablePath).Cast<dynamic>().ToArray();
        Check(path.Length > 0, "A player eight meters inside the follow-up half-plane must receive a route.");
        var hitAt = predictedHit.AddSeconds(actualInterval - delay);
        var position = start;
        for (var i = 0; i < path.Length; ++i)
        {
            position = (Vector3)path[i].Position;
            Check((bool)snapshot.ArenaBoundary.Contains(position, 0f), "The escape must remain inside the test room.");
            if (hitAt <= (DateTime)path[i].PlannedDepartureTime || i == path.Length - 1) break;
            if (hitAt < (DateTime)path[i + 1].PlannedArrivalTime)
            {
                position = Vector3.Lerp(position, (Vector3)path[i + 1].Position,
                    (float)((hitAt - (DateTime)path[i].PlannedDepartureTime) /
                        ((DateTime)path[i + 1].PlannedArrivalTime - (DateTime)path[i].PlannedDepartureTime)));
                break;
            }
        }
        Check(!(bool)actualShape.Contains(position, 0f), "The planned position at the actual recorded follow-up hit must be outside R30/180 degrees.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
