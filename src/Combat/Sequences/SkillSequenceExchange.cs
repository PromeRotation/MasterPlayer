using System.Text;
using System.Text.Json;

namespace MasterPlayer.Combat.Sequences;

internal static class SkillSequenceExchange
{
    private const string PREFIX = "XSZSEQ1:";
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static string Export(SkillSequenceProfile profile)
        => PREFIX + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Version = 1, Profile = profile })));

    public static SkillSequenceProfile Import(string text)
    {
        var raw = text.Trim();
        if (raw.StartsWith(PREFIX, StringComparison.OrdinalIgnoreCase))
            raw = Encoding.UTF8.GetString(Convert.FromBase64String(raw[PREFIX.Length..]));
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        var profileProperty = root.EnumerateObject().FirstOrDefault(p => p.Name.Equals("Profile", StringComparison.OrdinalIgnoreCase));
        if (profileProperty.Value.ValueKind != JsonValueKind.Undefined)
        {
            var version = root.EnumerateObject().FirstOrDefault(p => p.Name.Equals("Version", StringComparison.OrdinalIgnoreCase));
            if (version.Value.ValueKind != JsonValueKind.Undefined && version.Value.GetInt32() != 1)
                throw new FormatException("不支持的技能序列版本。");
            root = profileProperty.Value;
        }
        var profile = root.Deserialize<SkillSequenceProfile>(Options) ?? throw new FormatException("预设内容为空。");
        if (profile.JobId == 0 || profile.Entries == null || profile.Entries.Any(e => e == null))
            throw new FormatException("预设缺少职业或有效步骤。");
        profile.PresetId = Guid.NewGuid().ToString("N");
        profile.PresetName = string.IsNullOrWhiteSpace(profile.PresetName) ? "导入预设" : profile.PresetName.Trim();
        profile.CompletionCommand ??= "";
        profile.CompletionCommandDelayMs = Math.Clamp(profile.CompletionCommandDelayMs, 0, 3000);
        return profile;
    }

    public static SkillSequenceProfile Clone(SkillSequenceProfile profile) => Import(Export(profile));
}
