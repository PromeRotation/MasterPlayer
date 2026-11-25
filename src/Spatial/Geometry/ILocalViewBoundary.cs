using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

// Long course navigation retains the full topology; rendering uses a local viewport.
internal interface ILocalViewBoundary : IStaticArenaBoundary
{
    float ViewRadius { get; }
    Vector3? ViewCenter => null;
    bool ShowBoundary => true;
}
