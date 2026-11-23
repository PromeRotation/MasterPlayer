using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunEnuo(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(a)!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("enuo/" + prefix, StringComparison.Ordinal)).ToArray();
        string Icon(double time) => JsonSerializer.Serialize(new
        {
            t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = 1UL, TargetId = 102UL, MarkerId = 719, Timestamp = Start.AddSeconds(time) }
        });
        string Tether(double time) => JsonSerializer.Serialize(new
        {
            t = time, kind = "Tether", type = "PromeRotation.LogSystem.LogSystemTetherEvent",
            data = new { SourceId = 12UL, TargetId = 100UL, TetherId = 404, Timestamp = Start.AddSeconds(time) }
        });
        string Map(double time, uint state) => JsonSerializer.Serialize(new
        {
            t = time, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = 0, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(time) }
        });
        const string id = "masterplayer.trial.enuo";
        var c = new Vector3(100, 0, 100);
        object[] party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        var actors = new List<object> { Actor(1, 0x4DB9, c, combat: true), Actor(30, 0x4DBF, c + Vector3.UnitZ * 5, hitboxRadius: 2) };
        actors.AddRange(Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, c + Vector3.UnitX * i, classJob: i < 2 ? 19u : 20u)));
        actors.AddRange(Enumerable.Range(10, 20).Select(i => Actor((ulong)i, 0x233C, c + Vector3.UnitX * (i - 10))));
        load(id,
        [
            Header(1361), Frame(1361, 0, actors, full: true, party: party),
            Frame(1361, .1, [
                Actor(10, 0x233C, c, cast: Cast(49966, 5, c, target: 101)),
                Actor(11, 0x233C, c, cast: Cast(49959, 6, c)),
                Actor(13, 0x233C, c, cast: Cast(49968, 7, c, target: 103)),
                Actor(14, 0x233C, c, cast: Cast(49964, 7, c, target: 104)),
                Actor(15, 0x233C, c, cast: Cast(49946, 8, c)),
                Actor(20, 0x233C, c, cast: Cast(49952, 8, c)),
                Actor(21, 0x233C, c, cast: Cast(49953, 8, c))
            ], party: party),
            Tether(.2), Icon(.3), Map(1, 0x00020001),
            EffectEvent(2, 29, 49940, 1, new Vector3(102, 0, 100)),
            Frame(1361, 5.5, [Actor(10, 0x233C, c)], party: party),
            EffectEvent(5.7, 10, 49966, 2, target: 101),
            EffectEvent(5.8, 1, 49969, 3, target: 105),
            Frame(1361, 6.5, [Actor(11, 0x233C, c)], party: party),
            Frame(1361, 7.5, [Actor(13, 0x233C, c), Actor(14, 0x233C, c)], party: party),
            Map(8, 0x00080004),
            Frame(1361, 8.5, [Actor(15, 0x233C, c), Actor(20, 0x233C, c), Actor(21, 0x233C, c),
                Actor(31, 0x4EB4, c)], party: party),
            Frame(1361, 9, [], removed: [31], party: party),
            Frame(1361, 10, [Actor(100, 0, c, classJob: 19, dead: true), Actor(16, 0x233C, c, cast: Cast(49934, 4, c))], party: party),
            Frame(1361, 12, [Actor(1, 0x4DB9, c, dead: true)], party: party), Frame(1361, 13, [], party: party)
        ]);
        controller.Seek(1.3);
        Check(Group("freeze").Any() && Group("los").Any() && Group("chase").Any() && Group("line").Any()
            && Group("stack").Any() && Group("spread").Any() && Group("cone").Length == 2
            && previews().Length > 0 && dangers() > 0, "Enuo actual DLL event inputs/initial outputs.");
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(boundary.Contains(new Vector3(170, 0, 170)), "Enuo intermission map.");
        controller.Seek(2.3);
        Check(Group("chase").Any(a => ((string)Value(a, "Id")).EndsWith("/avoid")
            && (DateTime?)Value(a, "Activation") > Start.AddSeconds(2.5)), "Enuo actual effect did not advance chase.");
        controller.Seek(5.6); Check(Group("freeze").Any(), "Enuo finish incorrectly consumed effect-ended bait.");
        controller.Seek(6.7);
        Check(!Group("freeze").Any() && !Group("line").Any() && !Group("los").Any()
            && Group("stack").Any() && Group("chase").Any(), "Enuo separate effect/cast lifetimes.");
        controller.Seek(9.2);
        Check(!Group("chase").Any() && !Group("stack").Any() && !Group("spread").Any() && !Group("cone").Any(), "Enuo chaser destruction/cast endings.");
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!boundary.Contains(new Vector3(121, 0, 100)), "Enuo circle map restore.");
        controller.Seek(10.5); Check(previews().Length > 0, "Enuo personal death erased fight.");
        controller.Seek(12.5); Check(previews().Length == 0 && dangers() == 0, "Enuo primary death cleanup.");
        controller.Seek(1.3); Check(Group("los").Any() && Group("chase").Any(), "Enuo reverse Seek reconstruction.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Enuo disabled owner leaked outputs.");
        stop();
        Console.WriteLine("PASS Enuo actual DLL replay chase tethers and actual effects, effect-ended baits, LOS snapshots, icon line, independent party casts, map arenas, death, Seek and isolation");
    }
}
