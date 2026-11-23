using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed record HuntDrawingPrimitive(string Id, IGeometryShape Geometry, bool Safe, Vector4 Color);
