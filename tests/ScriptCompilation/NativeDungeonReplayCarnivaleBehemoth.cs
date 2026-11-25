using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleBehemoth(object managerObject, object runtimeObject, object controllerObject,
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
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        int Obstacles() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: [new { Slot = 0, EntityId = 100ul }]);
        string Cancel(double t, ulong source, uint action)
            => JsonSerializer.Serialize(new { t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(bool combat = true) => [Actor(1, 0x2732, center, combat: combat), Actor(2, 0x233C, center), Actor(100, 0, center)];

        load(guid, [Header(796), At(0, Initial(false), true), At(.1, [Actor(1, 0x2732, center, combat: true)]),
            At(1, [Actor(1, 0x2732, center, dead: true)]), At(2, [])]);
        controller.Seek(.05);
        Check(Value(Encounter(), "Active") == null && Hints().Any(h => h.Contains("40%"))
            && dangers() == 0 && Obstacles() == 0, "Behemoth preparation.");
        controller.Seek(.5);
        var sourceAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var small = sourceAssembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!.GetField("CircleSmall")!.GetValue(null)!;
        var wdir = sourceAssembly.GetType("BossMod.WDir")!;
        dynamic arena = Value(Encounter(), "Arena");
        var count = 0;
        for (var x = 74.13f; x < 126; x += .67f)
            for (var z = 74.17f; z < 126; z += .71f)
            {
                var expected = (bool)small.GetType().GetMethod("Contains")!.Invoke(small,
                    [Activator.CreateInstance(wdir, [x - 100, z - 100])])!;
                Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Behemoth source polygon.");
                ++count;
            }
        controller.Seek(1.5);
        Check(Value(Encounter(), "Active") == null && previews().Length == 0 && Obstacles() == 0, "Behemoth completion.");
        controller.Seek(.5);
        manager.SetMechanismEnabled(guid, "魔兽皇太子——贝希摩斯太子", false);
        controller.Seek(.6);
        Check(previews().Length == 0 && dangers() == 0 && Obstacles() == 0, "Behemoth disabled owner.");
        manager.SetMechanismEnabled(guid, "魔兽皇太子——贝希摩斯太子", true);
        stop();
        Console.WriteLine($"PASS native Carnivale Stage23 source arena oracle {count} points preparation default primary lifecycle no extra enemy markers reverse Seek owner");

        var pos = center + new Vector3(1.12345f, 2.6f, 3.87654f);
        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(3, 0x2733, pos, dead: true, targetable: false, eventState: 7, cast: Cast(15259, 1, pos))]),
            Cancel(1, 3, 15259), At(2, [Actor(3, 0x2733, pos + Vector3.UnitX * 2, dead: true, targetable: false, eventState: 7)]),
            At(3, [], removed: [3]), At(4, [])]);
        controller.Seek(.5);
        Check(Shown("carnivale/maelstrom/").Length == 1 && Obstacles() == 1 && dangers() == 0
            && Hints().Contains("GTFO from voidzone!") && Shown("carnivale/enemy/").Length == 1
            && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 3, ActionId = 15259 }),
            "Maelstrom exact entity obstacle scope.");
        controller.Seek(2.5);
        Check(Shown("carnivale/maelstrom/").Length == 1 && Obstacles() == 1, "Pool Finish/dead/state7 lifecycle.");
        controller.Seek(3.5);
        Check(Shown("carnivale/maelstrom/").Length == 0 && Obstacles() == 0, "Pool destruction.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage23 moving unquantized R8 entity hard obstacle includes dead untargetable state7 actual destroy clears Finish does not remove no cast duplicate reverse Seek");

        foreach (var action in new uint[] { 15258, 15256, 15261 })
        {
            load(guid, [Header(796), At(0, Initial(), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, rotation: .4f))]),
                EffectEvent(1, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(.5);
            Check(dangers() == 1 && Shown("cast/").Length == 1, "Behemoth ordinary outputs.");
            controller.Seek(8.5);
            Check(dangers() == 1, "Behemoth ordinary timeout/effect.");
            controller.Seek(9.5);
            Check(dangers() == 0 && Shown("cast/").Length == 0, "Behemoth ordinary Finish.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage23 exact R6 R10 and R55.8 total60 cone native navigation dual outputs raw locks observed Finish reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15257, 10, center))]),
            At(1, [Actor(2, 0x233C, center, cast: Cast(15257, 40, center))]), At(50, []),
            Cancel(51, 2, 15257), At(52, [])]);
        controller.Seek(.5);
        Check(Damage().Length == 1 && dangers() == 0 && Obstacles() == 0 && Hints().Contains("Use Diamondback!"), "Meteor scope.");
        controller.Seek(1.5);
        var activation = JsonSerializer.SerializeToElement(Cast(15257, 40, center)).GetProperty("FinishAt").GetDateTime();
        Check(Damage().All(d => (DateTime)Value(d, "Activation") == activation), "Meteor dynamic raw clock.");
        controller.Seek(50.5);
        Check(Damage().All(d => (DateTime)Value(d, "Activation") >= Start.AddSeconds(50.5)), "Meteor saturated delay.");
        controller.Seek(51.5);
        Check(Damage().Length == 0 && dangers() == 0, "Meteor Finish.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage23 Meteor current raw prediction NPC delay Diamondback text real Finish no R50 navigation pull or automatic action");

        load(guid, [Header(796), At(0, Initial(false), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15258, 1, center))]),
            At(10, [Actor(1, 0x2732, center, combat: true)]), Cancel(11, 2, 15258), At(12, [])]);
        controller.Seek(10.1);
        Check(dangers() == 1 && Shown("cast/").Length == 1, "Behemoth pre-pull unclaimed raw not replayed.");
        controller.Seek(11.5);
        Check(dangers() == 0 && Shown("cast/").Length == 0, "Behemoth consumed raw resurrected.");
        controller.Seek(10.1);
        stop();
        load(guid, [Header(796), At(0, Initial(false), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15258, 1, center))]),
            Cancel(.2, 2, 15258), At(10, [Actor(1, 0x2732, center, combat: true)]), At(12, [])]);
        controller.Seek(10.1);
        Check(dangers() == 0 && Shown("cast/").Length == 0, "Pre-activation cancel revived stale raw.");
        controller.Seek(.1);
        controller.Seek(10.1);
        Check(dangers() == 0, "Pre-activation cancel lost after reverse Seek.");
        stop();
        Console.WriteLine("PASS native Carnivale Stage23 late component activation claims current raw and preserves NPC finish delay consumed cast watermark reverse Seek");
    }
}
