using System.Numerics;
using System.Text.RegularExpressions;
using PromeRotation.Spatial.Drawing;
using PromeRotation.Spatial.Geometry;

namespace MasterPlayer.Scripting;

/// <summary>
/// 绘制门面：包装 PR DrawManager（仅主线程）。
/// 所有元素 id 自动加本脚本前缀（MasterPlayer_Script_{guid}_），
/// 脚本停止时由 Cleanup 按前缀一键清理；战斗结束 / 切换区域时 PR 也会原生清空。
/// </summary>
public sealed class ScriptDrawApi
{
    private readonly string _idPrefix;
    private readonly ScriptSafeZoneApi _safeZone;

    internal ScriptDrawApi(string idPrefix, ScriptSafeZoneApi safeZone)
    {
        _idPrefix = idPrefix;
        _safeZone = safeZone;
    }

    private static DrawManager Manager => PromeRotation.Plugin.Instance.DrawManager;

    private string Id(string id) => _idPrefix + id;

    /// <summary>只供显示的世界坐标 AOE 预览；服从两种视图的配色和分组开关，不进入解算。</summary>
    public void AddAoe(string id, IGeometryShape geometry, bool safe = false, float durationMs = 0f)
    {
        var (key, name) = _safeZone.DrawingSource;
        MasterPlayerPlugin.Instance?.AoeVisibility?.AddPreview(Id(id), geometry, safe, durationMs, key, name);
    }

    /// <summary>静态几何绘制（世界坐标）。</summary>
    public DrawHandle Add(
        string id,
        IGeometryShape geometry,
        DrawStyle? style = null,
        float durationMs = 0f,
        float startDelayMs = 0f)
        => Manager.Add(Id(id), geometry, style, durationMs, startDelayMs);

    /// <summary>绑定绘制（跟随实体 / 朝向 / 延伸）。</summary>
    public DrawHandle AddBound(
        string id,
        IGeometryShape geometry,
        DrawBinding binding,
        DrawStyle? style = null,
        float durationMs = 0f)
        => Manager.AddBound(Id(id), geometry, binding, style, durationMs);

    /// <summary>两点连线。</summary>
    public DrawHandle AddTether(
        string id,
        Vector3 start,
        Vector3 end,
        DrawStyle? style = null,
        float durationMs = 0f)
        => Manager.AddTether(Id(id), start, end, style, durationMs: durationMs);

    /// <summary>实体到坐标连线（如 我 → 安全点）。</summary>
    public DrawHandle AddTether(
        string id,
        uint startEntityId,
        Vector3 end,
        DrawStyle? style = null,
        float durationMs = 0f)
        => Manager.AddTether(Id(id), startEntityId, end, style, durationMs: durationMs);

    /// <summary>实体到实体连线。</summary>
    public DrawHandle AddTether(
        string id,
        uint startEntityId,
        uint endEntityId,
        DrawStyle? style = null,
        float durationMs = 0f)
        => Manager.AddTether(Id(id), startEntityId, endEntityId, style, durationMs: durationMs);

    /// <summary>按 id 移除本脚本的单个元素。</summary>
    public bool Remove(string id)
    {
        var removed = MasterPlayerPlugin.Instance?.AoeVisibility?.RemovePreview(Id(id)) ?? false;
        return Manager.Remove(Id(id)) || removed;
    }

    internal void Cleanup()
    {
        MasterPlayerPlugin.Instance?.AoeVisibility?.RemovePreviews(_idPrefix);
        Manager.RemoveByRegex("^" + Regex.Escape(_idPrefix));
    }
}
