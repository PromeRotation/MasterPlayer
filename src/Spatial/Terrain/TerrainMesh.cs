using System.Buffers.Binary;
using System.Numerics;

namespace MasterPlayer.Spatial.Terrain;

internal sealed class TerrainMesh(Vector3[] vertices, TerrainMesh.Triangle[] triangles)
{
    internal readonly record struct Triangle(int A, int B, int C, ulong Material);
    internal Vector3[] Vertices { get; } = vertices;
    internal Triangle[] Triangles { get; } = triangles;

    // A detached copy of a MeshPCB.FileNode tree, starting at its root node.
    internal static TerrainMesh Decode(byte[] data)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<Triangle>();
        var pending = new Stack<int>();
        var visited = new HashSet<int>();
        pending.Push(0);
        while (pending.TryPop(out var offset))
        {
            if (!visited.Add(offset)) throw new InvalidDataException("碰撞模型包含循环节点");
            if (offset < 0 || offset > data.Length - 48) throw new InvalidDataException("碰撞模型节点越界");
            var node = data.AsSpan(offset);
            var compressed = BinaryPrimitives.ReadUInt16LittleEndian(node[40..]);
            var primitiveCount = BinaryPrimitives.ReadUInt16LittleEndian(node[42..]);
            var raw = BinaryPrimitives.ReadUInt16LittleEndian(node[44..]);
            if (raw + compressed > 256) throw new InvalidDataException("碰撞模型顶点数量无效");
            var length = 48 + raw * 12 + compressed * 6 + primitiveCount * 12;
            if (length > node.Length) throw new InvalidDataException("碰撞模型数据不完整");
            var minimum = ReadVector(node[16..]);
            var scale = (ReadVector(node[28..]) - minimum) / 65535f;
            var first = vertices.Count;
            for (var i = 0; i < raw; ++i) vertices.Add(ReadVector(node[(48 + i * 12)..]));
            var cursor = 48 + raw * 12;
            for (var i = 0; i < compressed; ++i)
            {
                var bytes = node[(cursor + i * 6)..];
                vertices.Add(minimum + scale * new Vector3(BinaryPrimitives.ReadUInt16LittleEndian(bytes),
                    BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..])));
            }
            if (vertices.Skip(first).Any(v => !Finite(v))) throw new InvalidDataException("碰撞模型包含无效坐标");
            cursor += compressed * 6;
            for (var i = 0; i < primitiveCount; ++i)
            {
                var bytes = node[(cursor + i * 12)..];
                if (bytes[0] >= raw + compressed || bytes[1] >= raw + compressed || bytes[2] >= raw + compressed)
                    throw new InvalidDataException("碰撞模型三角面索引越界");
                triangles.Add(new(first + bytes[0], first + bytes[1], first + bytes[2], BinaryPrimitives.ReadUInt64LittleEndian(bytes[4..])));
            }
            foreach (var field in (ReadOnlySpan<int>)[8, 12])
            {
                var relative = BinaryPrimitives.ReadInt32LittleEndian(node[field..]);
                if (relative != 0) pending.Push(checked(offset + relative));
            }
        }
        return new(vertices.ToArray(), triangles.ToArray());
    }

    private static Vector3 ReadVector(ReadOnlySpan<byte> data) => new(
        BinaryPrimitives.ReadSingleLittleEndian(data), BinaryPrimitives.ReadSingleLittleEndian(data[4..]), BinaryPrimitives.ReadSingleLittleEndian(data[8..]));
    internal static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
