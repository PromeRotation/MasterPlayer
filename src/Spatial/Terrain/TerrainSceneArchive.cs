using System.IO.Compression;
using System.Numerics;
using System.Text.Json;

namespace MasterPlayer.Spatial.Terrain;

// Geometry is detached and decoded, including procedural meshes; no native addresses or SqPack are needed to rebuild.
internal static class TerrainSceneArchive
{
    private const int FORMAT_VERSION = 1;
    private const int MAX_ELEMENTS = 10_000_000;
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true, WriteIndented = true };

    internal sealed record Data(TerrainSceneCapture Capture, uint Territory, byte Floor, byte Layout,
        LocalTerrainBoundary PublishedBoundary, TerrainObstruction[] Obstructions);

    private sealed record Manifest(int FormatVersion, int BuilderVersion, Guid BuildMvid, DateTime CapturedAtUtc,
        uint Territory, byte Floor, byte Layout, Vector3 Origin, float Radius,
        Parameters Parameters,
        TerrainCaptureDiagnostics? CaptureDiagnostics, TerrainBuildDiagnostics BuildDiagnostics,
        TerrainObstruction[] Obstructions);

    private sealed record Parameters(float CellSize, float CellHeight, int BorderCells, int HeightVoxels,
        int ClimbVoxels, int ErosionVoxels, float MinNormalY)
    {
        internal static Parameters Current => new(SceneTerrainBuilder.CELL_SIZE, SceneTerrainBuilder.CELL_HEIGHT,
            SceneTerrainBuilder.BORDER, SceneTerrainBuilder.HEIGHT, SceneTerrainBuilder.CLIMB,
            SceneTerrainBuilder.EROSION, SceneTerrainBuilder.MIN_NORMAL_Y);
    }

    internal static void Write(string path, Data data)
    {
        // CreateNew keeps existing evidence intact, including when the caller supplies a filename.
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        var capture = data.Capture;
        var manifest = new Manifest(FORMAT_VERSION, SceneTerrainBuilder.VERSION, typeof(TerrainSceneArchive).Module.ModuleVersionId,
            capture.CapturedAtUtc, data.Territory, data.Floor, data.Layout, capture.Origin, capture.Radius,
            Parameters.Current,
            capture.CaptureDiagnostics, capture.Result.Diagnostics, data.Obstructions);
        using (var stream = archive.CreateEntry("manifest.json").Open())
            JsonSerializer.Serialize(stream, manifest, JsonOptions);
        using (var writer = Writer(archive, "geometry.bin"))
        {
            var assets = capture.Scene.Instances.Select(i => i.Asset).Distinct().ToArray();
            var indices = assets.Select((asset, index) => (asset, index)).ToDictionary(p => p.asset, p => p.index);
            writer.Write(assets.Length);
            foreach (var asset in assets)
            {
                writer.Write(asset.Key);
                var mesh = asset.Mesh.Value;
                writer.Write(mesh.Vertices.Length);
                foreach (var vertex in mesh.Vertices) WriteVector(writer, vertex);
                writer.Write(mesh.Triangles.Length);
                foreach (var triangle in mesh.Triangles)
                {
                    writer.Write(triangle.A); writer.Write(triangle.B); writer.Write(triangle.C); writer.Write(triangle.Material);
                }
            }
            writer.Write(capture.Scene.Instances.Length);
            foreach (var instance in capture.Scene.Instances)
            {
                writer.Write(instance.Id); writer.Write(indices[instance.Asset]);
                WriteMatrix(writer, instance.World);
                writer.Write(instance.Material); writer.Write(instance.MaterialMask);
            }
        }
        WriteBoundary(archive, "base-map.bin", capture.Result.Boundary);
        WriteBoundary(archive, "published-map.bin", data.PublishedBoundary);
    }

    internal static Data Read(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        Manifest manifest;
        using (var stream = Entry(archive, "manifest.json", 1_048_576).Open())
            manifest = JsonSerializer.Deserialize<Manifest>(stream, JsonOptions) ?? throw new InvalidDataException("地形清单为空");
        if (manifest.FormatVersion != FORMAT_VERSION || manifest.BuilderVersion != SceneTerrainBuilder.VERSION
            || manifest.Parameters != Parameters.Current)
            throw new InvalidDataException("不支持的地形归档或构建器版本");
        if (!TerrainMesh.Finite(manifest.Origin) || !float.IsFinite(manifest.Radius) || manifest.Radius is < 1 or > 30
            || manifest.BuildDiagnostics == null || manifest.Obstructions == null
            || manifest.Obstructions.Any(o => !TerrainMesh.Finite(o.Position) || !TerrainMesh.Finite(o.From)
                || !TerrainMesh.Finite(o.To) || o.Kind is not (TerrainCellKind.Wall or TerrainCellKind.Drop)))
            throw new InvalidDataException("地形清单包含无效数据");
        TerrainSceneSnapshot scene;
        using (var reader = new BinaryReader(Entry(archive, "geometry.bin", 512L * 1024 * 1024).Open()))
        {
            var assets = new TerrainMeshAsset[Count(reader, 100_000)];
            for (var i = 0; i < assets.Length; ++i)
            {
                var key = reader.ReadString();
                var vertices = new Vector3[Count(reader, MAX_ELEMENTS)];
                for (var v = 0; v < vertices.Length; ++v) vertices[v] = ReadVector(reader);
                var triangles = new TerrainMesh.Triangle[Count(reader, MAX_ELEMENTS)];
                for (var t = 0; t < triangles.Length; ++t)
                {
                    var a = reader.ReadInt32(); var b = reader.ReadInt32(); var c = reader.ReadInt32();
                    var material = reader.ReadUInt64();
                    if ((uint)a >= vertices.Length || (uint)b >= vertices.Length || (uint)c >= vertices.Length)
                        throw new InvalidDataException("地形三角面索引越界");
                    triangles[t] = new(a, b, c, material);
                }
                assets[i] = new(key, new TerrainMesh(vertices, triangles));
            }
            var instances = new TerrainSceneSnapshot.Instance[Count(reader, 100_000)];
            for (var i = 0; i < instances.Length; ++i)
            {
                var id = reader.ReadUInt64(); var asset = reader.ReadInt32();
                if ((uint)asset >= assets.Length) throw new InvalidDataException("地形模型索引越界");
                var matrix = ReadMatrix(reader);
                instances[i] = new(id, assets[asset], matrix, reader.ReadUInt64(), reader.ReadUInt64());
            }
            if (reader.BaseStream.ReadByte() != -1) throw new InvalidDataException("地形几何包含多余数据");
            scene = new(instances);
        }
        var boundary = ReadBoundary(archive, "base-map.bin");
        var published = ReadBoundary(archive, "published-map.bin");
        if (boundary.Side != published.Side || boundary.CellSize != published.CellSize || boundary.Position != published.Position)
            throw new InvalidDataException("地形发布图与基础图不匹配");
        var result = new SceneTerrainBuilder.Result(boundary, manifest.BuildDiagnostics.RasterizedTriangles,
            manifest.BuildDiagnostics.TotalMilliseconds, manifest.BuildDiagnostics);
        var capture = new TerrainSceneCapture(scene, manifest.Origin, manifest.Radius, manifest.CapturedAtUtc, manifest.CaptureDiagnostics, result);
        return new(capture, manifest.Territory, manifest.Floor, manifest.Layout, published, manifest.Obstructions);
    }

    internal static bool SameBoundary(LocalTerrainBoundary expected, LocalTerrainBoundary actual)
    {
        if (expected.Side != actual.Side || expected.CellSize != actual.CellSize || expected.Position != actual.Position) return false;
        for (var z = 0; z < expected.Side; ++z)
            for (var x = 0; x < expected.Side; ++x)
                if (expected.Cell(x, z) != actual.Cell(x, z) || !expected.Height(x, z).Equals(actual.Height(x, z))) return false;
        return true;
    }

    private static ZipArchiveEntry Entry(ZipArchive archive, string name, long maximum)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"地形归档缺少 {name}");
        if (entry.Length > maximum) throw new InvalidDataException($"地形归档条目过大：{name}");
        return entry;
    }

    private static BinaryWriter Writer(ZipArchive archive, string name)
        => new(archive.CreateEntry(name, CompressionLevel.Fastest).Open());

    private static int Count(BinaryReader reader, int maximum)
    {
        var value = reader.ReadInt32();
        if (value < 0 || value > maximum) throw new InvalidDataException("地形数据数量超出范围");
        return value;
    }

    private static void WriteBoundary(ZipArchive archive, string name, LocalTerrainBoundary boundary)
    {
        using var writer = Writer(archive, name);
        WriteVector(writer, boundary.Position); writer.Write(boundary.Side); writer.Write(boundary.CellSize);
        for (var z = 0; z < boundary.Side; ++z)
            for (var x = 0; x < boundary.Side; ++x)
            {
                writer.Write((byte)boundary.Cell(x, z)); writer.Write(boundary.Height(x, z));
            }
    }

    private static LocalTerrainBoundary ReadBoundary(ZipArchive archive, string name)
    {
        using var reader = new BinaryReader(Entry(archive, name, 8 * 1024 * 1024).Open());
        var center = ReadVector(reader); var side = Count(reader, 1024); var size = reader.ReadSingle();
        if (side == 0 || !float.IsFinite(size) || size <= 0) throw new InvalidDataException("地形网格尺寸无效");
        var cells = new TerrainCellKind[checked(side * side)];
        var heights = new float[cells.Length];
        for (var i = 0; i < cells.Length; ++i)
        {
            cells[i] = (TerrainCellKind)reader.ReadByte(); heights[i] = reader.ReadSingle();
            if (cells[i] > TerrainCellKind.Drop || (cells[i] == TerrainCellKind.Walkable && !float.IsFinite(heights[i])))
                throw new InvalidDataException("地形单元格无效");
        }
        if (reader.BaseStream.ReadByte() != -1) throw new InvalidDataException("地形网格包含多余数据");
        return new(center, side, cells, heights, size);
    }

    private static void WriteVector(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z);
    }

    private static Vector3 ReadVector(BinaryReader reader)
    {
        var value = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        if (!TerrainMesh.Finite(value)) throw new InvalidDataException("地形坐标无效");
        return value;
    }

    private static void WriteMatrix(BinaryWriter writer, Matrix4x4 m)
    {
        foreach (var value in (ReadOnlySpan<float>)[m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44]) writer.Write(value);
    }

    private static Matrix4x4 ReadMatrix(BinaryReader reader)
    {
        var values = new float[16];
        for (var i = 0; i < values.Length; ++i)
        {
            values[i] = reader.ReadSingle();
            if (!float.IsFinite(values[i])) throw new InvalidDataException("地形变换无效");
        }
        if (values[3] != 0 || values[7] != 0 || values[11] != 0 || values[15] != 1)
            throw new InvalidDataException("地形变换不是仿射矩阵");
        return new(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7],
            values[8], values[9], values[10], values[11], values[12], values[13], values[14], values[15]);
    }
}
