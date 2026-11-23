using System.Collections;
using System.Numerics;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntStack(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const uint territory = 956;
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        string? Damage()
        {
            object script = ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
            return (string?)script.GetType().GetProperty("PredictedDamageText")!.GetValue(script);
        }
        foreach (var inside in new[] { 7, 8, 9 })
        foreach (var target in new ulong[] { 100, 101 })
        {
            var party = Enumerable.Range(0, inside).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) })
                .Append(new { Slot = 24, EntityId = 200UL }).ToArray();
            object[] Allies(bool dead = false) => [
                .. Enumerable.Range(0, inside).Select(i => Actor((ulong)(100 + i), 0, Vector3.Zero, dead: dead && (ulong)(100 + i) == target)),
                Actor(200, 0, new(30, 0, 30))];
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, Allies().Append(Actor(1, 0x2890, Vector3.Zero, combat: true)), full: true, party: party),
                Frame(territory, .1, [Actor(1, 0x2890, Vector3.Zero, combat: true,
                    cast: Cast(16971, 8, Vector3.Zero, target: target))], party: party),
                StatusEvent(2, 100, 1435, 0, true),
                Frame(territory, 3, Allies(true), party: party),
                Frame(territory, 4, Allies(), party: party),
                Frame(territory, 9, [Actor(1, 0x2890, Vector3.Zero, combat: true)], party: party),
                Frame(territory, 10, [Actor(1, 0x2890, Vector3.Zero)], party: party),
                Frame(territory, 11, [], party: party)
            ]);
            controller.Seek(1d);
            Check(Goals() == 0 && dangers() == 1
                && previews().Length == 2 && Damage() == null,
                "Actual stack navigation lost its exact capacity, target buddy union or full-mask guard.");
            dynamic snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            object navigation = ((IEnumerable)snapshot.DangerAreas).Cast<object>().Single();
            dynamic constraint = navigation.GetType().GetProperty("Geometry")!.GetValue(navigation)!;
            object window = ((IEnumerable)navigation.GetType().GetProperty("DangerWindows")!.GetValue(navigation)!).Cast<object>().Single();
            Check((DateTime)window.GetType().GetProperty("DangerFrom")!.GetValue(window)! == Start.AddSeconds(8.4),
                "Actual stack registration discarded its activation deadline.");
            if (target == 100)
                Check(!(bool)constraint.Contains(new Vector3(30, 0, 30), 0f)
                    && !(bool)constraint.Contains(Vector3.Zero, 0f)
                    && (bool)constraint.Contains(new Vector3(15, 0, 15), 0f),
                    "Actual buddy registration required all circles or allowed the gap between disjoint circles.");
            var outline = previews().Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("/stack/preview"));
            Check((bool)outline.GetType().GetProperty("OutlineOnly")!.GetValue(outline)!,
                "RootsOfAtopy filled its source outline.");
            controller.Seek(2.5d);
            Check(Goals() == 0 && dangers() == 1,
                "A status gained after cast start changed the frozen stack membership.");
            controller.Seek(3.5d);
            Check(Goals() == 0 && dangers() == 0 && previews().Length == 1, "Dead target retained its stack.");
            controller.Seek(4.5d);
            Check(previews().Length == 2, "Revived target lost its active stack.");
            controller.Seek(9.5d);
            Check(Goals() == 0 && dangers() == 0 && previews().Length == 1, "Actual cast finish retained stack output.");
            controller.Seek(10.5d);
            Check(previews().Length == 0, "Disengagement retained Nariphon output.");
            controller.Seek(1d);
            Check(Goals() > 0 || dangers() == 1, "Reverse Seek failed to rebuild stack navigation.");
            stop();
        }
        var frozenParty = new object[] { new { Slot = 0, EntityId = 100UL }, new { Slot = 8, EntityId = 101UL }, new { Slot = 24, EntityId = 102UL } };
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(100, 0, Vector3.Zero), Actor(101, 0, new(1, 0, 1)), Actor(102, 0, new(30, 0, 30)),
                Actor(1, 0x2890, Vector3.Zero, combat: true)], full: true, party: frozenParty),
            StatusEvent(.05, 100, 1435, 0, true),
            Frame(territory, .1, [Actor(1, 0x2890, Vector3.Zero, combat: true,
                cast: Cast(16971, 5, Vector3.Zero, target: 101))], party: frozenParty),
            StatusEvent(1, 100, 1435, 0, false), StatusEvent(1.1, 102, 1435, 0, true),
            Frame(territory, 6, [Actor(1, 0x2890, Vector3.Zero, combat: true)], party: frozenParty),
            Frame(territory, 7, [Actor(1, 0x2890, Vector3.Zero, combat: true,
                cast: Cast(16972, 3, Vector3.Zero, target: 100, startedAt: 7))], party: frozenParty),
            Frame(territory, 11, [Actor(1, 0x2890, Vector3.Zero, combat: true,
                cast: Cast(16970, 3, Vector3.Zero, startedAt: 11))], party: frozenParty),
            Frame(territory, 15, [Actor(1, 0x2890, Vector3.Zero, combat: true)], party: frozenParty),
            Frame(territory, 16, [], party: frozenParty)
        ]);
        controller.Seek(.5d);
        Check(dangers() == 1 && Goals() == 0 && Damage()?.Contains("Shared") == true,
            "Pre-snapshot status event failed to freeze Forbidden and publish Shared.");
        controller.Seek(2d);
        Check(dangers() == 1 && Goals() == 0 && Damage()?.Contains("Shared") == true,
            "Later status changes altered the current cast's frozen forbidden member.");
        controller.Seek(7.5d);
        Check(dangers() == 2 && Goals() == 0 && previews().Length == 2,
            "AllergenInjection lost its self-bait ally avoidance.");
        controller.Seek(11.5d);
        Check(dangers() == 1 && previews().Length == 2, "OdiousMiasma cone failed to reach the native drawing/navigation paths.");
        controller.Seek(15.5d);
        Check(dangers() == 0 && Goals() == 0 && Damage() == null, "Nariphon endings leaked output.");
        stop();
        Console.WriteLine("PASS HuntStack actual DLL Nariphon seven/eight/nine capacity, own buddy goals, frozen status events, Shared guard, outlines, bait/cone, dead/revive, reverse Seek and cleanup");
    }
}
