using System.Numerics;

namespace MasterPlayer.Drawing;

internal readonly record struct GroundTriangle(Vector3 A, Vector3 B, Vector3 C, int Layer)
{
    internal Vector2 Min => Vector2.Min(new(A.X, A.Z), Vector2.Min(new(B.X, B.Z), new(C.X, C.Z)));
    internal Vector2 Max => Vector2.Max(new(A.X, A.Z), Vector2.Max(new(B.X, B.Z), new(C.X, C.Z)));

    internal bool TryHeight(Vector2 point, out float height)
    {
        var ab = new Vector2(B.X - A.X, B.Z - A.Z);
        var ac = new Vector2(C.X - A.X, C.Z - A.Z);
        var ap = point - new Vector2(A.X, A.Z);
        var determinant = Cross(ab, ac);
        var u = Cross(ap, ac) / determinant;
        var v = Cross(ab, ap) / determinant;
        height = A.Y + u * (B.Y - A.Y) + v * (C.Y - A.Y);
        return u >= -0.0001f && v >= -0.0001f && u + v <= 1.0001f;
    }

    internal static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}
