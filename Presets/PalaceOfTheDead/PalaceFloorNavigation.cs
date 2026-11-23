using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed class PalaceFloorNavigation
{
    private readonly List<Vector2> ignoredTraps = [];
    private readonly HashSet<Vector2> manuallyIgnoredTraps = [];
    private readonly HashSet<ulong> seenIndicators = [];
    private readonly Dictionary<ulong, uint> chestContents = [];
    private uint? lastOvercapPomander;
    private ReplayDeepDungeonState? state;
    private ReplayDeepDungeonState? previousFloor;
    private ReplayActorState[] actors = [];
    private ushort territory;
    private bool trapsHidden = true;
    private bool awaitingMap;
    internal bool BetweenFloors => awaitingMap;
    internal IReadOnlyList<ushort> Rooms => state?.Rooms ?? [];
    internal int DesiredRoom { get; private set; }
    internal int CurrentRoom(ulong player) => state?.PartyRooms.GetValueOrDefault(player, -1) ?? -1;
    internal ReplayActorState? InteractionTarget { get; private set; }
    internal (int Slot, uint Id)? PomanderToUse { get; private set; }

    internal bool SelectRoom(int room, ulong player)
    {
        if (state == null || awaitingMap || room <= 0 || room >= state.Rooms.Length
            || state.Rooms[room] == 0 || room == CurrentRoom(player)) return false;
        DesiredRoom = DesiredRoom == room ? 0 : room;
        return true;
    }

    internal void ClearDestination() => DesiredRoom = 0;

    internal bool IgnoreNearestTrap(Vector3 player)
    {
        if (state == null || awaitingMap || !PalaceFloorData.Traps.TryGetValue(territory, out var traps)) return false;
        var candidates = traps.Where(p => !manuallyIgnoredTraps.Contains(p)).ToArray();
        if (candidates.Length == 0) return false;
        var position = candidates.MinBy(p => Vector2.DistanceSquared(p, new(player.X, player.Z)));
        manuallyIgnoredTraps.Add(position);
        ignoredTraps.Add(position);
        return true;
    }

    internal void Synchronize(ReplayDeepDungeonState? snapshot, IEnumerable<ReplayActorState> observations, ushort currentTerritory)
    {
        if (snapshot is not { DungeonId: 1, Floor: >= 151 and <= 199 } || snapshot.Floor % 10 == 0
            || snapshot.Rooms.Length != 25)
        {
            if (!awaitingMap) Reset();
            else
            {
                state = null;
                actors = [];
            }
            return;
        }
        if (awaitingMap)
        {
            if (previousFloor != null && previousFloor.Floor == snapshot.Floor
                && previousFloor.Tileset == snapshot.Tileset && previousFloor.Rooms.SequenceEqual(snapshot.Rooms)) return;
            awaitingMap = false;
        }
        if (state != null && (state.Floor != snapshot.Floor || territory != currentTerritory))
            Reset(preserveManual: territory == currentTerritory);
        if (snapshot.TrapsHidden is { } hidden && (state == null || state.TrapsHidden != snapshot.TrapsHidden))
            trapsHidden = hidden;
        state = snapshot;
        territory = currentTerritory;
        actors = observations.ToArray();
        foreach (var actor in actors)
            if (actor.Oid == 0x1EA1F6 && seenIndicators.Add(actor.Id))
                ignoredTraps.Add(new(actor.Position.X, actor.Position.Z));
    }

    internal void SystemLog(uint id, IReadOnlyList<int?>? arguments = null)
    {
        if (id == 7222 && arguments is { Count: > 0 } && arguments[0] is >= 1 and <= 16)
            lastOvercapPomander = (uint)arguments[0]!.Value;
        else if (id == 7248)
        {
            if (awaitingMap) return;
            previousFloor = state;
            Reset(preserveManual: true);
            awaitingMap = true;
        }
        else if (id is 7255 or 7256) trapsHidden = false;
    }

    internal void Reset(bool preserveManual = false)
    {
        state = null;
        actors = [];
        InteractionTarget = null;
        PomanderToUse = null;
        chestContents.Clear();
        lastOvercapPomander = null;
        DesiredRoom = 0;
        if (!preserveManual) manuallyIgnoredTraps.Clear();
        ignoredTraps.Clear();
        ignoredTraps.AddRange(manuallyIgnoredTraps);
        seenIndicators.Clear();
        trapsHidden = true;
        awaitingMap = false;
    }

    internal IEnumerable<PalaceArea> Collect(DateTime now, ReplayActorState player, PalaceFloorOptions? options = null)
    {
        options ??= new();
        InteractionTarget = null;
        PomanderToUse = null;
        if (state == null || awaitingMap) yield break;
        var y = player.Position.Y;
        if (PalaceFloorData.Rooms.TryGetValue((state.Floor / 10 + 1, state.Tileset), out var rooms))
        {
            for (var i = 0; i < 25; ++i)
            {
                if (state.Rooms[i] == 0) continue;
                var room = rooms[i];
                var walls = new[] { room.North, room.South, room.West, room.East };
                for (var direction = 0; direction < walls.Length; ++direction)
                {
                    var wall = walls[direction];
                    if (wall.Depth <= 0 || (state.Rooms[i] & (1 << direction)) != 0) continue;
                    yield return new($"floor/wall/{i}/{direction}", "房间墙壁",
                        new RectangleTemplate(40, 2 * wall.Depth),
                        new(wall.Position.X, y, wall.Position.Y), direction >= 2 ? MathF.PI / 2 : 0,
                        now, DateTime.MaxValue, Obstacle: true, OutlineOnly: true);
                }
            }
        }
        if (options.TrapHints && trapsHidden && PalaceFloorData.Traps.TryGetValue(territory, out var traps))
        {
            var position = new Vector2(player.Position.X, player.Position.Z);
            for (var i = 0; i < traps.Length; ++i)
            {
                var trap = traps[i];
                if (Vector2.DistanceSquared(trap, position) > 900
                    || ignoredTraps.Any(p => MathF.Abs(p.X - trap.X) <= 1 && MathF.Abs(p.Y - trap.Y) <= 1)) continue;
                yield return new($"floor/trap/{i}", "隐藏陷阱", new CircleTemplate(2),
                    new(trap.X, y, trap.Y), 0, now, DateTime.MaxValue);
            }
        }
        // RotationModuleManager.TransformationStatuses; transformations change the available action set.
        var transformed = player.Statuses.Any(s => s.ExpiresAt > now && s.Id is
            1534 or 2760 or 2379 or 2633 or 2632 or 1448 or 565 or 439 or 1546 or 3502
            or 1284 or 404 or 4235 or 4376 or 4586 or 4708);
        var canNavigate = options.MaxPull <= 0 ? !player.InCombat
            : actors.Count(a => a.Hostile && a.Targetable && !a.Dead && a.AggroPlayer) < options.MaxPull;
        var fullCoffers = new HashSet<ulong>();
        foreach (var actor in actors.Where(a => a.Targetable))
        {
            if (!chestContents.TryGetValue(actor.Id, out var pomander) || state.Items == null) continue;
            var slot = Array.FindIndex(state.Items, item => item.PomanderId == pomander);
            if (slot < 0 || state.Items[slot].Count != 3) continue;
            fullCoffers.Add(actor.Id);
            if (PomanderToUse == null && options.AutoPomanders.Contains(pomander) && state.PartyRooms.Count <= 1
                && !transformed && state.Items[slot].Usable
                && !player.Statuses.Any(s => s.ExpiresAt > now && s.Id is 7 or 620 or 1094)
                && (pomander != 8 || player.Statuses.Any(s => s.Id == 1087 && s.ExpiresAt > now)))
                PomanderToUse = (slot, pomander);
        }
        var coffer = !transformed ? actors.Where(a => a.Targetable && !a.OpenedTreasure && !fullCoffers.Contains(a.Id)
                && IsWantedCoffer(a, player, options))
            .OrderBy(a => DistanceToHitbox(player, a)).FirstOrDefault() : null;
        if (coffer != null && lastOvercapPomander is { } contents)
        {
            chestContents[coffer.Id] = contents;
            lastOvercapPomander = null;
            yield break;
        }
        if (coffer != null && (options.AutoMoveTreasure && canNavigate || DistanceToHitbox(player, coffer) < 3.5f))
            InteractionTarget = coffer;
        var passage = actors.Where(a => a.Oid == 0x1EA094)
            .OrderBy(a => Vector3.DistanceSquared(a.Position, player.Position)).FirstOrDefault();
        var fullClearRoom = options.FullClear ? Array.FindIndex(state.Rooms, flags => flags > 0 && (flags & 0x80) == 0) : -1;
        if (fullClearRoom == 0) fullClearRoom = -1;
        if (fullClearRoom > 0) DesiredRoom = fullClearRoom;
        if (options.AutoPassage && !player.InCombat && state.PassageProgress >= 11 && fullClearRoom < 0 && passage != null)
        {
            yield return Goal("passage/near", new CircleTemplate(2), passage.Position, 0, 10);
            yield return Goal("passage/far", new CircleTemplate(25), passage.Position, 0, 5);
            if (!options.OpenChestsFirst && coffer != null && DistanceToHitbox(player, passage) < DistanceToHitbox(player, coffer))
                InteractionTarget = null;
        }
        if (InteractionTarget is { } chest)
        {
            yield return Goal("coffer", new CircleTemplate(25), chest.Position, 0, 20);
            // The source AI adds a high-weight proximity goal when it consumes
            // InteractWithTarget. The broad coffer goal alone can keep us idle.
            yield return Goal("coffer/interact", new CircleTemplate(3 + chest.HitboxRadius), chest.Position, 0, 2000);
        }
        if (!transformed && canNavigate && options.AutoMoveTreasure
            && state.Items?.Any(item => item.PomanderId == 14 && item.Active) == true
            && actors.FirstOrDefault(a => a.Oid == 0x1EA1F6) is { } hoard)
            yield return Goal("hoard", new CircleTemplate(2), hoard.Position, 0, 200);
        if (!canNavigate) yield break;
        if (!state.PartyRooms.TryGetValue(player.Id, out var currentRoom)) yield break;
        if (DesiredRoom == currentRoom) DesiredRoom = 0;
        if (DesiredRoom == 0 && options.AutoPassage && !player.InCombat && state.PassageProgress >= 11)
        {
            var passageRoom = Array.FindIndex(state.Rooms, flags => (flags & 0x20) != 0);
            if (passageRoom > 0 && passageRoom != currentRoom) DesiredRoom = passageRoom;
        }
        if (DesiredRoom == 0) yield break;
        var path = Pathfind(state.Rooms, currentRoom, DesiredRoom);
        if (path.Length == 0) yield break;
        var offset = path[0] - currentRoom;
        var directionVector = offset switch
        {
            -5 => -Vector3.UnitZ,
            5 => Vector3.UnitZ,
            -1 => -Vector3.UnitX,
            1 => Vector3.UnitX,
            _ => Vector3.Zero,
        };
        // Beyond ten yalms in the next room's direction, without inventing a passage location.
        yield return Goal("room", new RectangleTemplate(200, 100),
            player.Position + directionVector * 60.001f, MathF.Atan2(directionVector.X, directionVector.Z), 200);

        PalaceArea Goal(string key, GeometryTemplate shape, Vector3 position, float rotation, int priority)
            => new($"floor/goal/{key}", "前往转移石塔", shape, position, rotation, now, DateTime.MaxValue,
                Safe: true, NavigationOnly: true, Priority: priority, OverrideDangerMargin: false);
    }

    internal ulong PreferredTarget(DateTime now, ReplayActorState player, PalaceClearBehavior behavior)
    {
        if (state == null || awaitingMap) return 0;
        var clear = behavior switch
        {
            PalaceClearBehavior.Passage => state.PassageProgress < 11,
            PalaceClearBehavior.Leveling => player.Level < 60 || state.PassageProgress < 11,
            PalaceClearBehavior.All => true,
            _ => false,
        };
        bool CanClear(ReplayActorState actor) => clear
            && !actor.Statuses.Any(s => s.ExpiresAt > now && s.Id is 53 or 61 or 2056);
        bool AttackingParty(ReplayActorState actor) => actor.InCombat && actor.TargetId != 0
            && (actor.TargetId == player.Id || state.PartyRooms.ContainsKey(actor.TargetId));
        return actors.Where(a => a.Hostile && a.Targetable && !a.Dead && a.Hp > 0
            && (a.Statuses.Any(s => s.Id == 437 && s.ExpiresAt > now.AddSeconds(1.5)
                || s.Id == 1097 && s.ExpiresAt > now && a.Hp < 10)
                || a.AggroPlayer || AttackingParty(a) || CanClear(a)))
            // AutoClear runs after PalaceFloorModule and overwrites priority 10
            // when ordinary clearing is enabled for this actor.
            .OrderByDescending(a => a.Oid == 0x1842 && a.InCombat && a.TargetId == player.Id && !CanClear(a) ? 10 : 0)
            .ThenBy(a => a.Id == player.TargetId ? 0 : 1).ThenBy(a => DistanceToHitbox(player, a))
            .Select(a => a.Id).FirstOrDefault();
    }

    private bool IsWantedCoffer(ReplayActorState actor, ReplayActorState player, PalaceFloorOptions options)
        => actor.Oid switch
        {
            0x1EA13E => options.GoldCoffer,
            0x1EA13D => options.SilverCoffer && player.MaxHp > 0 && player.Hp > player.MaxHp * .7f
                && state!.WeaponLevel + state.ArmorLevel < 198,
            0x1EA1F7 => true,
            _ => options.BronzeCoffer && IsBronzeCoffer(actor.Oid),
        };

    internal static bool IsBronzeCoffer(uint oid) => oid is >= 782 and <= 790 or >= 802 and <= 805;
    internal static bool IsCoffer(uint oid) => oid is 0x1EA13E or 0x1EA13D or 0x1EA1F7 || IsBronzeCoffer(oid);
    private static float DistanceToHitbox(ReplayActorState player, ReplayActorState actor)
        => Vector2.Distance(new(player.Position.X, player.Position.Z), new(actor.Position.X, actor.Position.Z))
            - player.HitboxRadius - actor.HitboxRadius;

    internal static int[] Pathfind(ushort[] rooms, int start, int destination)
    {
        if (rooms.Length != 25 || start is < 0 or >= 25 || destination is < 0 or >= 25 || start == destination) return [];
        var previous = Enumerable.Repeat(-1, 25).ToArray();
        var queue = new Queue<int>();
        queue.Enqueue(start);
        previous[start] = start;
        while (queue.TryDequeue(out var current))
        {
            if (current == destination)
            {
                var path = new List<int>();
                for (var i = destination; i != start; i = previous[i]) path.Add(i);
                path.Reverse();
                return path.ToArray();
            }
            for (var direction = 0; direction < 4; ++direction)
            {
                if ((rooms[current] & (1 << direction)) == 0) continue;
                var next = current + new[] { -5, 5, -1, 1 }[direction];
                // Reject corrupt maps and horizontal row wrapping before visiting array cells.
                if (next is < 0 or >= 25 || direction >= 2 && next / 5 != current / 5
                    || rooms[next] == 0 || previous[next] >= 0) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        return [];
    }
}
