using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

internal enum BlundervilleCourse { None, FirstA, FirstB, Delivery, Ring, Final }

internal readonly record struct BlundervilleRouteNode(Vector3 Position, float ArrivalRadius = 1.2f, bool Jump = false);

/// <summary>
/// Verified course topology from twelve ARR recordings and the local w1ek layout/navmesh.
/// Nodes describe the course's lanes, not an assertion that a timed obstacle is safe.
/// Dynamic bridges and steps must additionally use the current collision and race state.
/// Height profiles below are drawing hints; they never replace a live floor probe.
/// </summary>
internal static partial class BlundervilleRoutes
{
    public static BlundervilleCourse FromDirectorStage(uint stage) => stage switch
    {
        0x4E2D => BlundervilleCourse.FirstA,
        0x4E2E => BlundervilleCourse.FirstB,
        0x4E2F => BlundervilleCourse.Ring,
        0x4E30 => BlundervilleCourse.Delivery,
        0x4E31 => BlundervilleCourse.Final,
        _ => BlundervilleCourse.None
    };

    public static BlundervilleCourse FromPosition(Vector3 position)
    {
        if (!Finite(position)) return BlundervilleCourse.None;
        if (position.X is > -224 and < -176 && position.Z is > -380 and < -95) return BlundervilleCourse.FirstA;
        if (position.X is > -22 and < 22 && position.Z is > -395 and < -95) return BlundervilleCourse.FirstB;
        if (position.X is > -252 and < -148 && position.Z is > 190 and < 286) return BlundervilleCourse.Delivery;
        if (position.X is > 165 and < 235 && position.Z is > -176 and < -95) return BlundervilleCourse.Ring;
        if (position.X is > -22 and < 22 && position.Z is > 112 and < 333) return BlundervilleCourse.Final;
        return BlundervilleCourse.None;
    }

    public static bool IsLoop(BlundervilleCourse course) => course == BlundervilleCourse.Ring;

    public static Vector3 VerifiedStart(BlundervilleCourse course) => course switch
    {
        BlundervilleCourse.FirstA => new(-200, 11.28f, -105),
        BlundervilleCourse.FirstB => new(0, 10.03f, -108),
        BlundervilleCourse.Delivery => new(-200, 3.01f, 277),
        BlundervilleCourse.Ring => new(215, 10, -138),
        BlundervilleCourse.Final => new(0, 4.24f, 323),
        _ => Vector3.Zero
    };

    public static Vector3 VerifiedFinish(BlundervilleCourse course) => course switch
    {
        BlundervilleCourse.FirstA => new(-200, 13.57f, -367),
        BlundervilleCourse.FirstB => new(0, 18.94f, -377),
        BlundervilleCourse.Delivery => new(-200, 5.42f, 252),
        BlundervilleCourse.Ring => new(215, 10, -138), // survival ring has no finish line
        BlundervilleCourse.Final => new(8.5f, 39.597f, 123.8f), // scoring range; the crown-center position is a later teleport
        _ => Vector3.Zero
    };

    public static IReadOnlyList<BlundervilleRouteNode> Nodes(BlundervilleCourse course, Vector3 start)
    {
        var side = start.X < (course == BlundervilleCourse.FirstA ? -200 : 0) ? -1f : 1f;
        return course switch
        {
            // All eight observed FirstA runs use this left approach. Keep the
            // unrecorded right approach disabled until its obstacle gaps are verified.
            BlundervilleCourse.FirstA => FirstANodes(-1),
            BlundervilleCourse.FirstB => FirstBNodes(side),
            BlundervilleCourse.Delivery => ReadOnly(N(-200, 6.03f, 252, 2f)),
            BlundervilleCourse.Ring => RingNodes,
            BlundervilleCourse.Final => FinalNodes(side),
            _ => Array.Empty<BlundervilleRouteNode>()
        };
    }

    // The high arrow bridge cannot be re-entered by steering through its edge.
    // The ramp rises inward along X. Its Z < -270.5 side is a wall, not an entrance.
    public static IReadOnlyList<BlundervilleRouteNode> FirstBRecovery(Vector3 player)
    {
        var side = player.X < 0 ? -1f : 1f;
        var nodes = new List<BlundervilleRouteNode>();
        if (player.Z < -270.5f || player.Y < 6.3f || MathF.Abs(player.X) > 13)
        {
            if (player.Z < -268.4f)
                nodes.Add(N(side * 13.4f, 6, player.Z, .4f));
            for (var z = player.Z + 20; z < -268.4f; z += 20)
                nodes.Add(N(side * 13.4f, 6, z, .4f));
            nodes.Add(N(side * 13.4f, 6, -268, .4f));
        }
        nodes.Add(N(side * 5, 10.03f, -268, .4f));
        nodes.Add(N(side * 5, 10.03f, -271, 1.2f));
        return nodes.AsReadOnly();
    }

    public static IReadOnlyList<BlundervilleRouteNode> DeliveryNodes(Vector3 player, Vector3 destination)
    {
        // Pass through the central pickup/ramp hub before taking one of three
        // raised spokes. A straight line between outer scoring platforms falls.
        // Carries on the course clock run the timed corridors of
        // BlundervilleFinalPlanner; these nodes serve returns and fallback.
        if (destination.X < -225)
            return ReadOnly(N(-200, 5.42f, 252, 2f), N(-215, 6, 261, 1.6f), N(-231, 6, 271, 1.6f), new(destination, 1.4f));
        if (destination.X > -175)
            return ReadOnly(N(-200, 5.42f, 252, 2f), N(-185, 6, 261, 1.6f), N(-169, 6, 271, 1.6f), new(destination, 1.4f));
        // The central pad at Z=216 is separated from the approach. Use the
        // connected west walkway rather than stopping beside that small island.
        return ReadOnly(N(-200, 5.42f, 252, 2f), N(-200, 6, 242, 1.6f), N(-200, 6, 229, 1.5f), N(-203.75f, 6, 216, 1.5f), new(destination, 1.4f));
    }

    public static IGeometryShape Boundary(BlundervilleCourse course) => course switch
    {
        BlundervilleCourse.FirstA => FirstABoundary,
        BlundervilleCourse.FirstB => FirstBBoundary,
        BlundervilleCourse.Delivery => DeliveryBoundary,
        BlundervilleCourse.Ring => RingBoundary,
        BlundervilleCourse.Final => FinalBoundary,
        _ => EmptyBoundary
    };

    public static IGeometryShape NavigationBoundary(BlundervilleCourse course, Vector3 player)
        => course == BlundervilleCourse.Delivery && player.Y >= 5.2f ? DeliveryUpperBoundary : Boundary(course);

    public static bool IsInsideCorridor(BlundervilleCourse course, Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => Boundary(course).Contains(point, epsilon);

    public static float SignedDistanceToCorridor(BlundervilleCourse course, Vector3 point)
        => ((CorridorShape)Boundary(course)).SignedDistance(point);

    /// <summary>Approximate verified floor elevation for rendering, never collision availability.</summary>
    public static bool TrySurfaceHeight(BlundervilleCourse course, Vector3 point, out float height)
    {
        height = 0;
        if (!Finite(point) || course == BlundervilleCourse.None || !IsInsideCorridor(course, point, 0.1f)) return false;
        height = HeightHint(course, point.Z);
        return true;
    }

    public static bool NeedsRepeatedJump(BlundervilleCourse course, Vector3 point)
        => course switch
        {
            BlundervilleCourse.FirstA => point.Z is < -318 and > -354,
            BlundervilleCourse.Final => point.Z is < 154 and > 135,
            _ => false
        };

    private static IReadOnlyList<BlundervilleRouteNode> FirstANodes(float side)
    {
        var outer = -200 + side * 11;
        var bridge = side < 0 ? -203.5f : -196.5f;
        return ReadOnly(N(-200 + side * 4, 11.28f, -119), N(-200 + side * 9.5f, 11.28f, -126), N(outer, 15.2f, -140), N(-200 + side * 10.25f, 17.47f, -158),
            N(outer, 17.47f, -173), N(-200 + side * 7, 17.47f, -183), N(-200 + side * 4, 17.47f, -193), N(-200 + side * 4, 17.47f, -207),
            N(-200, 16.8f, -219), N(-200, 11.5f, -240), N(-200, 10.03f, -254), N(-200, 10.03f, -290), N(-200, 10.03f, -307),
            N(bridge, 13.57f, -319), N(bridge, 13.57f, -325, 1.1f, true), N(bridge, 13.57f, -335, 1.1f, true),
            N(bridge, 13.57f, -345, 1.1f, true), N(bridge, 13.57f, -354, 1.1f, true), N(-200, 13.57f, -367, 1.4f));
    }

    private static IReadOnlyList<BlundervilleRouteNode> FirstBNodes(float side)
        => ReadOnly(N(side * 10, 10.03f, -123), N(side * 10.25f, 10.03f, -134.2f, .3f, true), N(side * 11.9f, 10.03f, -138.4f, .3f),
            N(side * 12, 10.03f, -146.5f, .3f), N(side * 8, 10.03f, -146.5f, .3f),
            N(side * 8, 10.03f, -151.5f, .3f, true), N(side * 8, 10.03f, -155.9f, .3f), N(side * 8, 10.03f, -160), N(side * 8, 10.03f, -189),
            N(side * 6.5f, 10.03f, -197, .5f), N(side * 6.5f, 10.03f, -207, .5f),
            N(side * 6.5f, 10.03f, -217, .5f), N(side * 6.5f, 10.03f, -227, .5f),
            N(side * 6.5f, 10.03f, -237, .5f), N(side * 5.5f, 10.03f, -247), N(side * 5, 10.03f, -260),
            N(side * 5, 10.03f, -271),
            N(-4.5f, 10.03f, -281, .5f), N(4.5f, 10.03f, -288, .5f),
            N(-4.5f, 10.03f, -301, .5f), N(4.5f, 10.03f, -308, .5f),
            N(-4.5f, 10.03f, -321, .5f), N(4.5f, 10.03f, -328, .5f), N(0, 10.03f, -336),
            N(0, 13.5f, -347), N(0, 17.6f, -362), N(0, 18.94f, -377, 1.4f));

    private static IReadOnlyList<BlundervilleRouteNode> FinalNodes(float side)
        // z=262–275: the only walkway is x≈5–15 beside the wall at x=±5 (a pit
        // spans |x|≤5 at z=270), i.e. inside the x=±10 R5 circle. Hug the wall
        // x=6 retains the timed traversal's 0.25m terrain clearance; x=5.8
        // is too close to the modelled moat edge for an executable fixed route.
        // z=256–238: the inner lane x=±3.5 (pits at |x|=6–9, walls at ±1) is
        // only reached by the x=±4 sweeps, twice per 11.07s. The 243 and 229
        // sweep rows leave a strip at z≈236 to cross outward; the outer lane
        // x=±13.5 then meets one 34801 position and the x=±10 34802 circle.
        => ReadOnly(N(0, 3.22f, 290), N(side * 3.5f, 4.8f, 278), N(side * 6f, 5.45f, 273), N(side * 6f, 6, 262),
            N(side * 3.5f, 8.2f, 256), N(side * 3.5f, 13.3f, 236), N(side * 13.5f, 13.3f, 236),
            N(side * 13, 25.6f, 190),
            N(side * 9, 28.9f, 180), N(side * 6, 30.3f, 167), N(side * FinalStairLane, 31.5f, 154),
            N(side * FinalStairLane, 31.5f, 153, .3f, true), N(side * FinalStairLane, 32.6f, 149, .3f, true),
            N(side * FinalStairLane, 33.5f, 145, .3f, true), N(side * FinalStairLane, 34.4f, 141, .3f, true), N(side * FinalStairLane, 35.8f, 137, .3f),
            N(side * FinalStairLane, 35.8f, 136.6f), N(side * 8.5f, 39.597f, 128), N(side * 8.5f, 39.597f, 123.8f, .3f));

    // The stair steps are flat across |x|≈1.5–14. The 34801 R6 circles centred
    // x=-12/-4/4/12, z=145.7 cycle every 6.03s. x=±4 isolates one circle and
    // x=-4 also misses the z=156 34796 circles. Continuous hops now take ~3s;
    // navigation validates the complete ascent before acquiring input.
    internal const float FinalStairLane = 4;

    // Away from the jumps a node hands over 2.5m early: the next route takes
    // a solve (~0.2s live) to arrive, and a route ending at the node stopped
    // the player there on every pass (Ring replays: ~0.5s per node, 26% of
    // the round standing). The solver still keeps the board's edges.
    // The east relay stays outside the fan/corner footprints. Its old inner
    // goal was pinched between the fixed base and the moving column.
    private static readonly IReadOnlyList<BlundervilleRouteNode> RingNodes = ReadOnly(
        N(220.8f, 10, -115.1f, RingPass), N(212, 10, -110, RingPass), N(198, 10, -107, RingPass), N(185, 10, -114, RingPass), N(184, 10, -135),
        N(183.8f, 10, -138.6f, .3f, true), N(185.6f, 10, -142.8f, .3f), N(188.7f, 10, -149, RingPass),
        N(192.8f, 10, -156.4f, RingPass), N(193.6f, 10, -157.5f, RingPass), N(208.9f, 10, -153.1f, .3f),
        N(209.5f, 10, -147.7f, .3f, true), N(210.3f, 10, -143.3f, .3f), N(210.8f, 10, -142.6f, .3f, true),
        N(213.2f, 10, -138.7f, .3f));
    private const float RingPass = 2.5f;

    private static BlundervilleRouteNode N(float x, float y, float z, float radius = 1.2f, bool jump = false) => new(new(x, y, z), radius, jump);
    private static IReadOnlyList<BlundervilleRouteNode> ReadOnly(params BlundervilleRouteNode[] nodes) => Array.AsReadOnly(nodes);
    private static bool Finite(Vector3 point) => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);

