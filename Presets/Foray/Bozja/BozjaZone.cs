using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

// A tower join is an intersection of inverted circles, including the one-circle
// case. It must use the existing finite sampler, rather than direct safe goals.
internal sealed record BozjaZone(string Key, string Component, DateTime Activation,
    GeometryTemplate? Shape, Vector3 Origin, float Rotation, Vector3[]? Join = null,
    float JoinRadius = 6, bool Goal = false, bool Obstacle = false, Vector3[]? Union = null)
{
    internal float Distance(float x, float z)
    {
        if (Shape is BozjaGauntletWindTemplate gauntletWind) return gauntletWind.Distance(x, z);
        if (Shape is BozjaGauntletArenaTemplate gauntletArena) return gauntletArena.Distance(x, z);
        if (Shape is BozjaDiabloOrbsTemplate orbs) return orbs.Distance(x, z);
        if (Shape is DonutTemplate donut)
        {
            var distanceFromCenter = MathF.Sqrt((x - Origin.X) * (x - Origin.X) + (z - Origin.Z) * (z - Origin.Z));
            return MathF.Max(distanceFromCenter - donut.OuterRadius, donut.InnerRadius - distanceFromCenter);
        }
        if (Shape is BozjaOrderTemplate order) return order.Distance(x, z);
        if (Shape is BozjaQueenChessTemplate chess) return chess.Distance(x, z);
        if (Shape is BozjaSeekerShelterTemplate seekerShelter) return seekerShelter.Distance(x, z);
        if (Shape is BozjaSeekerStackTemplate seekerStack) return seekerStack.Distance(x, z);
        if (Shape is BozjaPhantomShelterTemplate shelter) return shelter.Distance(x, z);
        if (Shape is BozjaPhantomRectTemplate rectangleZone) return rectangleZone.Distance(x, z);
        if (Shape is BozjaDahuClawsTemplate claws) return claws.Distance(x, z);
        if (Shape is CircleTemplate singleCircle && Union == null)
            return MathF.Sqrt((x - Origin.X) * (x - Origin.X) + (z - Origin.Z) * (z - Origin.Z)) - singleCircle.Radius;
        if (Union != null && Shape is CircleTemplate circle)
            return Union.Select(p => MathF.Sqrt((x - p.X) * (x - p.X) + (z - p.Z) * (z - p.Z)) - circle.Radius)
                .DefaultIfEmpty(float.PositiveInfinity).Min();
        if (Shape is BozjaMeteorTemplate meteor) return meteor.Geometry.ForbiddenDistance(x, z);
        if (Shape is BozjaCapsuleTemplate capsule)
            return capsule.Distance(Origin, Rotation, new(x, Origin.Y, z));
        if (Shape is RayRectangleTemplate ray)
        {
            var (sin, cos) = Math.SinCos(Rotation);
            var dx = x - Origin.X;
            var dz = z - Origin.Z;
            var along = dx * (float)sin + dz * (float)cos;
            var side = dx * (float)cos - dz * (float)sin;
            return MathF.Max(MathF.Max(along - ray.Length, -along),
                MathF.Max(side - ray.MaxLocalX, ray.MinLocalX - side));
        }
        if (Shape is RectangleTemplate rectangle)
        {
            var (sin, cos) = Math.SinCos(Rotation);
            var dx = x - Origin.X;
            var dz = z - Origin.Z;
            var along = dx * (float)sin + dz * (float)cos;
            var side = dx * (float)cos - dz * (float)sin;
            return MathF.Max(MathF.Abs(along) - rectangle.Length * .5f,
                MathF.Abs(side) - rectangle.Width * .5f);
        }
        if (Shape is FanTemplate fan)
        {
            var half = fan.AngleRadians * .5f;
            var factor = half > MathF.PI * .5f ? -1f : 1f;
            var (leftSin, leftCos) = Math.SinCos(Rotation + half);
            var (rightSin, rightCos) = Math.SinCos(Rotation - half);
            var dx = x - Origin.X;
            var dz = z - Origin.Z;
            var left = dx * (factor * (float)leftCos) + dz * (-factor * (float)leftSin);
            var right = dx * (-factor * (float)rightCos) + dz * (factor * (float)rightSin);
            var side = factor * (left > right ? left : right);
            var outer = MathF.Sqrt(dx * dx + dz * dz) - fan.Radius;
            return outer > side ? outer : side;
        }
        var distance = float.NegativeInfinity;
        var minAbs = float.MaxValue;
        var insideCount = 0;
        foreach (var center in Join ?? [])
        {
            var dx = x - center.X;
            var dz = z - center.Z;
            var d = JoinRadius - MathF.Sqrt(dx * dx + dz * dz);
            distance = MathF.Max(distance, d);
            if (d >= 0) ++insideCount;
            minAbs = MathF.Min(minAbs, MathF.Abs(d));
        }
        // Raw SDOutsideOfUnion is kept separate from the bridge's accepted
        // goal union. Do not reuse the adapted geometry as its source oracle.
        if (Goal && Join is { Length: > 1 }) return insideCount == 1 ? minAbs : -minAbs;
        return distance;
    }

    internal IEnumerable<IGeometryShape> Convert(Vector3 center, float radius)
    {
        if (Shape is BozjaGauntletWindTemplate gauntletWind) return gauntletWind.Convert(center, radius);
        // The source ring has a safe inner square. The old bridge's custom
        // polygon converter dropped holes; keep the actual source geometry.
        if (Shape is BozjaGauntletArenaTemplate gauntletArena) return [gauntletArena.Resolve(center, 0, null)];
        if (Shape is BozjaDiabloOrbsTemplate orbs) return orbs.Convert(center, radius);
        if (Shape is BozjaOrderTemplate order)
            return order.Inverted ? GeometryGridSampler.Sample(center, radius, order.Distance)
                : order.Resolve(Origin with { Y = center.Y }, 0, null).GetContours().Select(c => (IGeometryShape)new WorldPolygonGeometry(c));
        if (Shape is BozjaQueenChessTemplate chess) return chess.Convert(center, radius);
        if (Shape is DungeonCrossTemplate cross)
        {
            var (sin, cos) = Math.SinCos(Rotation);
            var rotation = MathF.Atan2((float)sin, (float)cos);
            return [new RectangleGeometry(Origin with { Y = center.Y }, rotation, 2 * cross.HalfWidth, 2 * cross.Length),
                new RectangleGeometry(Origin with { Y = center.Y }, rotation + MathF.PI / 2, 2 * cross.HalfWidth, 2 * cross.Length)];
        }
        if (Shape is BozjaSeekerShelterTemplate seekerShelter) return seekerShelter.Convert(center, radius);
        if (Shape is BozjaSeekerStackTemplate seekerStack) return seekerStack.Convert(center.Y);
        if (Shape is BozjaPhantomShelterTemplate shelter) return shelter.Convert(center, radius);
        if (Shape is BozjaPhantomArenaTemplate arena) return arena.Convert(center.Y);
        if (Union != null && Shape is CircleTemplate circle)
            return Union.Select(p => (IGeometryShape)new CircleGeometry(p with { Y = center.Y }, circle.Radius));
        if (Shape is BozjaDahuClawsTemplate claws) return claws.Convert(center, radius);
        if (Shape is BozjaMeteorTemplate) return GeometryGridSampler.Sample(center, radius, Distance);
        // An unclamped source Asin can produce NaN for a nearby bait buddy.
        // The existing bridge samples that empty SDF; it does not invent a cone.
        if (Shape is FanTemplate fan && !float.IsFinite(fan.AngleRadians)) return [];
        if (Shape is FanTemplate cone) return ConvertCone(cone, center, radius);
        return Shape is RayRectangleTemplate { Length: <= 0 } ? GeometryGridSampler.Sample(center, radius, Distance)
            : Join == null ? [Shape!.Resolve(Origin with { Y = center.Y }, Rotation, null)]
            : Goal ? Join.Select(p => (IGeometryShape)new CircleGeometry(p with { Y = center.Y }, JoinRadius))
            : GeometryGridSampler.Sample(center, radius, Distance);
    }

    private IEnumerable<IGeometryShape> ConvertCone(FanTemplate cone, Vector3 center, float sampleRadius)
        => TryDirectCone(cone, center.Y) is { } direct ? [direct] : GeometryGridSampler.Sample(center, sampleRadius, Distance);

    internal IGeometryShape? TryDirectCone(FanTemplate cone, float y)
    {
        var half = cone.AngleRadians * .5f;
        var factor = half > MathF.PI * .5f ? -1f : 1f;
        var (leftSin, leftCos) = Math.SinCos(Rotation + half);
        var (rightSin, rightCos) = Math.SinCos(Rotation - half);
        var nlX = factor * (float)leftCos;
        var nlZ = -factor * (float)leftSin;
        var nrX = -factor * (float)rightCos;
        var nrZ = factor * (float)rightSin;
        var u1x = -factor * nlZ;
        var u1z = factor * nlX;
        var u2x = factor * nrZ;
        var u2z = -factor * nrX;
        var bisectX = u1x + u2x;
        var bisectZ = u1z + u2z;
        // Exactly 180 degrees cannot recover a bisector in the existing
        // bridge, so it uses the finite SDF sampler instead of a direct cone.
        if (bisectX * bisectX + bisectZ * bisectZ <= 1e-12f)
            return null;
        if (factor < 0)
        {
            bisectX = -bisectX;
            bisectZ = -bisectZ;
        }
        var direction = MathF.Atan2(bisectX, bisectZ);
        var recovered = MathF.Atan2(u1x, u1z) - direction;
        while (recovered < -MathF.PI) recovered += MathF.Tau;
        while (recovered > MathF.PI) recovered -= MathF.Tau;
        recovered = MathF.Abs(recovered);
        return recovered > 1e-4f && recovered <= MathF.PI && cone.Radius > 0
            ? new FanGeometry(Origin with { Y = y }, direction, cone.Radius, MathF.Min(2 * recovered, 2 * MathF.PI))
            : null;
    }
}
