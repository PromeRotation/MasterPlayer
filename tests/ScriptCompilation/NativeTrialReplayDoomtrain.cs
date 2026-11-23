using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDoomtrain(object managerObject, object runtimeObject, object controllerObject,
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
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("doom/" + prefix)).ToArray();
        int Stage() => (int)Encounter().GetType().GetField("doomStage", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Encounter())!;
        int Portals() => ((IEnumerable)runtime.Teleporters).Cast<object>().Count();
        string Cancel(double time, ulong source, uint action) => JsonSerializer.Serialize(new
        {
            t = time, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(time) }
        });
        string Icon(double time, ulong source, ulong target, uint icon) => JsonSerializer.Serialize(new
        {
            t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, TargetId = target, MarkerId = icon, Timestamp = Start.AddSeconds(time) }
        });
        string Director(double time, uint command) => JsonSerializer.Serialize(new
        {
            t = time, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
            data = new { Command = command, Timestamp = Start.AddSeconds(time) }
        });
        const uint territory = 1307;
        const string id = "masterplayer.trial.doomtrain";
        var c = new Vector3(100, 0, 100);
        object[] party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        var actors = new List<object> { Actor(1, 0x4A30, c, combat: true) };
        actors.AddRange(Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, c + Vector3.UnitX * i, classJob: 19)));
        actors.AddRange(Enumerable.Range(10, 10).Select(i => Actor((ulong)i, 0x233C, c)));
        var lines = new List<string> { Header(territory), Frame(territory, 0, actors, full: true, party: party),
            Frame(territory, .5, [Actor(20, 0x4A31, new(94, 0, 85))], party: party) };
        for (var car = 1; car <= 3; ++car)
        {
            lines.Add(Frame(territory, car, [Actor(1, 0x4A30, c, combat: true, cast: Cast(45623, 5, c, startedAt: car)),
                Actor(10, 0x233C, c, cast: Cast(45624, 5.9, c, startedAt: car))], party: party));
            lines.Add(EffectEvent(car + .2, 10, 45624, (uint)car));
            lines.Add(Frame(territory, car + .3, [Actor(1, 0x4A30, c, combat: true, targetable: false),
                Actor(10, 0x233C, c), Actor(100, 0, new(100, 0, 100 + car * 50), classJob: 19)], party: party));
            if (car == 2)
            {
                lines.Add(Frame(territory, 2.4, [Actor(11, 0x233C, c, cast: Cast(45635, 7, c, startedAt: 2.4)),
                    Actor(12, 0x233C, c, cast: Cast(45637, 7, c, startedAt: 2.4))], party: party));
                lines.Add(Cancel(2.5, 11, 45635));
                lines.Add(Frame(territory, 2.6, [Actor(11, 0x233C, c), Actor(12, 0x233C, c)], party: party));
            }
        }
        lines.AddRange(
        [
            Frame(territory, 3.4, [Actor(11, 0x233C, c, cast: Cast(45637, 7, c, startedAt: 3.4))], party: party),
            Frame(territory, 4, [Actor(1, 0x4A30, c, combat: true, cast: Cast(45638, 5, c, startedAt: 4))], party: party),
            EffectEvent(4.2, 1, 45638, 4),
            Frame(territory, 4.3, [Actor(1, 0x4A30, c, combat: true, targetable: false), Actor(11, 0x233C, c)], party: party),
            Frame(territory, 4.4, [Actor(40, 0x4B80, new(-400, 0, -425)), Actor(41, 0x4B80, new(-425, 0, -400)),
                Actor(30, 0x4A34, new(-400, 0, -400), combat: true), Actor(100, 0, new(-400, 0, -400), classJob: 19)], party: party),
            Icon(4.5, 40, 100, 642), StatusEvent(4.6, 41, 4176, 0, true),
            Frame(territory, 4.7, [Actor(41, 0x4B80, new(-425, 0, -400), stackedStatus: 4176, statusParam: 0)], party: party),
            EffectEvent(4.8, 40, 45640, 5),
            Frame(territory, 5, [Actor(30, 0x4A34, new(-400, 0, -400), dead: true)], party: party),
            EffectEvent(5.2, 10, 45645, 6), EffectEvent(5.4, 1, 45638, 7),
            Frame(territory, 5.5, [Actor(50, 0x4A36, new(105, 0, 250)),
                Actor(100, 0, new(100, 0, 250), classJob: 19)], party: party),
            Frame(territory, 5.6, [Actor(10, 0x233C, c, cast: Cast(45654, 10, c, startedAt: 5.6)),
                Actor(11, 0x233C, c, cast: Cast(45649, 10, c, target: 100, startedAt: 5.6)),
                Actor(12, 0x233C, new(100, 0, 240), cast: Cast(45659, 3.2, new(100, 0, 240), startedAt: 5.6))], party: party),
            Icon(5.7, 1, 101, 343), Cancel(5.8, 10, 45654),
            Frame(territory, 6, [Actor(10, 0x233C, c), Actor(1, 0x4A30, c, combat: true),
                Actor(100, 0, new(92.4f, 4, 310), classJob: 19)], party: party),
            Frame(territory, 6.1, [Actor(13, 0x233C, c, cast: Cast(45667, 7, c, startedAt: 6.1)),
                Actor(14, 0x233C, new(100, 0, 290), cast: Cast(45627, 7, new(100, 0, 290), startedAt: 6.1))], party: party),
            EffectEvent(6.2, 15, 45661, 8), Cancel(6.3, 11, 45649),
            Frame(territory, 6.4, [Actor(11, 0x233C, c), Actor(100, 0, new(92.4f, 4, 310), classJob: 19, dead: true)], party: party),
            Director(7, 0x40000005), Frame(territory, 7.1, [], party: party),
            Director(7.2, 0x40000011),
            Frame(territory, 7.3, [Actor(100, 0, c, classJob: 19), Actor(1, 0x4A30, c, combat: true),
                Actor(13, 0x233C, c), Actor(14, 0x233C, c)], removed: [20, 30, 40, 41, 50], party: party),
            Frame(territory, 8, [], party: party),
        ]);
        load(id, lines);
        controller.Seek(1.35); dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(Stage() == 4 && !boundary.Contains(new Vector3(102.5f, 0, 147.5f)) && Group("levin").Length == 1, "Car2 transition/subtraction/replayed signals.");
        controller.Seek(2.45); Check(Portals() == 2 && Group("HeadOn").Length == 2 && Group("obstacle").Length == 4
            && previews().Length > 0 && dangers() > 0, "Car3 portals/height/dual-view output.");
        controller.Seek(2.55); Check(!Group("HeadOn").Any(), "Either HeadOn finish clears batch.");
        controller.Seek(3.45);
        var head = Group("HeadOn").Single();
        var headShape = Value(head, "Shape");
        var geometry = headShape.GetType().GetMethod("Resolve")!.Invoke(headShape, [Vector3.Zero, 0f, null])!;
        bool Contains(Vector3 point) => (bool)geometry.GetType().GetMethod("Contains")!.Invoke(geometry, [point, .001f])!;
        Check(Contains(new(92.5f, 0, 255)) && !Contains(new(92.5f, 0, 205)), "Car4 normalized origin.");
        controller.Seek(4.65); boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(Stage() == 15 && boundary.Contains(new Vector3(-400, 0, -400)) && boundary.Contains(new Vector3(-375, 0, -400))
            && !boundary.Contains(new Vector3(-380, 0, -400)) && (Vector3)Value(Group("ray").First(), "Position") == new Vector3(-425, 0, -400), "Intermission topology/icon/Stop.");
        controller.Seek(5.25); Check(Stage() == 20 && !Group("ray").Any(), "New P2 component counter or wrong raidwide action.");
        controller.Seek(5.75); boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(Stage() == 23 && boundary.Position.Z == 250 && Group("Derail").Any() && Group("arcane").Any()
            && Group("stack").Any() && Group("tank").Any(), "P2 component activation before actual derail.");
        controller.Seek(6.15); boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(boundary.Position.Z == 300 && Portals() == 2 && !Group("Derail").Any() && Group("obstacle").Length == 2
            && Group("windpipe").Length == 1 && Group("cast").Any(a => !(bool)Value(a, "Navigate")), "Car5 finish/upper obstacles/current windpipe/risky threshold.");
        controller.Seek(6.5); Check(Stage() == 23 && !Group("tank").Any() && !Group("stack").Any(), "Icon effect/stack finish/personal death.");
        controller.Seek(7.15); Check(Portals() == 0 && previews().Length == 0 && dangers() == 0, "Wipe owner cleanup.");
        controller.Seek(7.5); boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(Stage() == 0 && boundary.Position.Z == 100 && Portals() == 0, "Recommence fresh car.");
        controller.Seek(2.45); Check(Stage() == 8 && Portals() == 2 && Group("HeadOn").Length == 2, "Reverse Seek state rebuild.");
        manager.SetScriptEnabled(id, false);
        Check(Portals() == 0 && dangers() == 0 && previews().Length == 0, "Disabled output owner leak.");
        stop();
        Console.WriteLine("PASS Doomtrain actual DLL five-car state transitions, real effects and finishes, component reactivation, exact disconnected intermission, moving baits and Stop status, height obstacles, directed portals, risky displays, wipe, Seek and owner cleanup");
    }
}
