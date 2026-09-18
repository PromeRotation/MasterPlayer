using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PromeRotation.LogSystem;

namespace MasterPlayer.Replay;

/// <summary>录制文件头（首行 JSON）。territoryName 为后加字段：旧文件缺省为空串，新文件对旧读取器向后兼容（未知字段忽略）。</summary>
public sealed record ReplayHeader(
    [property: JsonPropertyName("v")] int Version,
    [property: JsonPropertyName("territory")] ushort Territory,
    [property: JsonPropertyName("startedUtc")] DateTime StartedUtc,
    [property: JsonPropertyName("pluginVersion")] string PluginVersion)
{
    [JsonPropertyName("territoryName")]
    public string TerritoryName { get; init; } = "";
}

/// <summary>一条录制事件：T = 相对录制开始的秒数。</summary>
public sealed record ReplayEventEntry(double T, LogSystemEvent Event);

/// <summary>一份完整录制（内存形态）。</summary>
public sealed record ReplayRecording(ReplayHeader Header, IReadOnlyList<ReplayEventEntry> Events, int SkippedEvents)
{
    public double Duration => Events.Count == 0 ? 0d : Events[^1].T;
}

/// <summary>
/// 录制写入器：JSON Lines，首行 header，之后每行一个事件。
/// 事件 record 的 Raw（运行态对象图）与 Kind（由类型固定派生）不写入。
/// </summary>
public sealed class ReplayRecordingWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private bool _disposed;

    public ReplayRecordingWriter(Stream stream)
    {
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
    }

    public void WriteHeader(ReplayHeader header)
        => _writer.WriteLine(JsonSerializer.Serialize(header, ReplayJson.Options));

    public void WriteEvent(double t, LogSystemEvent ev)
    {
        var line = new ReplayEventLine
        {
            T = Math.Round(t, 4),
            Kind = ev.Kind.ToString(),
            Type = ev.GetType().FullName!,
            Data = JsonSerializer.SerializeToElement(ev, ev.GetType(), ReplayJson.Options),
        };
        _writer.WriteLine(JsonSerializer.Serialize(line, ReplayJson.Options));
    }

    public void Flush() => _writer.Flush();

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _writer.Dispose();
    }
}

/// <summary>录制读取器：解析 JSON Lines，未知/损坏的事件行跳过并计数。</summary>
public static class ReplayRecordingReader
{
    public const string FileExtension = ".mpreplay.jsonl";

    private static readonly Dictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);

    public static ReplayRecording Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Load(stream);
    }

    public static ReplayRecording Load(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var headerLine = reader.ReadLine();
        if (headerLine == null)
            throw new InvalidDataException("空的录制文件。");

        var header = JsonSerializer.Deserialize<ReplayHeader>(headerLine, ReplayJson.Options)
            ?? throw new InvalidDataException("录制文件头解析失败。");

        var events = new List<ReplayEventEntry>();
        var skipped = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            ReplayEventLine? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<ReplayEventLine>(line, ReplayJson.Options);
            }
            catch (JsonException)
            {
                skipped++;
                continue;
            }

            var type = parsed?.Type == null ? null : ResolveType(parsed.Type);
            if (parsed == null || type == null)
            {
                skipped++;
                continue;
            }

            try
            {
                if (parsed.Data.Deserialize(type, ReplayJson.Options) is LogSystemEvent ev)
                    events.Add(new ReplayEventEntry(parsed.T, ev));
                else
                    skipped++;
            }
            catch (JsonException)
            {
                skipped++;
            }
        }

        events.Sort(static (a, b) => a.T.CompareTo(b.T));
        return new ReplayRecording(header, events, skipped);
    }

    private static Type? ResolveType(string fullName)
    {
        lock (TypeCache)
        {
            if (!TypeCache.TryGetValue(fullName, out var type))
            {
                type = typeof(LogSystemEvent).Assembly.GetType(fullName);
                if (type == null || !typeof(LogSystemEvent).IsAssignableFrom(type))
                    type = null;
                TypeCache[fullName] = type;
            }
            return type;
        }
    }
}

/// <summary>事件行 DTO（序列化外形与写入侧保持一致）。</summary>
internal sealed class ReplayEventLine
{
    [JsonPropertyName("t")] public double T { get; set; }
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("data")] public JsonElement Data { get; set; }
}

/// <summary>录制/回放共用的 JSON 配置：剔除 Raw/Kind，支持 Vector3。</summary>
internal static class ReplayJson
{
    internal static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver().WithAddedModifier(static info =>
        {
            if (!typeof(LogSystemEvent).IsAssignableFrom(info.Type))
                return;
            for (var i = info.Properties.Count - 1; i >= 0; i--)
                if (info.Properties[i].Name is nameof(LogSystemEvent.Raw) or nameof(LogSystemEvent.Kind))
                    info.Properties.RemoveAt(i);
        });

        var options = new JsonSerializerOptions { TypeInfoResolver = resolver };
        options.Converters.Add(new Vector3JsonConverter());
        return options;
    }
}

/// <summary>System.Numerics.Vector3 的 JSON 往返（{"X":…,"Y":…,"Z":…}）。</summary>
public sealed class Vector3JsonConverter : JsonConverter<Vector3>
{
    public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Vector3 需要 JSON 对象。");

        float x = 0f, y = 0f, z = 0f;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return new Vector3(x, y, z);
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Vector3 对象内需要属性。");

            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "X": x = reader.GetSingle(); break;
                case "Y": y = reader.GetSingle(); break;
                case "Z": z = reader.GetSingle(); break;
                default: reader.Skip(); break;
            }
        }
        throw new JsonException("Vector3 对象未闭合。");
    }

    public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Z", value.Z);
        writer.WriteEndObject();
    }
}
