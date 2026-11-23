using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntControls(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const uint territory = 956;
        var rules = new (uint Oid, uint Action, double Seconds)[]
        {
            (0x35DD, 27073, 5), (0x35DD, 27075, 3), (0x35DD, 27076, 5), (0x35DD, 27077, 5), (0x35DD, 27078, 4),
            (0x2A03, 17043, 3), (0x2A03, 18041, 3),
            (0x1AB5, 7924, 3), (0x1AB5, 7920, 2.1), (0x1AB5, 7921, 3.5), (0x1AB5, 7922, 2.5), (0x1AB5, 7919, 2.1), (0x1AB5, 7918, 3),
            (0x1AB1, 7990, 0), (0x1AB1, 7991, 2.5), (0x1AB1, 7996, 4), (0x1AB1, 7995, 3), (0x1AB1, 7993, 4), (0x1AB1, 7994, 3), (0x1AB1, 7992, 2),
        };
        var party = new object[] { new { Slot = 0, Role = "Tank", EntityId = 100UL }, new { Slot = 1, Role = "Healer", EntityId = 101UL } };
        Vector3 Location(int index) => new(100 + index * 10, 0, 0);
        var actors = rules.Select((r, i) => Actor((ulong)i + 1, r.Oid, Location(i) - Vector3.UnitZ * 20, combat: true,
            cast: Cast(r.Action, r.Seconds, Location(i), target: 101))).ToArray();
        var lines = new List<string>
        {
            Header(territory),
            Frame(territory, 0, [Actor(100, 0, Vector3.Zero, rotation: MathF.PI / 2), Actor(101, 0, Vector3.Zero)], full: true, party: party),
            Frame(territory, .1, actors, party: party),
        };
        for (var i = 0; i < rules.Length; ++i)
            lines.Add(JsonSerializer.Serialize(new { t = .1, kind = "CastStart",
                type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0,
                data = new { SourceId = i + 1, TargetId = 101, ActionId = rules[i].Action, CastTime = rules[i].Seconds,
                    SourcePosition = Pose(Location(i) - Vector3.UnitZ * 20), EffectPosition = Pose(Location(i)), Timestamp = Start.AddSeconds(.1) } }));
        lines.Add(Frame(territory, 2.9, [Actor(100, 0, Vector3.Zero, rotation: -MathF.PI / 2)], party: party));
        lines.Add(Frame(territory, 4, [Actor(2, 0x35DD, Vector3.Zero, combat: true)], party: party));
        lines.Add(Frame(territory, 5, rules.Select((r, i) => Actor((ulong)i + 1, r.Oid, Location(i), combat: true)), party: party));
        lines.Add(Frame(territory, 6, [], removed: Enumerable.Range(1, rules.Length).Select(i => (ulong)i).ToArray(), party: party));
        lines.Add(Frame(territory, 7, [], party: party));
        load("masterplayer.hunts.simple", lines);
        object Script() => ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
        object Encounter() => Script().GetType().BaseType!.GetField("encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object Value(object value, string name) => value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(value)!;
        float? Facing()
        {
            var script = Script();
            var player = Value(script, "CurrentPlayer");
            var encounter = Encounter();
            return (float?)encounter.GetType().GetMethod("Facing", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(encounter, [player, (object)runtime.CurrentTime]);
        }
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!,
                "Offline hunt gaze published a PR control request.");
        }
        controller.Seek(1d);
        Check(dangers() == 15 && previews().Length == 38, "Hunt controls lost AOE, charge, eyes/arcs, enemy markers or made gaze/Hint a danger.");
        Check(Facing() == null && ((string)Value(Script(), "MechanicHintsText")).Contains("Turn away from gaze!")
            && ((string)Value(Script(), "MechanicHintsText")).Contains("Applies damage buff to self"),
            "Hunt early gaze or pure CastHint was not preserved.");
        controller.Seek(2.7d);
        Check(Facing() != null, "Imminent gaze did not plan a safe PR heading."); Isolated();
        controller.Seek(3d);
        Check(!((string)Value(Script(), "MechanicHintsText")).Contains("Turn away from gaze!"), "Turning away retained a personal gaze warning.");
        controller.Seek(4.5d);
        Check(dangers() == 14 && previews().Length == 37, "Independent charge finish removed another hunt mechanic.");
        controller.Seek(5.5d);
        Check(dangers() == 0 && previews().Length == 20 && Facing() == null, "Observed cast end retained eyes/arcs, charge or facing.");
        controller.Seek(6.5d);
        Check(dangers() == 0 && previews().Length == 0, "Hunt instance removal leaked enemy markers.");
        controller.Seek(1d);
        Check(dangers() == 15 && previews().Length == 38, "Reverse Seek failed to rebuild all hunt control drawings.");
        stop();

        foreach (var (oid, action, seconds, distance) in new[] { (0x1AB5u, 7919u, 2.1, 20f), (0x1AB1u, 7995u, 3d, 30f), (0x1AB1u, 7993u, 4d, 20f) })
        {
            var activation = Start.AddSeconds(.1).AddSeconds((float)seconds + .3);
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, oid, new(50, 0, 50), combat: true), Actor(100, 0, new(0, 0, 5))], full: true),
                Frame(territory, .1, [Actor(1, oid, new(50, 0, 50), combat: true, cast: Cast(action, seconds, Vector3.Zero))]),
                CastEvent(.1, 1, action, seconds, Vector3.Zero),
                Frame(territory, 2, [Actor(100, 0, new(0, 0, 5), stackedStatus: 160, statusExpiresAt: activation.AddSeconds(1))]),
                Frame(territory, 3, [Actor(100, 0, new(0, 0, 5), stackedStatus: 160, statusExpiresAt: activation)]),
                Frame(territory, 5, [Actor(1, oid, new(50, 0, 50), combat: true)]),
                Frame(territory, 6, []),
            ]);
            controller.Seek(1d);
            Check(dangers() == 1 && previews().Length == 4, "Hunt knockback did not keep independent damage and projection.");
            var landing = previews().Single(p => ((string)Value(p, "Id")).EndsWith("/landing", StringComparison.Ordinal));
            dynamic landingGeometry = Value(landing, "Geometry");
            Check((bool)landingGeometry.Contains(new Vector3(0, 0, 5 + distance), 0f), "Hunt landing used boss position or wrong distance.");
            controller.Seek(2.5d);
            Check(dangers() == 1 && previews().Length == 2, "Knockback immunity removed damage or retained projection.");
            controller.Seek(3.5d);
            Check(dangers() == 1 && previews().Length == 4, "Immunity expiry equal to activation was incorrectly accepted.");
            controller.Seek(5.5d);
            Check(dangers() == 0 && previews().Length == 1, "Knockback cast end retained route/landing or deleted the boss marker.");
            controller.Seek(1d);
            Check(previews().Length == 4, "Knockback reverse Seek retained future immunity.");
            Isolated();
            stop();
        }
        foreach (var sameComponent in new[] { false, true })
        {
            var secondOrigin = Vector3.UnitX * (sameComponent ? 20 : 10);
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(100, 0, Vector3.UnitX)], full: true),
                Frame(territory, .1, [
                    Actor(1, 0x1AB1, Vector3.Zero, combat: true, cast: Cast(sameComponent ? 7995u : 7993u, 3, Vector3.Zero)),
                    Actor(2, 0x1AB1, secondOrigin, combat: true, cast: Cast(7995, 3, secondOrigin))]),
                Frame(territory, 2, [Actor(1, 0x1AB1, Vector3.Zero, combat: true,
                    cast: Cast(sameComponent ? 7995u : 7993u, 3.04, Vector3.Zero))]),
                Frame(territory, 4, [Actor(1, 0x1AB1, Vector3.Zero, combat: true)]),
                Frame(territory, 5, [Actor(2, 0x1AB1, secondOrigin, combat: true)]),
                Frame(territory, 6, []),
            ]);
            void Landing(ulong source, float x)
            {
                var landing = previews().Single(p => ((string)Value(p, "Id")).EndsWith($"knockback/{source}/landing", StringComparison.Ordinal));
                dynamic shape = Value(landing, "Geometry");
                Check((bool)shape.Contains(new Vector3(x, 0, 0), 0f), "Actual combined knockback landing differed from component-local source order.");
            }
            controller.Seek(1d);
            Check(previews().Length == 8 && dangers() == 2, "Combined knockback lost independent damage, enemies or projected landings.");
            Landing(1, sameComponent ? 31 : 21);
            Landing(2, sameComponent ? 61 : -29);
            controller.Seek(2.5d);
            if (sameComponent)
            {
                Check(previews().Length == 6, "Restarted knockback source did not move to the component-list tail.");
                Landing(1, 31);
            }
            else Landing(2, -29);
            controller.Seek(1d);
            Check(previews().Length == 8, "Combined knockback reverse Seek retained future source order.");
            stop();
        }
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(100, 0, Vector3.Zero)], full: true),
            Frame(territory, .1, [Actor(1, 0x2A03, Vector3.Zero, combat: true, cast: Cast(18041, 3, Vector3.Zero))]),
            Frame(territory, 3, [Actor(100, 0, Vector3.UnitZ, rotation: MathF.PI)]),
            Frame(territory, 4, [Actor(1, 0x2A03, Vector3.Zero, combat: true)]),
            Frame(territory, 5, []),
        ]);
        controller.Seek(2.8d);
        Check(Facing() == null && Value(Script(), "MechanicHintsText") == null, "Coincident gaze invented a personal warning or control direction.");
        controller.Seek(3.1d);
        Check(Facing() != null, "Gaze failed to resume planning after leaving the coincident point.");
        Isolated();
        stop();
        Console.WriteLine("PASS HuntControls actual DLL 20 rules, fixed charges, real gaze range/lead/PR replay isolation, eye/pupil/view arc and enemy drawings, three KB distances and immunity expiry, independent damage, observed ends, reverse Seek and cleanup");
    }
}
