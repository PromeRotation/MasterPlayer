using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.EurekaOrthos;
using MasterPlayer.Mechanics.DeepDungeon;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class EurekaFloorRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static ReplayActorState Player => new() { Id = 100, Kind = 1, Position = new(0, 0, 5), Rotation = MathF.PI };
    private static ReplayActorState Mob(ulong id = 1, ReplayCast? cast = null)
        => new() { Id = id, Oid = 0x3E13, Kind = 2, SubKind = 5, IsEnemy = true, Targetable = true, Cast = cast };
    private static ReplayCast Cast(uint action, double start = .1)
        => new(action, 100, Vector3.Zero, 0, Start.AddSeconds(start), Start.AddSeconds(start + 3.3), true, false)
            { TotalTime = 3, IsSpell = true };
    private static PalaceCast? Resolve(ReplayActorState actor)
        => actor.Cast is { } c ? new(actor.Id, c.ActionId, c.TargetId, c.Location, actor.Position, c.Rotation,
            c.StartedAt, c.FinishAt, c.ActionId == 32444 ? new DonutTemplate(3, 30) : null, "test", CastExtra: .2f) : null;
    private static void Sync(EurekaFloorEncounter model, double time, ReplayActorState[] actors, ReplayPartyMember[]? party = null)
        => model.SynchronizeFloor(actors, Start.AddSeconds(time), true, Resolve, null, null, party ?? [new(0, "", 100)]);
    private static EurekaFloorEncounter Fixture()
    {
        var model = new EurekaFloorEncounter { ZoneEnabled = true, Floor = 31, Territory = 1102 };
        Sync(model, 0, [Player, Mob()]);
        return model;
    }
    internal static void Run(Action<string, Action> run)
    {
        run("EO floor source walls traps and 99 floor exclusion", Tables);
        run("EO floor seven gazes four donuts range and actual raw ends", Gazes);
        run("EO floor stun interrupt floor threshold and circles escape goal", Intents);
        run("EO floor spikes actual Finish replacement destroy FIFO status loss timeout", Spikes);
        run("EO floor persistent native effects first-source FIFO dynamic pose generation", Effects);
        run("EO floor immunity original slot ordering expiry strictness loss and transference", Immunity);
        run("EO floor source LOS bitmap locked pixels all 24 maps independent runtime oracle", Los);
        run("EO floor cold callbacks hot start and asymmetric ClearState", Lifecycle);
        run("EO floor legacy packet targets complete effects two-way receipts and zero sequences", EffectInputs);
        run("EO floor packet-only rejection cannot manufacture Finish Spikes", PendingFinish);
        run("EO floor LOS interleaved expiry same-ID generation first output", LosGeneration);
    }

    private static void LosGeneration()
    {
        var m = new EurekaFloorEncounter { AutoLOS = true, Territory = 1101 };
        var mob = Mob() with { Position = new(300, 0, -300), ObjectIndex = 10 };
        Sync(m, 0, [Player, mob]);
        Sync(m, .1, [Player, mob with { Cast = Cast(32470) }]);
        Check(m.Collect(Start.AddSeconds(.2), Player).Any(a => a.Key.StartsWith("zone/los")), "Initial LOS absent.");
        Sync(m, 1, [Player, mob with { ObjectIndex = 11, Cast = Cast(32470, 1) }]);
        var first = m.Collect(Start.AddSeconds(1), Player).ToArray();
        Check(first.Any(a => a.Key.StartsWith("zone/los")),
            "Old generation expiry deleted new cache before its first output.");
        Check(!m.Collect(Start.AddSeconds(1.1), Player).Any(a => a.Key.StartsWith("zone/los")),
            "Source shared-ID cache removal order was not preserved.");
    }

    private static void PendingFinish()
    {
        foreach (var clear in new[] { false, true })
        {
            var m = Fixture();
            m.StartPacket(new(1, 33180, 100, Vector3.Zero, Vector3.Zero, 0,
                Start.AddSeconds(.1), Start.AddSeconds(3.4)));
            if (clear) m.ClearFloor();
            Sync(m, .2, [Player, Mob()]);
            Check(m.SpikedTargets(Start.AddSeconds(.2)).Length == 0,
                "Rejected packet staging manufactured actual Finish.");
            m.StartPacket(new(1, 32952, 100, Vector3.Zero, Vector3.Zero, 0,
                Start.AddSeconds(1), Start.AddSeconds(4.3)));
            Sync(m, 1.1, [Player, Mob(cast: Cast(123, 1))]);
            Check(m.SpikedTargets(Start.AddSeconds(1.1)).Length == 0,
                "Unconfirmed replacement manufactured actual Finish.");
            Sync(m, 2, [Player, Mob(cast: Cast(33180, 2))]);
            Sync(m, 3, [Player, Mob()]);
            Check(m.SpikedTargets(Start.AddSeconds(3)).Length == 1,
                "Observed cast Finish failed after pending rejection.");
        }
    }

    private static void EffectInputs()
    {
        var input = new DeepDungeonEffectInput();
        Check(input.Legacy(1, 33178, 7, Start) && !input.Legacy(1, 33178, 7, Start),
            "Legacy expanded targets produced duplicate callbacks.");
        Check(!input.Native(1, 33178, 7, Start.AddTicks(1)), "Legacy-first dual capture counted twice.");
        Check(input.Native(1, 33178, 8, Start) && !input.Legacy(1, 33178, 8, Start.AddTicks(1)),
            "Native-first dual capture counted twice.");
        Check(input.Native(1, 33178, 0, Start) && input.Native(1, 33178, 0, Start),
            "Complete identical zero sequence packets were deduplicated.");
        input.Reset();
        Check(input.Legacy(1, 33178, 0, Start) && !input.Legacy(1, 33178, 0, Start)
            && input.Legacy(1, 33178, 0, Start.AddTicks(1)), "Legacy zero sequence packet timestamp lost.");
        input.Reset();
        Check(input.Native(1, 33178, 0, Start) && !input.Legacy(1, 33178, 0, Start),
            "Exactly identified dual zero packet counted twice.");
    }

    private static void Tables()
    {
        Check(EurekaFloorData.Rooms.Values.Sum(v => v.Length) == 500 && EurekaFloorData.Traps.Values.Sum(v => v.Length) == 3188,
            "EO source tables incomplete.");
        foreach (var ((set, tileset), rooms) in EurekaFloorData.Rooms)
        {
            var source = BossMod.Global.DeepDungeon.LoadedFloors.Walls[$"3.{set}"];
            var expected = tileset == 0 ? source.RoomsA : source.RoomsB;
            for (var i = 0; i < 25; ++i)
            {
                var actual = new[] { rooms[i].North, rooms[i].South, rooms[i].West, rooms[i].East };
                var original = new[] { expected[i].North, expected[i].South, expected[i].West, expected[i].East };
                Check(rooms[i].Center == new Vector2(expected[i].Center.Position.X, expected[i].Center.Position.Z), "Room center changed.");
                for (var d = 0; d < 4; ++d)
                    Check(actual[d].Depth == original[d].Depth && actual[d].Position == new Vector2(original[d].Position.X, original[d].Position.Z),
                        "Wall source parameter changed.");
            }
        }
        foreach (var (territory, traps) in EurekaFloorData.Traps)
            Check(traps.SequenceEqual(BossMod.Global.DeepDungeon.GeneratedTrapData.Traps[territory].Select(p => new Vector2(p.X, p.Z))),
                "Trap sequence changed.");
        var floor = new PalaceFloorHazards(3, 100, EurekaFloorData.Rooms, EurekaFloorData.Traps);
        var map = new ReplayDeepDungeonState { DungeonId = 3, Floor = 99, Rooms = Enumerable.Repeat((ushort)128, 25).ToArray() };
        floor.Synchronize(map, [], 1108);
        Check(!floor.HasMap && !floor.Collect(Start, Player).Any(), "99 Boss floor received trash walls/traps.");
    }

    private static void Gazes()
    {
        foreach (var (action, radius) in new (uint, float)[] { (32437, 40), (32667, 40), (32896, 30),
            (32737, 20), (32762, 33), (32761, 33), (32731, 5) })
        {
            var model = Fixture();
            Sync(model, .1, [Player, Mob(cast: Cast(action))]);
            Check(model.Eyes(Start.AddSeconds(1), Player with { Position = new(radius, 0, 0) }).Any(), "Gaze radius shrank.");
            Check(!model.Eyes(Start.AddSeconds(1), Player with { Position = new(radius + .01f, 0, 0) }).Any(), "Gaze radius grew.");
            Sync(model, 2, [Player, Mob(cast: Cast(action)) with { Position = new(4, 0, 0), Dead = true }]);
            Check(model.Eyes(Start.AddSeconds(5), Player with { Position = new(4, 0, 1) }).Single().Origin.X == 4, "Death ended or source pose locked.");
            Sync(model, 6, [Player, Mob()]);
            Check(!model.Eyes(Start.AddSeconds(6), Player).Any(), "Gaze outlived actual raw end.");
        }
        foreach (var (action, radius) in new (uint, float)[] { (32444, 30), (32910, 30), (32979, 44), (33186, 60) })
        {
            var model = Fixture();
            Sync(model, .1, [Player, Mob(cast: Cast(action))]);
            var area = model.Collect(Start.AddSeconds(1), Player).Single(a => a.Key.StartsWith("zone/donut"));
            Check(area.Shape is DonutTemplate { InnerRadius: 8 } d && d.OuterRadius == radius, "Wrong special donut.");
            if (action == 32444) Check(model.Collect(Start.AddSeconds(1), Player).Count(a => a.Shape is DonutTemplate) == 2,
                "Source0 ordinary donut incorrectly suppressed.");
        }
    }

    private static void Intents()
    {
        foreach (var floor in new[] { 59, 60 })
        {
            var model = Fixture();
            model.Floor = floor;
            Sync(model, .1, [Player, Mob(cast: Cast(32798))]);
            var hints = model.Hints(Start.AddSeconds(1), Player).ToArray();
            Check(hints.Any(h => h.StartsWith("打断")) && hints.Any(h => h.StartsWith("眩晕")) == (floor < 60),
                "Infatuation floor threshold changed.");
        }
        var m = Fixture();
        Sync(m, .1, [Player, Mob(cast: Cast(32732))]);
        var areas = m.Collect(Start.AddSeconds(1), Player).ToArray();
        Check(areas.Any(a => a.Shape is CircleTemplate { Radius: 40 }) && areas.Single(a => a.Safe) is
            { Priority: 100, NavigationOnly: true, OverrideDangerMargin: false, Shape: DonutTemplate { InnerRadius: 15 } },
            "Catharsis escape weight/distance lost.");
        Sync(m, 1.1, [Player, Mob(cast: Cast(123))]);
        Check(m.Collect(Start.AddSeconds(2), Player).Any(a => a.Key.StartsWith("zone/circle")), "Non-null replacement prematurely pruned circle.");
    }

    private static void Spikes()
    {
        var model = Fixture();
        Sync(model, .1, [Player, Mob(cast: Cast(33180))]);
        Check(model.SpikedTargets(Start.AddSeconds(2)).Length == 0, "Spikes began at Start.");
        var status = new ReplayStatus(198, 0, 1, Start.AddSeconds(20)) { SlotIndex = 4 };
        Sync(model, 4, [Player, Mob(cast: Cast(32952, 4)) with { Statuses = [status] }]);
        Check(model.SpikedTargets(Start.AddSeconds(4)).Length == 1, "Replacement failed to Finish.");
        Sync(model, 7, [Player, Mob() with { Statuses = [status] }]);
        Check(model.SpikedTargets(Start.AddSeconds(7)).Length == 2, "Independent finish entries overwritten.");
        Sync(model, 8, [Player, Mob()]);
        Check(model.SpikedTargets(Start.AddSeconds(8)).Length == 1, "Lose did not remove only first original source.");
        Check(model.SpikedTargets(Start.AddSeconds(17)).Length == 0, "Timeout equality did not expire.");
        Sync(model, 18, [Player, Mob(cast: Cast(33180, 18))]);
        Sync(model, 19, [Player]);
        Sync(model, 20, [Player, Mob()]);
        Check(model.SpikedTargets(Start.AddSeconds(20)).Length == 0, "Old source spikes rebound same-ID birth.");
    }

    private static void Effects()
    {
        var m = Fixture();
        m.Effect(1, 33178);
        m.Effect(1, 33195);
        var a = m.Collect(Start, Player).Where(x => x.Key.StartsWith("zone/void")).ToArray();
        Check(a.Length == 2 && a.Any(x => x.Shape is FanTemplate { Radius: 7 }) && a.Any(x => x.Shape is CircleTemplate { Radius: 52 }),
            "Independent persistent native effects lost.");
        m.Effect(1, 32412);
        Check(m.Collect(Start, Player).Single(x => x.Key.StartsWith("zone/void")).Shape is CircleTemplate { Radius: 52 },
            "Native removal paired action instead of source FIFO.");
        Sync(m, 1, [Player, Mob() with { Position = new(5, 0, 6), Rotation = 1 }]);
        var area = m.Collect(Start.AddSeconds(1), Player).Single(x => x.Key.StartsWith("zone/void"));
        Check(area.Rotation == 1 && area.Position.X != 0, "Persistent voidzone did not follow source.");
        m.ClearFloor();
        Check(m.Collect(Start.AddSeconds(2), Player).Any(x => x.Key.StartsWith("zone/void")), "ClearState erased persistent voidzones.");
        Sync(m, 3, [Player, Mob() with { Dead = true }]);
        Check(!m.Collect(Start.AddSeconds(3), Player).Any(x => x.Key.StartsWith("zone/void")), "Death failed to expire voidzone.");
        Sync(m, 4, [Player, Mob()]);
        Check(!m.Collect(Start.AddSeconds(4), Player).Any(x => x.Key.StartsWith("zone/void")), "Revive resurrected expired voidzone.");
        m.Effect(1, 32491);
        Check(m.Collect(Start.AddSeconds(4), Player).Single(x => x.Key.StartsWith("zone/void")).Rotation == MathF.PI,
            "Rear cone offset missing.");
    }

    private static void Immunity()
    {
        var m = Fixture();
        var c = Cast(32413);
        var buff = new ReplayStatus(160, 0, 100, c.FinishAt.AddSeconds(1)) { SlotIndex = 4 };
        Sync(m, .1, [Player with { Statuses = [buff] }, Mob(cast: c)]);
        Check(!m.Collect(Start.AddSeconds(1), Player).Any(a => a.Key.StartsWith("zone/knockback")), "Role immunity lost.");
        Sync(m, 1.1, [Player, Mob(cast: c)]);
        Check(!m.Collect(Start.AddSeconds(2), Player).Any(a => a.Key.StartsWith("zone/knockback")), "Lose incorrectly cleared cached immunity.");
        m.ClearFloor();
        Check(m.Collect(Start.AddSeconds(2), Player).Any(a => a.Key.StartsWith("zone/knockback")), "ClearState erased KB or retained immunity.");
        var p = Player with { Statuses = [buff with { Id = 1512 }] };
        Sync(m, 2.1, [p, Mob(cast: c)], [new(0, "", 200)]);
        Check(!m.Collect(Start.AddSeconds(2.2), Player with { Id = 200 }).Any(a => a.Key.StartsWith("zone/knockback")),
            "Gain did not use old Party slot.");
        m.ClearFloor();
        Sync(m, 2.3, [p, Mob(cast: c)], [new(0, "", 100)]);
        Check(m.Collect(Start.AddSeconds(2.4), Player).Any(a => a.Key.StartsWith("zone/knockback")), "ClearState replayed unchanged penalty.");
    }

    private static void Los()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../.."));
        while (!Directory.Exists(Path.Combine(root, "MechanicRuntime/Upstream")))
            root = Directory.GetParent(root)?.FullName ?? throw new InvalidOperationException("Source root absent.");
        var directory = Path.Combine(root, "MechanicRuntime/Upstream/Pathfinding/ObstacleMaps");
        using var list = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "maplist.json")));
        var blocked = typeof(BossMod.Global.DeepDungeon.AutoClear).GetMethod("IsBlocked", BindingFlags.NonPublic | BindingFlags.Static)!;
        var maps = 0;
        var points = 0;
        foreach (var group in list.RootElement.EnumerateObject().Where(g => EurekaFloorEncounter.Territories.Contains(uint.Parse(g.Name.Split('.')[0]))))
        foreach (var entry in group.Value.EnumerateArray())
        {
            float F(string name) => entry.GetProperty(name).GetSingle();
            var origin = new BossMod.WPos(F("OriginX"), F("OriginZ"));
            var caster = new Vector3((F("MinBoundsX") + F("MaxBoundsX")) / 2, (F("MinBoundsY") + F("MaxBoundsY")) / 2,
                (F("MinBoundsZ") + F("MaxBoundsZ")) / 2);
            using var stream = File.OpenRead(Path.Combine(directory, entry.GetProperty("Filename").GetString()!));
            var source = new BossMod.Bitmap(stream);
            var worldRange = MathF.Min(30, Math.Min(source.Width, source.Height) * source.PixelSize * .24f);
            var actual = EurekaLosMap.Create(uint.Parse(group.Name.Split('.')[0]), caster, worldRange)!;
            var map = new BossMod.Bitmap(source.Width, source.Height, source.Color0, source.Color1, source.Resolution);
            var cell = (new BossMod.WPos(caster.X, caster.Z) - origin) / source.PixelSize;
            var cc = new Vector2((int)cell.X, (int)cell.Z);
            var range = (int)(worldRange / source.PixelSize);
            for (var x = (int)cc.X - range; x <= (int)cc.X + range; ++x)
            for (var z = (int)cc.Y - range; z <= (int)cc.Y + range; ++z)
                if (!(bool)blocked.Invoke(null, [source, new Vector2(x, z), cc, (float)range])!)
                    map[x, z] = true;
            var distance = new BossMod.SDDeepDungeonLOS(map, origin);
            var adapted = actual.Areas("oracle", Start, 0).Select(a => a.Geometry).ToArray();
            for (var x = (int)cc.X - range - 2; x <= (int)cc.X + range + 2; ++x)
            for (var z = (int)cc.Y - range - 2; z <= (int)cc.Y + range + 2; ++z)
            {
                var point = new Vector3(origin.X + (x + .37f) * source.PixelSize, 0, origin.Z + (z + .61f) * source.PixelSize);
                var expected = distance.Contains(new(point.X, point.Z));
                Check(actual.Contains(point) == expected && adapted.Any(a => a.Contains(point)) == expected, "LOS source pixels or adapter lost visibility.");
                ++points;
            }
            ++maps;
        }
        Check(maps == 24 && points > 200000, "LOS oracle coverage unexpectedly small.");
        Console.WriteLine($"INFO EO LOS source oracle: {maps} maps, {points} pixel-interior classifications and production rectangle unions.");
        var sample = EurekaLosMap.Create(1101, new(300, 0, -300), 30)!;
        Check(sample.Contains(new(280, 0, -300)) && !sample.Contains(new(281, 0, -305)), "Known real wall shadow changed.");
        Check(EurekaLosMap.Create(1101, new(300, 200, -300), 30) == null, "XYZ map selection ignored height.");
    }

    private static void Lifecycle()
    {
        var m = new EurekaFloorEncounter { AutoLOS = true, Territory = 1101, ZoneEnabled = true };
        Sync(m, 1, [Player, Mob(cast: Cast(32470))]);
        Check(!m.Collect(Start.AddSeconds(1), Player).Any(a => a.Key.StartsWith("zone/")), "Cold Zone replayed historical callbacks.");
        Sync(m, 2, [Player, Mob()]);
        Sync(m, 3, [Player, Mob(cast: Cast(32470, 3))]);
        Check(m.LosDiagnostic != null && !m.Collect(Start.AddSeconds(3), Player).Any(a => a.Shape is CircleTemplate { Radius: 30 }),
            "Missing LOS map silently fell back to circle.");
        m.Reset();
        Check(m.SpikedTargets(Start).Length == 0 && !m.Collect(Start, Player).Any(), "Reset retained owner state.");
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
