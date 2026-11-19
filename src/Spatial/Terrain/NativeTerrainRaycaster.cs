using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;

namespace MasterPlayer.Spatial.Terrain;

// Called only by the main-thread terrain controller, never by the solver.
internal sealed unsafe class NativeTerrainRaycaster : ITerrainRaycaster
{
    internal static (nint Director, byte Floor, byte Layout) CaptureLayoutIdentity()
    {
        var framework = EventFramework.Instance();
        var dungeon = framework == null ? null : framework->GetInstanceContentDeepDungeon();
        if (dungeon == null) return default;
        return ((nint)dungeon, dungeon->Floor, dungeon->ActiveLayoutIndex);
    }

    public bool IsAvailable
    {
        get
        {
            var framework = Framework.Instance();
            var collision = framework == null ? null : framework->BGCollisionModule;
            return collision != null && !collision->ShuttingDown && collision->LoadInProgressCounter == 0
                && BGCollisionModule.Addresses.RaycastMaterialFilter.Value != nint.Zero;
        }
    }

    public bool Cast(Vector3 origin, Vector3 direction, float distance, out Vector3 point, out Vector3 normal)
    {
        var result = BGCollisionModule.RaycastMaterialFilter(origin, direction, out var hit, distance);
        point = hit.Point;
        normal = hit.Normal;
        return result;
    }
}
