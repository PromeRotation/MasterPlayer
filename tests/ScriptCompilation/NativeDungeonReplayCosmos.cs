using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCosmos(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.cosmos";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object a) => (string)a.GetType().GetProperty("Id")!.GetValue(a)!;
        bool Ready() => (bool)Encounter().GetType().GetProperty("CosmosReadyToDrop", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Encounter())!;
        object? Seed() => Encounter().GetType().GetProperty("CosmosSeedTarget", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Encounter());
        string Map(double time, uint state) => JsonSerializer.Serialize(new { t = time, kind = "MapEffect",
            type = "PromeRotation.LogSystem.LogSystemMapEffectEvent", data = new { Position = 11, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(time) } });
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(0, 0, 187);
        load(id,
        [
            Header(884), Frame(884, 0, [Actor(1, 0x2C1A, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 12)], full: true, party: party),
            Frame(884, .1, [Actor(10, 0x233C, c, cast: Cast(18852, 3, c))], party: party),
            EffectEvent(3.2, 10, 18852, 1, c),
            Frame(884, 3.3, [Actor(10, 0x233C, c), Actor(20, 0x1EAEAE, c)], party: party),
            Frame(884, 4, [Actor(20, 0x1EAEAE, c, eventState: 7), Actor(30, 0x2C1B, c, rotation: .7f, animationState1: 1)], party: party),
            Frame(884, 5, [Actor(30, 0x2C1B, c + Vector3.UnitX * 4, rotation: .3f, animationState1: 1)], party: party),
            Frame(884, 6, [Actor(30, 0x2C1B, c, animationState1: 0), Actor(1, 0x2C1A, c, combat: true, cast: Cast(18282, 5, c, target: 101, startedAt: 6))], party: party),
            Frame(884, 11.1, [Actor(1, 0x2C1A, c, combat: true)], party: party),
            Frame(884, 12, [Actor(1, 0x2C1A, c, combat: true, cast: Cast(18282, 5, c, target: 100, startedAt: 12))], party: party),
        ]);
        controller.Seek(.2); Check(previews().Length == 1 && dangers() == 1, "Production Cosmos Tribulation missing.");
        controller.Seek(3.4); Check(Aoes().Length == 1 && AoeId(Aoes()[0]) == "cosmos/pool/20", "Production Cosmos pool birth handoff failed.");
        controller.Seek(4.1); Check(previews().Length == 1 && dangers() == 5 && Aoes().Length == 6, "Production Cosmos moving broom windows missing.");
        controller.Seek(5.1); Check((Vector3)Aoes().Single(a => AoeId(a) == "cosmos/broom/30").GetType().GetProperty("Position")!.GetValue(Aoes().Single(a => AoeId(a) == "cosmos/broom/30"))! == c + Vector3.UnitX * 4,
            "Production Cosmos broom position did not update.");
        controller.Seek(6.1); Check(Aoes().Any(a => AoeId(a) == "cosmos/stack/enter/1"), "Production Cosmos mandatory stack missing.");
        controller.Seek(11.2); Check(previews().Length == 0 && dangers() == 0, "Production Cosmos stack finish leaked.");
        controller.Seek(12.1); Check(Aoes().Any(a => AoeId(a) == "cosmos/stack/enter/1"), "Production Cosmos stack bearer did not approach party.");
        controller.Seek(.2); Check(previews().Length == 1, "Production Cosmos seeker seek failed."); stop();

        c = new(0, 0, -60);
        load(id,
        [
            Header(884), Frame(884, 0, [Actor(1, 0x2C04, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8),
                Actor(20, 0x1EAE9E, new(-5, 0, -75))], full: true, party: party),
            Map(.1, 0x00020001),
            Frame(884, .2, [Actor(1, 0x2C04, c, combat: true, cast: Cast(18205, 4, c, startedAt: .2))], party: party),
            Frame(884, 1, [Actor(100, 0, new(-5, 0, -75), stackedStatus: 404), Actor(20, 0x1EAE9E, new(-5, 0, -75), targetable: false)], party: party),
            Frame(884, 2, [Actor(100, 0, new(-15, 0, -75), stackedStatus: 404)], party: party),
            Frame(884, 3, [Actor(100, 0, new(-15, 0, -75), stackedStatus: 404)], party: party),
            Frame(884, 4.3, [Actor(1, 0x2C04, c, combat: true), Actor(100, 0, c)], party: party),
            Frame(884, 5, [Actor(1, 0x2C04, c, combat: true, cast: Cast(18205, 4, c, startedAt: 5))], party: party),
            Frame(884, 6, [Actor(30, 0x2C06, c - Vector3.UnitZ * 20, cast: Cast(18209, 13, c, 0, startedAt: 6))], party: party),
            Map(20, 0),
            Frame(884, 20.1, [Actor(30, 0x2C06, c - Vector3.UnitZ * 20), Actor(1, 0x2C04, c, combat: true)], party: party),
        ]);
        controller.Seek(.3); Check(previews().Length == 9 && dangers() == 0 && Seed() != null, "Production Cosmos first tiles/seed goals missing.");
        controller.Seek(1.1); Check(dangers() == 8 && Seed() == null && !Ready(), "Production Cosmos transporting did not switch tile risk.");
        controller.Seek(3.1); Check(Ready(), "Production Cosmos safe seed drop not planned.");
        var drop = Script().GetType().BaseType!.GetField("cosmosSeedDrop", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        Check(drop.GetType().GetField("safeSince", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(drop) == null,
            "Production Cosmos replay started native status cancellation.");
        controller.Seek(5.1); Check(!Aoes().Any(a => AoeId(a).StartsWith("cosmos/tile/")), "Production Cosmos second wave showed unshifted tiles.");
        controller.Seek(6.1); Check(Aoes().Count(a => AoeId(a).StartsWith("cosmos/tile/")) > 8 && Aoes().Any(a => AoeId(a) == "cosmos/wind/30"), "Production Cosmos shifted tiles/wind missing.");
        controller.Seek(20.2); Check(previews().Length == 0 && dangers() == 0, "Production Cosmos tile reset/wind finish leaked.");
        controller.Seek(.3); Check(Seed() != null, "Production Cosmos seed seek failed."); stop();

        c = new(0, 0, -340);
        List<string> frames =
        [
            Header(884), Frame(884, 0, [Actor(1, 0x2C13, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8),
                Actor(102, 0, c + Vector3.UnitZ * 8), Actor(103, 0, c - Vector3.UnitX * 8),
                Actor(20, 0x2C16, c + new Vector3(12, 0, 12), hitboxRadius: 1.5f)], full: true, party: party),
            ResearchIconEvent(.1, 100, 25), EffectEvent(4.2, 50, 18269, 1),
            Frame(884, 5, [Actor(100, 0, c, stackedStatus: 2136, statusExpiresAt: Start.AddSeconds(15))], party: party),
            Frame(884, 7, [Actor(100, 0, c)], party: party),
        ];
        for (var i = 0; i < 4; ++i) frames.Add(ResearchIconEvent(8 + i * .1, (ulong)(100 + i), (uint)(53 - i)));
        for (var i = 0; i < 4; ++i) frames.Add(EffectEvent(10 + i, 1, i % 2 == 0 ? 18271u : 18272u, (uint)(10 + i)));
        frames.Add(Frame(884, 14, [Actor(1, 0x2C13, c, combat: true, cast: Cast(18276, 5, c, target: 100, startedAt: 14))], party: party));
        frames.Add(Frame(884, 19.1, [Actor(1, 0x2C13, c, combat: true)], party: party));
        frames.Add(EffectEvent(20, 1, 18276, 30, target: 100));
        frames.Add(Frame(884, 21, [Actor(100, 0, c + Vector3.UnitX * 18),
            Actor(1, 0x2C13, c, combat: true, tethers: [new { Id = 57, TargetId = 100UL }])], party: party));
        frames.Add(Frame(884, 22, [Actor(1, 0x2C13, c, combat: true, tethers: [new { Id = 1, TargetId = 100UL }])], party: party));
        frames.Add(Frame(884, 23, [Actor(1, 0x2C13, c, combat: true)], party: party));
        load(id, frames);
        controller.Seek(.2); Check(Aoes().Any(a => AoeId(a) == "cosmos/black/100/cross/avoid/20") && previews().Length == 3, "Production Cosmos black flame/furniture protection missing.");
        Check(Aoes().Any(a => AoeId(a) == "cosmos/black/100/party-cross/101"), "Production Cosmos black cross party avoidance missing.");
        controller.Seek(4.3); Check(previews().Length == 0 && dangers() == 0, "Production Cosmos black flame effect leaked.");
        controller.Seek(5.1); Check(Aoes().Any(a => AoeId(a) == "cosmos/flame/enter") && previews().Length == 1, "Production Cosmos forced furniture transfer missing.");
        controller.Seek(7.1); Check(previews().Length == 0 && dangers() == 0, "Production Cosmos flame status removal leaked.");
        controller.Seek(8.4); Check(Aoes().Count(a => AoeId(a).StartsWith("cosmos/charge/") && AoeId(a).EndsWith("/cone")) == 4, "Production Cosmos four charge FIFO missing.");
        Check(Aoes().Any(a => AoeId(a) == "cosmos/charge/0/line-avoid/101"), "Production Cosmos charge path party avoidance missing.");
        for (var i = 0; i < 4; ++i)
        {
            controller.Seek(10.1 + i);
            Check(Aoes().Count(a => AoeId(a).StartsWith("cosmos/charge/") && AoeId(a).EndsWith("/cone")) == 3 - i, "Production Cosmos real charge effects not consumed.");
        }
        controller.Seek(19.2); Check(((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Count() == 1,
            "Production Cosmos delayed CaptiveBolt ended with cast.");
        controller.Seek(20.1); Check(!((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Any(), "Production Cosmos CaptiveBolt effect leaked.");
        controller.Seek(21.1); Check(previews().Length == 1 && Aoes().Any(a => AoeId(a) == "cosmos/tether/1"), "Production Cosmos tether not drawn.");
        controller.Seek(22.1); Check((bool)Aoes().Single(a => AoeId(a) == "cosmos/tether/1").GetType().GetProperty("Safe")!.GetValue(Aoes().Single(a => AoeId(a) == "cosmos/tether/1"))!, "Production Cosmos long tether not safe.");
        controller.Seek(23.1); Check(previews().Length == 0 && dangers() == 0, "Production Cosmos tether removal leaked.");
        controller.Seek(.2); Check(previews().Length == 3, "Production Cosmos Lugus seek failed."); stop();
        Console.WriteLine("PASS native Cosmos three bosses, persistent pool handoff, moving broom windows, mandatory stack, seed pickup/carry/drop replay isolation, shifted tiles and wind, black flame furniture protection, mortal flame soak, four charges and delayed buster, seek and stop");
    }
}
