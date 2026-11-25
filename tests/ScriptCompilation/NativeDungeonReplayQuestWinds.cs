using System.Collections;
using System.Numerics;
using System.Reflection;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunQuestWinds(object managerObject, object runtimeObject, object controllerObject,
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
        var center = new Vector3(-89.3f, 0, 0);
        object Paladin(object? cast = null, bool dead = false) => Actor(1, 0x1B51, center, combat: true, cast: cast, dead: dead);
        object Wind(byte state) => Actor(2, 0x1B52, center + Vector3.UnitX * 15, eventState: state, dead: true, targetable: false);
        load("masterplayer.quest.raising-the-sword",
        [
            Header(706), Frame(706, 0, [Paladin(), Actor(100, 0, center + Vector3.UnitX * 5), Wind(7)], full: true, party: party),
            Frame(706, 1, [Paladin(Cast(8136, 3, center, startedAt: 1))], party: party),
            CastEvent(1, 1, 8136, 3, center),
            Frame(706, 2, [Actor(100, 0, center + Vector3.UnitX * 18), Wind(0)], party: party),
            Frame(706, 3, [Actor(100, 0, center + Vector3.UnitX * 18, stackedStatus: 160,
                statusExpiresAt: Start.AddSeconds(7))], party: party),
            Frame(706, 4, [Actor(100, 0, center + Vector3.UnitX * 18, stackedStatus: 160,
                statusExpiresAt: Start.AddSeconds(4.3))], party: party),
            Frame(706, 5, [Paladin()], party: party),
            CastEvent(6, 1, 8138, 3, center),
            Frame(706, 7, [Paladin(dead: true)], party: party), Frame(706, 8, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(Aoes("quest-wind/live/").Length == 0 && Aoes("quest-wind/kb/avoid/").Length == 1
            && Aoes("quest-wind/kb/player").Length == 1 && Aoes("quest-wind/kb/path").Length == 1,
            "Paladin hidden wind still participates in cone navigation and independent KB projection.");
        controller.Seek(2.5);
        var landing = (Vector3)Value(Aoes("quest-wind/kb/player").Single(), "Position");
        Check(Aoes("quest-wind/live/").Length == 1
            && MathF.Abs(Vector3.Distance(landing, center) - 20.499f) < .01f
            && previews().Any(p => ((string)Value(p, "Id")).Contains("quest-wind/kb/player")),
            "Paladin dead wind, exact wall stopping or dual-view projected actor.");
        controller.Seek(3.5);
        Check(Aoes("quest-wind/kb/").Length == 0 && Aoes("quest-wind/live/").Length == 1, "Paladin immunity also hid independent live voidzone.");
        controller.Seek(4.1);
        Check(Aoes("quest-wind/kb/player").Length == 1, "Paladin immunity expiry equal to activation incorrectly skipped KB.");
        controller.Seek(5.5);
        Check(Aoes("quest-wind/kb/").Length == 0, "Paladin observed finish retained KB.");
        controller.Seek(6.5);
        Check(!(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 8138 }),
            "Paladin claimed the inactive Four Winds raidwide.");
        controller.Seek(7.5);
        Check(previews().Length == 0, "Paladin primary death retained wind.");
        controller.Seek(2.5);
        Check(Aoes("quest-wind/live/").Length == 1 && Aoes("quest-wind/kb/player").Length == 1, "Paladin reverse Seek lost wind/KB.");
        stop();
        Console.WriteLine("PASS production Paladin winds: distinct EventState/dead source sets, locked 10y KB, exact circular wall, immunity strict expiry, projected actor/path dual views, actual ending, inactive action fallback, death and reverse Seek");

        center = new(520.47f, 0, 124.99f);
        var target = center + Vector3.UnitX * 8;
        var movedBoss = center + Vector3.UnitZ * 2;
        object Dancer(Vector3 position, object? cast = null, bool dead = false) => Actor(1, 0x29D2, position, combat: true, cast: cast, dead: dead);
        object Whirlwind(bool dead) => Actor(3, 0x29D5, center + Vector3.UnitZ * 12, eventState: 7, dead: dead, targetable: false);
        load("masterplayer.quest.gamboling-for-gil",
        [
            Header(867), Frame(867, 0, [Dancer(center), Actor(100, 0, target),
                Actor(2, 0x29D1, center + Vector3.UnitX * 4, subKind: 5, isEnemy: false), Whirlwind(false)], full: true, party: party),
            Frame(867, 1, [Dancer(center, Cast(17289, 6, center, target: 100, startedAt: 1))], party: party),
            CastEvent(1, 1, 17289, 6, center, target: 100),
            Frame(867, 2, [Dancer(movedBoss, Cast(17289, 6, center, target: 100, startedAt: 1)),
                Actor(100, 0, center + Vector3.UnitX * 12)], party: party),
            Frame(867, 3, [Whirlwind(true)], party: party),
            Frame(867, 4, [Dancer(movedBoss)], party: party),
            Frame(867, 5, [Dancer(movedBoss, Cast(17198, 3, movedBoss, startedAt: 5)),
                Actor(100, 0, movedBoss - Vector3.UnitZ * 100, rotation: 0)], party: party),
            CastEvent(5, 1, 17198, 3, movedBoss),
            Frame(867, 8, [Dancer(movedBoss)], party: party),
            Frame(867, 9, [Dancer(movedBoss, dead: true)], party: party), Frame(867, 10, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(Aoes("quest-wind/live/").Length == 1 && Aoes("foxshot/1/party/2").Length == 1
            && (bool)Value(Aoes("foxshot/1/display").Single(), "OutlineOnly")
            && ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().Single() is { } damage
            && (ulong)Value(damage, "TargetId") == 100 && (string)Value(damage, "DamageType") == "Tankbuster",
            "Dancer EventState7 active wind, self bait, NPC cone or predicted damage.");
        controller.Seek(2.5);
        var bait = Aoes("foxshot/1/display").Single();
        var kb = Aoes("quest-wind/kb/avoid/").Single();
        Check((Vector3)Value(bait, "Position") == movedBoss
            && MathF.Abs((float)Value(Value(bait, "Shape"), "Length") - MathF.Sqrt(148)) < .001f
            && Vector3.Distance((Vector3)Value(kb, "Position"), center) < .04f,
            "Dancer charge failed dynamic length/source or rebound independent locked KB origin.");
        controller.Seek(3.5);
        Check(Aoes("quest-wind/live/").Length == 0 && Aoes("quest-wind/kb/avoid/").Length == 1,
            "Dancer dead wind was incorrectly dropped from KB cone set.");
        controller.Seek(4.5);
        Check(Aoes("foxshot/").Length == 0 && Aoes("quest-wind/kb/").Length == 0, "Dancer actual ending retained charge/KB.");
        controller.Seek(7.7);
        var facing = Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [movedBoss - Vector3.UnitZ * 100, 0f]);
        Check(facing is float angle && MathF.Cos(angle) < .707107f && Aoes("gamboling/gaze/").Length == 3
            && !previews().Any(p => ((string)Value(p, "Id")).EndsWith("gaze/1", StringComparison.Ordinal)),
            "Dancer gaze range10000 facing or eye/pupil/view arc drawing.");
        controller.Seek(8.5);
        Check(Aoes("gamboling/gaze/").Length == 0, "Dancer gaze actual ending retained outputs.");
        controller.Seek(9.5);
        Check(previews().Length == 0, "Dancer primary death cleanup.");
        controller.Seek(2.5);
        Check(Aoes("foxshot/").Length > 0 && Aoes("quest-wind/kb/player").Length == 1, "Dancer reverse Seek failed.");
        stop();
        Console.WriteLine("PASS production Gamboling: exact ellipse, dynamic NPC-aware Foxshot and tankbuster, independent locked 25y wall KB, dead-vs-EventState wind sets, gaze range10000 PR facing and eye/arc dual views, observed endings, primary death and reverse Seek");

        object[] fullParty = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        load("masterplayer.quest.gamboling-for-gil",
        [
            Header(867), Frame(867, 0, [Dancer(center), Actor(100, 0, center), Actor(101, 0, target)], full: true, party: fullParty),
            Frame(867, 1, [Dancer(center, Cast(17289, 6, center, target: 101, startedAt: 1))], party: fullParty),
            Frame(867, 2, [], removed: [101], party: fullParty),
            Frame(867, 3, [], party: party),
            Frame(867, 4, [Dancer(center, dead: true)], party: party), Frame(867, 5, [], party: party),
        ]);
        controller.Seek(2.5);
        var removedDamage = ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().Single();
        Check((ulong)Value(removedDamage, "TargetId") == 101 && (int)Value(removedDamage, "PartySlot") == 1
            && Aoes("foxshot/1/avoid").Length == 1, "Foxshot removed actor lost frozen geometry or retained roster damage.");
        controller.Seek(3.5);
        Check(!((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().Any(),
            "Foxshot actual roster removal retained predicted damage.");
        stop();
        Console.WriteLine("PASS production Foxshot roster: actor disappearance retains target mask and frozen geometry, actual member removal clears prediction");
    }
}
