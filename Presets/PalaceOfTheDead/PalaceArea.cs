using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed record PalaceArea(string Key, string Name, GeometryTemplate Shape, Vector3 Position, float Rotation,
    DateTime From, DateTime Until, ulong Owner = 0, uint Action = 0, bool Safe = false, bool NavigationOnly = false,
    bool PreviewOnly = false, DateTime Telegraph = default, bool OutlineOnly = false, bool Obstacle = false,
    int Priority = 5, bool OverrideDangerMargin = true)
{
    internal IGeometryShape Geometry => Shape.Resolve(Position, Rotation, null);
    internal DangerWindow Window => new(From, Until == DateTime.MaxValue ? null : Until, Telegraph)
    {
        HasExplicitTiming = true,
    };
}