    internal static float HeightHint(BlundervilleCourse course, float z) => course switch
    {
        BlundervilleCourse.FirstA => Interpolate(z, [-375, -318, -307, -250, -214, -152, -127, -100], [13.57f, 13.57f, 10.03f, 10.03f, 17.47f, 17.47f, 11.28f, 11.28f]),
        BlundervilleCourse.FirstB => Interpolate(z, [-390, -367, -336, -100], [18.94f, 18.94f, 10.03f, 10.03f]),
        BlundervilleCourse.Delivery => 6,
        BlundervilleCourse.Ring => 10,
        BlundervilleCourse.Final => Interpolate(z, [115,120,125,136,141,145,149,154,175,180,190.4f,196.4f,202.4f,208.4f,214.4f,225.3f,237,255,267.5f,279,284,302,311,323,330],
            [41.37f,41.37f,39.6f,35.8f,34.4f,33.5f,32.6f,31.5f,29.4f,28.9f,25.59f,23.37f,21.15f,18.94f,16.73f,14.8f,13.5f,8.2f,6,4.5f,3.22f,3.22f,4.3f,4.24f,4.24f]),
        _ => 0
    };

    private static float Interpolate(float value, ReadOnlySpan<float> positions, ReadOnlySpan<float> heights)
    {
        if (value <= positions[0]) return heights[0];
        for (var i = 1; i < positions.Length; ++i)
            if (value <= positions[i]) return heights[i - 1] + (heights[i] - heights[i - 1]) * (value - positions[i - 1]) / (positions[i] - positions[i - 1]);
        return heights[^1];
    }

    private sealed class CorridorShape : IStaticArenaBoundary
    {
        private readonly IReadOnlyList<IReadOnlyList<Vector3>> contours;
        private readonly GeometryBounds bounds;
        private readonly Edge[][] edgeBins;
        private const float BinSize = 2;
        private readonly record struct Edge(Vector2 A, Vector2 B)
        {
            public float MinX => MathF.Min(A.X, B.X);
            public float MaxX => MathF.Max(A.X, B.X);
            public float MinZ => MathF.Min(A.Y, B.Y);
            public float MaxZ => MathF.Max(A.Y, B.Y);
        }

        public CorridorShape(BlundervilleCourse course, params float[][] paths)
        {
            contours = Array.AsReadOnly(paths.Select(path => (IReadOnlyList<Vector3>)Array.AsReadOnly(Enumerable.Range(0, path.Length / 2)
                .Select(i => new Vector3(path[i * 2], HeightHint(course, path[i * 2 + 1]), path[i * 2 + 1])).ToArray())).ToArray());
            TraversalIndex = new(contours);
            var all = contours.SelectMany(p => p).ToArray();
            bounds = all.Length > 0 ? GeometryBounds.FromPoints(all) : new(0, 0, 0, 0);
            Position = new(bounds.Center.X, HeightHint(course, bounds.Center.Y), bounds.Center.Y);
            var bins = Enumerable.Range(0, Math.Max(1, (int)MathF.Ceiling(bounds.Height / BinSize) + 1))
                .Select(_ => new List<Edge>()).ToArray();
            foreach (var contour in contours)
                for (var i = 0; i < contour.Count; ++i)
                {
                    var a = contour[i]; var b = contour[(i + 1) % contour.Count];
                    var edge = new Edge(new(a.X, a.Z), new(b.X, b.Z));
                    var first = Math.Clamp((int)MathF.Floor((edge.MinZ - bounds.MinZ) / BinSize), 0, bins.Length - 1);
                    var last = Math.Clamp((int)MathF.Floor((edge.MaxZ - bounds.MinZ) / BinSize), 0, bins.Length - 1);
                    for (var bin = first; bin <= last; ++bin) bins[bin].Add(edge);
                }
            edgeBins = bins.Select(b => b.ToArray()).ToArray();
        }

        public GeometryKind Kind => GeometryKind.WorldPolygon;
        public ArenaBoundaryIndex TraversalIndex { get; }
        public Vector3 Position { get; }
        public float Rotation => 0;
        public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        {
            // Negative epsilon contracts the corridor; preserve the original exact SDF.
            if (epsilon < 0 || !float.IsFinite(epsilon) || !Finite(point) || contours.Count == 0)
                return SignedDistance(point) <= epsilon;
            if (!bounds.Contains(point, epsilon)) return false;
            var inside = false;
            var p = new Vector2(point.X, point.Z);
            foreach (var edge in edgeBins[Bin(point.Z)])
                if ((edge.A.Y > p.Y) != (edge.B.Y > p.Y)
                    && p.X < (edge.B.X - edge.A.X) * (p.Y - edge.A.Y) / (edge.B.Y - edge.A.Y) + edge.A.X)
                    inside = !inside;
            if (inside) return true;
            // Only an outside point near a contour needs segment distance. Most
            // rasterizer cells take the bounds or indexed ray-parity fast paths.
            var epsilonSquared = epsilon * epsilon;
            for (var bin = Bin(point.Z - epsilon); bin <= Bin(point.Z + epsilon); ++bin)
                foreach (var edge in edgeBins[bin])
                {
                    if (point.X < edge.MinX - epsilon || point.X > edge.MaxX + epsilon
                        || point.Z < edge.MinZ - epsilon || point.Z > edge.MaxZ + epsilon) continue;
                    var delta = edge.B - edge.A;
                    var t = Math.Clamp(Vector2.Dot(p - edge.A, delta) / MathF.Max(delta.LengthSquared(), 0.000001f), 0, 1);
                    if (Vector2.DistanceSquared(p, edge.A + t * delta) <= epsilonSquared) return true;
                }
            return false;
        }
        private int Bin(float z) => (int)Math.Clamp(MathF.Floor((z - bounds.MinZ) / BinSize), 0, edgeBins.Length - 1);
        public GeometryBounds GetBounds() => bounds;
        public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => contours;

        public float SignedDistance(Vector3 point)
        {
            if (!Finite(point) || contours.Count == 0) return float.PositiveInfinity;
            var p = new Vector2(point.X, point.Z);
            var distanceSquared = float.PositiveInfinity;
            var inside = false;
            foreach (var contour in contours)
            {
                for (var i = 0; i < contour.Count; ++i)
                {
                    var va = contour[i]; var vb = contour[(i + 1) % contour.Count];
                    var a = new Vector2(va.X, va.Z); var b = new Vector2(vb.X, vb.Z);
                    var ab = b - a;
                    var fraction = Math.Clamp(Vector2.Dot(p - a, ab) / MathF.Max(ab.LengthSquared(), 0.000001f), 0, 1);
                    distanceSquared = MathF.Min(distanceSquared, Vector2.DistanceSquared(p, a + fraction * ab));
                    if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
                }
            }
            var distance = MathF.Sqrt(distanceSquared);
            return inside ? -distance : distance;
        }

        public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
        {
            if (Contains(start, epsilon) || Contains(end, epsilon)) return true;
            var a = new Vector2(start.X, start.Z); var b = new Vector2(end.X, end.Z);
            foreach (var contour in contours)
                for (var i = 0; i < contour.Count; ++i)
                {
                    var v1 = contour[i]; var v2 = contour[(i + 1) % contour.Count];
                    if (GeometryHelper.SegmentsIntersect2D(a, b, new(v1.X, v1.Z), new(v2.X, v2.Z), epsilon)) return true;
                }
            return false;
        }
    }

    private static readonly CorridorShape EmptyBoundary = new(BlundervilleCourse.None);

