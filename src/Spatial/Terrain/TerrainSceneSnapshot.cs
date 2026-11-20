using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal sealed class TerrainSceneSnapshot(TerrainSceneSnapshot.Instance[] instances)
{
    internal sealed record Instance(ulong Id, TerrainMeshAsset Asset, Matrix4x4 World, ulong Material, ulong MaterialMask)
    {
        internal (Vector3 Min, Vector3 Max)? WorldBounds { get; init; }

        internal (Vector3 Min, Vector3 Max)? GetBounds(bool decode)
        {
            if (WorldBounds is { } bounds) return bounds;
            if (!decode && !Asset.Bounds.IsValueCreated) return null;
            var local = Asset.Bounds.Value;
            var minimum = new Vector3(float.PositiveInfinity);
            var maximum = new Vector3(float.NegativeInfinity);
            for (var corner = 0; corner < 8; ++corner)
            {
                var point = Vector3.Transform(new Vector3((corner & 1) == 0 ? local.Min.X : local.Max.X,
                    (corner & 2) == 0 ? local.Min.Y : local.Max.Y, (corner & 4) == 0 ? local.Min.Z : local.Max.Z), World);
                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
            }
            return (minimum, maximum);
        }

        internal bool SameGeometry(Instance other) => Id == other.Id && ReferenceEquals(Asset, other.Asset)
            && World == other.World && Material == other.Material && MaterialMask == other.MaterialMask;
    }
    internal Instance[] Instances { get; } = instances;
    internal bool SameGeometry(TerrainSceneSnapshot other)
    {
        if (Instances.Length != other.Instances.Length) return false;
        for (var i = 0; i < Instances.Length; ++i)
            if (!Instances[i].SameGeometry(other.Instances[i])) return false;
        return true;
    }
    internal static ulong ResolveMaterial(ulong primitive, ulong value, ulong mask)
        => mask == 0 ? primitive : (primitive & ~mask) | value;
    internal static bool IsUnwalkableMaterial(ulong material)
        => (material & 0x2000000) != 0 || (material & 0x1F) == 0x11;
    internal static bool IsActive(byte visibility, ulong layer) => (visibility & 1) != 0 && (layer & 1) != 0;
}
