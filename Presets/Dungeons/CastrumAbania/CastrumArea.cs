using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record CastrumArea(string Key, string Name, GeometryTemplate Shape,
    Vector3 Position, float Rotation, DateTime From, DateTime Until, ulong Owner = 0, uint Action = 0,
    bool Safe = false, bool NavigationOnly = false, bool PreviewOnly = false, bool OutlineOnly = false)
{
    internal IGeometryShape Geometry => Shape.Resolve(Position, Rotation, null);
    internal DangerWindow Window => new(From, Until == DateTime.MaxValue ? null : Until, From.AddSeconds(-5)) { HasExplicitTiming = true };
}
