using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTwintania(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.raid.coil5";
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        FieldInfo ScriptField(string name)
        {
            var type = Script().GetType();
            while (type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance) == null) type = type.BaseType!;
            return type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
        }
        object Encounter() => ScriptField("_encounter").GetValue(Script())!;
        int Stage() => (int)Encounter().GetType().GetField("twinStage", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Encounter())!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("twin/" + prefix)).ToArray();
        bool Active() => Value(Encounter(), "Active") != null;
        string Director(double t, uint command) => JsonSerializer.Serialize(new
        {
            t, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
            data = new { Command = command, Timestamp = Start.AddSeconds(t) },
        });
        object[] party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        uint[] jobs = [19, 21, 24, 28, 22, 30, 25, 23];
        object[] players = Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, new(i + 2, 0, i + 1), classJob: jobs[i])).ToArray();
        object Boss(bool targetable = true, object? cast = null, bool dead = false)
            => Actor(1, 0x7E5, Vector3.Zero, combat: true, targetable: targetable, targetId: 101, cast: cast, dead: dead);
        object Link(ulong actor) => Actor(actor, 0x7E6, new((float)(actor - 10) * 10, 0, 0));
        var records = new List<string>
        {
            Header(245), Frame(245, 0, [Boss(), .. players], full: true, party: party),
            Frame(245, 1, [Link(10)], party: party),
            EffectEvent(1.1, 1, 1452, 1, target: 102), EffectEvent(1.2, 1, 1451, 2, target: 100),
            StatusEvent(1.3, 100, 292, 0, true),
            Frame(245, 2, [Link(11)], party: party),
            Frame(245, 2.1, [Boss(false)], party: party),
        };
        for (var i = 0; i < 6; ++i)
        {
            var t = i < 3 ? 2.2 + .2 * i : 3.1 + .2 * (i - 3);
            if (i == 3)
                records.Add(Frame(245, 3, [Actor(30, 0x7E9, new(2, 0, 2)), Actor(31, 0x7E8, new(5, 0, 5))], party: party));
            records.Add(EffectEvent(t, 1, 1456, (uint)(3 + 2 * i), target: 101));
            records.Add(EffectEvent(t + .1, 1, 1247, (uint)(4 + 2 * i)));
        }
        records.AddRange(
        [
            Frame(245, 4, [Boss()], party: party),
            EffectEvent(4.1, 1, 1248, 20),
            Frame(245, 4.2, [Boss(cast: Cast(1458, .5, Vector3.Zero, target: 101, startedAt: 4.2))], party: party),
            Frame(245, 5, [Boss(cast: Cast(1249, .5, Vector3.Zero, startedAt: 5))], party: party),
            Frame(245, 5.51, [Boss(cast: Cast(1249, .5, Vector3.Zero, startedAt: 5))], party: party),
            Frame(245, 5.81, [Boss(cast: Cast(1249, .5, Vector3.Zero, startedAt: 5))], party: party),
            Frame(245, 5.9, [Boss(), Actor(40, 0x1E8910, new(5, 0, 5), hitboxRadius: .7f),
                Actor(41, 0x7EA, new(4, 0, 4))], party: party),
            EffectEvent(5.95, 1, 1251, 21, target: 101),
            Frame(245, 6, [Link(12)], party: party),
            EffectEvent(6.1, 1, 1453, 22, target: 100),
            EffectEvent(6.2, 1, 1457, 23, target: 100),
            EffectEvent(6.3, 1, 670, 24, target: 101), EffectEvent(6.4, 1, 670, 25, target: 101),
            EffectEvent(6.5, 1, 670, 26, target: 101), EffectEvent(6.6, 1, 670, 27, target: 101),
            EffectEvent(6.7, 1, 670, 28, target: 101),
            EffectEvent(6.8, 1, 1256, 29),
            Frame(245, 6.9, [Actor(100, 0, new(2, 0, 1), dead: true)], party: party),
            Director(7, 0x40000005), Frame(245, 7.05, [], party: party),
            Director(7.1, 0x40000011),
            Frame(245, 8, [Boss(dead: true)], party: party),
            Frame(245, 9, [], party: party),
        ]);
        load(id, records);
        controller.Seek(.01);
        var descriptor = ((IEnumerable)manager.Scripts).Cast<object>().Single(d => (string)Value(Value(d, "Metadata"), "Id") == id);
        dynamic store = managerObject.GetType().GetMethod("GetScriptSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(managerObject, [descriptor])!;
        var assignmentType = Script().GetType().GetProperty("Slot1")!.PropertyType;
        var roles = new[] { "MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2" };
        for (var i = 0; i < 8; ++i) store.SetPropertyValue("Slot" + (i + 1), Enum.Parse(assignmentType, roles[i]));
        controller.Seek(1.25);
        Check(Stage() == 1 && Group("conflag/personal").Length == 1 && Group("fireball/outline").Length == 1,
            "T05 production P2 personal and common display.");
        Check(previews().Length >= 2 && dangers() == 1, "T05 navigation and both drawing surfaces.");
        controller.Seek(1.35); Check(Group("conflag/personal").Length == 0, "T05 real status routing.");
        for (var i = 0; i < 6; ++i)
        {
            var t = i < 3 ? 2.2 + .2 * i : 3.1 + .2 * (i - 3);
            controller.Seek(t + .02);
            Check(Group("divebomb").Length == 1 && Stage() == (i < 3 ? 4 + 2 * i : 11 + 2 * (i - 3)), "T05 Divebomb marker state.");
            controller.Seek(t + .12);
            Check(Group("divebomb").Length == 0, "T05 Divebomb effect routing.");
        }
        controller.Seek(4.02); Check(Stage() == 17 && Group("profusion/position").Length == 1, "T05 reappear/Profusion.");
        Check((string?)Value(Script(), "PredictedDamageText") == "Raidwide：8人/1次", "T05 persistent raidwide diagnostic.");
        controller.Seek(4.25); Check(Stage() == 18 && Group("plummet/outline").Length == 1, "T05 common components recreated.");
        Check(Value(Script(), "PredictedDamageText") == null, "T05 completed Profusion retained its diagnostic.");
        var control = ScriptField("twintaniaActions").GetValue(Script())!;
        Check((DateTime)control.GetType().GetField("nextAttempt", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)! == default,
            "Offline T05 actions reached execution.");
        controller.Seek(5.52); Check(Group("twister/prediction").Length == 0, "T05 froze Twister positions before the true finish.");
        controller.Seek(5.82); Check(Group("twister/prediction").Length == 8, "T05 true finish Twister predictions.");
        controller.Seek(5.97); Check(Group("twister/prediction").Length == 0 && Group("twister/outline").Length == 1
            && Group("dreadknight/line").Length == 1, "T05 live Twister and Dreadknight tether.");
        controller.Seek(6.65); Check(Stage() == 19 && Group("hatch/target").Length == 1
            && Group("liquid/bait").Length == 7 && Group("liquid/event").Length == 4, "T05 simultaneous P5 mechanics.");
        controller.Seek(6.75); Check(Group("liquid/bait").Length == 0 && Group("liquid/event").Length == 5, "T05 fifth hit clears only target.");
        controller.Seek(6.95); Check(Active() && Stage() == 19, "T05 personal death reset phase.");
        controller.Seek(7.06); Check(!Active() && dangers() == 0 && previews().Length == 0, "T05 wipe owner cleanup.");
        Check(Value(Script(), "PredictedDamageText") == null, "T05 wipe retained its diagnostic.");
        controller.Seek(8.1); Check(!Active() && dangers() == 0, "T05 boss death cleanup.");
        controller.Seek(2.42); Check(Stage() == 6 && Group("divebomb").Length == 1, "T05 reverse Seek state reconstruction.");
        manager.SetScriptEnabled(id, false);
        Check(dangers() == 0 && previews().Length == 0, "T05 disabled owner cleanup.");
        stop();
        Console.WriteLine("PASS Twintania actual DLL five phases, six Divebombs, status routing, Twister finish, Dreadknight, simultaneous Hatch and five-hit LiquidHell, replay action isolation, death, wipe, reverse Seek and owner cleanup");
    }
}
