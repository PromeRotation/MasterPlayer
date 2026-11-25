using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunEurekaFinalBosses(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Value(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, flags) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Encounter(string id) => Value(Value(manager.Running[id], "Script"), "_encounter");
        object[] Items(object obj, string name) => ((IEnumerable)Value(obj, name)).Cast<object>().ToArray();
        object[] Group(string prefix) => previews().Where(p => ((string)Value(p, "Id")).Contains(prefix)).ToArray();
        object[] roster = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        string Effect(double t, uint action) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 2ul, ActionId = action, ActionType = 1, MainTargetId = 100ul,
                GlobalSequence = 7u, Targets = Array.Empty<ulong>() },
        });
        string Icon(double t, ulong source, uint icon) => JsonSerializer.Serialize(new
        {
            t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, TargetId = 100ul, MarkerId = icon, Timestamp = Start.AddSeconds(t) },
        });
        object WithStatuses(object actor, params object[] statuses)
        {
            var node = JsonSerializer.SerializeToNode(actor)!;
            node["Statuses"] = JsonSerializer.SerializeToNode(statuses);
            return node;
        }
        var center = new Vector3(-300, 0, -300);
        {
            const string id = "masterplayer.eureka.90";
            load(id,
            [
                Header(1107),
                Frame(1107, 0, [Actor(1, 0x3D23, center, combat: true), Actor(2, 0x233C, center),
                    Actor(3, 0x3D25, center + Vector3.UnitX * 10), Actor(4, 0x3D26, center - Vector3.UnitZ * 10),
                    Actor(5, 0x3D24, center - Vector3.UnitX * 10),
                    Actor(100, 0, center + Vector3.UnitZ * 8), Actor(101, 0, center - Vector3.UnitZ * 8)], full: true, party: roster),
                Icon(.1, 3, 390), Icon(.1, 4, 391),
                Frame(1107, .2, [Actor(2, 0x233C, center, cast: Cast(31452, 5, center, startedAt: .2)),
                    Actor(5, 0x3D24, center - Vector3.UnitX * 10, cast: Cast(31452, 5, center - Vector3.UnitX * 10, startedAt: .2))], party: roster),
                CancelHoH(.25, 2, 31452),
                Frame(1107, .3, [Actor(2, 0x233C, center)], party: roster),
                Frame(1107, .4, [Actor(1, 0x3D23, center, combat: true, cast: Cast(31448, 5, center, startedAt: .4))], party: roster),
                Frame(1107, .5, [Actor(1, 0x3D23, center, combat: true)], party: roster),
                Frame(1107, .6, [Actor(2, 0x233C, center, cast: Cast(31451, 5, center, startedAt: .6))], party: roster),
                Frame(1107, .7, [Actor(2, 0x233C, center)], party: roster),
                Frame(1107, .8, [Actor(1, 0x3D23, center, combat: true, cast: Cast(31456, 4, center, startedAt: .8))], party: roster),
                Frame(1107, .9, [Actor(1, 0x3D23, center, combat: true)], party: roster),
            ]);
            controller.Seek(.27);
            Check(Items(Encounter(id), "EurekaAdministratorCombo").Length == 2
                && Items(Encounter(id), "EurekaAdministratorLines").Length == 2
                && Group("eureka/administrator/").Length == 2 && dangers() > 0,
                "Administrator icon identity, hidden lines, navigation or queued Cancel.");
            controller.Seek(.35);
            Check(Items(Encounter(id), "EurekaAdministratorCombo").Length == 1
                && Items(Encounter(id), "EurekaAdministratorLines").Length == 1, "Administrator Finish consumption.");
            controller.Seek(.55);
            Check(Items(Encounter(id), "EurekaAdministratorCombo").Length == 1
                && Value(Items(Encounter(id), "EurekaAdministratorCombo")[0], "Shape").GetType().Name == "DungeonCrossTemplate",
                "Administrator wrong-source Finish must pop old icon ahead of boss cross.");
            controller.Seek(.75);
            Check(Items(Encounter(id), "EurekaAdministratorCombo").Length == 0
                && Group("eureka/administrator/").Length == 1 && dangers() > 0, "Administrator fallback line lost.");
            controller.Seek(.85);
            var predictions = Items(Encounter(id), "PredictedDamage");
            Check(predictions.Length == 2 && predictions.All(p => (string)Value(p, "DamageType") == "Raidwide"),
                "Administrator Raidwide mask/clock.");
            controller.Seek(.95);
            Check(Items(Encounter(id), "PredictedDamage").Length == 0, "Administrator Raidwide Finish.");
            controller.Seek(.27);
            Check(Items(Encounter(id), "EurekaAdministratorCombo").Length == 2, "Administrator reverse Seek.");
            stop();
            Check(previews().Length == 0 && dangers() == 0, "Administrator owner cleanup.");
            Console.WriteLine("PASS native Eureka90 source icons independent hidden line FIFO raw-only Cancel global Finish sequence raidwide reverse Seek dual-view data navigation owner clear");
        }
        center = new(-600, 0, -300);
        {
            const string id = "masterplayer.eureka.99";
            load(id,
            [
                Header(1108),
                Frame(1108, 0, [Actor(1, 0x3CFC, center, combat: true), Actor(2, 0x233C, center),
                    Actor(100, 0, center + Vector3.UnitX * 8), Actor(101, 0, center - Vector3.UnitX * 8)], full: true, party: roster),
                Frame(1108, .1, [Actor(1, 0x3CFC, center, combat: true, cast: Cast(31333, 5, center, startedAt: .1))], party: roster),
                Frame(1108, .2, [Actor(1, 0x3CFC, center, combat: true)], party: roster),
                Frame(1108, .3, [Actor(2, 0x233C, center, cast: Cast(31353, 5, center, startedAt: .3))], party: roster),
                Frame(1108, .4, [Actor(2, 0x233C, center)], party: roster),
                Frame(1108, .5, [Actor(1, 0x3CFC, center, combat: true, cast: Cast(31334, 2, center, startedAt: .5))], party: roster),
                Frame(1108, .6, [Actor(1, 0x3CFC, center, combat: true)], party: roster),
                Frame(1108, .7, [Actor(100, 0, center + Vector3.UnitX * 8, stackedStatus: 3410)], party: roster),
                Frame(1108, .8, [Actor(2, 0x233C, center, cast: Cast(31350, 5, center, startedAt: .8))], party: roster),
                Frame(1108, .9, [Actor(2, 0x233C, center)], party: roster),
                Effect(1, 31351),
                Frame(1108, 1.1, [Actor(2, 0x233C, center, cast: Cast(31346, 5, center, startedAt: 1.1))], party: roster),
                Frame(1108, 1.2, [Actor(2, 0x233C, center)], party: roster),
                Effect(1.3, 31348), Effect(1.4, 31348),
            ]);
            controller.Seek(.15);
            var blades = Items(Encounter(id), "EurekaSteelstrikes");
            Check(blades.Length == 10 && blades.All(a => (DateTime)Value(a, "Activation") == default),
                "Excalibur initial sword geometry/clock.");
            controller.Seek(.35);
            Check(Items(Encounter(id), "EurekaSteelstrikes").Length == 20
                && Group("eureka/excalibur/").Length == 20, "Excalibur full delayed strips.");
            controller.Seek(.65);
            Check(Items(Encounter(id), "EurekaSteelstrikes").Length == 0
                && Items(Encounter(id), "EurekaSteelAngles").Length == 10, "Excalibur first Finish scope.");
            controller.Seek(.85);
            var thermal = Value(Encounter(id), "EurekaThermalPattern");
            var previewsNow = Group("eureka/excalibur/thermal/");
            Check(thermal != null && previewsNow.Length == 2 && dangers() >= 2
                && (bool)Value(previewsNow.Single(a => ((string)Value(a, "Id")).EndsWith("/safe")), "Safe"),
                "Excalibur safe-side display/inverse hard navigation.");
            controller.Seek(.95);
            Check(Value(Encounter(id), "EurekaThermalPattern") != null, "Thermal true Finish incorrectly cleared.");
            controller.Seek(1.05);
            Check(Value(Encounter(id), "EurekaThermalPattern") == null
                && Group("eureka/excalibur/thermal/").Length == 0, "Thermal full effect did not clear.");
            controller.Seek(1.25);
            Check(Items(Encounter(id), "EurekaIceCrosses").Length == 2
                && Group("eureka/excalibur/").Length == 2, "Cross real Finish removed predictions.");
            controller.Seek(1.35);
            Check(Items(Encounter(id), "EurekaIceCrosses").Length == 1, "Cross first full effect.");
            controller.Seek(1.45);
            Check(Items(Encounter(id), "EurekaIceCrosses").Length == 0, "Cross duplicate full sequence was deduplicated.");
            controller.Seek(.85);
            Check(Value(Encounter(id), "EurekaThermalPattern") != null
                && Items(Encounter(id), "EurekaIceCrosses").Length == 0, "Excalibur reverse Seek.");
            stop();
            Check(previews().Length == 0 && dangers() == 0, "Excalibur owner cleanup.");
            Console.WriteLine("PASS native Excalibur original blades full delayed strips partial Finish ice/fire source-slot safe-side inverse hard navigation effect-only cross FIFO same-sequence Seek owner clear");
        }
        {
            const string id = "masterplayer.eureka.99";
            var bladeActors = Enumerable.Range(0, 10).Select(i => Actor((ulong)(10 + i), 0x3D01, center,
                rotation: -45f * (MathF.PI / 180f) + i * (10f * (MathF.PI / 180f)),
                stackedStatus: 2552, statusParam: (ushort)(i < 5 ? 0x219 : 0x21A))).ToArray();
            load(id,
            [
                Header(1108),
                Frame(1108, 0, [Actor(1, 0x3CFC, center, combat: true), Actor(2, 0x233C, center),
                    Actor(100, 0, center + Vector3.UnitX * 12), Actor(101, 0, center - Vector3.UnitX * 8)], full: true, party: roster),
                Frame(1108, .1, [Actor(1, 0x3CFC, center, combat: true,
                    cast: Cast(31333, 5, center, startedAt: .1))], party: roster),
                Frame(1108, .2, bladeActors, party: roster),
                Frame(1108, .3, [Actor(100, 0, center + Vector3.UnitX * 12, stackedStatus: 3411),
                    Actor(2, 0x233C, center, cast: Cast(31330, 5, center, startedAt: .3))], party: roster),
                Frame(1108, .4, [Actor(2, 0x233C, center)], party: roster),
                Frame(1108, .5, [Actor(2, 0x233C, center,
                    cast: Cast(31353, 5, center, startedAt: .5))], party: roster),
            ]);
            controller.Seek(.35);
            var encounter = Encounter(id);
            var fire = Items(encounter, "EurekaFireSwords");
            var ice = Items(encounter, "EurekaIceSwords");
            Check(fire.Length == 5 && ice.Length == 5, "Excalibur native sword status capture.");
            var safe = ((IEnumerable)encounter.GetType().GetMethod("EurekaSteelSafeSwords", flags)!
                .Invoke(encounter, null)!).Cast<object>().ToArray();
            Check(safe.Length == 2 && ReferenceEquals(safe[0], fire[0]) && ReferenceEquals(safe[1], fire[^1])
                && safe.All(a => (DateTime)Value(a, "Activation") == default
                    && Value(a, "Shape").GetType().Name == "RayRectangleTemplate"),
                "Excalibur safe blades must retain original short/default snapshots.");
            Check(Group("eureka/excalibur/").Count(a => (bool)Value(a, "Safe")) == 2,
                "Excalibur original safe blades did not reach both view consumers.");
            controller.Seek(.55);
            encounter = Encounter(id);
            Check(Items(encounter, "EurekaSteelstrikes").Length == 20
                && Items(encounter, "EurekaFireSwords").All(a => (DateTime)Value(a, "Activation") == default),
                "Excalibur delayed strips replaced captured original swords.");
            var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            Check(Items(snapshot, "DangerAreas").Length >= 10 && Items(snapshot, "GoalAreas").Length == 0,
                "Excalibur safe union complement was dropped or became a soft goal.");
            var assembly = runtimeObject.GetType().Assembly;
            var sourceAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
            var sourcePoint = sourceAssembly.GetType("BossMod.WPos")!;
            var sourceAngle = sourceAssembly.GetType("BossMod.Angle")!;
            var zones = Array.CreateInstance(sourceAssembly.GetType("BossMod.ShapeDistance")!, 2);
            for (var i = 0; i < safe.Length; ++i)
                zones.SetValue(Activator.CreateInstance(sourceAssembly.GetType("BossMod.SDInvertedRect")!,
                    [Activator.CreateInstance(sourcePoint, [center.X, center.Z])!,
                        Activator.CreateInstance(sourceAngle, [Value(safe[i], "Rotation")])!, 20f, 20f, 2f]), i);
            var intersection = Activator.CreateInstance(sourceAssembly.GetType("BossMod.SDIntersection")!, [zones])!;
            var sourceDistance = intersection.GetType().GetMethod("Distance")!;
            var outputType = assembly.GetType("MasterPlayer.Mechanics.Dungeons.DungeonOutput")!;
            var polygons = ((IEnumerable)outputType.GetMethod("EurekaSteelForbiddenPolygons",
                    BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [safe[0], safe[1], center, 19.5f])!)
                .Cast<Vector3[]>().Select(p => Activator.CreateInstance(
                    assembly.GetType("MasterPlayer.Spatial.Geometry.WorldPolygonGeometry")!, [p])!).ToArray();
            var compared = 0;
            var boundary = 0;
            for (var x = -19.5f; x <= 19.5f; x += .25f)
                for (var z = -19.5f; z <= 19.5f; z += .25f)
                {
                    if (x * x + z * z > 19.5f * 19.5f) continue;
                    var p = center + new Vector3(x, 0, z);
                    var expected = (float)sourceDistance.Invoke(intersection,
                        [Activator.CreateInstance(sourcePoint, [p.X, p.Z])!])!;
                    var actual = polygons.Any(polygon => (bool)polygon.GetType().GetMethod("Contains")!
                        .Invoke(polygon, [p, 0f])!);
                    if (MathF.Abs(expected) <= .0001f) ++boundary;
                    else Check(actual == (expected <= 0), $"Excalibur source safe-union interior differs at {x},{z}.");
                    ++compared;
                }
            Console.WriteLine($"PASS native Excalibur original source SDIntersection versus hard polygons {compared} points ({boundary} boundary points checked separately by model)");
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [center + Vector3.UnitX * 12, 0f, 6f, false, false, 0f, true, (int)Value(snapshot, "Version")])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            object result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)Value(result, "IsSuccess") && !(bool)Value(result, "IsLeastDangerFallback")
                && Value(result, "TargetPosition") is Vector3,
                "Excalibur production solver did not find a real safe blade.");
            var target = (Vector3)Value(result, "TargetPosition");
            bool InsideStrip(object sword)
            {
                var delta = target - (Vector3)Value(sword, "Origin");
                var (sin, cos) = Math.SinCos((float)Value(sword, "Rotation"));
                return MathF.Abs(delta.X * (float)cos - delta.Z * (float)sin) <= 2
                    && MathF.Abs(delta.X * (float)sin + delta.Z * (float)cos) <= 20;
            }
            Check(safe.Any(InsideStrip), "Excalibur solver target missed both complete safe strips.");
            controller.Seek(.35);
            Check(Items(Encounter(id), "EurekaFireSwords").Length == 5, "Excalibur status reverse Seek.");
            stop();
            Check(previews().Length == 0 && dangers() == 0, "Excalibur safe union owner cleanup.");
            Console.WriteLine("PASS native Excalibur ten sword status original short snapshots complete safe union hard polygons production solver no fallback reverse Seek owner clear");
        }
        {
            const string id = "masterplayer.eureka.80";
            object Status(uint status, int slot) => new { Id = status, Param = 0, SourceId = 0ul,
                SlotIndex = slot, ExpiresAt = Start.AddSeconds(30) };
            load(id,
            [
                Header(1106),
                Frame(1106, 0, [Actor(1, 0x3D18, center, combat: true), Actor(2, 0x233C, center),
                    Actor(100, 0, center + Vector3.UnitX * 8)], full: true, party: roster),
                Frame(1106, .1, [Actor(1, 0x3D18, center, combat: true,
                    cast: Cast(31427, 5, center, startedAt: .1))], party: roster),
                Frame(1106, .2, [WithStatuses(Actor(2, 0x233C, center), Status(160, 2), Status(3420, 4))], party: roster),
                Frame(1106, .3, [WithStatuses(Actor(2, 0x233C, center), Status(3420, 4))], party: roster),
                Frame(1106, .4, [Actor(2, 0x233C, center)], party: roster),
            ]);
            controller.Seek(.35);
            dynamic arena = Value(Encounter(id), "Arena");
            Check((bool)Value(Encounter(id), "EurekaBarofieldActive") && !(bool)arena.Contains(center, 0f),
                "Native StatusSlots compressed out the true Barofield slot.");
            controller.Seek(.45);
            Check(!(bool)Value(Encounter(id), "EurekaBarofieldActive"), "Native true status loss failed.");
            stop();
            Console.WriteLine("PASS native Eureka80 full status slots unrelated earlier slot removal preserves field and true Lose clears latch");
        }

        var runtimeAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var wdir = runtimeAssembly.GetType("BossMod.WDir")!;
        var wpos = runtimeAssembly.GetType("BossMod.WPos")!;
        object Point(Vector3 p) => Activator.CreateInstance(wpos, [p.X, p.Z])!;
        object Angle(float rotation) => Activator.CreateInstance(runtimeAssembly.GetType("BossMod.Angle")!, [rotation])!;
        var output = managerObject.GetType().Assembly.GetType("MasterPlayer.Mechanics.Dungeons.DungeonOutput")!;
        var inverse = output.GetMethod("EurekaThermalDistance", BindingFlags.Static | BindingFlags.NonPublic)!;
        var comparisons = 0;
        foreach (var rotation in new[] { 0f, .7f, 1.4f })
        {
            var origin = center + new Vector3(-4, 0, 0);
            var source = Activator.CreateInstance(runtimeAssembly.GetType("BossMod.SDInvertedCone")!,
                [Point(origin), 40f, Angle(rotation), Angle(MathF.PI / 2)])!;
            var distance = source.GetType().GetMethod("Distance")!;
            for (var x = -20f; x <= 20; x += .5f)
                for (var z = -20f; z <= 20; z += .5f)
                {
                    var p = center + new Vector3(x, 0, z);
                    var expected = (float)distance.Invoke(source, [Point(p)])!;
                    var actual = (float)inverse.Invoke(null, [origin, rotation, p.X, p.Z])!;
                    Check(MathF.Abs(expected - actual) < .00001f, "Excalibur inverse cone source Distance differs.");
                    ++comparisons;
                }
        }
        Console.WriteLine($"PASS native Excalibur inverse cone source Distance oracle {comparisons} points three directions");
    }
}