    // Restore the ktfv2 slope top faces: the cached navmesh erodes its edges
    // by about 0.5m. ARR 04:10:20 stands at (-205.04, 16.92, -145.8) on this floor.
    private static readonly CorridorShape FirstABoundary = new(BlundervilleCourse.FirstA,
        [-212.457f, -149.500f, -213.102f, -149.500f, -213.102f, -149.423f, -214.525f, -149.252f, -214.514f, -148.229f, -213.102f, -148.051f, -213.125f, -147.941f, -213.148f, -147.926f, -213.047f, -147.898f, -213.000f, -147.805f, -213.000f, -144.383f, -213.039f, -144.273f, -213.148f, -144.215f, -213.148f, -144.062f, -213.094f, -143.881f, -213.095f, -143.634f, -213.258f, -143.525f, -214.525f, -143.500f, -214.523f, -140.490f, -213.102f, -140.476f, -213.102f, -140.312f, -213.125f, -140.215f, -213.148f, -140.199f, -213.047f, -140.171f, -213.000f, -140.076f, -213.000f, -136.656f, -213.039f, -136.545f, -213.148f, -136.486f, -213.148f, -136.334f, -213.099f, -136.174f, -214.524f, -135.757f, -214.525f, -132.748f, -213.102f, -132.729f, -213.102f, -132.586f, -213.125f, -132.488f, -213.148f, -132.473f, -213.047f, -132.441f, -213.000f, -132.348f, -213.000f, -128.930f, -213.039f, -128.820f, -213.148f, -128.758f, -213.148f, -128.609f, -213.094f, -128.430f, -213.095f, -128.230f, -213.506f, -128.025f, -214.516f, -128.019f, -214.525f, -126.998f, -211.998f, -126.975f, -211.960f, -127.000f, -210.067f, -127.000f, -210.025f, -126.983f, -210.025f, -125.755f, -213.525f, -117.253f, -213.775f, -101.498f, -186.248f, -101.475f, -186.475f, -117.003f, -189.975f, -125.755f, -189.975f, -126.982f, -189.921f, -127.000f, -188.030f, -127.000f, -188.005f, -126.975f, -185.475f, -126.998f, -185.486f, -128.021f, -186.246f, -128.025f, -186.898f, -128.243f, -186.898f, -128.438f, -186.852f, -128.625f, -185.500f, -128.575f, -185.500f, -128.608f, -186.852f, -128.781f, -186.961f, -128.836f, -186.953f, -128.680f, -187.000f, -128.789f, -187.000f, -132.367f, -186.953f, -132.465f, -186.852f, -132.492f, -186.875f, -132.504f, -186.906f, -132.598f, -186.906f, -132.730f, -185.475f, -132.750f, -185.479f, -135.764f, -186.898f, -135.927f, -186.898f, -136.166f, -186.852f, -136.355f, -185.500f, -136.304f, -185.500f, -136.335f, -186.852f, -136.504f, -186.961f, -136.564f, -186.953f, -136.410f, -187.000f, -136.520f, -187.000f, -140.096f, -186.953f, -140.191f, -186.852f, -140.218f, -186.875f, -140.233f, -186.906f, -140.327f, -186.906f, -140.480f, -185.475f, -140.500f, -185.478f, -143.511f, -186.898f, -143.873f, -186.852f, -144.082f, -186.953f, -144.137f, -187.000f, -144.246f, -187.000f, -147.824f, -186.953f, -147.918f, -186.852f, -147.945f, -186.875f, -147.961f, -186.905f, -148.050f, -185.486f, -148.229f, -185.475f, -149.252f, -186.900f, -149.444f, -186.899f, -149.500f, -187.311f, -149.500f, -187.467f, -149.521f, -186.977f, -151.010f, -189.225f, -154.258f, -189.225f, -159.490f, -187.742f, -160.973f, -186.484f, -160.730f, -186.722f, -162.234f, -185.475f, -162.745f, -185.475f, -179.502f, -186.474f, -179.769f, -186.231f, -184.517f, -190.474f, -184.774f, -190.225f, -203.500f, -190.982f, -203.525f, -191.225f, -204.254f, -191.225f, -212.993f, -190.475f, -214.251f, -191.225f, -215.010f, -191.225f, -216.242f, -190.475f, -217.250f, -190.476f, -245.505f, -195.735f, -245.276f, -195.973f, -245.752f, -195.732f, -246.475f, -192.475f, -246.500f, -192.475f, -298.502f, -195.725f, -298.525f, -195.725f, -299.475f, -193.232f, -299.482f, -189.728f, -304.738f, -189.477f, -306.486f, -188.477f, -307.240f, -188.481f, -308.016f, -189.475f, -308.515f, -189.475f, -309.990f, -188.975f, -310.497f, -187.975f, -317.997f, -188.132f, -318.475f, -187.075f, -318.498f, -187.075f, -353.502f, -188.225f, -353.525f, -188.226f, -358.007f, -189.228f, -360.512f, -190.475f, -362.258f, -190.475f, -363.488f, -188.475f, -364.997f, -188.477f, -366.760f, -189.475f, -367.760f, -189.227f, -373.009f, -190.985f, -373.025f, -191.247f, -373.525f, -199.000f, -373.525f, -199.512f, -372.777f, -201.734f, -373.023f, -201.997f, -373.525f, -205.500f, -373.525f, -205.766f, -373.024f, -211.508f, -372.774f, -211.521f, -371.986f, -210.775f, -371.732f, -210.775f, -368.018f, -211.524f, -367.757f, -211.525f, -365.000f, -209.768f, -364.231f, -209.276f, -363.246f, -209.523f, -361.763f, -211.024f, -360.508f, -211.525f, -358.252f, -211.998f, -353.525f, -212.948f, -353.502f, -212.948f, -318.498f, -212.013f, -318.475f, -211.025f, -310.497f, -210.526f, -309.990f, -210.775f, -306.495f, -210.272f, -304.738f, -209.025f, -303.241f, -209.023f, -301.240f, -207.765f, -300.978f, -206.760f, -299.477f, -204.525f, -299.475f, -204.525f, -298.525f, -207.525f, -298.500f, -207.525f, -282.250f, -207.025f, -281.985f, -207.025f, -280.515f, -207.525f, -280.250f, -207.525f, -268.000f, -207.025f, -267.735f, -207.025f, -266.515f, -207.525f, -266.250f, -207.525f, -246.498f, -204.525f, -246.475f, -204.525f, -245.768f, -206.003f, -245.275f, -209.525f, -245.505f, -209.525f, -217.250f, -208.525f, -216.240f, -208.525f, -211.012f, -209.525f, -210.248f, -208.525f, -208.991f, -208.525f, -204.262f, -209.525f, -203.503f, -209.525f, -185.006f, -209.765f, -184.525f, -213.768f, -184.518f, -213.775f, -179.525f, -214.525f, -179.502f, -214.525f, -162.745f, -212.765f, -162.725f, -212.526f, -162.247f, -212.771f, -161.237f, -210.775f, -159.489f, -210.775f, -154.258f, -213.025f, -151.003f, -213.014f, -150.479f, -212.275f, -150.475f, -212.275f, -149.522f],
        [-194.075f, -353.225f, -191.925f, -353.225f, -191.925f, -322.775f, -194.075f, -322.775f],
        [-193.006f, -180.475f, -192.525f, -180.235f, -192.525f, -161.765f, -193.006f, -161.525f, -195.978f, -161.525f, -196.225f, -163.502f, -196.225f, -180.235f, -195.744f, -180.475f],
        [-193.512f, -359.462f, -193.033f, -358.265f, -193.737f, -358.031f, -194.218f, -358.991f],
        [-196.975f, -290.006f, -196.975f, -291.498f, -196.728f, -293.225f, -193.772f, -293.225f, -193.525f, -291.500f, -193.772f, -289.525f, -196.735f, -289.525f],
        [-196.975f, -250.500f, -196.728f, -252.225f, -193.772f, -252.225f, -193.525f, -250.500f, -193.772f, -248.775f, -196.728f, -248.775f],
        [-196.524f, -149.007f, -196.512f, -148.228f, -195.010f, -148.039f, -195.010f, -143.737f, -196.522f, -143.513f, -196.775f, -141.501f, -196.513f, -140.479f, -195.022f, -140.475f, -195.000f, -140.076f, -195.010f, -136.159f, -196.775f, -135.755f, -196.772f, -132.738f, -195.010f, -132.715f, -195.010f, -128.357f, -195.508f, -128.025f, -196.516f, -128.019f, -196.525f, -126.998f, -194.775f, -126.975f, -194.775f, -126.023f, -197.233f, -125.777f, -197.495f, -126.525f, -202.250f, -126.525f, -202.518f, -125.775f, -203.994f, -125.775f, -204.975f, -126.265f, -204.975f, -126.975f, -203.486f, -126.979f, -203.475f, -128.005f, -204.245f, -128.025f, -204.990f, -128.323f, -204.990f, -132.727f, -203.478f, -132.738f, -203.475f, -135.752f, -204.990f, -136.144f, -204.990f, -140.477f, -203.477f, -140.490f, -203.475f, -143.502f, -204.990f, -143.894f, -205.000f, -147.824f, -204.990f, -148.039f, -203.486f, -148.229f, -203.475f, -149.252f, -204.990f, -149.456f, -204.990f, -149.500f, -205.314f, -149.500f, -205.469f, -149.521f, -205.230f, -150.475f, -194.520f, -150.475f, -194.280f, -149.519f, -194.363f, -149.500f, -195.010f, -149.500f, -195.010f, -149.352f],
        [-201.985f, -204.475f, -197.765f, -204.475f, -197.525f, -203.994f, -197.525f, -186.006f, -197.765f, -185.525f, -201.744f, -185.525f, -202.225f, -185.765f, -202.225f, -203.994f],
        [-201.975f, -269.002f, -201.975f, -270.978f, -198.271f, -271.225f, -198.025f, -269.751f, -198.520f, -267.527f, -201.729f, -267.773f],
        [-198.275f, -282.985f, -198.522f, -279.775f, -201.729f, -279.775f, -201.975f, -282.985f, -201.494f, -283.225f, -198.756f, -283.225f],
        [-201.075f, -322.775f, -201.075f, -353.225f, -198.925f, -353.225f, -198.925f, -322.775f],
        [-203.272f, -293.225f, -203.025f, -291.498f, -203.025f, -290.006f, -203.265f, -289.525f, -206.477f, -289.525f, -206.725f, -291.999f, -206.480f, -293.225f],
        [-203.025f, -250.500f, -203.272f, -248.775f, -206.478f, -248.775f, -206.725f, -250.999f, -206.480f, -252.225f, -203.272f, -252.225f],
        [-203.525f, -163.502f, -203.772f, -161.525f, -206.744f, -161.525f, -207.225f, -161.765f, -207.225f, -179.245f, -206.733f, -180.475f, -203.765f, -180.475f, -203.525f, -179.994f],
        [-205.925f, -353.225f, -205.925f, -322.775f, -208.098f, -322.775f, -208.098f, -353.225f],
        [-207.000f, -357.026f, -207.725f, -357.268f, -207.725f, -357.975f, -206.275f, -357.975f, -206.275f, -357.268f],
        [-210.953f, -132.691f, -211.000f, -132.793f, -211.000f, -134.434f, -210.953f, -134.539f, -210.852f, -134.582f, -209.148f, -134.582f, -209.047f, -134.543f, -209.000f, -134.441f, -209.000f, -132.797f, -209.047f, -132.695f, -209.148f, -132.652f, -210.852f, -132.652f],
        [-207.148f, -140.379f, -207.043f, -140.335f, -207.000f, -140.231f, -207.000f, -138.588f, -207.043f, -138.487f, -207.148f, -138.447f, -208.852f, -138.447f, -208.953f, -138.489f, -209.000f, -138.592f, -209.000f, -140.235f, -208.953f, -140.337f, -208.852f, -140.379f],
        [-210.953f, -144.281f, -211.000f, -144.383f, -211.000f, -146.027f, -210.953f, -146.129f, -210.852f, -146.176f, -209.148f, -146.176f, -209.047f, -146.133f, -209.000f, -146.031f, -209.000f, -144.387f, -209.047f, -144.285f, -209.148f, -144.242f, -210.852f, -144.242f],
        [-192.957f, -132.691f, -193.000f, -132.793f, -193.000f, -134.434f, -192.957f, -134.539f, -192.852f, -134.582f, -191.148f, -134.582f, -191.047f, -134.543f, -191.000f, -134.441f, -191.000f, -132.797f, -191.047f, -132.695f, -191.148f, -132.652f, -192.852f, -132.652f],
        [-189.148f, -140.379f, -189.047f, -140.335f, -189.000f, -140.231f, -189.000f, -138.588f, -189.047f, -138.487f, -189.148f, -138.447f, -190.852f, -138.447f, -190.953f, -138.489f, -191.000f, -138.592f, -191.000f, -140.235f, -190.953f, -140.337f, -190.852f, -140.379f],
        [-192.957f, -144.281f, -193.000f, -144.383f, -193.000f, -146.027f, -192.957f, -146.129f, -192.852f, -146.176f, -191.148f, -146.176f, -191.047f, -146.133f, -191.000f, -146.031f, -191.000f, -144.387f, -191.047f, -144.285f, -191.148f, -144.242f, -192.852f, -144.242f],
        [-185.500f, -144.079f, -186.852f, -144.246f, -186.852f, -144.082f, -185.500f, -144.045f]);

