using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDivineIntervention(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Encounter()
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        var center = Vector3.Zero;
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var records = new List<string>
        {
            Header(456),
            Frame(456, 0, [Actor(1, 0x1010, center), Actor(2, 0x1011, center, combat: true), Actor(100, 0, center),
                Actor(3, 0x102C, Vector3.UnitX * 4), Actor(4, 0xEC4, center)], full: true, party: party),
            Frame(456, 1, [Actor(1, 0x1010, center, combat: true)], party: party),
            Frame(456, 2, [Actor(1, 0x1010, center, combat: true, cast: Cast(1003, 3.5, center, startedAt: 2)),
                Actor(2, 0x1011, center, cast: Cast(1316, 3, center, startedAt: 2))], party: party),
            CastEvent(2, 1, 1003, 3.5, center), CastEvent(2, 2, 1316, 3, center),
            Frame(456, 3, [Actor(1, 0x1010, center, dead: true)], party: party),
            JsonSerializer.Serialize(new { t = 3.1, kind = "BattleEnded", type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent",
                data = new { Timestamp = Start.AddSeconds(3.1) } }),
            Frame(456, 4, [], removed: [1], party: party),
            Frame(456, 6, [Actor(2, 0x1011, center)], party: party),
        };
        uint[] actions = [3993, 720, 1135, 866, 3992];
        foreach (var (action, index) in actions.Select((a, i) => (a, i)))
        {
            var time = 10 + index * 6;
            var location = action == 3993 ? Vector3.UnitX * 10 : center;
            records.Add(Frame(456, time, [Actor(4, 0xEC4, center,
                cast: Cast(action, 2, location, startedAt: time))], party: party));
            records.Add(CastEvent(time, 4, action, 2, location));
            records.Add(Frame(456, time + 1, [Actor(4, 0xEC4, Vector3.UnitZ * 10,
                cast: Cast(action, 2, location, startedAt: time))], party: party));
            records.Add(Frame(456, time + 4, [Actor(4, 0xEC4, center)], party: party));
        }
        records.Add(Frame(456, 40, [Actor(2, 0x1011, center, dead: true)], party: party));
        records.Add(Frame(456, 41, [], party: party));
        load("masterplayer.quest.divine-intervention", records);
        controller.Seek(.5);
        Check(dangers() == 0 && previews().Length == 0, "Secondary alone started Divine Intervention.");
        controller.Seek(1.5);
        Check(previews().Length == 3 && (ulong)Encounter().GetType().GetMethod("PriorityTarget")!
            .Invoke(Encounter(), [1ul, center])! == 3, "Divine pair/chain markers or chain priority.");
        dynamic arena = Value(Encounter(), "Arena");
        var sourceType = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "MasterPlayer.MechanicRuntime")
            .GetType("BossMod.Heavensward.Quest.MSQ.DivineIntervention.SerGrinnaux")!;
        var bounds = sourceType.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var wdir = sourceType.Assembly.GetType("BossMod.WDir")!;
        for (var x = -16.1f; x < 16; x += .5f)
            for (var z = -14.1f; z < 14; z += .5f)
            {
                var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds, [Activator.CreateInstance(wdir, [x, z])])!;
                Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, $"Divine exact source arena mismatch at {x},{z}.");
            }
        controller.Seek(2.5); Check(dangers() == 2 && previews().Length == 5, "Concurrent Divine boss casts.");
        controller.Seek(3.5); Check(dangers() == 1 && previews().Length == 3, "Primary death or BattleEnded reset the surviving cast.");
        controller.Seek(4.5); Check(dangers() == 1 && previews().Length == 3, "Primary destruction reset surviving cast.");
        controller.Seek(6.5); Check(dangers() == 0, "Observed secondary cast end leaked.");
        foreach (var (action, index) in actions.Select((a, i) => (a, i)))
        {
            controller.Seek(11.5 + index * 6);
            Check(dangers() == (action == 3992 ? 0 : 1), $"Divine action {action} navigation output.");
            if (action == 3993)
            {
                var pending = ((IEnumerable)Value(Encounter(), "Casts")).Cast<object>().Select(k => Value(k, "Value")).Single();
                Check(Vector3.Distance((Vector3)Value(pending, "Location"), new(-1000f / 65535, 0, -1000f / 65535)) < .000001
                    && Math.Abs((float)Value(pending, "Rotation") - MathF.PI / 2) < .00001, "Charge followed moved source or packet rotation.");
            }
            if (action == 3992)
                Check(((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().Count() == 1,
                    "Thunder Thrust lost raidwide prediction.");
            controller.Seek(14.5 + index * 6);
            Check(dangers() == 0, $"Divine action {action} finish.");
        }
        controller.Seek(40.5); Check(dangers() == 0 && previews().Length == 0, "Both bosses dead left the chain encounter active.");
        controller.Seek(3.5); Check(dangers() == 1, "Divine reverse Seek lost joint lifecycle.");
        stop();
        Console.WriteLine("PASS production Divine Intervention: exact source capsule difference, primary-only start, seven components, locked charge, joint boss lifecycle, BattleEnded, chain targeting and colored markers, both drawing and navigation, Seek and cleanup");
    }
}
