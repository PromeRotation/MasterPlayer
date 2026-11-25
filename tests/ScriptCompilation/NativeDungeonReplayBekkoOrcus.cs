using System.Collections;
using System.Numerics;
using System.Reflection;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBekkoOrcus(object managerObject, object runtimeObject, object controllerObject,
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
        int Obstacles() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var center = new Vector3(808.8f, 0, 69.5f);
        load("masterplayer.quest.the-battle-on-bekko",
        [
            Header(711), Frame(711, 0, [Actor(1, 0x1BF8, center, combat: true), Actor(100, 0, center),
                Actor(50, 0x1E8EA9, center + Vector3.UnitX * 10, dead: true, targetable: false)], full: true, party: party),
            Frame(711, 1, [Actor(2, 0x1BF9, center, cast: Cast(8439, 2.5, center, MathF.PI / 2, startedAt: 1))], party: party),
            CastEvent(1, 2, 8439, 2.5, center, MathF.PI / 2),
            Frame(711, 2, [Actor(2, 0x1BF9, center)], party: party),
            Frame(711, 3, [Actor(3, 0x1BF9, center, cast: Cast(8440, 2.5, center, MathF.PI / 2, startedAt: 3))], party: party),
            Frame(711, 4, [], removed: [3], party: party),
            Frame(711, 5, [Actor(4, 0x1BF9, center, cast: Cast(8441, 2.5, center, MathF.PI / 2, startedAt: 5))], party: party),
            Frame(711, 6, [Actor(4, 0x1BF9, center)], party: party),
            Frame(711, 7, [Actor(5, 0x1BF9, center, cast: Cast(8442, 2.5, center, MathF.PI / 2, startedAt: 7))], party: party),
            Frame(711, 8, [Actor(5, 0x1BF9, center), Actor(50, 0x1E8EA9, center + Vector3.UnitX * 10, eventState: 7)], party: party),
            Frame(711, 9, [Actor(1, 0x1BF8, center, combat: true, cast: Cast(8447, 2, center, startedAt: 9))], party: party),
            Frame(711, 10, [Actor(1, 0x1BF8, center, dead: true)], party: party),
            Frame(711, 11, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(dangers() == 1 && Obstacles() == 1 && previews().Length == 3
            && (float)Value(Aoes("bekko/ring/").Single(), "Rotation") == MathF.PI / 2,
            "Bekko directional ring, persistent obstacle or dual-view previews.");
        controller.Seek(2.5);
        Check(dangers() == 1 && (DateTime)Value(Aoes("bekko/ring/").Single(), "Activation") == Start.AddSeconds(4.5),
            "Bekko early cancellation did not advance from consumed time.");
        controller.Seek(4.5);
        Check((DateTime)Value(Aoes("bekko/ring/").Single(), "Activation") == Start.AddSeconds(6.5), "Bekko caster destruction failed to advance.");
        controller.Seek(8.5);
        Check(dangers() == 0 && Obstacles() == 0 && previews().Length == 1, "Bekko last stage or inactive-floor cleanup.");
        controller.Seek(9.5);
        Check(dangers() == 0 && ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().Count() == 1,
            "Bekko pure raidwide damage.");
        controller.Seek(10.5);
        Check(previews().Length == 0 && Obstacles() == 0, "Bekko primary-only death cleanup.");
        controller.Seek(1.5);
        Check(dangers() == 1 && Obstacles() == 1, "Bekko reverse Seek reconstruction.");
        stop();
        Console.WriteLine("PASS production Bekko: directional four-stage rings, cancellation and destruction clocks, persistent floor obstacle, raidwide, dual-view previews, death, reverse Seek and cleanup");

        center = new(-69.569f, 0, -388);
        load("masterplayer.quest.the-killing-art",
        [
            Header(1021), Frame(1021, 0, [Actor(1, 0x3664, center, combat: true), Actor(100, 0, center),
                Actor(20, 0x3666, center), Actor(21, 0x3667, center)], full: true, party: party),
            Frame(1021, 1, [Actor(2, 0x233C, center, cast: Cast(27602, 3.5, center, MathF.PI / 2, startedAt: 1)),
                Actor(3, 0x233C, center, cast: Cast(27600, 3.5, center, MathF.PI / 2, startedAt: 1))], party: party),
            Frame(1021, 2, [Actor(3, 0x233C, center)], party: party),
            Frame(1021, 3, [Actor(2, 0x233C, center)], party: party),
            Frame(1021, 4, [Actor(4, 0x233C, center, cast: Cast(27593, 3.5, center - Vector3.UnitX * 8, target: 100, startedAt: 4))], party: party),
            Frame(1021, 5, [Actor(100, 0, center + Vector3.UnitX * 8)], party: party),
            EffectEvent(6, 4, 27593, 1, target: 100),
            Frame(1021, 6.1, [Actor(4, 0x233C, center)], party: party),
            Frame(1021, 7, [Actor(50, 0x1E963D, center + Vector3.UnitX * 8)], party: party),
            Frame(1021, 8, [Actor(50, 0x1E963D, center + Vector3.UnitX * 8, eventState: 7)], party: party),
            Frame(1021, 9, [Actor(1, 0x3664, center, dead: true)], party: party),
            Frame(1021, 10, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(dangers() == 2 && previews().Length == 5 && Aoes("orcus/ring/").All(a => (float)Value(a, "Rotation") == MathF.PI / 2),
            "Orcus FIFO directions or adds marker previews.");
        controller.Seek(2.5);
        Check(dangers() == 1 && ((string)Value(Aoes("orcus/ring/").Single(), "Id")).StartsWith("orcus/ring/3/"),
            "Orcus non-head finish failed to consume FIFO head.");
        controller.Seek(3.5);
        Check(dangers() == 0, "Orcus second FIFO finish leak.");
        controller.Seek(5.5);
        Check(dangers() == 1 && (Vector3)Value(Aoes("trial/voidzone/cast/").Single(), "Position") == center + Vector3.UnitX * 8,
            "Orcus poison did not follow target.");
        controller.Seek(6.5);
        Check(dangers() == 1 && Aoes("trial/voidzone/event/").Length == 1, "Orcus cast-to-event handoff.");
        controller.Seek(7.5);
        Check(dangers() == 1 && Aoes("trial/voidzone/event/").Length == 0 && Aoes("trial/voidzone/live/").Length == 1,
            "Orcus event-to-live handoff duplicated poison.");
        Check(!(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 27606 }),
            "Inactive Orcus Explosion was claimed.");
        controller.Seek(8.5);
        Check(dangers() == 0, "Orcus EventState7 cleanup.");
        controller.Seek(9.5);
        Check(previews().Length == 0, "Orcus primary death waited for adds.");
        controller.Seek(5.5);
        Check(dangers() == 1 && Aoes("trial/voidzone/cast/").Length == 1, "Orcus reverse Seek reconstruction.");
        stop();
        Console.WriteLine("PASS production Orcus: FIFO rings and facing, dynamic poison target, cast/event/live handoff, source action ownership, adds, dual-view previews, primary death, reverse Seek and cleanup");
    }
}
