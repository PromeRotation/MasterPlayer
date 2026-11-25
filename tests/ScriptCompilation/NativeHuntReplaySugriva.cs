using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntSugriva(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        string? Text(string name) => (string?)Value(Script(), name);
        object[] Group(string name) => previews().Where(p => ((string)Value(p, "Id")).Contains(name)).ToArray();
        DateTime[] Deadlines()
        {
            dynamic runtime = runtimeObject;
            return ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>()
                .SelectMany(a => ((IEnumerable)Value(a, "DangerWindows")).Cast<object>())
                .Select(w => (DateTime)Value(w, "DangerFrom")).Order().ToArray();
        }
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld")
                && !(bool)Value(Script(), "PyreticActive"),
                "Sugriva invented facing/action/movement hold or retained a replay execution request.");
        }
        string PreyStatus(double lineAt, double eventAt, ulong target, bool present) => JsonSerializer.Serialize(new
        {
            t = lineAt, kind = present ? "StatusAdd" : "StatusRemove",
            type = "PromeRotation.LogSystem.LogSystemStatus" + (present ? "Add" : "Remove") + "Event",
            data = new { TargetId = target, SourceId = 0ul, StatusId = 2939u, Param = 0,
                Duration = 30f, Timestamp = Start.AddSeconds(eventAt) }
        });
        const uint territory = 817;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 2, EntityId = 102ul }];
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x35FC, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, new(0, 0, 8)), Actor(101, 0, default), Actor(102, 0, new(0, 0, 5))], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(27219, 5, default, target: 101))], party: party),
            Frame(territory, 1, [Actor(100, 0, new(18, 0, 0)), Actor(101, 0, new(10, 0, 0))], party: party),
            Frame(territory, 2, [Actor(999, 0x233C, default)], party: party),
            Frame(territory, 3, [], removed: [1], party: party)
        ]);
        controller.Seek(.5);
        Check(Group("/stack/preview/").Length == 1 && Group("knockback/").Length == 2 && dangers() == 1
            && Text("PredictedDamageText") == null && Text("MechanicHintsText")?.Contains("Stack and knockback") == true,
            "Twister lost independent stack navigation/knockback drawing or invented Shared prediction.");
        Check(Deadlines().Single() == Start.AddSeconds(.1 + 5 + .3),
            "Twister stack activation was not delivered to the production navigation snapshot.");
        controller.Seek(1.5);
        dynamic landing = Value(Group("knockback/").Single(p => ((string)Value(p, "Id")).EndsWith("/landing")), "Geometry");
        Check((Vector3)landing.Position == new Vector3(38, 0, 0), "Twister did not follow the target's real current position.");
        Isolated();
        controller.Seek(2.5);
        Check(Group("/stack/preview/").Length == 0 && Group("knockback/").Length == 0 && dangers() == 0,
            "Twister actual Finish retained either its stack or independent knockback slot.");
        controller.Seek(.5);
        Check(Group("knockback/").Length == 2 && dangers() == 1, "Twister reverse Seek did not rebuild output.");
        controller.Seek(3.5);
        Check(previews().Length == 0 && dangers() == 0, "Twister owner removal retained output.");
        stop();

        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x35FC, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, new(-10, 0, 0)), Actor(101, 0, new(0, 0, 5)), Actor(102, 0, new(0, 0, 10))], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(27223, 5, default, target: 101))], party: party),
            Frame(territory, 1, [Actor(999, 0x233C, new(2, 0, 0)), Actor(101, 0, new(12, 0, 0))], party: party),
            Frame(territory, 2, [Actor(999, 0x233C, new(2, 0, 0),
                cast: Cast(27223, 5, default, target: 102, startedAt: 2))], party: party),
            EffectEvent(3, 999, 27223, 1), EffectEvent(3.01, 999, 27223, 1),
            Frame(territory, 4, [Actor(102, 0, new(0, 0, 10), dead: true)], party: party),
            Frame(territory, 5, [], removed: [1], party: party)
        ]);
        controller.Seek(1.5);
        dynamic cone = Value(Group("/bait/").Single(), "Geometry");
        Check((Vector3)cone.Position == new Vector3(2, 0, 0) && MathF.Abs((float)cone.Rotation - MathF.PI / 2) < .00001f
            && dangers() == 1 && Text("PredictedDamageText")?.Contains("Tankbuster") == true,
            "Butcher Finish removed a bait or its live target direction/first prediction changed.");
        controller.Seek(2.5);
        Check(Group("/bait/").Length == 2 && dangers() == 2, "Second Butcher Start overwrote the original active bait.");
        controller.Seek(3.5);
        Check(Group("/bait/").Length == 1 && dangers() == 1, "Butcher duplicate Effect removed more than the matching source's first bait.");
        controller.Seek(4.5);
        Check(Group("/bait/").Length == 0 && dangers() == 0, "Butcher target death failed real component cleanup.");
        controller.Seek(1.5);
        Check(Group("/bait/").Length == 1, "Butcher reverse Seek retained a future death/effect.");
        Isolated();
        stop();

        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x35FC, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, new(-10, 0, 0)), Actor(101, 0, new(0, 0, 5)), Actor(102, 0, new(0, 0, 10))], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(27229, .5, default, target: 101))], party: party),
            Frame(territory, 1, [Actor(999, 0x233C, default), Actor(101, 0, new(0, 0, 5),
                stackedStatus: 2939, statusParam: 0, statusExpiresAt: Start.AddSeconds(30))], party: party),
            Frame(territory, 2, [Actor(101, 0, new(0, 0, 5))], party: party),
            Frame(territory, 3, [Actor(999, 0x233C, default,
                cast: Cast(27229, .5, default, target: 102, startedAt: 3))], party: party),
            PreyStatus(3.1, 2, 101, false),
            Frame(territory, 4, [Actor(999, 0x233C, default), Actor(102, 0, new(0, 0, 10),
                stackedStatus: 2939, statusParam: 0, statusExpiresAt: Start.AddSeconds(34))], party: party),
            Frame(territory, 5, [Actor(102, 0, new(0, 0, 10))], party: party),
            Frame(territory, 6, [Actor(999, 0x233C, default,
                cast: Cast(27229, .5, default, target: 101, startedAt: 6))], party: party),
            Frame(territory, 7, [Actor(999, 0x233C, default,
                cast: Cast(27226, 1.6, new(10, 0, 20), startedAt: 7))], party: party),
            Frame(territory, 8, [Actor(999, 0x233C, default)], party: party),
            Frame(territory, 9, [Actor(999, 0x233C, default,
                cast: Cast(27226, 1.6, new(20, 0, 30), startedAt: 9))], party: party),
            Frame(territory, 10, [], removed: [1], party: party)
        ]);
        controller.Seek(.5);
        Check(Group("/bait/").Length == 1 && dangers() == 1 && Text("PredictedDamageText") == null
            && Deadlines().Single() == Start.AddSeconds(.1 + .5 + .3).AddSeconds(1.1d),
            "Prey lost target navigation or precise F+1.1, or invented damage prediction.");
        controller.Seek(2.5);
        Check(Group("/bait/").Length == 0 && dangers() == 0, "Raw Prey loss failed to clear its complete bait list.");
        controller.Seek(3.5);
        Check(Group("/bait/").Length == 1 && dangers() == 1, "Late old raw Lose confirmation cleared the new Prey bait.");
        controller.Seek(5.5);
        Check(Group("/bait/").Length == 0, "The next real Prey loss was consumed as a duplicate.");
        controller.Seek(8.5);
        Check(Group("/bait/").Length == 1, "First RockThrowRest Start or Finish cleared the independent Prey bait.");
        controller.Seek(9.5);
        Check(Group("/bait/").Length == 0, "Second RockThrowRest Start failed to clear all Prey baits.");
        controller.Seek(3.5);
        Check(Group("/bait/").Length == 1, "Prey reverse Seek retained future loss or counter history.");
        Isolated();
        controller.Seek(10.5);
        Check(previews().Length == 0 && dangers() == 0, "Sugriva last owner retained drawing or navigation.");
        stop();
        foreach (var removeOld in new[] { false, true })
        {
            // A present actor after the helper loses its status after that helper's Start;
            // a removed actor is destroyed before every actor's current cast is applied.
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x35FC, default, combat: true), Actor(999, 0x233C, default),
                    Actor(101, 0, new(0, 0, 5), stackedStatus: 2939, statusParam: 0),
                    Actor(100, 0, new(-10, 0, 0)), Actor(102, 0, new(0, 0, 10))], full: true, party: party),
                Frame(territory, 1, [Actor(999, 0x233C, default, cast: Cast(27229, .5, default, target: 102, startedAt: 1)),
                    Actor(100, 0, new(-10, 0, 0)), Actor(102, 0, new(0, 0, 10)),
                    .. (removeOld ? Array.Empty<object>() : new[] { Actor(101, 0, new(0, 0, 5)) })],
                    removed: removeOld ? [101] : [], party: party)
            ]);
            controller.Seek(1.5);
            Check(Group("/bait/").Length == (removeOld ? 1 : 0),
                "Same-frame Prey destruction/status loss and new Start were reordered.");
            stop();
        }
        Console.WriteLine("PASS Sugriva actual DLL independent R8 stack and dynamic 20y knockback, Butcher live target/source Effect FIFO, Prey F+1.1 and raw/event loss receipts, second rock Start, reverse Seek and owner cleanup");
    }
}
