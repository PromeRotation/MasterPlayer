using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal sealed class TerrainMeshAsset
{
    internal string Key { get; }
    internal int Bytes { get; }
    internal Lazy<TerrainMesh> Mesh { get; }
    internal bool Encoded { get; }
    internal Lazy<(Vector3 Min, Vector3 Max)> Bounds { get; }

    internal TerrainMeshAsset(string key, byte[] nodes)
    {
        Key = key;
        Bytes = nodes.Length;
        Encoded = true;
        Mesh = new(() => TerrainMesh.Decode(nodes));
        Bounds = new(MeasureBounds);
    }

    internal TerrainMeshAsset(string key, TerrainMesh mesh)
    {
        Key = key;
        Bytes = mesh.Vertices.Length * 12 + mesh.Triangles.Length * 24;
        Mesh = new(() => mesh);
        Bounds = new(MeasureBounds);
    }

    private (Vector3 Min, Vector3 Max) MeasureBounds()
    {
        var minimum = new Vector3(float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity);
        foreach (var vertex in Mesh.Value.Vertices)
        {
            minimum = Vector3.Min(minimum, vertex);
            maximum = Vector3.Max(maximum, vertex);
        }
        return (minimum, maximum);
    }
}
