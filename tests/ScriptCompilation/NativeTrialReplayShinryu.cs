using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunShinryu(object managerObject, object runtimeObject, object controllerObject,
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
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        dynamic Arena() => Value(Encounter(), "Arena");
        string IconEvent(double t, ulong actor, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = actor, TargetId = actor, MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        string AnimationEvent(double t, ulong source, uint state) => JsonSerializer.Serialize(new { t, kind = "EObjAnimation",
            type = "PromeRotation.LogSystem.LogSystemEObjAnimationEvent",
            data = new { SourceId = source, Param1 = state >> 16, Param2 = state & 65535, Timestamp = Start.AddSeconds(t) } });
        var party = new object[] { new { Slot = 0, Role = "Tank", EntityId = 100UL }, new { Slot = 1, Role = "Tank", EntityId = 101UL } };
        const string id = "masterplayer.trial.shinryu";
        load(id,
        [
            Header(679), Frame(679, 0, [Actor(1, 0x1983, Vector3.Zero, combat: true),
                Actor(100, 0, Vector3.UnitZ * 5, classJob: 19, stackedStatus: 667, statusExpiresAt: Start.AddSeconds(.5))],
                full: true, party: party),
            Frame(679, 1, [], party: party),
            Frame(679, 2, [Actor(100, 0, Vector3.UnitZ * 5, classJob: 19)], party: party),
            Frame(679, 3, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(!Arena().Contains(new Vector3(29, 0, 0)), "Shinryu replay expired raw fetter slot expanded arena early.");
        controller.Seek(2.5);
        Check(Arena().Contains(new Vector3(29, 0, 0)), "Shinryu replay actual fetter removal failed.");
        stop();
        load(id,
        [
            Header(679), Frame(679, 0, [Actor(1, 0x1983, Vector3.Zero, combat: true),
                Actor(100, 0, Vector3.UnitZ * 5, classJob: 19), Actor(101, 0, Vector3.UnitX * 10, classJob: 21),
                Actor(10, 0x1B16, Vector3.Zero), Actor(11, 0x1B16, Vector3.Zero),
                Actor(12, 0x18D6, Vector3.Zero), Actor(13, 0x18D6, Vector3.Zero),
                Actor(20, 0x1E950D, Vector3.UnitX * -10), Actor(30, 0x1EA1A1, Vector3.UnitX * 20)], full: true, party: party),
            EffectEvent(.1, 10, 8096, 1), EffectEvent(.2, 11, 8096, 2),
            IconEvent(.3, 101, 24), IconEvent(.4, 100, 62), IconEvent(.5, 101, 23),
            StatusEvent(.6, 100, 667, 0, true), StatusEvent(.7, 100, 667, 0, false),
            AnimationEvent(.8, 12, 0x00040008), AnimationEvent(.9, 30, 0x00400080),
            Frame(679, 1, [Actor(1, 0x1983, Vector3.Zero, targetable: false, cast: Cast(8100, 3, Vector3.Zero, target: 100, startedAt: 1)),
                Actor(10, 0x1B16, Vector3.Zero, cast: Cast(8097, 3, Vector3.Zero, startedAt: 1)),
                Actor(11, 0x1B16, Vector3.Zero, cast: Cast(8097, 5, Vector3.Zero, startedAt: 1)),
                Actor(12, 0x18D6, Vector3.Zero, cast: Cast(8091, 20, Vector3.Zero, startedAt: 1)),
                Actor(13, 0x18D6, Vector3.Zero, cast: Cast(8107, 20, Vector3.Zero, startedAt: 1)),
                Actor(30, 0x1EA1A1, Vector3.UnitX * 20, eventAnimation: 0x00400080)], party: party),
            Frame(679, 2, [Actor(12, 0x18D6, Vector3.Zero), Actor(100, 0, Vector3.UnitZ * 5, classJob: 19, stackedStatus: 911)], party: party),
            EffectEvent(2.5, 10, 8092, 3, target: 101), EffectEvent(3, 1, 8093, 4),
            Frame(679, 4.5, [Actor(1, 0x1983, Vector3.Zero, targetable: false), Actor(10, 0x1B16, Vector3.Zero)], party: party),
            EffectEvent(5, 1, 8090, 5, target: 100),
            Frame(679, 6.5, [Actor(11, 0x1B16, Vector3.Zero), Actor(13, 0x18D6, Vector3.Zero),
                Actor(100, 0, Vector3.UnitZ * 5, classJob: 19)], party: party),
            Frame(679, 7, [Actor(1, 0x1983, Vector3.Zero, dead: true, targetable: false)], party: party),
            Frame(679, 8, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(Group("shinryu/ice").Length == 2 && Group("shinryu/thrust").Length == 2 && Group("shinryu/tank").Any()
            && Group("shinryu/icon").Count(a => (bool)Value(a, "Display")) == 3, "Shinryu independent mechanics missing.");
        Check(Arena().Contains(new Vector3(29, 0, 29)) && !Arena().Contains(new Vector3(20, 0, 0)),
            "Shinryu status expansion/platform difference lost.");
        Check((DateTime)Value(Group("shinryu/water").Single(), "Activation") == Start.AddSeconds(11)
            && (bool)Value(Group("shinryu/water").Single(), "Navigate"), "Shinryu water clock/flag precedence differs.");
        controller.Seek(2.2);
        Check(Group("shinryu/fire/join").Any() && Group("shinryu/slide").Length == 3
            && !Arena().Contains(new Vector3(20, 0, 0)), "Shinryu fire/ice/raw EAnim replay differs.");
        controller.Seek(3.2);
        Check(Group("shinryu/icon").Count(a => (bool)Value(a, "Display")) == 1, "Shinryu independent icon effects crossed.");
        controller.Seek(4.7);
        Check(!Group("shinryu/ice").Any() && Group("shinryu/thrust").Length == 1 && !Group("shinryu/tank").Any(),
            "Shinryu first real finish failed positional ice cleanup or cleared second thrust.");
        controller.Seek(6.7);
        Check(!Group("shinryu/thrust").Any() && !Group("shinryu/fire").Any() && !Group("shinryu/slide").Any()
            && !Group("shinryu/icon").Any(), "Shinryu actual endings leaked.");
        controller.Seek(7.5); Check(previews().Length == 0 && dangers() == 0, "Shinryu death leaked outputs.");
        controller.Seek(1.5);
        Check(Group("shinryu/ice").Length == 2 && Group("shinryu/tank").Any() && !Arena().Contains(new Vector3(20, 0, 0)),
            "Shinryu reverse Seek failed mechanics/arena.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Shinryu disable leaked."); stop();
        Console.WriteLine("PASS Shinryu actual DLL replay shared water clock, independent ice/thrust/KB/icons/tank, status and platform arena, thin ice, hidden primary, real endings, Seek and isolation");
    }
}
