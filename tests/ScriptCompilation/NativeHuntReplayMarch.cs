using System.Numerics;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntMarch(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        const uint territory = 956;
        var party = new object[] { new { Slot = 0, EntityId = 100UL } };
        object[] MarchPreviews() => previews().Where(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("march/100/")).ToArray();
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x35BF, Vector3.Zero, combat: true), Actor(100, 0, new(0, 0, 20))], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x35BF, Vector3.Zero, combat: true, cast: Cast(27230, 5, Vector3.Zero))], party: party),
            MarchGain(.2, 1959, 10), MarchGain(.3, 1960, 20),
            Frame(territory, 6, [Actor(1, 0x35BF, new(10, 0, 20), combat: true)], party: party),
            StatusEvent(7, 100, 1959, 0, false), StatusEvent(7.1, 100, 1960, 0, false),
            MarchGain(7.2, 1257, 2),
            Frame(territory, 8, [Actor(100, 0, new(0, 0, 20), rotation: MathF.PI / 2)], party: party),
            StatusEvent(9.3, 100, 1257, 0, false),
            Frame(territory, 20, [Actor(1, 0x35BF, new(30, 0, 40), combat: true)], party: party),
            Frame(territory, 21, [Actor(1, 0x35BF, new(30, 0, 40), combat: true,
                cast: Cast(27231, 6, new(30, 0, 40), startedAt: 21))], party: party),
            Frame(territory, 28, [Actor(1, 0x35BF, new(30, 0, 40), combat: true)], party: party),
            Frame(territory, 29, [], party: party)
        ]);
        controller.Seek(1d);
        Check(dangers() == 1 && MarchPreviews().Length == 4,
            "Yilan lost its single MiniLight danger or long-delay two-segment march drawing.");
        controller.Seek(6.5d);
        var circle = previews().Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("/prediction"));
        dynamic geometry = circle.GetType().GetProperty("Geometry")!.GetValue(circle)!;
        Check((bool)geometry.Contains(new Vector3(10, 0, 20), 0f) && MarchPreviews().Length == 4,
            "Soundstorm ending removed the dynamic prediction or pending status chain.");
        controller.Seek(8.5d);
        Check(MarchPreviews().Length == 2, "Active forced status was not drawn as one remaining segment.");
        controller.Seek(10d);
        Check(MarchPreviews().Length == 0, "Active status loss retained its projection.");
        controller.Seek(20.5d);
        Check(dangers() == 1, "Estimated MiniLight clock incorrectly expired its still-pending danger.");
        controller.Seek(21.5d);
        Check(dangers() == 1 && previews().Length == 2, "Actual MiniLight duplicated its earlier prediction.");
        controller.Seek(28.5d);
        Check(dangers() == 0, "Actual MiniLight finish retained output.");
        controller.Seek(1d);
        Check(MarchPreviews().Length == 4 && dangers() == 1, "Yilan reverse Seek lost the march/prediction history.");
        stop();

        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x35DB, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x35DB, Vector3.Zero, combat: true, cast: Cast(27331, 12, Vector3.Zero))], party: party),
            Frame(territory, 13, [Actor(1, 0x35DB, new(10, 0, 20), combat: true)], party: party),
            Frame(territory, 14, [Actor(1, 0x35DB, new(10, 0, 20), combat: true, cast: Cast(999, 1, Vector3.Zero, startedAt: 14))], party: party),
            Frame(territory, 16, [Actor(1, 0x35DB, Vector3.Zero, combat: true)], party: party),
            EffectEvent(17, 1, 27333, 1),
            Frame(territory, 18, [Actor(1, 0x35DB, new(30, 0, 40), rotation: MathF.PI / 2, combat: true)], party: party),
            EffectEvent(25, 1, 27519, 2), EffectEvent(26, 1, 27516, 3),
            Frame(territory, 27, [], party: party)
        ]);
        controller.Seek(1d);
        Check(dangers() == 1 && previews().Length == 2, "EmptyPromise exposed both stages at once.");
        controller.Seek(13.5d);
        var donut = previews().Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("/promise"));
        dynamic ring = donut.GetType().GetProperty("Geometry")!.GetValue(donut)!;
        Check(dangers() == 1 && !(bool)ring.Contains(new Vector3(10, 0, 20), 0f)
            && (bool)ring.Contains(new Vector3(20, 0, 20), 0f), "Promise finish lost its following live-origin donut.");
        controller.Seek(16.5d);
        Check(dangers() == 0, "Unlisted primary cast finish failed to consume the queue.");
        controller.Seek(25.5d);
        var beam = previews().Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("/beam"));
        dynamic ray = beam.GetType().GetProperty("Geometry")!.GetValue(beam)!;
        Check(dangers() == 1 && (bool)ray.Contains(new Vector3(60, 0, 40), 0f)
            && !(bool)ray.Contains(new Vector3(30, 0, 60), 0f), "VanishingRay timed out, consumed visual End, or lost live pose.");
        controller.Seek(26.5d);
        Check(dangers() == 0, "Actual VanishingRay failed to clear.");
        controller.Seek(13.5d);
        Check(dangers() == 1, "NarrowRift reverse Seek lost its second stage.");
        stop();
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x35DB, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x35DB, Vector3.Zero, combat: true, cast: Cast(27331, 12, Vector3.Zero))], party: party),
            Frame(territory, 13, [Actor(1, 0x35DB, Vector3.Zero, combat: true)], party: party),
            CastEvent(14, 1, 999, 1, Vector3.Zero),
            JsonSerializer.Serialize(new { t = 15, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = 1UL, ActionId = 999, Timestamp = Start.AddSeconds(15) } }),
            Frame(territory, 16, [], party: party)
        ]);
        controller.Seek(14.5d);
        Check(dangers() == 1, "Unknown packet-only cast unexpectedly removed the pending promise before its ending.");
        controller.Seek(15.5d);
        Check(dangers() == 0, "Unknown packet-only cast ending failed to consume the pending promise.");
        stop();
        Console.WriteLine("PASS HuntMarch actual DLL Yilan and NarrowRift state-driven pending/active projections, dynamic predicted hazards, unknown-cast queue ending, beam effects, reverse Seek and cleanup");
    }

    private static string MarchGain(double at, uint status, float duration) => JsonSerializer.Serialize(new
    {
        t = at, kind = "StatusAdd", type = "PromeRotation.LogSystem.LogSystemStatusAddEvent",
        data = new { TargetId = 100UL, SourceId = 1UL, StatusId = status, Duration = duration, Timestamp = Start.AddSeconds(at) }
    });
}
