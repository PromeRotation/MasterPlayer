using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleBoom(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var center = new Vector3(100, 0, 100);
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Encounter()
        {
            var script = Value(manager.Running[guid], "Script");
            return script.GetType().BaseType!.GetField("_encounter", flags)!.GetValue(script)!;
        }
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        object[] Values(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: [new { Slot = 0, EntityId = 100ul }]);
        string Cancel(double t, ulong source, uint action)
            => JsonSerializer.Serialize(new { t, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object Named(ulong id, uint oid, string name, bool targetable = true, bool dead = false)
        {
            var actor = JsonSerializer.SerializeToNode(Actor(id, oid, center, targetable: targetable, dead: dead))!;
            actor["Name"] = name;
            return actor;
        }
        object[] Initial(uint oid = 0x26FE, bool combat = true) =>
        [
            Actor(1, oid, center, combat: combat), Actor(2, 0x233C, center), Actor(100, 0, center),
        ];

        load(guid, [Header(796), At(0, [.. Initial(0x26FC), Actor(4, 0x26FC, center)], true),
            At(.1, [Actor(100, 0, center + Vector3.UnitY)]),
            At(.2, [Actor(3, 0x26FE, center, dead: true), Actor(100, 0, center + Vector3.UnitX)]),
            At(.3, [Actor(3, 0x26FE, center, dead: true, targetable: false)]),
            At(.4, [Actor(100, 0, center + Vector3.UnitX * 2)]),
            At(1, [Actor(1, 0x26FC, center, dead: true)]),
            At(2, [Actor(4, 0x26FC, center, dead: true)]), At(3, [])]);
        controller.Seek(.05);
        Check(Value(Encounter(), "Active") == null && Hints().Any(h => h.Contains("first act is easy"))
            && dangers() == 0 && Shown("carnivale/precombat/arena").Length == 1, "Cold grenade movement or navigation.");
        controller.Seek(.15);
        Check(Value(Encounter(), "Active") == null, "Y movement pulled.");
        controller.Seek(.25);
        Check(Value(Encounter(), "Active") == null, "Dead but targetable Act2 failed to block.");
        controller.Seek(.35);
        Check(Value(Encounter(), "Active") == null, "Unblocking reused previous movement.");
        controller.Seek(.45);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x26FC
            && Hints().Contains("Kill the grenades in one hit each or they will wipe you. They got 543 HP.")
            && dangers() == 0 && Shown("carnivale/enemy/").Length == 2, "Moving targetable grenade pull/combat hints.");
        controller.Seek(1.5);
        Check(Value(Encounter(), "Active") != null && Shown("carnivale/enemy/").Length == 1, "Primary death ended group.");
        controller.Seek(2.5);
        Check(Value(Encounter(), "Active") == null && Shown("carnivale/enemy/").Length == 0 && dangers() == 0,
            "Group death retained combat output.");
        controller.Seek(.45);
        stop();
        Console.WriteLine("PASS native Carnivale Stage22 first act exact current actor XZ movement cold Y stationary targetable Act2 guard group pull and all-dead lifecycle reverse Seek");

        var sourceAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var layouts = sourceAssembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!;
        var wdir = sourceAssembly.GetType("BossMod.WDir")!;
        foreach (var oid in new uint[] { 0x26FC, 0x26FE })
        {
            load(guid, [Header(796), At(0, [.. Initial(oid, false), Named(4, 0x26FC, "Grenade"), Named(5, 0x26FD, "Gas")], true),
                At(.1, [Actor(1, oid, center, combat: true), Actor(100, 0, center + Vector3.UnitX)]), At(1, [])]);
            controller.Seek(.5);
            var bounds = layouts.GetField(oid == 0x26FC ? "CircleBig" : "CircleSmall", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
            dynamic arena = Value(Encounter(), "Arena");
            var count = 0;
            for (var x = 74.13f; x < 126; x += .67f)
                for (var z = 74.17f; z < 126; z += .71f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(wdir, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Boom source polygon.");
                    ++count;
                }
            if (oid == 0x26FE)
                Check(Shown("carnivale/enemy/").Length == 3 && Hints().Any(h => h.StartsWith("Grenade in one hit"))
                    && Hints().Any(h => h.Contains("pull the Gas to the bos\n"))
                    && (Vector4)Value(Shown("carnivale/enemy/4").Single(), "Color") == new Vector4(1, 128f / 255, 0, 1),
                    "Boom add markers/hints.");
            var name = "爆炸湮灭——爆弹之祖·" + (oid == 0x26FC ? "第一幕" : "第二幕");
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(.6);
            Check(previews().Length == 0 && dangers() == 0, "Boom disabled owner.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage22 {oid:X} source arena oracle {count} points dual output grenade and gas colours text no invented priority reverse Seek owner");
        }

        foreach (var action in new uint[] { 14903, 14902 })
        {
            load(guid, [Header(796), At(0, Initial(), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, rotation: .4f))]),
                EffectEvent(1, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(.5);
            Check(dangers() == 1 && Shown("cast/").Length == 1
                && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action }),
                "Boom ordinary native outputs.");
            controller.Seek(8.5);
            Check(dangers() == 1, "Boom effect/prediction timeout.");
            controller.Seek(9.5);
            Check(dangers() == 0 && Shown("cast/").Length == 0, "Boom actual Finish.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage22 exact 11.75 total120 cone and 7.2 circle native navigation dual output raw locks effect deadline Finish reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14906, 9, center)),
                Actor(3, 0x233C, center, cast: Cast(14907, 3, center + Vector3.UnitZ * 5))]),
            EffectEvent(1, 2, 14906, 1), At(12, []), Cancel(13, 2, 60000),
            Cancel(14, 3, 14907), At(15, [])]);
        controller.Seek(.5);
        Check(Shown("carnivale/sap/").Length == 2 && dangers() == 2 && Shown("cast/").Length == 0, "Sap shared group duplicated.");
        controller.Seek(12.5);
        Check(Shown("carnivale/sap/").Length == 2 && dangers() == 2, "Sap effect/timeout.");
        controller.Seek(13.5);
        Check(Shown("carnivale/sap/").Length == 1 && dangers() == 1, "Sap arbitrary Finish.");
        controller.Seek(14.5);
        Check(Shown("carnivale/sap/").Length == 0 && dangers() == 0, "Sap full Finish.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage22 two-action Sap shared insertion group R8 navigation double view arbitrary action Finish no effect timeout reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(15040, 4, center))]),
            At(1, [Actor(2, 0x233C, center, cast: Cast(15040, 40, center))]), At(50, []),
            Cancel(51, 2, 15040), At(52, [])]);
        controller.Seek(.5);
        Check(Values("PredictedDamage").Length == 1 && dangers() == 0 && Shown("cast/").Length == 0
            && Hints().Contains("Wipe if Grenade is not killed yet, otherwise Raidwide"), "Ignition damage versus AOE scope.");
        controller.Seek(1.5);
        var activation = JsonSerializer.SerializeToElement(Cast(15040, 40, center)).GetProperty("FinishAt").GetDateTime();
        Check(Values("PredictedDamage").All(d => (DateTime)Value(d, "Activation") == activation), "Ignition raw current clock.");
        controller.Seek(50.5);
        Check(Values("PredictedDamage").All(d => (DateTime)Value(d, "Activation") >= Start.AddSeconds(50.5)),
            "Ignition saturated NPC remaining time.");
        controller.Seek(51.5);
        Check(Values("PredictedDamage").Length == 0 && dangers() == 0, "Ignition Finish.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage22 Ignition current raw damage clock NPC remaining floor raidwide text real Finish no navigation or automatic tongue enrage action");

        load(guid, [Header(796), At(0, [Actor(1, 0x272F, center, combat: true), Actor(4, 0x26FE, center), Actor(100, 0, center)], true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(14907, 1, center)),
                Actor(3, 0x233C, center, cast: Cast(14902, 1, center))]),
            At(10, [Actor(4, 0x26FE, center, combat: true)]),
            At(11, [Actor(1, 0x272F, center, dead: true)]), Cancel(12, 2, 60000), At(13, [])]);
        controller.Seek(10.5);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x272F
            && Shown("carnivale/sap/").Length == 0, "Boom background output leak.");
        controller.Seek(11.5);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x26FE
            && Shown("carnivale/sap/").Length == 1 && Shown("cast/").Length == 1 && dangers() == 2,
            "Boom unclaimed pre-pull casts not bootstrapped.");
        controller.Seek(12.5);
        Check(Shown("carnivale/sap/").Length == 0 && dangers() == 1, "Boom background Finish isolation.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage22 late activation unclaimed raw Sap and ordinary source casts independent lock clocks background immediate handoff no duplicate output reverse Seek");
    }
}
