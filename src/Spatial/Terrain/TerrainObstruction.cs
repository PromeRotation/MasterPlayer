using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal readonly record struct TerrainObstruction(Vector3 Position, TerrainCellKind Kind, Vector3 From, Vector3 To);
