using MasterPlayer.Settings;
using System.Numerics;
using PromeRotation.Spatial.Drawing;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Radar;

internal sealed class AoeVisibilityRegistry(Dictionary<string, AoeVisibility> rules)
{
    private const string OtherKey = "other";
    private readonly Dictionary<SafeZoneAreaHandle, string> _areaSources = new();
    private readonly HashSet<SafeZoneAreaHandle> _navigationOnly = [];
    private readonly Dictionary<string, AoePreview> _previews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AoeLinePreview> _lines = new(StringComparer.Ordinal);
    private DateTime _nextPruneUtc;

    public void Track(SafeZoneAreaHandle handle, string key, string name)
    {
        if (!handle.IsValid)
            return;
        _areaSources[handle] = key;
        GetRule(key, name);
    }

    public AoeVisibility GetVisibility(SafeZoneAreaHandle handle)
        => _areaSources.TryGetValue(handle, out var key)
            ? rules[key]
            : GetRule(OtherKey, "其他／未标记来源的 AOE");

    public bool IsNavigationOnly(SafeZoneAreaHandle handle) => _navigationOnly.Contains(handle);
    public void SetNavigationOnly(SafeZoneAreaHandle handle, bool value)
    { if (value) _navigationOnly.Add(handle); else _navigationOnly.Remove(handle); }
    public void Remove(SafeZoneAreaHandle handle) { _areaSources.Remove(handle); _navigationOnly.Remove(handle); }

    public void AddPreview(string id, IGeometryShape geometry, bool safe, float durationMs, string key, string name, bool outlineOnly = false, float progress = 1f)
        => _previews[id] = new AoePreview(id, geometry, safe, GetRule(key, name),
            durationMs > 0f ? DateTime.UtcNow.AddMilliseconds(durationMs) : DateTime.MaxValue, outlineOnly, progress);

    public void AddLine(string id, Vector3 start, Vector3 end, bool safe, string key, string name)
        => _lines[id] = new(id, start, end, safe, GetRule(key, name));
    public IEnumerable<AoeLinePreview> GetLines() => _lines.Values;

    public IEnumerable<AoePreview> GetPreviews(DateTime now)
        => _previews.Values.Where(p => now < p.ExpiresUtc);

    public bool RemovePreview(string id) => _previews.Remove(id) | _lines.Remove(id);

    public void RemovePreviews(string idPrefix)
    {
        foreach (var id in _previews.Keys.Where(id => id.StartsWith(idPrefix, StringComparison.Ordinal)).ToArray())
            _previews.Remove(id);
        foreach (var id in _lines.Keys.Where(id => id.StartsWith(idPrefix, StringComparison.Ordinal)).ToArray())
            _lines.Remove(id);
    }

    public void Update(SafeZoneManager manager)
    {
        var now = DateTime.UtcNow;
        if (now < _nextPruneUtc)
            return;
        _nextPruneUtc = now.AddSeconds(0.5);
        foreach (var handle in _areaSources.Keys.Where(h => !manager.ContainsArea(h)).ToArray())
            Remove(handle);
        foreach (var preview in _previews.Values.Where(p => now >= p.ExpiresUtc).ToArray())
            _previews.Remove(preview.Id);
    }

    private AoeVisibility GetRule(string key, string name)
    {
        if (!rules.TryGetValue(key, out var rule))
            rules[key] = rule = new AoeVisibility();
        rule.Name = name;
        return rule;
    }
}

internal sealed record AoePreview(string Id, IGeometryShape Geometry, bool Safe, AoeVisibility Visibility, DateTime ExpiresUtc, bool OutlineOnly = false, float Progress = 1f)
{
    public DrawStyle Style(AoeViewSettings settings)
    {
        var style = Safe ? settings.GetSafeStyle() : settings.GetDangerStyle(Progress);
        return OutlineOnly ? style with { FillColor = Vector4.Zero } : style;
    }
}
internal sealed record AoeLinePreview(string Id, Vector3 Start, Vector3 End, bool Safe, AoeVisibility Visibility);
