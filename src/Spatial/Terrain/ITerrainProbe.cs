using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal interface ITerrainProbe
{
    bool IsAvailable { get; }
    void BeginScanBatch();
    void EndScanBatch();
    bool TryGetGround(Vector3 reference, out Vector3 floor);
    TerrainCellKind Sample(Vector3 reference, out Vector3 floor);
    bool CanTraverse(Vector3 from, Vector3 to);
    bool CanTraverse(Vector3 from, Vector3 to, out TerrainObstruction? obstruction);
}