    // htpd3 has solid diamond walls at Z=-212/-232. The cached navmesh
    // fills part of their interiors; subtract the measured 5m diamond envelope.
    private static readonly CorridorShape FirstBBoundary = new(BlundervilleCourse.FirstB,
        [-16.774f, -254.755f, -16.526f, -251.265f, -17.525f, -250.753f, -17.523f, -238.490f, -17.482f, -238.482f, -15.490f, -240.475f, -14.510f, -240.475f, -11.525f, -237.490f, -11.525f, -236.510f, -14.510f, -233.525f, -15.490f, -233.525f, -17.482f, -235.518f, -17.525f, -235.504f, -17.270f, -228.736f, -17.232f, -228.732f, -15.490f, -230.475f, -14.510f, -230.475f, -11.527f, -227.492f, -11.773f, -226.262f, -14.510f, -223.525f, -15.490f, -223.525f, -17.232f, -225.268f, -17.275f, -225.252f, -17.271f, -218.736f, -17.232f, -218.732f, -15.490f, -220.475f, -14.510f, -220.475f, -11.773f, -217.738f, -11.527f, -216.508f, -14.510f, -213.525f, -15.490f, -213.525f, -17.232f, -215.268f, -17.274f, -215.256f, -17.525f, -209.251f, -17.270f, -208.485f, -17.231f, -208.484f, -15.489f, -210.475f, -14.510f, -210.475f, -11.525f, -207.490f, -11.525f, -206.510f, -14.510f, -203.525f, -15.490f, -203.525f, -17.482f, -205.518f, -17.524f, -205.507f, -17.525f, -198.998f, -16.775f, -198.487f, -17.025f, -189.498f, -17.000f, -189.475f, -11.272f, -189.475f, -11.025f, -187.249f, -11.025f, -161.025f, -11.432f, -161.025f, -11.455f, -161.002f, -11.454f, -158.013f, -11.329f, -157.214f, -11.202f, -156.871f, -11.004f, -156.500f, -10.768f, -156.194f, -10.446f, -155.852f, -10.062f, -155.566f, -9.708f, -155.354f, -9.298f, -155.188f, -8.918f, -155.073f, -8.463f, -154.995f, -7.998f, -154.963f, -7.494f, -155.002f, -7.082f, -155.073f, -6.654f, -155.207f, -6.284f, -155.361f, -5.933f, -155.568f, -5.563f, -155.855f, -5.236f, -156.186f, -4.996f, -156.500f, -4.821f, -156.830f, -4.659f, -157.210f, -4.584f, -157.576f, -4.542f, -158.013f, -4.542f, -161.002f, -4.564f, -161.025f, -4.975f, -161.025f, -4.975f, -187.249f, -4.728f, -189.475f, 4.728f, -189.475f, 4.975f, -187.249f, 4.975f, -161.025f, 4.564f, -161.025f, 4.542f, -161.002f, 4.542f, -158.012f, 4.671f, -157.214f, 4.798f, -156.871f, 4.992f, -156.500f, 5.232f, -156.194f, 5.559f, -155.851f, 6.290f, -155.353f, 7.079f, -155.074f, 7.535f, -154.995f, 7.998f, -154.963f, 8.510f, -155.002f, 8.922f, -155.074f, 9.716f, -155.361f, 10.070f, -155.570f, 10.437f, -155.855f, 10.768f, -156.185f, 11.006f, -156.504f, 11.336f, -157.205f, 11.416f, -157.576f, 11.455f, -158.013f, 11.455f, -161.002f, 11.432f, -161.025f, 11.025f, -161.025f, 11.025f, -187.249f, 11.272f, -189.475f, 17.021f, -189.486f, 17.025f, -196.752f, 16.527f, -197.999f, 17.517f, -199.979f, 18.525f, -200.250f, 18.516f, -206.519f, 18.482f, -206.518f, 15.490f, -203.525f, 14.510f, -203.525f, 11.525f, -206.510f, 11.525f, -207.490f, 14.510f, -210.475f, 15.489f, -210.475f, 17.241f, -208.477f, 17.274f, -208.492f, 17.525f, -209.247f, 17.269f, -215.266f, 17.232f, -215.268f, 15.490f, -213.525f, 14.510f, -213.525f, 11.527f, -216.508f, 11.773f, -217.738f, 14.510f, -220.475f, 15.490f, -220.475f, 17.232f, -218.732f, 17.274f, -218.743f, 17.271f, -225.264f, 17.232f, -225.268f, 15.490f, -223.525f, 14.511f, -223.525f, 11.531f, -226.753f, 12.520f, -228.485f, 14.510f, -230.475f, 15.490f, -230.475f, 17.232f, -228.732f, 17.270f, -228.736f, 17.524f, -235.506f, 17.482f, -235.518f, 15.490f, -233.525f, 14.510f, -233.525f, 11.525f, -236.510f, 11.525f, -237.490f, 14.510f, -240.475f, 15.490f, -240.475f, 17.482f, -238.482f, 17.523f, -238.490f, 17.524f, -250.755f, 16.526f, -251.512f, 16.774f, -254.007f, 15.523f, -256.010f, 15.019f, -257.766f, 13.525f, -258.763f, 13.519f, -259.516f, 12.762f, -259.525f, 11.762f, -260.772f, 10.262f, -261.023f, 9.257f, -262.024f, 7.019f, -262.025f, 6.526f, -263.750f, 6.775f, -264.746f, 7.525f, -269.750f, 7.525f, -334.750f, 6.523f, -335.267f, 6.275f, -336.752f, 6.275f, -337.735f, 6.775f, -338.000f, 6.775f, -346.003f, 6.278f, -346.264f, 6.775f, -348.751f, 6.525f, -357.500f, 6.775f, -365.999f, 6.278f, -367.246f, 7.012f, -368.224f, 9.764f, -367.979f, 12.523f, -371.240f, 11.775f, -372.258f, 11.775f, -372.475f, 12.521f, -372.486f, 12.524f, -373.507f, 11.775f, -373.525f, 11.775f, -373.737f, 13.275f, -374.748f, 13.273f, -385.259f, 11.281f, -385.521f, 11.524f, -386.494f, 11.500f, -386.525f, 5.495f, -386.525f, 5.475f, -385.026f, 0.000f, -385.275f, -13.010f, -385.273f, -13.275f, -374.748f, -11.775f, -373.737f, -11.775f, -373.525f, -12.524f, -373.507f, -12.521f, -372.486f, -11.775f, -372.475f, -11.775f, -372.257f, -12.521f, -370.986f, -10.012f, -367.978f, -7.018f, -368.223f, -6.525f, -366.496f, -6.525f, -357.750f, -6.775f, -348.750f, -6.775f, -343.000f, -6.525f, -336.999f, -6.525f, -335.514f, -7.775f, -334.750f, -7.775f, -270.500f, -7.750f, -270.475f, -6.525f, -270.475f, -6.525f, -262.760f, -7.012f, -262.273f, -9.762f, -261.772f, -11.014f, -260.522f, -12.515f, -260.020f, -15.019f, -257.767f, -15.772f, -256.013f],
        [6.225f, -222.240f, 6.225f, -221.513f, 5.242f, -220.775f, 4.514f, -220.775f, 3.779f, -222.000f, 4.514f, -223.225f, 5.240f, -223.225f],
        [6.225f, -201.513f, 5.242f, -200.775f, 4.514f, -200.775f, 3.780f, -201.998f, 4.762f, -203.225f, 5.488f, -203.225f, 6.225f, -202.242f],
        [6.221f, -241.753f, 5.488f, -240.775f, 4.514f, -240.775f, 3.779f, -242.000f, 4.514f, -243.225f, 5.485f, -243.225f],
        [5.475f, -231.513f, 4.745f, -231.027f, 4.171f, -231.171f, 1.475f, -228.475f, 1.475f, -227.762f, 0.001f, -226.533f, -4.483f, -230.518f, -5.475f, -231.510f, -5.475f, -232.487f, -4.742f, -232.975f, -4.025f, -232.975f, -0.292f, -236.708f, -0.469f, -237.237f, -0.004f, -237.470f, 4.483f, -233.482f, 5.475f, -232.490f],
        [5.475f, -212.490f, 5.475f, -211.513f, 4.742f, -211.025f, 4.267f, -211.025f, 4.198f, -211.198f, 1.898f, -208.898f, 1.975f, -208.744f, 1.975f, -208.265f, 1.018f, -207.787f, 0.980f, -207.980f, 0.289f, -207.289f, -0.010f, -206.542f, -4.483f, -210.518f, -5.475f, -211.510f, -5.475f, -212.487f, -4.742f, -212.975f, -4.025f, -212.975f, -0.292f, -216.708f, -0.469f, -217.237f, -0.004f, -217.470f, 4.483f, -213.482f],
        [-3.779f, -222.000f, -4.514f, -220.775f, -5.242f, -220.775f, -6.225f, -221.513f, -6.225f, -222.487f, -5.242f, -223.225f, -4.514f, -223.225f],
        [-6.221f, -202.247f, -5.488f, -203.225f, -4.514f, -203.225f, -3.779f, -202.000f, -4.514f, -200.775f, -5.485f, -200.775f],
        [-6.221f, -241.753f, -5.485f, -243.225f, -4.514f, -243.225f, -3.779f, -242.000f, -4.514f, -240.775f, -5.488f, -240.775f],
        [16.775f, -236.746f, 15.270f, -238.765f, 15.235f, -238.770f, 13.225f, -237.254f, 14.746f, -235.225f],
        [14.746f, -225.225f, 16.775f, -226.746f, 15.270f, -228.765f, 15.235f, -228.770f, 13.225f, -227.254f],
        [13.225f, -217.254f, 14.746f, -215.225f, 16.775f, -216.746f, 15.270f, -218.765f, 15.235f, -218.770f],
        [16.770f, -206.765f, 15.254f, -208.775f, 13.225f, -207.254f, 14.746f, -205.225f, 16.765f, -206.730f],
        [-14.525f, -105.732f, -14.525f, -105.018f, -15.275f, -104.752f, -15.274f, -102.743f, -14.530f, -102.484f, -14.768f, -101.483f, -8.749f, -101.225f, -2.269f, -101.474f, -2.000f, -100.475f, 1.505f, -100.475f, 2.510f, -101.475f, 9.996f, -101.475f, 13.250f, -100.475f, 13.518f, -101.225f, 14.516f, -101.231f, 14.525f, -101.982f, 15.275f, -102.245f, 15.275f, -105.250f, 14.523f, -105.519f, 14.275f, -107.251f, 14.525f, -114.752f, 14.027f, -115.751f, 14.525f, -117.000f, 14.525f, -134.500f, 14.502f, -134.525f, 14.306f, -134.525f, 14.284f, -135.352f, 13.976f, -135.384f, 13.952f, -135.361f, 13.669f, -135.352f, 13.655f, -134.525f, 13.475f, -134.502f, 13.475f, -123.773f, 10.774f, -123.527f, 10.525f, -128.001f, 10.525f, -134.500f, 10.502f, -134.525f, 10.322f, -134.525f, 10.322f, -135.306f, 10.300f, -135.358f, 10.000f, -135.384f, 9.975f, -135.361f, 9.694f, -135.354f, 9.678f, -134.525f, 9.475f, -134.502f, 9.475f, -123.773f, 6.774f, -123.527f, 6.525f, -128.001f, 6.525f, -134.500f, 6.502f, -134.525f, 6.322f, -134.525f, 6.322f, -135.227f, 6.296f, -135.358f, 5.996f, -135.384f, 5.971f, -135.361f, 5.688f, -135.352f, 5.674f, -134.525f, 5.475f, -134.502f, 5.475f, -123.773f, 2.774f, -123.527f, 2.525f, -128.001f, 2.525f, -134.500f, 2.502f, -134.525f, 2.324f, -134.525f, 2.324f, -135.227f, 2.298f, -135.357f, 1.997f, -135.384f, 1.972f, -135.361f, 1.691f, -135.354f, 1.674f, -134.525f, 1.475f, -134.502f, 1.475f, -123.771f, 0.000f, -123.525f, -1.475f, -123.771f, -1.475f, -134.500f, -1.498f, -134.525f, -1.676f, -134.525f, -1.694f, -135.350f, -2.002f, -135.384f, -2.027f, -135.361f, -2.313f, -135.349f, -2.326f, -134.525f, -2.502f, -134.525f, -2.525f, -134.500f, -2.525f, -123.773f, -5.226f, -123.527f, -5.475f, -128.001f, -5.475f, -134.502f, -5.678f, -134.525f, -5.693f, -135.349f, -6.004f, -135.384f, -6.029f, -135.361f, -6.315f, -135.349f, -6.326f, -134.525f, -6.502f, -134.525f, -6.525f, -134.500f, -6.525f, -123.773f, -9.226f, -123.527f, -9.475f, -128.001f, -9.475f, -134.502f, -9.678f, -134.525f, -9.698f, -135.349f, -10.000f, -135.384f, -10.026f, -135.361f, -10.311f, -135.349f, -10.329f, -134.525f, -10.502f, -134.525f, -10.525f, -134.500f, -10.525f, -123.773f, -13.226f, -123.527f, -13.475f, -128.001f, -13.475f, -134.502f, -13.678f, -134.525f, -13.696f, -135.356f, -14.000f, -135.384f, -14.025f, -135.361f, -14.298f, -135.359f, -14.330f, -135.102f, -14.329f, -134.525f, -14.502f, -134.525f, -14.525f, -134.500f, -14.525f, -128.000f, -14.025f, -115.755f, -14.525f, -114.752f, -14.276f, -107.269f, -15.275f, -107.000f, -15.271f, -105.986f],
        [4.322f, -137.475f, 4.525f, -137.497f, 4.767f, -145.975f, 11.240f, -145.975f, 11.475f, -137.497f, 11.678f, -137.475f, 11.702f, -137.141f, 11.975f, -137.139f, 12.000f, -137.108f, 12.307f, -137.138f, 12.322f, -137.475f, 12.502f, -137.475f, 12.525f, -137.500f, 12.525f, -146.500f, 12.502f, -146.525f, 12.322f, -146.525f, 12.307f, -146.996f, 12.000f, -147.029f, 11.975f, -147.000f, 11.712f, -147.002f, 11.688f, -147.025f, 8.314f, -147.025f, 8.314f, -152.870f, 8.293f, -152.948f, 8.135f, -152.966f, 7.862f, -152.966f, 7.693f, -152.923f, 7.686f, -147.025f, 4.309f, -147.025f, 4.284f, -147.004f, 3.996f, -147.029f, 3.971f, -147.000f, 3.688f, -146.990f, 3.674f, -146.491f, 3.502f, -146.525f, 3.475f, -146.502f, 3.475f, -137.500f, 3.498f, -137.475f, 3.674f, -137.475f, 3.690f, -137.148f, 3.971f, -137.139f, 3.996f, -137.108f, 4.294f, -137.133f],
        [-12.308f, -137.148f, -12.025f, -137.139f, -12.000f, -137.108f, -11.702f, -137.133f, -11.678f, -137.475f, -11.475f, -137.497f, -11.233f, -145.975f, -4.760f, -145.975f, -4.525f, -137.497f, -4.326f, -137.475f, -4.302f, -137.141f, -4.029f, -137.139f, -4.004f, -137.108f, -3.699f, -137.137f, -3.676f, -137.475f, -3.498f, -137.475f, -3.475f, -137.500f, -3.475f, -146.500f, -3.498f, -146.525f, -3.676f, -146.525f, -3.695f, -146.994f, -4.004f, -147.029f, -4.029f, -147.000f, -4.284f, -147.001f, -4.309f, -147.025f, -7.686f, -147.025f, -7.686f, -152.867f, -7.711f, -152.948f, -7.865f, -152.966f, -8.286f, -152.948f, -8.314f, -152.870f, -8.314f, -147.025f, -11.688f, -147.025f, -11.712f, -147.004f, -12.000f, -147.029f, -12.025f, -147.000f, -12.289f, -147.002f, -12.330f, -146.871f, -12.329f, -146.491f, -12.500f, -146.525f, -12.525f, -146.500f, -12.525f, -137.500f, -12.502f, -137.475f, -12.322f, -137.475f],
        [-13.230f, -236.765f, -14.746f, -238.775f, -16.775f, -237.254f, -15.254f, -235.225f, -13.235f, -236.730f],
        [-13.235f, -226.730f, -13.230f, -226.765f, -14.746f, -228.775f, -16.775f, -227.254f, -15.254f, -225.225f],
        [-15.254f, -215.225f, -13.235f, -216.730f, -13.230f, -216.765f, -14.746f, -218.775f, -16.775f, -217.254f],
        [-16.775f, -207.254f, -15.254f, -205.225f, -13.235f, -206.730f, -13.230f, -206.765f, -14.746f, -208.775f]);

