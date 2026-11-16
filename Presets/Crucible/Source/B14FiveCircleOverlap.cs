namespace MasterPlayer.Mechanics.Crucible;

// Damage minimization for the observed full-coverage pattern, never zero-hit safety.
internal sealed class B14FiveCircleOverlap(WPos[] origins) : ShapeDistance
{
    internal static bool TryCreate(WPos center, Components.GenericAOEs.AOEInstance[] active, out ShapeDistance? overlap)
    {
        overlap = null;
        var circles = active.Where(a => a.Shape is AOEShapeCircle { Radius: 20 }).ToArray();
        if (circles.Length != 5 || circles.Any(a => a.Activation == default)
            || (circles.Max(a => a.Activation) - circles.Min(a => a.Activation)).TotalSeconds > .05) return false;
        WDir[] offsets = [new(-15,-15), new(15,-15), new(-15,15), new(15,15), new(0,0)];
        if (offsets.Any(d => !circles.Any(a => (a.Origin - (center+d)).LengthSq() < .04f))) return false;
        overlap = new B14FiveCircleOverlap(circles.Select(a => a.Origin).ToArray());
        return true;
    }
    public override float Distance(in WPos p)
    {
        var first = float.PositiveInfinity; var second = first;
        foreach (var center in origins)
        {
            var d = (p-center).Length()-20.35f;
            if (d < first) { second=first; first=d; } else if (d < second) second=d;
        }
        return second;
    }
    public override string? ContentKey() => "B14-overlap|"+string.Join(";",origins.Select(p=>$"{p.X:F2},{p.Z:F2}"));
}
