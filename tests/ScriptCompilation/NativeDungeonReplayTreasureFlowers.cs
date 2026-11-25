using System.Collections;
using System.Numerics;
using System.Reflection;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureFlowers(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Encounter()
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object[] Models(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        object[] Aoes(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(key)).ToArray();
        const string guid = "masterplayer.treasure.vault-oneiron";
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object[] players = [Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10)];
        uint[] paeonia = [43701, 43702, 43714, 43706, 43707, 43715, 43716, 43710, 43712, 43713, 43709, 43704,
            32301, 32302, 32303, 32304, 32305];
        uint[] spirit = [43773, 43774, 43778, 43779, 43780, 43781, 43776, 43788, 43786, 43782, 43784,
            43727, 43726, 32301, 32302, 32303, 32304, 32305];
        foreach (var (oid, actions, expectedDanger) in new[] { (0x48AEu, paeonia, 11), (0x48B5u, spirit, 15) })
        {
            var casting = actions.Select((action, i) => Actor((ulong)i + 2, 0x233C, c,
                cast: Cast(action, 5, c + (action is 43701 or 43702 ? Vector3.UnitZ * 100 : Vector3.Zero), target: 100))).ToArray();
            load(guid,
            [
                Header(1279), Frame(1279, 0, [Actor(1, oid, c, combat: true), .. players], full: true, party: party),
                Frame(1279, .1, casting, party: party), Frame(1279, 30, [], party: party),
                Frame(1279, 31, casting.Select((_, i) => Actor((ulong)i + 2, 0x233C, c)).ToArray(), party: party),
                Frame(1279, 32, [], party: party),
            ]);
            controller.Seek(.2);
            Check(dangers() == expectedDanger && actions.All(action => (bool)manager.IsGenericDodgeSuppressed(
                new LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action })),
                $"{oid:X} active source input or dangerous output differs: {dangers()} expected {expectedDanger}.");
            var spread = Aoes("quest/spread/");
            var radius = oid == 0x48AE ? 6f : 5f;
            Check((float)Value(Value(spread.Single(a => (bool)Value(a, "Display")), "Shape"), "Radius") == radius
                && (float)Value(Value(spread.Single(a => (bool)Value(a, "Navigate")), "Shape"), "Radius") == radius + 1
                && Models("PredictedDamage").Length == 3, "Spread geometry or source damage mask differs.");
            if (oid == 0x48AE)
            {
                Check(Find("treasure/gaze/").Length >= 5, "Two default10000 gaze eyes absent from dual views.");
                controller.Seek(5);
                Check(Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [c, 0f]) is float,
                    "Paeonia PR gaze facing intent missing.");
            }
            controller.Seek(30.1);
            Check(dangers() == expectedDanger, "Forecast clock expired source active mechanisms.");
            controller.Seek(31.1);
            Check(dangers() == 0 && !Models("PredictedDamage").Any() && !Find("treasure/gaze/").Any(),
                "True Finish retained source output.");
            controller.Seek(.2);
            Check(dangers() == expectedDanger, "Flowers reverse Seek failed.");
            stop();
            Console.WriteLine($"PASS native treasure production flowers {oid:X} all{actions.Length} rules exact shapes independent clocks spread{radius} and{radius + 1} source damage gaze10000 facing true Finish dual views navigation Seek owner cleanup");
        }

        foreach (var (oid, raidwide, tankbuster) in new[] { (0x48AEu, 43715u, 43716u), (0x48B5u, 43786u, 43784u) })
        {
            load(guid,
            [
                Header(1279), Frame(1279, 0, [Actor(1, oid, c, combat: true), .. players], full: true, party: party),
                Frame(1279, .1, [Actor(2, 0x233C, c, cast: Cast(raidwide, 5, c)),
                    Actor(3, 0x233C, c, cast: Cast(tankbuster, 9, c, target: 101))], party: party),
                Frame(1279, 20, [], party: party),
            ]);
            controller.Seek(.2);
            var predictions = Models("PredictedDamage");
            Check(predictions.Length == 3
                && predictions.Count(p => (string)Value(p, "DamageType") == "Raidwide"
                    && (DateTime)Value(p, "Activation") == Start.AddSeconds(.1 + 5 + .3)
                    && new ulong[] { 100, 101 }.Contains((ulong)Value(p, "TargetId"))) == 2
                && predictions.Single(p => (string)Value(p, "DamageType") == "Tankbuster") is { } tb
                && (ulong)Value(tb, "TargetId") == 101 && (DateTime)Value(tb, "Activation") == Start.AddSeconds(.1 + 9 + .3),
                $"Independent raidwide/tankbuster target, type or actual cast clock differs for {oid:X}: "
                    + string.Join("; ", predictions.Select(p => $"{Value(p, "DamageType")}:{Value(p, "TargetId")}@{(DateTime)Value(p, "Activation"):O}")));
            stop();
        }
        Console.WriteLine("PASS native treasure production flowers raidwide and tankbuster independent targets types and clocks5 and9");

        var origin = c + new Vector3(.13f, 0, .17f);
        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48AE, c, combat: true), .. players], full: true, party: party),
            Frame(1279, .1, [Actor(2, 0x233C, c, cast: Cast(43710, 5, origin)),
                Actor(3, 0x233C, c, cast: Cast(43713, 7, origin + Vector3.UnitX * 5))], party: party),
            Frame(1279, 20, [Actor(2, 0x233C, c)], party: party),
            Frame(1279, 21, [Actor(4, 0x233C, c, cast: Cast(43712, 2, origin + new Vector3(1, 0, 1), rotation: 2))], party: party),
            Frame(1279, 23, [Actor(4, 0x233C, c)], party: party),
            Frame(1279, 30, [Actor(3, 0x233C, c)], party: party),
            Frame(1279, 33, [Actor(5, 0x233C, c, cast: Cast(43709, 1, origin + Vector3.UnitX * 5, rotation: -2))], party: party),
            Frame(1279, 34, [Actor(5, 0x233C, c)], party: party), Frame(1279, 35, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Models("TreasureIceSequences").Length == 2 && dangers() == 2
            && (Vector3)Value(Models("TreasureIceSequences")[0], "Origin") == origin
            && (float)Value(Value(Aoes("treasure/ice/")[1], "Shape"), "OuterRadius") == 30,
            "Independent opposite concentric origins or initial shapes differ.");
        controller.Seek(20.1);
        Check((int)Value(Models("TreasureIceSequences")[0], "Order") == 1
            && (DateTime)Value(Models("TreasureIceSequences")[0], "Activation") == Start.AddSeconds(22)
            && (int)Value(Models("TreasureIceSequences")[1], "Order") == 0,
            "Concentric first true Finish failed consumption clock+2 or advanced unrelated sequence.");
        controller.Seek(23.1);
        Check(Models("TreasureIceSequences").Length == 1 && dangers() == 1,
            "Axis1 cross-source Finish failed default-angle matching.");
        controller.Seek(30.1);
        Check((DateTime)Value(Models("TreasureIceSequences").Single(), "Activation") == Start.AddSeconds(32)
            && (float)Value(Value(Aoes("treasure/ice/").Single(), "Shape"), "Radius") == 10,
            "Opposite concentric second shape or consumption clock differs.");
        controller.Seek(34.1);
        Check(Models("TreasureIceSequences").Length == 0 && dangers() == 0, "Final concentric Finish retained output.");
        controller.Seek(20.1);
        Check(Models("TreasureIceSequences").Length == 2 && dangers() == 2, "Concentric reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Paeonia independent opposite concentric30 source origins cross-source axis1 default angle consumption clock2 true Finish reverse Seek");

        var normal = Enumerable.Range(0, 8).Select(i => Actor((ulong)i + 2, 0x233C, c,
            cast: Cast(i % 2 == 0 ? 43773u : 43774u, 20 - i, c))).ToArray();
        var high = Enumerable.Range(0, 4).Select(i => Actor((ulong)i + 20, 0x233C, c,
            cast: Cast((uint)(43778 + i), 10 - i, c))).ToArray();
        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48B5, c, combat: true), .. players], full: true, party: party),
            Frame(1279, .1, [.. normal, .. high], party: party),
            Frame(1279, 1, [Actor(23, 0x233C, c)], party: party),
            Frame(1279, 2, [Actor(22, 0x233C, c), Actor(21, 0x233C, c)], party: party),
            Frame(1279, 3, [], party: party),
        ]);
        controller.Seek(.2);
        Check(dangers() == 7 && Models("TreasureVaultGroups").Length == 8 && Models("TreasureHighVoltage").Length == 4
            && Aoes("treasure/high-voltage/").Length == 3
            && Aoes("treasure/high-voltage/").Single(a => (string)Value(a, "Id") == "treasure/high-voltage/23") is { } first
            && (Vector4)Value(first, "Color") == new Vector4(1, 1, 0, 1)
            && Find("treasure/high-voltage/20").Length == 0, "Independent expected8/4 limits or first yellow differs.");
        controller.Seek(1.1);
        Check(dangers() == 7 && Find("treasure/high-voltage/20").Length == 1, "HighVoltage fifth promotion failed.");
        controller.Seek(2.1);
        Check(dangers() == 5 && Aoes("treasure/high-voltage/").Single() is { } last && Value(last, "Color") == null,
            "Last HighVoltage danger color or ordinary queue isolation failed.");
        stop();
        Console.WriteLine("PASS native treasure production Spirit independent expected8 normal first4 and expected4 high first3 allRisky yellow first hidden promotion single default color");

        var portals = Enumerable.Range(0, 6).Select(i => Actor((ulong)i + 50, 0x48B6,
            c + new Vector3(i + .13f, 0, .17f), rotation: .5f + i)).ToArray();
        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48B5, c, combat: true), .. players, .. portals], full: true, party: party),
            Frame(1279, 1, [Actor(50, 0x48B6, c + Vector3.UnitX * 10, dead: true)], removed: [51], party: party),
            TreasureNativeEffect(2, 50, 43788, 1, 100, []),
            Frame(1279, 20, [Actor(2, 0x233C, c, cast: Cast(43788, 5, c, startedAt: 20))], party: party),
            Frame(1279, 21, [Actor(2, 0x233C, c)], party: party), Frame(1279, 22, [], party: party),
        ]);
        controller.Seek(20.1);
        Check(dangers() == 6 && Models("TreasureBeams").Length == 6 && Find("treasure/beam/").Length == 6,
            "PowerLine capacity incorrectly limited display or Destroy/effect/timeout consumed beams.");
        var beam = Aoes("treasure/beam/").First();
        Check((float)Value(Value(beam, "Shape"), "Length") == 40
            && (float)Value(Value(beam, "Shape"), "MaxLocalX") == 5
            && (DateTime)Value(Models("TreasureBeams")[0], "Activation") == Start.AddSeconds(7.7)
            && MathF.Abs(((Vector3)Value(beam, "Position")).Z - 3281.5f * (2000f / 65535f)) < .0001f,
            "PowerLine original quantized origin40x10/+7.7 differs.");
        controller.Seek(21.1);
        Check(dangers() == 0 && Models("TreasureBeams").Length == 0, "Any source PowerLine Finish failed all clear.");
        controller.Seek(20.1);
        Check(dangers() == 6, "PowerLine reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Spirit all6 Created PowerLine original quantized position40x10 and7.7 default retention any Finish clears all reverse Seek");

        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48B5, c, combat: true), .. players], full: true, party: party),
            Frame(1279, .1, [Actor(2, 0x233C, c, cast: Cast(43784, 5, c, target: 101))], party: party),
            TreasureNativeEffect(.5, 2, 43784, 1, 101, []),
            Frame(1279, 1, [Actor(101, 0, c + Vector3.UnitZ * 6, dead: true)], party: party),
            Frame(1279, 2, [], removed: [101], party: party),
            Frame(1279, 3, [Actor(101, 0, c + Vector3.UnitX * 20)], party: party),
            Frame(1279, 4, [Actor(2, 0x233C, c)], party: party), Frame(1279, 5, [], party: party),
        ]);
        controller.Seek(3.1);
        var bait = Aoes("treasure/cast-bait/").Single(a => (bool)Value(a, "Display"));
        Check((float)Value(Value(bait, "Shape"), "Radius") == 6
            && (Vector3)Value(bait, "Position") == c + Vector3.UnitZ * 6 && dangers() == 1,
            $"Finish-only circle bait lost dead target or rebound destroyed reference: position={Value(bait, "Position")}, dangers={dangers()}.");
        controller.Seek(4.1);
        Check(dangers() == 0 && !Find("treasure/cast-bait/").Any(), "True Finish retained Hypercondensed bait.");
        stop();
        Console.WriteLine("PASS native treasure production Spirit dynamic target circle6 dead retention destroyed reference frozen effect retention actual Finish cleanup");
    }
}
