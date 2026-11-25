using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.EurekaOrthos;

// Mechanic-owned, start-locked visibility pixels. No runtime/solver objects are
// retained. The output adapter emits row runs without the generic fill rejection.
internal sealed class EurekaLosMap(Vector2 origin, float pixelSize, bool[,] danger)
{
    private (int Left, int Right, int Top, int Bottom)[]? rectangleCache;
    internal Vector2 Origin { get; } = origin;
    internal float PixelSize { get; } = pixelSize;
    internal int Width => danger.GetLength(0);
    internal int Height => danger.GetLength(1);
    internal bool Contains(Vector3 point)
    {
        var offset = new Vector2(point.X - Origin.X, point.Z - Origin.Y) * (1f / PixelSize);
        var x = (int)offset.X;
        var z = (int)offset.Y;
        return x >= 0 && x < Width && z >= 0 && z < Height && danger[x, z];
    }

    internal static EurekaLosMap? Create(uint territory, Vector3 caster, float range)
    {
        var assembly = typeof(EurekaLosMap).Assembly;
        using var list = assembly.GetManifestResourceStream("MasterPlayer.EurekaLOS.maplist.json")
            ?? throw new InvalidDataException("Missing native Eureka LOS map list.");
        using var document = JsonDocument.Parse(list);
        var key = $"{territory}.{territory - 202}";
        if (!document.RootElement.TryGetProperty(key, out var entries)) return null;
        foreach (var entry in entries.EnumerateArray())
        {
            float Read(string name) => entry.GetProperty(name).GetSingle();
            if (caster.X < Read("MinBoundsX") || caster.X > Read("MaxBoundsX")
                || caster.Y < Read("MinBoundsY") || caster.Y > Read("MaxBoundsY")
                || caster.Z < Read("MinBoundsZ") || caster.Z > Read("MaxBoundsZ")) continue;
            using var stream = assembly.GetManifestResourceStream("MasterPlayer.EurekaLOS." + entry.GetProperty("Filename").GetString())
                ?? throw new InvalidDataException("Missing native Eureka LOS bitmap.");
            using var reader = new BinaryReader(stream);
            if (reader.ReadUInt16() != 0x4D42) throw new InvalidDataException("Invalid LOS bitmap magic.");
            stream.Position = 14;
            if (reader.ReadInt32() != 40) throw new InvalidDataException("Unsupported LOS bitmap header.");
            var width = reader.ReadInt32();
            var height = -reader.ReadInt32();
            if (reader.ReadUInt16() != 1 || reader.ReadUInt16() != 1 || reader.ReadInt32() != 0)
                throw new InvalidDataException("Unsupported LOS bitmap encoding.");
            stream.Position = 38;
            var resolution = reader.ReadInt32();
            if (resolution <= 0 || reader.ReadInt32() != resolution || width <= 0 || height <= 0)
                throw new InvalidDataException("Invalid LOS bitmap dimensions.");
            // The source loader reads the two palette entries then packed rows.
            stream.Position = 62;
            var stride = (width + 31) >> 5 << 2;
            var pixels = reader.ReadBytes(height * stride);
            if (pixels.Length != height * stride) throw new InvalidDataException("Truncated LOS bitmap.");
            bool Blocked(int x, int z)
                => x >= 0 && x < width && z >= 0 && z < height
                    && (pixels[z * stride + (x >> 3)] & (0x80 >> (x & 7))) != 0;
            var step = 1024f / resolution;
            var origin = new Vector2(Read("OriginX"), Read("OriginZ"));
            var sourceCell = new Vector2(caster.X - origin.X, caster.Z - origin.Y) * (1f / step);
            var cx = (int)sourceCell.X;
            var cz = (int)sourceCell.Y;
            var pixelRange = (int)(range / step);
            var output = new bool[width, height];
            for (var x = Math.Max(0, cx - pixelRange); x <= Math.Min(width, cx + pixelRange); ++x)
            for (var z = Math.Max(0, cz - pixelRange); z <= Math.Min(height, cz + pixelRange); ++z)
            {
                var direction = new Vector2(cx - x, cz - z);
                var distance = direction.Length();
                if (distance >= pixelRange) continue;
                direction *= 1f / distance;
                var px = (float)x;
                var pz = (float)z;
                var blocked = false;
                for (var i = 0; i < (int)distance; ++i)
                {
                    if (Blocked((int)px, (int)pz)) { blocked = true; break; }
                    px += direction.X;
                    pz += direction.Y;
                }
                if (!blocked && x < width && z < height) output[x, z] = true;
            }
            return new(origin, step, output);
        }
        return null;
    }

    internal IEnumerable<PalaceArea> Areas(string prefix, DateTime now, float y)
    {
        var rectangles = rectangleCache ??= BuildRectangles();
        for (var i = 0; i < rectangles.Length; ++i)
        {
            var r = rectangles[i];
            yield return new($"{prefix}/{i}", "遮挡视线危险像素",
                new RectangleTemplate((r.Right - r.Left) * PixelSize, (r.Bottom - r.Top) * PixelSize),
                new(Origin.X + (r.Left + r.Right) * .5f * PixelSize, y,
                    Origin.Y + (r.Top + r.Bottom) * .5f * PixelSize), 0, now, DateTime.MaxValue);
        }
    }

    private (int Left, int Right, int Top, int Bottom)[] BuildRectangles()
    {
        // Merge equal adjacent row runs, preserving every visible cell and hole.
        var active = new Dictionary<(int Left, int Right), (int Top, int Bottom)>();
        var rectangles = new List<(int Left, int Right, int Top, int Bottom)>();
        for (var z = 0; z <= Height; ++z)
        {
            var runs = new HashSet<(int Left, int Right)>();
            for (var x = 0; z < Height && x < Width; ++x)
            {
                if (!danger[x, z]) continue;
                var left = x;
                while (x + 1 < Width && danger[x + 1, z]) ++x;
                runs.Add((left, x + 1));
            }
            foreach (var key in active.Keys.Where(k => !runs.Contains(k)).ToArray())
            {
                var row = active[key];
                rectangles.Add((key.Left, key.Right, row.Top, row.Bottom));
                active.Remove(key);
            }
            foreach (var run in runs)
                active[run] = active.TryGetValue(run, out var row) ? (row.Top, z + 1) : (z, z + 1);
        }
        return rectangles.ToArray();
    }
}
