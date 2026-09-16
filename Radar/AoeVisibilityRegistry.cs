using MasterPlayer.Settings;
using PromeRotation.Spatial.Geometry;
using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Radar;

internal sealed class AoeVisibilityRegistry(Dictionary<string, AoeVisibility> rules)
{
    private const string OtherKey = "other";
    private readonly Dictionary<SafeZoneAreaHandle, string> _areaSources = new();
    private readonly Dictionary<string, AoePreview> _previews = new(StringComparer.Ordinal);
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

    public void Remove(SafeZoneAreaHandle handle) => _areaSources.Remove(handle);

    public void AddPreview(string id, IGeometryShape geometry, bool safe, float durationMs, string key, string name)
        => _previews[id] = new AoePreview(id, geometry, safe, GetRule(key, name),
            durationMs > 0f ? DateTime.UtcNow.AddMilliseconds(durationMs) : DateTime.MaxValue);

    public IEnumerable<AoePreview> GetPreviews(DateTime now)
        => _previews.Values.Where(p => now < p.ExpiresUtc);

    public bool RemovePreview(string id) => _previews.Remove(id);

    public void RemovePreviews(string idPrefix)
    {
        foreach (var id in _previews.Keys.Where(id => id.StartsWith(idPrefix, StringComparison.Ordinal)).ToArray())
            _previews.Remove(id);
    }

    public void Update(SafeZoneManager manager)
    {
        var now = DateTime.UtcNow;
        if (now < _nextPruneUtc)
            return;
        _nextPruneUtc = now.AddSeconds(0.5);
        foreach (var handle in _areaSources.Keys.Where(h => !manager.ContainsArea(h)).ToArray())
            _areaSources.Remove(handle);
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

internal sealed record AoePreview(string Id, IGeometryShape Geometry, bool Safe, AoeVisibility Visibility, DateTime ExpiresUtc);
