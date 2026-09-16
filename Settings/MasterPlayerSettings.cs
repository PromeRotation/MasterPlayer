using System.Text.Json;
using ECommons.DalamudServices;

namespace MasterPlayer.Settings;

public sealed class MasterPlayerSettings
{
    private static readonly string ConfigFilePath = Path.Combine(
        Svc.PluginInterface.ConfigDirectory.FullName,
        "MasterPlayer.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };

    public static MasterPlayerSettings Instance { get; } = Load();

    public bool EnableRadar = true;
    public bool EnableAutoDodge = false;
    public bool EnableMechanics = true;
    public bool DebugMode = false;

    public bool EnableRadarOverlay = false;
    public int RadarCenterMode = 0;
    public bool RadarRotateWithCamera = true;
    public float RadarScale = 1.0f;
    public bool RadarOverlayLock = false;
    public bool RadarOverlayTransparent = true;

    public AoeViewSettings WorldAoe = new();
    public AoeViewSettings RadarAoe = AoeViewSettings.CreateRadarDefaults();
    public Dictionary<string, AoeVisibility> AoeVisibility = new();

    public bool EnableFollow = false;
    public int FollowSlot = 0;
    public bool FollowTargetWhenNoSlot = true;
    public float FollowDistance = 1.5f;
    public bool FollowDuringCombat = true;
    public bool FollowOutOfCombat = false;

    public float DodgeDangerMargin = 0.5f;

    public bool EnableScripts = true;
    public Dictionary<string, bool> ScriptEnabled = new();
    public Dictionary<string, System.Text.Json.Nodes.JsonObject> ScriptSettings = new();
    public Dictionary<string, HashSet<string>> MechanismEnabled = new();

    private static MasterPlayerSettings Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var settings = JsonSerializer.Deserialize<MasterPlayerSettings>(json, JsonOptions) ?? new MasterPlayerSettings();
                settings.WorldAoe ??= new AoeViewSettings();
                settings.RadarAoe ??= AoeViewSettings.CreateRadarDefaults();
                settings.AoeVisibility ??= new Dictionary<string, AoeVisibility>();
                return settings;
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MasterPlayer] 加载配置失败，使用默认配置");
        }

        return new MasterPlayerSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MasterPlayer] 保存配置失败");
        }
    }
}
