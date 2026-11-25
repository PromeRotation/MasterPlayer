using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntIhnuxokiy(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        string? Text(string name) => (string?)Value(Script(), name);
        object[] Smog() => previews().Where(p => ((string)Value(p, "Id")).Contains("/ihnuxokiy/")).ToArray();
        const uint territory = 1187;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        foreach (var storage in new uint[] { 39778, 39779 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x4582, default, combat: true), Actor(999, 0x233C, default),
                    Actor(100, 0, default), Actor(101, 0, default)], full: true, party: party),
                Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(storage, 4, default))], party: party),
                Frame(territory, 1, [Actor(999, 0x233C, default)], party: party),
                Frame(territory, 2, [Actor(999, 0x233C, default, cast: Cast(39773, 5, default, startedAt: 2))], party: party),
                Frame(territory, 7, [Actor(999, 0x233C, default)], party: party),
                EffectEvent(8, 1, 39773, 1), EffectEvent(8.01, 1, 39773, 1),
                EffectEvent(9, 999, 39781, 2),
                Frame(territory, 10, [], removed: [1], party: party),
            ]);
            controller.Seek(3d);
            Check(Smog().Length == 2 && dangers() == 1, "Ihnuxokiy lost stored preview or made the tail risky.");
            controller.Seek(7.5);
            Check(Smog().Length == 2 && dangers() == 1, "Ihnuxokiy observed Finish consumed its effect-only queue.");
            controller.Seek(8.5);
            Check(Smog().Length == 1 && dangers() == 1, "Ihnuxokiy helper/owner effects failed FIFO promotion or deduplication.");
            dynamic geometry = Value(Smog().Single(), "Geometry");
            Check((bool)geometry.Contains(Vector3.Zero, 0f) == (storage == 39778)
                && (bool)geometry.Contains(new Vector3(10, 0, 0), 0f), "Ihnuxokiy changed the stored circle/ring.");
            dynamic snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            var deadline = ((IEnumerable)snapshot.DangerAreas).Cast<object>()
                .SelectMany(a => ((IEnumerable)Value(a, "DangerWindows")).Cast<object>())
                .Select(w => (DateTime)Value(w, "DangerFrom")).Single();
            Check(deadline == Start.AddSeconds(2 + 5 + .3).AddSeconds(2.2f),
                "Ihnuxokiy stored stage did not retain the exact float delay.");
            controller.Seek(9.5);
            Check(Smog().Length == 0 && dangers() == 0, "Ihnuxokiy final effect retained output.");
            controller.Seek(3d);
            Check(Smog().Length == 2 && dangers() == 1, "Ihnuxokiy reverse Seek retained future consumption.");
            controller.Seek(10.1);
            Check(previews().Length == 0 && dangers() == 0, "Ihnuxokiy owner removal retained helper output.");
            stop();
        }

        object MarchPlayer() => new
        {
            Id = 100ul, Kind = 1, Hp = 1000, Position = Pose(default), Statuses = new[]
            {
                new { Id = 2163u, ExpiresAt = Start.AddSeconds(30) },
                new { Id = 2161u, ExpiresAt = Start.AddSeconds(40) }
            }
        };
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x4582, default, combat: true), MarchPlayer()], full: true, party: party),
            Frame(territory, 1, [Actor(100, 0, default)], party: party),
            Frame(territory, 2, [Actor(100, 0, default, stackedStatus: 1257, statusExpiresAt: Start.AddSeconds(5))], party: party),
            Frame(territory, 4, [], removed: [1], party: party),
            Frame(territory, 6, [], party: party),
        ]);
        controller.Seek(.5);
        var landing = previews().Single(p => ((string)Value(p, "Id")).Contains("march/100/1/landing"));
        dynamic marker = Value(landing, "Geometry");
        Check((bool)marker.Contains(new Vector3(39.6f, 0, 0), 0f)
            && Text("MechanicHintsText")?.Contains("Aim for safe spot!") == true && dangers() == 0
            && !(bool)Value(Script(), "PyreticActive"), "Ihnuxokiy march duration, final hint or display-only execution contract differs.");
        controller.Seek(1.5);
        Check(!previews().Any(p => ((string)Value(p, "Id")).Contains("march/100/")), "Ihnuxokiy direction loss retained a projection.");
        controller.Seek(3d);
        landing = previews().Single(p => ((string)Value(p, "Id")).Contains("march/100/0/landing"));
        marker = Value(landing, "Geometry");
        Check((bool)marker.Contains(new Vector3(0, 0, 13.2f), 0f), "Ihnuxokiy active march did not use the remaining duration.");
        controller.Seek(4.5);
        Check(previews().Length == 0 && dangers() == 0, "Ihnuxokiy owner removal retained march projections.");
        stop();

        foreach (var action in new uint[] { 39774, 39776, 39777 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x4582, default, combat: true, targetId: 101),
                    Actor(100, 0, default), Actor(101, 0, default)], full: true, party: party),
                Frame(territory, .1, [Actor(1, 0x4582, default, combat: true, targetId: 101,
                    cast: Cast(action, 5, default, target: 1))], party: party),
                Frame(territory, 6, [Actor(1, 0x4582, default, combat: true)], party: party),
                Frame(territory, 7, [], party: party),
            ]);
            controller.Seek(1d);
            Check(dangers() == (action == 39774 ? 1 : 0), "Ihnuxokiy ordinary cast classification differs.");
            if (action != 39774)
                Check(Text("PredictedDamageText") == "Tankbuster：1人/1次",
                    "Ihnuxokiy source SingleTargetCast was changed to raidwide damage.");
            if (action == 39777) Check(Text("MechanicHintsText")?.Contains("Raidwide + forced march debuffs") == true,
                "Ihnuxokiy ChaoticStorm source hint missing.");
            controller.Seek(6.5);
            Check(dangers() == 0 && Text("PredictedDamageText") == null, "Ihnuxokiy actual Finish retained ordinary output.");
            stop();
        }
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x4582, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, default)], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(39778, 4, default))], party: party),
            Frame(territory, .5, [Actor(999, 0x233C, default)], party: party),
            CastEvent(1, 999, 39773, 5, default),
            Frame(territory, 2, [], removed: [999], party: party),
            EffectEvent(3, 1, 39773, 20), EffectEvent(4, 1, 39780, 21),
            Frame(territory, 5, [], party: party),
        ]);
        controller.Seek(2.5);
        Check(Smog().Length == 2 && dangers() == 1,
            "Ihnuxokiy packet helper removal discarded the effect-only queue and consumed storage.");
        controller.Seek(3.5);
        Check(Smog().Length == 1 && dangers() == 1, "Ihnuxokiy surviving caster failed to promote the destroyed helper's stored stage.");
        controller.Seek(4.5);
        Check(Smog().Length == 0 && dangers() == 0, "Ihnuxokiy surviving caster effects failed to clear the destroyed helper's history.");
        stop();
        Console.WriteLine("PASS HuntIhnuxokiy actual DLL stored circle/ring, head-only navigation, exact float delay, helper effect FIFO, three-second march and active remainder, source single-target damage, reverse Seek and cleanup");
    }
}
