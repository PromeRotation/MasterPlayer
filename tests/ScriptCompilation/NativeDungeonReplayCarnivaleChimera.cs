using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleChimera(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const string imps = "The imps are weak to fire spells and strong against ice.\nInterrupt Void Blizzard with Flying Sardine.";
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
        object[] Initial(uint oid = 0x2730) =>
        [
            Actor(1, oid, center, combat: true),
            Actor(2, 0x233C, center), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 3),
        ];
        foreach (var oid in new uint[] { 0x272F, 0x2730 })
        {
            var add = oid == 0x272F ? oid : 0x2731u;
            load(guid, [Header(796), At(0, [Actor(100, 0, center)], true),
                At(.1, [Actor(1, oid, center), Actor(4, add, center + Vector3.UnitX * 5, combat: true)]),
                At(1, [Actor(1, oid, center, combat: true)]),
                At(2, [Actor(1, oid, center, combat: true, dead: true)]),
                At(3, [], removed: [4]), At(4, [])]);
            controller.Seek(.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x272F), "Chimera group versus primary pull.");
            controller.Seek(1.5);
            Check(Shown("carnivale/enemy/").Length == 2 && Hints().Contains(imps)
                && Value(Encounter(), "CarnivalePrecombatHint") == null, "Chimera combat text/markers.");
            Check((Vector4)Value(Shown("carnivale/enemy/4").Single(), "Color") ==
                (oid == 0x2730 ? new Vector4(1, 128f / 255, 0, 1) : new Vector4(1, 0, 0, 1)), "Chimera imp colours.");
            var source = Assembly.Load("MasterPlayer.MechanicRuntime");
            var bounds = source.GetType("BossMod.Global.MaskedCarnivale.Layouts")!
                .GetField(oid == 0x272F ? "CircleBig" : "CircleSmall", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
            var wdir = source.GetType("BossMod.WDir")!;
            dynamic arena = Value(Encounter(), "Arena");
            var count = 0;
            for (var x = 74.13f; x < 126; x += .67f)
                for (var z = 74.17f; z < 126; z += .71f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(wdir, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Chimera source arena.");
                    ++count;
                }
            controller.Seek(2.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x272F), "Chimera group completion versus primary.");
            controller.Seek(3.5);
            Check(Value(Encounter(), "Active") == null && previews().Length == 0, "Chimera completion cleanup.");
            controller.Seek(1.5);
            var name = "愤怒合成兽——阿帕德马克·" + (oid == 0x272F ? "第一幕" : "第二幕");
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(1.6);
            Check(previews().Length == 0 && dangers() == 0, "Chimera disabled owner.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage21 {oid:X} source arena oracle {count} points group primary lifecycle imp colours combat hint reverse Seek owner");
        }
        foreach (var (oid, action, shape) in new[]
        {
            (0x272Fu, 15064u, "CircleTemplate"), (0x2730u, 15064u, "CircleTemplate"),
            (0x2730u, 15079u, "CircleTemplate"), (0x2730u, 15080u, "DonutTemplate"),
        })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center))]),
                EffectEvent(7, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(.5);
            var template = Value(Value(Value(Values("Casts").Single(), "Value"), "Rule"), "Shape");
            Check(template.GetType().Name == shape && dangers() == 1 && Shown("cast/").Length == 1
                && (action == 15080 ? (float)Value(template, "InnerRadius") == 8 && (float)Value(template, "OuterRadius") == 30
                    : (float)Value(template, "Radius") == (action == 15064 ? 5 : 9))
                && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action }),
                "Chimera exact ordinary rule/native dual output.");
            controller.Seek(8.5);
            Check(dangers() == 1, "Chimera ordinary expiry/effect.");
            controller.Seek(9.5);
            Check(dangers() == 0, "Chimera ordinary Finish.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage21 four ordinary component instances exact R5 R9 donut8 30 dual views navigation source observed lifetime reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15081, 6, center, target: 101)),
                Actor(3, 0x2731, center, targetable: false, cast: Cast(15063, 6, center))]),
            Cancel(1, 2, 15081), At(2, [Actor(3, 0x2731, center, dead: true)]), At(3, [])]);
        controller.Seek(.5);
        Check(Values("CarnivaleInterrupts").Length == 2 && Hints().Count(h => h == "Interrupt!") == 2
            && Hints().Contains(imps) && Shown("carnivale/enemy/3").Length == 0
            && Values("Gazes").Length == 0 && Values("PredictedDamage").Length == 0, "Two interrupts or imp hint targetability.");
        controller.Seek(1.5);
        Check(Values("CarnivaleInterrupts").Length == 1 && dangers() == 0, "Keeper independent interrupt clear.");
        controller.Seek(2.5);
        Check(!Hints().Contains(imps), "Dead imp combat hint.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage21 independent VoidBlizzard Keeper interrupt components imp text ignores targetability dead removes text no gaze extra damage or auto action");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15081, 6, center + Vector3.UnitZ * 12, target: 101))]),
            At(1, [Actor(101, 0, center + Vector3.UnitX * 7)]), At(2, [], removed: [101]),
            Cancel(3, 2, 15081), At(4, [])]);
        controller.Seek(.5);
        Check(Shown("carnivale/ray/cast/").Length == 1 && dangers() == 1
            && (Vector3)Value(Value(Shown("carnivale/ray/cast/").Single(), "Geometry"), "Position") == center + Vector3.UnitX * 3,
            "Keeper dynamic target.");
        controller.Seek(1.5);
        Check((Vector3)Value(Value(Shown("carnivale/ray/cast/").Single(), "Geometry"), "Position") == center + Vector3.UnitX * 7, "Keeper target movement.");
        controller.Seek(2.5);
        Check((Vector3)Value(Value(Shown("carnivale/ray/cast/").Single(), "Geometry"), "Position") == center + Vector3.UnitZ * 12, "Keeper missing target Loc.");
        controller.Seek(3.5);
        Check(Values("CarnivaleRayCasts").Length == 0 && Values("CarnivaleInterrupts").Length == 0 && dangers() == 0, "Keeper Finish.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage21 Keeper R9 cast dynamic current target missing fallback independent Finish navigation dual output reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true), EffectEvent(.1, 2, 15081, 0, center, target: 100),
            TreasureNativeEffect(.1, 2, 15081, 0, 101, [100, 101]), TreasureNativeEffect(.2, 2, 15081, 0, 101, []),
            At(1, [Actor(101, 0, center + Vector3.UnitX * 15)]), At(10, []),
            At(11, [Actor(5, 0x1E972A, center + Vector3.UnitX * 3.123456f, dead: true)]),
            At(11.1, [Actor(5, 0x1E972A, center + Vector3.UnitX * 3.123456f, dead: true, eventState: 7)]), At(12, [])]);
        controller.Seek(.15);
        var prediction = Values("CarnivaleRayPredictions").Single();
        Check((ulong)Value(prediction, "MainTarget") == 101 && (uint)Value(prediction, "Action") == 15081
            && (DateTime)Value(prediction, "Activation") == ((DateTime)Value(prediction, "ObservedAt")).AddSeconds(.9d)
            && dangers() == 1, "Keeper native MainTarget, delay or legacy duplicate.");
        controller.Seek(10.5);
        Check(Values("CarnivaleRayPredictions").Length == 2 && dangers() == 2
            && Values("CarnivaleRayPredictions").All(p => (Vector3)Value(p, "Position") == center + Vector3.UnitX * 3), "Keeper event lock/empty targets/zero sequence.");
        controller.Seek(11.01);
        Check(Shown("carnivale/ray/live/").Length == 1 && dangers() >= 1, "Keeper dead live entity.");
        controller.Seek(11.15);
        Check(Shown("carnivale/ray/live/").Length == 0, "Keeper state7.");
        controller.Seek(.15);
        stop();
        Console.WriteLine("PASS native Carnivale Stage21 Keeper actual native packet MainTarget empty targets legacy skip zero events exact point nine delay persistent lock dead live state7 source handoff reverse Seek");

        load(guid, [Header(796), At(0, [Actor(9, 0x272F, center, combat: true), Actor(100, 0, center)], true),
            At(.05, Initial()),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15063, 6, center))]),
            TreasureNativeEffect(.2, 2, 15081, 1, 100, []),
            At(.3, [Actor(9, 0x272F, center, combat: true, dead: true)]), Cancel(.4, 2, 15063), At(1, [])]);
        controller.Seek(.25);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x272F && Shown("carnivale/ray/").Length == 0, "Keeper background output leak.");
        controller.Seek(.35);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x2730 && Values("CarnivaleInterrupts").Length == 1
            && Values("CarnivaleRayPredictions").Length == 1 && dangers() == 1, "Keeper shared cast/interrupt/event handoff.");
        controller.Seek(.5);
        Check(Values("CarnivaleInterrupts").Length == 0 && Values("CarnivaleRayPredictions").Length == 1, "Keeper shared Finish cleared event.");
        controller.Seek(.35);
        stop();
        Console.WriteLine("PASS native Carnivale Stage21 shared VoidBlizzard background input and Keeper native event retained independent Finish primary handoff reverse Seek owner");
    }
}
