using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

// Native collision queries stay on Framework.Update. No game pointers enter the published map.
internal sealed class GameTerrainProbe(ITerrainRaycaster raycaster) : ITerrainProbe
{
    private const float MAX_RISE = .65f;
    private const float MAX_DROP = .8f;
    private const float BODY_RADIUS = .3f;
    private static readonly Vector2[] Corners = [new(-.375f, -.375f), new(.375f, -.375f), new(.375f, .375f), new(-.375f, .375f)];
    private readonly Dictionary<Vector3, (bool Valid, Vector3 Floor)> supportCache = [];
    private readonly Dictionary<Vector3, (bool Hit, Vector3 Floor, Vector3 Normal)> hitCache = [];
    private bool scanning;

    public bool IsAvailable => raycaster.IsAvailable;

    public void BeginScanBatch()
    {
        supportCache.Clear(); hitCache.Clear();
        scanning = true;
    }

    public void EndScanBatch()
    {
        scanning = false;
        supportCache.Clear(); hitCache.Clear();
    }

    public TerrainCellKind Sample(Vector3 reference, out Vector3 floor)
    {
        if (!Finite(reference)) { floor = default; return TerrainCellKind.Unknown; }
        if (!TryGetGround(reference, out floor)) return TerrainCellKind.Drop;
        foreach (var corner in Corners)
        {
            var edge = floor + new Vector3(corner.X, 0, corner.Y);
            if (!TryGetGround(edge, out var support)) return TerrainCellKind.Drop;
            if (!BodyClear(floor, support)) return TerrainCellKind.Wall;
        }
        if (Hits(floor + Vector3.UnitY * .15f, floor + Vector3.UnitY * 1.65f)) return TerrainCellKind.Wall;
        return TerrainCellKind.Walkable;
    }

    public bool CanTraverse(Vector3 from, Vector3 to)
        => CanTraverse(from, to, out _);

    public bool CanTraverse(Vector3 from, Vector3 to, out TerrainObstruction? obstruction)
    {
        obstruction = null;
        if (!Finite(from) || !Finite(to)) return false;
        if (!HeadroomClear(from))
        {
            obstruction = new(from, TerrainCellKind.Wall, from, from);
            return false;
        }
        var distance = Vector2.Distance(new(from.X, from.Z), new(to.X, to.Z));
        var steps = Math.Max(1, (int)MathF.Ceiling(distance / .3f));
        var runStart = from;
        var previous = from;
        for (var i = 1; i <= steps; ++i)
        {
            var expected = Vector3.Lerp(from, to, (float)i / steps);
            expected.Y = previous.Y;
            if (!TryGetGround(expected, out var next))
            {
                obstruction = new(expected, TerrainCellKind.Drop, previous, expected);
                return false;
            }
            if (!HeadroomClear(next))
            {
                obstruction = new(next, TerrainCellKind.Wall, next, next);
                return false;
            }
            var first = previous - runStart;
            var second = next - previous;
            if (Vector3.Cross(first, second).LengthSquared() > first.LengthSquared() * second.LengthSquared() * 1e-8f)
            {
                if (!BodyClear(runStart, previous, out obstruction)) return false;
                runStart = previous;
            }
            previous = next;
        }
        // Adjacent collinear support segments have the same six body rays.
        // Keep every ground sample, and split again at any slope/step change.
        return BodyClear(runStart, previous, out obstruction);
    }

    public bool TryGetGround(Vector3 reference, out Vector3 floor)
    {
        if (!Finite(reference)) { floor = default; return false; }
        if (scanning && supportCache.TryGetValue(reference, out var cached))
        {
            floor = cached.Floor;
            return cached.Valid;
        }
        var valid = GroundHit(reference, out floor, out var normal);
        if (valid && normal.LengthSquared() < .000001f) valid = MeasureSurfaceNormal(floor, out normal);
        if (valid)
        {
            normal = Vector3.Normalize(normal);
            valid = Finite(normal) && normal.Y >= .65f;
        }
        if (scanning) supportCache[reference] = (valid, floor);
        return valid;
    }

    private bool GroundHit(Vector3 reference, out Vector3 floor, out Vector3 normal)
    {
        if (scanning && hitCache.TryGetValue(reference, out var cached))
        {
            floor = cached.Floor;
            normal = cached.Normal;
            return cached.Hit;
        }
        var origin = reference + Vector3.UnitY * MAX_RISE;
        var hit = raycaster.Cast(origin, -Vector3.UnitY, MAX_RISE + MAX_DROP, out var point, out normal)
            && Finite(point) && Finite(normal);
        floor = hit ? point : default;
        hit &= floor.Y <= reference.Y + MAX_RISE && floor.Y >= reference.Y - MAX_DROP;
        if (scanning) hitCache[reference] = (hit, floor, normal);
        return hit;
    }

    private bool MeasureSurfaceNormal(Vector3 floor, out Vector3 normal)
    {
        normal = default;
        // FFCS does not populate RaycastHit.Normal for every collider type.
        // Measure actual neighbouring support instead of assuming a flat surface.
        const float STEP = .1f;
        if (!GroundHit(floor - Vector3.UnitX * STEP, out var west, out _)
            || !GroundHit(floor + Vector3.UnitX * STEP, out var east, out _)
            || !GroundHit(floor - Vector3.UnitZ * STEP, out var north, out _)
            || !GroundHit(floor + Vector3.UnitZ * STEP, out var south, out _)) return false;
        normal = new(-(east.Y - west.Y) / (2 * STEP), 1, -(south.Y - north.Y) / (2 * STEP));
        return Finite(normal);
    }

    private bool BodyClear(Vector3 from, Vector3 to)
        => BodyClear(from, to, out _);

    private bool HeadroomClear(Vector3 floor)
        => !Hits(floor + Vector3.UnitY * .15f, floor + Vector3.UnitY * 1.65f);

    private bool BodyClear(Vector3 from, Vector3 to, out TerrainObstruction? obstruction)
    {
        obstruction = null;
        var delta = new Vector2(to.X - from.X, to.Z - from.Z);
        var side = delta.LengthSquared() > .00001f ? Vector2.Normalize(new(-delta.Y, delta.X)) * BODY_RADIUS : Vector2.UnitX * BODY_RADIUS;
        var nearest = float.PositiveInfinity;
        foreach (var shift in (ReadOnlySpan<float>)[0f, -1f, 1f])
        {
            var offset = new Vector3(side.X * shift, .35f, side.Y * shift);
            foreach (var height in (ReadOnlySpan<float>)[0, 1])
            {
                var start = from + offset + Vector3.UnitY * height;
                var end = to + offset + Vector3.UnitY * height;
                var direction = end - start;
                var length = direction.Length();
                if (length <= .0001f || !raycaster.Cast(start, direction / length, length + BODY_RADIUS + .02f, out var point, out _)) continue;
                if (!Finite(point)) throw new InvalidDataException("实时碰撞查询返回无效命中点");
                var distance = Vector3.DistanceSquared(start, point);
                if (distance >= nearest) continue;
                nearest = distance;
                var fraction = Math.Clamp(MathF.Sqrt(distance) / length, 0, 1);
                point.Y = float.Lerp(from.Y, to.Y, fraction);
                obstruction = new(point, TerrainCellKind.Wall, from, to);
            }
        }
        return obstruction == null;
    }

    private bool Hits(Vector3 from, Vector3 to)
    {
        var delta = to - from;
        var length = delta.Length();
        return length > .0001f && raycaster.Cast(from, delta / length, length + .02f, out _, out _);
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
