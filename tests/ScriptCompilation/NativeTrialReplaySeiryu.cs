using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunSeiryu(object managerObject, object runtimeObject, object controllerObject,
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
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("seiryu/" + prefix, StringComparison.Ordinal)).ToArray();
        bool ArenaAt(Vector3 p)
        {
            dynamic arena = Value(Encounter(), "Arena");
            return arena.Contains(p, 0f);
        }
        const string id = "masterplayer.trial.seiryu";
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        load(id,
        [
            Header(824), Frame(824, 0, [Actor(1, 0x25F4, c, combat: true),
                Actor(100, 0, c + Vector3.UnitX * 5), Actor(101, 0, c + Vector3.UnitZ * 10),
                Actor(10, 0x233C, c), Actor(11, 0x233C, c), Actor(12, 0x233C, c), Actor(13, 0x233C, c),
                Actor(20, 0x2786, c - Vector3.UnitZ * 15), Actor(21, 0x2788, c - Vector3.UnitZ * 10),
                Actor(30, 0x2672, c, modelState: 32), Actor(40, 0x1EAA3C, c - Vector3.UnitX * 10)], full: true, party: party),
            Frame(824, .1, [Actor(20, 0x2786, c - Vector3.UnitZ * 15, tethers: [new { Id = 17, TargetId = 100UL }]),
                Actor(21, 0x2788, c - Vector3.UnitZ * 10, tethers: [new { Id = 17, TargetId = 101UL }]),
                Actor(1, 0x25F4, c, combat: true, cast: Cast(14281, 5, c)),
                Actor(10, 0x233C, c, cast: Cast(14349, 7.5, c, rotation: MathF.PI / 2)),
                Actor(11, 0x233C, c, cast: Cast(14344, 4.5, c))], party: party),
            EffectEvent(.2, 12, 14319, 1, target: 101), EffectEvent(.3, 13, 14277, 2, target: 100),
            JsonSerializer.Serialize(new { t = .4, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = 101UL, TargetId = 101UL, MarkerId = 169, Timestamp = Start.AddSeconds(.4) } }),
            JsonSerializer.Serialize(new { t = .5, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = 100UL, TargetId = 100UL, MarkerId = 169, Timestamp = Start.AddSeconds(.5) } }),
            Frame(824, 1, [Actor(30, 0x2672, c, modelState: 32, animationState1: 1)], party: party),
            Frame(824, 1.7, [Actor(31, 0x2672, c, modelState: 33)], party: party),
            Frame(824, 2, [Actor(1, 0x25F4, c, targetable: false), Actor(11, 0x233C, c)], party: party),
            EffectEvent(2.1, 1, 15490, 3, target: 100),
            Frame(824, 3, [Actor(100, 0, c + Vector3.UnitX * 5, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(30)),
                Actor(11, 0x233C, c, cast: Cast(14346, 5, c, startedAt: 3)),
                Actor(20, 0x2786, c - Vector3.UnitZ * 15)], party: party),
            EffectEvent(3.1, 1, 15474, 4, target: 101), EffectEvent(3.2, 20, 15389, 5, target: 100),
            Frame(824, 4, [Actor(10, 0x233C, c), Actor(11, 0x233C, c),
                Actor(12, 0x233C, c, cast: Cast(14855, 3, c, startedAt: 4)),
                Actor(21, 0x2788, c - Vector3.UnitZ * 10)], removed: [40UL], party: party),
            EffectEvent(4.1, 12, 15388, 6, target: 100), EffectEvent(4.2, 10, 14340, 7, target: 101),
            EffectEvent(4.3, 10, 14340, 9, target: 100),
            EffectEvent(5, 10, 14989, 8),
            Frame(824, 6, [Actor(12, 0x233C, c)], party: party),
            Frame(824, 7, [Actor(1, 0x25F4, c, dead: true)], party: party), Frame(824, 8, [], party: party),
        ]);
        controller.Seek(.8);
        Check(!ArenaAt(c + Vector3.UnitX * 19.8f) && !Group("sigil").Any() && Group("tower").Any()
            && Group("bait/15389").Any() && Group("bait/15392").Any()
            && Group("line/15388").Any(a => ((string)Value(a, "Id")).EndsWith("/forbidden"))
            && Group("line/15474").Any() && Group("spread").Any()
            && Group("knockback/14349").Any() && !Group("river/join").Any(),
            "Seiryu initial raw tethers, model baseline, marker stacks, spreads or handprint gate differs.");
        controller.Seek(1.5);
        Check(Group("sigil").Single() is { } sigil && (DateTime)Value(sigil, "Activation") == Start.AddSeconds(6.6),
            "Seiryu model triple transition clock differs.");
        controller.Seek(1.8);
        Check(Group("sigil").Single() is { } bornSigil && (DateTime)Value(bornSigil, "Activation") == Start.AddSeconds(1.7).AddSeconds(5.6),
            "Seiryu new actor's nonzero model did not dispatch a callback.");
        controller.Seek(2.5);
        Check(ArenaAt(c + Vector3.UnitX * 19.8f) && Group("river/join").Any() && Group("line/15474").Any(),
            "Seiryu phase actual finish, Handprint release or first arts hit differs.");
        controller.Seek(3.5);
        Check(!Group("line/15474").Any() && Group("knockback/14349").Any()
            && !Group("knockback/14346").Any() && Group("force/join").Any(),
            "Seiryu second arts resolve or different immunity/custom AI lost.");
        controller.Seek(4.25);
        Check(Group("spread").Count(a => ((string)Value(a, "Id")).EndsWith("/display")) == 1,
            "Seiryu first spread hit erased the second target.");
        controller.Seek(4.5);
        Check(!Group("line/15388").Any() && !Group("spread").Any() && Group("tower").Any() && Group("sigil").Any(),
            "Seiryu corrected line target, spread or tower destroy lifetime differs.");
        controller.Seek(6.5);
        Check(!Group("tower").Any() && !Group("sigil").Any() && dangers() == 0,
            "Seiryu tower fail/model actual finish leaked.");
        controller.Seek(7.5); Check(previews().Length == 0 && dangers() == 0, "Seiryu primary death leaked.");
        controller.Seek(.8); Check(Group("line/15388").Any() && !ArenaAt(c + Vector3.UnitX * 19.8f), "Seiryu reverse Seek failed.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Seiryu disable leaked outputs.");
        stop();
        Console.WriteLine("PASS Seiryu actual DLL replay raw tether batches, first-line forbidden, effect stacks, model triple, birth towers, phase polygon, distinct KB immunity/AI, Seek, death and isolation");
    }
}
