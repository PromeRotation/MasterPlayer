using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunSkalla(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.skalla";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter()
        {
            var script = Script();
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object aoe) => (string)aoe.GetType().GetProperty("Id")!.GetValue(aoe)!;
        DateTime Activation(object aoe) => (DateTime)aoe.GetType().GetProperty("Activation")!.GetValue(aoe)!;
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var center = new Vector3(-220, 0, 4);
        load(id,
        [
            Header(1172), Frame(1172, 0, [Actor(1, 0x1FAB, center - Vector3.UnitX * 14, combat: true),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 10), Actor(10, 0x2051, center + Vector3.UnitZ * 5)],
                full: true, party: party),
            AlaTether(.1, 10, 101, 3, true), ResearchIconEvent(.1, 100, 43),
            Frame(1172, .2, [Actor(10, 0x2051, center + Vector3.UnitZ * 5, tethers: [new { Id = 3, TargetId = 101UL }]),
                Actor(1, 0x1FAB, center - Vector3.UnitX * 14, combat: true, cast: Cast(9810, 7, center - Vector3.UnitX * 14,
                    MathF.PI / 2, startedAt: .2))], party: party),
            AlaTether(1, 10, 101, 3, false), Frame(1172, 2, [], party: party),
            EffectEvent(3, 20, 9813, 1),
            Frame(1172, 3.1, [Actor(11, 0x1EA7D5, center + Vector3.UnitZ * 10)], party: party),
            Frame(1172, 7.2, [Actor(1, 0x1FAB, center - Vector3.UnitX * 14, combat: true)], removed: [11], party: party),
        ]);
        controller.Seek(.3);
        Check(Aoes().Any(a => AoeId(a) == "skalla/sphere/10/display")
            && Aoes().Any(a => AoeId(a).EndsWith("/landing")) && dangers() >= 3, "Production Skalla tether/puddle/KB missing.");
        controller.Seek(2.1); Check(!Aoes().Any(a => AoeId(a).StartsWith("skalla/sphere")), "Production Skalla stale sphere resurrected.");
        controller.Seek(3.2); Check(!Aoes().Any(a => AoeId(a).StartsWith("skalla/puddle"))
            && Aoes().Any(a => AoeId(a).StartsWith("skalla/pool")), "Production Skalla puddle handoff failed.");
        controller.Seek(7.3); Check(previews().Length == 0 && dangers() == 0, "Production Skalla KB/voidzone cleanup failed.");
        controller.Seek(.3); Check(Aoes().Any(a => AoeId(a).EndsWith("/landing")), "Production Skalla Kelpie seek failed."); stop();

        center = new(115, 0, 4);
        load(id,
        [
            Header(1172), Frame(1172, 0, [Actor(1, 0x1FAC, center, combat: true, stackedStatus: 325),
                Actor(100, 0, center, stackedStatus: 1448), Actor(10, 0x1FAD, center + Vector3.UnitZ)], full: true, party: party),
            Frame(1172, .1, [Actor(11, 0x18D6, center + Vector3.UnitX * 10, cast: Cast(9817, 3.5, center + Vector3.UnitX * 10))], party: party),
            StatusEvent(2, 1, 325, 0, false), StatusEvent(2, 100, 1448, 0, false),
            Frame(1172, 2.1, [Actor(1, 0x1FAC, center, combat: true), Actor(100, 0, center)], removed: [10], party: party),
            Frame(1172, 3.6, [Actor(11, 0x18D6, center + Vector3.UnitX * 10)], party: party),
        ]);
        controller.Seek(.2);
        Check(((ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!).SequenceEqual([1UL]),
            "Production Skalla invincible target not banned.");
        Check(Encounter().GetType().GetProperty("SkallaShatterstoneTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Encounter()) != null,
            "Production Skalla transformed duty target missing.");
        var script = Script();
        var duty = script.GetType().BaseType!.GetField("skallaDuty", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        Check((int)duty.GetType().GetProperty("Attempts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(duty)! == 0,
            "Production Skalla replay attempted native duty action.");
        controller.Seek(2.2); Check(((ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!).Length == 0
            && !Aoes().Any(a => AoeId(a).StartsWith("skalla/subservient")), "Production Skalla invincibility/add cleanup failed.");
        controller.Seek(3.7); Check(previews().Length == 0 && dangers() == 0, "Production Skalla flame cleanup failed.");
        controller.Seek(.2); Check(previews().Length == 2, "Production Skalla Old One seek failed."); stop();

        center = new(479, 0, 4);
        load(id,
        [
            Header(1172), Frame(1172, 0, [Actor(1, 0x1FAE, center, combat: true), Actor(100, 0, center - Vector3.UnitZ * 8),
                Actor(101, 0, center + Vector3.UnitX * 10)], full: true, party: party),
            ResearchIconEvent(.1, 100, 121), ResearchIconEvent(.1, 101, 122), ResearchIconEvent(.1, 100, 28),
            Frame(1172, .2, [Actor(1, 0x1FAE, center, combat: true, cast: Cast(9829, 3, center, startedAt: .2))], party: party),
            EffectEvent(3, 10, 9833, 1),
            Frame(1172, 3.2, [Actor(1, 0x1FAE, center, combat: true)], party: party),
            EffectEvent(4, 10, 9831, 2),
        ]);
        controller.Seek(.3); Check(Aoes().Count(a => AoeId(a).EndsWith("/display")) == 3 && previews().Length >= 4,
            "Production Skalla chaos/gaze drawing missing.");
        Check(Aoes().Where(a => AoeId(a).EndsWith("/display")).All(a =>
            Math.Abs((Activation(a) - Start.AddSeconds(4.8)).TotalMilliseconds) < 1), "Production Skalla chaos clock shifted.");
        controller.Seek(3.1); Check(Aoes().Count(a => AoeId(a).EndsWith("/display")) == 1
            && Aoes().Any(a => AoeId(a).Contains("/121/")), "Production Skalla mixed chaos cleanup differs.");
        controller.Seek(4.1); Check(previews().Length == 0 && dangers() == 0, "Production Skalla final chaos leaked.");
        controller.Seek(.3); Check(previews().Length >= 4, "Production Skalla Hrodric seek failed."); stop();
        Console.WriteLine("PASS native Skalla three bosses, directional knockback, sphere stale snapshots, puddle handoff, invincibility, transformed duty replay isolation, flame, gaze, mixed chaos timing, seek and stop");
    }
}
