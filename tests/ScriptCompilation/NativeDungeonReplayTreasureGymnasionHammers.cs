using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureGymnasionHammers(object managerObject, object runtimeObject, object controllerObject,
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
        object[] Models(string property) => ((IEnumerable)Value(Encounter(), property)).Cast<object>().ToArray();
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        object[] Aoes(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(key)).ToArray();
        const string guid = "masterplayer.treasure.shifting-gymnasion";
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object[] players = [Actor(100, 0, c + Vector3.UnitZ * 8), Actor(101, 0, c + Vector3.UnitX * 8)];
        foreach (var (oid, actions, single, raidwide) in new[]
        {
            (0x3D3Eu, new uint[] { 32279, 32286, 32278, 32317, 32301, 32302, 32303, 32304, 32305 }, 32275u, 32276u),
            (0x3D49u, new uint[] { 32357, 32351, 32353, 32317, 32301, 32302, 32303, 32304, 32305 }, 32341u, 32356u),
        })
        {
            var casting = actions.Select((action, i) => Actor((ulong)i + 2, 0x233C, c,
                cast: Cast(action, 5 + i, c))).ToArray();
            load(guid,
            [
                Header(1123), Frame(1123, 0, [Actor(1, oid, c, combat: true), .. players], full: true, party: party),
                Frame(1123, .1, [.. casting, Actor(30, 0x233C, c, cast: Cast(single, 5, c, target: 101)),
                    Actor(31, 0x233C, c, cast: Cast(raidwide, 6, c))], party: party),
                Frame(1123, 30, [], party: party),
                Frame(1123, 31, [.. casting.Select((_, i) => Actor((ulong)i + 2, 0x233C, c)), Actor(30, 0x233C, c), Actor(31, 0x233C, c)], party: party),
                Frame(1123, 32, [], party: party),
            ]);
            controller.Seek(.2);
            Check(dangers() == actions.Length && Models("PredictedDamage").Length == 3,
                "Full ordinary/raidwide/current-target output differs.");
            Check(actions.All(action => (bool)manager.IsGenericDodgeSuppressed(
                new PromeRotation.LogSystem.LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action })),
                "Full active ordinary suppression missing.");
            controller.Seek(30.1);
            Check(dangers() == actions.Length, "Ordinary source predicted clock expired.");
            controller.Seek(31.1);
            Check(dangers() == 0 && !Models("PredictedDamage").Any(), "True Finish retained ordinary output.");
            controller.Seek(.2);
            Check(dangers() == actions.Length, "Ordinary reverse Seek failed.");
            stop();
            Console.WriteLine($"PASS native treasure production gymnasion {oid:X} complete ordinary rules predictions independent clocks true Finish dual views navigation Seek");
        }
        load(guid,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D3E, c, combat: true), .. players], full: true, party: party),
            Frame(1123, .1, [Actor(2, 0x233C, c, cast: Cast(32284, 5, c, rotation: 2, startedAt: .1))], party: party),
            Frame(1123, .2, [Actor(3, 0x233C, c, cast: Cast(32283, 5, c, startedAt: .2))], party: party),
            Frame(1123, .3, [Actor(4, 0x233C, c + new Vector3(3.12345f, 0, 4.98765f),
                cast: Cast(32858, 5, c, rotation: .7f, startedAt: .3))], party: party),
            Frame(1123, .4, [Actor(5, 0x233C, c, cast: Cast(32860, 5, c, startedAt: .4))], party: party),
            Frame(1123, 1, [Actor(5, 0x233C, c)], party: party),
            Frame(1123, 2, [], removed: [4], party: party),
            Frame(1123, 3, [Actor(6, 0x233C, c, cast: Cast(32861, 1, c, startedAt: 3))], party: party),
            Frame(1123, 4, [Actor(6, 0x233C, c)], party: party),
            Frame(1123, 5, [Actor(7, 0x233C, c, cast: Cast(32860, 1, c, startedAt: 5))], party: party),
            Frame(1123, 6, [Actor(7, 0x233C, c)], party: party), Frame(1123, 7, [], party: party),
        ]);
        controller.Seek(.5);
        Check(Find("treasure/double-hammer/").Length == 2 && Find("treasure/rotation/").Length == 2 && dangers() == 3,
            "Double first-only risk/quad two-risk previews differ.");
        var doubles = Models("TreasureDoubleHammers");
        Check((float)Value(doubles[0], "Rotation") == -1.5708661079406738f
            && (float)Value(doubles[1], "Rotation") == 1.5707788467407227f
            && (DateTime)Value(doubles[0], "Activation") == Start.AddSeconds(.1 + 5 + .3).AddSeconds((double).8f)
            && (DateTime)Value(doubles[1], "Activation") == Start.AddSeconds(.1 + 5 + .3).AddSeconds((double)3.7f),
            "Double absolute angle/future clock differs.");
        var quad = Models("TreasureRotations").Single();
        Check((Vector3)Value(quad, "Origin") == c + new Vector3(3.12345f, 0, 4.98765f)
            && (float)Value(quad, "Rotation") == .7f && (float)Value(quad, "Increment") == MathF.PI / 2
            && (DateTime)Value(quad, "NextActivation") == Start.AddSeconds(.3 + 5 + .3).AddSeconds(.8d),
            "Quad raw origin, initial rotation/increment or first activation differs.");
        controller.Seek(1.1);
        Check(Models("TreasureDoubleHammers").Length == 1 && (int)Value(Models("TreasureRotations").Single(), "Remaining") == 3
            && (DateTime)Value(Models("TreasureRotations").Single(), "NextActivation") == Start.AddSeconds(1).AddSeconds((double)3.3f),
            "Shared Finish did not advance independent double and quad.");
        controller.Seek(2.1);
        Check((int)Value(Models("TreasureRotations").Single(), "Remaining") == 2
            && Models("TreasureDoubleHammers").Length == 1, "Destroyed first hammer Finish advanced wrong components.");
        controller.Seek(4.1);
        Check(!Models("TreasureDoubleHammers").Any() && (int)Value(Models("TreasureRotations").Single(), "Remaining") == 1
            && dangers() == 1, "Last quad/double independent completion failed.");
        controller.Seek(6.1);
        Check(dangers() == 0 && !Models("TreasureRotations").Any(), "Fourth quad Finish retained.");
        controller.Seek(.5);
        Check(dangers() == 3, "Concurrent hammer reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production gymnasion concurrent double and quadruple exact angles raw origin independent Finish destroy consumption3.3 clocks previews navigation Seek");

        load(guid,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D3E, c, combat: true), .. players], full: true, party: party),
            Frame(1123, .1, [Actor(2, 0x233C, c, cast: Cast(32281, 5, c, rotation: 2, startedAt: .1))], party: party),
            Frame(1123, .2, [Actor(3, 0x233C, c, cast: Cast(32858, 5, c, rotation: .7f, startedAt: .2))], party: party),
            Frame(1123, .3, [Actor(4, 0x233C, c, cast: Cast(32280, 5, c, startedAt: .3))], party: party),
            Frame(1123, .4, [Actor(5, 0x233C, c, cast: Cast(32859, 5, c, startedAt: .4))], party: party),
            Frame(1123, 1, [Actor(5, 0x233C, c)], party: party), Frame(1123, 2, [], party: party),
        ]);
        controller.Seek(.5);
        doubles = Models("TreasureDoubleHammers");
        quad = Models("TreasureRotations").Single();
        Check((float)Value(doubles[0], "Rotation") == 1.5707788467407227f
            && (float)Value(doubles[1], "Rotation") == -1.5708661079406738f
            && (float)Value(quad, "Rotation") == .7f && (float)Value(quad, "Increment") == -MathF.PI / 2
            && (DateTime)Value(quad, "NextActivation") == Start.AddSeconds(.3 + 5 + .3).AddSeconds(.8d),
            "Reverse double/quad angle order or last callback clock differs.");
        controller.Seek(1.1);
        Check(Models("TreasureDoubleHammers").Length == 1
            && (int)Value(Models("TreasureRotations").Single(), "Remaining") == 4,
            "32859 should independently consume only the double hammer.");
        stop();
        Console.WriteLine("PASS native treasure production gymnasion reverse hammer angles initial clocks and32859 independent double consumption");

        foreach (var (action, offsets) in new[]
        {
            (33164u, new[] { 0f, MathF.PI }),
            (32342u, new[] { 0f, MathF.PI, MathF.PI / 2, -MathF.PI / 2 }),
            (32343u, new[] { 0f, -MathF.PI / 2, MathF.PI, MathF.PI / 2 }),
            (32344u, new[] { 0f, MathF.PI / 2, -MathF.PI / 2, MathF.PI }),
        })
        {
            var count = offsets.Length;
            uint[] effects = action is 32343 or 32344 ? [32346, 32347, 32348, 32349] : [32345, 33165, 32348, 32349];
            load(guid,
            [
                Header(1123), Frame(1123, 0, [Actor(1, 0x3D49, c, combat: true), Actor(4, 0x233C, c), .. players], full: true, party: party),
                Frame(1123, .1, [Actor(2, 0x233C, c, cast: Cast(action, 3, c, rotation: .7f, startedAt: .1))], party: party),
                Frame(1123, 10, [], removed: [2], party: party),
                .. Enumerable.Range(0, count).Select(i => TreasureNativeEffect(20 + i, 4,
                    effects[i], (uint)i + 1, 100, [])),
                Frame(1123, 25, [], party: party),
            ]);
            controller.Seek(.2);
            Check(Models("TreasureBlasters").Length == count && Find("treasure/blaster/").Length == 1
                && dangers() == 2 && Aoes("treasure/blaster/center").Length == 1,
                "Hippomenes first cone/hidden center navigation missing.");
            var cones = Models("TreasureBlasters");
            for (var i = 0; i < count; ++i)
                Check((float)Value(cones[i], "Rotation") == .7f + offsets[i]
                    && (Vector3)Value(cones[i], "Origin") == c
                    && (DateTime)Value(cones[i], "Activation") == Start.AddSeconds(.1 + 3 + .3).AddSeconds(6f + 3f * i),
                    $"Blaster {action} order{i} source angle/origin/activation differs.");
            var coneShape = Value(Aoes("treasure/blaster/").Single(a => (bool)Value(a, "Display")), "Shape");
            Check((float)Value(coneShape, "Radius") == 30
                && (float)Value(coneShape, "AngleRadians") == 3 * MathF.PI / 2,
                "Blaster radius30/full270 geometry differs.");
            var centerShape = Value(Aoes("treasure/blaster/center").Single(), "Shape");
            var shelter = ((IEnumerable)Value(centerShape, "Shelters")).Cast<object>().Single();
            Check((float)Value(shelter, "Radius") == 5 && (Vector3)Value(shelter, "Position") == c,
                "Blaster center must constrain the original charge origin to exactly radius5.");
            var second = cones[1];
            controller.Seek(10.1);
            Check(Models("TreasureBlasters").Length == count && dangers() == 2,
                "Hippomenes destruction/Finish/expected expiry cleared history.");
            controller.Seek(20.1);
            Check(Models("TreasureBlasters").Length == count - 1
                && Value(Models("TreasureBlasters")[0], "Rotation").Equals(Value(second, "Rotation"))
                && Value(Models("TreasureBlasters")[0], "Activation").Equals(Value(second, "Activation")),
                "Hippomenes Native effect failed global FIFO/preserved clock.");
            controller.Seek(20 + count - 1 - .1);
            Check(Models("TreasureBlasters").Length == 1 && dangers() == 1
                && !Aoes("treasure/blaster/center").Any(), "Hippomenes last cone retained center restriction.");
            controller.Seek(24.1);
            Check(dangers() == 0 && !Models("TreasureBlasters").Any(), "Hippomenes final effect retained.");
            controller.Seek(.2);
            Check(dangers() == 2, "Hippomenes reverse Seek failed.");
            stop();
            Console.WriteLine($"PASS native treasure production Hippomenes {action} true native effect global FIFO first-only270 center5 history clocks destroy retention Seek");
        }
        load(guid,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D49, c, combat: true), .. players], full: true, party: party),
            Frame(1123, .1, [Actor(2, 0x233C, c, cast: Cast(32354, 5, c, target: 100))], party: party),
            Frame(1123, 1, [Actor(100, 0, c + Vector3.UnitZ * 8, dead: true)], party: party),
            Frame(1123, 2, [Actor(100, 0, c + Vector3.UnitZ * 8)], party: party),
            Frame(1123, 3, [Actor(2, 0x233C, c)], party: party), Frame(1123, 4, [], party: party),
        ]);
        controller.Seek(.2);
        Check((float)Value(Value(Aoes("quest/spread/").Single(a => (bool)Value(a, "Display")), "Shape"), "Radius") == 6
            && Aoes("quest/spread/").Any(a => ((string)Value(a, "Id")).EndsWith("/separate/101")
                && (float)Value(Value(a, "Shape"), "Radius") == 7) && !Models("PredictedDamage").Any(),
            "Hippomenes self spread6/7 or no forecast differs.");
        controller.Seek(1.1);
        Check(!Find("quest/spread/").Any() && dangers() == 0, "Dead spread not hidden.");
        controller.Seek(2.1);
        Check(Find("quest/spread/").Length == 1 && dangers() == 1, "Spread revival not restored.");
        controller.Seek(3.1);
        Check(dangers() == 0 && !Find("quest/spread/").Any(), "Spread true Finish retained.");
        stop();
        Console.WriteLine("PASS native treasure production Hippomenes self-target spread6 and7 navigation no forecast dead revival true Finish owner cleanup");
    }
}