    private static readonly CorridorShape DeliveryBoundary = new(BlundervilleCourse.Delivery,
        [-156.761f, 281.522f, -154.484f, 280.019f, -153.476f, 278.757f, -152.725f, 276.255f, -152.725f, 274.500f, -153.742f, 271.976f, -153.275f, 273.753f, -153.524f, 277.491f, -156.016f, 280.729f, -157.272f, 281.238f],
        [-244.025f, 256.000f, -243.272f, 260.511f, -242.544f, 260.809f, -242.509f, 261.023f, -241.064f, 261.401f, -241.270f, 262.225f, -242.271f, 262.236f, -242.275f, 263.250f, -240.883f, 266.271f, -241.275f, 266.751f, -240.779f, 268.737f, -243.267f, 269.982f, -245.523f, 273.240f, -245.775f, 276.253f, -244.772f, 278.512f, -243.511f, 279.772f, -240.254f, 281.025f, -236.740f, 280.273f, -235.241f, 279.025f, -234.265f, 279.025f, -233.518f, 280.517f, -232.879f, 280.469f, -232.258f, 281.274f, -231.781f, 280.369f, -230.262f, 280.523f, -229.024f, 281.761f, -228.768f, 284.517f, -225.506f, 286.024f, -222.982f, 285.017f, -222.475f, 284.004f, -222.706f, 283.303f, -212.257f, 289.024f, -203.005f, 290.775f, -196.999f, 291.025f, -188.990f, 289.523f, -180.738f, 286.022f, -174.733f, 281.769f, -170.234f, 276.731f, -181.737f, 270.478f, -187.238f, 266.978f, -192.983f, 263.981f, -193.487f, 263.229f, -195.482f, 262.480f, -195.993f, 261.476f, -196.525f, 261.498f, -196.525f, 265.980f, -197.475f, 266.218f, -197.475f, 261.495f, -198.475f, 261.475f, -198.475f, 260.525f, -195.243f, 260.524f, -194.237f, 259.025f, -193.765f, 259.025f, -193.527f, 259.502f, -194.021f, 261.233f, -194.524f, 261.495f, -194.014f, 262.021f, -193.240f, 262.023f, -193.050f, 261.657f, -192.732f, 261.768f, -191.986f, 260.030f, -189.262f, 260.773f, -188.295f, 261.546f, -189.274f, 263.993f, -189.100f, 264.111f, -189.267f, 264.519f, -186.015f, 266.021f, -184.262f, 267.772f, -182.845f, 267.541f, -180.940f, 268.615f, -180.016f, 269.769f, -178.020f, 270.768f, -177.763f, 271.521f, -176.010f, 271.774f, -175.021f, 272.515f, -174.761f, 273.272f, -174.262f, 272.622f, -173.981f, 272.766f, -172.737f, 271.025f, -171.757f, 271.025f, -169.277f, 272.513f, -169.441f, 273.826f, -169.894f, 274.733f, -170.275f, 274.998f, -170.076f, 275.096f, -170.272f, 275.512f, -169.512f, 276.272f, -167.237f, 273.275f, -166.518f, 273.275f, -166.277f, 273.997f, -167.766f, 276.478f, -169.016f, 276.731f, -169.273f, 277.737f, -173.768f, 281.983f, -173.935f, 282.638f, -177.520f, 285.735f, -179.010f, 290.273f, -171.627f, 284.811f, -170.983f, 285.268f, -170.977f, 282.025f, -169.775f, 282.025f, -169.775f, 282.949f, -170.025f, 283.250f, -169.775f, 284.505f, -169.234f, 284.519f, -167.226f, 282.007f, -168.279f, 281.562f, -167.323f, 280.275f, -166.482f, 280.267f, -165.738f, 279.029f, -164.010f, 279.523f, -161.755f, 280.774f, -158.745f, 280.774f, -156.483f, 279.769f, -154.475f, 277.005f, -154.225f, 274.247f, -154.977f, 271.990f, -156.735f, 269.980f, -159.225f, 268.735f, -159.225f, 267.506f, -158.726f, 266.507f, -158.970f, 266.245f, -158.726f, 265.745f, -159.975f, 265.233f, -159.975f, 264.270f, -159.005f, 264.027f, -158.648f, 264.265f, -158.519f, 265.016f, -157.732f, 265.518f, -156.725f, 262.253f, -156.999f, 261.725f, -158.011f, 262.228f, -158.765f, 263.725f, -159.479f, 263.725f, -159.703f, 262.378f, -158.916f, 261.275f, -157.493f, 261.274f, -157.422f, 260.694f, -156.728f, 260.511f, -155.975f, 256.002f, -155.975f, 247.248f, -156.979f, 241.487f, -157.754f, 241.225f, -161.273f, 242.240f, -160.616f, 244.382f, -160.775f, 245.503f, -160.025f, 249.003f, -160.025f, 250.315f, -160.491f, 250.471f, -161.226f, 249.490f, -162.225f, 242.996f, -162.977f, 239.740f, -166.979f, 230.987f, -169.981f, 226.983f, -176.485f, 220.730f, -184.237f, 216.228f, -193.248f, 213.725f, -193.275f, 236.750f, -193.523f, 244.009f, -188.258f, 241.283f, -187.789f, 241.987f, -188.519f, 242.233f, -188.770f, 242.981f, -191.515f, 243.980f, -191.770f, 244.732f, -193.253f, 245.474f, -194.233f, 245.229f, -194.972f, 243.750f, -194.475f, 242.750f, -194.475f, 229.245f, -194.975f, 229.225f, -194.995f, 228.975f, -197.475f, 228.727f, -197.475f, 225.506f, -197.235f, 225.025f, -194.995f, 225.025f, -194.975f, 224.525f, -194.475f, 224.505f, -194.476f, 213.245f, -197.501f, 212.725f, -197.525f, 212.991f, -202.475f, 213.146f, -202.475f, 212.020f, -200.247f, 211.525f, -196.001f, 211.525f, -194.269f, 211.772f, -194.007f, 212.524f, -189.506f, 213.274f, -182.821f, 215.423f, -181.762f, 216.522f, -175.013f, 220.246f, -168.769f, 226.266f, -165.514f, 231.271f, -162.725f, 229.747f, -168.480f, 221.735f, -169.592f, 220.845f, -169.231f, 220.484f, -173.235f, 216.730f, -182.742f, 211.226f, -185.752f, 210.225f, -187.503f, 211.724f, -189.219f, 211.479f, -188.980f, 210.525f, -187.976f, 210.507f, -188.750f, 209.725f, -190.016f, 209.731f, -189.779f, 210.982f, -190.725f, 211.218f, -190.736f, 209.480f, -192.068f, 209.295f, -192.245f, 208.725f, -194.482f, 208.725f, -194.720f, 208.010f, -193.976f, 207.507f, -193.975f, 205.248f, -194.977f, 202.741f, -198.245f, 200.225f, -201.763f, 200.229f, -205.019f, 202.734f, -206.025f, 205.245f, -206.012f, 207.522f, -199.515f, 207.525f, -199.277f, 208.002f, -199.518f, 208.725f, -208.007f, 208.726f, -208.120f, 209.321f, -209.273f, 209.490f, -208.819f, 210.645f, -210.507f, 211.725f, -212.730f, 211.725f, -212.983f, 210.732f, -213.740f, 210.725f, -214.248f, 210.225f, -217.258f, 211.226f, -226.765f, 216.730f, -230.775f, 220.497f, -227.493f, 223.274f, -225.983f, 221.518f, -220.487f, 217.521f, -213.678f, 214.117f, -206.007f, 212.278f, -205.525f, 212.760f, -205.525f, 224.505f, -205.025f, 224.525f, -205.005f, 225.025f, -202.765f, 225.025f, -202.588f, 225.380f, -202.775f, 228.001f, -202.525f, 228.727f, -205.005f, 228.975f, -205.025f, 229.225f, -205.525f, 229.245f, -205.525f, 242.750f, -205.029f, 243.748f, -206.013f, 245.225f, -206.987f, 245.225f, -207.487f, 244.479f, -210.971f, 242.737f, -210.733f, 241.785f, -207.520f, 243.268f, -207.261f, 244.022f, -206.226f, 243.756f, -206.475f, 236.250f, -206.501f, 213.725f, -213.760f, 215.477f, -222.262f, 219.978f, -229.520f, 226.484f, -234.522f, 233.988f, -237.523f, 241.741f, -238.272f, 244.983f, -238.775f, 245.247f, -239.024f, 252.484f, -239.502f, 252.723f, -239.953f, 252.572f, -240.098f, 250.405f, -239.877f, 246.764f, -239.108f, 242.195f, -237.477f, 236.760f, -235.475f, 233.002f, -235.485f, 232.230f, -237.489f, 230.978f, -238.269f, 230.984f, -242.352f, 241.330f, -242.774f, 241.245f, -244.025f, 247.247f],
        [-160.013f, 270.279f, -159.536f, 271.233f, -160.487f, 271.471f, -160.964f, 270.517f],
        [-189.224f, 254.505f, -187.977f, 252.490f, -189.009f, 252.227f, -189.770f, 252.736f, -190.020f, 253.725f, -190.730f, 253.725f, -190.959f, 252.579f, -190.226f, 251.495f, -191.469f, 249.998f, -190.984f, 249.270f, -187.239f, 247.772f, -186.485f, 246.772f, -185.503f, 246.526f, -185.029f, 246.763f, -185.272f, 247.734f, -188.507f, 249.725f, -189.514f, 249.729f, -189.768f, 250.518f, -179.512f, 256.022f, -163.997f, 265.275f, -163.230f, 265.015f, -162.725f, 263.505f, -161.225f, 256.005f, -160.976f, 251.766f, -160.498f, 251.527f, -160.025f, 251.685f, -160.025f, 255.997f, -160.775f, 258.746f, -160.685f, 259.295f, -161.062f, 261.183f, -161.524f, 261.995f, -161.774f, 264.742f, -162.767f, 266.728f, -163.983f, 266.971f, -164.234f, 266.230f, -165.009f, 266.227f, -166.265f, 268.225f, -167.492f, 268.225f, -170.133f, 266.304f, -168.978f, 264.261f, -169.147f, 264.135f, -168.988f, 263.728f, -174.238f, 260.728f, -175.716f, 260.284f, -177.463f, 259.325f, -179.237f, 257.728f, -182.985f, 255.979f, -183.740f, 255.227f, -185.512f, 257.225f, -186.494f, 257.225f, -188.977f, 255.983f],
        [-162.017f, 275.729f, -162.997f, 275.974f, -163.471f, 275.737f, -163.228f, 274.765f, -162.477f, 274.260f, -162.230f, 273.275f, -161.520f, 273.275f, -161.276f, 274.247f],
        [-196.530f, 230.418f, -195.550f, 230.753f, -195.765f, 231.465f, -196.719f, 230.987f],
        [-198.225f, 231.025f, -197.146f, 231.025f, -197.273f, 236.259f, -195.775f, 236.275f, -195.775f, 237.259f, -197.475f, 237.302f, -197.478f, 236.238f, -198.225f, 235.982f],
        [-204.522f, 262.484f, -224.012f, 273.728f, -229.768f, 276.732f, -225.542f, 281.740f, -226.559f, 282.147f, -230.983f, 277.482f, -231.475f, 277.239f, -231.475f, 275.780f, -230.246f, 276.025f, -228.739f, 275.272f, -223.736f, 272.271f, -223.725f, 272.025f, -222.736f, 271.771f, -222.147f, 270.571f, -216.996f, 267.589f, -216.241f, 267.773f, -213.990f, 266.024f, -212.237f, 265.771f, -211.979f, 265.017f, -209.493f, 263.525f, -208.736f, 263.521f, -208.338f, 262.537f, -205.986f, 261.271f, -206.115f, 260.998f, -205.742f, 260.774f, -201.525f, 260.525f, -201.525f, 261.475f, -204.257f, 261.476f],
        [-202.768f, 237.327f, -202.974f, 236.503f, -202.816f, 236.189f, -201.939f, 236.434f, -201.962f, 237.246f],
        [-202.735f, 232.225f, -203.262f, 231.228f, -203.275f, 236.497f, -203.501f, 237.400f, -203.712f, 237.421f, -204.232f, 237.291f, -204.458f, 230.982f, -204.055f, 230.378f, -202.025f, 230.275f, -202.025f, 232.225f],
        [-203.475f, 267.235f, -203.475f, 262.274f, -202.493f, 262.231f, -202.276f, 267.232f, -202.998f, 267.473f],
        [-210.019f, 252.231f, -217.763f, 255.979f, -222.014f, 258.480f, -222.439f, 259.321f, -225.971f, 261.314f, -230.012f, 262.978f, -232.758f, 264.975f, -234.012f, 264.978f, -234.268f, 265.725f, -235.264f, 265.729f, -236.275f, 266.753f, -234.849f, 270.772f, -235.725f, 271.272f, -235.731f, 270.984f, -236.229f, 270.650f, -237.475f, 267.190f, -237.475f, 265.765f, -212.490f, 251.273f, -210.230f, 250.516f, -210.238f, 249.728f, -211.494f, 249.725f, -213.733f, 248.481f, -214.221f, 247.748f, -213.987f, 247.281f, -211.762f, 248.022f, -210.260f, 249.273f, -209.015f, 249.275f, -208.778f, 249.750f],
        [-233.903f, 273.744f, -232.670f, 273.039f, -232.560f, 273.206f, -232.771f, 274.475f, -233.485f, 274.475f],
        [-238.610f, 263.225f, -238.725f, 260.495f, -239.315f, 260.285f, -239.971f, 256.025f, -239.725f, 256.003f, -239.832f, 254.387f, -239.514f, 254.281f, -238.775f, 256.006f, -238.524f, 260.506f, -237.772f, 260.769f, -237.280f, 263.225f],
        [-162.523f, 237.009f, -161.515f, 240.770f, -158.230f, 240.015f, -161.747f, 230.975f, -164.775f, 232.497f],
        [-198.754f, 199.025f, -196.264f, 200.021f, -193.979f, 202.264f, -195.984f, 199.731f, -199.000f, 198.475f, -202.011f, 198.728f, -204.016f, 199.731f, -206.023f, 201.759f, -205.737f, 202.271f, -203.735f, 200.020f, -202.243f, 199.275f],
        [-211.000f, 208.725f, -213.009f, 209.477f, -213.023f, 209.509f, -212.010f, 210.523f, -210.741f, 210.523f, -209.479f, 209.514f, -209.475f, 208.748f],
        [-231.730f, 227.016f, -228.975f, 224.250f, -231.498f, 221.725f, -237.270f, 229.235f, -237.272f, 229.762f, -234.488f, 231.272f],
        [-232.750f, 281.725f, -231.017f, 284.268f, -229.982f, 284.268f, -229.977f, 282.740f, -230.982f, 281.732f],
        [-246.273f, 279.009f, -244.264f, 281.021f, -242.729f, 281.514f, -245.730f, 278.985f, -246.725f, 276.745f, -246.725f, 273.755f, -245.479f, 270.766f, -242.231f, 268.266f, -242.743f, 267.476f, -245.270f, 269.736f, -247.274f, 274.493f, -247.274f, 276.256f]);

