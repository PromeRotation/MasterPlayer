using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntIxtab(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        string? Text(string name) => (string?)Value(Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script"), name);
        object[] Queue() => previews().Where(p => ((string)Value(p, "Id")).Contains("/ixtab/dual/")).ToArray();
        object[] Baits() => previews().Where(p => ((string)Value(p, "Id")).Contains("/bait/")).ToArray();
        const uint territory = 1187;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 2, EntityId = 102ul }];
        foreach (var action in new uint[] { 17843, 17999 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x2838, default, combat: true), Actor(999, 0x233C, default),
                    Actor(100, 0, default)], full: true, party: party),
                Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(18077, 3, default))], party: party),
                Frame(territory, 1, [Actor(999, 0x233C, default)], party: party),
                Frame(territory, 2, [Actor(999, 0x233C, default, cast: Cast(action, 5, new(10, 0, 20), startedAt: 2))], party: party),
                Frame(territory, 3, [Actor(999, 0x233C, default)], party: party),
                EffectEvent(20, 999, 18074, 1), EffectEvent(20.01, 999, 18074, 1),
                EffectEvent(22, 1, 18075, 2),
                Frame(territory, 23, [], removed: [1], party: party)
            ]);
            controller.Seek(.5);
            Check(Text("MechanicHintsText")?.Contains("Preparing In/Out or Out/In AOE") == true,
                "Ixtab dual cast lost its independent hint.");
            controller.Seek(1.5);
            Check(Text("MechanicHintsText") == null, "Ixtab cancelled preparation kept the cast hint.");
            controller.Seek(19d);
            Check(Queue().Length == 2 && dangers() == 1, "Ixtab dual flag or pair was consumed by cancel/timeout.");
            controller.Seek(20.5);
            Check(Queue().Length == 1 && dangers() == 1, "Ixtab effect failed head promotion or deduplication.");
            controller.Seek(22.5);
            Check(Queue().Length == 0 && dangers() == 0, "Ixtab final effect retained navigation.");
            controller.Seek(19d);
            Check(Queue().Length == 2 && dangers() == 1, "Ixtab dual reverse Seek retained consumption.");
            stop();
        }
        foreach (var action in new uint[] { 17848, 17847 })
        {
            var crypt = action == 17847;
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x2838, default, combat: true), Actor(999, 0x233C, default),
                    Actor(100, 0, new(10, 0, 20)), Actor(101, 0, new(-10, 0, 20)), Actor(102, 0, new(0, 0, -10))], full: true, party: party),
                Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(action, 3, default, target: 100))], party: party),
                Frame(territory, 1, [Actor(100, 0, new(20, 0, 20))], party: party),
                Frame(territory, 2, [Actor(999, 0x233C, default)], party: party),
                Frame(territory, 3, [Actor(100, 0, new(20, 0, 20), dead: true)], party: party),
                EffectEvent(10, 1, action, 3),
                Frame(territory, 11, [], removed: [1], party: party)
            ]);
            controller.Seek(1.5);
            Check(Baits().Length == 1 && dangers() == 2 && Text("PredictedDamageText") == (crypt ? "Raidwide：1人/1次" : "Tankbuster：1人/1次"),
                "Ixtab bait lost personal navigation or expanded target-only damage.");
            controller.Seek(2.5);
            Check(Baits().Length == (crypt ? 1 : 0) && dangers() == (crypt ? 2 : 0)
                && Text("MechanicHintsText")?.Contains("Cone reduces health") != true,
                "Ixtab Abyss/ Crypt Finish semantics or cast hint lifetime differ.");
            controller.Seek(9d);
            Check(Baits().Length == (crypt ? 1 : 0) && Text("PredictedDamageText") == (crypt ? "Raidwide：1人/1次" : null),
                "Ixtab Crypt dead target or delayed effect lost bait/prediction.");
            controller.Seek(10.5);
            Check(Baits().Length == 0 && dangers() == 0, "Ixtab Crypt effect failed all-source cleanup.");
            stop();
        }
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x2838, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, default, classJob: 24), Actor(101, 0, new(0, 0, 10)), Actor(102, 0, new(0, 0, -10))], full: true, party: party),
            StatusEvent(.1, 100, 1769, 0, true),
            StatusEvent(.2, 100, 1769, 0, true),
            StatusEvent(1, 100, 1769, 0, false),
            StatusEvent(3, 100, 1769, 0, false),
            Frame(territory, 4, [Actor(999, 0x233C, default, cast: Cast(17844, 5, default, target: 101, startedAt: 4))], party: party),
            Frame(territory, 5, [Actor(999, 0x233C, default)], party: party),
            Frame(territory, 6, [], removed: [1], party: party)
        ]);
        controller.Seek(2d);
        Check(Text("MechanicHintsText")?.Contains("Heal yourself to full! (Doom).") == true && dangers() == 0
            && Text("PredictedDamageText") == null, "Ixtab Doom duplicate Gain/one Lose lost healer hint or invented gameplay output.");
        controller.Seek(3.5);
        Check(Text("MechanicHintsText") == null, "Ixtab second Doom Lose retained hint.");
        controller.Seek(4.5);
        Check(previews().Count(p => ((string)Value(p, "Id")).Contains("/stack/preview/")) == 1
            && dangers() == 1 && Text("PredictedDamageText") == null, "Ixtab Meteor lost navigation/outline or invented Shared damage.");
        controller.Seek(5.5);
        Check(dangers() == 0, "Ixtab Meteor real Finish retained navigation.");
        controller.Seek(2d);
        Check(Text("MechanicHintsText")?.Contains("Heal yourself to full! (Doom).") == true, "Ixtab Doom reverse Seek failed reconstruction.");
        controller.Seek(6.1);
        Check(previews().Length == 0 && dangers() == 0 && Text("MechanicHintsText") == null, "Ixtab owner removal retained output.");
        stop();
        foreach (var action in new uint[] { 17848, 17844 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x2838, default, combat: true), Actor(999, 0x233C, default),
                    Actor(100, 0, default), Actor(101, 0, new(0, 0, 10))], full: true, party: party),
                Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(action, 5, default, target: 100, startedAt: .1))], party: party),
                Frame(territory, 1, [Actor(999, 0x233C, default, cast: Cast(action, 5, default, target: 101, startedAt: .1))], party: party),
                Frame(territory, 1, [], removed: [999], party: party),
                Frame(territory, 2, [], party: party)
            ]);
            controller.Seek(1.5);
            Check(Baits().Length == 0 && !previews().Any(p => ((string)Value(p, "Id")).Contains("/stack/preview/"))
                && dangers() == 0 && Text("PredictedDamageText") == null,
                "Ixtab same-frame raw replacement/helper removal failed generation-specific Finish.");
            stop();
        }
        string DoomEvent(double offset, double eventAt, bool present, float duration = 0)
            => JsonSerializer.Serialize(new
            {
                t = offset, kind = present ? "StatusAdd" : "StatusRemove",
                type = "PromeRotation.LogSystem.LogSystemStatus" + (present ? "Add" : "Remove") + "Event",
                data = new { TargetId = 100ul, SourceId = 0ul, StatusId = 1769u, Param = 0,
                    Duration = duration, Timestamp = Start.AddSeconds(eventAt) }
            });
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x2838, default, combat: true),
                Actor(100, 0, default, classJob: 24)], full: true, party: party),
            // A complete A lifetime was missed by sampling; B and its C refresh were sampled first.
            Frame(territory, 12,
                [Actor(100, 0, default, classJob: 24, stackedStatus: 1769, statusParam: 0,
                    statusExpiresAt: Start.AddSeconds(35))], party: party),
            DoomEvent(12.1, 0, true, 25), DoomEvent(12.2, 1, false),
            Frame(territory, 13,
                [Actor(100, 0, default, classJob: 24, stackedStatus: 1769, statusParam: 0,
                    statusExpiresAt: Start.AddSeconds(45))], party: party),
            DoomEvent(13.1, 10, true, 25),
            Frame(territory, 20, [Actor(100, 0, default, classJob: 24)], party: party),
            DoomEvent(20.1, 13, true, 32), DoomEvent(20.2, 20, false),
            Frame(territory, 21, [], removed: [1], party: party)
        ]);
        controller.Seek(20.5);
        Check(Text("MechanicHintsText")?.Contains("Heal yourself to full! (Doom).") == true,
            "Ixtab delayed A/B/C status history swallowed a sampled refresh or consumed a confirmed transition twice.");
        stop();
        foreach (var rawFirst in new[] { false, true })
        {
            var history = new List<string>
            {
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x2838, default, combat: true),
                    Actor(100, 0, default, classJob: 24, stackedStatus: 1769, statusParam: 0,
                        statusExpiresAt: Start.AddSeconds(25))], full: true, party: party),
                DoomEvent(.1, 0, true, 25)
            };
            if (!rawFirst)
            {
                history.Add(DoomEvent(1, 1, true, 34));
                history.Add(DoomEvent(2, 2, false));
            }
            history.Add(Frame(territory, 3, [Actor(100, 0, default, classJob: 24)], party: party));
            if (rawFirst)
            {
                history.Add(DoomEvent(3.1, 1, true, 34));
                history.Add(DoomEvent(3.2, 2, false));
            }
            history.Add(Frame(territory, 4, [], party: party));
            load("masterplayer.hunts.simple", history);
            controller.Seek(3.5);
            Check(Text("MechanicHintsText")?.Contains("Heal yourself to full! (Doom).") == true,
                "Ixtab skipped-refresh raw sampling duplicated Lose with " + (rawFirst ? "late" : "early") + " events.");
            controller.Seek(.5);
            Check(Text("MechanicHintsText")?.Contains("Heal yourself to full! (Doom).") == true,
                "Doom merged-transition reverse Seek retained the later sampling baseline.");
            stop();
        }
        Console.WriteLine("PASS Ixtab actual DLL cancelled dual flag and two-stage FIFO, dynamic Abyss/Crypt personal bait navigation and target-only damage, Doom duplicate Gains and healer hints, Meteor target stack, reverse Seek and owner cleanup");
    }
}
