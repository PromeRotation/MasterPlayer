using System.Numerics;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

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
        run("Palace floor native BFS safe bounds passage without loaded actor and combat gating", Passage);
        run("Palace floor native map and system logs survive recording playback and reset", Recording);
        run("Palace floor native coffer selection thresholds transformations combat and passage priority", Coffers);
        run("Palace floor native clearing passage leveling petrification and dangerous enemies", Clearing);
        run("Palace floor native actual enmity count and mapped Intuition state govern navigation", EnmityAndItems);
        run("Palace floor native manual rooms and nearest trap commands preserve floor lifecycle and navigation gates", ManualCommands);
        run("Palace parity gaze avoids unnecessary cancellation and independently cancels forbidden casts", CastControl);
        run("Palace parity party assistance and final clearing priority preserve current targets", PartyAssistance);
        run("Palace parity coffer navigation reaches interaction range before room exploration", CofferApproach);
        run("Palace parity overcap uses confirmed chest contents mapped slots solo settings and status gates", Pomanders);
    }

    private static void Pomanders()
    {
        var floor = new PalaceFloorNavigation();
        var player = Player(Vector3.Zero) with { InCombat = true };
        var chest = new ReplayActorState { Id = 1, Oid = 0x1EA13E, Targetable = true, Position = Vector3.UnitZ * 2 };
        var items = new ReplayDeepDungeonItem[16];
        items[5] = new(8, 3, 1);
        var state = State() with { Items = items };
        var options = new PalaceFloorOptions { AutoPassage = false, AutoMoveTreasure = false,
            GoldCoffer = false, AutoPomanders = new HashSet<uint> { 8 } };
        void Collect(ReplayActorState current, PalaceFloorOptions? use = null)
            => floor.Collect(Start, current, use ?? options).ToArray();
        floor.Synchronize(state, [chest], 603);
        Collect(player);
        Check(floor.PomanderToUse == null, "Full inventory alone triggered a pomander.");
        floor.SystemLog(7222, [8]);
        // Chest association uses the same nearest eligible chest rule as the source.
        Collect(player, options with { GoldCoffer = true });
        Collect(player);
        Check(floor.PomanderToUse == null, "Purity was used without Pox.");
        var pox = player with { Statuses = [new(1087, 0, 100, Start.AddSeconds(20))] };
        Collect(pox);
        Check(floor.PomanderToUse == (5, 8u) && floor.InteractionTarget == null,
            "Confirmed full coffer did not request the mapped slot independently of combat/treasure navigation switches.");
        Collect(pox, options with { AutoPomanders = new HashSet<uint>() });
        Check(floor.PomanderToUse == null, "Default empty AutoPoms selection used an item.");
        floor.Synchronize(state with { PartyRooms = new() { [100] = 12, [101] = 12 } }, [chest], 603);
        Collect(pox);
        Check(floor.PomanderToUse == null, "AutoPoms ran with another party member.");
        floor.Synchronize(state, [chest], 603);
        foreach (var status in new uint[] { 7, 620, 1094, 565, 439, 4586 })
        {
            Collect(pox with { Statuses = [.. pox.Statuses, new(status, 0, 100, Start.AddSeconds(20))] });
            Check(floor.PomanderToUse == null, $"AutoPoms bypassed status {status}.");
        }
        items[5] = new(8, 2, 1);
        floor.Synchronize(state, [chest], 603);
        Collect(pox, options with { GoldCoffer = true });
        Check(floor.PomanderToUse == null && floor.InteractionTarget?.Id == 1,
            "After consuming a pomander, the coffer did not become available again.");
        items[5] = new(8, 3, 0);
        floor.Synchronize(state, [chest], 603);
        Collect(pox);
        Check(floor.PomanderToUse == null, "Unusable inventory requested a native action.");
        floor.SystemLog(7248);
        floor.Synchronize(state with { Floor = 152 }, [chest], 603);
        items[5] = new(8, 3, 1);
        Collect(pox);
        Check(floor.PomanderToUse == null, "Floor transfer reused a previous chest entity association.");
        floor.SystemLog(7222, [null]);
        Collect(pox, options with { GoldCoffer = true });
        Collect(pox);
        Check(floor.PomanderToUse == null, "Missing typed integer was guessed as a pomander.");
    }

    private static void CofferApproach()
    {
        var floor = new PalaceFloorNavigation();
        var state = State() with { PartyRooms = new() { [100] = 6 } };
        state.Rooms[6] = 0x88;
        state.Rooms[7] = 4;
        var player = Player(Vector3.Zero);
        var chest = new ReplayActorState { Id = 1, Oid = 0x1EA13E, Targetable = true, Position = Vector3.UnitZ * 10 };
        floor.Synchronize(state, [chest], 603);
        var goals = floor.Collect(Start, player, new() { FullClear = true, AutoPassage = false }).Where(a => a.Safe).ToArray();
        var approach = goals.Single(a => a.Key == "floor/goal/coffer/interact");
        Check(!approach.Geometry.Contains(player.Position) && approach.Geometry.Contains(chest.Position)
            && approach.Geometry.Contains(chest.Position - Vector3.UnitZ * 2.9f)
            && !approach.Geometry.Contains(chest.Position - Vector3.UnitZ * 3.1f)
            && approach.Priority > goals.Single(a => a.Key == "floor/goal/room").Priority,
            "The broad coffer goal still permits idling outside interaction range or loses to room exploration.");
        var snapshot = new SafeZoneSnapshot(1, Start, new CircleGeometry(Vector3.Zero, 30), [], [],
            goals.Select(a => new GoalAreaSnapshot(default, a.Geometry,
                [GoalWindow.Persistent(Start, a.Priority, PositionPreference.Nearest)])).ToArray());
        var solution = new SafeZoneSolver().Solve(snapshot,
            new(player.Position, 0, 6, false, false, 0, true, snapshot.Version), new(6, 6, 0, false, 6, 6),
            new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0,
                GoalDirectedTemporalPlanning = true }, Start);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.IsSuccess && !solution.IsNoMovement && path.Count > 0
            && Vector3.Distance(path[^1].Position, chest.Position) < 3.5f,
            "The real solver did not bring the player within coffer interaction range.");
        floor.Collect(Start, player with { InCombat = true }, new() { AutoPassage = false }).ToArray();
        Check(floor.InteractionTarget == null, "Approach bypassed combat navigation gating.");
    }

    private static void PartyAssistance()
    {
        var floor = new PalaceFloorNavigation();
        var state = State() with { PartyRooms = new() { [100] = 12, [101] = 12 } };
        var player = Player(Vector3.Zero) with { Level = 60, TargetId = 1 };
        var normal = new ReplayActorState { Id = 1, Hostile = true, Targetable = true, Hp = 100 };
        var partyEnemy = normal with { Id = 2, InCombat = true, TargetId = 101 };
        floor.Synchronize(state, [partyEnemy], 603);
        Check(floor.PreferredTarget(Start, player, PalaceClearBehavior.None) == 2
            && floor.PreferredTarget(Start, player, PalaceClearBehavior.Leveling) == 2,
            "A monster attacking a party member was ignored after clearing stopped.");
        floor.Synchronize(state, [partyEnemy with { TargetId = 999 }], 603);
        Check(floor.PreferredTarget(Start, player, PalaceClearBehavior.None) == 0, "Unrelated combat enabled party assistance.");
        var patrol = partyEnemy with { Oid = 0x1842, TargetId = 100 };
        floor.Synchronize(state, [normal, patrol], 603);
        Check(floor.PreferredTarget(Start, player, PalaceClearBehavior.All) == 1,
            "Palace extra priority was not overwritten by the later clearing branch.");
        Check(floor.PreferredTarget(Start, player, PalaceClearBehavior.None) == 2,
            "Stopped clearing lost the hostile patrol's extra priority.");
    }

    private static void ManualCommands()
    {
        var state = State() with { PassageProgress = 0, PartyRooms = new() { [100] = 6 } };
        state.Rooms[6] = 0x88;
        state.Rooms[7] = 0x04;
        var floor = new PalaceFloorNavigation();
        var player = Player(Vector3.Zero);
        var options = new PalaceFloorOptions { AutoPassage = false, TrapHints = false };
        floor.Synchronize(state, [], 603);
        Check(!floor.SelectRoom(0, 100) && !floor.SelectRoom(6, 100) && !floor.SelectRoom(8, 100),
            "Invalid, current or unknown room became a destination.");
        Check(floor.SelectRoom(7, 100) && floor.Collect(Start, player, options).Any(a => a.Key == "floor/goal/room"),
            "Manual room destination incorrectly required an active passage or AutoPassage.");
        Check(!floor.Collect(Start, player with { InCombat = true }, options).Any(a => a.Safe),
            "Manual navigation bypassed the combat gate.");
        Check(floor.SelectRoom(7, 100) && floor.DesiredRoom == 0, "Repeated room click did not clear the destination.");
        floor.SelectRoom(7, 100);
        floor.Synchronize(state with { PartyRooms = new() { [100] = 7 } }, [], 603);
        Check(!floor.Collect(Start, player, options).Any(a => a.Key == "floor/goal/room") && floor.DesiredRoom == 0,
            "Reaching the manual destination retained navigation.");
        floor.Synchronize(state, [], 603);
        floor.SelectRoom(7, 100);
        floor.ClearDestination();
        Check(floor.DesiredRoom == 0, "Clear command retained the destination.");
        var traps = PalaceFloorData.Traps[603];
        var far = new Vector3(100000, 0, 100000);
        var nearest = Enumerable.Range(0, traps.Length).MinBy(i => Vector2.DistanceSquared(traps[i], new(far.X, far.Z)));
        var atTrap = Player(new(traps[nearest].X, 0, traps[nearest].Y));
        var key = $"floor/trap/{nearest}";
        Check(floor.Collect(Start, atTrap).Any(a => a.Key == key), "Missing the historical trap command baseline.");
        Check(floor.IgnoreNearestTrap(far) && !floor.Collect(Start, atTrap).Any(a => a.Key == key),
            "Manual trap ignore was limited to thirty yalms or used a rounded position.");
        floor.SelectRoom(7, 100);
        floor.SystemLog(7248);
        floor.Synchronize(state with { Floor = 152 }, [], 603);
        Check(floor.DesiredRoom == 0 && !floor.Collect(Start, atTrap).Any(a => a.Key == key),
            "Floor transfer retained the destination or lost the manual trap exclusion.");
        floor.Reset();
        floor.Synchronize(state, [], 603);
        Check(floor.Collect(Start, atTrap).Any(a => a.Key == key), "Stopping the floor session retained a manual trap exclusion.");
    }

    private static void EnmityAndItems()
    {
        var state = State();
        var floor = new PalaceFloorNavigation();
        var player = Player(Vector3.Zero) with { InCombat = true, Level = 60 };
        var chest = new ReplayActorState { Id = 1, Oid = 0x1EA13E, Targetable = true, Position = Vector3.UnitZ * 10 };
        var mob = new ReplayActorState { Id = 3, Hostile = true, Targetable = true, Hp = 100, AggroPlayer = true,
            TargetId = 101 };
        floor.Synchronize(state, [chest, mob], 603);
        floor.Collect(Start, player, new() { MaxPull = 2 }).ToArray();
        Check(floor.InteractionTarget?.Id == 1, "Available pull capacity did not permit coffer navigation.");
        floor.Collect(Start, player, new() { MaxPull = 1 }).ToArray();
        Check(floor.InteractionTarget == null, "Hater membership targeting someone else did not stop navigation.");
        Check(floor.PreferredTarget(Start, player, PalaceClearBehavior.None) == 3, "Enmity enemy was omitted because it targets another actor.");
        floor.Synchronize(state, [chest, mob with { AggroPlayer = false, TargetId = 100 }], 603);
        floor.Collect(Start, player, new() { MaxPull = 1 }).ToArray();
        Check(floor.InteractionTarget?.Id == 1
            && floor.PreferredTarget(Start, player, PalaceClearBehavior.None) == 0, "TargetId was used as a substitute for Hater membership.");
        var items = new ReplayDeepDungeonItem[16];
        items[5] = new(14, 3, 2);
        var indicator = new ReplayActorState { Id = 4, Oid = 0x1EA1F6, Position = Vector3.UnitX * 5 };
        floor.Synchronize(state with { Items = items }, [indicator], 603);
        var hoard = floor.Collect(Start, player with { InCombat = false }).Single(a => a.Key == "floor/goal/hoard");
        Check(hoard.Priority == 200 && items[5].Active && !items[5].Usable,
            "Intuition mapping, active flag or hoard priority was lost.");
        items[5] = new(14, 3, 1);
        floor.Synchronize(state with { Items = items }, [indicator], 603);
        Check(!floor.Collect(Start, player with { InCombat = false }).Any(a => a.Key == "floor/goal/hoard"),
            "Usable inventory was confused with active Intuition.");
        var before = new ReplayWorldFrame { Actors = [mob with { AggroPlayer = false }] };
        var after = before with { Actors = [mob] };
        Check(ReplayWorldFrame.Delta(before, after).Actors.Single().AggroPlayer, "Enmity-only change vanished from recording delta.");
    }

    private static void Coffers()
    {
        var floor = new PalaceFloorNavigation();
        var state = State() with { WeaponLevel = 99, ArmorLevel = 98 };
        var player = Player(Vector3.Zero) with { Hp = 71, MaxHp = 100, HitboxRadius = 1 };
        var silver = new ReplayActorState { Id = 1, Oid = 0x1EA13D, Position = Vector3.UnitZ * 5, Targetable = true };
        var gold = silver with { Id = 2, Oid = 0x1EA13E, Position = Vector3.UnitZ * 8 };
        var passage = silver with { Id = 3, Oid = 0x1EA094, Position = Vector3.UnitZ * 3 };
        void Collect(ReplayActorState current, PalaceFloorOptions? options = null)
            => floor.Collect(Start, current, options).ToArray();
        floor.Synchronize(state, [silver, gold], 603);
        Collect(player);
        Check(floor.InteractionTarget?.Id == 1, "Eligible nearest silver coffer was not selected.");
        Collect(player with { Hp = 70 });
        Check(floor.InteractionTarget?.Id == 2, "Silver chest opened at exactly seventy percent HP.");
        floor.Synchronize(state with { ArmorLevel = 99 }, [silver, gold], 603);
        Collect(player);
        Check(floor.InteractionTarget?.Id == 2, "Capped silver coffer was selected.");
        floor.Synchronize(state, [gold with { OpenedTreasure = true }, silver with { Targetable = false }], 603);
        Collect(player);
        Check(floor.InteractionTarget == null, "Opened or untargetable coffers were selected.");
        floor.Synchronize(state, [gold, passage], 603);
        Collect(player);
        Check(floor.InteractionTarget == null, "Farther coffer overrode the nearer active passage.");
        Collect(player, new() { OpenChestsFirst = true });
        Check(floor.InteractionTarget?.Id == 2, "Explicit chest priority was lost.");
        foreach (var status in new uint[] { 565, 439, 1534, 1448, 4586, 4708 })
        {
            Collect(player with { Statuses = [new(status, 0, 100, Start.AddSeconds(20))] }, new() { OpenChestsFirst = true });
            Check(floor.InteractionTarget == null, $"Transformed player selected coffer ({status}).");
        }
        floor.Synchronize(state, [gold], 603);
        Collect(player with { InCombat = true });
        Check(floor.InteractionTarget == null, "Combat navigation pulled a remote coffer.");
        floor.Synchronize(state, [gold with { Position = Vector3.UnitZ * 4.49f }], 603);
        Collect(player with { InCombat = true });
        Check(floor.InteractionTarget?.Id == 2, "Nearby combat coffer interaction was suppressed.");
        floor.Synchronize(state, [gold with { Position = Vector3.UnitZ * 4.5f }], 603);
        Collect(player with { InCombat = true });
        Check(floor.InteractionTarget == null, "Strict 3.5 hitbox distance was widened.");
        Check(PalaceFloorNavigation.IsBronzeCoffer(782) && PalaceFloorNavigation.IsBronzeCoffer(805)
            && !PalaceFloorNavigation.IsBronzeCoffer(791), "Bronze object IDs were confused with name IDs.");
    }

    private static void Clearing()
    {
        var floor = new PalaceFloorNavigation();
        var state = State();
        var player = Player(Vector3.Zero) with { Level = 60 };
        var enemy = new ReplayActorState { Id = 1, Hostile = true, Targetable = true, Hp = 100,
            Position = Vector3.UnitZ * 10 };
        floor.Synchronize(state, [enemy], 603);
        Check(floor.PreferredTarget(Start, player, PalaceClearBehavior.Leveling) == 0, "Capped leveling pulled after passage opened.");
        Check(floor.PreferredTarget(Start, player with { Level = 59 }, PalaceClearBehavior.Leveling) == 1,
            "Uncapped leveling ignored valid enemy.");
        floor.Synchronize(state with { PassageProgress = 10 }, [enemy], 603);
        Check(floor.PreferredTarget(Start, player, PalaceClearBehavior.Passage) == 1, "Inactive passage did not enable clearing.");
        foreach (var status in new uint[] { 53, 61, 2056 })
        {
            floor.Synchronize(state, [enemy with { Statuses = [new(status, 0, 1, Start.AddSeconds(20))] }], 603);
            Check(floor.PreferredTarget(Start, player, PalaceClearBehavior.All) == 0, "Dangerous enemy was proactively pulled.");
        }
        floor.Synchronize(state, [enemy with { Statuses = [new(437, 0, 1, Start.AddSeconds(1.6))] }], 603);
        Check(floor.PreferredTarget(Start, player, PalaceClearBehavior.None) == 1, "Petrified enemy was not prioritized.");
        Check(floor.PreferredTarget(Start.AddSeconds(.1), player, PalaceClearBehavior.None) == 0, "Petrification expiry threshold was widened.");
        floor.Synchronize(state, [enemy with { Hp = 9, Statuses = [new(1097, 0, 1, Start.AddSeconds(20))] }], 603);
        Check(floor.PreferredTarget(Start, player, PalaceClearBehavior.None) == 1, "Storm-weakened enemy was ignored.");
        floor.Reset();
        Check(floor.PreferredTarget(Start, player, PalaceClearBehavior.All) == 0, "Floor reset retained a target.");
    }

    private static void Walls()
    {
        var floor = new PalaceFloorNavigation();
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
            var floor = new PalaceFloorNavigation();
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

    private static void Passage()
    {
        var state = State();
        state.Rooms[12] = 0x88;
        state.Rooms[13] = 0x86;
        state.Rooms[18] = 0xA1;
        Check(PalaceFloorNavigation.Pathfind(state.Rooms, 12, 18).SequenceEqual([13, 18]), "BFS changed the connected path.");
        state.Rooms[13] = 0x06;
        Check(PalaceFloorNavigation.Pathfind(state.Rooms, 12, 18).SequenceEqual([13, 18]), "Unrevealed valid connection was rejected.");
        state.Rooms[13] = 0x84;
        Check(PalaceFloorNavigation.Pathfind(state.Rooms, 12, 18).Length == 0, "Disconnected rooms became reachable.");
        state.Rooms[13] = 0x86;
        var floor = new PalaceFloorNavigation();
        var player = Player(new(-300, 0, 300));
        floor.Synchronize(state, [], 603);
        var goal = floor.Collect(Start, player).Single(a => a.Key == "floor/goal/room");
        Check(goal.Safe && goal.NavigationOnly && !goal.OverrideDangerMargin
            && !goal.Geometry.Contains(player.Position + Vector3.UnitX * 10)
            && goal.Geometry.Contains(player.Position + Vector3.UnitX * 10.01f), "Map-only passage direction or ten-yalm threshold differs.");
        Check(!floor.Collect(Start, player with { InCombat = true }).Any(a => a.Safe), "Passage pulls during combat.");
        floor.Synchronize(state with { PassageProgress = 10 }, [], 603);
        floor.ClearDestination();
        Check(!floor.Collect(Start, player).Any(a => a.Safe), "Inactive passage generated goals.");
        state.Rooms[4] = 0x88;
        state.Rooms[5] = 0x84;
        Check(PalaceFloorNavigation.Pathfind(state.Rooms, 4, 5).Length == 0
            && PalaceFloorNavigation.Pathfind(state.Rooms, -1, 18).Length == 0, "Malformed map wrapped rows or indexed out of bounds.");
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
