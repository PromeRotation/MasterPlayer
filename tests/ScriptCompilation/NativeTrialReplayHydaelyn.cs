using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHydaelyn(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object Value(object a, string name) => a.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(a)!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("hyda/" + prefix, StringComparison.Ordinal)).ToArray();
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        string Icon(double time) => JsonSerializer.Serialize(new
        {
            t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = 100UL, TargetId = 101UL, MarkerId = 305, Timestamp = Start.AddSeconds(time) }
        });
        const string id = "masterplayer.trial.hydaelyn";
        var c = new Vector3(100, 0, 100);
        object[] party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        var actors = new List<object> { Actor(1, 0x34EA, c, combat: true),
            Actor(10, 0x233C, c), Actor(11, 0x233C, c), Actor(12, 0x233C, c), Actor(13, 0x34F1, c),
            Actor(30, 0x1EB24C, c, rotation: .3f), Actor(31, 0x1EB24D, c, rotation: -.4f) };
        actors.AddRange(Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, c + Vector3.UnitX * i, classJob: i < 2 ? 19u : 24u)));
        actors.AddRange(Enumerable.Range(20, 6).Select(i => Actor((ulong)i, 0x34EC, c)));
        load(id,
        [
            Header(995), Frame(995, 0, actors, full: true, party: party),
            Frame(995, .1, [Actor(10, 0x233C, c, cast: Cast(27737, 5, c, target: 100)),
                Actor(11, 0x233C, c, cast: Cast(27738, 5, c, target: 101)),
                Actor(12, 0x233C, c, cast: Cast(26070, 5, c, target: 100)),
                Actor(13, 0x34F1, c, cast: Cast(26062, 5, c + Vector3.UnitX * 12, rotation: MathF.PI))], party: party),
            Icon(.2), StatusEvent(.25, 1, 2273, 0x1B4, true), EffectEvent(.3, 11, 26058, 1),
            Frame(995, .4, [Actor(11, 0x233C, c + Vector3.UnitX * 4, rotation: MathF.PI / 2,
                cast: Cast(27738, 5, c, target: 101))], party: party),
            Frame(995, .7, [], removed: [20, 21, 22, 23, 24, 25], party: party),
            EffectEvent(1, 10, 26065, 2), EffectEvent(1.2, 10, 26065, 3),
            EffectEvent(1.4, 10, 26065, 4), EffectEvent(1.6, 10, 26065, 5),
            EffectEvent(2, 10, 26065, 6),
            Frame(995, 2.2, [Actor(100, 0, c, classJob: 19, dead: true)], party: party),
            Frame(995, 2.4, [Actor(100, 0, c, classJob: 19)], removed: [11], party: party),
            StatusEvent(2.6, 1, 2273, 0, false),
            Frame(995, 3, [Actor(11, 0x233C, c - Vector3.UnitX * 8)], party: party),
            EffectEvent(3.1, 11, 26058, 7),
            Frame(995, 4, [Actor(10, 0x233C, c, cast: Cast(26061, 1, c, startedAt: 4)),
                Actor(12, 0x233C, c), Actor(13, 0x34F1, c)], party: party),
            Frame(995, 5.5, [Actor(10, 0x233C, c)], party: party),
            EffectEvent(6, 10, 26255, 8), EffectEvent(6.1, 10, 26066, 9),
            Frame(995, 8, [Actor(1, 0x34EA, c, dead: true)], party: party),
            Frame(995, 9, [], party: party)
        ]);
        controller.Seek(.5);
        Check(Group("parhelic").Length == 10 && Group("weapon").Length == 1 && Group("wave").Length == 1
            && Group("stack/26065").Single(a => (bool)Value(a, "Display")) is { } echo
            && (Vector3)Value(echo, "Position") is { } p && Math.Abs(p.X - 100) < .04
            && Group("shared").Any() && Group("charge").Length == 1 && dangers() > 10,
            "Hydaelyn native actor/icon/status/cast/effect outputs missing.");
        controller.Seek(1.8);
        Check(Group("stack/26065").Any() && Damage().Count(a => (string)Value(a, "DamageType") == "Raidwide") == 8,
            "Hydaelyn fourth echo or sixth crystal destruction differs.");
        controller.Seek(2.3);
        Check(!Group("stack/26065").Any() && Group("parhelic").Length == 10 && Group("wave").Length == 1,
            "Hydaelyn fifth echo or personal death lost encounter history.");
        controller.Seek(3.2);
        Check(Group("wave").Length == 2 && Group("weapon").Single() is { } cross
            && Value(cross, "Shape").GetType().Name == "DungeonCrossTemplate"
            && !Group("spread").Any(), "Hydaelyn destroyed wave generation or weapon loss differs.");
        controller.Seek(5.6);
        Check(!Group("parhelic").Any() && Group("wave").Length == 2 && !Group("shared").Any()
            && !Group("charge").Any(), "Hydaelyn observed cast endings failed independent cleanup.");
        controller.Seek(6.5);
        Check(!Group("weapon").Any() && Damage().Length == 0 && Group("wave").Length == 2, "Hydaelyn effect-ended predictions leaked.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
            "Hydaelyn replay executed PR action controls.");
        controller.Seek(8.5); Check(previews().Length == 0 && dangers() == 0, "Hydaelyn phase death leaked.");
        controller.Seek(.5); Check(Group("parhelic").Length == 10 && Group("wave").Length == 1, "Hydaelyn reverse Seek failed.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Hydaelyn disabled script retained boundary or outputs.");
        stop();
        Console.WriteLine("PASS Hydaelyn actual DLL replay exact arena, parhelic, weapons, five-hit echoes, crystal destruction, persistent wave generations, tank roles, independent endings, death, Seek and isolation");
    }
}
