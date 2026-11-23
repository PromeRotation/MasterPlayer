using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunZoraalJa(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Encounter()
        {
            var context = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = context.GetType().GetProperty("Script")!.GetValue(context)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("zoraal/" + prefix)).ToArray();
        string Map(double time, uint index, uint state) => JsonSerializer.Serialize(new
        {
            t = time, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(time) }
        });
        string Icon(double time, ulong actor) => JsonSerializer.Serialize(new
        {
            t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = actor, TargetId = actor, MarkerId = 376u, Timestamp = Start.AddSeconds(time) }
        });
        string Cancel(double time, ulong actor, uint action) => JsonSerializer.Serialize(new
        {
            t = time, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = actor, ActionId = action, Timestamp = Start.AddSeconds(time) }
        });
        string Tether(double time, ulong actor) => JsonSerializer.Serialize(new
        {
            t = time, kind = "Tether", type = "PromeRotation.LogSystem.LogSystemTetherEvent",
            data = new { SourceId = actor, TargetId = 100UL, TetherId = 999u, Timestamp = Start.AddSeconds(time) }
        });
        string Director(double time, uint command) => JsonSerializer.Serialize(new
        {
            t = time, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
            data = new { Command = command, Timestamp = Start.AddSeconds(time) }
        });
        const string id = "masterplayer.trial.zoraal-ja";
        var c = new Vector3(100, 0, 100);
        object[] party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        var actors = new List<object> { Actor(1, 0x42A9, c, combat: true) };
        actors.AddRange(Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, c + Vector3.UnitX * i, classJob: 19)));
        actors.AddRange(Enumerable.Range(10, 6).Select(i => Actor((ulong)i, 0x233C, c)));
        load(id,
        [
            Header(1200), Frame(1200, 0, actors, full: true, party: party),
            Frame(1200, .1, [Actor(10, 0x233C, c, cast: Cast(37714, 20, c)),
                Actor(11, 0x233C, c, cast: Cast(37714, 20, c, MathF.PI))], party: party),
            CastEvent(.2, 10, 37714, 19.9, c, .6f),
            EffectEvent(.3, 10, 37714, 1), Cancel(.4, 11, 37714), Cancel(.5, 10, 37714),
            Frame(1200, 1, [Actor(1, 0x42A9, c, combat: true, targetable: false),
                Actor(10, 0x233C, c), Actor(11, 0x233C, c)], party: party),
            Frame(1200, 1.5, [Actor(2, 0x42B4, c, combat: true)], party: party),
            Frame(1200, 2, [Actor(10, 0x233C, c, cast: Cast(37738, 10, c, startedAt: 2)),
                Actor(11, 0x42B6, new(130, 0, 130), cast: Cast(37720, 10, new(121.2132f, 0, 121.2132f), startedAt: 2))], party: party),
            Map(2.2, 0x20, 0x00020001),
            Frame(1200, 2.25, [Actor(12, 0x233C, c, tethers: [new { Id = 999u, TargetId = 100UL }])], party: party),
            Tether(2.3, 12), Icon(2.4, 101), EffectEvent(2.5, 13, 35567, 1, target: 102),
            Cancel(3, 11, 37720), Map(3.1, 0x20, 0x00080004), EffectEvent(3.2, 12, 37730, 1),
            EffectEvent(3.3, 13, 37752, 2, target: 101),
            EffectEvent(3.4, 13, 37750, 3), EffectEvent(3.5, 13, 37750, 4),
            EffectEvent(3.6, 13, 37750, 5), EffectEvent(3.7, 13, 37750, 6),
            Frame(1200, 4, [Actor(100, 0, c, classJob: 19, dead: true)], party: party),
            Frame(1200, 5, [Actor(2, 0x42B4, c, combat: true, dead: true)], party: party),
            Frame(1200, 6, [Actor(3, 0x42B4, c, combat: true), Actor(100, 0, c, classJob: 19),
                Actor(12, 0x233C, c)], removed: [2], party: party),
            Map(6.6, 0x20, 0x00080004), Tether(6.7, 14), Director(6.8, 0x40000005),
            Frame(1200, 7, [], party: party), Director(7.1, 0x40000011),
            Frame(1200, 7.2, [], party: party), Frame(1200, 8, [], party: party)
        ]);
        controller.Seek(.35);
        Check(Group("sword").Length == 2 && Group("sword").Count(a => (bool)Value(a, "Navigate")) == 1
            && MathF.Abs((float)Value(Group("sword")[0], "Rotation") - .6f) < .0001f
            && dangers() == 1 && previews().Length > 1, "ZoraalJa packet correction/risky display/effect isolation.");
        controller.Seek(.45); Check(Group("sword").Length == 1
            && (float)Value(Group("sword")[0], "Rotation") == MathF.PI, "ZoraalJa FIFO finish.");
        controller.Seek(1.2); Check(dangers() == 0 && previews().Length == 0, "P1 untargetable retained output.");
        controller.Seek(2.6);
        Check((uint)Value(Value(Encounter(), "Active"), "BossOid") == 0x42B4
            && !Group("half").Any() && Group("chasm").Length == 1 && Group("transition").Any()
            && Group("track").Length == 1 && Group("spread").Any() && Group("stack").Any()
            && dangers() > 0 && previews().Length > 0, "P2 handoff/projection/independent outputs.");
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(boundary.Contains(new Vector3(126, 0, 100)), "Predicted shrink changed boundary early.");
        controller.Seek(3.25); boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!boundary.Contains(new Vector3(120, 0, 100)) && !Group("chasm").Any() && Group("half").Length == 1
            && !Group("transition").Any() && !Group("track").Any(), "Actual map/finish/effect endpoints.");
        controller.Seek(3.65); Check(Group("stack").Any(), "Four-hit stack ended after only three.");
        controller.Seek(4.2); Check(!Group("stack").Any() && !Group("spread").Any()
            && Group("half").Length == 1, "Four-hit/target spread/personal death.");
        controller.Seek(5.5); Check(dangers() == 0 && previews().Length == 0, "P2 death output leak.");
        controller.Seek(6.5); boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(boundary.Contains(new Vector3(126, 0, 100)) && !Group("track").Any(), "Checkpoint did not start fresh.");
        controller.Seek(7.05); Check(dangers() == 0 && previews().Length == 0 && Value(Encounter(), "Active") == null,
            "Director wipe with living boss restored stale mechanics.");
        controller.Seek(7.3); boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(boundary.Contains(new Vector3(126, 0, 100)) && Group("track").Length == 0, "Recommence retained shrink or tether.");
        controller.Seek(2.6); Check(Group("track").Length == 1 && Group("chasm").Length == 1, "Reverse Seek.");
        manager.SetScriptEnabled(id, false); Check(previews().Length == 0 && dangers() == 0, "Disabled owner leak.");
        stop();
        Console.WriteLine("PASS ZoraalJa actual DLL two phases/checkpoint, packet correction, FIFO risky versus display, square shrink, projected chasm and hidden half, tracks, spread target, four-hit stack, personal death, Seek and cleanup");
    }
}
