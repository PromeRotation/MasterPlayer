using System.Diagnostics;
using System.Numerics;
using MasterPlayer.Spatial.Terrain;

internal sealed class AnalyticTerrainRaycaster : ITerrainRaycaster
{
    public bool IsAvailable { get; set; } = true;
    public bool ThrowOnCast { get; set; }
    public bool OmitNormals { get; set; }
    public float Slope { get; set; }
    public float? AdditionalFloorHeight { get; set; }
    public float? CliffAtX { get; set; }
    public float? Ceiling { get; set; }
    public (float MinX, float MaxX, float Height)? RaisedFloor { get; set; }
    public double WorkMilliseconds { get; set; }
    public List<(float X, float MinZ, float MaxZ, float Height)> Walls { get; } = [];
    public List<(float Z, float MinX, float MaxX, float Height)> CrossWalls { get; } = [];
    public int Calls { get; private set; }

    public bool Cast(Vector3 origin, Vector3 direction, float distance, out Vector3 point, out Vector3 normal)
    {
        ++Calls;
        if (WorkMilliseconds > 0)
        {
            var start = Stopwatch.GetTimestamp();
            while (Stopwatch.GetElapsedTime(start).TotalMilliseconds < WorkMilliseconds) Thread.SpinWait(16);
        }
        if (ThrowOnCast) throw new InvalidOperationException("Synthetic collision query failure");
        if (!IsAvailable) throw new InvalidOperationException("A query was made against an unavailable native scene.");
        var nearest = float.PositiveInfinity;
        var hitNormal = Vector3.Zero;
        var denominator = direction.Y - Slope * direction.X;
        if (MathF.Abs(denominator) > .00001f)
        {
            var t = (Slope * origin.X - origin.Y) / denominator;
            var candidate = origin + direction * t;
            var raised = RaisedFloor;
            if ((CliffAtX == null || candidate.X < CliffAtX)
                && !(raised is { } region && candidate.X >= region.MinX && candidate.X <= region.MaxX))
                Hit(t, Vector3.Normalize(new(-Slope, 1, 0)));
            if (raised is { } platform)
            {
                var raisedT = (platform.Height + Slope * origin.X - origin.Y) / denominator;
                var raisedPoint = origin + direction * raisedT;
                if (raisedPoint.X >= platform.MinX && raisedPoint.X <= platform.MaxX)
                    Hit(raisedT, Vector3.Normalize(new(-Slope, 1, 0)));
            }
            if (AdditionalFloorHeight is { } height) Hit((height + Slope * origin.X - origin.Y) / denominator, Vector3.Normalize(new(-Slope, 1, 0)));
        }
        foreach (var wall in Walls)
            if (MathF.Abs(direction.X) > .00001f)
            {
                var t = (wall.X - origin.X) / direction.X;
                var candidate = origin + direction * t;
                if (candidate.Z >= wall.MinZ && candidate.Z <= wall.MaxZ && candidate.Y >= 0 && candidate.Y <= wall.Height)
                    Hit(t, new(-MathF.Sign(direction.X), 0, 0));
            }
        foreach (var wall in CrossWalls)
            if (MathF.Abs(direction.Z) > .00001f)
            {
                var t = (wall.Z - origin.Z) / direction.Z;
                var candidate = origin + direction * t;
                if (candidate.X >= wall.MinX && candidate.X <= wall.MaxX && candidate.Y >= 0 && candidate.Y <= wall.Height)
                    Hit(t, new(0, 0, -MathF.Sign(direction.Z)));
            }
        if (Ceiling is { } ceiling && direction.Y > .00001f) Hit((ceiling - origin.Y) / direction.Y, -Vector3.UnitY);
        point = float.IsFinite(nearest) ? origin + direction * nearest : default;
        normal = OmitNormals ? Vector3.Zero : hitNormal;
        return float.IsFinite(nearest);
        void Hit(float t, Vector3 n)
        {
            if (t < 0 || t > distance || t >= nearest) return;
            nearest = t; hitNormal = n;
        }
    }
}
