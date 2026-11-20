using System.Numerics;
using PromeRotation.GreenMoveSystem;

internal sealed class Pt10SimulatedMove : IGreenMoveSystem
{
    private IReadOnlyList<GreenMovePoint> points = [];
    private int index;
    private double elapsed;
    private double ready;
    public bool IsRunning => index < points.Count;
    public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? points[index] : null,
        points.Count > 0 ? points[^1] : null, index, points.Count, points.Count - index);
    public void MoveTo(GreenMovePoint point) => MoveTo([point]);
    public void MoveTo(IReadOnlyList<GreenMovePoint> route)
    {
        if (!IsRunning) ready = elapsed + .08;
        points = route;
        index = 0;
    }
    public void Stop() { points = []; index = 0; }
    public void Append(GreenMovePoint point) => throw new NotSupportedException();
    public void Append(IReadOnlyList<GreenMovePoint> route) => throw new NotSupportedException();
    public void ClearQueue() => throw new NotSupportedException();
    public void SkipCurrent() => ++index;
    public Vector3 Advance(Vector3 current, float dt)
    {
        elapsed += dt;
        if (elapsed < ready) return current;
        while (IsRunning)
        {
            var point = points[index];
            var delta = point.Position - current;
            delta.Y = 0;
            if (delta.Length() <= point.Tolerance) { ++index; continue; }
            return current + Vector3.Normalize(delta) * Math.Min(6 * point.SpeedMultiplier * dt, delta.Length());
        }
        return current;
    }
}
