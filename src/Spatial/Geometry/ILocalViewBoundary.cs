namespace MasterPlayer.Spatial.Geometry;

// Long course navigation retains the full topology; rendering uses a local viewport.
internal interface ILocalViewBoundary : IStaticArenaBoundary
{
    float ViewRadius { get; }
}