    private static readonly CorridorShape RingBoundary = new(BlundervilleCourse.Ring,
        [181.517f, -141.769f, 181.481f, -141.766f, 179.229f, -139.014f, 179.225f, -137.758f, 176.976f, -134.758f, 175.475f, -128.003f, 175.225f, -123.001f, 176.226f, -118.995f, 176.977f, -115.241f, 178.980f, -111.985f, 179.977f, -110.988f, 180.229f, -109.736f, 181.225f, -109.480f, 181.234f, -108.480f, 187.236f, -103.729f, 191.491f, -101.477f, 194.244f, -101.225f, 196.746f, -100.225f, 198.747f, -100.474f, 200.249f, -99.975f, 203.754f, -100.225f, 206.005f, -100.726f, 207.260f, -101.477f, 211.510f, -102.727f, 214.258f, -104.725f, 214.766f, -104.731f, 214.775f, -105.238f, 219.269f, -108.984f, 219.770f, -110.482f, 220.773f, -110.990f, 220.775f, -112.240f, 222.523f, -113.990f, 224.272f, -121.233f, 224.774f, -121.493f, 224.775f, -128.000f, 222.522f, -136.011f, 221.766f, -136.271f, 220.524f, -138.259f, 220.269f, -139.766f, 218.775f, -140.763f, 218.766f, -141.519f, 217.775f, -141.770f, 217.766f, -142.769f, 216.760f, -142.775f, 215.768f, -143.768f, 215.734f, -143.769f, 213.055f, -140.509f, 212.910f, -140.746f, 212.870f, -140.746f, 211.800f, -139.425f, 211.948f, -139.161f, 209.975f, -136.755f, 212.731f, -134.483f, 213.975f, -132.493f, 213.978f, -130.988f, 214.975f, -129.990f, 214.975f, -127.997f, 215.226f, -125.745f, 215.716f, -125.489f, 215.225f, -124.502f, 215.225f, -121.265f, 214.727f, -121.009f, 213.477f, -116.762f, 211.235f, -114.021f, 209.981f, -113.516f, 209.730f, -112.525f, 208.737f, -112.521f, 206.734f, -111.270f, 205.737f, -110.272f, 202.997f, -109.525f, 198.752f, -109.525f, 194.509f, -110.274f, 189.267f, -113.519f, 187.770f, -115.515f, 186.521f, -116.764f, 185.523f, -119.259f, 184.525f, -122.003f, 185.025f, -127.998f, 185.274f, -130.242f, 186.515f, -132.475f, 186.767f, -132.482f, 187.018f, -133.225f, 187.272f, -133.238f, 187.275f, -133.742f, 188.525f, -135.504f, 184.799f, -138.838f, 184.942f, -139.201f, 183.700f, -140.376f, 183.661f, -140.369f, 183.501f, -139.997f],
        [209.260f, -144.510f, 209.324f, -144.057f, 208.725f, -143.253f, 208.981f, -141.734f, 209.743f, -141.226f, 210.677f, -141.225f, 210.878f, -140.915f, 211.040f, -140.946f, 211.602f, -141.230f, 212.051f, -141.672f, 212.177f, -141.925f, 212.004f, -142.218f, 212.025f, -142.250f, 212.021f, -143.514f, 211.016f, -144.519f, 210.918f, -144.512f, 210.853f, -144.802f, 210.405f, -144.869f, 209.785f, -144.775f],
        [204.522f, -163.761f, 188.991f, -161.273f, 186.735f, -159.770f, 183.231f, -156.517f, 184.475f, -146.497f, 184.723f, -144.759f, 183.725f, -143.754f, 183.976f, -141.992f, 184.310f, -141.885f, 184.171f, -141.513f, 184.738f, -141.225f, 185.358f, -141.131f, 185.800f, -141.198f, 186.011f, -141.664f, 186.772f, -142.238f, 186.775f, -143.305f, 187.148f, -144.058f, 186.997f, -144.357f, 186.775f, -144.581f, 186.775f, -144.613f, 186.873f, -144.613f, 193.744f, -157.006f, 208.215f, -153.444f, 208.491f, -152.899f, 209.203f, -146.211f, 210.518f, -146.232f, 210.520f, -146.301f, 210.587f, -146.332f, 210.423f, -147.233f, 209.774f, -153.507f],
        [185.517f, -157.481f, 189.011f, -160.227f, 202.499f, -162.724f, 204.233f, -162.229f, 207.474f, -155.746f, 207.231f, -154.531f, 204.780f, -155.143f, 198.217f, -157.031f, 193.239f, -158.241f, 193.185f, -158.200f, 186.218f, -145.596f, 185.525f, -145.770f, 185.525f, -146.753f, 184.276f, -156.241f],
        [190.500f, -137.475f, 191.274f, -138.243f, 191.275f, -139.752f, 189.997f, -141.025f, 188.229f, -139.763f, 188.225f, -138.248f, 188.993f, -137.476f]);

