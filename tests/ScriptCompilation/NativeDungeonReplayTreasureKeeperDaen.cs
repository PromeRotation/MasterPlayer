using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureKeeperDaen(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Models(string property) => ((IEnumerable)Value(Encounter(), property)).Cast<object>().ToArray();
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        object[] Aoes(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(key)).ToArray();
        const string guid = "masterplayer.treasure.shifting-oubliettes";
        var c = new Vector3(100, 0, 100);
        var p = c + Vector3.UnitZ * 10;
        object[] party = [.. Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) })];
        object[] players = [.. Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, p + Vector3.UnitX * i))];
        foreach (var (oid, actions) in new[]
        {
            (0x3023u, new uint[] { 21680, 21677, 21681, 21769, 21767, 21768 }),
            (0x3030u, new uint[] { 21751, 21754, 21755, 21752, 21745, 21750, 6448, 6449, 6450, 6451, 6452, 21769, 21767, 21768 }),
            (0x3032u, new uint[] { 21758, 21764, 6448, 6449, 6450, 6451, 6452, 21769, 21767, 21768 }),
        })
        {
            var casting = actions.Select((action, i) => Actor((ulong)i + 2, 0x233C, c,
                cast: Cast(action, 5 + i, c))).ToArray();
            load(guid,
            [
                Header(924), Frame(924, 0, [Actor(1, oid, c, combat: true), .. players], full: true, party: party),
                Frame(924, .1, casting, party: party), Frame(924, 30, [], party: party),
                Frame(924, 31, [Actor(2, 0x233C, c)], party: party),
                Frame(924, 32, casting.Skip(1).Select((_, i) => Actor((ulong)i + 3, 0x233C, c)), party: party),
                Frame(924, 33, [], party: party),
            ]);
            controller.Seek(.2);
            Check(dangers() == actions.Length && Models("OutputCasts").Length == actions.Length,
                $"Ordinary full rules missing for {oid:X}: dangers={dangers()}.");
            Check(actions.All(action => (bool)manager.IsGenericDodgeSuppressed(
                new PromeRotation.LogSystem.LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action })),
                "Accepted full rules failed generic suppression.");
            controller.Seek(30.1);
            Check(dangers() == actions.Length, "Predicted clock expired ordinary observed-end casts.");
            controller.Seek(31.1);
            Check(dangers() == actions.Length - 1, "One Finish cleared other clocks.");
            controller.Seek(32.1);
            Check(dangers() == 0, "Ordinary Finish cleanup failed.");
            controller.Seek(.2);
            Check(dangers() == actions.Length, "Ordinary reverse Seek failed.");
            stop();
            Console.WriteLine($"PASS native treasure production {oid:X} all ordinary rules suppression independent clocks real Finish dual views navigation reverse Seek cleanup");
        }

        load(guid,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3023, c, combat: true),
                Actor(2, 0x233C, c, hitboxRadius: 3.99f), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, hitboxRadius: 3.99f, cast: Cast(21677, 5, c))], party: party),
            Frame(924, 1, [Actor(100, 0, p, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(8))], party: party),
            Frame(924, 2, [Actor(100, 0, p, hitboxRadius: 8)], party: party),
            Frame(924, 10, [], party: party), Frame(924, 11, [Actor(2, 0x233C, c)], party: party),
            Frame(924, 12, [], party: party),
        ]);
        controller.Seek(.2);
        var pull = Models("TreasureWallKnockbackSources").Single();
        Check((bool)Value(pull, "TowardsOrigin") && !(bool)Value(pull, "StopAtWall")
            && (float)Value(pull, "Distance") == 20 && (float)Value(pull, "MinDistance") == 4.49f
            && Find("treasure/kb/").Length == 2 && dangers() == 1, "Keeper pull source model/slot0/ordinary cone differs.");
        controller.Seek(1.1);
        Check(Find("treasure/kb/").Length == 0 && dangers() == 1, "Pull immunity removed ordinary cone.");
        controller.Seek(10.1);
        Check((float)Value(Models("TreasureWallKnockbackSources").Single(), "MinDistance") == 4.49f
            && Find("treasure/kb/").Length == 2, "Pull recomputed start hitbox/expired.");
        controller.Seek(11.1);
        Check(!Models("TreasureWallKnockbackSources").Any() && dangers() == 0, "Pull true Finish retained model.");
        controller.Seek(.2);
        Check(Models("TreasureWallKnockbackSources").Length == 1, "Pull reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Keeper pull20 cached slot0 hitboxes shape filtering immunity independent cone actual ends Seek");

        load(guid,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3023, c, combat: true), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21679, 4, c, target: 101))], party: party),
            Frame(924, 1, [Actor(2, 0x233C, c)], party: party),
            TreasureNativeEffect(2, 2, 21679, 1, 101, []),
            Frame(924, 3, [Actor(20, 0x1E8FC7, p + Vector3.UnitX, dead: true, targetable: false)], party: party),
            Frame(924, 4, [Actor(20, 0x1E8FC7, p, eventState: 7)], party: party),
            Frame(924, 5, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Find("trial/voidzone/cast/").Length == 1 && dangers() == 1, "Keeper cast poison missing.");
        controller.Seek(1.1);
        Check(dangers() == 0, "Cancelled poison created extra event.");
        controller.Seek(2.1);
        Check((DateTime)Value(Aoes("trial/voidzone/event/").Single(), "Activation") == Start.AddSeconds(2).AddSeconds(1.4d),
            "Keeper native main target/double poison delay differs.");
        controller.Seek(3.1);
        Check(Find("trial/voidzone/live/").Length == 1 && Find("trial/voidzone/event/").Length == 0 && dangers() == 1
            && !((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Any(),
            "Keeper live poison dead/untargetable filter or hard obstacle differs.");
        controller.Seek(4.1);
        Check(dangers() == 0 && !Find("trial/voidzone/live/").Any(), "Keeper state7 retained poison.");
        stop();
        Console.WriteLine("PASS native treasure production Keeper poison cast native main target exact1.4 event live consumption state7 no hard obstacle");

        load(guid,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3023, c, combat: true, targetId: 100, cast: Cast(21681, 5, c, startedAt: 0)),
                Actor(20, 0x3023, c + Vector3.UnitX, targetId: 101), .. players], full: true, party: party),
            Frame(924, 1, [Actor(20, 0x3023, c + Vector3.UnitX, targetId: 100)], party: party),
            Frame(924, 2, [Actor(20, 0x3023, c + Vector3.UnitX, targetId: 100, dead: true)], party: party),
            Frame(924, 3, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Find("treasure/cleave/").Length == 2 && Aoes("treasure/cleave/").Count(a => (bool)Value(a, "Navigate")) >= 2,
            "Keeper multiple source casting persistent cleaves missing.");
        controller.Seek(2.1);
        Check(Find("treasure/cleave/").Length == 1, "Dead source cleave retained.");
        stop();
        Console.WriteLine("PASS native treasure production Keeper all-source persistent R12 cleave casting dynamic targets dead source cleanup");

        load(guid,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3030, c, combat: true), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21749, 5, c)),
                Actor(3, 0x233C, p, cast: Cast(21750, 8, p))], party: party),
            Frame(924, 1, [Actor(100, 0, p, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(10))], party: party),
            Frame(924, 2, [Actor(2, 0x233C, c)], party: party), Frame(924, 3, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Models("TreasureWallKnockbackSources").Length == 1 && Find("treasure/kb/").Length == 2 && dangers() == 2
            && !Models("PredictedDamage").Any() && Aoes("treasure/turmoil/").Single() is { } direction
            && !(bool)Value(direction, "Display") && (DateTime)Value(direction, "Activation") == Start.AddSeconds(.1 + 5 + .3),
            "Ultros wall displacement/independent hidden direction differs.");
        controller.Seek(1.1);
        Check(Find("treasure/kb/").Length == 0 && dangers() == 2 && Aoes("treasure/turmoil/").Length == 1,
            "Ultros immunity removed hidden direction.");
        controller.Seek(2.1);
        Check(dangers() == 1 && Aoes("treasure/turmoil/").Length == 0, "Ultros KB Finish failed independent output.");
        stop();
        Console.WriteLine("PASS native treasure production Ultros wall20 only splatter unsafe hidden direction immune independence no raidwide actual Finish");

        load(guid,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3030, c, combat: true), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21747, 5, c, target: 101)),
                Actor(3, 0x233C, c, cast: Cast(21747, 6, c, target: 101))], party: party),
            Frame(924, 1, [Actor(101, 0, p, dead: true)], party: party),
            Frame(924, 2, [Actor(101, 0, p)], party: party),
            Frame(924, 3, [Actor(3, 0x233C, c)], party: party),
            Frame(924, 4, [Actor(2, 0x233C, c)], party: party), Frame(924, 5, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Find("quest/spread/").Length == 2 && dangers() == 2 && !Models("PredictedDamage").Any(), "Ultros spread output differs.");
        controller.Seek(1.1);
        Check(dangers() == 0, "Dead Ultros spread target retained.");
        controller.Seek(2.1);
        Check(dangers() == 2, "Ultros spread target revival lost.");
        controller.Seek(3.1);
        Check((DateTime)Value(Aoes("quest/spread/").Single(a => (bool)Value(a, "Navigate")), "Activation") == Start.AddSeconds(.1 + 6 + .3),
            "Ultros spread target FIFO used source.");
        controller.Seek(4.1);
        Check(dangers() == 0, "Ultros spread final Finish failed.");
        stop();
        Console.WriteLine("PASS native treasure production Ultros spread8 display9 navigation target FIFO death revival no prediction");

        load(guid,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3032, c, combat: true), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21762, 5, c, target: 101))], party: party),
            Frame(924, 1, [Actor(3, 0x233C, c, cast: Cast(21760, 6, c, target: 102, startedAt: 1))], party: party),
            Frame(924, 2, [Actor(2, 0x233C, c)], party: party),
            Frame(924, 3, [Actor(3, 0x233C, c)], party: party), Frame(924, 4, [], party: party),
        ]);
        controller.Seek(1.1);
        Check(Models("Gazes").Length == 1 && (ulong)Value(Models("Gazes").Single(), "TargetId") == 102
            && Find("treasure/stack/").Length > 0 && !Models("PredictedDamage").Any(), "Echidna global slot/eight-stack output differs.");
        controller.Seek(2.1);
        Check(!Models("Gazes").Any() && Find("treasure/stack/").Length > 0, "Cross-source gaze Finish cleared stack or retained eye.");
        controller.Seek(3.1);
        Check(!Find("treasure/stack/").Any(), "Stack Finish failed.");
        controller.Seek(1.1);
        Check(Models("Gazes").Length == 1, "Echidna dynamic slot reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Echidna shared dynamic slot last start cross-source Finish independent eight-stack no forecast Seek");

        foreach (var cleared in new[] { false, true })
        {
            load(guid,
            [
                Header(924), Frame(924, 0, [Actor(1, 0x3032, c, combat: true), .. players], full: true, party: party),
                Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21760, 5, c, target: 100))], party: party),
                Frame(924, 1, [Actor(3, 0x233C, c, cast: Cast(21762, 6, c, target: 101, startedAt: 1))], party: party),
                Frame(924, 4, [], party: party),
            ]);
            controller.Seek(1.2);
            var encounter = Encounter();
            var finish = encounter.GetType().GetMethod("Finish")!;
            if (cleared) finish.Invoke(encounter, [3ul, 21762u, Start.AddSeconds(1.3), false]);
            if (cleared)
            {
                var missing = Activator.CreateInstance(encounter.GetType().Assembly.GetType(
                    "MasterPlayer.Mechanics.Dungeons.DungeonCastObservation")!,
                    [21760u, c, 0f, Start.AddSeconds(.1), Start.AddSeconds(.1 + 5 + .3), 999ul, null, null, null])!;
                encounter.GetType().GetMethod("StartCast")!.Invoke(encounter, [2ul, missing, Start.AddSeconds(1.35), true]);
                Check(!Aoes("treasure/stack/").Any(), "Missing corrected target retained stack.");
            }
            var observation = Activator.CreateInstance(encounter.GetType().Assembly.GetType(
                "MasterPlayer.Mechanics.Dungeons.DungeonCastObservation")!,
                [21760u, c, 0f, Start.AddSeconds(.1), Start.AddSeconds(.1 + 5 + .3), 103ul, null, null, null])!;
            encounter.GetType().GetMethod("StartCast")!.Invoke(encounter, [2ul, observation, Start.AddSeconds(1.4), true]);
            Check(cleared ? !Models("Gazes").Any() : (ulong)Value(Models("Gazes").Single(), "Source") == 3,
                "Accepted packet target correction stole/revived the global gaze slot.");
            Check(Vector3.Distance((Vector3)Value(Aoes("treasure/stack/").Single(a => (bool)Value(a, "Display")), "Position"),
                p + Vector3.UnitX * 3) < .05f, "Corrected stack kept stale target reference.");
            finish.Invoke(encounter, [2ul, 21760u, Start.AddSeconds(1.5), false]);
            Check(!Aoes("treasure/stack/").Any(), "Corrected stack survived real Finish.");
            stop();
        }
        Console.WriteLine("PASS native treasure production Echidna accepted packet target correction preserves global eye ownership and stack Finish consistency");

        var moving = c + Vector3.UnitX * 7;
        load(guid,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3032, c, combat: true), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21762, 5, c, target: 101))], party: party),
            Frame(924, 1, [Actor(101, 0, moving, dead: true)], party: party),
            Frame(924, 2, [], removed: [101], party: party),
            Frame(924, 3, [Actor(101, 0, c - Vector3.UnitZ * 9)], party: party),
            Frame(924, 5, [], party: party),
            Frame(924, 6, [Actor(2, 0x233C, c)], party: party), Frame(924, 7, [], party: party),
        ]);
        controller.Seek(5.1);
        Check((Vector3)Value(Models("Gazes").Single(), "Location") == moving && Find("treasure/gaze/").Length == 3
            && dangers() == 0 && Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [p, -MathF.PI / 2]) is float,
            "Dynamic eye lost original dead/destroyed target/facing or became ground AOE.");
        var hints = Value(Script(), "Hints");
        var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
            "Echidna replay executed PR facing.");
        controller.Seek(6.1);
        Check(!Find("treasure/gaze/").Any(), "Dynamic eye true Finish retained.");
        stop();
        Console.WriteLine("PASS native treasure production Echidna dead target dynamic eye destroy same-ID freeze eye pupil arc facing intent replay isolation");

        var streams = Enumerable.Range(2, 12).Select(i => Actor((ulong)i, 0x233C, c,
            cast: Cast((uint)(22797 + i % 4), 20 - i, c))).ToArray();
        load(guid,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3032, c, combat: true), .. players], full: true, party: party),
            Frame(924, .1, streams, party: party), Frame(924, 30, [], party: party),
            Frame(924, 31, [Actor(2, 0x233C, c)], party: party),
            Frame(924, 32, streams.Skip(1).Select((_, i) => Actor((ulong)i + 3, 0x233C, c)), party: party),
            Frame(924, 33, [], party: party),
        ]);
        controller.Seek(30.1);
        Check(Models("TreasureVaultGroups").Length == 12 && (ulong)Value(Models("TreasureVaultGroups")[0], "Source") == 2
            && Find("treasure/vault-group/").Length == 4 && dangers() == 4, "VoidStream sorted at twelve/expired/max4 differs.");
        controller.Seek(31.1);
        Check(Models("TreasureVaultGroups").Length == 11 && (ulong)Value(Models("TreasureVaultGroups")[3], "Source") == 6
            && Find("treasure/vault-group/").Any(a => ((string)Value(a, "Id")).EndsWith("/6")),
            $"VoidStream fifth promotion failed: {string.Join(';', Find("treasure/vault-group/").Select(a => Value(a, "Id")))}.");
        controller.Seek(32.1);
        Check(dangers() == 0, "VoidStream real Finish failed.");
        controller.Seek(.2);
        Check(dangers() == 4 && Models("TreasureVaultGroups").Length == 12, "VoidStream reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Echidna VoidStream twelve unsorted expected99 first4 immediate danger source Finish fifth promotion Seek");
    }
}
