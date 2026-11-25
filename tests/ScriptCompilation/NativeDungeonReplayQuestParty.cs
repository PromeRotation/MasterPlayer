using System.Collections;
using System.Numerics;
using System.Reflection;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunQuestParty(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var center = new Vector3(0, 0, -82.146f);
        object Npc(Vector3 position, bool dead = false) => Actor(2, 0x3589, position, dead: dead, subKind: 5, isEnemy: false);
        load("masterplayer.quest.sages-focus",
        [
            Header(1022), Frame(1022, 0, [Actor(1, 0x3587, center, combat: true, targetId: 2, objectIndex: 2, isEnemy: true),
                Npc(center + Vector3.UnitX * 8), Actor(100, 0, center), Actor(3, 0x3586, center), Actor(4, 0x3588, center)],
                full: true, party: party),
            Frame(1022, 1, [Actor(1, 0x3587, center, combat: true, cast: Cast(26535, 8, center, target: 2, startedAt: 1))], party: party),
            CastEvent(1, 1, 26535, 8, center, target: 2),
            Frame(1022, 2, [Actor(1, 0x3587, center, combat: true)], party: party),
            Frame(1022, 3, [Actor(1, 0x3587, center, combat: true, cast: Cast(26553, 5, center, target: 2, startedAt: 3))], party: party),
            CastEvent(3, 1, 26553, 5, center, target: 2),
            Frame(1022, 4, [Npc(center + Vector3.UnitX * 3)], party: party),
            Frame(1022, 5, [Npc(center + Vector3.UnitX * 3, dead: true)], party: party),
            Frame(1022, 6, [Npc(center + Vector3.UnitX * 3)], party: party),
            Frame(1022, 7, [Actor(1, 0x3587, center, combat: true)], party: party),
            Frame(1022, 8, [Actor(1, 0x3587, center, combat: true, cast: Cast(26548, 5.6, center, startedAt: 8))], party: party),
            CastEvent(8, 1, 26548, 5.6, center),
            Frame(1022, 9, [Actor(1, 0x3587, center, combat: true)], party: party),
            CastEvent(10, 1, 26551, 8, center, target: 2),
            Frame(1022, 11, [Actor(1, 0x3587, center, dead: true)], party: party), Frame(1022, 12, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(Damage().Single() is { } damage && (ulong)Value(damage, "TargetId") == 2
            && (int)Value(damage, "PartySlot") == 24, "Sage NPC Triple Threat prediction.");
        Check(previews().Length == 3 && dangers() == 0, "Sage enemy collection or nonspatial single-target cast.");
        controller.Seek(4.5);
        var display = Aoes().Single(a => ((string)Value(a, "Id")).EndsWith("/display"));
        var avoid = Aoes().Single(a => ((string)Value(a, "Id")).EndsWith("/avoid"));
        Check((float)Value(Value(display, "Shape"), "Radius") == 5 && (float)Value(Value(avoid, "Shape"), "Radius") == 6
            && (Vector3)Value(display, "Position") is { X: > 2.9f and < 3.1f } && dangers() == 1,
            "Sage dynamic NPC spread display R5 or navigation R6.");
        controller.Seek(5.5);
        Check(dangers() == 0 && previews().Length == 3, "Dead spread target remained active.");
        controller.Seek(6.5);
        Check(dangers() == 1, "Same living NPC did not restore its pending spread.");
        controller.Seek(7.5);
        Check(dangers() == 0, "Actual cancelled spread did not clean up.");
        controller.Seek(8.5);
        Check(Damage().Length == 2 && dangers() == 0, "Sage raidwide party NPC mask or fabricated circle.");
        controller.Seek(10.5);
        Check(Damage().Length == 0 && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 26551 }),
            "Inactive DosisIII was claimed.");
        controller.Seek(11.5);
        Check(previews().Length == 0 && dangers() == 0, "Sage primary death waited for other bosses.");
        controller.Seek(4.5);
        Check(dangers() == 1, "Sage reverse Seek failed to rebuild NPC spread.");
        stop();
        Console.WriteLine("PASS production Sage: exact 20-gon, NPC24 damage, three boss markers, dynamic R5/R6 spread, dead/revived target, actual finish, raidwide NPC mask, inactive DosisIII fallback, primary-only death, Seek and owner cleanup");

        center = new(-166.8f, 0, 290);
        object StackNpc(Vector3 position) => Actor(2, 0x1C5C, position, subKind: 5, isEnemy: false);
        object Ompagne(object? cast = null, bool dead = false) => Actor(1, 0x1C5E, center,
            combat: true, targetId: 100, objectIndex: 2, isEnemy: true, cast: cast, dead: dead);
        load("masterplayer.quest.the-orphans-and-the-broken-blade",
        [
            Header(715), Frame(715, 0, [Ompagne(),
                StackNpc(center + Vector3.UnitX * 8), Actor(100, 0, center),
                Actor(3, 0x1C60, center, objectIndex: 4, isEnemy: true)], full: true, party: party),
            Frame(715, 1, [Ompagne(Cast(8457, 5, center, target: 2, startedAt: 1))], party: party),
            CastEvent(1, 1, 8457, 5, center, target: 2),
            Frame(715, 2, [Ompagne()], party: party),
            Frame(715, 7, [StackNpc(center + Vector3.UnitX * 10)], party: party),
            EffectEvent(8, 1, 8457, 1),
            EffectEvent(9, 999, 9298, 2),
            Frame(715, 10, [Ompagne(dead: true)], party: party), Frame(715, 11, [], party: party),
        ]);
        controller.Seek(7.5);
        var stack = Aoes().Single(a => ((string)Value(a, "Id")).EndsWith("/display"));
        var join = Aoes().Single(a => (string)Value(a, "Id") == "quest/stack/join");
        var shape = Value(join, "Shape");
        var geometry = shape.GetType().GetMethod("Resolve")!.Invoke(shape, [center, 0f, null])!;
        bool Contains(Vector3 p) => (bool)geometry.GetType().GetMethod("Contains")!.Invoke(geometry, [p, .001f])!;
        Check(dangers() == 1 && previews().Length == 3 && (bool)Value(stack, "Safe") && (bool)Value(stack, "OutlineOnly")
            && Math.Abs(((DateTime)Value(join, "Activation") - Start.AddSeconds(6.3)).TotalMilliseconds) < .01 && Contains(center)
            && !Contains(center + Vector3.UnitX * 10) && Damage().Length == 0,
            $"Persistent NPC stack: dangers={dangers()}, previews={previews().Length}, safe={Value(stack, "Safe")}, outline={Value(stack, "OutlineOnly")}, activation={Value(join, "Activation"):O}, outside={Contains(center)}, inside={Contains(center + Vector3.UnitX * 10)}, damage={Damage().Length}.");
        Check((ulong)Encounter().GetType().GetMethod("PriorityTarget")!.Invoke(Encounter(), [1ul, center])! == 3,
            "Ompagne chain priority5.");
        controller.Seek(8.5);
        Check(dangers() == 1, "8457 effect incorrectly resolved persistent stack.");
        controller.Seek(9.5);
        Check(dangers() == 0 && previews().Length == 2, "9298 did not clear all persistent stacks.");
        controller.Seek(10.5);
        Check(previews().Length == 0, "Ompagne primary death waited for chain.");
        controller.Seek(7.5);
        Check(dangers() == 1, "Ompagne reverse Seek did not restore post-finish stack.");
        stop();
        Console.WriteLine("PASS production Ompagne: NPC persistent stack survives cancellation/time/8457 effect, dynamic safe outline and hard entry navigation, chain priority5, 9298 clears all, primary-only death, reverse Seek and owner cleanup");
    }
}
