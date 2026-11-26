using MasterPlayer.Settings;
using System.Numerics;
using PromeRotation.Spatial.Drawing;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Radar;

internal sealed class AoeVisibilityRegistry(Dictionary<string, AoeVisibility> rules, Func<DateTime>? clock = null)
{
    private DateTime Now => clock?.Invoke() ?? DateTime.UtcNow;
    private const string OtherKey = "other";
    private readonly Dictionary<SafeZoneAreaHandle, (string Key, bool DrawAsBoundary)> _areaSources = new();
    private readonly HashSet<SafeZoneAreaHandle> _navigationOnly = [];
    private readonly Dictionary<string, AoePreview> _previews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AoeLinePreview> _lines = new(StringComparer.Ordinal);
    private DateTime _nextPruneUtc;

    public void Track(SafeZoneAreaHandle handle, string key, string name, bool drawAsBoundary = false)
    {
        if (!handle.IsValid)
            return;
        _areaSources[handle] = (key, drawAsBoundary);
        GetRule(key, name);
    }

    public AoeVisibility GetVisibility(SafeZoneAreaHandle handle)
        => _areaSources.TryGetValue(handle, out var source)
            ? rules[source.Key]
            : GetRule(OtherKey, "其他／未标记来源的 AOE");

    public bool DrawsAsBoundary(SafeZoneAreaHandle handle) => _areaSources.GetValueOrDefault(handle).DrawAsBoundary;

    public bool IsNavigationOnly(SafeZoneAreaHandle handle) => _navigationOnly.Contains(handle);
    public void SetNavigationOnly(SafeZoneAreaHandle handle, bool value)
    { if (value) _navigationOnly.Add(handle); else _navigationOnly.Remove(handle); }
    public void Remove(SafeZoneAreaHandle handle) { _areaSources.Remove(handle); _navigationOnly.Remove(handle); }

    public void AddPreview(string id, IGeometryShape geometry, bool safe, float durationMs, string key, string name, bool outlineOnly = false, float progress = 1f, bool unclipped = false, Vector4? color = null)
        => _previews[id] = new AoePreview(id, geometry, safe, GetRule(key, name),
            durationMs > 0f ? Now.AddMilliseconds(durationMs) : DateTime.MaxValue, outlineOnly, progress, unclipped, color);

    public void AddLine(string id, Vector3 start, Vector3 end, bool safe, string key, string name)
        => _lines[id] = new(id, start, end, safe, GetRule(key, name));
    public IEnumerable<AoeLinePreview> GetLines()
    {
        var lines = _lines.Values.ToArray();
        var ordered = lines.Where(p => p.OrderOwner != null).GroupBy(p => p.OrderOwner!)
            .ToDictionary(g => g.Key, g => new Queue<AoeLinePreview>(g.OrderBy(p => p.Order)));
        foreach (var line in lines)
            yield return line.OrderOwner is { } owner ? ordered[owner].Dequeue() : line;
    }

    public void SetPreviewOrder(string owner, IEnumerable<string> ids)
    {
        var order = 0;
        foreach (var id in ids)
        {
            if (_previews.TryGetValue(id, out var preview))
                _previews[id] = preview with { OrderOwner = owner, Order = order };
            if (_lines.TryGetValue(id, out var line))
                _lines[id] = line with { OrderOwner = owner, Order = order };
            ++order;
        }
    }

    public IEnumerable<AoePreview> GetPreviews(DateTime now)
    {
        var previews = _previews.Values.Where(p => now < p.ExpiresUtc).ToArray();
        // Explicit order only permutes that owner's occupied output positions.
        // Other consumers retain their existing enumeration and layering.
        var ordered = previews.Where(p => p.OrderOwner != null).GroupBy(p => p.OrderOwner!)
            .ToDictionary(g => g.Key, g => new Queue<AoePreview>(g.OrderBy(p => p.Order)));
        foreach (var preview in previews)
            yield return preview.OrderOwner is { } owner ? ordered[owner].Dequeue() : preview;
    }

    public bool RemovePreview(string id) => _previews.Remove(id) | _lines.Remove(id);

    public AoeVisibility VisibilityForKey(string key, string name) => GetRule(key, name);

    public AoeVisibility? FindRule(string key) => rules.GetValueOrDefault(key);

    public void RemovePreviews(string idPrefix)
    {
        foreach (var id in _previews.Keys.Where(id => id.StartsWith(idPrefix, StringComparison.Ordinal)).ToArray())
            _previews.Remove(id);
        foreach (var id in _lines.Keys.Where(id => id.StartsWith(idPrefix, StringComparison.Ordinal)).ToArray())
            _lines.Remove(id);
    }

    public void Update(SafeZoneManager manager)
    {
        var now = Now;
        if (now < _nextPruneUtc && _nextPruneUtc - now <= TimeSpan.FromSeconds(0.5))
            return;
        _nextPruneUtc = now.AddSeconds(0.5);
        foreach (var handle in _areaSources.Keys.Where(h => !manager.ContainsArea(h)).ToArray())
            Remove(handle);
        _navigationOnly.RemoveWhere(h => !manager.ContainsArea(h));
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

internal sealed record AoePreview(string Id, IGeometryShape Geometry, bool Safe, AoeVisibility Visibility, DateTime ExpiresUtc, bool OutlineOnly = false, float Progress = 1f, bool Unclipped = false, Vector4? Color = null)
{
    internal string? OrderOwner { get; init; }
    internal int Order { get; init; }

    public DrawStyle Style(AoeViewSettings settings)
    {
        var style = Safe ? settings.GetSafeStyle() : settings.GetDangerStyle(Progress);
        if (Color is { } color)
            style = new DrawStyle(color, color);
        return OutlineOnly ? style with { FillColor = Vector4.Zero } : style;
    }
}
internal sealed record AoeLinePreview(string Id, Vector3 Start, Vector3 End, bool Safe, AoeVisibility Visibility)
{
    internal string? OrderOwner { get; init; }
    internal int Order { get; init; }
}
