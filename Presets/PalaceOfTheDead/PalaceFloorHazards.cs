using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed class PalaceFloorHazards(
    byte dungeonId = 1,
    int maximumFloor = 200,
    IReadOnlyDictionary<(int FloorSet, byte Tileset), PalaceFloorRoom[]>? roomData = null,
    IReadOnlyDictionary<ushort, Vector2[]>? trapData = null)
{
    private readonly IReadOnlyDictionary<(int FloorSet, byte Tileset), PalaceFloorRoom[]> roomsByFloor = roomData ?? PalaceFloorData.Rooms;
    private readonly IReadOnlyDictionary<ushort, Vector2[]> trapsByTerritory = trapData ?? PalaceFloorData.Traps;
    private readonly List<Vector2> ignoredTraps = [];
    private readonly HashSet<Vector2> manuallyIgnoredTraps = [];
    private readonly HashSet<ulong> seenIndicators = [];
    private ReplayDeepDungeonState? state;
    private ReplayDeepDungeonState? lastMap;
    private ReplayDeepDungeonState? previousFloor;
    private ushort territory;
    private bool trapsHidden = true;
    private bool awaitingMap;
    internal bool BetweenFloors => awaitingMap;
    internal bool HasMap => state != null && !awaitingMap;

    internal bool IgnoreNearestTrap(Vector3 player)
    {
        if (state == null || awaitingMap || !trapsByTerritory.TryGetValue(territory, out var traps)) return false;
        var candidates = traps.Where(p => !manuallyIgnoredTraps.Contains(p)).ToArray();
        if (candidates.Length == 0) return false;
        var position = candidates.MinBy(p => Vector2.DistanceSquared(p, new(player.X, player.Z)));
        manuallyIgnoredTraps.Add(position);
        ignoredTraps.Add(position);
        return true;
    }

    internal void Synchronize(ReplayDeepDungeonState? snapshot, IEnumerable<ReplayActorState> observations, ushort currentTerritory)
    {
        if (snapshot == null || snapshot.DungeonId != dungeonId || snapshot.Floor < 1
            || snapshot.Floor > maximumFloor || snapshot.Rooms.Length != 25)
        {
            // HoH/EO can observe hot indicator births before their map is ready.
            // A missing sample is not AutoClear.ClearState. Keep creation history
            // until a real floor/territory transition or 7248 proves invalidation.
            if (dungeonId != 1)
            {
                if (territory != 0 && territory != currentTerritory) Reset();
                territory = currentTerritory;
                state = null;
            }
            else if (!awaitingMap) Reset();
            else state = null;
            return;
        }
        if (awaitingMap)
        {
            if (previousFloor != null && previousFloor.Floor == snapshot.Floor
                && previousFloor.Tileset == snapshot.Tileset && previousFloor.Rooms.SequenceEqual(snapshot.Rooms)) return;
            awaitingMap = false;
        }
        // A Boss map is also a real map change. Release transfer isolation before
        // excluding its ordinary-floor hazards so Boss mechanics can render.
        if (snapshot.Floor % 10 == 0 || dungeonId is 3 or 4 && snapshot.Floor == 99)
        {
            Reset();
            lastMap = snapshot;
            return;
        }
        var prior = state ?? (dungeonId != 1 ? lastMap : null);
        if (prior != null && (prior.Floor != snapshot.Floor || territory != currentTerritory))
            Reset(preserveManual: territory == currentTerritory);
        if (snapshot.TrapsHidden is { } hidden && (state == null || state.TrapsHidden != snapshot.TrapsHidden))
            trapsHidden = hidden;
        state = snapshot;
        lastMap = snapshot;
        territory = currentTerritory;
        if (dungeonId == 1)
            foreach (var actor in observations)
                if (actor.Oid == 0x1EA1F6 && seenIndicators.Add(actor.Id))
                    ObserveCreated(actor);
    }

    // HoH supplies actual Created events from its actor lifetime model. A cold
    // snapshot must not replay historical indicators, nor suppress same-ID births.
    internal void ObserveCreated(ReplayActorState actor)
    {
        if (actor.Oid == 0x1EA1F6 || dungeonId == 2 && actor.Oid == 0x1EA9A3)
            ignoredTraps.Add(new(actor.Position.X, actor.Position.Z));
    }

    internal void SystemLog(uint id)
    {
        if (id == 7248)
        {
            if (awaitingMap) return;
            previousFloor = lastMap;
            Reset(preserveManual: true);
            awaitingMap = true;
        }
        else if (id is 7255 or 7256) trapsHidden = false;
    }

    internal void Reset(bool preserveManual = false)
    {
        state = null;
        lastMap = null;
        if (!preserveManual) manuallyIgnoredTraps.Clear();
        ignoredTraps.Clear();
        ignoredTraps.AddRange(manuallyIgnoredTraps);
        seenIndicators.Clear();
        trapsHidden = true;
        awaitingMap = false;
    }

    internal IEnumerable<PalaceArea> Collect(DateTime now, ReplayActorState player, bool trapHints = true)
    {
        if (state == null || awaitingMap) yield break;
        var y = player.Position.Y;
        if (roomsByFloor.TryGetValue((state.Floor / 10 + 1, state.Tileset), out var rooms))
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
        if (trapHints && trapsHidden && trapsByTerritory.TryGetValue(territory, out var traps))
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
    }
}
