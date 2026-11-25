using System.Numerics;
using MasterPlayer.Mechanics.HeavenOnHigh;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class HeavenOnHighFloorRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static ReplayActorState Player => new() { Id = 100, Kind = 1, Position = new(0, 0, 5), Rotation = MathF.PI };
    private static ReplayActorState Mob(ulong id = 1, ReplayCast? cast = null)
        => new() { Id = id, Oid = 0x22AF, Kind = 2, SubKind = 5, IsEnemy = true, Cast = cast };
    private static ReplayCast Cast(uint action, double start = .1, Vector3? position = null, ulong target = 100)
        => new(action, target, position ?? Vector3.Zero, 0, Start.AddSeconds(start),
            Start.AddSeconds(start + 3.3), true, false) { TotalTime = 3, IsSpell = true };
    private static PalaceCast? Resolve(ReplayActorState actor)
    {
        if (actor.Cast is not { } cast) return null;
        var shape = cast.ActionId switch
        {
            12415 => new DonutTemplate(3, 41) as GeometryTemplate,
            90000 => new CircleTemplate(6),
            90001 => new CircleTemplate(20),
            90002 => RayRectangleTemplate.Symmetric(4, 0),
            _ => null,
        };
        return new(actor.Id, cast.ActionId, cast.TargetId, cast.Location, actor.Position, cast.Rotation,
            cast.StartedAt, cast.FinishAt, shape, $"test {cast.ActionId}", Charge: cast.ActionId == 90002,
            CastExtra: .2f, GenericGaze: cast.ActionId == 90001);
    }
    private static HeavenOnHighFloorEncounter Fixture()
    {
        var model = new HeavenOnHighFloorEncounter();
        model.Synchronize([Player, Mob()], Start, true, Resolve);
        return model;
    }
    internal static void Run(Action<string, Action> run)
    {
        run("HoH floor all source walls traps and signed rectangles independent oracle", Tables);
        run("HoH floor seven gaze ranges current pose NPC cap and real raw end", Gazes);
        run("HoH floor cold Zone baseline delayed packet and same-ID generation", Cold);
        run("HoH floor independent non-null replacements donuts HintDisabled and hidden lists", Replacements);
        run("HoH floor generic gaze old target reference dynamic charge exact quantization", Generic);
        run("HoH floor true Created trap ignores no cold replay no flow targets", Traps);
        run("HoH floor interrupt stun intents no invented reflection or automatic actions", Intents);
        run("HoH floor joint facing switch death repeated snapshots owner model reset", Facing);
        run("HoH floor nullable packet raw initialization late first cap and hot floor clear", InputEdges);
        run("HoH floor packet ClearState first raw and retired generation late packet", PacketTransitions);
        run("HoH floor hot indicator before consecutive null maps preserves birth position", MissingMap);
    }

    private static void Tables()
    {
        Check(HeavenOnHighFloorEncounter.Territories.Length == 10
            && HeavenOnHighFloorData.Rooms.Count == 20 && HeavenOnHighFloorData.Traps.Count == 10, "Missing floor sets.");
        foreach (var ((set, tileset), rooms) in HeavenOnHighFloorData.Rooms)
        {
            var data = BossMod.Global.DeepDungeon.LoadedFloors.Walls[$"2.{set}"];
            var source = tileset == 0 ? data.RoomsA : data.RoomsB;
            var floor = new PalaceFloorHazards(2, 100, HeavenOnHighFloorData.Rooms, HeavenOnHighFloorData.Traps);
            var snapshot = new ReplayDeepDungeonState
            {
                DungeonId = 2, Floor = (byte)(set * 10 - 9), Tileset = tileset, Rooms = Enumerable.Repeat((ushort)0x80, 25).ToArray(),
            };
            floor.Synchronize(snapshot, [], (ushort)HeavenOnHighFloorEncounter.Territories[set - 1]);
            var areas = floor.Collect(Start, Player, false).Where(a => a.Obstacle).ToArray();
            for (var i = 0; i < 25; ++i)
            {
                Check(rooms[i].Center == new Vector2(source[i].Center.Position.X, source[i].Center.Position.Z), "Room center changed.");
                var walls = new[] { rooms[i].North, rooms[i].South, rooms[i].West, rooms[i].East };
                var expected = new[] { source[i].North, source[i].South, source[i].West, source[i].East };
                for (var d = 0; d < 4; ++d)
                {
                    var wall = expected[d];
                    Check(walls[d].Depth == wall.Depth && walls[d].Position == new Vector2(wall.Position.X, wall.Position.Z), "Wall data mismatch.");
                    if (wall.Depth <= 0) continue;
                    var area = areas.Single(a => a.Key == $"floor/wall/{i}/{d}");
                    var signed = new BossMod.SDRect(wall.Position, new BossMod.Angle(d >= 2 ? MathF.PI / 2 : 0), wall.Depth, wall.Depth, 20);
                    foreach (var x in new[] { -21f, -19f, -.2f, .2f, 19f, 21f })
                    foreach (var z in new[] { -4f, -.1f, .1f, 4f })
                    {
                        var point = new BossMod.WPos(wall.Position.X + x, wall.Position.Z + z);
                        Check(area.Geometry.Contains(new(point.X, 0, point.Z)) == (signed.Distance(point) <= 0), "Wall source signed geometry differs.");
                    }
                }
            }
            floor.Synchronize(snapshot with { Tileset = 2 }, [], 770);
            Check(!floor.Collect(Start, Player, false).Any(), "Tileset 2 invented walls.");
        }
        foreach (var (territory, traps) in HeavenOnHighFloorData.Traps)
            Check(traps.SequenceEqual(BossMod.Global.DeepDungeon.GeneratedTrapData.Traps[territory].Select(p => new Vector2(p.X, p.Z))),
                "Trap table differs.");
    }

    private static void Gazes()
    {
        foreach (var (action, radius) in new (uint, float)[] { (6351, 100), (12174, 25), (12322, 35),
            (12357, 35), (12261, 40), (12416, 40), (12442, 30) })
        {
            var model = Fixture();
            var cast = Cast(action);
            model.Synchronize([Player, Mob(cast: cast)], Start.AddSeconds(.1), true, Resolve);
            var sourceCircle = new BossMod.AOEShapeCircle(radius);
            for (var x = -110; x <= 110; x += 3)
            for (var z = -110; z <= 110; z += 3)
            {
                var player = Player with { Position = new(x + .17f, 0, z + .31f) };
                Check(model.Eyes(Start.AddSeconds(2), player).Any() == sourceCircle.Check(
                    new BossMod.WPos(player.Position.X, player.Position.Z), new BossMod.WPos(0, 0), default), "Gaze range source mismatch.");
            }
            var eye = model.Eyes(Start.AddSeconds(8), Player).Single();
            Check(Math.Abs((eye.Hit - Start.AddSeconds(8)).TotalSeconds - .1f) < .00001, "NPC elapsed cap/Extra wrong.");
            model.Synchronize([Player, Mob(cast: cast) with { Dead = true, Position = new(10, 0, 0) }],
                Start.AddSeconds(9), true, Resolve);
            Check(model.Eyes(Start.AddSeconds(9), Player).Single().Origin.X == 10, "Death falsely ended or source pose locked.");
            model.Synchronize([Player, Mob()], Start.AddSeconds(10), true, Resolve);
            Check(!model.Eyes(Start.AddSeconds(10), Player).Any(), "Raw null did not finish gaze.");
        }
    }

    private static void Cold()
    {
        var model = new HeavenOnHighFloorEncounter();
        var cast = Cast(6351);
        model.Synchronize([Player, Mob(cast: cast)], Start.AddSeconds(1), true, Resolve);
        model.StartPacket(new(1, 6351, 100, Vector3.Zero, Vector3.Zero, 0, cast.StartedAt, cast.FinishAt));
        Check(!model.Eyes(Start.AddSeconds(1), Player).Any(), "Cold Zone replayed old callback.");
        model.Synchronize([Player, Mob(cast: cast) with { GameObjectId = 99 }], Start.AddSeconds(2), true, Resolve);
        model.Synchronize([Player, Mob(cast: cast) with { GameObjectId = 100 }], Start.AddSeconds(2.1), true, Resolve);
        Check(model.Eyes(Start.AddSeconds(2.1), Player).Count() == 1, "Same-ID new generation not observed.");
        model.Synchronize([Player], Start.AddSeconds(2.2), true, Resolve);
        Check(!model.Eyes(Start.AddSeconds(2.2), Player).Any(), "Destroy did not clear source.");
    }

    private static void Replacements()
    {
        var model = Fixture();
        model.Synchronize([Player, Mob(cast: Cast(12415))], Start.AddSeconds(.1), true, Resolve);
        var areas = model.Collect(Start.AddSeconds(.2), Player).ToArray();
        Check(areas.Count(a => a.Shape is DonutTemplate) == 2
            && areas.Any(a => a.Shape is DonutTemplate { InnerRadius: 3 }), "HintDisabled deleted ordinary Source=0 donut.");
        model.Synchronize([Player, Mob(cast: Cast(90000, 1, new(3, 0, 4)))], Start.AddSeconds(1), true, Resolve);
        areas = model.Collect(Start.AddSeconds(1), Player).ToArray();
        Check(areas.Count(a => a.Shape is CircleTemplate) == 1
            && areas.Single(a => a.Shape is DonutTemplate).Position == new Vector3(3, 0, 4), "Non-null replacement lost separate Zone list.");
        model.ZoneEnabled = false;
        Check(model.Collect(Start.AddSeconds(1), Player).All(a => !a.Key.StartsWith("zone/")), "Disabled Zone still output.");
        model.Synchronize([Player, Mob()], Start.AddSeconds(2), true, Resolve);
        model.ZoneEnabled = true;
        Check(!model.Collect(Start.AddSeconds(2), Player).Any(), "Re-enabled old list not pruned.");
    }

    private static void Generic()
    {
        var model = Fixture();
        var target = Player with { Id = 101, Position = new(3, 0, 0), GameObjectId = 1 };
        model.Synchronize([Player, target, Mob()], Start.AddSeconds(.01), true, Resolve);
        model.Synchronize([Player, target, Mob(cast: Cast(90001, target: 101))], Start.AddSeconds(.1), true, Resolve);
        model.Synchronize([Player, target with { Position = new(6, 0, 0) }, Mob(cast: Cast(90001, target: 101))],
            Start.AddSeconds(.2), true, Resolve);
        Check(model.Eyes(Start.AddSeconds(.2), Player).Single().Origin.X == 6, "Generic target did not follow original.");
        model.Synchronize([Player, Mob(cast: Cast(90001, target: 101))], Start.AddSeconds(.3), true, Resolve);
        model.Synchronize([Player, target with { GameObjectId = 2, Position = new(15, 0, 0) }, Mob(cast: Cast(90001, target: 101))],
            Start.AddSeconds(.4), true, Resolve);
        Check(model.Eyes(Start.AddSeconds(.4), Player).Single().Origin.X == 6, "Destroyed original rebound by ID.");
        var source = Mob(cast: Cast(90002, 1, new(20, 0, 10))) with { Position = new(1.111f, 0, 2.222f) };
        model.Synchronize([Player, source], Start.AddSeconds(1), true, Resolve);
        var area = model.Collect(Start.AddSeconds(1), Player).Single();
        var expected = new BossMod.WPos(source.Position.X, source.Position.Z).Quantized();
        Check(area.Position == new Vector3(expected.X, 0, expected.Z), "Charge origin quantization mismatch.");
        var signed = new BossMod.SDRect(expected, new BossMod.WPos(20, 10), 2);
        for (var x = -10; x <= 30; ++x)
        for (var z = -10; z <= 30; ++z)
            Check(area.Geometry.Contains(new(x + .17f, 0, z + .31f)) == (signed.Distance(new(x + .17f, z + .31f)) <= 0), "Charge signed source geometry.");
    }

    private static void Traps()
    {
        var floor = new PalaceFloorHazards(2, 100, HeavenOnHighFloorData.Rooms, HeavenOnHighFloorData.Traps);
        var trap = HeavenOnHighFloorData.Traps[770][0];
        var player = Player with { Position = new(trap.X, 0, trap.Y) };
        var indicator = Mob() with { Oid = 0x1EA9A3, Position = player.Position };
        var snapshot = new ReplayDeepDungeonState { DungeonId = 2, Floor = 1, Rooms = new ushort[25] };
        floor.Synchronize(snapshot, [indicator], 770);
        Check(floor.Collect(Start, player).Any(a => a.Position == player.Position), "Cold indicator incorrectly replayed.");
        floor.ObserveCreated(indicator);
        Check(!floor.Collect(Start, player).Any(a => a.Position == player.Position), "Real Beacon Created did not ignore.");
        floor.SystemLog(7255);
        Check(!floor.Collect(Start, player).Any(), "Safety did not hide hypothetical traps.");
        var model = Fixture();
        model.Synchronize([player, Mob() with { Oid = 0x1EA08E, Dead = true, EventState = 7 }], Start, true, Resolve);
        Check(model.Collect(Start, player).Single().Name == "已揭示陷阱", "Revealed traps incorrectly filtered by death/state.");
        floor.SystemLog(7248);
        Check(floor.BetweenFloors && !floor.Collect(Start, player).Any(), "Transition output survived.");
        floor.Synchronize(snapshot with { Floor = 10 }, [], 770);
        Check(!floor.BetweenFloors && !floor.HasMap, "Boss map did not release transfer.");
    }

    private static void Intents()
    {
        foreach (var action in new uint[] { 12313, 12258, 12227, 12350 })
        {
            var model = Fixture();
            model.Synchronize([Player, Mob(cast: Cast(action))], Start.AddSeconds(.1), true, Resolve);
            Check(model.Hints(Start.AddSeconds(20), Player).Single().StartsWith(action == 12350 ? "眩晕" : "打断")
                && !model.Collect(Start.AddSeconds(20), Player).Any(), "Intent became geometry or expired on clock.");
            model.Synchronize([Player, Mob()], Start.AddSeconds(21), true, Resolve);
            Check(!model.Hints(Start.AddSeconds(21), Player).Any(), "Raw null retained interrupt.");
        }
    }

    private static void Facing()
    {
        var model = Fixture();
        var cast = Cast(6351);
        var other = Mob(2, cast) with { Position = new(5, 0, 5) };
        model.Synchronize([Player, Mob(cast: cast), other], Start.AddSeconds(.1), true, Resolve);
        Check(model.Facing(Start.AddSeconds(1), Player) == null, "Facing before activation window.");
        var result = model.Facing(Start.AddSeconds(3), Player);
        Check(result.HasValue && float.IsFinite(result.Value), "Joint facing unavailable.");
        var before = model.Eyes(Start.AddSeconds(3), Player).ToArray();
        model.Synchronize([Player, Mob(cast: cast), other], Start.AddSeconds(3.1), false, Resolve);
        Check(model.Eyes(Start.AddSeconds(3.1), Player).Count() == before.Length, "Duplicate frame reinterpreted casts.");
        model.BossActive = true;
        Check(!model.Collect(Start.AddSeconds(3.1), Player).Any() && model.Facing(Start.AddSeconds(3.1), Player) == null, "Boss handoff failed.");
        model.Reset();
        model.BossActive = false;
        Check(!model.Collect(Start, Player).Any(), "Reset retained owner model state.");
    }

    private static void InputEdges()
    {
        var model = Fixture();
        var cast = Cast(90001) with { IsSpell = null };
        model.StartPacket(new(1, cast.ActionId, cast.TargetId, cast.Location, Vector3.Zero,
            cast.Rotation, cast.StartedAt, cast.FinishAt));
        model.Synchronize([Player, Mob(cast: cast)], Start.AddSeconds(.1), true, Resolve);
        Check(model.Eyes(Start.AddSeconds(.1), Player).Single().Gaze.Generic, "First nullable raw failed generic initialization.");
        var late = Fixture();
        late.Synchronize([Player, Mob(cast: Cast(6351))], Start.AddSeconds(3.2), true, Resolve);
        Check(Math.Abs((late.Eyes(Start.AddSeconds(3.2), Player).Single().Hit - Start.AddSeconds(3.2)).TotalSeconds - .3f) < .00001,
            "First late raw incorrectly capped to adjusted total.");
        late.ClearFloor();
        var created = new List<ulong>();
        late.Synchronize([Player, Mob(cast: Cast(6351)), Mob(2, Cast(12432, 4))], Start.AddSeconds(4), true,
            Resolve, a => created.Add(a.Id));
        Check(created.SequenceEqual([2ul]) && !late.Eyes(Start.AddSeconds(4), Player).Any()
            && late.Collect(Start.AddSeconds(4), Player).Single().Shape is DonutTemplate { InnerRadius: 8 },
            "Hot clear replayed old casts or suppressed new births.");
    }

    private static void PacketTransitions()
    {
        var model = Fixture();
        var cast = Cast(6351);
        var packet = new PalaceCast(1, cast.ActionId, cast.TargetId, cast.Location, Vector3.Zero,
            cast.Rotation, cast.StartedAt, cast.FinishAt);
        model.StartPacket(packet);
        Check(model.Eyes(Start.AddSeconds(.1), Player).Count() == 1, "Packet failed to stage gaze.");
        model.ClearFloor();
        model.Synchronize([Player, Mob(cast: cast)], Start.AddSeconds(.2), true, Resolve);
        Check(model.Eyes(Start.AddSeconds(.2), Player).Count() == 1, "First real raw Start lost after ClearState.");
        model.Synchronize([Player], Start.AddSeconds(.3), true, Resolve);
        model.Synchronize([Player, Mob() with { GameObjectId = 2 }], Start.AddSeconds(.4), true, Resolve);
        model.StartPacket(packet);
        Check(!model.Eyes(Start.AddSeconds(.4), Player).Any(), "Old packet rebound a new shell.");
        var fresh = Cast(6351, .5);
        model.StartPacket(packet with { Start = fresh.StartedAt, Hit = fresh.FinishAt });
        Check(!model.Eyes(Start.AddSeconds(.5), Player).Any(), "Unidentified reused-ID packet bypassed raw confirmation.");
        model.Synchronize([Player, Mob(cast: fresh) with { GameObjectId = 2 }], Start.AddSeconds(.6), true, Resolve);
        Check(model.Eyes(Start.AddSeconds(.6), Player).Count() == 1, "Raw confirmation duplicated a new packet.");
    }

    private static void MissingMap()
    {
        var floor = new PalaceFloorHazards(2, 100, HeavenOnHighFloorData.Rooms, HeavenOnHighFloorData.Traps);
        var trap = HeavenOnHighFloorData.Traps[770][0];
        var player = Player with { Position = new(trap.X, 0, trap.Y) };
        var birth = Mob() with { Oid = 0x1EA9A3, Position = player.Position };
        var map = new ReplayDeepDungeonState { DungeonId = 2, Floor = 1, Rooms = new ushort[25] };
        floor.Synchronize(null, [], 770);
        floor.ObserveCreated(birth);
        floor.Synchronize(null, [birth with { Position = new(100, 0, 100) }], 770);
        floor.Synchronize(null, [], 770);
        floor.Synchronize(map, [], 770);
        Check(!floor.Collect(Start, player).Any(a => a.Position == player.Position),
            "Null maps erased the original hot indicator position.");
        floor.Synchronize(null, [], 770);
        floor.Synchronize(map, [], 770);
        Check(!floor.Collect(Start, player).Any(a => a.Position == player.Position),
            "Temporarily missing established map erased history.");
        floor.SystemLog(7248);
        floor.Synchronize(map with { Floor = 2 }, [], 770);
        Check(floor.Collect(Start, player).Any(a => a.Position == player.Position), "ClearState failed to clear history.");
        floor.SystemLog(7256);
        Check(!floor.Collect(Start, player).Any(), "Sight failed to hide hypothetical traps.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
