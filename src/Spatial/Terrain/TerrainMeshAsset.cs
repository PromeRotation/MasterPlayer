namespace MasterPlayer.Spatial.Terrain;

internal sealed class TerrainMeshAsset
{
    internal string Key { get; }
    internal int Bytes { get; }
    internal Lazy<TerrainMesh> Mesh { get; }
    internal bool Encoded { get; }

    internal TerrainMeshAsset(string key, byte[] nodes)
    {
        Key = key;
        Bytes = nodes.Length;
        Encoded = true;
        Mesh = new(() => TerrainMesh.Decode(nodes));
    }

    internal TerrainMeshAsset(string key, TerrainMesh mesh)
    {
        Key = key;
        Bytes = mesh.Vertices.Length * 12 + mesh.Triangles.Length * 24;
        Mesh = new(() => mesh);
    }
}
