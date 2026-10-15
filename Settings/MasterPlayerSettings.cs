using System.Text.Json;
using ECommons.DalamudServices;

namespace MasterPlayer.Settings;

public enum CombatDistanceMode
{
    Melee,
    Ranged,
}

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
    public bool OptimizeDodgeTiming = true;
    public bool EnableMechanics = true;
    public bool DebugMode = false;

    public bool EnableRadarOverlay = false;
    public int RadarCenterMode = 0;
    public bool RadarRotateWithCamera = true;
    public float RadarScale = 1.0f;
    public bool RadarOverlayLock = false;
    public float RadarBackgroundOpacity = 0.55f;    // 圆盘背景不透明度，0 = 不要圆盘背景
    public bool RadarShowCompass = true;            // 罗盘方位字母
    public bool RadarShowRangeRings = true;         // 距离参考环
    public bool RadarPulseDanger = true;            // 危险区即将判定时描边脉冲
    public bool RadarOnlyWithScript = true;         // 仅在有机制脚本的区域显示
    public bool ShowModuleTimeline = true;          // 悬浮窗下方显示机制模块预测时间轴

    public AoeViewSettings WorldAoe = new();
    public AoeViewSettings RadarAoe = AoeViewSettings.CreateRadarDefaults();
    public Dictionary<string, AoeVisibility> AoeVisibility = new();

    public bool EnableFollow = false;
    public int FollowSlot = 0;
    public bool FollowTargetWhenNoSlot = true;
    public float FollowDistance = 1.5f;
    public CombatDistanceMode CombatDistance = CombatDistanceMode.Melee;
    public float RangedAttackRange = 25f;
    public bool FollowDuringCombat = true;
    public bool FollowOutOfCombat = false;

    public float DodgeDangerMargin = 0.5f;

    public bool EnableScripts = true;
    // Retain the previous editor's saved data; current navigation uses FinalRoute.
    public Mechanics.Blunderville.BlundervilleRouteLibrary BlundervilleRoutes = new();
    public Mechanics.Blunderville.BlundervilleFinalRouteSettings BlundervilleFinalRoute = new();
    public Dictionary<string, bool> ScriptEnabled = new();
    public Dictionary<string, System.Text.Json.Nodes.JsonObject> ScriptSettings = new();
    public Dictionary<string, HashSet<string>> MechanismEnabled = new();

    // 机制模块宿主（MechanicHost）
    public bool EnableMechanicModules = true;
    public int MechanicModuleMinMaturity = 3;  // BossMod.BossModuleInfo.Maturity：0=Dummy 1=WIP 2=Contributed 3=Verified 4=AISupport

    // 录制回放
    public bool AutoRecordOnBattleStart = false;    // 战斗开始自动录制、战斗结束自动停止

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
                settings.BlundervilleRoutes ??= new();
                settings.BlundervilleFinalRoute ??= new();
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
        => TrySave();

    public bool TrySave()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);
            return true;
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MasterPlayer] 保存配置失败");
            return false;
        }
    }
}
