using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleClaws(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Values(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: party);
        string Cancel(double t, ulong source, uint action)
            => JsonSerializer.Serialize(new { t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(uint oid = 0x2721) =>
        [
            Actor(1, oid, center, combat: true, objectIndex: 2, isEnemy: true),
            Actor(2, 0x233C, center), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 3),
        ];
        foreach (var (oid, add, name) in new[]
        {
            (0x2720u, 0x271Fu, "刚柔铁巨人——克利俄斯改·第一幕"),
            (0x2721u, 0x2722u, "刚柔铁巨人——克利俄斯改·第二幕"),
        })
        {
            load(guid, [Header(796), At(0, [Actor(100, 0, center)], true),
                At(.1, [Actor(1, oid, center), Actor(4, add, center + Vector3.UnitX * 5, combat: true)]),
                At(1, [Actor(1, oid, center, combat: true)]),
                At(2, [Actor(1, oid, center, combat: true, dead: true)]),
                At(3, [], removed: [4]), At(4, [])]);
            controller.Seek(.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x2720), "Claw group versus primary pull.");
            controller.Seek(1.5);
            Check(Shown("carnivale/enemy/").Length == 2 && Value(Encounter(), "CarnivalePrecombatHint") == null
                && ((Array)Value(Encounter(), "ForbiddenTargets")).Length == 0, "Claw counter scope/markers.");
            Check((Vector4)Value(Shown("carnivale/enemy/4").Single(), "Color") ==
                (oid == 0x2721 ? new Vector4(1, 128f / 255, 0, 1) : new Vector4(1, 0, 0, 1)), "Claw colours.");
            var assembly = Assembly.Load("MasterPlayer.MechanicRuntime");
            var bounds = assembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!
                .GetField("CircleSmall", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
            var direction = assembly.GetType("BossMod.WDir")!;
            dynamic arena = Value(Encounter(), "Arena");
            var count = 0;
            for (var x = 82.13f; x < 118; x += .47f)
                for (var z = 82.17f; z < 118; z += .53f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(direction, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Claw source arena.");
                    ++count;
                }
            controller.Seek(2.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x2720), "Claw completion group.");
            controller.Seek(3.5);
            Check(Value(Encounter(), "Active") == null && previews().Length == 0, "Claw completion cleanup.");
            controller.Seek(1.5);
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(1.6);
            Check(previews().Length == 0 && dangers() == 0, "Claw disabled owner.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage17 {oid:X} source arena oracle {count} points group primary lifecycle enemy colour counter hint no automatic restriction reverse Seek owner");
        }

        foreach (var (oid, action, expectedShape) in new[]
        {
            (0x2720u, 14759u, "RayRectangleTemplate"), (0x2720u, 14760u, "FanTemplate"),
            (0x2721u, 14759u, "RayRectangleTemplate"), (0x2721u, 14760u, "FanTemplate"),
            (0x2721u, 15047u, "RayRectangleTemplate"),
        })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, .7f))]),
                EffectEvent(7, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(.5);
            var cast = Values("Casts").Single();
            var template = Value(Value(Value(cast, "Value"), "Rule"), "Shape");
            Check(template.GetType().Name == expectedShape && dangers() == 1 && Shown("cast/").Length == 1,
                "Claw ordinary shape dual output.");
            Check(action == 14760 ? Math.Abs((float)Value(template, "Radius") - 8) < .0001f
                : (float)Value(template, "Length") == (action == 15047 ? 77.5f : 6)
                    && (float)Value(template, "MaxLocalX") == 2, "Claw source dimensions.");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action }),
                "Claw ordinary takeover.");
            controller.Seek(8.5);
            Check(dangers() == 1, "Claw effect/deadline lifetime.");
            controller.Seek(9.5);
            Check(dangers() == 0 && Shown("cast/").Length == 0, "Claw Finish.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage17 all five ordinary component instances exact cone rectangle dimensions native dual output observed lifetime reverse Seek");

        var position = center + Vector3.UnitX * 3;
        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15048, 3, center + Vector3.UnitZ * 12, target: 101))]),
            At(1, [Actor(101, 0, center + Vector3.UnitX * 7)]),
            At(2, [], removed: [101]), Cancel(3, 2, 15048), At(4, [])]);
        controller.Seek(.5);
        Check(Shown("carnivale/ray/cast/").Length == 1 && dangers() == 1
            && (Vector3)Value(Value(Shown("carnivale/ray/cast/").Single(), "Geometry"), "Position") == position,
            "Ray cast target anchor.");
        controller.Seek(1.5);
        Check((Vector3)Value(Value(Shown("carnivale/ray/cast/").Single(), "Geometry"), "Position") == center + Vector3.UnitX * 7,
            "Ray moving target.");
        controller.Seek(2.5);
        Check((Vector3)Value(Value(Shown("carnivale/ray/cast/").Single(), "Geometry"), "Position") == center + Vector3.UnitZ * 12,
            "Ray missing target fallback.");
        controller.Seek(3.5);
        Check(Values("CarnivaleRayCasts").Length == 0 && dangers() == 0, "Ray cast cancellation.");
        controller.Seek(1.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage17 MagitekRay cast current target follows movement missing target Loc fallback cancel navigation double view reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            EffectEvent(.1, 2, 15048, 1, center, target: 100),
            TreasureNativeEffect(.1, 2, 15048, 1, 101, [100, 101], center + Vector3.UnitZ * 20),
            TreasureNativeEffect(.2, 2, 15048, 2, 101, []),
            At(1, [Actor(101, 0, center + Vector3.UnitX * 15)]),
            At(10, []),
            At(11, [Actor(5, 0x1E8D9B, position + Vector3.UnitX * .123456f, dead: true)]),
            At(11.1, [Actor(5, 0x1E8D9B, position + Vector3.UnitX * .123456f, dead: true, eventState: 7)]),
            At(12, [])]);
        controller.Seek(.15);
        var prediction = Values("CarnivaleRayPredictions").Single();
        Check((ulong)Value(prediction, "MainTarget") == 101 && (uint)Value(prediction, "Sequence") == 1
            && (Vector3)Value(prediction, "Position") == position
            && (DateTime)Value(prediction, "Activation") == ((DateTime)Value(prediction, "ObservedAt")).AddSeconds((double)1.1f)
            && dangers() == 1 && Shown("carnivale/ray/event/").Length == 1, "Native MainTarget or flattened legacy double event.");
        controller.Seek(10.5);
        Check(Values("CarnivaleRayPredictions").Length == 2 && dangers() == 2
            && Values("CarnivaleRayPredictions").All(p => (Vector3)Value(p, "Position") == position), "Event movement/timeout/empty target packet.");
        controller.Seek(11.01);
        Check(Shown("carnivale/ray/live/").Length == 1 && dangers() >= 1, "Dead live zone not observed.");
        controller.Seek(11.15);
        Check(Shown("carnivale/ray/live/").Length == 0, "State7 live zone displayed.");
        controller.Seek(.15);
        manager.SetMechanismEnabled(guid, "刚柔铁巨人——克利俄斯改·第二幕", false);
        controller.Seek(.16);
        Check(previews().Length == 0 && dangers() == 0, "Ray event disabled owner.");
        manager.SetMechanismEnabled(guid, "刚柔铁巨人——克利俄斯改·第二幕", true);
        stop();
        Console.WriteLine("PASS native Carnivale Stage17 actual native MainTarget packet flattened legacy skip empty targets float consumer delay locked events no timeout dead quantized live state7 owner reverse Seek");

        load(guid, [Header(796), At(0, [Actor(9, 0x26F2, center, combat: true), Actor(100, 0, center)], true),
            At(.05, Initial()),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15048, 3, center, target: 101))]),
            TreasureNativeEffect(.2, 2, 15048, 1, 101, []),
            At(.3, [Actor(9, 0x26F2, center, combat: true, dead: true)]),
            Cancel(.4, 2, 15048), At(1, [])]);
        controller.Seek(.25);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x26F2 && Shown("carnivale/ray/").Length == 0,
            "Ray background leaked output.");
        controller.Seek(.35);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x2721
            && Values("CarnivaleRayCasts").Length == 1 && Values("CarnivaleRayPredictions").Length == 1 && dangers() == 2,
            "Ray background input lost during handoff.");
        controller.Seek(.5);
        Check(Values("CarnivaleRayCasts").Length == 0 && Values("CarnivaleRayPredictions").Length == 1 && dangers() == 1,
            "Ray Finish cleared event state.");
        controller.Seek(.35);
        stop();
        Console.WriteLine("PASS native Carnivale Stage17 background cast and native effect retained immediate module handoff independent event cancellation reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            TreasureNativeEffect(.1, 2, 15048, 0, 999, [], center),
            TreasureNativeEffect(.2, 2, 15048, 0, 999, [], center + Vector3.UnitX * 3), At(1, [])]);
        controller.Seek(.5);
        Check(Values("CarnivaleRayPredictions").Length == 2 && dangers() == 2
            && Values("CarnivaleRayPredictions").All(p => (uint)Value(p, "Sequence") == 0)
            && Values("CarnivaleRayPredictions").Select(p => (Vector3)Value(p, "Position")).Distinct().Count() == 2,
            "Distinct native zero-sequence packets collapsed.");
        controller.Seek(.15);
        Check(Values("CarnivaleRayPredictions").Length == 1, "Zero-sequence reverse Seek.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage17 distinct native zero sequence timestamps preserve both persistent event predictions double view navigation reverse Seek");
    }
}
