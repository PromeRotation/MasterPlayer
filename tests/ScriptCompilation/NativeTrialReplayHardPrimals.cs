using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHardPrimals(object managerObject, object runtimeObject, object controllerObject,
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
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("hard-trial/" + key)).ToArray();
        ulong Priority(ulong current = 1) => (ulong)Encounter().GetType().GetMethod("PriorityTarget")!.Invoke(Encounter(), [current, Vector3.Zero])!;
        string? Countdown() => (string?)Encounter().GetType().GetProperty("HardTrialCountdown", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Encounter());
        void Isolated()
        {
            var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
            var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
                "Hard trial replay issued PR facing/action control.");
        }
        string Timeline(double t, ulong actor) => JsonSerializer.Serialize(new { t, kind = "PlayActionTimeline",
            type = "PromeRotation.LogSystem.LogSystemPlayActionTimelineEvent",
            data = new { SourceId = actor, TimelineId = 0x008D, Timestamp = Start.AddSeconds(t) } });
        string Tether(double t, uint id) => JsonSerializer.Serialize(new { t, kind = "Tether",
            type = "PromeRotation.LogSystem.LogSystemTetherEvent",
            data = new { SourceId = 11, TargetId = 1, TetherId = id, Timestamp = Start.AddSeconds(t) } });
        var party = new object[] { new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL } };
        load("masterplayer.trial.ifrit-hard",
        [
            Header(292), Frame(292, 0, [Actor(1, 0xD1, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero),
                Actor(10, 0xD1, Vector3.UnitZ * 5), Actor(11, 0xD2, Vector3.Zero, targetable: false),
                Actor(20, 0x1B2, Vector3.Zero)], full: true, party: party),
            Timeline(.1, 10),
            Frame(292, 1, [Actor(10, 0xD1, Vector3.UnitX * 7, rotation: MathF.PI / 2), Actor(11, 0xD2, Vector3.Zero)], party: party),
            Frame(292, 2, [Actor(10, 0xD1, Vector3.UnitX * 7, cast: Cast(457, 3, Vector3.UnitX * 7, MathF.PI, startedAt: 2)),
                Actor(20, 0x1B2, Vector3.Zero, cast: Cast(1358, 3, Vector3.Zero, startedAt: 2))], party: party),
            CastEvent(2, 10, 457, 3, Vector3.UnitX * 7, MathF.PI), CastEvent(2, 20, 1358, 3, Vector3.Zero),
            Frame(292, 5.4, [Actor(10, 0xD1, Vector3.UnitX * 7), Actor(20, 0x1B2, Vector3.Zero)], party: party),
            Frame(292, 6, [Actor(11, 0xD2, Vector3.Zero, dead: true)], party: party),
            Frame(292, 7, [], party: party),
        ]);
        controller.Seek(.5);
        Check(Group("cyclone").Length == 1 && (DateTime)Value(Group("cyclone").Single(), "Activation") == Start.AddSeconds(4.5)
            && Countdown() == null, "Ifrit timeline pre-cast state missing."); Isolated();
        controller.Seek(1.5);
        Check((Vector3)Value(Group("cyclone").Single(), "Position") == Vector3.UnitX * 7
            && (float)Value(Group("cyclone").Single(), "Rotation") == MathF.PI / 2
            && (DateTime)Value(Group("cyclone").Single(), "Activation") == Start.AddSeconds(5.5)
            && Countdown()!.Contains("54.5") && Priority() == 11, "Ifrit rolling charge or first nail clock differs.");
        controller.Seek(2.5);
        Check((DateTime)Value(Group("cyclone").Single(), "Activation") == Start.AddSeconds(5.3)
            && (float)Value(Group("cyclone").Single(), "Rotation") == MathF.PI && dangers() == 2, "Ifrit true charge cast did not lock.");
        controller.Seek(5.5); Check(Group("cyclone").Length == 0 && dangers() == 0, "Ifrit real cast endings leaked.");
        controller.Seek(6.5); Check(Countdown() == null && Priority() == 1, "Ifrit dead nail retained timer/priority.");
        controller.Seek(1.5); Check(Group("cyclone").Length == 1 && Countdown()!.Contains("54.5"), "Ifrit Seek lost timeline/clock.");
        manager.SetScriptEnabled("masterplayer.trial.ifrit-hard", false);
        Check(dangers() == 0 && previews().Length == 0, "Ifrit hard disable leaked."); stop();

        var origin = new Vector3(0, 0, -15);
        load("masterplayer.trial.garuda-hard",
        [
            Header(294), Frame(294, 0, [Actor(1, 0xF2, origin, combat: true), Actor(100, 0, Vector3.UnitZ * 5),
                Actor(10, 0xF0, Vector3.Zero, dead: true, targetable: false, hitboxRadius: 2),
                Actor(11, 0x61E, origin, tethers: [new { Id = 4, TargetId = 1UL }]),
                Actor(12, 0x623, Vector3.Zero), Actor(13, 0x5FF, Vector3.Zero), Actor(14, 0x624, Vector3.Zero)], full: true, party: party),
            Frame(294, .1, [Actor(1, 0xF2, origin, combat: true, cast: Cast(1384, 3, origin)),
                Actor(11, 0x61E, origin, cast: Cast(1390, 4, origin), tethers: [new { Id = 4, TargetId = 1UL }])], party: party),
            CastEvent(.1, 1, 1384, 3, origin), CastEvent(.1, 11, 1390, 4, origin),
            Tether(1, 11),
            Frame(294, 1.1, [Actor(10, 0xF0, Vector3.UnitX * 15, hitboxRadius: 10),
                Actor(11, 0x61E, origin, cast: Cast(1390, 4, origin), tethers: [new { Id = 11, TargetId = 1UL }])], party: party),
            Frame(294, 3.5, [Actor(1, 0xF2, origin, combat: true)], party: party),
            Frame(294, 4.5, [Actor(11, 0x61E, origin, tethers: [new { Id = 11, TargetId = 1UL }])], party: party),
            Frame(294, 5, [Actor(1, 0xF2, origin, combat: true, cast: Cast(1385, 4, origin, startedAt: 5)),
                Actor(12, 0x623, Vector3.Zero, cast: Cast(1386, 3, Vector3.Zero, startedAt: 5)),
                Actor(14, 0x624, Vector3.Zero, cast: Cast(1387, 3, Vector3.Zero, startedAt: 5))], party: party),
            Frame(294, 8.5, [Actor(12, 0x623, Vector3.Zero), Actor(14, 0x624, Vector3.Zero)], party: party),
            Frame(294, 9.5, [Actor(1, 0xF2, origin, combat: true)], party: party),
            Frame(294, 10, [], removed: [10], party: party),
            Frame(294, 11, [], party: party),
        ]);
        controller.Seek(.01); Check(Group("friction").Length == 1 && Priority(11) == 11, "Garuda initial Friction/green priority missing.");
        controller.Seek(.5);
        Check(Group("los").Count(a => (bool)Value(a, "Display")) == 2 && Group("friction").Length == 0 && dangers() == 2,
            "Garuda independent LOS/Friction missing."); Isolated();
        controller.Seek(1.5);
        dynamic shadow = Value(previews().First(a => (bool)Value(a, "Safe")), "Geometry");
        Check((bool)shadow.Contains(Vector3.UnitZ * 5, 0f) && Priority(11) == 13, "Garuda LOS snapshot or red tether replacement differs.");
        controller.Seek(3.7); Check(Group("los").Count(a => (bool)Value(a, "Display")) == 1 && Group("friction").Length == 1, "Garuda one LOS finish consumed both.");
        controller.Seek(4.7); Check(Group("los").Length == 0 && dangers() == 1, "Garuda LOS finish retained shadow.");
        controller.Seek(5.5); Check(dangers() == 2 && Group("friction").Length == 0 && previews().Length == 4, "Garuda cast wind/eye combination missing.");
        controller.Seek(8.7); Check(dangers() == 0 && Group("friction").Length == 0, "Garuda cast eye became persistent.");
        controller.Seek(9.7); Check(dangers() == 1, "Garuda raidwide finish did not restore Friction.");
        controller.Seek(10.5); Check(dangers() == 0, "Garuda removed pillar retained navigation.");
        controller.Seek(.5); Check(Group("los").Count(a => (bool)Value(a, "Display")) == 2 && Priority(11) == 11, "Garuda reverse Seek retained future tether or pillar.");
        stop();

        load("masterplayer.trial.titan-hard",
        [
            Header(293), Frame(293, 0, [Actor(1, 0xF7, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero),
                Actor(10, 0x5E4, Vector3.Zero, targetable: false), Actor(11, 0x5E1, Vector3.Zero, targetable: false),
                Actor(20, 0x5A3, Vector3.UnitZ * 10), Actor(21, 0x5A3, -Vector3.UnitZ * 10), Actor(22, 0x5A3, Vector3.UnitX * 10)], full: true, party: party),
            Frame(293, .1, [Actor(20, 0x5A3, Vector3.UnitZ * 10, cast: Cast(1052, 5, Vector3.UnitZ * 10)),
                Actor(21, 0x5A3, -Vector3.UnitZ * 10, cast: Cast(1052, 7.249, -Vector3.UnitZ * 10)),
                Actor(22, 0x5A3, Vector3.UnitX * 10, cast: Cast(1052, 7.25, Vector3.UnitX * 10))], party: party),
            Frame(293, 1, [Actor(1, 0xF7, Vector3.Zero, combat: true, cast: Cast(1365, 3, Vector3.Zero, startedAt: 1)),
                Actor(10, 0x5E4, Vector3.Zero)], party: party),
            Frame(293, 2, [Actor(11, 0x5E1, Vector3.Zero)], party: party),
            Frame(293, 4.4, [Actor(1, 0xF7, Vector3.Zero, combat: true)], party: party),
            Frame(293, 5.4, [Actor(20, 0x5A3, Vector3.UnitZ * 10)], party: party),
            Frame(293, 7.7, [Actor(21, 0x5A3, -Vector3.UnitZ * 10), Actor(22, 0x5A3, Vector3.UnitX * 10)], party: party),
            Frame(293, 8, [Actor(1, 0xF7, Vector3.Zero, combat: true, cast: Cast(1365, 3, Vector3.Zero, startedAt: 8))], party: party),
            Frame(293, 11.4, [Actor(1, 0xF7, Vector3.Zero, combat: true), Actor(10, 0x5E4, Vector3.Zero, dead: true),
                Actor(11, 0x5E1, Vector3.Zero, dead: true)], party: party),
            Frame(293, 12, [], party: party),
        ]);
        controller.Seek(.5); Check(Group("bomb").Length == 2 && dangers() == 2 && Countdown() == null, "Titan bomb strict equality boundary differs.");
        controller.Seek(1.5); Check(Group("geocrush").Length == 2 && dangers() == 4 && Countdown()!.Contains("61.5") && Priority() == 10,
            "Titan historical inner/outer pair or heart clock missing."); Isolated();
        controller.Seek(2.5); Check(Priority() == 11, "Titan gaol priority missing.");
        controller.Seek(4.7); Check(Group("geocrush").Length == 1 && dangers() == 3, "Titan cast finish did not retain only outer.");
        controller.Seek(5.7); Check(Group("bomb").Length == 2, "Titan first explosion did not slide bomb window.");
        controller.Seek(7.9); Check(Group("bomb").Length == 0 && dangers() == 1, "Titan bomb finish leaked.");
        controller.Seek(8.5);
        dynamic inner = Value(previews().Single(a => ((string)Value(a, "Id")).EndsWith("hard-trial/geocrush/inner")), "Geometry");
        Check(Group("geocrush").Length == 2 && (bool)inner.Contains(Vector3.UnitZ * 17.99f, 0f)
            && !(bool)inner.Contains(Vector3.UnitZ * 18.01f, 0f), "Titan second jump radius differs.");
        controller.Seek(11.7); Check(Group("geocrush").Length == 1 && Countdown() == null && Priority() == 1, "Titan ended heart/gaol retained output.");
        controller.Seek(1.5); Check(Group("geocrush").Length == 2 && Countdown()!.Contains("61.5"), "Titan Seek retained future jump count.");
        manager.SetScriptEnabled("masterplayer.trial.titan-hard", false);
        Check(dangers() == 0 && previews().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Titan hard disable retained output."); stop();
        Console.WriteLine("PASS ARR hard primals actual DLL replay timeline charges, independent pillar LOS, tether priority, wind/eye cast lifetimes, bomb strict windows, Geocrush pair, clocks, disable, Seek and PR isolation");
    }
}
