using System.Collections;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntAtticus(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        string? Text(string name)
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return (string?)Value(script, name);
        }
        object[] Breaths() => previews().Where(p => ((string)Value(p, "Id")).Contains("/atticus/")).ToArray();
        string Yell(double at, ushort id) => JsonSerializer.Serialize(new
        {
            t = at, kind = "NpcYell", type = "PromeRotation.LogSystem.LogSystemNpcYellEvent",
            data = new { SourceId = 999, NpcYellId = id, Timestamp = Start.AddSeconds(at) }
        });
        const uint territory = 1187;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 24, EntityId = 102ul }];
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x416D, default, combat: true), Actor(999, 0x233C, new(3, 0, 4), rotation: .2f),
                Actor(100, 0, new(30, 0, 0))], full: true, party: party),
            Yell(.05, 16874), Yell(.06, 16876), Yell(.07, 16875), Yell(.08, 16904),
            Frame(territory, .1, [Actor(999, 0x233C, new(40, 0, 40), cast: Cast(39004, 5, default))], party: party),
            CastEvent(.1, 999, 39004, 5, default),
            Frame(territory, 6, [Actor(999, 0x233C, new(40, 0, 40))], party: party),
            EffectEvent(6.2, 999, 39011, 1),
            EffectEvent(8.5, 999, 39003, 2),
            EffectEvent(10.8, 999, 39010, 3),
            Frame(territory, 12, [], removed: [1], party: party),
        ]);
        controller.Seek(1d);
        Check(Breaths().Length == 2 && dangers() == 2, "Atticus first-two previews or second risky navigation missing.");
        dynamic snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        var deadlines = ((IEnumerable)snapshot.DangerAreas).Cast<object>()
            .SelectMany(a => ((IEnumerable)Value(a, "DangerWindows")).Cast<object>())
            .Select(w => (DateTime)Value(w, "DangerFrom")).Order().ToArray();
        var firstDeadline = Start.AddSeconds(.1).AddSeconds(5d + .3d);
        Check(deadlines.SequenceEqual(new[] { firstDeadline, firstDeadline.AddSeconds(2.3d) }),
            "Atticus cast retiming did not preserve exact native sequence deadlines: "
            + string.Join(", ", deadlines.Select(d => (d - Start).Ticks)));
        controller.Seek(6.1);
        Check(Breaths().Length == 2 && dangers() == 2, "Atticus observed Finish consumed an effect-only sequence.");
        controller.Seek(6.3);
        Check(Breaths().Length == 2 && dangers() == 2, "Atticus helper effect failed to promote the hidden third breath.");
        controller.Seek(8.6);
        Check(Breaths().Length == 1 && dangers() == 1, "Atticus second effect did not advance FIFO.");
        controller.Seek(10.9);
        Check(Breaths().Length == 0 && dangers() == 0, "Atticus final breath remained after real effect.");
        controller.Seek(1d);
        Check(Breaths().Length == 2 && dangers() == 2, "Atticus reverse Seek retained future sequence consumption.");
        stop();

        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x416D, default, combat: true), Actor(100, 0, new(30, 0, 0)),
                Actor(101, 0, default), Actor(102, 0, new(3, 0, 0)),
                Actor(998, 0x233C, default), Actor(999, 0x233C, default)], full: true, party: party),
            Frame(territory, .1, [Actor(998, 0x233C, default, cast: Cast(39013, 5, default, target: 101))], party: party),
            Frame(territory, .2, [Actor(999, 0x233C, default, cast: Cast(39013, 5, default, target: 101, startedAt: .2))], party: party),
            Frame(territory, 1, [Actor(999, 0x233C, default)], party: party),
            Frame(territory, 2, [Actor(999, 0x233C, default, cast: Cast(39012, 3, default, startedAt: 2))], party: party),
            Frame(territory, 6, [Actor(998, 0x233C, default), Actor(999, 0x233C, default,
                cast: Cast(39014, 4, default, startedAt: 6))], party: party),
            Frame(territory, 7, [], removed: [1], party: party),
        ]);
        controller.Seek(.5);
        Check(previews().Count(p => ((string)Value(p, "Id")).Contains("/stack/preview/")) == 2
            && dangers() == 1 && Text("PredictedDamageText") == null,
            "Atticus helper stacks did not share one native R6 union or invented Shared damage.");
        controller.Seek(1.5);
        Check(previews().Count(p => ((string)Value(p, "Id")).Contains("/stack/preview/")) == 1 && dangers() == 1,
            "Atticus Finished failed to remove the first matching target across helpers.");
        controller.Seek(2.5);
        Check(Text("MechanicHintsText")?.Contains("Applies Haste") == true && Text("PredictedDamageText") == null,
            "Atticus Brutality lost its pure hint contract.");
        controller.Seek(6.5);
        Check(dangers() == 0 && Text("PredictedDamageText")?.Contains("Raidwide") == true,
            "Atticus Intimidation lost damage prediction or created a raidwide danger circle.");
        controller.Seek(7.1);
        Check(previews().Length == 0 && dangers() == 0 && Text("PredictedDamageText") == null,
            "Atticus owner removal retained auxiliary caster output.");
        stop();
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x416D, default, combat: true), Actor(100, 0, new(30, 0, 0)),
                Actor(101, 0, default), Actor(999, 0x233C, default, cast: Cast(39013, 8, default, target: 101, startedAt: 0))],
                full: true, party: party),
            Frame(territory, 1, [Actor(2, 0x416D, default, combat: true)], party: party),
            Frame(territory, 2, [], removed: [1], party: party),
            Frame(territory, 3, [], removed: [999], party: party),
            Frame(territory, 4, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(previews().Count(p => ((string)Value(p, "Id")).Contains("/stack/preview/")) == 2 && dangers() == 2,
            "Atticus new owner failed to receive an existing helper cast.");
        controller.Seek(2.5);
        Check(previews().Count(p => ((string)Value(p, "Id")).Contains("/stack/preview/")) == 1 && dangers() == 1,
            "Atticus surviving owner lost its shared helper stack.");
        controller.Seek(3.5);
        Check(!previews().Any(p => ((string)Value(p, "Id")).Contains("/stack/preview/")) && dangers() == 0,
            "Atticus snapshot helper removal retained stack output: "
            + string.Join(", ", previews().Select(p => Value(p, "Id"))) + $"; dangers={dangers()}");
        controller.Seek(1.5);
        Check(previews().Count(p => ((string)Value(p, "Id")).Contains("/stack/preview/")) == 2,
            "Atticus reverse Seek failed to rebuild both owners.");
        stop();
        Console.WriteLine("PASS HuntAtticus actual DLL yell pose history, two risky breath previews, exact retiming, auxiliary effect FIFO, R6 stack union, pure Haste hint, raidwide prediction, new-owner replay, helper removal, reverse Seek and owner cleanup");
    }
}
