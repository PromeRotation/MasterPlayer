using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAbania(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.castrum-abania";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter()
        {
            var script = Script();
            return script.GetType().BaseType!.GetField("encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Areas(double t)
        {
            var e = Encounter();
            return ((IEnumerable)e.GetType().GetMethod("Collect", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(e, [Start.AddSeconds(t), 100UL])!).Cast<object>().ToArray();
        }
        string Key(object area) => (string)area.GetType().GetProperty("Key")!.GetValue(area)!;
        void CannonIsIsolated()
        {
            var script = Script();
            var cannon = script.GetType().BaseType!.GetField("cannonControl", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
            foreach (var name in new[] { "InteractionAttempts", "PulseAttempts" })
                Check((int)cannon.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cannon)! == 0,
                    "Production Abania replay invoked native cannon.");
        }
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var center = new Vector3(-212.99f, 0, 186.99f);
        var helpers = Enumerable.Range(10, 4).Select(i => Actor((ulong)i, 0x18D6, center + Vector3.UnitX * (i - 10))).ToArray();
        var casts = Enumerable.Range(10, 4).Select(i => Actor((ulong)i, 0x18D6, center + Vector3.UnitX * (i - 10),
            cast: Cast(7960, 6.5 + i - 10, center + Vector3.UnitX * (i - 10)))).ToArray();
        List<string> records =
        [
            Header(1145), Frame(1145, 0, [Actor(1, 0x1AA9, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 10), Actor(20, 0x1AAC, center), Actor(21, 0x1AAA, center), .. helpers], full: true, party: party),
            Frame(1145, .1, casts, party: party),
            Frame(1145, 1, [Actor(20, 0x1AAC, center, targetable: false)], party: party),
            Frame(1145, 10, helpers, party: party),
        ];
        for (uint i = 0; i < 4; ++i) records.Add(EffectEvent(10.5 + i, 99, 8184, i + 1));
        load(id, records);
        controller.Seek(.2); Check(previews().Length == 5 && dangers() == 4, "Production Abania Haywire/cannon outline missing."); CannonIsIsolated();
        controller.Seek(1.2);
        var forbidden = (ulong[])Encounter().GetType().GetProperty("ForbiddenTargets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Encounter())!;
        Check(forbidden.Contains(1UL) && forbidden.Contains(21UL), "Production Abania mounted target ban missing.");
        controller.Seek(10.1); Check(previews().Length == 4 && dangers() == 4, "Production Abania natural Haywire finish lost queue.");
        for (var i = 0; i < 4; ++i)
        {
            controller.Seek(10.6 + i); Check(previews().Length == 3 - i && dangers() == 3 - i, "Production Abania real Haywire effect failed to advance.");
        }
        controller.Seek(.2); Check(previews().Length == 5, "Production Abania Haywire seek failed."); CannonIsIsolated(); stop();
        load(id,
        [
            Header(1145), Frame(1145, 0, [Actor(1, 0x1AA9, center, combat: true), Actor(100, 0, center), .. helpers], full: true, party: party),
            Frame(1145, .1, casts, party: party), StatusEvent(1, 1, 1399, 0, true),
            Frame(1145, 1.1, [], party: party),
        ]);
        controller.Seek(1.2); Check(previews().Length == 0 && dangers() == 0, "Production Abania Fetters did not clear queue."); stop();

        center = new(10.5f, 0, 186.5f);
        load(id,
        [
            Header(1145), Frame(1145, 0, [Actor(1, 0x3F3B, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 10), Actor(10, 0x233C, center - Vector3.UnitX * 10),
                Actor(11, 0x233C, center + Vector3.UnitX * 10)], full: true, party: party),
            Frame(1145, .1, [Actor(10, 0x233C, center - Vector3.UnitX * 10, cast: Cast(33464, 3, center)),
                Actor(11, 0x233C, center + Vector3.UnitX * 10, cast: Cast(33464, 3, center))], party: party),
            Frame(1145, 3.1, [Actor(10, 0x233C, center - Vector3.UnitX * 10), Actor(11, 0x233C, center + Vector3.UnitX * 10)], party: party),
            Frame(1145, 4, [Actor(10, 0x233C, center, cast: Cast(33462, 5, center, target: 101, startedAt: 4))], party: party),
            Frame(1145, 9, [Actor(10, 0x233C, center)], party: party),
            EffectEvent(10, 10, 33467, 1, target: 100),
            EffectEvent(17, 10, 33466, 2),
        ]);
        controller.Seek(.2); Check(previews().Length == 2 && dangers() == 2 && Areas(.2).Any(a => Key(a) == "tower/enter"), "Production Abania strong tower demand missing.");
        controller.Seek(3.2); Check(previews().Length == 0 && dangers() == 0, "Production Abania tower natural finish leaked.");
        controller.Seek(4.2); Check(previews().Length == 1 && dangers() == 1 && Areas(4.2).Any(a => Key(a).EndsWith("/enter")), "Production Abania strong stack demand missing.");
        controller.Seek(9.2); Check(previews().Length == 0 && dangers() == 0, "Production Abania stack finish leaked.");
        controller.Seek(16.1); Check(previews().Length == 1 && dangers() == 1, "Production Abania bait expired before actual effect.");
        var bait = Areas(16.1).Single(a => Key(a).StartsWith("bait/"));
        Check(Math.Abs(((DateTime)bait.GetType().GetProperty("From")!.GetValue(bait)! - Start.AddSeconds(15)).TotalMilliseconds) < 1,
            "Production Abania bait original event clock shifted.");
        controller.Seek(17.1); Check(previews().Length == 0 && dangers() == 0, "Production Abania bait effect retained.");
        controller.Seek(.2); Check(previews().Length == 2, "Production Abania Subject seek failed."); stop();
        var gridActors = Enumerable.Range(10, 11).Select(i => Actor((ulong)i, 0x233C, center)).ToArray();
        var flameActors = Enumerable.Range(30, 4).Select(i => Actor((ulong)i, 0x233C, center)).ToArray();
        load(id,
        [
            Header(1145), Frame(1145, 0, [Actor(1, 0x3F3B, center, combat: true), Actor(100, 0, center), .. gridActors, .. flameActors], full: true, party: party),
            Frame(1145, .1, [.. Enumerable.Range(10, 11).Select(i => Actor((ulong)i, 0x233C, center, cast: Cast(33460, 4, center))),
                .. Enumerable.Range(30, 4).Select(i => Actor((ulong)i, 0x233C, center, cast: Cast(33463, 4, center)))], party: party),
            EffectEvent(4, 10, 33460, 1), EffectEvent(4, 30, 33463, 2),
            Frame(1145, 4.1, [.. gridActors, .. flameActors], party: party),
        ]);
        controller.Seek(.2); Check(previews().Length == 13 && dangers() == 13, "Production Abania ten grids/three cones cap differs.");
        controller.Seek(4.05); Check(previews().Length == 13, "Production Abania next helpers did not advance.");
        controller.Seek(4.2); Check(previews().Length == 0 && dangers() == 0, "Production Abania helpers finish leaked."); stop();

        center = new(282.5f, 0, -27.25f);
        load(id,
        [
            Header(1145), Frame(1145, 0, [Actor(1, 0x1AAE, center, combat: true),
                Actor(100, 0, center + Vector3.UnitZ * 3), Actor(101, 0, center + Vector3.UnitX * 12),
                Actor(10, 0x1AB0, center - Vector3.UnitX * 8, tethers: [new { Id = 1, TargetId = 100UL }]),
                Actor(11, 0x18D6, center), Actor(12, 0x18D6, center + Vector3.UnitZ * 10)], full: true, party: party),
            ResearchIconEvent(.1, 100, 74), ResearchIconEvent(.1, 101, 74),
            Frame(1145, .2, [Actor(11, 0x18D6, center, cast: Cast(8329, 3.5, center, startedAt: .2)),
                Actor(12, 0x18D6, center + Vector3.UnitZ * 10, cast: Cast(7976, 4, center + Vector3.UnitZ * 10, startedAt: .2))], party: party),
            Frame(1145, 4.2, [Actor(11, 0x18D6, center), Actor(12, 0x18D6, center + Vector3.UnitZ * 10)], party: party),
            EffectEvent(7, 11, 7978, 1, target: 100), EffectEvent(7, 11, 7978, 1, target: 101),
            JsonSerializer.Serialize(new { t = 20, kind = "TetherCancel", type = "PromeRotation.LogSystem.LogSystemTetherCancelEvent",
                data = new { SourceId = 10, TargetId = 100, TetherId = 1, Timestamp = Start.AddSeconds(20) } }),
            Frame(1145, 21, [Actor(10, 0x1AB0, center - Vector3.UnitX * 8)], party: party),
        ]);
        controller.Seek(.3);
        Check(previews().Length == 5 && Areas(.3).Any(a => Key(a).StartsWith("knockback-landing/")), "Production Abania comet/wave/landing/spreads missing.");
        controller.Seek(4.3); Check(previews().Length == 2, "Production Abania comet/wave finish leaked.");
        controller.Seek(7.1); Check(previews().Length == 0 && dangers() == 1, "Production Abania batched spread effect or long claw differs.");
        controller.Seek(19); Check(dangers() == 1, "Production Abania claw timed out at fifteen seconds.");
        controller.Seek(20.8); Check(dangers() == 0, "Production Abania stale claw resurrected after cancellation.");
        controller.Seek(.3); Check(previews().Length == 5, "Production Abania Inferno seek failed."); stop();
        Console.WriteLine("PASS native Abania three bosses, four-effect Haywire, cannon planning and attack ban, native execution isolation, Fetters, strong towers and stacks, capped helpers, bait clock, comet landing, long claw, batch spread, seek and stop");
    }
}