    private static readonly CorridorShape FinalBoundary = new(BlundervilleCourse.Final,
        [14.488f, 120.772f, 15.225f, 121.263f, 15.236f, 122.271f, 15.998f, 122.027f, 17.975f, 123.015f, 17.975f, 123.968f, 15.980f, 123.486f, 10.225f, 135.501f, 15.752f, 138.025f, 16.275f, 137.754f, 19.273f, 121.240f, 17.496f, 120.475f, 17.719f, 121.481f, 16.505f, 121.723f, 11.760f, 118.477f, 8.756f, 117.476f, 6.727f, 117.239f, 8.741f, 118.523f, 10.482f, 118.525f, 10.739f, 119.272f],
        [-16.274f, 143.492f, -16.274f, 156.257f, -15.012f, 156.028f, -14.518f, 156.767f, -12.028f, 159.008f, -12.522f, 160.737f, -15.008f, 163.223f, -16.275f, 163.000f, -16.273f, 171.260f, -15.258f, 171.028f, -12.025f, 174.510f, -12.025f, 175.239f, -14.774f, 177.742f, -14.775f, 181.968f, -15.725f, 181.730f, -15.725f, 178.247f, -16.275f, 178.000f, -16.275f, 286.250f, -15.775f, 288.004f, -15.775f, 303.003f, -15.025f, 305.254f, -15.024f, 306.257f, -14.268f, 307.767f, -13.735f, 306.775f, -12.763f, 306.775f, -9.016f, 312.020f, -5.025f, 313.520f, -5.019f, 329.516f, -1.250f, 329.775f, 0.001f, 329.525f, 1.999f, 329.775f, 3.003f, 329.525f, 5.252f, 329.525f, 5.275f, 313.520f, 6.508f, 313.274f, 8.762f, 312.272f, 9.513f, 311.275f, 10.520f, 311.265f, 10.277f, 310.257f, 12.520f, 307.765f, 13.773f, 304.760f, 14.275f, 301.750f, 14.274f, 281.493f, 8.502f, 281.475f, 6.525f, 281.228f, 6.525f, 277.775f, 7.475f, 277.775f, 7.493f, 280.524f, 14.272f, 280.512f, 14.275f, 260.270f, 15.228f, 260.031f, 15.475f, 262.751f, 15.475f, 273.500f, 15.725f, 280.250f, 15.475f, 287.250f, 15.525f, 304.251f, 15.775f, 287.256f, 16.275f, 286.250f, 16.275f, 178.000f, 15.725f, 178.247f, 15.725f, 185.230f, 14.773f, 185.468f, 14.524f, 177.493f, 12.025f, 174.740f, 12.025f, 174.260f, 15.258f, 171.028f, 16.273f, 171.260f, 16.275f, 163.000f, 15.008f, 163.223f, 12.522f, 160.737f, 12.029f, 159.009f, 13.518f, 157.768f, 15.009f, 156.027f, 16.275f, 156.255f, 16.275f, 143.500f, 16.025f, 142.747f, 16.275f, 139.247f, 15.727f, 139.240f, 15.725f, 141.235f, 15.226f, 141.495f, 15.225f, 144.480f, 14.275f, 144.718f, 14.252f, 143.725f, 2.484f, 143.730f, 1.225f, 150.498f, 14.977f, 150.525f, 15.223f, 153.975f, 14.275f, 153.975f, 14.250f, 151.475f, 0.000f, 151.725f, -14.252f, 151.475f, -14.275f, 153.975f, -15.230f, 153.975f, -15.723f, 151.506f, -14.988f, 150.525f, -1.225f, 150.498f, -1.726f, 146.994f, -2.488f, 143.728f, -14.255f, 143.725f, -14.522f, 146.469f, -15.473f, 146.231f, -15.225f, 142.750f, -15.475f, 139.748f, -15.750f, 138.975f, -16.275f, 139.747f, -16.025f, 142.747f],
        [14.525f, 230.975f, 14.525f, 229.275f, 15.725f, 229.275f, 15.725f, 230.975f],
        [15.724f, 219.250f, 15.482f, 219.975f, 14.770f, 219.975f, 14.529f, 219.013f, 15.485f, 218.535f],
        [15.723f, 236.502f, 15.485f, 237.215f, 14.531f, 236.737f, 14.768f, 236.025f, 15.485f, 236.025f],
        [15.225f, 146.775f, 15.225f, 148.725f, 14.275f, 148.725f, 14.275f, 146.775f],
        [15.225f, 146.225f, 14.275f, 146.225f, 14.275f, 145.025f, 15.225f, 145.025f],
        [14.275f, 155.475f, 14.275f, 154.775f, 15.215f, 154.775f, 14.982f, 155.475f],
        [5.775f, 194.010f, 8.510f, 191.025f, 9.489f, 191.025f, 11.977f, 193.762f, 12.223f, 194.994f, 10.233f, 197.730f, 8.756f, 198.469f, 5.775f, 195.240f],
        [5.775f, 210.510f, 8.760f, 207.525f, 9.490f, 207.525f, 11.975f, 210.259f, 11.975f, 212.240f, 9.242f, 214.972f, 8.265f, 214.728f, 5.775f, 211.491f],
        [3.753f, 292.025f, 2.802f, 291.975f, 3.775f, 290.752f, 3.775f, 287.497f, 3.275f, 287.235f, 3.275f, 286.260f, 4.010f, 285.525f, 4.985f, 285.525f, 5.250f, 286.025f, 9.480f, 285.775f, 9.724f, 286.751f, 9.227f, 289.486f, 8.230f, 290.235f, 7.730f, 291.732f, 4.744f, 293.225f, 3.267f, 293.225f, 2.833f, 292.140f],
        [5.776f, 246.228f, 6.272f, 238.275f, 8.744f, 238.275f, 9.225f, 238.515f, 9.225f, 246.228f, 7.500f, 246.475f],
        [5.775f, 247.525f, 8.982f, 247.525f, 9.225f, 248.254f, 8.976f, 255.479f, 7.500f, 255.725f, 5.775f, 255.478f],
        [-2.991f, 206.722f, -5.226f, 203.990f, -5.474f, 202.009f, -2.988f, 199.025f, -2.018f, 199.025f, -1.768f, 199.767f, -0.001f, 201.025f, 1.768f, 199.767f, 2.018f, 199.025f, 2.988f, 199.025f, 5.725f, 202.260f, 5.725f, 203.490f, 2.739f, 206.725f, 2.012f, 206.725f, 0.001f, 204.225f, -2.014f, 206.478f],
        [-1.237f, 277.975f, -1.724f, 277.243f, -1.478f, 270.488f, -5.475f, 270.227f, -5.475f, 263.258f, -4.987f, 262.525f, 5.244f, 262.525f, 5.725f, 262.765f, 5.725f, 268.249f, 5.478f, 270.475f, 1.731f, 270.484f, 1.725f, 277.494f, 1.485f, 277.975f],
        [-2.977f, 189.238f, -3.223f, 187.758f, -0.985f, 185.270f, -0.004f, 184.780f, 2.726f, 187.262f, 2.974f, 189.241f, 0.000f, 192.215f],
        [-1.725f, 255.478f, -1.725f, 248.006f, -1.485f, 247.525f, 1.725f, 247.772f, 1.725f, 255.478f, 0.000f, 255.725f],
        [-1.475f, 238.521f, 0.000f, 238.275f, 1.725f, 238.522f, 1.725f, 246.228f, 0.000f, 246.475f, -1.475f, 246.229f],
        [-4.744f, 293.225f, -7.728f, 291.733f, -7.981f, 290.484f, -8.979f, 290.229f, -9.724f, 287.000f, -9.480f, 285.775f, -4.010f, 285.526f, -3.276f, 286.259f, -3.775f, 290.752f, -2.779f, 292.004f, -3.267f, 293.225f],
        [-9.225f, 246.240f, -9.225f, 238.756f, -8.985f, 238.276f, -5.775f, 238.523f, -5.775f, 246.240f, -6.260f, 246.725f, -8.740f, 246.725f],
        [-9.225f, 247.772f, -6.015f, 247.525f, -5.775f, 248.006f, -6.024f, 255.478f, -7.750f, 255.725f, -9.225f, 255.479f],
        [-11.977f, 195.739f, -12.223f, 194.257f, -9.488f, 191.025f, -8.512f, 191.025f, -6.025f, 193.760f, -6.025f, 195.740f, -8.760f, 198.475f, -9.489f, 198.475f],
        [-12.221f, 211.004f, -9.735f, 207.772f, -8.759f, 207.528f, -6.025f, 210.759f, -6.025f, 212.240f, -8.263f, 214.728f, -9.242f, 214.972f, -11.479f, 212.736f],
        [-15.475f, 268.520f, -14.519f, 268.281f, -14.275f, 269.502f, -14.268f, 280.518f, -7.493f, 280.524f, -7.475f, 277.768f, -6.757f, 277.529f, -6.275f, 278.010f, -6.275f, 281.228f, -8.002f, 281.475f, -14.269f, 281.484f, -14.275f, 286.997f, -14.518f, 287.970f, -15.475f, 287.730f],
        [-15.224f, 300.769f, -14.270f, 300.531f, -13.525f, 304.500f, -13.769f, 305.716f, -14.976f, 305.233f],
        [-14.275f, 146.775f, -14.275f, 148.725f, -15.225f, 148.725f, -15.225f, 146.775f],
        [-14.982f, 155.475f, -15.215f, 154.775f, -14.275f, 154.775f, -14.275f, 155.475f],
        [-14.524f, 258.025f, -14.275f, 262.995f, -14.513f, 263.471f, -15.475f, 263.230f, -15.475f, 259.252f, -15.230f, 258.025f],
        [-14.525f, 230.975f, -15.725f, 230.975f, -15.725f, 229.275f, -14.525f, 229.275f],
        [-15.725f, 183.015f, -14.525f, 182.775f, -14.525f, 184.975f, -15.725f, 184.975f],
        [-14.531f, 219.263f, -14.765f, 219.965f, -15.719f, 219.487f, -15.485f, 218.785f],
        [14.275f, 139.750f, 2.995f, 139.725f, 2.476f, 142.505f, 14.257f, 142.774f],
        [-7.025f, 135.007f, -7.025f, 136.234f, -12.273f, 138.759f, -3.003f, 138.775f, -2.024f, 139.020f, -1.775f, 143.004f, -1.273f, 143.512f, 0.011f, 150.773f, 1.273f, 143.512f, 1.775f, 143.004f, 2.024f, 139.024f, 12.273f, 138.761f, 8.760f, 136.727f, 7.518f, 136.725f, 7.029f, 135.259f, 7.991f, 134.538f, 8.487f, 135.521f, 9.272f, 135.261f, 10.275f, 132.747f, 9.019f, 133.216f, 8.776f, 132.004f, 9.522f, 131.011f, 11.022f, 128.011f, 11.274f, 126.760f, 13.771f, 122.737f, 10.760f, 120.977f, 9.730f, 120.985f, 8.226f, 123.993f, 8.721f, 125.245f, 6.978f, 126.988f, 6.975f, 127.975f, 6.727f, 127.989f, 4.477f, 132.741f, 4.475f, 133.974f, -2.250f, 133.475f, -3.009f, 134.223f, -4.478f, 133.978f, -4.727f, 131.990f, -5.974f, 130.244f, -5.727f, 128.740f, -6.236f, 127.979f, -6.975f, 127.975f, -6.980f, 127.236f, -8.721f, 125.245f, -8.226f, 123.993f, -9.732f, 120.983f, -13.513f, 122.479f, -13.272f, 124.011f, -12.272f, 126.013f, -11.904f, 126.201f, -11.275f, 127.207f, -11.271f, 127.764f, -10.769f, 128.018f, -9.275f, 131.255f, -9.272f, 132.262f, -8.517f, 132.521f],
        [8.265f, 114.230f, 4.475f, 114.249f, 11.770f, 116.765f, 11.765f, 116.730f],
        [6.990f, 123.773f, 8.773f, 120.240f, 8.260f, 119.477f, 4.739f, 118.728f, 3.476f, 121.494f, 3.727f, 122.260f],
        [5.225f, 291.501f, 7.521f, 290.014f, 8.264f, 287.229f, 4.980f, 287.236f],
        [-5.021f, 128.015f, -3.515f, 131.270f, -3.230f, 130.275f, -2.275f, 130.275f, -2.264f, 132.521f, 2.255f, 132.525f, 2.159f, 130.275f, 2.985f, 130.275f, 3.519f, 131.267f, 5.021f, 128.015f, 6.272f, 126.762f, 6.774f, 125.243f, 3.005f, 123.225f, 2.730f, 124.225f, 2.020f, 124.225f, 1.755f, 122.975f, -1.757f, 122.976f, -2.020f, 124.225f, -2.730f, 124.225f, -3.005f, 123.225f, -6.773f, 125.241f, -6.271f, 126.764f],
        [2.265f, 128.475f, 2.025f, 127.994f, 2.025f, 125.775f, 2.975f, 126.018f, 2.975f, 128.475f],
        [-2.975f, 126.025f, -2.025f, 126.025f, -2.025f, 127.994f, -2.265f, 128.475f, -2.975f, 128.475f],
        [4.500f, 269.275f, 4.514f, 263.729f, -4.271f, 263.736f, -4.264f, 269.271f],
        [4.004f, 116.525f, 2.250f, 116.225f, -2.250f, 116.225f, -4.025f, 116.496f, -2.602f, 116.725f, -4.025f, 116.750f, 3.751f, 117.275f, 4.012f, 116.728f, 2.602f, 116.725f],
        [0.002f, 118.225f, -3.272f, 118.488f, -2.014f, 121.521f, 2.021f, 121.514f, 3.271f, 118.486f],
        [1.266f, 188.770f, 0.000f, 187.225f, -1.275f, 188.750f, 0.000f, 189.775f],
        [0.500f, 245.025f, 0.525f, 239.997f, -0.003f, 239.725f, -0.025f, 245.002f],
        [0.525f, 249.250f, 0.001f, 248.975f, -0.524f, 249.245f, -0.505f, 254.025f, 0.525f, 254.005f],
        [0.000f, 270.475f, -0.275f, 274.253f, -0.024f, 276.508f, 0.502f, 276.525f, 0.525f, 270.498f],
        [-2.500f, 142.775f, -2.993f, 139.726f, -14.275f, 139.995f, -14.255f, 142.775f],
        [-3.489f, 122.022f, -4.740f, 118.727f, -8.773f, 119.741f, -7.010f, 123.773f],
        [-4.492f, 114.524f, -8.255f, 114.226f, -11.274f, 116.257f, -10.494f, 116.524f],
        [-5.009f, 291.273f, -4.981f, 287.234f, -8.266f, 287.231f, -7.519f, 290.266f],
        [-9.000f, 118.525f, -7.486f, 118.020f, -7.475f, 117.522f, -5.488f, 117.272f, -5.478f, 116.738f, -6.503f, 116.725f, -8.506f, 117.226f, -13.009f, 118.977f, -14.514f, 120.478f, -16.024f, 121.008f, -14.991f, 121.273f, -10.987f, 119.271f, -10.487f, 118.525f],
        [-8.000f, 239.725f, -8.025f, 245.005f, -6.995f, 245.025f, -6.975f, 239.745f],
        [-9.271f, 212.514f, -7.725f, 211.505f, -8.746f, 209.725f, -10.275f, 210.995f],
        [-11.979f, 130.519f, -11.225f, 130.255f, -11.734f, 129.481f, -13.210f, 129.475f, -12.475f, 128.001f, -12.979f, 126.987f, -14.484f, 126.975f, -15.983f, 123.482f, -18.225f, 123.969f, -18.225f, 123.017f, -16.984f, 122.519f, -16.480f, 121.486f, -17.975f, 121.965f, -17.998f, 120.725f, -18.510f, 120.727f, -19.266f, 121.231f, -19.274f, 122.756f, -16.012f, 138.022f, -10.228f, 135.512f, -12.224f, 131.497f]);

}
