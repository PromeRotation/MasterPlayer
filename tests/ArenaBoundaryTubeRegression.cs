using System.Numerics;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial.Geometry;

internal static class ArenaBoundaryTubeRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("Arena boundary tube rejects a diagonal edge clipped between cardinal samples", () =>
        {
            var diamond = new WorldPolygonGeometry([new(0,0,4),new(4,0,0),new(0,0,-4),new(-4,0,0)]);
            Check(ArenaBoundaryTraversal.ContainsTube(diamond,new(1,0,1),new(1.5f,0,1.5f),.7f), "An interior tube was rejected.");
            Check(!ArenaBoundaryTraversal.ContainsTube(diamond,new(1,0,1),new(1.5f,0,1.5f),.8f), "The diagonal edge cut the tube despite four axial samples fitting.");
        });
        run("Arena boundary tube protects concave corners and narrow passages", () =>
        {
            var corner = new WorldPolygonGeometry([new(0,0,0),new(6,0,0),new(6,0,2),new(2,0,2),new(2,0,6),new(0,0,6)]);
            Check(!ArenaBoundaryTraversal.ContainsTube(corner,new(1,0,5),new(5,0,1),.1f), "A diagonal crossed the concavity.");
            Check(ArenaBoundaryTraversal.ContainsTube(corner,new(1,0,1),new(1,0,5),.99f), "The wide enough passage was rejected.");
            Check(!ArenaBoundaryTraversal.ContainsTube(corner,new(1,0,1),new(1,0,5),1.01f), "An oversized tube entered a narrow passage.");
        });
        run("Arena boundary tube uses exact circle and donut clearance", () =>
        {
            var circle = new CircleGeometry(Vector3.Zero,10);
            Check(ArenaBoundaryTraversal.ContainsTube(circle,new(0,0,0),new(9,0,0),1), "Circle tangency was rejected.");
            Check(!ArenaBoundaryTraversal.ContainsTube(circle,new(0,0,0),new(9,0,0),1.01f), "The outer circle was clipped.");
            var ring = new DonutGeometry(Vector3.Zero,3,10);
            Check(ArenaBoundaryTraversal.ContainsTube(ring,new(-4,0,4),new(4,0,4),1), "A tangent to the inner circle was rejected.");
            Check(!ArenaBoundaryTraversal.ContainsTube(ring,new(-4,0,4),new(4,0,4),1.01f), "The inner hole was clipped.");
            Check(!ArenaBoundaryTraversal.ContainsTube(ring,new(-6,0,0),new(6,0,0),.1f), "A tube crossed the ring's hole.");
        });
        run("Arena boundary tube checks both fan rays and its outer arc", () =>
        {
            var fan = new FanGeometry(Vector3.Zero,0,10,MathF.PI/2);
            Check(ArenaBoundaryTraversal.ContainsTube(fan,new(0,0,2),new(0,0,8),1), "A tube on the fan centreline was rejected.");
            Check(!ArenaBoundaryTraversal.ContainsTube(fan,new(0,0,.5f),new(0,0,8),1), "The tube clipped the fan apex.");
            Check(!ArenaBoundaryTraversal.ContainsTube(fan,new(0,0,2),new(0,0,9.1f),1), "The tube clipped the fan arc.");
        });
        run("Arena boundary tube indexed contours agree across bridge holes and bin boundaries", () =>
        {
            var prepared = BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA);
            var plain = new Unindexed(prepared);
            foreach (var x in new[] {-212f,-209f,-203f,-200f,-197f,-191f})
            foreach (var z in new[] {-181f,-185.99f,-190f,-202f,-205f})
            foreach (var offset in new[] {new Vector3(0,0,-5),new Vector3(5,0,-2),new Vector3(-4,0,1)})
            foreach (var radius in new[] {0f,.2f,.75f})
            {
                var a = new Vector3(x,17,z); var b = a+offset;
                Check(ArenaBoundaryTraversal.ContainsTube(prepared,a,b,radius) == ArenaBoundaryTraversal.ContainsTube(plain,a,b,radius),
                    $"Indexed clearance differs at {a} -> {b}, radius={radius}.");
            }
            Check(!ArenaBoundaryTraversal.ContainsTube(prepared,new(-205,17,-190),new(-195,17,-190),.1f), "The bridge's central hole was crossed.");
        });
        run("Arena boundary tube rejects invalid input", () =>
        {
            var circle = new CircleGeometry(Vector3.Zero,10);
            foreach (var radius in new[] {-1f,float.NaN,float.PositiveInfinity})
                Check(!ArenaBoundaryTraversal.ContainsTube(circle,Vector3.Zero,Vector3.UnitX,radius), "Invalid radius was accepted.");
            Check(!ArenaBoundaryTraversal.ContainsTube(circle,new(float.NaN,0,0),Vector3.Zero,1), "An invalid point was accepted.");
        });
    }

    private sealed class Unindexed(IGeometryShape shape) : IGeometryShape
    {
        public GeometryKind Kind => shape.Kind;
        public Vector3 Position => shape.Position;
        public float Rotation => shape.Rotation;
        public bool Contains(Vector3 point,float epsilon=GeometryHelper.DefaultEpsilon) => shape.Contains(point,epsilon);
        public bool IntersectsSegment(Vector3 a,Vector3 b,float epsilon=GeometryHelper.DefaultEpsilon) => shape.IntersectsSegment(a,b,epsilon);
        public GeometryBounds GetBounds() => shape.GetBounds();
        public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments=64) => shape.GetContours(segments);
    }
    private static void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
}
