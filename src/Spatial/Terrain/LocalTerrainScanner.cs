using System.Diagnostics;
using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal sealed class LocalTerrainScanner(ITerrainProbe probe)
{
    private readonly PriorityQueue<(int Index, Vector3 Reference), float> pending = new();
    private TerrainCellKind[] cells = [];
    private float[] heights = [];
    private DateTime[] sampledAt = [];
    private bool[] queued = [];
    private int side;
    private Vector3 center, priorityOrigin;
    private DateTime nextRefreshCheck, publishedAt;
    private bool dirty;
    internal LocalTerrainBoundary? Current { get; private set; }
    internal bool Complete => pending.Count == 0;
    internal int SampledCount { get; private set; }
    internal double LastScanMilliseconds { get; private set; }

    internal void Reset(Vector3 player, float radius)
    {
        Clear();
        Start(player, SizeForRadius(radius));
    }

    private static int SizeForRadius(float radius)
    {
        radius = float.IsFinite(radius) ? Math.Clamp(radius, 8, 30) : 20;
        return 2 * (int)MathF.Ceiling(radius / LocalTerrainBoundary.CELL_SIZE) + 1;
    }

    internal void Clear()
    {
        pending.Clear(); cells = []; heights = []; sampledAt = []; queued = []; Current = null;
        SampledCount = 0; nextRefreshCheck = publishedAt = default;
        dirty = false;
        LastScanMilliseconds = 0;
    }

    internal void Update(Vector3 player, float radius, DateTime now, int maxCells = 128, double budgetMilliseconds = 1)
    {
        var frameStart = Stopwatch.GetTimestamp();
        var nextSide = SizeForRadius(radius);
        var moved = DistanceSquared(player, center) > 16;
        var previousFloor = default(Vector3);
        var knownFloor = Current != null && Current.TryGetFloorPosition(player, out previousFloor);
        var changedLayer = knownFloor && MathF.Abs(player.Y - previousFloor.Y) > 1;
        if (cells.Length == 0 || side != nextSide || changedLayer) Start(player, nextSide);
        else if (moved && knownFloor) Shift(player, now);
        else if (moved) Start(player, nextSide);
        if (DistanceSquared(player, priorityOrigin) > LocalTerrainBoundary.CELL_SIZE * LocalTerrainBoundary.CELL_SIZE)
        {
            priorityOrigin = player;
            var entries = pending.UnorderedItems.Select(item => item.Element).ToArray();
            pending.Clear();
            foreach (var entry in entries) pending.Enqueue(entry, Priority(entry.Index));
        }
        if (Complete && now >= nextRefreshCheck)
        {
            QueueRefresh(now);
            nextRefreshCheck = now.AddMilliseconds(500);
        }
        if (Complete || !probe.IsAvailable)
        {
            LastScanMilliseconds = Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds;
            return;
        }
        var queryStart = Stopwatch.GetTimestamp();
        try
        {
            probe.BeginScanBatch();
            var sampled = 0;
            while (pending.Count != 0 && sampled < maxCells && (sampled == 0 || Stopwatch.GetElapsedTime(queryStart).TotalMilliseconds < budgetMilliseconds))
            {
                var (index, reference) = pending.Dequeue();
                queued[index] = false;
                ++sampled; ++SampledCount;
                var x = index % side;
                var z = index / side;
                var candidate = CellPosition(x, z, reference.Y);
                var kind = probe.Sample(candidate, out var floor);
                if (kind == TerrainCellKind.Walkable && Vector3.DistanceSquared(reference, floor) > 1e-8f
                    && !probe.CanTraverse(reference, floor)) kind = TerrainCellKind.Wall;
                var publishedHeight = Current!.Height(x, z);
                dirty |= cells[index] != kind || kind == TerrainCellKind.Walkable
                    && (!float.IsFinite(publishedHeight) || MathF.Abs(publishedHeight - floor.Y) > .02f);
                cells[index] = kind;
                sampledAt[index] = now;
                heights[index] = kind == TerrainCellKind.Walkable ? floor.Y : float.NaN;
                if (kind != TerrainCellKind.Walkable) continue;
                Enqueue(x - 1, z, floor); Enqueue(x + 1, z, floor); Enqueue(x, z - 1, floor); Enqueue(x, z + 1, floor);
            }
            if (dirty && (Complete || Current!.WalkableCount == 0 || now >= publishedAt.AddMilliseconds(50)))
            {
                Current = new(center, side, cells, heights);
                publishedAt = now;
                dirty = false;
            }
        }
        finally
        {
            probe.EndScanBatch();
            LastScanMilliseconds = Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds;
        }
    }

    private void Start(Vector3 player, int size)
    {
        var cellSize = LocalTerrainBoundary.CELL_SIZE;
        center = new(MathF.Round(player.X / cellSize) * cellSize, player.Y, MathF.Round(player.Z / cellSize) * cellSize);
        priorityOrigin = player;
        side = size; SampledCount = 0;
        cells = new TerrainCellKind[side * side]; queued = new bool[cells.Length]; pending.Clear();
        heights = Enumerable.Repeat(float.NaN, cells.Length).ToArray();
        sampledAt = new DateTime[cells.Length];
        Enqueue(side / 2, side / 2, player);
        // A conservative cell at a nearby wall can fail although a neighbour is reachable.
        Enqueue(side / 2 - 1, side / 2, player); Enqueue(side / 2 + 1, side / 2, player);
        Enqueue(side / 2, side / 2 - 1, player); Enqueue(side / 2, side / 2 + 1, player);
        Current = new(center, side, cells, heights);
        dirty = false;
        nextRefreshCheck = publishedAt = default;
    }

    private void Shift(Vector3 player, DateTime now)
    {
        var oldCenter = center;
        var oldCells = cells; var oldHeights = heights; var oldSamples = sampledAt;
        Start(player, side);
        pending.Clear(); Array.Clear(queued);
        var offsetX = (int)MathF.Round((center.X - oldCenter.X) / LocalTerrainBoundary.CELL_SIZE);
        var offsetZ = (int)MathF.Round((center.Z - oldCenter.Z) / LocalTerrainBoundary.CELL_SIZE);
        for (var z = 0; z < side; ++z)
            for (var x = 0; x < side; ++x)
            {
                var oldX = x + offsetX; var oldZ = z + offsetZ;
                if (!InRange(x, z) || oldX < 0 || oldZ < 0 || oldX >= side || oldZ >= side) continue;
                var index = z * side + x;
                var oldIndex = oldZ * side + oldX;
                cells[index] = oldCells[oldIndex];
                heights[index] = oldHeights[oldIndex];
                sampledAt[index] = oldSamples[oldIndex];
            }
        // Preserve sampled overlap and extend only the newly exposed frontier.
        for (var z = 0; z < side; ++z)
            for (var x = 0; x < side; ++x)
            {
                if (cells[z * side + x] != TerrainCellKind.Walkable) continue;
                var floor = CellPosition(x, z, heights[z * side + x]);
                Enqueue(x - 1, z, floor); Enqueue(x + 1, z, floor); Enqueue(x, z + 1, floor); Enqueue(x, z - 1, floor);
            }
        Current = new(center, side, cells, heights);
        publishedAt = now;
    }

    private void QueueRefresh(DateTime now)
    {
        for (var z = 0; z < side; ++z)
            for (var x = 0; x < side; ++x)
            {
                var index = z * side + x;
                var kind = cells[index];
                if (kind == TerrainCellKind.Unknown) continue;
                var near = Priority(index) <= 36;
                var interval = near ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);
                if (near && kind != TerrainCellKind.Walkable) interval = TimeSpan.FromMilliseconds(500);
                if (now - sampledAt[index] < interval) continue;
                if (kind == TerrainCellKind.Walkable)
                {
                    Enqueue(x, z, CellPosition(x, z, heights[index]), true);
                    continue;
                }
                foreach (var (dx, dz) in (ReadOnlySpan<(int, int)>)[(-1, 0), (1, 0), (0, -1), (0, 1)])
                {
                    var nx = x + dx; var nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= side || nz >= side || cells[nz * side + nx] != TerrainCellKind.Walkable) continue;
                    Enqueue(x, z, CellPosition(nx, nz, heights[nz * side + nx]), true);
                    break;
                }
            }
    }

    private Vector3 CellPosition(int x, int z, float height)
        => new(center.X + (x - side / 2) * LocalTerrainBoundary.CELL_SIZE, height, center.Z + (z - side / 2) * LocalTerrainBoundary.CELL_SIZE);

    private float Priority(int index) => DistanceSquared(CellPosition(index % side, index / side, 0), priorityOrigin);

    private static float DistanceSquared(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).LengthSquared();

    private bool InRange(int x, int z)
    {
        if (x < 0 || z < 0 || x >= side || z >= side) return false;
        var dx = x - side / 2;
        var dz = z - side / 2;
        return dx * dx + dz * dz <= (side / 2) * (side / 2);
    }

    private void Enqueue(int x, int z, Vector3 reference, bool refresh = false)
    {
        if (!InRange(x, z)) return;
        var index = z * side + x;
        if (queued[index] || !refresh && cells[index] != TerrainCellKind.Unknown) return;
        queued[index] = true;
        pending.Enqueue((index, reference), Priority(index));
    }
}
