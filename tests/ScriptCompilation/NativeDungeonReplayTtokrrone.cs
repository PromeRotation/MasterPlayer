using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTtokrrone(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.fate.ttokrrone";
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var center = new Vector3(53, 0, -820);
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Boss(object? cast = null) => Actor(1, 0x41DF, center, combat: true, cast: cast);
        object Helper(ulong id = 10, object? cast = null) => Actor(id, 0x4225, center, cast: cast);
        object[] Aoes() => previews().Where(p => ((string)Value(p, "Id")).Contains("ttokrrone/aoe/")).ToArray();
        string Native(double t, uint action, uint sequence) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 10ul, ActionId = action, MainTargetId = 100ul, GlobalSequence = sequence,
                Targets = new ulong[] { 100, 101 }, Timestamp = Start.AddSeconds(t) },
        });
        void Isolated()
        {
            var script = ((IEnumerable)manager.Running.Values).Cast<object>().Select(s => Value(s, "Script"))
                .Single(s => s.GetType().Name == "Ttokrrone");
            var hints = Value(script, "Hints");
            var facing = hints.GetType().GetField("_facing", flags)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", flags)!.GetValue(facing)!
                && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any()
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Ttokrrone replay leaked PR control.");
        }

        load(guid, [Header(1190), Frame(1190, 0, [Boss(), Actor(100, 0, center)], full: true), Frame(1190, 1, [])]);
        controller.Seek(.2);
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(((IEnumerable)boundary.GetContours()[0]).Cast<object>().Count() == 48, "Ttokrrone actual polygon lost vertices.");
        var source = Assembly.Load("MasterPlayer.MechanicRuntime");
        dynamic oracle = source.GetType("BossMod.Dawntrail.FATE.Ttokrrone.Ttokrrone")!
            .GetField("arena", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var sourceDir = source.GetType("BossMod.WDir")!;
        var contains = ((object)oracle).GetType().GetMethod("Contains")!;
        var clamp = ((object)oracle).GetType().GetMethod("ClampToBounds", [sourceDir.MakeByRefType()])!;
        var marker = managerObject.GetType().Assembly.GetType("MasterPlayer.Mechanics.Dungeons.DungeonEncounter")!
            .GetMethod("TtokrroneMarkerPosition", BindingFlags.NonPublic | BindingFlags.Static)!;
        var compared = 0;
        for (var x = -32; x <= 32; ++x)
            for (var z = -32; z <= 32; ++z)
            {
                var point = center + new Vector3(x + .137f, 0, z + .173f);
                var offset = Activator.CreateInstance(sourceDir, [point.X - center.X, point.Z - center.Z])!;
                Check((bool)boundary.Contains(point, 0f) == (bool)contains.Invoke((object)oracle, [offset])!,
                    $"Ttokrrone polygon oracle mismatch at {point}.");
                ++compared;
            }
        foreach (var delta in new[] { new Vector3(0, 0, 100), new(.05f, 0, 100), new(.1f, 0, 100),
            new(1, 0, 100), new(100, 0, 80), new(-55, 0, -60),
            new(29.48f * MathF.Sin(MathF.PI / 48), 0, 29.48f * MathF.Cos(MathF.PI / 48)) })
        {
            var point = center + delta;
            var offset = Activator.CreateInstance(sourceDir, [point.X - center.X, point.Z - center.Z])!;
            dynamic expected = clamp.Invoke((object)oracle, [offset])!;
            var actual = (Vector3)marker.Invoke(null, [(object)boundary, point])!;
            Check(Vector3.Distance(actual, center + new Vector3((float)expected.X, 0, (float)expected.Z)) < .003f,
                $"Ttokrrone marker source clamp mismatch at {delta}: {actual} vs {expected}.");
        }
        Isolated();
        stop();
        Console.WriteLine($"PASS native fate Ttokrrone production exact48 arena {compared} source oracle points marker clamp and replay isolation");

        load(guid,
        [
            Header(1190), Frame(1190, 0, [Boss(), Helper(), Actor(100, 0, center)], full: true),
            Frame(1190, .1, [Boss(Cast(37333, 8, center, .3f))]),
            Frame(1190, 1, [Boss()]), Native(2, 37337, 1),
            Frame(1190, 3, [Helper(cast: Cast(37338, 1, center, startedAt: 3))]),
            Frame(1190, 4, [Helper()]), Frame(1190, 5, []),
        ]);
        controller.Seek(.5);
        Check(Aoes().Length == 2 && dangers() == 2, "Tempest paired sides lack dual previews/navigation.");
        controller.Seek(2.2);
        Check(Aoes().Length == 2 && dangers() == 2, "Visual cancel or native effect consumed Finish-only Tempest.");
        controller.Seek(4.2);
        Check(Aoes().Length == 0 && dangers() == 0, "Actual helper Finish failed global Tempest clear.");
        controller.Seek(.5);
        Isolated();
        stop();
        Console.WriteLine("PASS native fate Ttokrrone production Tempest locked two sides cancel retains effect ignores true Finish clears reverse Seek");

        foreach (var (visual, effect) in new[] { (37313u, 39813u), (37314u, 39814u), (37315u, 39843u), (37316u, 39816u) })
        {
            load(guid,
            [
                Header(1190), Frame(1190, 0, [Boss(), Actor(100, 0, center)], full: true),
                Frame(1190, .1, [Boss(Cast(visual, 8, center, .2f))]),
                Frame(1190, 1, [Boss()]), Native(2, effect, 1), Frame(1190, 3, []),
            ]);
            controller.Seek(1.2);
            Check(Aoes().Length == 2 && dangers() == 2, "Sandspout cancel removed paired cone/circle navigation.");
            controller.Seek(2.2);
            Check(Aoes().Length == 0 && dangers() == 0, "Sandspout Native effect failed global clear.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native fate Ttokrrone production all four Sandspout directions circle cone native package clear dual previews navigation and Seek");

        var records = new List<string>
        {
            Header(1190), Frame(1190, 0, [Boss(), Actor(100, 0, center),
                .. Enumerable.Range(10, 6).Select(id => Helper((ulong)id))], full: true),
            Frame(1190, .1, Enumerable.Range(0, 6).Select(i => Helper((ulong)(10 + i),
                Cast((uint)(37578 + i), 20 - i, center)))),
            Native(1, 38643, 1), Frame(1190, 2, []),
        };
        load(guid, records);
        controller.Seek(.5);
        Check(Aoes().Length == 6 && dangers() == 6, "All six Landswallow must be risky.");
        controller.Seek(1.2);
        Check(Aoes().Length == 5 && dangers() == 5, "Unnamed effect38643 missed FIFO or consumed per target.");
        stop();
        load(guid,
        [
            Header(1190), Frame(1190, 0, [Boss(), Actor(100, 0, center), Helper(10), Helper(11), Helper(12)], full: true),
            Frame(1190, .1, [Helper(10, Cast(38647, 10, center)), Helper(11, Cast(39245, 14.5, center)),
                Helper(12, Cast(39246, 11, center))]),
            Frame(1190, 1, [Helper(10)]), Frame(1190, 2, [Helper(12)]), Frame(1190, 3, []),
        ]);
        controller.Seek(.5);
        Check(Aoes().Length == 1 && dangers() == 1, "Sandspheres strict4.5 continuous prefix differs.");
        controller.Seek(1.2);
        Check(Aoes().Length == 2 && dangers() == 2, "Sandspheres hidden prefix did not promote.");
        controller.Seek(2.2);
        Check(Aoes().Length == 1 && dangers() == 1, "Sandspheres Finish did not match source.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native fate Ttokrrone production Landswallow six sorting numeric native FIFO and Sandspheres strict prefix source Finish promotion");

        foreach (var restart in new[] { false, true })
        {
            records =
            [
                Header(1190), Frame(1190, 0, [Boss(), Helper(), Actor(100, 0, center)], full: true),
                Frame(1190, .1, [Boss(Cast(37317, 8, center, .2f))]),
                Frame(1190, .5, []), Frame(1190, 1, [Boss()]),
            ];
            if (restart)
            {
                records.Add(Frame(1190, 2, [Boss(Cast(37321, 8, center + Vector3.UnitX * 3, .4f, startedAt: 2))]));
                records.Add(Frame(1190, 3, [Boss()]));
            }
            var count = restart ? 4 : 7;
            for (var i = 0; i < count; ++i)
            {
                var at = 4 + i * 3;
                records.Add(Frame(1190, at, [Helper(cast: Cast(39817, 1, center + Vector3.UnitX * i, startedAt: at))]));
                records.Add(Native(at + .5, 39817, (uint)(i + 1)));
                records.Add(Frame(1190, at + 1, [Helper()]));
            }
            records.Add(Frame(1190, 30, []));
            load(guid, records);
            controller.Seek(.6);
            Check(Aoes().Length == 3 && dangers() == 3, "Dustdevil future/current/circle must all be risky.");
            controller.Seek(4.6);
            Check(Aoes().Length == 3 && dangers() == 3, "Dustdevil Native effect consumed Finish-only rotation.");
            controller.Seek(5.2);
            Check(Aoes().Length == 3 && dangers() == 3, "First true Finish lost next rotation.");
            controller.Seek(4 + (count - 1) * 3 + 1.2);
            Check(Aoes().Length == 0 && dangers() == 0, "Final Dustdevil Finish retained cone or circle.");
            controller.Seek(.6);
            manager.SetMechanismEnabled(guid, "Ttokrrone", false);
            controller.Seek(.7);
            Check(previews().Length == 0 && dangers() == 0, "Disabled Ttokrrone retained owner output.");
            manager.SetMechanismEnabled(guid, "Ttokrrone", true);
            controller.Seek(.6);
            Isolated();
            stop();
        }
        Console.WriteLine("PASS native fate Ttokrrone production seven and restart four rotating true Finishes native ignores owner disable reverse Seek and replay execution isolation");
    }
}
