using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal sealed class TerrainSceneSnapshot(TerrainSceneSnapshot.Instance[] instances)
{
    internal sealed record Instance(ulong Id, TerrainMeshAsset Asset, Matrix4x4 World, ulong Material, ulong MaterialMask);
    internal Instance[] Instances { get; } = instances;
    internal bool SameGeometry(TerrainSceneSnapshot other) => Instances.AsSpan().SequenceEqual(other.Instances);
    internal static ulong ResolveMaterial(ulong primitive, ulong value, ulong mask)
        => mask == 0 ? primitive : (primitive & ~mask) | value;
    internal static bool IsUnwalkableMaterial(ulong material)
        => (material & 0x2000000) != 0 || (material & 0x1F) == 0x11;
    internal static bool IsActive(byte visibility, ulong layer) => (visibility & 1) != 0 && (layer & 1) != 0;
}
