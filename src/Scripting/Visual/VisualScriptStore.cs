using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MasterPlayer.Scripting.Visual;

public static class VisualScriptStore
{
    public const string FileExtension = ".mechanic.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static VisualScriptDefinition Parse(string json)
    {
        var definition = JsonSerializer.Deserialize<VisualScriptDefinition>(json, Options)
            ?? throw new InvalidDataException("脚本文件不能为空。");
        var errors = definition.Validate();
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        return definition;
    }

    public static string Serialize(VisualScriptDefinition definition) => JsonSerializer.Serialize(definition, Options);

    public static VisualScriptDefinition Clone(VisualScriptDefinition definition)
        => JsonSerializer.Deserialize<VisualScriptDefinition>(Serialize(definition), Options)!;

    public static VisualMechanicRule CloneRule(VisualMechanicRule rule)
    {
        var copy = JsonSerializer.Deserialize<VisualMechanicRule>(JsonSerializer.Serialize(rule, Options), Options)!;
        copy.Id = Guid.NewGuid().ToString("D");
        return copy;
    }

    public static string Save(string path, VisualScriptDefinition definition, string? expectedContent)
    {
        var json = Serialize(definition);
        Parse(json);
        CheckUnchanged(path, expectedContent);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            CheckUnchanged(path, expectedContent);
            if (expectedContent == null)
                File.Move(temporary, path, overwrite: false);
            else
                File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return json;
    }

    public static void Delete(string path, string expectedContent)
    {
        CheckUnchanged(path, expectedContent);
        File.Delete(path);
    }

    private static void CheckUnchanged(string path, string? expectedContent)
    {
        if (expectedContent == null ? File.Exists(path) : !File.Exists(path) || File.ReadAllText(path) != expectedContent)
            throw new IOException("文件已在外部修改或删除。请先重新读取文件，再保存或删除。");
    }
}
