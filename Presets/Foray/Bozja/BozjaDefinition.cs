using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed record BozjaDefinition(string Key, string Name, uint PrimaryOid, uint DynamicEventId,
    Vector3 Center, IGeometryShape Arena, BozjaRule[] Rules, float PullRadius = 30, float SampleRadius = 29.5f,
    bool SquarePull = false, uint Territory = BozjaCatalog.TERRITORY, bool OnlyLoadIfTargetable = false,
    bool PullWhileUntargetable = false, uint FateId = 0);
