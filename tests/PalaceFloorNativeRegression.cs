using System.Numerics;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class PalaceFloorNativeRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
    private static ReplayActorState Player(Vector3 position) => new() { Id = 100, Kind = 1, Position = position };
    private static ReplayDeepDungeonState State(byte tileset = 0)
        => new() { DungeonId = 1, Floor = 151, Tileset = tileset, Rooms = new ushort[25],
            PassageProgress = 11, PartyRooms = new() { [100] = 12 } };

    internal static void Run(Action<string, Action> run)
    {
        run("Palace floor native exact wall dimensions connections tilesets and boss exclusion", Walls);
        run("Palace floor native trap history ignores persist Safety and floor transitions", Traps);
        run("Palace floor native map and system logs survive recording playback and reset", Recording);
        run("Palace floor publishes only walls and traps despite coffers passage hoard full inventory and elapsed time", HazardsOnly);
        run("Palace floor native nearest trap correction preserves floor lifecycle", ManualTraps);
        run("Palace parity gaze avoids unnecessary cancellation and independently cancels forbidden casts", CastControl);
    }

    private static void HazardsOnly()
    {
        var floor = new PalaceFloorHazards();
        var trap = PalaceFloorData.Traps[603][0];
        var player = Player(new(trap.X, 0, trap.Y));
        var items = new ReplayDeepDungeonItem[16];
        items[5] = new(8, 3, 1);
        items[6] = new(14, 3, 2);
        var state = State() with { Items = items };
        state.Rooms[12] = 0x88;
        state.Rooms[13] = 0x24;
        var actors = new ReplayActorState[]
        {
            new() { Id = 1, Oid = 0x1EA13E, Targetable = true, Position = player.Position + Vector3.UnitZ * 2 },
            new() { Id = 2, Oid = 0x1EA13D, Targetable = true, Position = player.Position + Vector3.UnitX * 3 },
            new() { Id = 3, Oid = 782, Targetable = true, Position = player.Position + Vector3.UnitZ * 4 },
            new() { Id = 4, Oid = 0x1EA094, Position = player.Position + Vector3.UnitX * 15 },
            new() { Id = 5, Oid = 0x1EA1F6, Position = player.Position + Vector3.UnitX * 10 },
            new() { Id = 6, Oid = 0x1842, Targetable = true, Hostile = true, Hp = 100, InCombat = true, TargetId = player.Id },
        };
        floor.Synchronize(state, actors, 603);
        floor.SystemLog(7222);
        foreach (var inCombat in new[] { false, true })
        foreach (var time in new[] { Start, Start.AddMinutes(35) })
        {
            var areas = floor.Collect(time, player with { InCombat = inCombat }).ToArray();
            Check(areas.Any(a => a.Obstacle) && areas.Any(a => a.Key.StartsWith("floor/trap/")),
                "Removing ordinary work also removed avoidance constraints.");
            Check(areas.All(a => !a.Safe && (a.Obstacle || a.Key.StartsWith("floor/trap/"))),
                "Ordinary floor objects generated movement goals.");
        }
        Check(floor.Collect(Start, player, trapHints: false).All(a => a.Obstacle),
            "Disabling hidden trap hints changed walls or published an ordinary goal.");
        floor.Synchronize(null, [], 603);
        Check(!floor.HasMap && !floor.Collect(Start, player).Any(), "Leaving the dungeon retained constraints.");
    }

    private static void ManualTraps()
    {
        var floor = new PalaceFloorHazards();
        var state = State();
        floor.Synchronize(state, [], 603);
        var traps = PalaceFloorData.Traps[603];
        var far = new Vector3(100000, 0, 100000);
        var nearest = Enumerable.Range(0, traps.Length).MinBy(i => Vector2.DistanceSquared(traps[i], new(far.X, far.Z)));
        var atTrap = Player(new(traps[nearest].X, 0, traps[nearest].Y));
        var key = $"floor/trap/{nearest}";
        Check(floor.Collect(Start, atTrap).Any(a => a.Key == key), "Missing historical trap baseline.");
        Check(floor.IgnoreNearestTrap(far) && !floor.Collect(Start, atTrap).Any(a => a.Key == key),
            "Manual trap correction was limited to thirty yalms or rounded the position.");
        floor.SystemLog(7248);
        floor.Synchronize(state with { Floor = 152 }, [], 603);
        Check(!floor.Collect(Start, atTrap).Any(a => a.Key == key), "Floor transfer lost manual trap correction.");
        floor.Reset();
        floor.Synchronize(state, [], 603);
        Check(floor.Collect(Start, atTrap).Any(a => a.Key == key), "Stopping retained a manual trap exclusion.");
    }

    private static void Walls()
    {
        var floor = new PalaceFloorHazards();
        var state = State();
        state.Rooms[12] = 0x80;
        var player = Player(new(-300, 0, 300));
        floor.Synchronize(state, [], 603);
        var walls = floor.Collect(Start, player).Where(a => a.Obstacle).ToArray();
        Check(walls.Length == 4, "Isolated room did not have four walls.");
        var north = walls.Single(w => w.Key.EndsWith("/0"));
        Check(north.Position == new Vector3(-300, 0, 282.2f)
            && north.Shape is RectangleTemplate { Width: 40, Length: 4 }
            && north.Geometry.Contains(new(-319.9f, 0, 282.2f))
            && !north.Geometry.Contains(new(-320.1f, 0, 282.2f)), "Wall coordinates or half-length conversion differs.");
        Check(walls.Single(w => w.Key.EndsWith("/3")).Rotation == MathF.PI / 2, "East wall did not rotate.");
        state.Rooms[12] = 0x88;
        floor.Synchronize(state, [], 603);
        Check(floor.Collect(Start, player).Count(a => a.Obstacle) == 3, "Open east connection kept a wall.");
        var mirrored = State(1);
        mirrored.Rooms[12] = 0x80;
        floor.Synchronize(mirrored, [], 603);
        Check(floor.Collect(Start, player).Single(a => a.Key == "floor/wall/12/0").Position.Z < 0, "Tileset used layout index.");
        foreach (var snapshot in new[] { State(2), State(byte.MaxValue), State() with { Floor = 160 } })
        {
            floor.Synchronize(snapshot, [], 603);
            Check(!floor.Collect(Start, player).Any(a => a.Obstacle), "Unknown/boss map invented walls.");
        }
    }

    private static void Traps()
    {
        Check(PalaceFloorData.Traps.Values.Sum(a => a.Length) == 1445, "Incomplete historical trap migration.");
        foreach (var territory in Enumerable.Range(603, 5).Select(t => (ushort)t))
        {
            var floor = new PalaceFloorHazards();
            var trap = PalaceFloorData.Traps[territory][0];
            var player = Player(new(trap.X, 0, trap.Y));
            var state = State();
            floor.Synchronize(state, [], territory);
            Check(floor.Collect(Start, player).Any(a => a.Key == "floor/trap/0" && a.Shape is CircleTemplate { Radius: 2 }),
                "Territory trap history missing.");
            var indicator = new ReplayActorState { Id = 5, Oid = 0x1EA1F6, Position = player.Position + new Vector3(1, 0, 1) };
            floor.Synchronize(state, [indicator], territory);
            floor.Synchronize(state, [], territory);
            Check(!floor.Collect(Start, player).Any(a => a.Key == "floor/trap/0"), "Hoard exclusion did not persist after despawn.");
            floor.SystemLog(7255);
            floor.Synchronize(state, [], territory);
            Check(!floor.Collect(Start, player).Any(a => a.Key.StartsWith("floor/trap/")), "Safety lost to an unchanged map.");
            floor.SystemLog(7248);
            floor.SystemLog(7248);
            floor.Synchronize(state, [], territory);
            Check(!floor.Collect(Start, player).Any(), "Old map reactivated immediately after transfer.");
            floor.Synchronize(state with { Floor = 152 }, [], territory);
            Check(floor.Collect(Start, player).Any(a => a.Key == "floor/trap/0"), "New floor did not restore trap hints.");
            floor.Reset();
            floor.Synchronize(state with { TrapsHidden = false }, [], territory);
            Check(!floor.Collect(Start, player).Any(a => a.Key.StartsWith("floor/trap/")), "Initial recorded Safety history was ignored.");
        }
        var encounter = new PalaceEncounter();
        var visible = new ReplayActorState { Id = 20, Oid = 0x1EA9A0 };
        encounter.Synchronize([visible], Start);
        Check(encounter.Collect(Start, 100).Any(a => a.Key == "trap/20"), "Revealed traps were lost when hidden hints are disabled.");
        foreach (var trap in new[] { visible with { EventState = 7 }, visible with { Dead = true } })
        {
            encounter.Synchronize([trap], Start);
            Check(encounter.Collect(Start, 100).Any(a => a.Key == "trap/20"),
                "A revealed trap still present in the world was removed by unrelated actor lifecycle flags.");
        }
    }

    private static void Recording()
    {
        var items = new ReplayDeepDungeonItem[16];
        items[5] = new(14, 3, 3);
        var state = State() with { TrapsHidden = true, Items = items };
        state.Rooms[12] = 0x80;
        var initial = new ReplayWorldFrame { Full = true, Territory = 603, LocalPlayerId = 100,
            Actors = [Player(Vector3.Zero) with { AggroPlayer = true, OpenedTreasure = true }], DeepDungeon = state };
        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            using (var writer = new ReplayRecordingWriter(stream))
            {
                writer.WriteHeader(new(2, 603, Start, "native-floor-test"));
                writer.WriteWorldFrame(0, initial);
                writer.WriteEvent(1, new ReplaySystemLogEvent { MessageId = 7255, Timestamp = Start.AddSeconds(1) });
                writer.WriteWorldFrame(2, ReplayWorldFrame.Delta(initial, initial with { DeepDungeon = null }));
                writer.Flush();
                bytes = stream.ToArray();
            }
        }
        var recording = ReplayRecordingReader.Load(new MemoryStream(bytes));
        Check(recording.SkippedEvents == 0 && recording.Events.Single().Event is ReplaySystemLogEvent { MessageId: 7255 },
            "SystemLog was skipped as an unknown PR event.");
        var playback = new ReplayPlayback(recording);
        using var router = new ReplayScriptEventRouter(playback.World);
        var observed = new List<uint>();
        router.Subscribe(new object(), "SystemLog", (Action<uint>)(id => observed.Add(id)));
        playback.AdvanceTo(1, router.Raise, _ => { });
        Check(observed.SequenceEqual([7255u]) && playback.World.DeepDungeon?.Rooms[12] == 0x80
            && playback.World.DeepDungeon.PartyRooms[100] == 12
            && playback.World.DeepDungeon.Items?[5] is { PomanderId: 14, Count: 3, Active: true, Usable: true }
            && playback.World.Actors[100] is { AggroPlayer: true, OpenedTreasure: true },
            "Playback lost floor map, inventory, enmity or event routing.");
        playback.AdvanceTo(2, router.Raise, _ => { });
        Check(playback.World.DeepDungeon == null, "Leaving DD retained a stale map.");
        var restarted = new ReplayPlayback(recording);
        restarted.AdvanceTo(0, _ => { }, _ => { });
        Check(restarted.World.DeepDungeon?.TrapsHidden == true, "Replay reconstruction reused later trap state.");
    }

    private static void CastControl()
    {
        var encounter = new PalaceEncounter();
        var player = Player(Vector3.Zero) with { Rotation = MathF.PI,
            Cast = new(1, 10, default, null, Start, Start.AddSeconds(3.6), false, false) };
        var eye = new ReplayActorState { Id = 10, Oid = 0x1890, Position = Vector3.UnitZ * 10, Hostile = true };
        encounter.Synchronize([player, eye], Start);
        encounter.StartCast(new(10, 6953, 100, eye.Position, eye.Position, 0, Start, Start.AddSeconds(4)));
        var restrictions = encounter.CastRestrictions(Start.AddSeconds(3.5), 100, true);
        Check(restrictions.BlockCasts && !restrictions.CancelCast, "A safe gaze facing cancelled a completed-before-hit cast.");
        encounter.Synchronize([player with { Rotation = 0 }, eye], Start.AddSeconds(3.5));
        Check(encounter.CastRestrictions(Start.AddSeconds(3.5), 100, true).CancelCast, "Unsafe gaze did not cancel for a required turn.");
        encounter.Reset();
        eye = eye with { Statuses = [new(197, 0, 10, Start.AddSeconds(10))] };
        encounter.Synchronize([player, eye], Start);
        Check(!encounter.CastRestrictions(Start, 100, false).CancelCast
            && !encounter.ForbiddenTargets(Start).Contains(10ul)
            && encounter.ForbiddenAutoAttackTargets(Start).Contains(10ul), "Spikes blocked ordinary actions instead of only autoattacks.");
        var burstBoss = eye with { Oid = 0x181F, Hp = 15500, MaxHp = 100000, InCombat = true };
        encounter.Synchronize([player, burstBoss], Start);
        Check(encounter.CastRestrictions(Start, 100, false).CancelCast, "Forbidden boss cancellation incorrectly depended on a gaze.");
        var eyes = Enumerable.Range(0, 4).Select(i => eye with { Id = (ulong)(20 + i), Statuses = [],
            Position = new(MathF.Sin(i * MathF.PI / 2) * 10, 0, MathF.Cos(i * MathF.PI / 2) * 10) }).ToArray();
        encounter.Reset();
        encounter.Synchronize(eyes.Append(player), Start);
        foreach (var source in eyes)
            encounter.StartCast(new(source.Id, 6953, 100, source.Position, source.Position, 0, Start, Start.AddSeconds(4)));
        Check(encounter.Facing(Start.AddSeconds(3.5), 100) == null
            && encounter.CastRestrictions(Start.AddSeconds(3.5), 100, true).BlockCasts, "No feasible gaze heading cleared cast restrictions.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
