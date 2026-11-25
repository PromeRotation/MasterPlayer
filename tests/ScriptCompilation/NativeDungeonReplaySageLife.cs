using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunSageLife(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        dynamic runtime = runtimeObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Aoes(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix)).ToArray();
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var center = new Vector3(224.8f, 0, -855.8f);
        var towerA = center + Vector3.UnitX * 8;
        var towerB = center - Vector3.UnitX * 8;
        int Obstacles() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
        load("masterplayer.quest.life-ephemeral-path-eternal",
        [
            Header(1023), Frame(1023, 0, [Actor(1, 0x35C5, center, combat: true), Actor(20, 0x35C4, center),
                Actor(100, 0, center + Vector3.UnitZ * 10), Actor(10, 0x3589, towerA, subKind: 5, isEnemy: false)], full: true, party: party),
            Frame(1023, 1, [Actor(1, 0x35C5, center, combat: true, cast: Cast(26832, 5, center, startedAt: 1))], party: party),
            Frame(1023, 2, [Actor(2, 0x233C, center, cast: Cast(26833, 4, center, startedAt: 2))], party: party),
            Frame(1023, 3, [Actor(2, 0x233C, center)], party: party),
            Frame(1023, 4, [Actor(1, 0x35C5, center, combat: true)], party: party),
            Frame(1023, 5, [Actor(3, 0x233C, center, cast: Cast(26834, 4, center, startedAt: 5))], party: party),
            Frame(1023, 6, [], removed: [3], party: party),
            Frame(1023, 7, [Actor(4, 0x233C, towerA, cast: Cast(26844, 8, towerA, startedAt: 7)),
                Actor(5, 0x233C, towerB, cast: Cast(26844, 8, towerB, startedAt: 7))], party: party),
            Frame(1023, 8, [Actor(100, 0, towerA)], party: party),
            Frame(1023, 9, [Actor(5, 0x233C, towerB)], party: party),
            Frame(1023, 10, [Actor(4, 0x233C, towerA)], party: party),
            Frame(1023, 11, [Actor(1, 0x35C5, center, dead: true)], party: party),
            Frame(1023, 12, [], party: party),
        ]);
        controller.Seek(2.5);
        Check(dangers() == 1 && Aoes("sage/rock/").Length == 1 && previews().Length == 3, "Rockbreaker duplicated helper cast AOE.");
        controller.Seek(3.5);
        Check(dangers() == 1 && (DateTime)Value(Aoes("sage/rock/").Single(), "Activation") == Start.AddSeconds(5),
            "Rockbreaker helper ending clock.");
        controller.Seek(4.5);
        Check(dangers() == 1, "Rockbreaker visual cancellation cleared the active ring.");
        controller.Seek(6.5);
        Check(dangers() == 0, "Rockbreaker second helper destruction cleanup.");
        controller.Seek(7.5);
        Check(dangers() == 1 && Obstacles() == 0 && previews().Length == 4 && Damage().Single() is { } damage
            && (ulong)Value(damage, "TargetId") == 10 && (int)Value(damage, "PartySlot") == 24,
            "Sage towers hard entry, two marker/tower previews or NPC damage mask.");
        controller.Seek(8.5);
        Check(Damage().Length == 2 && Aoes("sage/tower/").Single(a => (string)Value(a, "Id") == "sage/tower/4/display") is { } display
            && !(bool)Value(display, "Safe") && (bool)Value(display, "OutlineOnly"), "Sage tower oversoak mask or outline.");
        controller.Seek(9.5);
        Check(dangers() == 1 && Aoes("sage/tower/").Any(a => ((string)Value(a, "Id")).EndsWith("/avoid")), "Sage full tower avoidance branch.");
        controller.Seek(10.5);
        Check(dangers() == 0 && Damage().Length == 0, "Sage actual tower cancellation cleanup.");
        controller.Seek(11.5);
        Check(previews().Length == 0, "Ancel primary death waited for Mahaud.");
        controller.Seek(7.5);
        Check(dangers() == 1 && Damage().Length == 1, "Sage tower reverse Seek.");
        stop();
        Console.WriteLine("PASS production Ancel and Mahaud: visual/helper concentric clocks, cancellation/destruction, NPC single towers, hard entry versus full avoidance, oversoak outlines and damage, dual views, primary death, reverse Seek and cleanup");

        var sourcePosition = center - Vector3.UnitX * 10;
        object Noulith(ulong target) => Actor(2, 0x35C8, sourcePosition, tethers: [new { Id = 17u, TargetId = target }]);
        bool Large() => (bool)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary.Contains(center + Vector3.UnitZ * 15, 0f);
        load("masterplayer.quest.life-ephemeral-path-eternal",
        [
            Header(1023), Frame(1023, 0, [Actor(1, 0x35C6, center, combat: true), Actor(100, 0, center), Noulith(100),
                Actor(10, 0x3589, center, subKind: 5, isEnemy: false), Actor(11, 0x3589, center + Vector3.UnitZ * 5, subKind: 9),
                Actor(20, 0x3859, center), Actor(50, 0x1EA1A1, center, eventState: 7)], full: true, party: party),
            Frame(1023, 1, [Actor(100, 0, center + Vector3.UnitZ * 10)], party: party),
            EffectEvent(2, 999, 26853, 1, target: 100),
            Frame(1023, 3, [Noulith(100)], party: party),
            Frame(1023, 4, [Actor(1, 0x35C6, center, combat: true, cast: Cast(26854, 5, center, startedAt: 4))], party: party),
            Frame(1023, 5, [Actor(1, 0x35C6, center, combat: true)], party: party),
            EffectEvent(6, 1, 26854, 2),
            JsonSerializer.Serialize(new { t = 7, kind = "EObjSetState", type = "PromeRotation.LogSystem.LogSystemEObjSetStateEvent",
                data = new { SourceId = 50ul, State = 7, Timestamp = Start.AddSeconds(7) } }),
            Frame(1023, 8, [Actor(50, 0x1EA1A1, center, eventState: 6)], party: party),
            Frame(1023, 9, [Actor(50, 0x1EA1A1, center, eventState: 7)], party: party),
            EffectEvent(10, 1, 26854, 3),
            Frame(1023, 11, [Noulith(11)], party: party),
            Frame(1023, 12, [], removed: [2], party: party),
            Frame(1023, 13, [Actor(1, 0x35C6, center, dead: true)], party: party),
            Frame(1023, 14, [], party: party),
        ]);
        controller.Seek(.5);
        Check(Large() && dangers() == 1 && previews().Length == 4 && Damage().Single() is { } baitDamage
            && (string)Value(baitDamage, "DamageType") == "Tankbuster", "Guildivain bootstrap tether, NPC avoid cone, line, markers or target prediction.");
        controller.Seek(1.5);
        Check(MathF.Abs((float)Value(Aoes("guildivain/bait/").Single(a => ((string)Value(a, "Id")).EndsWith("/display")), "Rotation")
            - MathF.PI / 4) < .001f, "Guildivain dynamic tether direction.");
        controller.Seek(3.5);
        Check(dangers() == 0 && Aoes("guildivain/bait/").Length == 0, "Guildivain old tether snapshot resurrected consumed bait.");
        controller.Seek(5.5);
        Check(Large(), "Tracheostomy cancellation shrank arena.");
        controller.Seek(7.5);
        Check(!Large(), "Tracheostomy effect or network/native state separation.");
        controller.Seek(9.5);
        Check(Large(), "Native EventState transition failed to restore.");
        controller.Seek(10.5);
        Check(!Large(), "Second Tracheostomy effect failed to shrink.");
        controller.Seek(11.5);
        Check(dangers() == 1 && Aoes("guildivain/bait/").Length == 2, "Buddy target classification or filled other-bait rectangle.");
        controller.Seek(12.5);
        Check(dangers() == 0 && Aoes("guildivain/bait/").Length == 0, "Outgoing source destruction failed to untether.");
        controller.Seek(13.5);
        Check(previews().Length == 0, "Guildivain primary death cleanup.");
        controller.Seek(.5);
        Check(Large() && dangers() == 1, "Guildivain reverse Seek bootstrap.");
        stop();
        Console.WriteLine("PASS production Guildivain: bootstrap/dynamic Player and Buddy tethers, line and self-bait avoidance, target effect and source destruction, native state versus network object state, repeated arena shrink/restore, primary death, dual views, reverse Seek and cleanup");
        load("masterplayer.quest.life-ephemeral-path-eternal",
        [
            Header(1023), Frame(1023, 0, [Actor(1, 0x35C6, center, combat: true), Actor(100, 0, center),
                Actor(2, 0x35C8, sourcePosition)], full: true, party: party),
            ResearchTetherEvent(.1, 2, 100, 17, true),
            Frame(1023, 1, [], removed: [2], party: party),
            JsonSerializer.Serialize(new { t = 1.1, kind = "ObjectRemove", type = "PromeRotation.LogSystem.LogSystemObjectRemoveEvent",
                data = new { SourceId = 2ul, Timestamp = Start.AddSeconds(1.1) } }),
            Frame(1023, 2, [], party: party),
        ]);
        controller.Seek(.5);
        Check(Aoes("guildivain/bait/").Length == 2, "Packet-only tether was not established.");
        controller.Seek(1.5);
        Check(Aoes("guildivain/bait/").Length == 0 && Damage().Length == 0,
            "Removal frame preceding ObjectRemove retained packet-only outgoing tether.");
        controller.Seek(.5);
        Check(Aoes("guildivain/bait/").Length == 2, "Packet-only reverse Seek did not rebuild.");
        stop();
        Console.WriteLine("PASS production Guildivain packet-only tether: removed snapshot before ObjectRemove, frozen actor outgoing untether, prediction cleanup and reverse Seek");
    }
}
