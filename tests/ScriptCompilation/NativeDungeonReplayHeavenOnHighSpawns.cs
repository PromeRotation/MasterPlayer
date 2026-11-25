using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHeavenOnHighSpawns(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        var center = new Vector3(-300, 0, -300);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Read(object value, string name)
        {
            var property = value.GetType().GetProperty(name, flags);
            return property != null ? property.GetValue(value)! : value.GetType().GetField(name, flags)!.GetValue(value)!;
        }
        object Encounter(string id)
        {
            object script = Read(manager.Running[id], "Script");
            return script.GetType().BaseType!.GetField("_encounter", flags)!.GetValue(script)!;
        }
        object[] Models(string id) => ((IEnumerable)Read(Encounter(id), "HeavenOnHighSpawnAoes")).Cast<object>().ToArray();
        object[] Shown() => previews().Where(p => ((string)Read(p, "Id")).Contains("hoh/spawn/")).ToArray();
        object[] DangerWindows()
        {
            dynamic runtime = runtimeObject;
            return ((IEnumerable)Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), "DangerAreas")).Cast<object>()
                .SelectMany(a => ((IEnumerable)Read(a, "DangerWindows")).Cast<object>()).ToArray();
        }
        foreach (var (floor, territory, oid, spawnOid, count, delay) in new[]
        { (60, 783u, 0x23E4u, 0x23E5u, 11, 7.9), (80, 784u, 0x23ECu, 0x23EDu, 12, 8.5) })
        {
            var id = $"masterplayer.hoh.{floor}";
            var actors = Enumerable.Range(3, count).Select(i => Actor((ulong)i, spawnOid, center + Vector3.UnitX * (i - 3))).ToArray();
            var finishOrEffect = floor == 60 ? 11930u : 11992u;
            load(id, [Header(territory), Frame(territory, 0, actors.Prepend(Actor(1, oid, center))
                    .Append(Actor(100, 0, center + Vector3.UnitZ * 15)).ToArray(), full: true),
                Frame(territory, 2, [Actor(1, oid, center, combat: true)]),
                Frame(territory, 3, [Actor(3, spawnOid, center + Vector3.UnitZ * 20, dead: true)]),
                Frame(territory, 4, [], removed: [3]),
                EffectEvent(4.1, 3, finishOrEffect, 1), Frame(territory, 4.2, []),
                Frame(territory, 5, [Actor(4, spawnOid, center + Vector3.UnitX,
                    cast: Cast(finishOrEffect, 3, center, startedAt: 5))]),
                Frame(territory, 6, [], removed: [4]),
                TreasureNativeEffect(7, 3, finishOrEffect, 2, 0, []), Frame(territory, 8, []),
                Frame(territory, 20, []), Frame(territory, 21, [Actor(1, oid, center, dead: true)]),
                Frame(territory, 22, [])]);
            controller.Seek(1);
            Check(Models(id).Length == 0 && Shown().Length == 0 && dangers() == 0, "HoH spawn precombat activation scope.");
            controller.Seek(2.1);
            Check(Models(id).Length == count && Shown().Length == count && dangers() == count
                && Models(id).All(m => (DateTime)Read(m, "Activation") == Start.AddSeconds(2 + delay)), "HoH spawn capacity/bootstrap clock.");
            Check(DangerWindows().Length == count && DangerWindows().All(w =>
                (DateTime)Read(w, "DangerFrom") == Start.AddSeconds(2 + delay) && Read(w, "DangerUntil") == null),
                "HoH spawn actual navigation activation/end differs from source model.");
            var firstOrigin = (Vector3)Read(Models(id)[0], "Origin");
            controller.Seek(4.3);
            Check(Models(id).Length == count && (Vector3)Read(Models(id)[0], "Origin") == firstOrigin,
                "HoH spawn death/destruction/flattened effect incorrectly consumed or moved prediction.");
            controller.Seek(6.5);
            Check(Models(id).Length == count - (floor == 60 ? 1 : 0),
                "HoH spawn destruction with cast did not distinguish Finish from effect-only.");
            controller.Seek(20);
            var expected = count - 1;
            Check(Models(id).Length == expected && Shown().Length == expected && dangers() == expected
                && Models(id).All(m => (DateTime)Read(m, "Activation") == Start.AddSeconds(2 + delay)),
                "HoH native consumption or original overdue activation lost.");
            if (floor == 80)
                Check(Shown().All(p => (float)Read(Read(p, "Geometry"), "Radius") == 8), "HoH80 exact sphere display radius.");
            controller.Seek(2.1);
            Check(Models(id).Length == count && Shown().Length == count, "HoH spawn reverse Seek not reconstructed.");
            manager.SetMechanismEnabled(id, floor == 60 ? "忍冬" : "锻冶媪", false);
            controller.Seek(2.2);
            Check(Models(id).Length == 0 && dangers() == 0 && Shown().Length == 0, "HoH spawn disabled owner retained.");
            manager.SetMechanismEnabled(id, floor == 60 ? "忍冬" : "锻冶媪", true);
            stop();
            Console.WriteLine($"PASS native HoH{floor} creation bootstrap {count} unlimited locked origin clock death destroy no timeout source Finish versus native effect only dual views navigation reverse Seek owner");
        }
        load("masterplayer.hoh.90", [Header(775), Frame(775, 0, [Actor(1, 0x23EF, center, combat: true),
            Actor(100, 0, center), Actor(3, 0x2413, center), Actor(4, 0x2413, center)], full: true),
            Frame(775, 1, [Actor(3, 0x2413, center, cast: Cast(12210, 3, center, startedAt: 1))]),
            Frame(775, 2, [Actor(3, 0x2413, center)]),
            Frame(775, 3, [Actor(3, 0x2413, center, cast: Cast(12210, 3, center, startedAt: 3))]),
            Frame(775, 4, [Actor(3, 0x2413, center)]),
            Frame(775, 5, [Actor(3, 0x2413, center, cast: Cast(12210, 3, center, startedAt: 5))]),
            Frame(775, 6, [], removed: [3]), Frame(775, 10, [])]);
        controller.Seek(2.5);
        Check(Models("masterplayer.hoh.90").Select(m => (int)Read(m, "Hits")).SequenceEqual([1, 0]), "HoH90 first position match.");
        controller.Seek(4.5);
        Check(Models("masterplayer.hoh.90").Select(m => (int)Read(m, "Hits")).SequenceEqual([2, 0])
            && Models("masterplayer.hoh.90").All(m => (DateTime)Read(m, "Activation") == Start.AddSeconds(3.2)),
            "HoH90 count refreshed source activation.");
        controller.Seek(6.5);
        Check(Models("masterplayer.hoh.90").Length == 0 && dangers() == 0 && Shown().Length == 0,
            "HoH90 destroyed sphere must Finish third hit before destroying next position match.");
        controller.Seek(1.5);
        Check(Models("masterplayer.hoh.90").Length == 2 && dangers() == 2 && Shown().Length == 2, "HoH90 reverse Seek output.");
        stop();
        Console.WriteLine("PASS native HoH90 two identical positions ordered first match three Finish same activation Destroy after final Finish removes next dual views navigation reverse Seek");
        load("masterplayer.hoh.90", [Header(775), Frame(775, 0, [Actor(1, 0x23EF, center, combat: true),
            Actor(100, 0, center), Actor(3, 0x2413, center)], full: true),
            Frame(775, 1, [Actor(3, 0x2413, center, cast: Cast(12210, 3, center, startedAt: 1))]),
            Frame(775, 2, [Actor(3, 0x2413, center)]),
            Frame(775, 3, [Actor(3, 0x2413, center, cast: Cast(12210, 3, center, startedAt: 3))]),
            Frame(775, 4, [Actor(3, 0x2413, center)]),
            Frame(775, 5, [Actor(3, 0x2413, center, cast: Cast(12210, 3, center, startedAt: 5))]),
            Frame(775, 6, [Actor(4, 0x2413, center)], removed: [3]), Frame(775, 10, [])]);
        controller.Seek(6.5);
        Check(Models("masterplayer.hoh.90").Length == 1
            && (ulong)Read(Models("masterplayer.hoh.90").Single(), "ActorId") == 4
            && (int)Read(Models("masterplayer.hoh.90").Single(), "Hits") == 0
            && Math.Abs(((DateTime)Read(Models("masterplayer.hoh.90").Single(), "Activation") - Start.AddSeconds(6).AddSeconds(3.2)).TotalSeconds) < .00001
            && dangers() == 1 && Shown().Length == 1,
            $"HoH90 old destroy/new sphere: models={Models("masterplayer.hoh.90").Length} nav={dangers()} previews={Shown().Length}.");
        stop();
        Console.WriteLine("PASS native HoH90 missing old sphere Finish destroy precedes same-frame new spawn original activation intact");
    }
}
