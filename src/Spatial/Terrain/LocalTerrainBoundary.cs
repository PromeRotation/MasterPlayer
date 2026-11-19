using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Spatial.Terrain;

internal sealed class LocalTerrainBoundary : GeometryShape, ILocalViewBoundary
{
    private readonly TerrainCellKind[] cells;
    private readonly float[] heights;
    private readonly IReadOnlyList<IReadOnlyList<Vector3>> contours;
    internal IReadOnlyList<(GeometryBounds Bounds, TerrainCellKind Kind)> BlockedRuns { get; }
    internal const float CELL_SIZE = .75f;
    internal float CellSize { get; }
    internal float MinX { get; }
    internal float MinZ { get; }
    internal int Side { get; }
    public float ViewRadius { get; }
    public ArenaBoundaryIndex TraversalIndex { get; }
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    internal int WalkableCount { get; }

    internal LocalTerrainBoundary(Vector3 center, int side, TerrainCellKind[] cells, float[] heights, float cellSize = CELL_SIZE) : base(center)
    {
        CellSize = cellSize;
        Side = side;
        ViewRadius = side * CellSize / 2;
        MinX = center.X - ViewRadius;
        MinZ = center.Z - ViewRadius;
        this.cells = (TerrainCellKind[])cells.Clone();
        this.heights = (float[])heights.Clone();
        WalkableCount = cells.Count(c => c == TerrainCellKind.Walkable);
        var runs = new List<(GeometryBounds, TerrainCellKind)>();
        for (var z = 0; z < side; ++z)
            for (var x = 0; x < side;)
            {
                var first = x;
                var kind = Cell(x++, z);
                if (kind is not (TerrainCellKind.Wall or TerrainCellKind.Drop)) continue;
                while (x < side && Cell(x, z) == kind) ++x;
                runs.Add((new(MinX + first * CellSize, MinZ + z * CellSize, MinX + x * CellSize, MinZ + (z + 1) * CellSize), kind));
            }
        BlockedRuns = runs.ToArray();
        var edges = new List<IReadOnlyList<Vector3>>();
        for (var z = 0; z < side; ++z)
            for (var x = 0; x < side; ++x)
            {
                if (Cell(x, z) != TerrainCellKind.Walkable) continue;
                var a = new Vector3(MinX + x * CellSize, heights[z * side + x], MinZ + z * CellSize);
                if (Cell(x, z - 1) != TerrainCellKind.Walkable) edges.Add(new[] { a, a + Vector3.UnitX * CellSize });
                if (Cell(x + 1, z) != TerrainCellKind.Walkable) edges.Add(new[] { a + Vector3.UnitX * CellSize, a + new Vector3(CellSize, 0, CellSize) });
                if (Cell(x, z + 1) != TerrainCellKind.Walkable) edges.Add(new[] { a + new Vector3(CellSize, 0, CellSize), a + Vector3.UnitZ * CellSize });
                if (Cell(x - 1, z) != TerrainCellKind.Walkable) edges.Add(new[] { a + Vector3.UnitZ * CellSize, a });
            }
        // Two-point contours describe only the union's exposed edges, including holes.
        contours = edges.ToArray();
        TraversalIndex = new(contours);
    }

    internal TerrainCellKind Cell(int x, int z) => x >= 0 && z >= 0 && x < Side && z < Side ? cells[z * Side + x] : TerrainCellKind.Unknown;
    internal float Height(int x, int z) => heights[z * Side + x];

    internal LocalTerrainBoundary WithObstructions(IEnumerable<TerrainObstruction> obstructions)
    {
        var corrected = (TerrainCellKind[])cells.Clone();
        var modified = false;
        const float RADIUS = .3f;
        foreach (var obstruction in obstructions)
        {
            var point = obstruction.Position;
            var minX = Math.Max(0, (int)MathF.Floor((point.X - RADIUS - MinX) / CellSize));
            var maxX = Math.Min(Side - 1, (int)MathF.Floor((point.X + RADIUS - MinX) / CellSize));
            var minZ = Math.Max(0, (int)MathF.Floor((point.Z - RADIUS - MinZ) / CellSize));
            var maxZ = Math.Min(Side - 1, (int)MathF.Floor((point.Z + RADIUS - MinZ) / CellSize));
            for (var z = minZ; z <= maxZ; ++z)
                for (var x = minX; x <= maxX; ++x)
                {
                    var index = z * Side + x;
                    if (corrected[index] != TerrainCellKind.Walkable || MathF.Abs(heights[index] - point.Y) > .8f) continue;
                    var dx = point.X - Math.Clamp(point.X, MinX + x * CellSize, MinX + (x + 1) * CellSize);
                    var dz = point.Z - Math.Clamp(point.Z, MinZ + z * CellSize, MinZ + (z + 1) * CellSize);
                    if (dx * dx + dz * dz > RADIUS * RADIUS) continue;
                    corrected[index] = obstruction.Kind;
                    modified = true;
                }
        }
        return modified ? new(Position, Side, corrected, heights, CellSize) : this;
    }

    internal bool TryGetFloorPosition(Vector3 position, out Vector3 floor)
    {
        floor = default;
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Z)) return false;
        var x = (int)MathF.Floor((position.X - MinX) / CellSize);
        var z = (int)MathF.Floor((position.Z - MinZ) / CellSize);
        var onXEdge = MathF.Abs(position.X - (MinX + x * CellSize)) <= .001f;
        var onZEdge = MathF.Abs(position.Z - (MinZ + z * CellSize)) <= .001f;
        for (var dz = 0; dz >= (onZEdge ? -1 : 0); --dz)
            for (var dx = 0; dx >= (onXEdge ? -1 : 0); --dx)
            {
                if (Cell(x + dx, z + dz) != TerrainCellKind.Walkable || !float.IsFinite(Height(x + dx, z + dz))) continue;
                floor = new(position.X, Height(x + dx, z + dz), position.Z);
                return true;
            }
        return false;
    }

    internal IEnumerable<(Vector3 Center, TerrainCellKind Kind)> BlockedCells()
    {
        for (var z = 0; z < Side; ++z)
            for (var x = 0; x < Side; ++x)
                if (Cell(x, z) is TerrainCellKind.Wall or TerrainCellKind.Drop)
                    yield return (new(MinX + (x + .5f) * CellSize, Position.Y, MinZ + (z + .5f) * CellSize), Cell(x, z));
    }

    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var x = (int)MathF.Floor((point.X - MinX) / CellSize);
        var z = (int)MathF.Floor((point.Z - MinZ) / CellSize);
        if (Cell(x, z) == TerrainCellKind.Walkable) return true;
        // Include shared grid edges without opening a passage through blocked cells.
        var ex = MathF.Abs(point.X - (MinX + x * CellSize)) <= MathF.Min(MathF.Max(epsilon, 0), .001f);
        var ez = MathF.Abs(point.Z - (MinZ + z * CellSize)) <= MathF.Min(MathF.Max(epsilon, 0), .001f);
        return ex && Cell(x - 1, z) == TerrainCellKind.Walkable || ez && Cell(x, z - 1) == TerrainCellKind.Walkable
            || ex && ez && Cell(x - 1, z - 1) == TerrainCellKind.Walkable;
    }

    public override GeometryBounds GetBounds() => new(MinX, MinZ, MinX + Side * CellSize, MinZ + Side * CellSize);
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => contours;
}
