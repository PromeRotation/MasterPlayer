using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static readonly DateTime Start = new(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Assembly assembly, dynamic manager, dynamic runtime, dynamic controller, string path)
    {
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = plugin.GetType().GetProperty("AoeVisibility", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(plugin)!;
        object[] Previews() => ((IEnumerable)visibility.GetType().GetMethod("GetPreviews")!
            .Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().ToArray();
        int Dangers() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Count();
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        void Load(string id, IEnumerable<string> lines)
        {
            Check((int)controller.RouterErrorCount == 0, $"Replay callback error before replacing session: {controller.StatusMessage}");
            File.WriteAllLines(path, lines);
            manager.SetScriptEnabled(id, true);
            Check(controller.Load(path), (string)controller.StatusMessage);
            controller.Play();
            controller.Pause();
        }
        void Stop()
        {
            Check((int)controller.RouterErrorCount == 0, $"Replay callback error before stop: {controller.StatusMessage}");
            controller.StopSession();
            Check(Dangers() == 0 && Goals() == 0 && Previews().Length == 0, "Production dungeon stop leaked output.");
        }

        if (Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--native-replay=")) is { } replayFilter)
        {
            var filters = replayFilter["--native-replay=".Length..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var runners = typeof(NativeDungeonReplayRegression).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Where(m => m.Name.StartsWith("Run") && m.GetParameters() is { Length: 7 } parameters
                    && parameters[0].ParameterType == typeof(object)
                    && parameters[3].ParameterType.IsAssignableFrom(typeof(Action<string, IEnumerable<string>>)))
                .ToArray();
            foreach (var filter in filters)
                Check(runners.Any(m => m.Name[3..].Contains(filter, StringComparison.OrdinalIgnoreCase)), $"Unknown native replay filter: {filter}");
            var selected = runners.Where(m => filters.Any(f => m.Name[3..].Contains(f, StringComparison.OrdinalIgnoreCase))).ToArray();
            Check(selected.Length > 0, "Native replay filter selected no scenarios.");
            foreach (var runner in selected)
                runner.Invoke(null, [(object)manager, (object)runtime, (object)controller, (Action<string, IEnumerable<string>>)Load,
                    (Action)Stop, (Func<object[]>)Previews, (Func<int>)Dangers]);
            return;
        }

        var controllerType = assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        var bridge = RuntimeHelpers.GetUninitializedObject(controllerType);
        bool Suppressed() => (bool)controllerType.GetMethod("IsReplaySuppressed", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(bridge, null)!;
        var hallway = new Vector3(30, 0, 130);
        Load("masterplayer.dungeon.halatali",
        [
            Header(1245),
            Frame(1245, 0, [Actor(1, 0x4643, hallway), Actor(100, 0, hallway)], full: true),
            Frame(1245, 1, [Actor(1, 0x4643, hallway, combat: true)]),
        ]);
        controller.Seek(.01);
        Check(!Suppressed(), "An inactive native dungeon script suppressed hallway/zone mechanisms.");
        controller.Seek(1.01);
        Check(Suppressed(), "An active native encounter failed to suppress duplicate bridge output.");
        Stop();

        var center = new Vector3(30, 0, 130);
        var records = new List<string> { Header(1245), Frame(1245, 0, [
            Actor(1, 0x4643, center, combat: true), Actor(100, 0, center),
            .. Enumerable.Range(10, 8).Select(i => Actor((ulong)i, 0x233C, center))], full: true) };
        records.Add(Frame(1245, .1, Enumerable.Range(10, 8).Select(i =>
            Actor((ulong)i, 0x233C, center, cast: Cast(i < 14 ? 40589u : 40588u, i < 14 ? 9 : 6, center, i * MathF.PI / 4)))));
        foreach (var i in Enumerable.Range(10, 8))
            records.Add(CastEvent(.1, (ulong)i, i < 14 ? 40589u : 40588u, i < 14 ? 9 : 6, center, i * MathF.PI / 4));
        records.Add(Frame(1245, 6.1, Enumerable.Range(14, 4).Select(i => Actor((ulong)i, 0x233C, center))));
        records.Add(Frame(1245, 9.1, Enumerable.Range(10, 4).Select(i => Actor((ulong)i, 0x233C, center))));
        Load("masterplayer.dungeon.halatali", records);
        controller.Seek(1d);
        Check(Dangers() == 4 && Previews().Length == 4, "Production Fireflow first wave output differs.");
        controller.Seek(6.11);
        Check(Dangers() == 4 && Previews().Length == 4, "Production Fireflow second wave was lost.");
        controller.Seek(9.11);
        Check(Dangers() == 0 && Previews().Length == 0, "Production Fireflow finish leaked.");
        controller.Seek(1d);
        Check(Dangers() == 4, "Production Fireflow seek did not reconstruct the first wave.");
        Stop();

        center = new(-180, 0, -135);
        Load("masterplayer.dungeon.halatali",
        [
            Header(1245),
            Frame(1245, 0, [Actor(1, 0x4644, center, combat: true), Actor(100, 0, center), Actor(10, 0x233C, center)], full: true),
            Frame(1245, .1, [Actor(10, 0x233C, center, cast: Cast(41113, 10, center))]),
            CastEvent(.1, 10, 41113, 10, center),
            Frame(1245, 10.1, [Actor(10, 0x233C, center)]),
        ]);
        controller.Seek(1d);
        Check(Dangers() == 1 && Previews().Length == 1, "Production Shock output missing.");
        dynamic shock = Previews()[0].GetType().GetProperty("Geometry")!.GetValue(Previews()[0])!;
        Check(!(bool)shock.Contains(new Vector3(-183, 0, -108), 0f)
            && !(bool)shock.Contains(new Vector3(-174, 0, -160), 0f)
            && (bool)shock.Contains(center, 0f), "Production Shock filled a safe shore.");
        controller.Seek(10.11);
        Check(Dangers() == 0 && Previews().Length == 0, "Production Shock retained finished water.");
        Stop();

        center = new(-258, 0, 17);
        Load("masterplayer.dungeon.halatali",
        [
            Header(1245),
            Frame(1245, 0, [Actor(1, 0x4645, center, combat: true, shield: true), Actor(100, 0, center),
                Actor(10, 0x233C, center), Actor(11, 0x233C, center)], full: true),
            Frame(1245, .1, [Actor(1, 0x4645, center, combat: true, cast: Cast(40599, 5, center), shield: true),
                Actor(10, 0x233C, center, cast: Cast(40600, 8, center)), Actor(11, 0x233C, center, cast: Cast(40601, 11, center))]),
            CastEvent(.1, 1, 40599, 5, center), CastEvent(.1, 10, 40600, 8, center), CastEvent(.1, 11, 40601, 11, center),
            Frame(1245, 5.1, [Actor(1, 0x4645, center, combat: true)]),
            Frame(1245, 8.1, [Actor(10, 0x233C, center)]),
            Frame(1245, 11.1, [Actor(11, 0x233C, center)]),
        ]);
        foreach (var (at, shape) in new[] { (1d, "CircleGeometry"), (5.11, "DonutGeometry"), (8.11, "DonutGeometry") })
        {
            controller.Seek(at);
            Check(Dangers() == 1 && Previews().Length == 1, "Production Tangata should publish only the current stage.");
            var geometry = Previews()[0].GetType().GetProperty("Geometry")!.GetValue(Previews()[0])!;
            Check(geometry.GetType().Name == shape, "Production Tangata stage geometry differs.");
        }
        controller.Seek(1d);
        var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
        var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
        var encounter = script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        var forbidden = (ulong[])encounter.GetType().GetProperty("ForbiddenTargets")!.GetValue(encounter)!;
        Check(forbidden.SequenceEqual([1UL]), "Production Tangata did not sample its shield.");
        var hints = script.GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
            "Offline replay published a PR action/facing request.");
        controller.Seek(5.11);
        // Seek rebuilds script instances; reacquire its encounter before checking subsequent state.
        running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
        script = running.GetType().GetProperty("Script")!.GetValue(running)!;
        encounter = script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        Check(((ulong[])encounter.GetType().GetProperty("ForbiddenTargets")!.GetValue(encounter)!).Length == 0,
            "Production shield removal retained target ban.");
        controller.Seek(11.11);
        Check(Dangers() == 0 && Previews().Length == 0, "Production Tangata final stage leaked.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Halatali three bosses, helper waves, three concentric stages, water shores, shield, seek, replay isolation and stop cleanup");

        center = new(215, 0, -145);
        Load("masterplayer.dungeon.totorak",
        [
            Header(1039),
            Frame(1039, 0, [Actor(1, 0x103, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x1E8614, center + Vector3.UnitX * 5)], full: true),
            Frame(1039, 2, [], removed: [10]),
        ]);
        controller.Seek(1d);
        Check(Dangers() == 1 && Previews().Length == 1, "Production Totorak pollen missing.");
        controller.Seek(2.1);
        Check(Dangers() == 0 && Previews().Length == 0, "Production Totorak pollen removal leaked.");
        Stop();
        center = Vector3.Zero;
        Load("masterplayer.dungeon.haukke",
        [
            Header(1040),
            Frame(1040, 0, [Actor(1, 0x38A8, center, combat: true), Actor(100, 0, center), Actor(10, 0x38A9, new(0, 0, 5))], full: true),
            Frame(1040, .1, [Actor(10, 0x38A9, new(0, 0, 5), cast: Cast(28648, 5, new(0, 0, 5)))]),
            CastEvent(.1, 10, 28648, 5, new(0, 0, 5)),
            Frame(1040, 5.1, [Actor(10, 0x38A9, new(0, 0, 5))]),
        ]);
        controller.Seek(4.7);
        Check(Dangers() == 0 && Previews().Length == 1, "Production Haukke gaze became a damage zone or lost its display.");
        controller.Seek(5.2);
        Check(Previews().Length == 0, "Production gaze finish leaked.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Totorak persistent pollen and Haukke gaze, independent script discovery and cleanup");

        center = new(-26, 0, -236);
        records =
        [
            Header(1041),
            Frame(1041, 0, [Actor(1, 0x38C5, center, combat: true), Actor(100, 0, center),
                .. Enumerable.Range(10, 5).Select(i => Actor((ulong)i, 0x233C, center))], full: true),
            Frame(1041, .1, Enumerable.Range(10, 5).Select(i => Actor((ulong)i, 0x233C, center, cast: Cast(28658, 9, center)))),
            .. Enumerable.Range(10, 5).Select(i => CastEvent(.1, (ulong)i, 28658, 9, center)),
            Frame(1041, 9.1, Enumerable.Range(10, 5).Select(i => Actor((ulong)i, 0x233C, center))),
        ];
        Load("masterplayer.dungeon.brayflox", records);
        controller.Seek(1d);
        Check(Dangers() == 4 && Previews().Length == 4, "Production Brayflox Burst cap differs.");
        controller.Seek(9.2);
        Check(Dangers() == 0 && Previews().Length == 0, "Production Brayflox Burst finish leaked.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Brayflox helper cap, cast finish and stop cleanup");

        center = new(-75, 0, -59);
        Load("masterplayer.dungeon.qarn",
        [
            Header(1267),
            Frame(1267, 0, [Actor(1, 0x477A, center, combat: true),
                Actor(100, 0, center, doom: true), Actor(10, 0x1E87E2, center + Vector3.UnitX * 5, renderFlags: 0)], full: true),
            Frame(1267, 2, [Actor(10, 0x1E87E2, center + Vector3.UnitX * 5, renderFlags: 1),
                Actor(11, 0x1E87E3, center - Vector3.UnitX * 5, renderFlags: 0)]),
            Frame(1267, 3, [Actor(100, 0, center)]),
        ]);
        controller.Seek(1d);
        Check(Goals() == 1 && Dangers() == 0 && Previews().Length == 1, "Production Qarn Doom did not publish a safe goal.");
        controller.Seek(2.1);
        dynamic cleansing = Previews().Single().GetType().GetProperty("Geometry")!.GetValue(Previews().Single())!;
        Check((bool)cleansing.Contains(center - Vector3.UnitX * 5, 0f), "Production Qarn glow transition retained old platform.");
        controller.Seek(3.1);
        Check(Goals() == 0 && Previews().Length == 0, "Production Qarn expired Doom retained output.");
        controller.Seek(1d);
        Check(Goals() == 1, "Backward seek lost Doom/glow state.");
        Stop();

        center = new(236, 0, 0);
        Load("masterplayer.dungeon.qarn",
        [
            Header(1267),
            Frame(1267, 0, [Actor(1, 0x477E, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x477F, center), Actor(11, 0x4780, center)], full: true),
            Frame(1267, .1, [Actor(10, 0x477F, center, cast: Cast(42244, 2.2, center)),
                Actor(11, 0x4780, center, cast: Cast(42241, 20, center, MathF.PI / 2))]),
            CastEvent(.1, 10, 42244, 2.2, center), CastEvent(.1, 11, 42241, 20, center, MathF.PI / 2),
            Frame(1267, 2.3, [Actor(10, 0x477F, center)]),
            Frame(1267, 20.1, [Actor(11, 0x4780, center)]),
        ]);
        controller.Seek(1d);
        Check(Dangers() == 2 && Previews().Length == 2, "Production Qarn rays failed to coexist.");
        controller.Seek(3d);
        Check(Dangers() == 1 && Previews().Length == 1, "Production Qarn 20 second ray vanished with short ray.");
        controller.Seek(20.2);
        Check(Dangers() == 0 && Previews().Length == 0, "Production Qarn final ray leaked.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Qarn Doom, glow transitions, safe movement goal, independent Verge rays, seek and cleanup");

        center = new(-142.695f, 0, 154.942f);
        var pillar = new Vector3(-142.695f, 0, 144.942f);
        Load("masterplayer.dungeon.cutters-cry",
        [
            Header(1303),
            Frame(1303, 0, [Actor(1, 0x48FF, center, combat: true), Actor(100, 0, center), Actor(10, 0x233C, pillar)], full: true),
            EffectEvent(.1, 10, 44232, 1), EffectEvent(1, 10, 44232, 2),
            EffectEvent(1.9, 10, 44232, 3), EffectEvent(2.8, 10, 44232, 4),
            EffectEvent(3.7, 10, 44232, 5), EffectEvent(4.6, 10, 44232, 6),
            EffectEvent(5.5, 10, 44232, 7), EffectEvent(6.4, 1, 44233, 8),
        ]);
        controller.Seek(.2);
        Check(Dangers() == 7 && Previews().Length == 7, "Production Worm event did not create seven future hits.");
        controller.Seek(2d);
        Check(Dangers() == 5 && Previews().Length == 5, "Production Worm events advanced the wrong number of stages.");
        controller.Seek(6.5);
        Check(Dangers() == 0 && Previews().Length == 0, "Production Worm final blast leaked.");
        controller.Seek(.2);
        Check(Dangers() == 7, "Backward seek did not rebuild no-cast sand sequence.");
        Stop();
        center = new(-180, 0, -210);
        Load("masterplayer.dungeon.cutters-cry",
        [
            Header(1303),
            Frame(1303, 0, [Actor(1, 0x4900, center, combat: true), Actor(100, 0, center), Actor(10, 0x4901, center)], full: true),
            JsonSerializer.Serialize(new { t = .1, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = 100, TargetId = 100, MarkerId = 23 } }),
            Frame(1303, 2, [Actor(10, 0x4901, center + Vector3.UnitX * 5)]),
            EffectEvent(10.5, 10, 44240, 1),
        ]);
        controller.Seek(1d);
        Check(Dangers() == 2 && Previews().Length == 1, "Production Chimera orb did not separate target kite restriction from display.");
        controller.Seek(2.1);
        dynamic orb = Previews().Single().GetType().GetProperty("Geometry")!.GetValue(Previews().Single())!;
        Check((bool)orb.Contains(center + Vector3.UnitX * 5, 0f), "Production orb did not follow actor position.");
        controller.Seek(10.6);
        Check(Dangers() == 0 && Previews().Length == 0, "Production orb detonation retained output.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Cutters Cry seven no-cast stages, final Earthbreak, moving targeted orb and seek");

        center = new(44, 0, -80);
        Load("masterplayer.dungeon.stone-vigil",
        [
            Header(1042),
            Frame(1042, 0, [Actor(1, 0x38C7, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x38C8, new(57, 0, -80)), Actor(11, 0x38D0, new(54, 0, -80))], full: true),
            EffectEvent(.1, 11, 28731, 1),
            Frame(1042, 2, [], removed: [10]),
        ]);
        controller.Seek(.05);
        Check(Dangers() == 4 && Previews().Length == 4, "Production Typhoon initial four-stage forecast differs.");
        controller.Seek(.2);
        Check(Dangers() == 10 && Previews().Length == 10, "Production Typhoon did not expand to ten forecasts.");
        controller.Seek(2.1);
        Check(Dangers() == 0 && Previews().Length == 0, "Production removed Typhoon visual retained forecasts.");
        Stop();
        center = new(0, 0, -248);
        Load("masterplayer.dungeon.stone-vigil",
        [
            Header(1042),
            Frame(1042, 0, [Actor(1, 0x5AF, center, combat: true), Actor(100, 0, center)], full: true),
            JsonSerializer.Serialize(new { t = .1, kind = "PlayActionTimeline",
                type = "PromeRotation.LogSystem.LogSystemPlayActionTimelineEvent",
                data = new { SourceId = 1, TimelineId = 0x8E } }),
            EffectEvent(7.9, 1, 1027, 1),
        ]);
        controller.Seek(1d);
        Check(Dangers() == 1 && Previews().Length == 1, "Production Touchdown timeline did not create forecast.");
        controller.Seek(8d);
        Check(Dangers() == 0 && Previews().Length == 0, "Production Touchdown effect retained forecast.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Stone Vigil four-to-ten Typhoon stages, removed visuals and timeline Touchdown");

        center = new(80, 0, -175);
        Load("masterplayer.dungeon.dzemael",
        [
            Header(1330),
            Frame(1330, 0, [Actor(1, 0x4A8D, center, combat: true, invincible: true),
                Actor(100, 0, center), Actor(10, 0x4A8E, center + Vector3.UnitX * 5)], full: true),
            Frame(1330, 2, [Actor(1, 0x4A8D, center, combat: true)]),
        ]);
        controller.Seek(1d);
        var dungeon = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
        var instance = dungeon.GetType().GetProperty("Script")!.GetValue(dungeon)!;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var current = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return current.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(current)
                ?? throw new InvalidOperationException("Current dungeon script has no encounter after replay update.");
        }
        Check(((ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!).SequenceEqual([1UL]),
            "Production Batraal invincibility did not restrict boss.");
        Check((ulong)Encounter().GetType().GetMethod("PriorityTarget")!.Invoke(Encounter(), [1UL, center])! == 10,
            "Production Batraal did not prioritize corrupted crystal.");
        controller.Seek(2.1);
        dungeon = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
        instance = dungeon.GetType().GetProperty("Script")!.GetValue(dungeon)!;
        Check(((ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!).Length == 0,
            "Production Batraal status loss retained ban.");
        Stop();
        center = new(48, 0, 78);
        Load("masterplayer.dungeon.dzemael",
        [
            Header(1330),
            Frame(1330, 0, [Actor(1, 0x4A8A, center, combat: true), Actor(100, 0, center),
                .. Enumerable.Range(10, 8).Select(i => Actor((ulong)i, 0x233C, center))], full: true),
            Frame(1330, .1, Enumerable.Range(10, 8).Select(i => Actor((ulong)i, 0x233C, center, cast: Cast(45568, 8, center)))),
            .. Enumerable.Range(10, 8).Select(i => CastEvent(.1, (ulong)i, 45568, 8, center)),
            Frame(1330, 8.1, Enumerable.Range(10, 8).Select(i => Actor((ulong)i, 0x233C, center))),
        ]);
        controller.Seek(1d);
        Check(Dangers() == 6 && Previews().Length == 6, "Production VoidMatter cap differs.");
        controller.Seek(8.2);
        Check(Dangers() == 0 && Previews().Length == 0, "Production VoidMatter finish retained output.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Dzemael invincibility ban, crystal priority and six-helper VoidMatter cap");

        foreach (var (boss, status, fruit, location) in new[]
        {
            (0x4B0Cu, 302u, 0x1E8ED9u, new Vector3(34, 0, 8)),
            (0x4B0Eu, 303u, 0x1E8EE3u, new Vector3(-400, 0, -125)),
        })
        {
            Load("masterplayer.dungeon.aurum-vale",
            [
                Header(1331),
                Frame(1331, 0, [Actor(1, boss, location, combat: true), Actor(100, 0, location, stackedStatus: status),
                    Actor(10, fruit, location)], full: true),
                Frame(1331, 2, [Actor(100, 0, location)]),
            ]);
            controller.Seek(1d);
            Check(Goals() == 1 && Previews().Length == 1 && Dangers() == 0, "Production Aurum three stacks lost fruit guidance.");
            dungeon = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            instance = dungeon.GetType().GetProperty("Script")!.GetValue(dungeon)!;
            var interaction = instance.GetType().BaseType!.GetField("fruitInteraction", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
            Check(!(bool)interaction.GetType().GetProperty("Holding", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(interaction)!
                && !(bool)interaction.GetType().GetProperty("Attempted", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(interaction)!,
                "Offline Aurum replay attempted live fruit interaction.");
            controller.Seek(2.1);
            Check(Goals() == 0 && Previews().Length == 0, "Production Aurum cleanse retained guidance.");
            controller.Seek(1d);
            Check(Goals() == 1, "Production Aurum backward seek lost stack parameters.");
            Stop();
        }
        center = new(-70, 0, -50);
        Load("masterplayer.dungeon.aurum-vale",
        [
            Header(1331),
            Frame(1331, 0, [Actor(1, 0x4B0D, center, combat: true), Actor(100, 0, center)], full: true),
            Frame(1331, .1, [Actor(1, 0x4B0D, center, combat: true, cast: Cast(45747, 2.5, center, MathF.PI / 2))]),
            CastEvent(.1, 1, 45747, 2.5, center, MathF.PI / 2),
            Frame(1331, 2.6, [Actor(1, 0x4B0D, center, combat: true)]),
        ]);
        controller.Seek(1d);
        dynamic sector = Previews().Single().GetType().GetProperty("Geometry")!.GetValue(Previews().Single())!;
        Check(Dangers() == 1 && (bool)sector.Contains(center + Vector3.UnitX * 12, 0f)
            && !(bool)sector.Contains(center - Vector3.UnitX * 12, 0f)
            && !(bool)sector.Contains(center + Vector3.UnitX * 7, 0f), "Production Aurum donut sector rotation/hole differs.");
        controller.Seek(2.7);
        Check(Dangers() == 0 && Previews().Length == 0, "Production Aurum sector finish leaked.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Aurum fruit stack parameters, movement goals, cleanse, seek, interaction isolation and locked donut sector");

        center = new(-98, 0, -33);
        Load("masterplayer.dungeon.castrum-meridianum",
        [
            Header(1043),
            Frame(1043, 0, [Actor(1, 0x38CE, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x233C, center), Actor(11, 0x233C, center)], full: true),
            Frame(1043, .1, [Actor(10, 0x233C, center, cast: Cast(29163, 5, center)), Actor(11, 0x233C, center, cast: Cast(28787, 7, center))]),
            CastEvent(.1, 10, 29163, 5, center), CastEvent(.1, 11, 28787, 7, center),
            Frame(1043, 5.1, [Actor(10, 0x233C, center)]),
            Frame(1043, 7.1, [Actor(11, 0x233C, center)]),
        ]);
        controller.Seek(1d);
        Check(Dangers() == 1 && Previews().Length == 1, "Production Livia first stage failed to hide side circles.");
        controller.Seek(5.2);
        dynamic sides = Previews().Single().GetType().GetProperty("Geometry")!.GetValue(Previews().Single())!;
        Check(Dangers() == 1 && (bool)sides.Contains(center + Vector3.UnitX * 7.99f, 0f)
            && !(bool)sides.Contains(center + Vector3.UnitX * 8.01f, 0f), "Production Livia side-stage promotion failed.");
        controller.Seek(7.2); Check(Dangers() == 0 && Previews().Length == 0, "Production Livia sequence leaked.");
        controller.Seek(1d); Check(Dangers() == 1, "Production Livia seek lost initial stage.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Castrum Livia mutually staged circles, original finish times and backward seek");

        center = new(-164, 0, 0);
        Load("masterplayer.dungeon.praetorium",
        [
            Header(1044),
            Frame(1044, 0, [Actor(1, 0x3873, center, combat: true), Actor(100, 0, center)], full: true),
            JsonSerializer.Serialize(new { t = 1, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
                data = new { Position = 0, Param1 = 1, Param2 = 2 } }),
        ]);
        controller.Seek(.5);
        Check(Dangers() == 1 && Previews().Length == 1, "Production Nero initial ring missing.");
        controller.Seek(1.1);
        Check(Dangers() == 0 && Previews().Length == 0, "Production Nero map effect did not remove ring.");
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!(bool)boundary.Contains(center + Vector3.UnitX * 24, 0f), "Production Nero boundary did not shrink.");
        controller.Seek(.5); Check(Dangers() == 1, "Nero seek failed to restore pre-map ring.");
        Stop();
        center = new(-562, 0, 220);
        Load("masterplayer.dungeon.praetorium",
        [
            Header(1044),
            Frame(1044, 0, [Actor(1, 0x3875, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x3878, center, rotation: MathF.PI / 2)], full: true),
            EffectEvent(6, 10, 28487, 1),
        ]);
        controller.Seek(.5);
        Check(Dangers() == 1 && Previews().Length == 1, "Production Gaius spawn forecast missing.");
        dynamic ray = Previews().Single().GetType().GetProperty("Geometry")!.GetValue(Previews().Single())!;
        Check((bool)ray.Contains(center + Vector3.UnitX * 30, 0f) && !(bool)ray.Contains(center + Vector3.UnitZ * 30, 0f),
            "Production Gaius replay rotation lost.");
        controller.Seek(6.1); Check(Dangers() == 0 && Previews().Length == 0, "Production Gaius effect retained spawn ray.");
        controller.Seek(.5); Check(Dangers() == 1, "Production Gaius seek did not rebuild spawn forecast.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Praetorium Nero map-driven boundary, Gaius spawn rotation, effect cleanup and seek");

        center = new(125, 0, 108);
        Load("masterplayer.dungeon.wanderers-palace",
        [
            Header(159),
            Frame(159, 0, [Actor(1, 0x41C, center, combat: true), Actor(100, 0, center)], full: true),
            Frame(159, .1, [Actor(1, 0x41C, center, combat: true, cast: Cast(950, 2.5, center, MathF.PI / 2))]),
            CastEvent(.1, 1, 950, 2.5, center, MathF.PI / 2),
            Frame(159, 2.6, [Actor(1, 0x41C, center, combat: true)]),
            EffectEvent(3.7, 1, 942, 1),
        ]);
        controller.Seek(1d); Check(Dangers() == 2 && Previews().Length == 2, "Production Keeper follow-up was not forecast with Inhale.");
        controller.Seek(2.7); Check(Dangers() == 1 && Previews().Length == 1, "Production Keeper natural cast end dropped follow-up.");
        controller.Seek(3.8); Check(Dangers() == 0 && Previews().Length == 0, "Production Keeper effect retained follow-up.");
        controller.Seek(1d); Check(Dangers() == 2, "Production Keeper seek lost initial chain.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Wanderer simultaneous Inhale and delayed Grief, natural finish, effect and seek");

        center = Vector3.Zero;
        Load("masterplayer.dungeon.amdapor-keep",
        [
            Header(167),
            Frame(167, 0, [Actor(1, 0x5CC, center, combat: true), Actor(100, 0, new(5, 0, 4)),
                Actor(10, 0x1E86A7, new(10, 0, 0), hitboxRadius: 2),
                Actor(11, 0x1E86A8, new(-10, 0, 0), hitboxRadius: 2, eventState: 1)], full: true),
            Frame(167, .1, [Actor(1, 0x5CC, center, combat: true, cast: Cast(1074, 7, center))]),
            CastEvent(.1, 1, 1074, 7, center),
            Frame(167, 7.1, [Actor(1, 0x5CC, center, combat: true)]),
        ]);
        controller.Seek(1d);
        var safePreview = Previews().Single(p => (bool)p.GetType().GetProperty("Safe")!.GetValue(p)!
            && p.GetType().GetProperty("Geometry")!.GetValue(p)!.GetType().Name == "DungeonLosGeometry");
        dynamic shelter = safePreview.GetType().GetProperty("Geometry")!.GetValue(safePreview)!;
        Check(Dangers() == 1 && (bool)shelter.Contains(new Vector3(15, 0, 0), 0f)
            && !(bool)shelter.Contains(new Vector3(-15, 0, 0), 0f)
            && !(bool)shelter.Contains(new Vector3(11, 0, 0), 0f), "Production Amdapor lost intact-pillar state or obstacle exclusion.");
        controller.Seek(7.2); Check(Dangers() == 0, "Production Amdapor LoS completion leaked danger.");
        controller.Seek(1d); Check(Dangers() == 1, "Production Amdapor seek lost LoS snapshot.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Amdapor intact pillar state, shelter complement, cast finish, seek and full cleanup");

        center = new(42, 0, -56);
        Load("masterplayer.dungeon.pharos-sirius",
        [
            Header(160),
            Frame(160, 0, [Actor(1, 0x8FB, center, combat: true), Actor(100, 0, center, stackedStatus: 374),
                Actor(10, 0x1E9058, center + Vector3.UnitX * 10)], full: true),
            Frame(160, 1, [Actor(10, 0x1E9059, center + Vector3.UnitX * 10)]),
            EffectEvent(2.5, 1, 1500, 1),
            Frame(160, 3, [Actor(10, 0x1E905A, center + Vector3.UnitX * 10)]),
            Frame(160, 4, [], removed: [10]),
        ]);
        controller.Seek(.5);
        Check(Previews().Length == 2 && Dangers() == 1, "Production Pharos lost three-stack burst or initial crystal.");
        controller.Seek(1.1);
        Check(Previews().Any(p => p.GetType().GetProperty("Geometry")!.GetValue(p)!.GetType().GetProperty("Radius")?.GetValue(
            p.GetType().GetProperty("Geometry")!.GetValue(p)!) is float radius && radius == 12), "Production Pharos crystal failed to grow.");
        controller.Seek(3.1);
        Check(Previews().Length == 1 && Dangers() == 1, "Production Pharos stale stack snapshot resurrected resolved burst.");
        dynamic crystal = Previews().Single().GetType().GetProperty("Geometry")!.GetValue(Previews().Single())!;
        Check((float)crystal.Radius == 15, "Production Pharos third crystal stage differs.");
        controller.Seek(4.1); Check(Dangers() == 0 && Previews().Length == 0, "Production Pharos removed crystal leaked.");
        controller.Seek(.5); Check(Previews().Length == 2, "Production Pharos backward seek lost stack parameters.");
        Stop();
        center = Vector3.Zero;
        Load("masterplayer.dungeon.pharos-sirius",
        [
            Header(160),
            Frame(160, 0, [Actor(1, 0x8F4, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x8F7, center + Vector3.UnitX * 15), Actor(11, 0x8F8, center - Vector3.UnitX * 15),
                Actor(12, 0x8F5, center)], full: true),
            Frame(160, 2, [], removed: [10, 11]),
        ]);
        controller.Seek(1d);
        instance = ((IEnumerable)manager.Running.Values).Cast<object>().Single().GetType().GetProperty("Script")!.GetValue(
            ((IEnumerable)manager.Running.Values).Cast<object>().Single())!;
        Check(((ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!).Order().SequenceEqual([10UL, 11UL])
            && Dangers() == 2 && Previews().Length == 2, "Production Pharos egg restrictions differ.");
        hints = instance.GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
        facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
            "Offline egg ban published a live PR request.");
        controller.Seek(2.1); Check(Dangers() == 0 && Previews().Length == 0, "Production Pharos removed eggs leaked.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Pharos crystal growth, three-stack effect latch, egg ban, seek, execution isolation and cleanup");

        center = new(-30, 0, -170);
        Load("masterplayer.dungeon.snowcloak",
        [
            Header(1062),
            Frame(1062, 0, [Actor(1, 0xD1E, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x1E94F1, center, eventAnimation: 0x00040008),
                Actor(11, 0x1E96D4, center, eventAnimation: 0x00040008)], full: true),
            Frame(1062, 1, [Actor(11, 0x1E96D4, center, eventAnimation: 0x00010002)]),
        ]);
        controller.Seek(.5);
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(Dangers() == 1 && Previews().Length == 1 && (bool)boundary.Contains(new Vector3(-50, 0, -110), 0f),
            "Production Snowcloak lost environmental animation or expanded terrain.");
        controller.Seek(2d);
        var windAreas = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().ToArray();
        dynamic wind = windAreas.Single();
        Check(((IEnumerable)wind.DangerWindows).Cast<dynamic>().Single().DangerFrom == Start.AddSeconds(6),
            "Production Snowcloak wind timer restarted after snapshot.");
        Stop();
        center = new(56, 0, -88);
        Load("masterplayer.dungeon.snowcloak",
        [
            Header(1062),
            Frame(1062, 0, [Actor(1, 0xD07, center, combat: true), Actor(100, 0, center)], full: true),
            Frame(1062, .1, [Actor(1, 0xD07, center, combat: true, cast: Cast(3080, 5, center))]),
            CastEvent(.1, 1, 3080, 5, center),
            Frame(1062, 5.1, [Actor(1, 0xD07, center, combat: true)]),
            EffectEvent(7.4, 20, 3079, 1),
        ]);
        controller.Seek(6.7);
        Check(Dangers() == 1 && Previews().Length == 0, "Production Snow Drift lost native continuous-move navigation.");
        controller.Seek(7.5); Check(Dangers() == 0, "Production Snow Drift effect retained move requirement.");
        controller.Seek(6.7); Check(Dangers() == 1, "Production Snow Drift seek lost delayed movement.");
        Stop();
        center = new(0, 0, 65);
        Load("masterplayer.dungeon.snowcloak",
        [
            Header(1062),
            Frame(1062, 0, [Actor(1, 0x3979, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x397A, center + Vector3.UnitX * 10, hitboxRadius: 2.5f),
                Actor(11, 0x397A, center - Vector3.UnitX * 10, hitboxRadius: 2.5f, animationState1: 1)], full: true),
            Frame(1062, .1, [Actor(1, 0x3979, center, combat: true, cast: Cast(29599, 8, center))]),
            CastEvent(.1, 1, 29599, 8, center),
            Frame(1062, 8.1, [Actor(1, 0x3979, center, combat: true)]),
            EffectEvent(12.6, 10, 29601, 1),
        ]);
        controller.Seek(1d);
        shelter = Previews().Select(p => p.GetType().GetProperty("Geometry")!.GetValue(p)!)
            .Single(g => g.GetType().Name == "DungeonLosGeometry");
        Check(Dangers() == 1 && (bool)shelter.Contains(center + Vector3.UnitX * 15, 0f)
            && !(bool)shelter.Contains(center - Vector3.UnitX * 15, 0f), "Production Fenrir lost animation-state blocker filter.");
        controller.Seek(8.2);
        Check(Dangers() == 1 && Previews().Any(p => p.GetType().GetProperty("Geometry")!.GetValue(p)!.GetType()
            .GetProperty("Radius")?.GetValue(p.GetType().GetProperty("Geometry")!.GetValue(p)!) is float radius && radius == 8),
            "Production Fenrir natural cast end dropped delayed shatter.");
        controller.Seek(12.7); Check(Dangers() == 0, "Production Fenrir shatter effect leaked.");
        controller.Seek(1d); Check(Dangers() == 1, "Production Fenrir seek failed to rebuild LoS.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Snowcloak wind/wall snapshots, continuous movement, intact ice LoS, delayed shatter, effects and seek");

        center = new(-103, 0, 136);
        Load("masterplayer.dungeon.sastasha-hard",
        [
            Header(387),
            Frame(387, 0, [Actor(1, 0xCBB, center, combat: true), Actor(100, 0, center),
                Actor(10, 0xCBD, center)], full: true),
            Frame(387, .1, [Actor(10, 0xCBD, center, cast: Cast(1299, 2.5, center, MathF.PI / 2))]),
            CastEvent(.1, 10, 1299, 2.5, center, MathF.PI / 2),
            Frame(387, 2.6, [Actor(10, 0xCBD, center)]),
        ]);
        controller.Seek(1d);
        ray = Previews().Single().GetType().GetProperty("Geometry")!.GetValue(Previews().Single())!;
        Check(Dangers() == 1 && (bool)ray.Contains(center + Vector3.UnitX * 17.99f, 0f)
            && !(bool)ray.Contains(center + Vector3.UnitX * 18.01f, 0f), "Production hard Sastasha used incorrect Rive range.");
        controller.Seek(2.7); Check(Dangers() == 0 && Previews().Length == 0, "Production Madison add cast leaked.");
        controller.Seek(1d); Check(Dangers() == 1, "Production Madison seek lost add cast.");
        Stop();
        center = new(166, 0, -85);
        Load("masterplayer.dungeon.sastasha-hard",
        [
            Header(387),
            Frame(387, 0, [Actor(1, 0xCBA, center, combat: true), Actor(100, 0, center, stackedStatus: 569, classJob: 24)],
                full: true, party: [new { Slot = 0, Role = "H1", EntityId = 100UL }]),
            JsonSerializer.Serialize(new { t = .1, kind = "StatusAdd", type = "PromeRotation.LogSystem.LogSystemStatusAddEvent",
                data = new { TargetId = 100, StatusId = 420, Timestamp = Start.AddSeconds(.1) } }),
            JsonSerializer.Serialize(new { t = 1d, kind = "StatusRemove", type = "PromeRotation.LogSystem.LogSystemStatusRemoveEvent",
                data = new { TargetId = 100, StatusId = 420, Timestamp = Start.AddSeconds(1) } }),
        ]);
        controller.Seek(.5);
        Check(Previews().Length == 1 && Dangers() == 0, "Production Karlabos status event lost party prey marker.");
        instance = ((IEnumerable)manager.Running.Values).Cast<object>().Single().GetType().GetProperty("Script")!.GetValue(
            ((IEnumerable)manager.Running.Values).Cast<object>().Single())!;
        Check((DateTime)instance.GetType().BaseType!.GetField("nextSlimeCleanse", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)! == default,
            "Offline Karlabos replay attempted live dispel.");
        controller.Seek(1.1); Check(Dangers() == 0 && Previews().Length == 0, "Karlabos status removal leaked.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Sastasha Hard add rays, status events, dispel execution isolation, seek and cleanup");

        center = new(8, 0, -150);
        Load("masterplayer.dungeon.keeper-of-the-lake",
        [
            Header(1063),
            Frame(1063, 0, [Actor(1, 0xE68, center, combat: true), Actor(100, 0, center)], full: true),
            Frame(1063, .1, [Actor(1, 0xE68, center, combat: true, cast: Cast(3389, 3.5, center))]),
            CastEvent(.1, 1, 3389, 3.5, center),
            Frame(1063, 3.6, [Actor(1, 0xE68, center, combat: true)]),
            EffectEvent(3.6, 1, 3389, 1),
            EffectEvent(3.6, 1, 3389, 1),
            EffectEvent(4.1, 20, 3390, 2), EffectEvent(4.6, 20, 3390, 3), EffectEvent(5.1, 20, 3390, 4),
            EffectEvent(5.6, 20, 3390, 5), EffectEvent(6.1, 20, 3390, 6),
        ]);
        controller.Seek(3.7); Check(Dangers() == 1 && Previews().Length == 1, "Production Flamethrower vanished at first cast finish.");
        controller.Seek(5.7); Check(Dangers() == 1, "Production Flamethrower counted duplicate AOE target as another hit.");
        controller.Seek(6.2); Check(Dangers() == 0 && Previews().Length == 0, "Production sixth flame hit failed to clear.");
        controller.Seek(3.7); Check(Dangers() == 1, "Production flame seek failed.");
        Stop();
        center = new(-40.8f, 0, -78.2f);
        Load("masterplayer.dungeon.keeper-of-the-lake",
        [
            Header(1063),
            Frame(1063, 0, [Actor(1, 0x392A, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x233C, center), Actor(11, 0x233C, center), Actor(12, 0x233C, center)], full: true),
            Frame(1063, .1, [Actor(10, 0x233C, center, cast: Cast(29286, 4, center)),
                Actor(11, 0x233C, center, cast: Cast(29287, 6, center)), Actor(12, 0x233C, center, cast: Cast(29288, 8, center))]),
            CastEvent(.1, 10, 29286, 4, center), CastEvent(.1, 11, 29287, 6, center), CastEvent(.1, 12, 29288, 8, center),
            Frame(1063, 4.1, [Actor(10, 0x233C, center)]), Frame(1063, 6.1, [Actor(11, 0x233C, center)]),
            Frame(1063, 8.1, [Actor(12, 0x233C, center)]),
        ]);
        foreach (var (at, innerRadius, outerRadius) in new[] { (4.2, 6f, 12f), (6.2, 12f, 20f) })
        {
            controller.Seek(at);
            dynamic ring = Previews().Single().GetType().GetProperty("Geometry")!.GetValue(Previews().Single())!;
            Check(Dangers() == 1 && (float)ring.InnerRadius == innerRadius && (float)ring.OuterRadius == outerRadius,
                "Production Antipathy stage promotion changed.");
        }
        controller.Seek(8.2); Check(Dangers() == 0 && Previews().Length == 0, "Production Antipathy last stage leaked.");
        Stop();
        Load("masterplayer.dungeon.keeper-of-the-lake",
        [
            Header(1063),
            Frame(1063, 0, [Actor(1, 0x392A, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x392C, center, rotation: MathF.PI / 2)], full: true),
            JsonSerializer.Serialize(new { t = .1, kind = "PlayActionTimeline",
                type = "PromeRotation.LogSystem.LogSystemPlayActionTimelineEvent",
                data = new { SourceId = 10, TimelineId = 0x11D3, Timestamp = Start.AddSeconds(.1) } }),
            EffectEvent(12.1, 10, 29290, 1),
        ]);
        controller.Seek(1d); Check(Dangers() == 1 && Previews().Length == 1, "Production Mirage timeline failed to forecast.");
        controller.Seek(12.2); Check(Dangers() == 0 && Previews().Length == 0, "Production Mirage effect failed to clear.");
        controller.Seek(1d); Check(Dangers() == 1, "Production Mirage seek failed.");
        Stop();
        Load("masterplayer.dungeon.keeper-of-the-lake",
        [
            Header(1063),
            Frame(1063, 0, [Actor(1, 0x392A, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 5)], full: true),
            Frame(1063, .1, [Actor(1, 0x392A, center, combat: true, cast: Cast(29283, 5, center, target: 101))]),
            JsonSerializer.Serialize(new { t = .1, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0,
                data = new { SourceId = 1, ActionId = 29283, TargetId = 101, CastTime = 5, EffectPosition = Pose(center), Timestamp = Start.AddSeconds(.1) } }),
            Frame(1063, 5.1, [Actor(1, 0x392A, center, combat: true)]),
            EffectEvent(5.1, 1, 29283, 1), EffectEvent(5.1, 1, 29283, 1),
            EffectEvent(6.1, 1, 29284, 2), EffectEvent(7.1, 1, 29284, 3), EffectEvent(8.1, 1, 29284, 4),
        ]);
        controller.Seek(7.2); Check(Goals() == 1 && Previews().Length == 1, "Production Akh Morn dropped before fourth hit.");
        controller.Seek(8.2); Check(Goals() == 0 && Previews().Length == 0, "Production fourth Akh Morn hit failed to clear.");
        controller.Seek(7.2); Check(Goals() == 1, "Production Akh Morn seek failed.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Keeper six-hit flame, three Antipathy stages, timeline Mirage, four-hit stack, duplicate packets and seek");
        center = new(-104, 0, -395);
        Load("masterplayer.dungeon.sohm-al",
        [
            Header(1064),
            Frame(1064, 0, [Actor(1, 0xE96, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x1B2, center + Vector3.UnitX * 20)], full: true),
            JsonSerializer.Serialize(new { t = .05, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = 100, MarkerId = 7 } }),
            Frame(1064, .1, [Actor(10, 0x1B2, center + Vector3.UnitX * 20, cast: Cast(4999, 3.5, center + Vector3.UnitX * 20))]),
            CastEvent(.1, 10, 4999, 3.5, center + Vector3.UnitX * 20),
            Frame(1064, 3.6, [Actor(10, 0x1B2, center + Vector3.UnitX * 20)]),
            EffectEvent(5.4, 20, 4997, 1),
        ]);
        controller.Seek(.07); Check(Goals() == 1 && Previews().Length == 1, "Production meteor bait lacks personal edge goal.");
        controller.Seek(3.7); Check(Goals() == 0 && Dangers() == 1 && Previews().Length == 1, "Production meteor lost delayed hit after cast.");
        controller.Seek(5.5); Check(Dangers() == 0 && Previews().Length == 0, "Production meteor effect retained impact.");
        controller.Seek(.07); Check(Goals() == 1, "Production meteor seek failed to restore bait.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Sohm Al Tioman edge bait, locked meteor, delayed impact, legacy icon clock, seek and cleanup");
        center = new(334, 0, -202);
        Load("masterplayer.dungeon.aery",
        [
            Header(1065),
            Frame(1065, 0, [Actor(1, 0xEA6, center, combat: true), Actor(100, 0, center + Vector3.UnitX * 10),
                Actor(101, 0, center + Vector3.UnitZ * 10), Actor(102, 0, center - Vector3.UnitX * 10),
                Actor(103, 0, center - Vector3.UnitZ * 10)], full: true,
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL },
                    new { Slot = 2, EntityId = 102UL }, new { Slot = 3, EntityId = 103UL }]),
            Frame(1065, .1, [Actor(1, 0xEA6, center, combat: true, cast: Cast(3890, 3, center))],
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL },
                    new { Slot = 2, EntityId = 102UL }, new { Slot = 3, EntityId = 103UL }]),
            CastEvent(.1, 1, 3890, 3, center),
            Frame(1065, 3.1, [Actor(1, 0xEA6, center, combat: true)]),
            EffectEvent(4.3, 1, 3891, 1), EffectEvent(4.3, 1, 3891, 1),
            EffectEvent(4.5, 1, 3891, 2), EffectEvent(4.7, 1, 3891, 3),
        ]);
        controller.Seek(3.2); Check(Previews().Length == 4 && Dangers() > 0, "Production Electrocution lost candidate rays after visual.");
        controller.Seek(4.6); Check(Previews().Length == 4, "Production Electrocution counted repeated target rows.");
        controller.Seek(4.8); Check(Previews().Length == 0 && Dangers() == 0, "Production third electric hit leaked.");
        controller.Seek(3.2); Check(Previews().Length == 4, "Production Electrocution seek failed.");
        Stop();
        center = new(11.978f, 0, 67.979f);
        Load("masterplayer.dungeon.aery",
        [
            Header(1065),
            Frame(1065, 0, [Actor(1, 0x3970, center, combat: true), Actor(100, 0, center)], full: true),
            Frame(1065, .1, [Actor(1, 0x3970, center, combat: true, cast: Cast(30181, 4, center))]),
            CastEvent(.1, 1, 30181, 4, center),
            Frame(1065, 4.1, [Actor(1, 0x3970, center, combat: true)]),
            JsonSerializer.Serialize(new { t = 5.2, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
                data = new { Position = 0, Param1 = 1, Param2 = 2 } }),
        ]);
        controller.Seek(4.2); Check(Dangers() == 1 && Previews().Length == 1, "Production shrink forecast vanished with cast.");
        controller.Seek(5.3);
        dynamic shrunk = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(Dangers() == 0 && (float)shrunk.Radius == 19.9f, "Production packed map event failed to shrink arena.");
        controller.Seek(4.2); Check(Dangers() == 1, "Production shrink seek failed.");
        Stop();
        center = new(35, 0, -267);
        Load("masterplayer.dungeon.aery",
        [
            Header(1065),
            Frame(1065, 0, [Actor(1, 0x39CA, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 5), Actor(10, 0x39CB, center + Vector3.UnitX * 10, hitboxRadius: 1)], full: true,
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }]),
            StatusEvent(.1, 101, 3324, 0, true), StatusEvent(1, 101, 3324, 0, false),
            Frame(1065, 1.1, [], removed: [10]),
        ]);
        controller.Seek(.2);
        instance = ((IEnumerable)manager.Running.Values).Cast<object>().Single().GetType().GetProperty("Script")!.GetValue(
            ((IEnumerable)manager.Running.Values).Cast<object>().Single())!;
        Check(((ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!).SequenceEqual([1UL])
            && Goals() == 1, "Production fetter did not publish boss ban and rescue goal.");
        controller.Seek(1.2);
        Check(((ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!).Length == 0
            && Goals() == 0, "Production fetter release retained ban or rescue goal.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Aery three-hit rays, packed map shrink, fetter boss ban, rescue goal, seek and cleanup");
        center = new(0, 0, -100);
        Load("masterplayer.dungeon.vault",
        [
            Header(1066),
            Frame(1066, 0, [Actor(1, 0x1051, center + Vector3.UnitX * 17, combat: true), Actor(100, 0, center)], full: true),
            JsonSerializer.Serialize(new { t = .1, kind = "NpcYell", type = "PromeRotation.LogSystem.LogSystemNpcYellEvent",
                data = new { SourceId = 1, NpcYellId = 2523 } }),
            EffectEvent(.3, 1, 4130, 1), EffectEvent(.3, 1, 4130, 1),
            EffectEvent(2.5, 1, 4130, 2), EffectEvent(4.7, 1, 4130, 3), EffectEvent(6.9, 1, 4130, 4),
        ]);
        controller.Seek(.2); Check(Dangers() == 4 && Previews().Length == 4, "Production Vault yell did not forecast four charges.");
        controller.Seek(.4); Check(Dangers() == 3 && Previews().Length == 3, "Production Vault duplicate effect advanced twice.");
        controller.Seek(7d); Check(Dangers() == 0 && Previews().Length == 0, "Production Vault fourth charge leaked.");
        controller.Seek(.2); Check(Dangers() == 4, "Production Vault charge seek lost timeline.");
        Stop();
        center = new(0, 0, 72);
        Load("masterplayer.dungeon.vault",
        [
            Header(1066),
            Frame(1066, 0, [Actor(1, 0x1054, center, combat: true), Actor(100, 0, center)], full: true),
            Frame(1066, .1, [Actor(1, 0x1054, center, combat: true, cast: Cast(4140, 3, center + Vector3.UnitX * 6))]),
            CastEvent(.1, 1, 4140, 3, center + Vector3.UnitX * 6),
            Frame(1066, 3.1, [Actor(1, 0x1054, center, combat: true)]),
            EffectEvent(3.1, 1, 4140, 1, center + Vector3.UnitX * 6),
            Frame(1066, 4.2, [Actor(10, 0x1E91C1, center + Vector3.UnitX * 6)]),
            Frame(1066, 5, [Actor(10, 0x1E91C1, center + Vector3.UnitX * 6, eventState: 7)]),
        ]);
        controller.Seek(3.2); Check(Dangers() == 1 && Previews().Length == 1, "Production Vault rip effect lost spawn forecast.");
        controller.Seek(4.3); Check(Dangers() == 1 && Previews().Length == 1, "Production Vault rip handoff duplicated output.");
        controller.Seek(5.1); Check(Dangers() == 0 && Previews().Length == 0, "Production Vault inactive rip leaked.");
        Stop();
        center = new(0, 0, 4.1f);
        Load("masterplayer.dungeon.vault",
        [
            Header(1066),
            Frame(1066, 0, [Actor(1, 0x1056, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 5), Actor(10, 0x1057, new(0, 0, 18), modelState: 8)], full: true,
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }]),
            JsonSerializer.Serialize(new { t = .1, kind = "Tether", type = "PromeRotation.LogSystem.LogSystemTetherEvent",
                data = new { SourceId = 100, TargetId = 101, TetherId = 9 } }),
            JsonSerializer.Serialize(new { t = .8, kind = "TetherCancel", type = "PromeRotation.LogSystem.LogSystemTetherCancelEvent",
                data = new { SourceId = 100, TargetId = 101, TetherId = 9 } }),
            Frame(1066, 1.1, [Actor(10, 0x1057, center, modelState: 0)]),
        ]);
        controller.Seek(.2);
        var knightPreview = Previews().Single(p => p.GetType().GetProperty("Geometry")!.GetValue(p)!.GetType().Name == "DungeonCapsuleGeometry");
        dynamic capsule = knightPreview.GetType().GetProperty("Geometry")!.GetValue(knightPreview)!;
        Check(Dangers() == 5 && Previews().Length == 2 && (bool)capsule.Contains(new Vector3(0, 0, 29.99f), 0f)
            && ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count() == 1,
            "Production Vault knight model state, four forecasts, obstacle or chain missing.");
        controller.Seek(1.2);
        Check(Dangers() == 0 && Previews().Length == 0
            && !((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Any(),
            "Production Vault cancelled chain or central stationary knight leaked.");
        controller.Seek(.2); Check(Dangers() == 5, "Production Vault model state or chain seek failed.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Vault four charges, legacy yell clock, rip object handoff, knight model state, timed capsules, chains and seek");
        center = new(-10, 0, 0);
        Load("masterplayer.dungeon.gubal-library",
        [
            Header(1109),
            Frame(1109, 0, [Actor(1, 0xE82, Vector3.Zero, combat: true), Actor(100, 0, center),
                Actor(10, 0xED6, Vector3.Zero)], full: true),
            Frame(1109, .1, [Actor(10, 0xED6, Vector3.Zero, cast: Cast(4818, 8, Vector3.Zero))]),
            CastEvent(.1, 10, 4818, 8, Vector3.Zero),
            Frame(1109, 8.1, [Actor(10, 0xED6, Vector3.Zero)]),
            EffectEvent(10.5, 10, 3989, 1),
        ]);
        controller.Seek(.2);
        Check(Dangers() == 2 && Previews().Length == 2, "Production Gubal disclosure or spin missing.");
        controller.Seek(8.2);
        Check(Dangers() == 1 && Previews().Length == 1, "Production Gubal cast finish lost delayed spin.");
        controller.Seek(10.6);
        Check(Dangers() == 0 && Previews().Length == 0, "Production Gubal spin effect leaked.");
        controller.Seek(.2); Check(Dangers() == 2, "Production Gubal spin seek failed.");
        Stop();
        center = new(177.8f, 0, 27.2f);
        Load("masterplayer.dungeon.gubal-library",
        [
            Header(1109),
            Frame(1109, 0, [Actor(1, 0xE83, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x10C4, center + Vector3.UnitX * 10), Actor(11, 0x10C5, center + Vector3.UnitZ * 10)], full: true),
            StatusEvent(.1, 1, 325, 0, true),
            JsonSerializer.Serialize(new { t = .1, kind = "Tether", type = "PromeRotation.LogSystem.LogSystemTetherEvent",
                data = new { SourceId = 10, TargetId = 100, TetherId = 3 } }),
            JsonSerializer.Serialize(new { t = .8, kind = "TetherCancel", type = "PromeRotation.LogSystem.LogSystemTetherCancelEvent",
                data = new { SourceId = 10, TargetId = 100, TetherId = 3 } }),
            StatusEvent(.8, 1, 325, 0, false),
        ]);
        controller.Seek(.2);
        Check(((ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!).SequenceEqual([1UL])
            && Goals() == 1 && ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count() == 1,
            "Production Gubal invincibility, oil goal or wind obstacle missing.");
        controller.Seek(.9);
        Check(Goals() == 0 && ((ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!).Length == 0,
            "Production Gubal tether/status cancellation leaked.");
        Stop();
        center = new(377.8f, 0, -59.7f);
        Load("masterplayer.dungeon.gubal-library",
        [
            Header(1109),
            Frame(1109, 0, [Actor(1, 0xE84, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x1E99F3, center + Vector3.UnitX * 10), Actor(11, 0x1E99F3, center - Vector3.UnitX * 10),
                Actor(12, 0xEC5, center)], full: true, party: [new { Slot = 0, EntityId = 100UL }]),
            Frame(1109, 1, [Actor(10, 0x1E99F3, center + Vector3.UnitX * 10, eventAnimation: 0x00100020)]),
            Frame(1109, 2, [Actor(13, 0xE86, center)]),
        ]);
        controller.Seek(.2);
        Check(Goals() == 2 && Dangers() == 1 && Previews().Length == 3, "Production Gubal towers or sphere birth missing.");
        controller.Seek(1.1); Check(Goals() == 1, "Production Gubal tower EAnim did not resolve exactly one.");
        controller.Seek(2.1); Check(Goals() == 0, "Production Gubal summon did not clear towers.");
        controller.Seek(.2); Check(Goals() == 2, "Production Gubal tower seek failed.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Gubal spin deadlines, arena switching, invincibility, oil tethers, wind obstacles, sphere births, tower animation, summons and seek");
        center = new(-10, 0, 0);
        Load("masterplayer.dungeon.gubal-library",
        [
            Header(1109),
            Frame(1109, 0, [Actor(1, 0xE82, Vector3.Zero, combat: true), Actor(100, 0, center, stackedStatus: 911)], full: true,
                legacyMovement: true, cameraAzimuth: -MathF.PI / 2),
            Frame(1109, 1, [], legacyMovement: false),
        ]);
        controller.Seek(.2);
        var landings = Previews().Select(p => p.GetType().GetProperty("Geometry")!.GetValue(p)!)
            .Where(g => g.GetType().Name == "CircleGeometry" && (float)g.GetType().GetProperty("Radius")!.GetValue(g)! < 1).ToArray();
        var landingPosition = (Vector3)landings.Single().GetType().GetProperty("Position")!.GetValue(landings.Single())!;
        Check(landingPosition.X > -4 && landingPosition.X < -3, "Production legacy ice ignored recorded camera.");
        controller.Seek(1.1);
        landings = Previews().Select(p => p.GetType().GetProperty("Geometry")!.GetValue(p)!)
            .Where(g => g.GetType().Name == "CircleGeometry" && (float)g.GetType().GetProperty("Radius")!.GetValue(g)! < 1).ToArray();
        landingPosition = (Vector3)landings.Single().GetType().GetProperty("Position")!.GetValue(landings.Single())!;
        Check(landingPosition.Z > 9, "Production standard ice did not use actor rotation.");
        Stop();
        Console.WriteLine("PASS production native dungeons: Gubal recorded legacy camera and standard thin ice direction");
        RunResearch(assembly, (object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers, Goals);
        RunFractal(assembly, (object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers, Goals);
        RunArboretum((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunAntitower((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunSohrKhai((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunXelphatol((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunBaelsar((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunSirensong((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunShisui((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunBardam((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDoma((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunAbania((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunAlaMhigo((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunSkalla((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunBurn((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunGhimlyt((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunHolminster((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunDohnMheg((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunQitana((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunMalikah((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunMtGulg((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunAmaurot((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunTwinning((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunAnyder((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunCosmos((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunAnamnesis((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunHeroes((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunMatoya((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunPaglthan((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunZot((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunBabil((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunVanaspati((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunKtisis((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunAitiascope((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunDeadEnds((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunSmileton((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunStigma((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunAlzadaal((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunTroia((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunLapis((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunAetherfont((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunLunar((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunIhuykatumu((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunWorqor((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunSkydeep((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunVanguard((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunOrigenics((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunAlexandria((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunTenderValley((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunStrayborough((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunYuweyawata((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunUnderkeep((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunMeso((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunMistwake((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunClyteum((object)manager, (object)controller, Load, Stop, Previews, Dangers);
        RunArrPrimals((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunPorta((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHardPrimals((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunLateArrTrials((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunSusano((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunLakshmi((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunShinryu((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTsukuyomi((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunYojimbo((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunFourLords((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunSeiryu((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTitania((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunInnocence((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHades((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunWarriorOfLight((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHydaelyn((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunAsura((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunZelenia((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunValigarmanda((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunArkveld((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunEnuo((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunNecron((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunQueenEternal((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunZoraalJa((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDoomtrain((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunCoil((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTwintania((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunLabyrinth((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunSyrcus((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDarkness((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunArrQuests((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHeavenswardIntro((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunPalacePlants((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunPalaceNybeth((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunPalaceGodmother((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunPalaceGudanna((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunPalaceBlackRider((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunPalaceYaquaru((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureGeometry((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasurePredictions((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureDelay((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureLights((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureOubliettes((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureRoster((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureBull((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureGymnasion((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureTritonSampling((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureSphinx((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureAltars((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasurePersistent((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureBaits((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureFuathGargantuan((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureSequences((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureVaultTrio((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureLyhe((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureCanalsLucky((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureAltarWall((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureAltarWater((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureCenoteRooms((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureIceBubble((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureGymnasionTrio((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureVaultFour((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureKeeperDaen((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureCladoTyphon((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureGymnasionHammers((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureNarkissos((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureMolter((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureFlowers((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureFortress((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureYan((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTreasureHexapod((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHeavenswardQuests((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTisiphone((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunPalaceSerpents((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDivineIntervention((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunGraoully((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunQuestTargets((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunJobQuests((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunPortrait((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunViperSamurai((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunBekkoOrcus((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunFallenKuribu((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunSageLife((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunIoSteelNode((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunQuestWinds((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunMainScenario((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunFaded((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunFeast((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunRhalgr((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunEndwalkerQuests((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunQuestParty((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FFXIV_SQPACK")))
            RunHeavenswardFloor((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunUltimaExtreme((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunGarudaExtreme((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTitanExtreme((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunIfritExtreme((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunSimpleHunts((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntControls((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntSequences((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntInvertedGaze((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntCombos((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntFanAil((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntMisdirection((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntStored((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntRotations((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntMissiles((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntCompressor((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntTargets((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntFollowups((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntPulls((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntAegeiros((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntStack((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntMarch((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntExecution((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntTarchia((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntRecordedTurns((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntForecast((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntKirlirger((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntTyger((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntCatsEye((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntQueenHawk((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntRaintriller((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntKer((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntGunitt((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntAtticus((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntHeshuala((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntIhnuxokiy((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntNeyoozoteel((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntNechuciho((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntOwnerInitialization((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntPedantry((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntIxtab((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntSlippery((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntRebellion((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntSugriva((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntFinalTrio((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHuntAglaope((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunNovice((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunFates((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDaivadipa((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunNoviceTank04((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunFormidable((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunChiMica((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTtokrrone((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTactical((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunSleepOtis((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunSlickshell((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunMax((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHope((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunTime((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHeroesPretenders((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunIseultalonDikaiosyne((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunAlphaCarnivale((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHeavenOnHigh((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHeavenOnHighFloors((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunEurekaFloors((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunPilgrimsFloors((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunEurekaOrthos((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunQuantum((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunKefka((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunWickedThunder((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHalicarnassus((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunAlteRoite((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunCrescentFates((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunCrescentVariants((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunHydatos((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunBozja((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunBozjaRobots((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDal1((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDal0((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDal2((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDal3((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDAL4DiabloArmament((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDAL1Gauntlet((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunCLL2Albeleo((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunBozjaSkirmishes((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunBozjaPyromancer((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunCLL1Brionac((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunCLL3Adrammelech((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunCLL4Dawon((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunBaldesionArt((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunBaldesionOwain((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunBaldesionRaiden((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunBaldesionVirtue((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunAllianceTrash((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunZadnor((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunZadnorBurn((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunZadnorGrave((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunZadnorTime((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunZadnorCrystals((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunZadnorHashmal((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunZadnorAida((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunZadnorCavalry((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunZadnorFrost((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDelubrumDahu((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDelubrumPhantom((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDelubrumPhantomSavage((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDelubrumDahuSavage((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDelubrumSeeker((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDelubrumGuard((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunDelubrumQueen((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunThordan((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunBarPassageP2((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
        RunPilgrimsLiveJoint((object)manager, (object)runtime, (object)controller, Load, Stop, Previews, Dangers);
    }

    private static string Header(uint territory)
        => JsonSerializer.Serialize(new { v = 2, territory, startedUtc = Start, pluginVersion = "native-dungeon-synthetic-regression" });

    private static string Frame(uint territory, double t, IEnumerable<object> actors, bool full = false, ulong[]? removed = null, object[]? party = null,
        bool legacyMovement = false, float? cameraAzimuth = null, ulong? sourcePlayerId = null, uint? allowedFateId = null)
        => JsonSerializer.Serialize(new { t, kind = "WorldFrame", data = new { Full = full, Territory = territory, LocalPlayerId = 100,
            LegacyMovement = legacyMovement, CameraAzimuth = cameraAzimuth, SourcePlayerId = sourcePlayerId, AllowedFateId = allowedFateId,
            Actors = actors.ToArray(), Removed = removed ?? [], Party = party ?? [] } });

    private static object Actor(ulong id, uint oid, Vector3 position, bool combat = false, object? cast = null, bool shield = false,
        bool doom = false, int? renderFlags = null, bool invincible = false, uint stackedStatus = 0, float rotation = 0,
        float hitboxRadius = .5f, byte eventState = 0, byte animationState1 = 0, uint? eventAnimation = null, uint classJob = 0, byte modelState = 0,
        bool targetable = true, ulong targetId = 0, object[]? tethers = null, ushort statusParam = 3, bool dead = false, DateTime? statusExpiresAt = null,
        uint nameId = 0, byte animationState2 = 0, byte subKind = 0, ushort? objectIndex = null, bool? isEnemy = null)
    {
        var statuses = new List<object>();
        if (shield) statuses.Add(new { Id = 4175, ExpiresAt = Start.AddSeconds(30) });
        if (doom) statuses.Add(new { Id = 5187, ExpiresAt = Start.AddSeconds(30) });
        if (invincible) statuses.Add(new { Id = 4410, ExpiresAt = Start.AddSeconds(30) });
        if (stackedStatus != 0) statuses.Add(new { Id = stackedStatus, Param = statusParam, ExpiresAt = statusExpiresAt ?? Start.AddSeconds(30) });
        return new { Id = id, Oid = oid, Kind = oid == 0 ? 1 : 2, SubKind = subKind, ObjectIndex = objectIndex, IsEnemy = isEnemy, Position = Pose(position),
            Hp = dead ? 0 : 1000, MaxHp = 1000, Dead = dead, Targetable = targetable, TargetId = targetId, InCombat = combat, Hostile = oid != 0, Cast = cast,
            Rotation = rotation, HitboxRadius = hitboxRadius, EventState = eventState, RenderFlags = renderFlags,
            AnimationState1 = animationState1, AnimationState2 = animationState2, EventAnimation = eventAnimation, ClassJob = classJob, ModelState = modelState, NameId = nameId, Statuses = statuses.ToArray(), Tethers = tethers ?? [] };
    }

    private static object Cast(uint action, double seconds, Vector3 position, float rotation = 0, ulong target = 0, double startedAt = .1)
        => new { ActionId = action, TargetId = target, Location = Pose(position), Rotation = rotation, StartedAt = Start.AddSeconds(startedAt),
            FinishAt = Start.AddSeconds(startedAt + seconds + .3) };

    private static string CastEvent(double t, ulong source, uint action, double seconds, Vector3 position, float rotation = 0, ulong target = 0)
        => JsonSerializer.Serialize(new { t, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = rotation,
            data = new { SourceId = source, TargetId = target, ActionId = action, CastTime = seconds, EffectPosition = Pose(position), Timestamp = Start.AddSeconds(t) } });

    private static string EffectEvent(double t, ulong source, uint action, uint sequence, Vector3? position = null, float? rotation = null, ulong target = 0)
        => JsonSerializer.Serialize(new { t, kind = "ActionEffect", type = "PromeRotation.LogSystem.LogSystemActionEffectEvent",
            data = new { SourceId = source, TargetId = target, ActionId = action, GlobalSequence = sequence, EffectPosition = Pose(position ?? default),
                SourceRotation = rotation, Timestamp = Start.AddSeconds(t) } });

    private static string StatusEvent(double t, ulong target, uint status, ushort param, bool present)
        => JsonSerializer.Serialize(new { t, kind = present ? "StatusAdd" : "StatusRemove",
            type = "PromeRotation.LogSystem.LogSystemStatus" + (present ? "Add" : "Remove") + "Event",
            data = new { TargetId = target, StatusId = status, Param = param, Timestamp = Start.AddSeconds(t) } });

    private static object Pose(Vector3 p) => new { p.X, p.Y, p.Z };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
