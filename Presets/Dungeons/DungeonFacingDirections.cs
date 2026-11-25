namespace MasterPlayer.Mechanics.Dungeons;

internal static class DungeonFacingDirections
{
    internal static float? Solve((float Center, float HalfWidth)[] directions, float rotation)
    {
        if (directions.Length == 0) return null;
        var edges = directions.SelectMany(d => new[] { d.Center - d.HalfWidth, d.Center + d.HalfWidth })
            .Select(d => (d % MathF.Tau + MathF.Tau) % MathF.Tau).Order().ToArray();
        var candidates = new[] { rotation }.Concat(directions.SelectMany(d => new[]
            { d.Center + MathF.PI, d.Center + d.HalfWidth + .05f, d.Center - d.HalfWidth - .05f }))
            .Concat(edges.Select((edge, i) => (edge + (i + 1 < edges.Length ? edges[i + 1] : edges[0] + MathF.Tau)) / 2));
        foreach (var margin in new[] { .01f, 0f })
            foreach (var candidate in candidates)
                if (float.IsFinite(candidate) && directions.All(d => MathF.Abs(MathF.Atan2(
                    MathF.Sin(candidate - d.Center), MathF.Cos(candidate - d.Center))) > d.HalfWidth + margin))
                    return candidate;
        return null;
    }
}
