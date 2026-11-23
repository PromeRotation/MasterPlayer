using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunYojimbo(object managerObject, object runtimeObject, object controllerObject,
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
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("yojimbo/" + prefix, StringComparison.Ordinal)).ToArray();
        string Icon(double t, ulong actor, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = actor, TargetId = actor, MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        const string id = "masterplayer.trial.yojimbo";
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        load(id,
        [
            Header(806), Frame(806, 0, [Actor(1, 0x25FD, c, combat: true), Actor(2, 0x25FF, c),
                Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX), Actor(10, 0x233C, c),
                Actor(11, 0x233C, c), Actor(20, 0x25FE, c), Actor(21, 0x2603, c + Vector3.UnitX * 10),
                Actor(22, 0x2601, c - Vector3.UnitZ * 20), Actor(23, 0x2602, c)], full: true, party: party),
            Icon(.1, 100, 144), Icon(.1, 101, 144), Icon(.1, 100, 55), Icon(.1, 101, 55), Icon(.1, 101, 87),
            Frame(806, .2, [Actor(100, 0, c, tethers: [new { Id = 57, TargetId = 1UL }, new { Id = 81, TargetId = 1UL }]),
                Actor(10, 0x233C, c, cast: Cast(14388, 4, c, startedAt: .2))], party: party),
            Frame(806, 1, [Actor(1, 0x25FD, c, rotation: MathF.PI / 2, cast: Cast(14405, 10, c, startedAt: 1)),
                Actor(11, 0x233C, c, cast: Cast(14386, 2, c, startedAt: 1))], party: party),
            Frame(806, 2, [Actor(11, 0x233C, c, cast: Cast(14409, 5, c, startedAt: 2))], party: party),
            Frame(806, 3, [Actor(10, 0x233C, c), Actor(20, 0x25FE, c, cast: Cast(14378, 1, c, startedAt: 3))], party: party),
            Frame(806, 4, [Actor(20, 0x25FE, c), Actor(21, 0x2603, c, cast: Cast(14400, 1, c, startedAt: 4))], party: party),
            EffectEvent(4.1, 2, 14398, 1), Frame(806, 5, [Actor(21, 0x2603, c)], party: party),
            Frame(806, 5.7, [], party: party), Frame(806, 6, [], party: party),
            Frame(806, 7, [Actor(11, 0x233C, c)], party: party),
            EffectEvent(7.1, 2, 14387, 2),
            Frame(806, 7.2, [Actor(100, 0, c), Actor(23, 0x2602, c, dead: true)], party: party),
            Frame(806, 8, [Actor(1, 0x25FD, c, dead: true)], party: party),
            Frame(806, 9, [], party: party),
        ]);
        controller.Seek(.5);
        Check(Group("birth").Length == 2 && Group("dragon").Length == 20 && Group("kasha").Length == 1
            && Group("gekko").Any() && Group("tiny").Any() && Group("jump").Any()
            && Group("enchain").Any() && Group("chain").Length == 2, "Yojimbo replay birth/icons/tethers missing.");
        controller.Seek(2.5);
        Check(Group("storm").Length == 3 && Group("gekko").All(a => !(bool)Value(a, "Navigate")),
            "Yojimbo locked storm sides or Yukikaze gate missing.");
        controller.Seek(3.5);
        Check(Group("birth").Length == 2 && !Group("kasha").Any(), "Yojimbo Kasha finish crossed birth components.");
        controller.Seek(4.5);
        Check(Group("birth").Length == 1 && !Group("jump").Any() && Group("tiny").Any(),
            "Yojimbo component-wide actual finish/jump effect crossed unrelated output.");
        controller.Seek(5.8);
        Check(!Group("birth").Any() && !Group("tiny").Any() && Group("dragon").Length == 19,
            "Yojimbo clock expiry or first dragon step differs.");
        controller.Seek(6.1);
        Check(Group("storm").Length == 2, "Yojimbo center fixed deadline erased sides.");
        controller.Seek(7.5);
        Check(!Group("storm").Any() && !Group("gekko").Any() && !Group("enchain").Any() && !Group("chain").Any(),
            "Yojimbo side finish, Gekko effect or untether cleanup leaked.");
        controller.Seek(8.5); Check(previews().Length == 0 && dangers() == 0, "Yojimbo death leaked outputs.");
        controller.Seek(.5);
        Check(Group("dragon").Length == 20 && Group("kasha").Length == 1 && Group("jump").Any(),
            "Yojimbo reverse Seek lost component history.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Yojimbo disable leaked outputs.");
        stop();
        Console.WriteLine("PASS Yojimbo actual DLL replay birth clocks, twenty-step dragon, independent finish, icons, storm sides, chain navigation, Seek, death and isolation");
    }
}
