using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunIoSteelNode(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Aoes(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix)).ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        string Remove(double t, ulong id) => JsonSerializer.Serialize(new { t, kind = "ObjectRemove",
            type = "PromeRotation.LogSystem.LogSystemObjectRemoveEvent", data = new { SourceId = id, Timestamp = Start.AddSeconds(t) } });
        var center = new Vector3(76.285f, 0, -659.475f);
        load("masterplayer.quest.the-power-to-protect",
        [
            Header(648), Frame(648, 0, [Actor(1, 0x1BCB, center, combat: true), Actor(100, 0, center,
                stackedStatus: 1269, statusExpiresAt: Start.AddSeconds(10))], full: true, party: party),
            Frame(648, 1, [Actor(2, 0x1C00, center, cast: Cast(2175, 2, center + Vector3.UnitX * 8, startedAt: 1))], party: party),
            EffectEvent(3, 2, 2175, 1, position: center + Vector3.UnitX * 8),
            Frame(648, 3.1, [Actor(2, 0x1C00, center)], party: party),
            Frame(648, 4, [Actor(3, 0x1EA4C6, center + Vector3.UnitX * 8, dead: true, targetable: false)], party: party),
            Frame(648, 5, [Actor(3, 0x1EA4C6, center + Vector3.UnitX * 8, eventState: 7)], party: party),
            Frame(648, 11, [Actor(100, 0, center)], party: party),
            Frame(648, 12, [Actor(1, 0x1BCB, center, dead: true)], party: party),
            Frame(648, 13, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(dangers() == 1 && !(bool)Value(Encounter(), "RequireAllStop"), "Io fire cast or early hold.");
        controller.Seek(4.5);
        Check(dangers() == 1 && Aoes("trial/voidzone/live/").Length == 1 && Aoes("trial/voidzone/event/").Length == 0,
            "Io persistent fire death filter or effect/live handoff.");
        controller.Seek(9d);
        Check(!(bool)Value(Encounter(), "RequireAllStop"), "Io strict one-second threshold.");
        controller.Seek(9.1);
        Check((bool)Value(Encounter(), "RequireAllStop") && dangers() == 0, "Io final-second hold or EventState7 cleanup.");
        controller.Seek(10.5);
        Check((bool)Value(Encounter(), "RequireAllStop"), "Io state timed out before observed loss.");
        controller.Seek(11.5);
        Check(!(bool)Value(Encounter(), "RequireAllStop"), "Io state loss retained movement/action hold.");
        controller.Seek(12.5);
        Check(previews().Length == 0, "Io primary death cleanup.");
        controller.Seek(9.1);
        Check((bool)Value(Encounter(), "RequireAllStop"), "Io reverse Seek control intent.");
        stop();
        Console.WriteLine("PASS production Io: caution final-second strict clock, persistent status until loss, action/movement intent in replay, fire cast/effect/live transfer, EventState, dual views, primary death and reverse Seek");

        center = new(-111, 0, -295.51f);
        var indicator = center + Vector3.UnitX * 8;
        var bomb = indicator + new Vector3(.9f, 0, .9f);
        load("masterplayer.quest.an-art-for-the-living",
        [
            Header(702), Frame(702, 0, [Actor(1, 0x1CBA, center, combat: true),
                Actor(100, 0, center), Actor(2, 0x1CD7, indicator, dead: true, targetable: false)], full: true, party: party),
            Frame(702, 1, [Actor(3, 0x1CD5, bomb)], party: party),
            Frame(702, 2, [Actor(3, 0x1CD5, bomb, dead: true)], party: party),
            Frame(702, 3, [Actor(3, 0x1CD5, bomb)], party: party),
            Frame(702, 4, [], removed: [3], party: party),
            Frame(702, 5, [Actor(4, 0x1CD7, indicator)], party: party),
            Frame(702, 6, [], removed: [4], party: party),
            Remove(6.1, 4),
            Frame(702, 7, [Actor(4, 0x1CD7, center - Vector3.UnitX * 8)], party: party),
            Frame(702, 8, [Actor(1, 0x1CBA, center, dead: true)], party: party),
            Frame(702, 9, [], party: party),
        ]);
        controller.Seek(.5);
        Check(dangers() == 1 && previews().Length == 2 && (DateTime)Value(Aoes("node/").Single(), "Activation") == Start.AddSeconds(3),
            "Node dead indicator bootstrap clock or views.");
        controller.Seek(1.5);
        Check((Vector3)Value(Aoes("node/").Single(), "Position") == bomb
            && (DateTime)Value(Aoes("node/").Single(), "Activation") == Start.AddSeconds(3), "Node per-axis priming transfer.");
        controller.Seek(2.5);
        Check(dangers() == 0, "Node dead primed bomb remained dangerous.");
        controller.Seek(3.5);
        Check(dangers() == 1, "Node primed history failed to revive.");
        controller.Seek(4.5);
        Check(dangers() == 0, "Node bomb destruction failed first position removal.");
        controller.Seek(7.5);
        Check(dangers() == 2 && Aoes("node/").Length == 2, "Node indicator destruction or same-ID recreation lost frozen history.");
        controller.Seek(8.5);
        Check(previews().Length == 0, "Node primary death waited for explosive history.");
        controller.Seek(1.5);
        Check(dangers() == 1, "Node reverse Seek priming.");
        stop();
        Console.WriteLine("PASS production Summoning Node: ordered indicator bootstrap, per-axis priming and original clock, dead bomb hide/revive, first position destruction, frozen same-ID history, dual views, primary death and reverse Seek");

        center = new(-266, 0, -507);
        var swordPosition = center + Vector3.UnitX * 10;
        load("masterplayer.quest.steel-against-steel",
        [
            Header(866), Frame(866, 0, [Actor(1, 0x2A45, center, combat: true),
                Actor(100, 0, center), Actor(2, 0x2A46, swordPosition, rotation: .7f)], full: true, party: party),
            Frame(866, 1, [Actor(1, 0x2A45, center, combat: true, cast: Cast(17568, 4, center, startedAt: 1))], party: party),
            Frame(866, 2, [Actor(2, 0x2A46, swordPosition + Vector3.UnitZ * 2, rotation: .8f, dead: true, targetable: false)], party: party),
            Frame(866, 3, [Actor(1, 0x2A45, center, combat: true)], party: party),
            Frame(866, 4, [], removed: [2], party: party),
            Frame(866, 5, [Actor(2, 0x2A46, center - Vector3.UnitX * 10, rotation: 1.2f)], party: party),
            EffectEvent(6, 2, 17567, 1),
            Frame(866, 7, [new { Id = 1ul, Oid = 0x2A45u, Kind = 2, Position = Pose(center),
                InCombat = true, Targetable = true, Hp = 0, MaxHp = 1000, Dead = false }], party: party),
            Frame(866, 8, [], party: party),
        ]);
        controller.Seek(.5);
        Check(dangers() == 0, "Steel swords active before The Order.");
        controller.Seek(1.5);
        Check(dangers() == 1 && (DateTime)Value(Aoes("steel/sword/").Single(), "Activation") == Start.AddSeconds(5.3),
            "Steel The Order activation.");
        controller.Seek(3.5);
        Check(dangers() == 1 && (float)Value(Aoes("steel/sword/").Single(), "Rotation") == .8f,
            "Steel primary cancellation, sword death or untargetable lost active geometry.");
        controller.Seek(5.5);
        Check(dangers() == 2 && Aoes("steel/sword/").Length == 2, "Steel same-ID new generation replaced frozen old sword.");
        controller.Seek(6.5);
        Check(dangers() == 1 && (float)Value(Aoes("steel/sword/").Single(), "Rotation") == .8f,
            "Steel live caster effect removed the old generation.");
        controller.Seek(7.5);
        Check(previews().Length == 0, "Steel HP0 cleanup.");
        controller.Seek(1.5);
        Check(dangers() == 1, "Steel reverse Seek.");
        stop();
        Console.WriteLine("PASS production Steel: ordered dynamic sword group, primary cast clock and cancellation, dead/untargetable/destroyed frozen generations, same-ID effect identity, HP0, dual views and reverse Seek");

        object Npc(Vector3 position, bool dead = false) => Actor(20, 0x2A44, position, dead: dead, subKind: 5, isEnemy: false);
        var helpers = Enumerable.Range(2, 5).Select(i => Actor((ulong)i, 0x233C, center + Vector3.UnitX * i,
            cast: Cast(17561, 10 - i, center + Vector3.UnitX * i, rotation: i * .1f, startedAt: 0)));
        load("masterplayer.quest.steel-against-steel",
        [
            Header(866), Frame(866, 0, new object[]
            {
                Actor(1, 0x2A45, center, combat: true, cast: Cast(17558, 8, center, target: 20, startedAt: 0)),
                Actor(100, 0, center), Npc(center + Vector3.UnitZ * 8),
            }.Concat(helpers), full: true, party: party),
            Frame(866, 1, [Actor(2, 0x233C, center + Vector3.UnitX * 2), Npc(center + Vector3.UnitZ * 12)], party: party),
            Frame(866, 2, [Npc(center + Vector3.UnitZ * 12, dead: true)], party: party),
            Frame(866, 3, [Actor(1, 0x2A45, center, combat: true)], party: party),
            Frame(866, 4, [Npc(center + Vector3.UnitZ * 12)], party: party),
            Frame(866, 5, [Actor(1, 0x2A45, center, dead: true)], party: party),
            Frame(866, 6, [], party: party),
        ]);
        controller.Seek(.5);
        Check(Aoes("steel/bolt/").Length == 4
            && Aoes("steel/bolt/").All(a => ((Vector3)Value(a, "Position")).X <= center.X + 5)
            && Aoes("quest/stack/").Length == 2 && dangers() == 5,
            "Steel first four FIFO bolts or NPC stack registration.");
        controller.Seek(1.5);
        var promoted = Aoes("steel/bolt/").Single(a => ((Vector3)Value(a, "Position")).X == center.X + 6);
        Check((DateTime)Value(promoted, "Activation") == Start.AddSeconds(4.3)
            && Math.Abs((float)Value(promoted, "Rotation") - .6f) < .0001f,
            "Steel fifth bolt promotion reordered or reset the original clock/facing.");
        var stackDisplay = Aoes("quest/stack/").Single(a => ((string)Value(a, "Id")).EndsWith("/display"));
        Check(Vector3.Distance((Vector3)Value(stackDisplay, "Position"), center + Vector3.UnitZ * 12) < .04f
            && (bool)Value(stackDisplay, "Safe") && (bool)Value(stackDisplay, "OutlineOnly")
            && (float)Value(Value(stackDisplay, "Shape"), "Radius") == 6,
            $"Steel NPC stack: position={Value(stackDisplay, "Position")}, expected={center + Vector3.UnitZ * 12}, safe={Value(stackDisplay, "Safe")}, outline={Value(stackDisplay, "OutlineOnly")}, radius={Value(Value(stackDisplay, "Shape"), "Radius")}.");
        controller.Seek(2.5);
        Check(Aoes("quest/stack/").Length == 0 && dangers() == 4, "Steel dead NPC stack remained active.");
        controller.Seek(4.5);
        Check(Aoes("quest/stack/").Length == 0, "Steel cancelled stack returned when NPC revived.");
        controller.Seek(5.5);
        Check(previews().Length == 0, "Steel bolt/stack cleanup on primary death.");
        stop();
        Console.WriteLine("PASS production Steel FIFO bolts and NPC stack: fifth promotion preserves clock/facing, dynamic R6 safe outline with navigation, NPC death, actual cancellation, dual views and owner cleanup");
    }
}
