using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureMolterRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(0, 5, -372);
    private static readonly Vector3 RoomCenter = new(0, 5, 377);
    private static DungeonActor Boss => new(1, 0x4306, Center, InCombat: true);
    private static DungeonActor Player => new(100, 0, Center, PartySlot: 0, Player: true, Hostile: false);
    private static DungeonEncounter Create()
    {
        var e = new DungeonEncounter(1209) { TreasurePartyInput = [100, 101] };
        e.Synchronize([Boss, Player, Player with { Id = 101, PartySlot = 1, Position = Center + Vector3.UnitX * 8 },
            new(2, 0x233C, Center), new(3, 0x233C, Center)], Center, Start, playerId: 100);
        return e;
    }
    private static DungeonCastObservation Cast(uint action, double time = 0, ulong target = 100, double duration = 5)
        => new(action, Center, .37f, Start.AddSeconds(time), Start.AddSeconds(time + duration), target);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure Molter19 Room1 full18 rules source active and inactive boundary", Rules);
        run("native treasure Molter Crypsis dynamic9 real Start any Lose and Sprint diagnostic", Crypsis);
        run("native treasure Molter Crypsis raw occupied expiry true Lose and duplicate event receipt", ConcealedRaw);
        run("native treasure Molter current Lose cannot consume prior cycle raw receipt", CurrentLose);
        run("native treasure Molter destroyed primary retained for Crypsis until reward phase ends", DestroyedPrimary);
        run("native treasure Molter Crypsis interleaves actor cast before its status transitions", ActorOrder);
        run("native treasure Molter pool quantized creation all circles ushort4 global FIFO and no expiry", Pools);
        run("native treasure Molter AetherialLight insertion first4 danger first2 hidden promotion", Lights);
        run("native treasure Molter spread6 and7 dynamic single raidwide forecasts", Party);
        run("native treasure Cenote Room1 native state lifetime and network estate separation", RoomLifetime);
        run("native treasure Cenote Room1 charge dynamic3 original references finish only and teammate angle", RoomCharge);
        run("native treasure Molter reward life main dead marker and exact priority", Rewards);
    }
    private static void Rules()
    {
        var e = Create();
        var rules = e.Active!.Casts.ToDictionary(c => c.Action);
        Check(rules.Count == 19 && NativeTreasureCatalog.Owns(0x4306, 1209)
            && !NativeTreasureCatalog.Owns(0x4306, 1123)
            && rules[38267].Shape is FanTemplate { Radius: 40, AngleRadians: MathF.PI }
            && rules[38273].Shape is CircleTemplate { Radius: 5 }
            && rules[38271].Shape is FanTemplate { Radius: 40 } light && light.AngleRadians == MathF.PI / 3
            && new uint[] { 38335, 38336, 38337, 38277 }.All(a => rules[a].Shape is CircleTemplate { Radius: 6 })
            && new uint[] { 38268, 38280, 38272, 38276 }.All(a => !rules.ContainsKey(a)), "Molter source rules differ.");
        var room = NativeCenoteRoomCatalog.Create().Single(d => d.BossOid == 0x1EBAA4).Casts.ToDictionary(c => c.Action);
        Check(room.Count == 18 && room[38291].Shape is FanTemplate { Radius: 10, AngleRadians: MathF.PI / 2 }
            && room[38292].Shape is FanTemplate { Radius: 12 } hush && hush.AngleRadians == 2 * MathF.PI / 3
            && room[38290].Shape is RayRectangleTemplate { Length: 14, MinLocalX: -2, MaxLocalX: 2 }
            && room[38287].Shape is RayRectangleTemplate { Length: 20, MinLocalX: -2, MaxLocalX: 2 }
            && room[38289].Shape is RayRectangleTemplate { Length: 10, MinLocalX: -2, MaxLocalX: 2 }
            && room[38283].Shape is CircleTemplate { Radius: 5 }
            && room[38281].Shape == null && !room.ContainsKey(38282) && !room.ContainsKey(38280)
            && NativeCenoteRoomCatalog.Trash(0x1EBAA4).Length == 12, "Room1 rules/charge or pull list differ.");
    }
    private static void Crypsis()
    {
        var e = Create();
        e.StatusChanged(3, 3997, true, Start, expiresAt: Start.AddSeconds(1));
        Check(e.TreasureSprintIntent == null, "Gain itself concealed boss.");
        e.StartCast(2, Cast(38265), Start);
        Check(e.TreasureSprintIntent == (3u, 4000, 100ul), "Source Sprint intent lost or expanded execution.");
        e.Finish(2, 38265);
        e.Tick(Start.AddSeconds(100));
        Check(e.TreasureSprintIntent != null, "Finish/expected timer revealed boss.");
        var aoes = e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/crypsis/")).ToArray();
        Check(aoes.Single(a => a.Display) is { Shape: CircleTemplate { Radius: 9 }, OutlineOnly: true, Navigate: false }
            && aoes.Single(a => a.Display).Color == new Vector4(0, 1, 0, 1)
            && !aoes.Single(a => a.Navigate).Shape.Resolve(e.Arena!.Position, 0, null).Contains(Center, 0)
            && aoes.Single(a => a.Navigate).Shape.Resolve(e.Arena.Position, 0, null).Contains(Center + Vector3.UnitX * 10, 0),
            "Crypsis display/enter9 constraint differs.");
        e.StatusChanged(100, 3997, false, Start.AddSeconds(101));
        Check(e.TreasureSprintIntent == null, "Any actor Lose without prior Gain failed to reveal.");
    }
    private static void ConcealedRaw()
    {
        var e = Create();
        e.StatusChanged(1, 3997, false, Start.AddSeconds(-1));
        var occupied = Boss with { StatusExpirations = new Dictionary<uint, DateTime> { [3997] = Start.AddSeconds(1) } };
        e.Synchronize([occupied, Player, new(2, 0x233C, Center)], Center, Start);
        e.StartCast(2, Cast(38265), Start);
        e.Synchronize([occupied, Player, new(2, 0x233C, Center)], Center, Start.AddSeconds(20));
        Check(e.TreasureSprintIntent != null, "Expired occupied raw slot emitted Lose.");
        e.Synchronize([Boss, Player, new(2, 0x233C, Center)], Center, Start.AddSeconds(21));
        Check(e.TreasureSprintIntent == null, "True raw Lose retained concealment.");
        e.StartCast(2, Cast(38266, 22), Start.AddSeconds(22));
        e.StatusChanged(1, 3997, false, Start.AddSeconds(20.5));
        Check(e.TreasureSprintIntent != null, "Delayed event duplicate of consumed raw Lose cleared a newer Crypsis.");
    }
    private static void ActorOrder()
    {
        foreach (var reversed in new[] { false, true })
        {
            var e = Create();
            var occupied = Boss with { Statuses = [3997] };
            e.Synchronize([occupied, new(2, 0x233C, Center), Player], Center, Start);
            var caster = new DungeonActor(2, 0x233C, Center, Cast: Cast(38265, 1));
            e.Synchronize(reversed ? [caster, Boss, Player] : [Boss, caster, Player], Center, Start.AddSeconds(1));
            Check((e.TreasureSprintIntent != null) == !reversed, "Raw actor ordering did not interleave Cast then Status.");
        }
    }
    private static void CurrentLose()
    {
        foreach (var eventFirst in new[] { false, true })
        {
            var e = Create();
            var occupied = Boss with { Statuses = [3997] };
            e.Synchronize([occupied, Player], Center, Start);
            if (eventFirst) e.StatusChanged(1, 3997, true, Start, expiresAt: DateTime.MaxValue);
            e.Synchronize([Boss, Player], Center, Start.AddSeconds(1));
            e.StartCast(1, Cast(38265, 2), Start.AddSeconds(2));
            if (eventFirst) e.StatusChanged(1, 3997, true, Start.AddSeconds(3), expiresAt: DateTime.MaxValue);
            else e.Synchronize([occupied, Player], Center, Start.AddSeconds(3));
            e.StatusChanged(1, 3997, false, Start.AddSeconds(4));
            Check(e.TreasureSprintIntent == null, "Current Lose consumed a previous cycle's raw receipt.");
        }
    }
    private static void DestroyedPrimary()
    {
        var e = Create();
        var bonus = new DungeonActor(5, 0x42FE, Center);
        e.Synchronize([Boss, Player, bonus], Center, Start);
        e.StartCast(1, Cast(38265), Start);
        e.RemoveActor(1, Start.AddSeconds(1));
        e.Synchronize([Player, bonus], Center, Start.AddSeconds(1));
        Check(e.Active != null && e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/crypsis/")) == 2,
            "Destroyed primary lost Crypsis output while rewards retained phase.");
        e.Synchronize([Boss with { Position = Center + Vector3.UnitX * 20 }, Player, bonus], Center, Start.AddSeconds(2));
        Check(e.AmbientAoes().Single(a => a.Id == "treasure/crypsis/display").Position == Center,
            "Same-ID new actor rebound the destroyed primary reference.");
    }
    private static void Pools()
    {
        var e = Create();
        for (var i = 0; i < 5; ++i)
            e.ObjectCreated((ulong)(10 + i), Start, 0x1EBCB8, Center + new Vector3(i + .13f, 0, .17f));
        Check(e.TreasureCreatedCircles.Count == 5 && e.TreasureCreatedCircles.All(c => c.Radius == 17 && c.Activation == default),
            "Pool first3 limitation or fabricated activation.");
        var first = e.TreasureCreatedCircles[0];
        e.EventState(3, 0x0104);
        e.EventState(999, 4);
        e.RemoveActor(10);
        e.Tick(Start.AddSeconds(100));
        Check(e.TreasureCreatedCircles.Count == 5 && first.Position != Center + new Vector3(.13f, 0, .17f),
            "Pool lost quantized creation lock or wrong state/Destroy/expiry cleared it.");
        e.EventState(3, 4);
        Check(e.TreasureCreatedCircles.Count == 4 && e.TreasureCreatedCircles[0].Source == 11,
            "Any known actor ushort4 failed global first deletion.");
        e.EventState(3, 4);
        Check(e.TreasureCreatedCircles.Count == 3 && e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/created/")) == 3,
            "Independent repeat4 did not consume another pool.");
    }
    private static void Lights()
    {
        var e = Create();
        e.Synchronize([Boss, Player, .. Enumerable.Range(2, 5).Select(i => new DungeonActor((ulong)i, 0x233C, Center))], Center, Start);
        for (var i = 2; i <= 6; ++i) e.StartCast((ulong)i, Cast(38271, duration: 12 - i), Start);
        var lights = e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/light/")).ToArray();
        Check(lights.Length == 4 && lights.All(a => a.Navigate && a.Display)
            && lights.Select(a => a.Id).SequenceEqual(new[] { 2, 3, 4, 5 }.Select(i => $"treasure/light/{i}"))
            && lights.Take(2).All(a => a.Color == new Vector4(1, 1, 0, 1))
            && lights.Skip(2).All(a => a.Color == null)
            && !e.OutputCasts.Any(c => c.Value.Rule.Action == 38271), "First4/first2 yellow or risk insertion order differs.");
        e.Finish(2, 38271);
        Check(e.AmbientAoes().Any(a => a.Id == "treasure/light/6"), "Hidden light failed FIFO promotion.");
    }
    private static void Party()
    {
        var e = Create();
        e.StartCast(2, Cast(38274), Start);
        e.StartCast(3, Cast(38275), Start);
        Check(e.PredictedDamage.Count() == 3, "Single/raidwide source predictions missing.");
        e.Finish(2, 38274);
        e.StartCast(2, Cast(38580, 10), Start.AddSeconds(10));
        var spread = e.AmbientAoes().Where(a => a.Id.StartsWith("quest/spread/")).ToArray();
        Check(spread.Single(a => a.Display).Shape is CircleTemplate { Radius: 6 }
            && spread.Single(a => a.Navigate).Shape is CircleTemplate { Radius: 7 }, "Molter spread6/7 differs.");
    }
    private static DungeonEncounter Room()
    {
        var e = new DungeonEncounter(1209) { TreasurePartyInput = [100, 101] };
        e.Synchronize([new(1, 0x1EBAA4, RoomCenter), new(2, 0x4307, RoomCenter, Dead: true, Targetable: false),
            Player with { Position = RoomCenter }, Player with { Id = 101, Position = RoomCenter + Vector3.UnitX * 10 }],
            RoomCenter, Start, playerId: 100);
        return e;
    }
    private static void RoomLifetime()
    {
        var e = Room();
        Check(e.Active?.BossOid == 0x1EBAA4, "Dead untargetable trash did not pull room1.");
        e.EventState(1, 7);
        e.Synchronize(e.Actors.Values.Where(a => a.Id != 2).ToArray(), RoomCenter, Start.AddSeconds(1));
        Check(e.Active != null, "Network EState7 or empty trash ended native room.");
        e.Synchronize(e.Actors.Values.Select(a => a.Id == 1 ? a with { Dead = true } : a).ToArray(), RoomCenter, Start.AddSeconds(2));
        Check(e.Active != null, "Room primary death ended module.");
        e.Synchronize(e.Actors.Values.Select(a => a.Id == 1 ? a with { EventState = 7 } : a).ToArray(), RoomCenter, Start.AddSeconds(3));
        Check(e.Active == null, "Native EventState7 failed room completion.");
    }
    private static void RoomCharge()
    {
        var e = Room();
        var actors = e.Actors.Values.Select(a => a.Id == 2 ? a with { Dead = false, Targetable = true } : a).ToArray();
        e.Synchronize(actors, RoomCenter, Start);
        e.StartCast(2, Cast(38281) with { Location = RoomCenter }, Start);
        e.Synchronize(actors.Select(a => a.Id == 100 ? a with { Position = RoomCenter + Vector3.UnitZ * 8, Dead = true } : a),
            RoomCenter, Start.AddSeconds(.1), playerId: 100);
        var display = e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/cast-bait/") && a.Display);
        Check(display.Shape is RayRectangleTemplate { Length: 8, MinLocalX: -1.5f, MaxLocalX: 1.5f }
            && display.Rotation == 0 && display.OutlineOnly
            && !e.Hints(RoomCenter).Contains("Tankbuster cleave"), "Charge dynamic width/reference/death contract differs.");
        var partyCone = e.AmbientAoes().Single(a => a.Id.EndsWith("/party/101"));
        Check(partyCone.Shape is FanTemplate { Radius: 100 } fan && fan.AngleRadians == 2 * MathF.Asin(1.5f / 10),
            "Charge teammate angle did not use halfwidth1.5.");
        e.TreasureEffect(2, 38281, 1, 100);
        Check(e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/cast-bait/")), "Effect consumed Finish-only charge.");
        e.Finish(2, 38281);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/cast-bait/")), "True Finish failed charge removal.");
    }
    private static void Rewards()
    {
        var e = Create();
        e.Synchronize([Boss with { Dead = true, Targetable = false }, new(4, 0x42FE, Center), Player], Center, Start.AddSeconds(1));
        Check(e.Active != null && e.AmbientAoes().Any(a => a.Id == "treasure/enemy/1")
            && NativeTreasureCatalog.Priority(0x4306, 0x4300) == 6
            && NativeTreasureCatalog.Priority(0x4306, 0x42FE) == 2
            && NativeTreasureCatalog.Priority(0x4306, 0x42FF) == 1, "Molter dead primary marker/reward lifecycle/priority differs.");
        e.Synchronize([Boss with { Dead = true }], Center, Start.AddSeconds(2));
        Check(e.Active == null, "Dead source group retained encounter.");
    }
}
