using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal interface ITerrainSceneSource
{
    TerrainCaptureDiagnostics? Diagnostics => null;
    bool TryCapture(Vector3 origin, float radius, out TerrainSceneSnapshot? snapshot);
}
