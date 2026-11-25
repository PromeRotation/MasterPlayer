using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunEurekaKaliya(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string id = "masterplayer.eureka.80";
        var center = new Vector3(-600, 0, -300);
        object Value(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, flags) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Encounter() => Value(Value(manager.Running[id], "Script"), "_encounter");
        object[] Items(object obj, string name) => ((IEnumerable)Value(obj, name)).Cast<object>().ToArray();
        object[] Group(string prefix) => previews().Where(p => ((string)Value(p, "Id")).Contains(prefix)).ToArray();
        object[] roster = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object[] tether = [new { Id = 38u, TargetId = 100ul }];
        string Effect(double t) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 2ul, ActionId = 31422u, ActionType = 1, MainTargetId = 999ul, GlobalSequence = 7u, Targets = Array.Empty<ulong>() },
        });
        load(id,
        [
            Header(1106),
            Frame(1106, 0, [Actor(1, 0x3D18, center, combat: true, targetId: 101),
                Actor(2, 0x233C, center), Actor(3, 0x3D19, center + Vector3.UnitX * 15, stackedStatus: 3417),
                Actor(100, 0, center - Vector3.UnitX * 6, stackedStatus: 3418),
                Actor(101, 0, center + Vector3.UnitX * 10)], full: true, party: roster),
            Frame(1106, .1, [Actor(3, 0x3D19, center + Vector3.UnitX * 15, stackedStatus: 3417, tethers: tether)], party: roster),
            Frame(1106, .2, [Actor(2, 0x233C, center, cast: Cast(32930, 7.2, center, startedAt: .2))], party: roster),
            Frame(1106, .3, [Actor(1, 0x3D18, center, combat: true, targetId: 101,
                cast: Cast(31427, 3, center, startedAt: .3))], party: roster),
            Frame(1106, .4, [Actor(1, 0x3D18, center, combat: true, targetId: 101, stackedStatus: 3420)], party: roster),
            Frame(1106, .5, [Actor(3, 0x3D19, center + Vector3.UnitX * 15, stackedStatus: 3417, tethers: tether,
                cast: Cast(31432, 4, center + Vector3.UnitX * 15, rotation: .7f, startedAt: .5))], party: roster),
            CancelHoH(.55, 3, 31432),
            Frame(1106, .6, [Actor(2, 0x233C, center)], party: roster),
            Frame(1106, .7, [Actor(2, 0x233C, center, cast: Cast(31422, 5, center, target: 101, startedAt: .7))], party: roster),
            Frame(1106, .8, [Actor(2, 0x233C, center)], removed: [101], party: roster),
            Frame(1106, .89, [Actor(101, 0, center - Vector3.UnitZ * 8)], party: roster),
            Frame(1106, .9, [Actor(2, 0x233C, center, cast: Cast(31422, 5, center, target: 101, startedAt: .9)),
                Actor(101, 0, center - Vector3.UnitZ * 8)], party: roster),
            Frame(1106, 1, [Actor(2, 0x233C, center)], party: roster),
            Effect(1.1), Effect(1.2),
            Frame(1106, 1.3, [Actor(1, 0x3D18, center, combat: true, targetId: 101)], party: roster),
            Frame(1106, 1.4, [Actor(3, 0x3D19, center + Vector3.UnitX * 15)], party: roster),
        ]);
        controller.Seek(.15);
        Check(Items(Encounter(), "EurekaMagnetisms")[0] != null && Group("eureka/kaliya/magnetism").Length == 0,
            "Eureka80 tether failed native model or displayed without source donut.");
        controller.Seek(.25);
        Check(Items(Encounter(), "EurekaKaliyaCombined").Length == 2 && Group("eureka/kaliya/magnetism").Length > 0 && dangers() > 0,
            "Eureka80 ring/cross or shared projection/navigation absent.");
        controller.Seek(.45);
        dynamic arena = Value(Encounter(), "Arena");
        Check((bool)Value(Encounter(), "EurekaBarofieldActive") && Value(Encounter(), "EurekaBarofield") == null
            && !(bool)arena.Contains(center, 0f) && Group("eureka/kaliya/cleave/").Length == 0,
            "Eureka80 Barofield state/arena/latch merged.");
        var runtimeAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var sourceArena = runtimeAssembly.GetType("BossMod.Endwalker.DeepDungeon.EurekaOrthos.DD80ProtoKaliya.DD80ProtoKaliya")!
            .GetField("DonutArena", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
        var wdir = runtimeAssembly.GetType("BossMod.WDir")!;
        var contains = sourceArena.GetType().GetMethod("Contains", flags)!;
        var count = 0;
        var vertexDifferences = 0;
        var vertices = ((IEnumerable)arena.GetContours()).Cast<IEnumerable>().SelectMany(c => c.Cast<Vector3>()).ToArray();
        for (var x = -20.25f; x <= 20.25f; x += .25f)
            for (var z = -20.25f; z <= 20.25f; z += .25f)
            {
                var expected = (bool)contains.Invoke(sourceArena, [Activator.CreateInstance(wdir, [x, z])!])!;
                var point = center + new Vector3(x, 0, z);
                if ((bool)arena.Contains(point, 0f) != expected)
                {
                    // The source indexed Contains drops its isolated top vertex at (0,20).
                    // MP's resolved polygon includes its boundary; reject any interior mismatch.
                    Check(vertices.Contains(point), $"Eureka80 interior arena mismatch at ({x},{z}), expected={expected}.");
                    ++vertexDifferences;
                }
                ++count;
            }
        Console.WriteLine($"PASS native Eureka80 source holed offset 64-edge arena {count} points ({vertexDifferences} exact-vertex boundary-policy differences) Barofield independent latch shared magnetic projection");
        controller.Seek(.57);
        Check(Items(Encounter(), "EurekaKaliyaCombined").Length == 2
            && Value(Items(Encounter(), "EurekaKaliyaCombined").Last(), "Shape").GetType().Name == "DungeonCrossTemplate",
            "Eureka80 queued cancel wrongly consumed cannon before unchanged raw or rect replaced cross.");
        controller.Seek(.65);
        Check(Items(Encounter(), "EurekaKaliyaCombined").Length == 1
            && Group("eureka/kaliya/magnetism").Length == 0
            && Items(Encounter(), "EurekaMagnetisms")[0] != null,
            "Eureka80 donut Finish erased slot model or failed display withdrawal.");
        controller.Seek(.85);
        Check(Items(Encounter(), "EurekaResonances").Length == 1
            && (bool)Value(Value(Items(Encounter(), "EurekaResonances")[0], "Target"), "Removed"),
            "Eureka80 destroyed target erased original delayable bait.");
        controller.Seek(1.05);
        var baits = Items(Encounter(), "EurekaResonances");
        Check(baits.Length == 2 && !ReferenceEquals(Value(baits[0], "Target"), Value(baits[1], "Target")),
            "Eureka80 target reconstruction rebound original or forward source creation order changed.");
        controller.Seek(1.15);
        Check(Items(Encounter(), "EurekaResonances").Length == 1, "Eureka80 source effect did not remove exactly one.");
        controller.Seek(1.25);
        Check(Items(Encounter(), "EurekaResonances").Length == 0, "Eureka80 repeated full same-sequence packet deduplicated.");
        controller.Seek(1.35);
        Check(!(bool)Value(Encounter(), "EurekaBarofieldActive")
            && Group("eureka/kaliya/cleave/").Length != 0, "Eureka80 true status loss failed cleave restoration.");
        controller.Seek(1.45);
        Check(Items(Encounter(), "EurekaMagnetisms")[0] == null, "Eureka80 true untether failed slot cancellation.");
        controller.Seek(.45);
        Check(Items(Encounter(), "EurekaKaliyaCombined").Length == 2
            && Items(Encounter(), "EurekaResonances").Length == 0, "Eureka80 reverse Seek retained later consumption.");
        stop();
        Check(previews().Length == 0 && dangers() == 0, "Eureka80 owner cleanup leaked.");
        Console.WriteLine("PASS native Eureka80 combined source FIFO raw-only cancel original target reconstruction delayed effect same-sequence FIFO true Lose reverse Seek owner clear");

        var output = managerObject.GetType().Assembly.GetType("MasterPlayer.Mechanics.Dungeons.DungeonOutput")!;
        var nativeDistance = output.GetMethod("EurekaMagnetismDistance", BindingFlags.Static | BindingFlags.NonPublic)!;
        object Point(Vector3 p) => Activator.CreateInstance(runtimeAssembly.GetType("BossMod.WPos")!, [p.X, p.Z])!;
        var shapeDistance = runtimeAssembly.GetType("BossMod.ShapeDistance")!;
        var total = 0;
        foreach (var pull in new[] { false, true })
            foreach (var rotation in new[] { 0f, .7f })
            {
                var origin = center + new Vector3(15, 0, 0);
                var zones = Array.CreateInstance(shapeDistance, 2);
                zones.SetValue(Activator.CreateInstance(runtimeAssembly.GetType("BossMod.SDDonut")!, [Point(center), 8f, 50f]), 0);
                var (sin, cos) = Math.SinCos(rotation);
                zones.SetValue(Activator.CreateInstance(runtimeAssembly.GetType("BossMod.SDRect")!,
                    [Point(origin), Activator.CreateInstance(wdir, [(float)sin, (float)cos])!, 43f, 0f, 2.5f]), 1);
                var union = Activator.CreateInstance(runtimeAssembly.GetType("BossMod.SDUnion")!, [zones])!;
                var source = Activator.CreateInstance(runtimeAssembly.GetType("BossMod.SDKnockbackInCircleAwayFromOriginPlusMixedAOEsPlusSingleCircleIntersection")!,
                    [Point(center), Point(origin), 19f, 10f, union, Point(center), 5.5f, pull])!;
                var distance = source.GetType().GetMethod("Distance")!;
                for (var x = -20f; x <= 20; x += .5f)
                    for (var z = -20f; z <= 20; z += .5f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        var expected = (float)distance.Invoke(source, [Point(p)])!;
                        var actual = (float)nativeDistance.Invoke(null, [center, origin, pull,
                            new (Vector3, float)[] { (origin, rotation) }, p.X, p.Z])!;
                        Check(MathF.Abs(expected - actual) < .00002f,
                            $"Eureka80 direct source Distance oracle differs: pull={pull}, rotation={rotation}, x={x}, z={z}, source={expected}, native={actual}.");
                        ++total;
                    }
            }
        Console.WriteLine($"PASS native Eureka80 {total} source Distance oracle pull/away rotated forward drones OR-intersection zero-valued forbidden geometry");
    }
}
