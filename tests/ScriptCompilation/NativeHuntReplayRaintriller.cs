using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntRaintriller(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        string? Text(string name)
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return (string?)Value(script, name);
        }
        object[] Rain() => previews().Where(p => ((string)Value(p, "Id")).Contains("/raintriller/")).ToArray();
        const uint territory = 1187;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 2, EntityId = 102ul }];
        string Yell(double at, ushort id) => JsonSerializer.Serialize(new
        {
            t = at, kind = "NpcYell", type = "PromeRotation.LogSystem.LogSystemNpcYellEvent",
            data = new { SourceId = 999, NameId = 13442, NpcYellId = id, Timestamp = Start.AddSeconds(at) }
        });
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x457F, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, new(30, 0, 0))], full: true, party: party),
            Yell(.05, 17721),
            Frame(territory, .1, [Actor(1, 0x457F, default, combat: true, cast: Cast(39749, 7.5, default))], party: party),
            CastEvent(.1, 1, 39749, 7.5, default),
            Frame(territory, 8, [Actor(1, 0x457F, default, combat: true)], party: party),
            Frame(territory, 9, [Actor(1, 0x457F, default, combat: true, cast: Cast(39753, 1, default))], party: party),
            Frame(territory, 10.4, [Actor(1, 0x457F, default, combat: true)], party: party),
            Frame(territory, 12, [Actor(1, 0x457F, default, combat: true, cast: Cast(39750, 1, default))], party: party),
            Frame(territory, 13.4, [Actor(1, 0x457F, default, combat: true)], party: party),
            Frame(territory, 15, [Actor(1, 0x457F, default, combat: true, cast: Cast(39752, 1, default))], party: party),
            Frame(territory, 16.4, [Actor(1, 0x457F, default, combat: true)], party: party),
            Frame(territory, 17, [], removed: [1], party: party),
        ]);
        controller.Seek(1d);
        Check(Rain().Length == 2 && dangers() == 1 && Text("MechanicHintsText")?.Contains("Out -> In -> Back") == true,
            "Raintriller NpcYell cache, full sequence hint or first-two visibility missing.");
        controller.Seek(8.1);
        Check(Rain().Length == 2 && dangers() == 1, "Visual finish consumed Raintriller sequence.");
        controller.Seek(10.5);
        Check(Rain().Length == 2 && dangers() == 2 && Text("MechanicHintsText")?.Contains("In -> Back") == true,
            "Unmatched short finish failed to pop FIFO and promote hidden cone.");
        controller.Seek(13.5);
        Check(Rain().Length == 1 && dangers() == 1 && Text("MechanicHintsText")?.Contains("Back") == true,
            "Raintriller final cone did not promote.");
        controller.Seek(16.5);
        Check(Rain().Length == 0 && dangers() == 0, "Raintriller third finish failed to consume queue.");
        controller.Seek(1d);
        Check(Rain().Length == 2 && dangers() == 1, "Raintriller reverse Seek retained future queue consumption.");
        stop();

        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x457F, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, new(30, 0, 0))], full: true, party: party),
            Yell(.05, 17721),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(39758, 5, default))], party: party),
            CastEvent(.1, 999, 39758, 5, default),
            Frame(territory, 6, [Actor(999, 0x233C, default)], party: party),
            Frame(territory, 7, [Actor(999, 0x233C, default, cast: Cast(39750, 1, default))], party: party),
            Frame(territory, 8.4, [Actor(999, 0x233C, default)], party: party),
            Frame(territory, 9, [], removed: [1], party: party),
        ]);
        controller.Seek(1d);
        Check(Rain().Length == 2 && dangers() == 1 && Text("MechanicHintsText")?.Contains("Out -> In -> Back") == true,
            "Raintriller helper raw start and packet confirmation failed to release exactly one sequence.");
        controller.Seek(8.5);
        Check(Rain().Length == 2 && dangers() == 2 && Text("MechanicHintsText")?.Contains("In -> Back") == true,
            "Raintriller observed helper finish did not advance the FIFO exactly once.");
        controller.Seek(9.1);
        Check(Rain().Length == 0 && dangers() == 0, "Raintriller removal retained helper predictions.");
        stop();

        foreach (var removed in new[] { false, true })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x457F, default, combat: true), Actor(999, 0x233C, default),
                    Actor(100, 0, new(30, 0, 0))], full: true, party: party),
                Yell(.05, 17721),
                Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(39749, 5, default))], party: party),
                Frame(territory, 6, [Actor(999, 0x233C, default, cast: Cast(39750, 1, default))], party: party),
                removed ? Frame(territory, 7, [], removed: [999], party: party)
                    : Frame(territory, 7, [Actor(999, 0x233C, default, cast: Cast(9999, 1, default))], party: party),
                Frame(territory, 8, [], removed: [1], party: party),
            ]);
            controller.Seek(7.1);
            Check(Rain().Length == 2 && dangers() == 2 && Text("MechanicHintsText")?.Contains("In -> Back") == true,
                $"Raintriller helper {(removed ? "removal" : "unrelated replacement")} lost its final short Finish.");
            stop();
        }

        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x457F, default, combat: true), Actor(100, 0, new(20, 0, 0)),
                Actor(101, 0, default), Actor(102, 0, default)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x457F, default, combat: true, cast: Cast(39754, 5, default, target: 101))], party: party),
            Frame(territory, 2, [], removed: [102], party: party),
            Frame(territory, 6, [Actor(1, 0x457F, default, combat: true)], party: party),
            Frame(territory, 7, [], removed: [1], party: party),
        ]);
        controller.Seek(1d);
        Check(dangers() == 1 && Text("MechanicHintsText") == null && Text("PredictedDamageText") == null,
            "Raintriller min2 stack inherited min8 or Shared.");
        controller.Seek(2.1);
        Check(Text("MechanicHintsText")?.Contains("Stack!") == true, "Raintriller unsatisfied min2 stack hint missing.");
        controller.Seek(6.1);
        Check(dangers() == 0, "Raintriller real stack finish retained navigation.");
        stop();
        Console.WriteLine("PASS HuntRaintriller actual DLL NPC yell and casts from existing helpers, packet confirmation, three hidden-stage FIFO and promotion, min-two stack, full hint, reverse Seek and cleanup");
    }
}
