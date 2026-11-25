using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTisiphone(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Encounter()
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(r => Value(r, "Script").GetType().BaseType?.Name == "DungeonScript"), "Script");
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        string[] Hints()
        {
            var encounter = Encounter();
            return ((IEnumerable<string>)encounter.GetType().GetMethod("Hints")!.Invoke(encounter, [new Vector3(-300, 0, -236)])!).ToArray();
        }
        ulong Target()
        {
            var encounter = Encounter();
            return (ulong)encounter.GetType().GetMethod("PriorityTarget")!.Invoke(encounter, [2ul, new Vector3(-300, 0, -236)])!;
        }
        object[] Areas() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().ToArray();
        DateTime LatestActivation() => Areas().SelectMany(a => ((IEnumerable)Value(a, "DangerWindows")).Cast<object>())
            .Select(w => (DateTime)Value(w, "DangerFrom")).Max();
        var center = new Vector3(-300, 0, -236);
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var records = new List<string>
        {
            Header(602), Frame(602, 0, [Actor(1, 0x181C, center), Actor(100, 0, center, classJob: 25),
                Actor(2, 0x18ED, center + Vector3.UnitX * 4), Actor(3, 0x18EE, center + Vector3.UnitX * 8, targetable: false),
                Actor(4, 0x18EB, center)], full: true, party: party),
            Frame(602, 1, [Actor(1, 0x181C, center, combat: true, targetId: 100)], party: party),
            Frame(602, 3, [Actor(1, 0x181C, center), Actor(2, 0x18ED, center + Vector3.UnitX * 6, dead: true, targetable: false)], party: party),
            JsonSerializer.Serialize(new { t = 3.1, kind = "BattleEnded", type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent",
                data = new { Timestamp = Start.AddSeconds(3.1) } }),
            Frame(602, 4, [Actor(100, 0, center, classJob: 22), Actor(2, 0x18ED, center + Vector3.UnitX * 6)], party: party),
            Frame(602, 5, [Actor(100, 0, center, classJob: 24)], party: party),
            Frame(602, 6, [], removed: [2], party: party),
        };
        uint[] actions = [7153, 7108, 7112, 7110, 7148, 7113, 7177, 7150, 7109];
        for (var i = 0; i < actions.Length; ++i)
        {
            var time = 10 + i * 6;
            records.Add(Frame(602, time, [Actor(4, 0x18EB, center,
                cast: Cast(actions[i], 3, center, MathF.PI / 2, target: 3, startedAt: time))], party: party));
            records.Add(CastEvent(time, 4, actions[i], 3, center, MathF.PI / 2));
            records.Add(Frame(602, time + 4, [Actor(4, 0x18EB, center)], party: party));
        }
        records.Add(Frame(602, 65, [Actor(1, 0x181C, center, dead: true)], party: party));
        records.Add(Frame(602, 66, [], party: party));
        load("masterplayer.palace.tisiphone", records);
        controller.Seek(.5);
        Check(Hints().Any(h => h.Contains("4 zombies")) && dangers() == 0, "Tisiphone constructor hint before combat.");
        manager.SetStatus(Hints().Single(h => h.Contains("4 zombies")));
        var precombatScript = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(r => Value(r, "Script").GetType().BaseType?.Name == "DungeonScript"), "Script");
        controller.Play(); controller.Update(1f); controller.Pause();
        Check((string)manager.StatusMessage == ""
            && ReferenceEquals(precombatScript, Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(r => Value(r, "Script").GetType().BaseType?.Name == "DungeonScript"), "Script")),
            "Tisiphone phase entry failed to clear its published precombat status in the same script instance.");
        controller.Seek(1.5);
        Check(dangers() == 2 && previews().Length == 2 && Target() == 1 && !Hints().Any(h => h.Contains("4 zombies")),
            "Tisiphone ranged priority, zombie navigation-only zones or phase hint removal.");
        Check(LatestActivation() == Start.AddSeconds(6.5), "Tisiphone first rolling +5s activation.");
        controller.Seek(3.5);
        Check(dangers() == 2 && previews().Length == 1 && LatestActivation() == Start.AddSeconds(8.5),
            "Tisiphone rolling clock, dead zombie retention or BattleEnded continuity.");
        controller.Seek(4.5); Check(dangers() == 0 && Target() == 2, "Tisiphone melee perspective did not switch priority/zones.");
        controller.Seek(5.5); Check(dangers() == 2 && Target() == 1, "Tisiphone healer classification.");
        controller.Seek(6.5); Check(dangers() == 0, "Tisiphone destroyed zombie zones leaked.");
        for (var i = 0; i < actions.Length; ++i)
        {
            controller.Seek(10.5 + i * 6);
            var noGeometry = actions[i] is 7153 or 7110;
            Check(dangers() == (noGeometry ? 0 : 1), $"Tisiphone action {actions[i]} output.");
            if (actions[i] == 7153) Check(Hints().Any(h => h.Contains("Heavy Raidwide")), "Blood Rain warning.");
            if (actions[i] == 7110) Check(Hints().Any(h => h.Contains("life stealing")), "Fatal Allure warning.");
            controller.Seek(14.1 + i * 6);
            Check(dangers() == 0, $"Tisiphone action {actions[i]} finish.");
        }
        controller.Seek(65.1); Check(previews().Length == 0 && dangers() == 0, "Tisiphone death leaked output.");
        controller.Seek(3.5); Check(dangers() == 2, "Tisiphone reverse Seek reconstruction.");
        stop();
        Console.WriteLine("PASS production Tisiphone: precombat hint, nine cast components, exact arena, ranged/healer/melee priority, rolling zombie nav-only zones, dead/untargetable retention, BattleEnded, Seek and cleanup");
    }
}
