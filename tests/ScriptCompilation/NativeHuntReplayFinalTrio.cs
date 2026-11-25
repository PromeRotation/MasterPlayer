using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntFinalTrio(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(obj)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        string? Text() => (string?)Value(Script(), "MechanicHintsText");
        object[] Group(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld") && !(bool)Value(Script(), "PyreticActive"),
                "Final trio expanded SpecialMode diagnostics into execution or leaked replay control.");
        }
        string State(double t, uint sid, bool present, float duration = 10) => JsonSerializer.Serialize(new
        {
            t, kind = present ? "StatusAdd" : "StatusRemove",
            type = "PromeRotation.LogSystem.LogSystemStatus" + (present ? "Add" : "Remove") + "Event",
            data = new { TargetId = 100ul, SourceId = 0ul, StatusId = sid, Param = 3, Duration = duration, Timestamp = Start.AddSeconds(t) }
        });
        const uint territory = 817;
        const string guid = "masterplayer.hunts.simple";
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object[] players = [Actor(100, 0, new(0, 0, -10)), Actor(101, 0, new(0, 0, 10))];

        load(guid,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4544, default, combat: true), .. players,
                Actor(999, 0x233C, default), Actor(998, 0x233C, default), Actor(997, 0x233C, default)], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(39509, 5, default, rotation: .4f))], party: party),
            Frame(territory, .2, [Actor(998, 0x233C, default, cast: Cast(39510, 5, new(20, 0, 10), rotation: .8f, startedAt: .2))], party: party),
            Frame(territory, 1, [Actor(998, 0x233C, default)], party: party),
            EffectEvent(1.1, 997, 39514, 1),
            Frame(territory, 2, [Actor(997, 0x233C, default, cast: Cast(39514, 1, default, startedAt: 2))], party: party),
            Frame(territory, 3, [Actor(997, 0x233C, default)], party: party),
            Frame(territory, 50, [], party: party), Frame(territory, 51, [], removed: [1], party: party),
        ]);
        controller.Seek(.3);
        Check(Group("soulless/").Length == 4 && dangers() == 4, "Arch all stages did not reach dual view and navigation.");
        controller.Seek(1.2);
        Check(Group("soulless/").Length == 3 && dangers() == 3, "Arch foreign Finish or Effect consumed wrong number.");
        controller.Seek(3.2);
        Check(Group("soulless/").Length == 2 && dangers() == 2, "Short Finish failed global FIFO.");
        controller.Seek(50.2);
        Check(Group("soulless/").Length == 2, "Arch prediction expired without true Finish.");
        controller.Seek(.3);
        Check(Group("soulless/").Length == 4, "Arch reverse Seek failed.");
        Isolated();
        controller.Seek(51.2);
        Check(previews().Length == 0 && dangers() == 0, "Arch owner removal retained output.");
        stop();
        Console.WriteLine("PASS native hunt production ArchAethereater continuous all-stage dual views actual navigation global Finish FIFO effect isolation locked clocks cancellation reverse Seek owner cleanup");

        load(guid,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x43DD, default, combat: true), .. players,
                Actor(999, 0x233C, default), Actor(997, 0x233C, default)], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(39292, 5, new(10, 0, 20), rotation: MathF.PI / 2))], party: party),
            Frame(territory, 1, [Actor(999, 0x233C, default)], party: party),
            EffectEvent(2, 997, 39557, 1), EffectEvent(2.01, 997, 39557, 1),
            EffectEvent(3, 997, 39294, 2),
            Frame(territory, 50, [], party: party), Frame(territory, 51, [], removed: [1], party: party),
        ]);
        controller.Seek(.2);
        Check(Group("twinscorch/").Length == 2 && dangers() == 1, "Sansheya cone pair reference risk not preserved.");
        controller.Seek(1.2);
        Check(Group("twinscorch/").Length == 2 && dangers() == 1, "Visual Finish cleared effect queue.");
        controller.Seek(2.2);
        Check(Group("twinscorch/").Length == 2 && dangers() == 2, "Future circle did not enter navigation or duplicate advanced twice.");
        controller.Seek(3.2);
        Check(Group("twinscorch/").Length == 1 && dangers() == 1, "Nonexpected effect failed FIFO.");
        controller.Seek(50.2);
        Check(Group("twinscorch/").Length == 1, "Final circle expired without effect.");
        controller.Seek(.2);
        Check(dangers() == 1, "Sansheya reverse Seek failed risk rebuild.");
        Isolated();
        controller.Seek(51.2);
        Check(previews().Length == 0 && dangers() == 0, "Sansheya owner removal retained output.");
        stop();
        Console.WriteLine("PASS native hunt production Sansheya two-view two-stage display reference risks future circle navigation exact global effects duplicate isolation visual Finish preservation Seek");

        foreach (var (oid, preparation, resolving, window) in new[] { (0x4544u, 4141u, 960u, 3), (0x4544u, 4142u, 2540u, 3), (0x43DDu, 4140u, 960u, 1000) })
        {
            load(guid,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, default, combat: true), .. players], full: true, party: party),
                State(.1, preparation, true), State(1, preparation, false),
                Frame(territory, 20, [], party: party), State(21, resolving, false), Frame(territory, 22, [], party: party),
            ]);
            controller.Seek(.2);
            Check(Text()?.Contains("SpecialMode:") == true
                && (window == 1000 ? Text()?.Contains("Stop everything!") == true : Text()?.Contains("Stop everything!") != true),
                "Preparation packet expiry/window or mode lost.");
            controller.Seek(1.2);
            Check(Text()?.Contains("SpecialMode:") == true, "Preparation Lose cleared required movement.");
            controller.Seek(20.2);
            Check(Text()?.Contains("SpecialMode:") == true, "Prediction expired without resolving Lose.");
            Isolated();
            controller.Seek(21.2);
            Check(Text()?.Contains("SpecialMode:") != true, "Resolving Lose failed release.");
            controller.Seek(.2);
            Check(Text()?.Contains("SpecialMode:") == true, "StayMove reverse Seek failed.");
            stop();
        }
        Console.WriteLine("PASS native hunt production Arch Heatstroke ColdSweats Sansheya Boiling true preparation expiry persistent Stay Move resolving Lose independent diagnostics windows replay execution isolation Seek");

        foreach (var (oid, preparation, resolving) in new[] { (0x4544u, 4141u, 960u), (0x4544u, 4142u, 2540u), (0x43DDu, 4140u, 960u) })
        {
            foreach (var (oldSlot, newSlot, expected) in new[] { (4, 4, true), (2, 8, true), (8, 2, false) })
            {
                object Occupied(uint sid, int slot)
                {
                    var actor = JsonSerializer.SerializeToNode(Actor(100, 0, new(0, 0, -10)))!;
                    actor["Statuses"] = JsonSerializer.SerializeToNode(new[]
                    {
                        new { Id = sid, Param = 0, SourceId = 1ul, ExpiresAt = Start.AddSeconds(20), SlotIndex = slot }
                    });
                    return actor;
                }
                load(guid,
                [
                    Header(territory), Frame(territory, 0, [Actor(1, oid, default, combat: true),
                        Occupied(resolving, oldSlot), players[1]], full: true, party: party),
                    Frame(territory, .1, [Occupied(preparation, newSlot)], party: party),
                    Frame(territory, 1, [], party: party),
                ]);
                controller.Seek(.2);
                Check((Text()?.Contains("SpecialMode:") == true) == expected, "Native status slot order was lost in recording/readback or raw application.");
                controller.Seek(0);
                Check(Text()?.Contains("SpecialMode:") != true, "Raw slot state leaked over reverse Seek.");
                controller.Seek(.2);
                Check((Text()?.Contains("SpecialMode:") == true) == expected, "Slot order did not rebuild deterministically.");
                Isolated();
                stop();
            }
        }
        Console.WriteLine("PASS native hunt production original status slot recording readback same-slot replacement low-slot Gain Lose order independent Heat Cold Boiling reverse Seek");

        load(guid,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x416F, default, combat: true),
                Actor(100, 0, new(0, 0, 2), stackedStatus: 4071), players[1],
                Actor(999, 0x233C, default), Actor(998, 0x233C, default)], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(39110, 5, new(40, 0, 0)))], party: party),
            Frame(territory, 1, [Actor(1, 0x416F, new(10, 0, 0), combat: true)], party: party),
            Frame(territory, 2, [Actor(999, 0x233C, default)], party: party),
            Frame(territory, 3, [Actor(998, 0x233C, default, cast: Cast(39113, 1, new(99, 0, 99), startedAt: 3))], party: party),
            Frame(territory, 4, [Actor(998, 0x233C, default)], party: party),
            Frame(territory, 50, [], party: party), Frame(territory, 51, [], removed: [1], party: party),
        ]);
        controller.Seek(.2);
        Check(Group("magnetism/aoe").Length == 1 && Group("knockback/").Length == 2 && dangers() == 1,
            "Urna static display/dedicated navigation/projected displacement missing.");
        var aoeOrigin = (Vector3)Value(Value(Group("magnetism/aoe").Single(), "Geometry"), "Position");
        controller.Seek(1.2);
        Check((Vector3)Value(Value(Group("magnetism/aoe").Single(), "Geometry"), "Position") == aoeOrigin
            && (Vector3)Value(Value(Group("knockback/").Single(p => ((string)Value(p, "Id")).EndsWith("/landing")), "Geometry"), "Position") != new Vector3(0, 0, 12),
            "Dynamic primary failed or contaminated static AOE.");
        controller.Seek(2.2);
        Check(Group("knockback/").Length == 0 && Group("magnetism/aoe").Length == 1 && dangers() == 1,
            "Magnetic Finish removed independent circle or kept projection.");
        controller.Seek(3.2);
        Check((Vector3)Value(Value(Group("magnetism/aoe").Single(), "Geometry"), "Position") == aoeOrigin, "Short Start reset circle.");
        controller.Seek(4.2);
        Check(Group("magnetism/aoe").Length == 0 && dangers() == 0, "Ordinary Finish did not clear circle.");
        controller.Seek(.2);
        Check(Group("knockback/").Length == 2 && dangers() == 1, "Urna Seek failed.");
        Isolated();
        controller.Seek(51.2);
        Check(previews().Length == 0 && dangers() == 0, "Urna owner cleanup retained output.");
        stop();
        Console.WriteLine("PASS native hunt production UrnaVariabilis dynamic primary locked AOE independent F plus2.5 display dedicated polarity navigation real ends short Start no reset dual view projection Seek owner cleanup");

        foreach (var (oid, stack) in new[] { (0x4544u, 39515u), (0x43DDu, 39296u), (0x416Fu, 39115u) })
        {
            load(guid,
            [
                Header(territory), Frame(territory, 0, [Actor(1, oid, default, combat: true), .. players,
                    Actor(999, 0x233C, default)], full: true, party: party),
                Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(stack, 5, default, target: 101))], party: party),
                Frame(territory, 1, [Actor(101, 0, new(0, 0, 10), dead: true)], party: party),
                Frame(territory, 2, [Actor(101, 0, new(10, 0, 0))], party: party),
                Frame(territory, 3, [Actor(999, 0x233C, default)], party: party), Frame(territory, 4, [], party: party),
            ]);
            controller.Seek(.2);
            Check(Group("/stack/preview/").Length == 1 && dangers() == 1
                && Value(Script(), "PredictedDamageText") == null, "Stack actual navigation or no-Shared prediction boundary differs.");
            controller.Seek(1.2);
            Check(Group("/stack/preview/").Length == 0 && dangers() == 0, "Dead stack not hidden.");
            controller.Seek(2.2);
            Check(Group("/stack/preview/").Length == 1 && dangers() == 1, "Revived stack failed original record.");
            controller.Seek(3.2);
            Check(Group("/stack/preview/").Length == 0 && dangers() == 0, "Stack true Finish failed.");
            stop();
        }
        Console.WriteLine("PASS native hunt production final trio min8 unlimited stacks actual navigation death revival dynamic target matching Finish no invented Shared prediction");

        load(guid,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x43DD, default, combat: true), .. players,
                Actor(999, 0x233C, default)], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(39285, 5, default, target: 101))], party: party),
            Frame(territory, 1, [Actor(999, 0x233C, new(2, 0, 0), cast: Cast(39285, 5, default, target: 101)),
                Actor(101, 0, new(12, 0, 0))], party: party),
            Frame(territory, 2, [Actor(999, 0x233C, new(2, 0, 0))], party: party), Frame(territory, 3, [], party: party),
        ]);
        controller.Seek(1.2);
        Check(Group("/bait/").Length == 1 && dangers() == 1
            && (string?)Value(Script(), "PredictedDamageText") is { } damage && damage.Contains("Tankbuster"),
            "Dynamic charge missing navigation or target prediction.");
        controller.Seek(2.2);
        Check(Group("/bait/").Length == 0 && dangers() == 0, "Charge source Finish failed.");
        stop();
        Console.WriteLine("PASS native hunt production Sansheya dynamic width6 charge source target movement Tankbuster prediction true Finish replay isolation");
    }
}
