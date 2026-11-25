using System.Collections;
using System.Numerics;
using System.Reflection;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureAltarWall(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        object[] Sources() => ((IEnumerable)Value(Encounter(), "TreasureWallKnockbackSources")).Cast<object>().ToArray();
        const string guid = "masterplayer.treasure.shifting-altars";
        var c = new Vector3(100, 0, 100);
        var player = c + Vector3.UnitZ * 5;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        foreach (var (oid, action, expectedDangers) in new[] { (0x2533u, 13314u, 0), (0x253Du, 13377u, 1) })
        {
            var cast = Cast(action, 3, c);
            load(guid,
            [
                Header(794), Frame(794, 0, [Actor(1, oid, c, combat: true), Actor(2, 0x233C, c), Actor(100, 0, player)],
                    full: true, party: party),
                Frame(794, .1, [Actor(2, 0x233C, c, cast: cast)], party: party),
                Frame(794, 1, [Actor(2, 0x233C, c + Vector3.UnitX * 10, cast: cast)], party: party),
                Frame(794, 5, [], party: party),
                Frame(794, 6, [Actor(2, 0x233C, c)], party: party),
                Frame(794, 7, [], party: party),
            ]);
            controller.Seek(.2);
            Check(Sources().Length == 1 && Find("treasure/kb/").Length == 2 && dangers() == expectedDangers,
                "Knockback missing dual-view preview, added KB navigation or lost ordinary Wingbeat navigation.");
            var source = Sources().Single();
            var sourceCast = Value(source, "Cast");
            Check((float)Value(source, "Distance") == 20 && (bool)Value(source, "StopAtWall")
                && (ulong)Value(sourceCast, "Source") == 2 && (Vector3)Value(sourceCast, "Location") == c,
                "Production knockback model lost source, lock or wall parameters.");
            var landing = (Vector3)Value(Value(Find("treasure/kb/2/0/player").Single(), "Geometry"), "Position");
            Check(landing.Z > 118.9f && landing.Z < 119.01f && landing.X == c.X,
                "Wall-truncated landing differs.");
            controller.Seek(1.1);
            Check((Vector3)Value(Value(Find("treasure/kb/2/0/player").Single(), "Geometry"), "Position") == landing,
                "Moving caster rebound locked spell origin.");
            controller.Seek(5.1);
            Check(Sources().Length == 1 && Find("treasure/kb/").Length == 2,
                "Predicted expiry removed still-observed knockback.");
            controller.Seek(6.1);
            Check(Sources().Length == 0 && Find("treasure/kb/").Length == 0 && dangers() == 0,
                "Actual finish retained model or navigation.");
            controller.Seek(.2);
            Check(Sources().Length == 1 && Find("treasure/kb/").Length == 2, "Reverse Seek failed knockback rebuild.");
            stop();
            Check(previews().Length == 0 && dangers() == 0, "Knockback owner stop leaked.");
            Console.WriteLine($"PASS native treasure production {oid:X} raw KB model locked origin actual end reverse Seek dual-view preview and navigation scope");
        }

        load(guid,
        [
            Header(794), Frame(794, 0, [Actor(1, 0x2533, c, combat: true), Actor(2, 0x2563, c), Actor(3, 0x233C, c),
                Actor(100, 0, player)], full: true, party: party),
            Frame(794, .1, [Actor(1, 0x2533, c, combat: true, cast: Cast(13314, 3, c)),
                Actor(2, 0x2563, c, cast: Cast(13644, 4, c + Vector3.UnitZ * 19))], party: party),
            Frame(794, 1, [Actor(2, 0x2563, c)], party: party),
            Frame(794, 2, [], removed: [1], party: party),
            Frame(794, 3, [], party: party),
        ]);
        controller.Seek(.2);
        var hints = (IEnumerable<string>)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [player])!;
        Check(hints.Contains("About to be knocked into danger!") && dangers() == 1 && Find("treasure/kb/").Length == 2,
            "Terror Eye landing hint or normal navigation missing.");
        controller.Seek(1.1);
        hints = (IEnumerable<string>)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [player])!;
        Check(!hints.Contains("About to be knocked into danger!") && dangers() == 0, "Finished Terror Eye retained danger.");
        controller.Seek(2.1);
        Check(Sources().Length == 0 && Find("treasure/kb/").Length == 0, "Destroyed source retained knockback.");
        stop();
        Console.WriteLine("PASS native treasure production Dullahan Terror Eye only destination hazard actual finish and destroyed source cleanup");

        load(guid,
        [
            Header(794), Frame(794, 0, [Actor(1, 0x2533, c, combat: true), Actor(2, 0x233C, c), Actor(100, 0, player),
                Actor(101, 0, c)], full: true, party: party),
            Frame(794, .1, [Actor(2, 0x233C, c, cast: Cast(13315, 4, c, target: 101))], party: party),
            Frame(794, 1, [Actor(101, 0, c + Vector3.UnitX * 3)], party: party),
            Frame(794, 2, [Actor(2, 0x233C, c)], party: party),
            Frame(794, 3, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Find("treasure/stack/").Length == 1 && dangers() == 1, "Eight stack dual-view display or join navigation missing.");
        controller.Seek(1.1);
        var stackPosition = (Vector3)Value(Value(Find("treasure/stack/").Single(), "Geometry"), "Position");
        const float positionGrid = 2000f / 65535f;
        var expectedStackX = ((int)MathF.Round(103 / positionGrid) - .5f) * positionGrid;
        Check(MathF.Abs(stackPosition.X - expectedStackX) < .001f,
            $"Stack did not follow quantized target: {stackPosition}.");
        controller.Seek(2.1);
        Check(Find("treasure/stack/").Length == 0 && dangers() == 0, "Stack actual finish leaked.");
        stop();
        Console.WriteLine("PASS native treasure production Dullahan exact-eight stack target tracking display navigation and cleanup");

        foreach (var (oid, reward, extra, action) in new[] { (0x2533u, 0x2545u, 0x2563u, 13316u), (0x253Du, 0x2544u, 0x253Eu, 13381u) })
        {
            load(guid,
            [
                Header(794), Frame(794, 0, [Actor(1, oid, c, combat: true), Actor(2, reward, c),
                    Actor(3, extra, c), Actor(100, 0, player)], full: true, party: party),
                Frame(794, .1, [Actor(3, extra, c, cast: Cast(action, 3, c))], party: party),
                Frame(794, 1, [Actor(1, oid, c, dead: true), Actor(2, reward, c, dead: true), Actor(3, extra, c, dead: true)], party: party),
                Frame(794, 2, [], party: party),
            ]);
            controller.Seek(.2);
            Check(Find("treasure/enemy/").Length == (oid == 0x2533 ? 3 : 2)
                && (Vector4)Value(Find("treasure/enemy/2").Single(), "Color") == new Vector4(1, 0, 1, 1)
                && dangers() == 1, "Enemy markers/reward color, Feather exclusion or ordinary hazard missing.");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 3, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 13378 }),
                "Player source or inactive visual incorrectly claimed.");
            controller.Seek(1.1);
            Check(Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) == null && previews().Length == 0,
                "AllDeadOrDestroyed waited for corpses or Feather.");
            stop();
            Console.WriteLine($"PASS native treasure production {oid:X} source rules rewards and AllDeadOrDestroyed lifecycle");
        }
    }
}
