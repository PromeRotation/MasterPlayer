using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunStigma(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.stigma";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        bool Hold() => (bool)Encounter().GetType().GetProperty("RequireStigmaMovementHold")!.GetValue(Encounter())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string Key(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        int Damage() => ((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Count();
        void Isolated()
        {
            var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
            var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!,
                "Stigma replay published real PR control.");
        }
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(-144, 0, -136);
        load(id,
        [
            Header(986), Frame(986, 0, [Actor(1, 0x3417, c, combat: true), Actor(100, 0, c + Vector3.UnitX * 10),
                Actor(101, 0, c)], full: true, party: party),
            Frame(986, .1, [Actor(1, 0x3417, c, combat: true, cast: Cast(25386, 5, c, target: 101))], party: party),
            Frame(986, 5.5, [Actor(1, 0x3417, c, combat: true)], party: party),
            Frame(986, 6, [Actor(1, 0x3417, c, combat: true, cast: Cast(25376, 7, c, .9f, startedAt: 6))], party: party),
            Frame(986, 13.4, [Actor(1, 0x3417, c, combat: true)], party: party),
            Frame(986, 14, [Actor(1, 0x3417, c, combat: true, cast: Cast(25387, 5, c, target: 100, startedAt: 14))], party: party),
            Frame(986, 19.4, [Actor(1, 0x3417, c, combat: true)], party: party),
        ]);
        controller.Seek(1); Check(previews().Length == 3 && dangers() == 1 && Aoes().Any(a => Key(a) == "stigma/knockback/1/landing"),
            "Production Omega stack/knockback missing.");
        controller.Seek(5.6); Check(previews().Length == 0, "Production Omega stack/knockback leaked.");
        controller.Seek(6.1); Check(previews().Length == 1 && dangers() == 1, "Production side cannon missing.");
        controller.Seek(13.5); Check(previews().Length == 0, "Production side cannon leaked.");
        controller.Seek(14.1); Check(Damage() == 1, "Production Omega buster missing.");
        controller.Seek(19.5); Check(Damage() == 0, "Production Omega buster leaked.");
        controller.Seek(1); Isolated(); stop();
        c = Vector3.Zero;
        load(id,
        [
            Header(986), Frame(986, 0, [Actor(1, 0x3416, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            Frame(986, .1, [Actor(10, 0x233C, c - Vector3.UnitX * 15, cast: Cast(25518, 1.5, c + Vector3.UnitX * 15)),
                Actor(11, 0x233C, c - Vector3.UnitZ * 15, cast: Cast(25518, 4, c + Vector3.UnitZ * 15))], party: party),
            Frame(986, 2, [Actor(10, 0x233C, c)], party: party),
            Frame(986, 4.5, [Actor(11, 0x233C, c)], party: party),
            Frame(986, 5, [Actor(1, 0x3416, c, combat: true, cast: Cast(25519, 2.5, c, .8f, startedAt: 5))], party: party),
            Frame(986, 7.9, [Actor(1, 0x3416, c, combat: true)], party: party),
        ]);
        controller.Seek(1); Check(previews().Length == 2 && dangers() == 2 && Aoes().Count(a => Key(a).StartsWith("stigma/charge/")) == 2,
            "Production charge origin/length previews missing.");
        controller.Seek(2.1); Check(previews().Length == 1, "One charge end erased another.");
        controller.Seek(4.6); Check(previews().Length == 0, "Production charge leaked.");
        controller.Seek(5.1); Check(previews().Length == 1, "Production WaveCannon missing.");
        controller.Seek(8); Check(previews().Length == 0, "Production WaveCannon leaked.");
        controller.Seek(1); Isolated(); stop();
        c = new(0, 0, 2.5f); var player = c + Vector3.UnitZ * 18;
        string MarchStatus(double t, uint status, float duration) => JsonSerializer.Serialize(new { t, kind = "StatusAdd",
            type = "PromeRotation.LogSystem.LogSystemStatusAddEvent", data = new { TargetId = 100UL, StatusId = status,
                Duration = duration, Timestamp = Start.AddSeconds(t) } });
        load(id,
        [
            Header(986), Frame(986, 0, [Actor(1, 0x3428, c, combat: true), Actor(100, 0, player, stackedStatus: 1958, statusExpiresAt: Start.AddSeconds(10)),
                Actor(101, 0, c)], full: true, party: party),
            StatusEvent(10, 100, 1958, 0, false), MarchStatus(10, 1257, 2),
            Frame(986, 10.1, [Actor(100, 0, player, rotation: MathF.PI, stackedStatus: 1257, statusExpiresAt: Start.AddSeconds(12))], party: party),
            StatusEvent(12, 100, 1257, 0, false),
            Frame(986, 12.1, [Actor(100, 0, c)], party: party),
            MarchStatus(13, 1961, 6), StatusEvent(19, 100, 1961, 0, false),
            Frame(986, 20, [Actor(20, 0x233C, c, cast: Cast(25650, 10, c, startedAt: 20)),
                Actor(21, 0x342B, c, cast: Cast(25646, 7, c, .3f, startedAt: 20)),
                Actor(22, 0x3429, c - Vector3.UnitX * 15, cast: Cast(25645, 5, c + Vector3.UnitX * 15, startedAt: 20))], party: party),
            Frame(986, 25.4, [Actor(22, 0x3429, c)], party: party),
            Frame(986, 27.4, [Actor(21, 0x342B, c)], party: party),
            Frame(986, 30.4, [Actor(20, 0x233C, c)], party: party),
        ]);
        controller.Seek(1); Check(previews().Length == 0, "Production march exceeded eight second limit.");
        controller.Seek(2.1); Check(previews().Length == 2 && dangers() == 1 && !Hold(), "Production snapshot march missing.");
        controller.Seek(9.5);
        var heading = (float?)Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [player, 0f]);
        Check(heading != null && Math.Abs(heading.Value) > .5f, "Production impending march did not choose safe PR facing.");
        Isolated();
        controller.Seek(10.2); Check(Hold() && previews().Length == 2 && dangers() == 0, "Production active forced march hold/remaining path missing.");
        Isolated();
        controller.Seek(12.2); Check(!Hold() && previews().Length == 0 && dangers() == 0, "Production forced march release leaked.");
        controller.Seek(13.1); Check(previews().Length == 2 && dangers() == 1, "Production directional status event not routed.");
        controller.Seek(15); Check(previews().Length == 2 && dangers() == 1, "Old object frame deleted live march event.");
        controller.Seek(19.1); Check(previews().Length == 0, "Production direction loss leaked.");
        controller.Seek(20.1); Check(previews().Length == 3 && dangers() == 3, "Production Stigma add combo missing.");
        controller.Seek(25.5); Check(previews().Length == 2, "Production rocket end erased other add casts.");
        controller.Seek(27.5); Check(previews().Length == 1, "Production dragon end erased donut.");
        controller.Seek(30.5); Check(previews().Length == 0, "Production add combo leaked.");
        controller.Seek(10.2); Check(Hold(), "Forced march Seek failed."); Isolated(); stop();
        Console.WriteLine("PASS production Stigma: three WIP source modules, radial fifteen wall stop, source-charge geometry, independent helper stages, four-direction march event/snapshot, eight-second visibility, PR facing plan, active movement hold, replay isolation, seek/stop");
    }
}
