using System.Numerics;
using System.Collections;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntAegeiros(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        dynamic runtime = runtimeObject;
        const uint territory = 956;
        var party = new object[] { new { Slot = 0, EntityId = 100UL }, new { Slot = 8, EntityId = 101UL }, new { Slot = 24, EntityId = 102UL } };
        object[] Allies() => [Actor(100, 0, new(0, 0, 5)), Actor(101, 0, new(0, 0, 8)), Actor(102, 0, new(50, 0, 0))];
        bool Canopy() => previews().Any(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("/canopy"));
        foreach (var target in new ulong[] { 100, 101 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, Allies().Append(Actor(1, 0x3671, Vector3.Zero, combat: true, targetId: target)), full: true, party: party),
                Frame(territory, 1, [Actor(1, 0x3671, Vector3.Zero, combat: true, targetId: target,
                    cast: Cast(27708, 6, Vector3.Zero, startedAt: 1))], party: party),
                Frame(territory, 3, [Actor(1, 0x3671, new(10, 0, 20), combat: true, targetId: target,
                    cast: Cast(27708, 6, Vector3.Zero, startedAt: 1))], party: party),
                Frame(territory, 8, [Actor(1, 0x3671, new(10, 0, 20), combat: true, rotation: .7f, targetId: target)], party: party),
                Frame(territory, 12, [Actor(1, 0x3671, new(10, 0, 20), combat: true, targetId: target,
                    cast: Cast(27709, 1, new(10, 0, 20), rotation: 1.2f, startedAt: 12))], party: party),
                Frame(territory, 14, [Actor(1, 0x3671, new(10, 0, 20), combat: true, targetId: target)], party: party),
                Frame(territory, 15, [Actor(1, 0x3671, new(10, 0, 20), combat: true, targetId: target,
                    cast: Cast(999, 1, new(10, 0, 20), startedAt: 15))], party: party),
                Frame(territory, 17, [Actor(1, 0x3671, new(10, 0, 20), combat: true, targetId: target)], party: party),
                Frame(territory, 18, [Actor(1, 0x3671, new(10, 0, 20), targetId: target)], party: party),
                Frame(territory, 19, [], party: party)
            ]);
            controller.Seek(.5d);
            Check(Canopy() && previews().Length == 2 && dangers() == (target == 100 ? 2 : 1),
                "Idle Canopy lost its exact outline or target-dependent personal navigation.");
            foreach (dynamic area in (IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas)
                foreach (object window in (IEnumerable)area.DangerWindows)
                {
                    Check((DateTime)window.GetType().GetProperty("DangerFrom")!.GetValue(window)! > DateTime.MinValue,
                        "An immediate cleave exposed DateTime.MinValue to the navigation buffer.");
                    window.GetType().GetMethod("Buffered", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(window, [.2f, .1f]);
                }
            controller.Seek(2d);
            Check(!Canopy() && dangers() == 2 && previews().Length == 3,
                "Leafstorm failed to hide the idle cleave and publish both circle/cone hazards.");
            controller.Seek(3.5d);
            foreach (var preview in previews().Where(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("hunt/1")))
            {
                dynamic shape = preview.GetType().GetProperty("Geometry")!.GetValue(preview)!;
                Check((bool)shape.Contains(new Vector3(10, 0, 20), 0f), "Leafstorm/Rimestorm fixed its origin at the start packet.");
            }
            controller.Seek(11d);
            Check(Canopy() && dangers() == (target == 100 ? 3 : 2),
                "Leafstorm ending/estimated time removed its predicted Rimestorm or failed to restore Canopy.");
            controller.Seek(12.5d);
            Check(!Canopy() && dangers() == 1 && previews().Length == 2,
                "Actual Rimestorm duplicated the old prediction.");
            controller.Seek(14.5d);
            Check(Canopy() && dangers() == (target == 100 ? 2 : 1), "Actual Rimestorm finish retained the prediction.");
            controller.Seek(15.5d);
            Check(!Canopy() && dangers() == 0, "An unlisted cast retained the constant cleave.");
            controller.Seek(17.5d);
            Check(Canopy(), "The unlisted cast ending did not restore Canopy.");
            controller.Seek(18.5d);
            Check(previews().Length == 0 && dangers() == 0, "Aegeiros disengagement retained output.");
            controller.Seek(.5d);
            Check(Canopy(), "Aegeiros reverse Seek failed to restore its idle mechanism.");
            stop();
        }
        Console.WriteLine("PASS HuntAegeiros actual DLL idle Canopy target/other navigation, alliance/NPC cones, all-cast hiding, live Leafstorm/Rimestorm origins, persistent prediction, actual ending, reverse Seek and cleanup");
    }
}
