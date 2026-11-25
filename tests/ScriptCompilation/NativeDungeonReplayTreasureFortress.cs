using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureFortress(object managerObject, object runtimeObject, object controllerObject,
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
        object[] Aoes(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(key)).ToArray();
        const string guid = "masterplayer.treasure.vault-oneiron";
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object[] players = [Actor(100, 0, c + Vector3.UnitX * 15), Actor(101, 0, c + Vector3.UnitZ * 10)];
        uint[] bitter = [43675, 43678, 43683, 43680, 43682, 43681, 43677, 43685, 43684, 43676,
            43727, 43726, 32301, 32302, 32303, 32304, 32305];
        uint[] fortress = [43732, 43744, 43746, 43734, 43735, 43736, 43737, 43742, 43748, 43739, 43740, 43741,
            43727, 43726, 32301, 32302, 32303, 32304, 32305];
        foreach (var (oid, actions, expected) in new[] { (0x48AAu, bitter, 14), (0x48B2u, fortress, 15) })
        {
            var casts = actions.Select((a, i) => Actor((ulong)i + 2, 0x233C, c,
                cast: Cast(a, 5, c, rotation: .37f, target: 101))).ToArray();
            var effectAction = oid == 0x48AA ? 43682u : 43748u;
            var effectSource = (ulong)Array.IndexOf(actions, effectAction) + 2;
            load(guid,
            [
                Header(1279), Frame(1279, 0, [Actor(1, oid, c, combat: true), .. players], full: true, party: party),
                Frame(1279, .1, casts, party: party), Frame(1279, 50, [], party: party),
                Frame(1279, 51, casts.Select((_, i) => Actor((ulong)i + 2, 0x233C, c)).ToArray(), party: party),
                TreasureNativeEffect(52, effectSource, effectAction, 1, 101, []), Frame(1279, 53, [], party: party),
            ]);
            controller.Seek(.2);
            Check(dangers() == expected && Models("PredictedDamage").Length == 3, $"{oid:X} all-rules navigation/damage differs: {dangers()}/{expected}.");
            Check(actions.All(a => (bool)manager.IsGenericDodgeSuppressed(new PromeRotation.LogSystem.LogSystemCastStartEvent
                { SourceId = 2, ActionId = (ushort)a })), "Source active actions not suppressed.");
            if (oid == 0x48B2)
            {
                var spread = Aoes("quest/spread/");
                Check((float)Value(Value(spread.Single(a => (bool)Value(a, "Display")), "Shape"), "Radius") == 6
                    && (float)Value(Value(spread.Single(a => (bool)Value(a, "Navigate")), "Shape"), "Radius") == 7,
                    "Guided Missile spread6/7 differs.");
            }
            controller.Seek(50.1);
            Check(dangers() == expected, "Predicted finish expired source active mechanics.");
            controller.Seek(51.1);
            Check(dangers() == 1 && Aoes("treasure/cast-bait/").Length >= 2, "Finish consumed effect-only bait or retained other mechanics.");
            controller.Seek(52.1);
            Check(dangers() == 0 && !Aoes("treasure/cast-bait/").Any(), "Real effect retained bait.");
            controller.Seek(.2);
            Check(dangers() == expected, "Reverse Seek failed.");
            stop();
            Console.WriteLine($"PASS native treasure production {oid:X} all{actions.Length} active rules independent retained clocks dual views navigation source damage Finish and Effect separation Seek owner cleanup");
        }

        var carpets = Enumerable.Range(0, 8).Select(i => Actor((ulong)i + 2, 0x233C, c,
            cast: Cast((uint)(43734 + i % 4), 20 + i, c, rotation: .3f + i))).ToArray();
        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48B2, c, combat: true), .. players], full: true, party: party),
            Frame(1279, .1, carpets, party: party),
            Frame(1279, 1, [Actor(2, 0x233C, c)], party: party),
            Frame(1279, 2, [Actor(5, 0x233C, c)], party: party), Frame(1279, 3, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Models("TreasureCarpet").Length == 8 && Aoes("treasure/carpet/").Length == 4 && dangers() == 2
            && Aoes("treasure/carpet/").Take(2).All(a => (Vector4)Value(a, "Color") == new Vector4(1, 1, 0, 1))
            && MathF.Abs((float)Value(Aoes("treasure/carpet/")[1], "Rotation") - 1.3f) < .0001,
            "Carpet expanded display4 risk2 yellow2 or locked facing differs.");
        controller.Seek(1.1);
        Check(Models("TreasureCarpet").Length == 7 && Aoes("treasure/carpet/").Length == 4 && dangers() == 2,
            "Count7 incorrectly reverted expanded mode.");
        controller.Seek(2.1);
        Check(Aoes("treasure/carpet/").Length == 2 && dangers() == 1, "Any IV Finish failed default display2/risk1 reset.");
        controller.Seek(.2);
        Check(dangers() == 2, "Carpet Seek failed expanded state rebuild.");
        stop();
        Console.WriteLine("PASS native treasure production Fortress Carpet exact8 expanded4 risk2 yellow2 count7 retention IV reset locked facing dual views Seek");

        foreach (var same in new[] { false, true })
        {
            var quiet = Enumerable.Range(0, 5).Select(i => Actor((ulong)i + 2, 0x233C, c,
                cast: Cast(43732, 5 + i, c, rotation: same ? .5f : i % 2))).ToArray();
            load(guid,
            [
                Header(1279), Frame(1279, 0, [Actor(1, 0x48B2, c, combat: true), .. players], full: true, party: party),
                Frame(1279, .1, quiet, party: party), Frame(1279, 20, [], party: party),
            ]);
            controller.Seek(.2);
            Check(Models("TreasureQuietWorld").Length == 5 && dangers() == (same ? 2 : 4),
                "Quiet prefix lost exact rotation conjunction or time window.");
            stop();
        }
        Console.WriteLine("PASS native treasure production Fortress Quiet insertion prefix max4 deadline3.5 and exact dual-rotation conjunction including zero facing");
        foreach (var gap in new[] { 3.5, 3.49 })
        {
            load(guid,
            [
                Header(1279), Frame(1279, 0, [Actor(1, 0x48B2, c, combat: true), .. players], full: true, party: party),
                Frame(1279, .1, [Actor(2, 0x233C, c, cast: Cast(43732, 5, c)),
                    Actor(3, 0x233C, c, cast: Cast(43732, 5 + gap, c, rotation: 1))], party: party),
                Frame(1279, 20, [], party: party),
            ]);
            controller.Seek(.2);
            Check(dangers() == (gap == 3.5 ? 1 : 2), "Quiet exact3.5 cutoff or inside boundary differs.");
            stop();
        }
        Console.WriteLine("PASS native treasure production Fortress Quiet exact3.5 cutoff excludes boundary3.49 includes separate from prefix maximum");

        var systematic = Enumerable.Range(0, 16).Select(i => Actor((ulong)i + 2, 0x233C, c,
            cast: Cast((uint)(43739 + i % 3), 40 - i * 2, c, rotation: .25f + i))).ToArray();
        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48B2, c, combat: true), .. players], full: true, party: party),
            Frame(1279, .1, systematic.Take(15).ToArray(), party: party),
            Frame(1279, 1, [systematic[15]], party: party),
            TreasureNativeEffect(2, 2, 43739, 1, 100, []),
            Frame(1279, 3, [Actor(2, 0x233C, c)], party: party), Frame(1279, 4, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Aoes("treasure/systematic/").Length == 15 && dangers() == 15
            && ((string)Value(Aoes("treasure/systematic/")[0], "Id")).Contains("/2/"), "Before16 sorting, all display or risk promotion differs.");
        controller.Seek(1.1);
        Check(Aoes("treasure/systematic/").Length == 9 && dangers() == 9
            && ((string)Value(Aoes("treasure/systematic/")[0], "Id")).Contains("/17/"), "Exact16 sort/max9 or sticky Risky differs.");
        controller.Seek(2.1);
        Check(Models("TreasureSystematic").Length == 16, "Effect consumed Finish-only Systematic.");
        controller.Seek(3.1);
        Check(Aoes("treasure/systematic/").Length == 15 && dangers() == 15
            && ((string)Value(Aoes("treasure/systematic/")[0], "Id")).Contains("/16/"),
            "Different source Finish did not delete global first or retain promoted flags.");
        controller.Seek(.2);
        Check(Aoes("treasure/systematic/").Length == 15 && dangers() == 15, "Systematic reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Fortress Systematic insertion15 exact16 sort9 globally consumed first source mismatch sticky Risky Danger effect retention locked clocks Seek");
        var near = Enumerable.Range(0, 8).Select(i => Actor((ulong)i + 2, 0x233C, c,
            cast: Cast(43739, 5 + i * .1, c, rotation: .25f))).ToArray();
        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48B2, c, combat: true), .. players], full: true, party: party),
            Frame(1279, .1, near, party: party), Frame(1279, 1, [Actor(2, 0x233C, c)], party: party),
            Frame(1279, 2, [], party: party),
        ]);
        controller.Seek(.2);
        Check(dangers() == 8 && Aoes("treasure/systematic/").All(a => (Vector4)Value(a, "Color") == new Vector4(1, 1, 0, 1)),
            "Eight near Systematic casts failed danger promotion.");
        controller.Seek(1.1);
        Check(dangers() == 7 && Aoes("treasure/systematic/").All(a => (Vector4)Value(a, "Color") == new Vector4(1, 1, 0, 1)),
            "Count7 reverted sticky Systematic danger color.");
        stop();
        Console.WriteLine("PASS native treasure production Fortress Systematic8 near first deadline all risky yellow persistent after decrement7");

        load(guid,
        [
            Header(1279), Frame(1279, 0, [Actor(1, 0x48AA, c, combat: true), .. players], full: true, party: party),
            Frame(1279, .1, [Actor(2, 0x233C, c, cast: Cast(43684, 5, c))], party: party),
            Frame(1279, 1, [Actor(2, 0x233C, c)], party: party),
            Frame(1279, 2, [Actor(3, 0x233C, c, cast: Cast(43683, 4, new(99.99231f, 0, 99.99231f), startedAt: 2))], party: party),
            Frame(1279, 3, [Actor(3, 0x233C, c)], party: party),
            Frame(1279, 4, [Actor(4, 0x233C, c, cast: Cast(43683, 4, c, startedAt: 4))], party: party),
            Frame(1279, 30, [Actor(4, 0x233C, c)], party: party),
            Frame(1279, 31, [Actor(5, 0x233C, c, cast: Cast(43676, 1, c, startedAt: 31))], party: party),
            Frame(1279, 32, [Actor(5, 0x233C, c)], party: party), Frame(1279, 33, [], party: party),
        ]);
        controller.Seek(.2);
        var pull = Models("TreasureWallKnockbackSources").Single();
        Check((float)Value(pull, "Distance") == 50 && (float)Value(pull, "MinDistance") == 4.9f
            && !(bool)Value(pull, "StopAtWall") && (bool)Value(pull, "TowardsOrigin") && dangers() == 1
            && Aoes("treasure/kb/").All(a => !(bool)Value(a, "Navigate")),
            "Pull original model or display-only adaptation differs.");
        controller.Seek(3.1);
        Check(!Models("TreasureWallKnockbackSources").Any()
            && (float)Value(Value(Aoes("treasure/bitter/").Single(), "Shape"), "Radius") == 13,
            "Exact center/cancel or pull Finish changed independent prediction.");
        controller.Seek(30.1);
        Check((DateTime)Value(Value(Encounter(), "TreasureBitterPrediction"), "FinishAt") == Start.AddSeconds(30).AddSeconds((4f + .3f - 4f) + 7.1d)
            && (float)Value(Value(Aoes("treasure/bitter/").Single(), "Shape"), "InnerRadius") == 5,
            "Non-exact100 center branch failed source capped NPCRemainingTime+7.1.");
        controller.Seek(32.1);
        Check(dangers() == 0 && !Aoes("treasure/bitter/").Any(), "Other source Hundred Finish failed all clear.");
        controller.Seek(.2);
        Check(dangers() == 1, "Bitter reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Bitter pull50 min4.9 model display-only exact float center keeps prediction late Finish capped source remaining7.1 any source Hundred clears Seek");
    }
}
