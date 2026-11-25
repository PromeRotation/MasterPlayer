using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivalePit(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Encounter()
        {
            var script = Value(manager.Running[guid], "Script");
            return script.GetType().BaseType!.GetField("_encounter", flags)!.GetValue(script)!;
        }
        object[] Values(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        int Obstacles() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
        object[] Moves()
        {
            var player = ((IDictionary)Value(Encounter(), "Actors"))[100ul]!;
            return ((IEnumerable)Encounter().GetType().GetMethod("CarnivaleCastKnockbackMovements", flags)!
                .Invoke(Encounter(), [player, 15325u])!).Cast<object>().ToArray();
        }
        Vector3 From(object movement) => (Vector3)movement.GetType().GetField("Item2")!.GetValue(movement)!;
        Vector3 To(object movement) => (Vector3)movement.GetType().GetField("Item3")!.GetValue(movement)!;
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed,
                party: [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }]);
        string Cancel(double t, ulong source, uint action)
            => JsonSerializer.Serialize(new { t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object Named(ulong id, uint oid, string name, bool targetable = true, bool dead = false, bool combat = false)
        {
            var actor = JsonSerializer.SerializeToNode(Actor(id, oid, center, targetable: targetable, dead: dead, combat: combat))!;
            actor["Name"] = name;
            return actor;
        }
        object[] Initial(uint oid = 0x2739, bool combat = true) =>
        [
            Actor(1, oid, center, combat: combat), Actor(2, 0x233C, center),
            Actor(100, 0, center + Vector3.UnitZ * 5), Actor(101, 0, center + Vector3.UnitX * 5),
        ];
        var sourceAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var layouts = sourceAssembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!;
        var wdir = sourceAssembly.GetType("BossMod.WDir")!;
        foreach (var (oid, add) in new[] { (0x2735u, 0x2734u), (0x2736u, 0x2738u), (0x2739u, 0x273Au) })
        {
            load(guid, [Header(796), At(0, Initial(oid, false), true),
                At(.1, [Named(4, add, "Add", combat: true)]), At(1, [Actor(1, oid, center, combat: true)]),
                At(2, [Actor(1, oid, center, dead: true)]), At(3, [Actor(4, add, center, dead: true)]), At(4, [])]);
            controller.Seek(.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x2735), "Pit group/default pull.");
            controller.Seek(1.5);
            Check(Shown("carnivale/enemy/").Length == 2
                && (Vector4)Value(Shown("carnivale/enemy/4").Single(), "Color") == new Vector4(1, 0, 0, 1), "Pit red enemy markers.");
            var bounds = layouts.GetField(oid == 0x2739 ? "CircleSmall" : "CircleBig")!.GetValue(null)!;
            dynamic arena = Value(Encounter(), "Arena");
            var count = 0;
            for (var x = 74.13f; x < 126; x += .67f)
                for (var z = 74.17f; z < 126; z += .71f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(wdir, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Pit source polygon.");
                    ++count;
                }
            controller.Seek(2.5);
            Check((Value(Encounter(), "Active") != null) == (oid == 0x2735), "Pit primary/group completion.");
            controller.Seek(3.5);
            Check(Value(Encounter(), "Active") == null && dangers() == 0 && Obstacles() == 0, "Pit completion.");
            controller.Seek(1.5);
            var name = "异形人偶师——艾匹罗基·" + (oid == 0x2735 ? "第一幕" : oid == 0x2736 ? "第二幕" : "第三幕");
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(1.6);
            Check(previews().Length == 0 && dangers() == 0 && Obstacles() == 0, "Pit disabled owner.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage24 {oid:X} source arena oracle {count} points group versus primary lifecycle red markers reverse Seek disabled owner");
        }
        foreach (var (oid, action) in new[]
        {
            (0x2735u, 15317u), (0x2735u, 15316u), (0x2736u, 15317u), (0x2736u, 15316u),
            (0x2736u, 15322u), (0x2739u, 15327u), (0x2739u, 15324u),
        })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, rotation: .4f))]),
                EffectEvent(1, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(.5);
            Check(dangers() == 1 && Shown("cast/").Length == 1, "Pit ordinary outputs.");
            controller.Seek(8.5);
            Check(dangers() == 1, "Pit ordinary timeout/effect.");
            controller.Seek(9.5);
            Check(dangers() == 0 && Shown("cast/").Length == 0, "Pit ordinary Finish.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage24 seven ordinary instances exact R5 R6 R8 and R8 PageTear raw locks navigation dual outputs observed Finish reverse Seek");

        foreach (var (oid, action) in new[] { (0x2735u, 15318u), (0x2736u, 15321u) })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center))]),
                Cancel(1, 2, action), At(2, [])]);
            controller.Seek(.5);
            Check(Values("CarnivaleInterrupts").Length == 1 && Hints().Contains("Interrupt!")
                && Values("PredictedDamage").Length == 0 && dangers() == 0, "Pit interrupt scope.");
            controller.Seek(1.5);
            Check(Values("CarnivaleInterrupts").Length == 0, "Pit interrupt Finish.");
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage24 LightningSpark Silence independent intent cast identities real Finish no automatic skill or invented damage");

        load(guid, [Header(796), At(0, Initial(0x2736), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15319, 4, center, target: 100)),
                Actor(3, 0x233C, center, cast: Cast(15320, 4, center, target: 3), targetId: 101)]),
            At(1, [Actor(2, 0x233C, center, cast: Cast(15319, 30, center, target: 101)), Actor(101, 0, center, dead: true)]),
            Cancel(2, 2, 15319), Cancel(3, 3, 15320), At(4, [])]);
        controller.Seek(.5);
        Check(Values("PredictedDamage").Select(d => (ulong)Value(d, "TargetId")).Order().SequenceEqual(new ulong[] { 100, 101 })
            && Values("PredictedDamage").All(d => (string)Value(d, "DamageType") == "Tankbuster")
            && dangers() == 0 && Hints().Contains("Use Diamondback!"), "Pit single targets/self fallback scope.");
        controller.Seek(1.5);
        Check(Values("PredictedDamage").Count(d => (ulong)Value(d, "TargetId") == 101) == 2, "Pit raw target/dead roster.");
        controller.Seek(2.5);
        Check(Values("PredictedDamage").Length == 1, "Pit one action Finish cleared both.");
        controller.Seek(3.5);
        Check(Values("PredictedDamage").Length == 0, "Pit full single Finish.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage24 two-action single target current raw clock self-cast current target fallback dead roster independent Finish no AOE or auto Diamondback");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(4, 0x273B, center, targetable: false, eventState: 7, cast: Cast(15328, 3, center))]),
            At(1, [Actor(4, 0x273B, center, dead: true)]),
            At(2, [Actor(4, 0x273B, center + Vector3.UnitX, targetable: false, eventState: 7)]),
            At(3, [], removed: [4]), At(4, [])]);
        controller.Seek(.5);
        Check(Obstacles() == 1 && Shown("carnivale/vacuum/").Length == 1 && dangers() == 0
            && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 4, ActionId = 15328 }),
            "Pit vacuum obstacle or duplicate cast.");
        controller.Seek(1.5);
        Check(Obstacles() == 0 && Shown("carnivale/vacuum/").Length == 0, "Dead vacuum remained.");
        controller.Seek(2.5);
        Check(Obstacles() == 1 && Shown("carnivale/vacuum/").Length == 1, "Revived vacuum missing.");
        controller.Seek(3.5);
        Check(Obstacles() == 0, "Destroyed vacuum remained.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage24 live R3 vacuum hard obstacles state7 untargetable death hide resurrection restore real destroy no cast dependency dual view reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15325, 5, center, rotation: MathF.PI / 2, target: 100))]),
            At(1, [Actor(100, 0, center + Vector3.UnitZ * 10),
                Actor(2, 0x233C, center + Vector3.UnitX * 3, rotation: -1, cast: Cast(15325, 5, center, rotation: MathF.PI / 2, target: 100))]),
            Cancel(2, 2, 15325), At(3, [])]);
        controller.Seek(.5);
        Check(Values("CarnivaleCharges").Length == 1 && Moves().Length == 1
            && Shown("carnivale/charge/").Length != 0 && Shown("carnivale/pit/kb/").Length != 0
            && Values("PredictedDamage").Any(d => (string)Value(d, "DamageType") == "Tankbuster"), "Pit charge/KB/prediction.");
        controller.Seek(1.5);
        Check(Vector3.Distance(To(Moves().Single()), center + new Vector3(10, 0, 10)) < .001f,
            "Pit KB followed dynamic charge facing.");
        controller.Seek(2.5);
        Check(Values("CarnivaleCharges").Length == 0 && Moves().Length == 0
            && Shown("carnivale/pit/kb/").Length == 0, "Pit charge KB Finish.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage24 dynamic old-reference charge navigation damage independent Start-locked forward10 KB display real Finish reverse Seek");

        var landing = center + new Vector3(10, 0, 5);
        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15325, 5, center, rotation: MathF.PI / 2, target: 999)),
                Actor(3, 0x233C, center, cast: Cast(15325, 5, center, rotation: MathF.PI, target: 999))]),
            At(1, [Actor(4, 0x273B, landing, targetable: false, eventState: 7)]),
            At(2, [Actor(4, 0x273B, landing, dead: true)]),
            At(3, [Actor(100, 0, center + Vector3.UnitZ * 5, stackedStatus: 1722)]), At(4, [])]);
        controller.Seek(.5);
        var moves = Moves();
        Check(Values("CarnivaleCharges").Length == 0 && moves.Length == 2 && From(moves[1]) == To(moves[0])
            && dangers() == 0, "Pit KB target eligibility or sequential chain.");
        controller.Seek(1.5);
        Check(Hints().Contains("About to be knocked into danger!") && Obstacles() == 1, "Pit live landing hazard.");
        controller.Seek(2.5);
        Check(!Hints().Contains("About to be knocked into danger!") && Obstacles() == 0, "Pit dead hazard remains.");
        controller.Seek(3.5);
        Check(Moves().Length == 0 && Shown("carnivale/pit/kb/").Length == 0, "Pit immunity projection.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage24 multi-caster forward chain without bait target locked rotations current live electric landing risk strict native immunity no KB navigation reverse Seek");

        load(guid, [Header(796), At(0, [.. Initial(0x2736), Named(4, 0x2737, "Viking")], true),
            At(.1, [Named(5, 0x2738, "Magus")]), At(1, [Named(5, 0x2738, "Magus", dead: true)]), At(2, [])]);
        controller.Seek(.05);
        Check(!Hints().Any(h => h.Contains("immune to")), "Viking substituted for source Magus bug.");
        controller.Seek(.5);
        Check(Hints().Count(h => h.StartsWith("Magus is immune to")) == 2, "Source duplicate Magus text not preserved.");
        controller.Seek(1.5);
        Check(!Hints().Any(h => h.Contains("immune to")), "Dead Magus text retained.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage24 source second act both immunity text queries use Magus no invented damage-type action restriction");
    }
}
