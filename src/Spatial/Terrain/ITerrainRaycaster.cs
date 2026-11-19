using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal interface ITerrainRaycaster
{
    bool IsAvailable { get; }
    bool Cast(Vector3 origin, Vector3 direction, float distance, out Vector3 point, out Vector3 normal);
}
