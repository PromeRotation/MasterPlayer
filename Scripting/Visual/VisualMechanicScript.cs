using PromeRotation.LogSystem;
using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Scripting.Visual;

internal sealed class VisualMechanicScript : MasterPlayerScript
{
    private readonly VisualScriptDefinition _definition;
    private readonly List<PendingAoe> _pending = [];
    private readonly Dictionary<ulong, long> _casts = [];
    private long _sequence;
    private bool _active;

    internal VisualMechanicScript(VisualScriptDefinition definition)
    {
        _definition = VisualScriptStore.Clone(definition);
    }

    protected override void OnStart()
    {
        _active = true;
        OnStartCast(HandleCast);
        OnCancelCast(e => Cancel(e.SourceId, e.ActionId));
        OnInterruptCast(e => Cancel(e.SourceId, e.ActionId));
        OnBattleEnded(Clear);
    }

    internal bool SuppressesGenericDodge(LogSystemCastStartEvent e)
    {
        if (!_active || !_definition.Rules.Any(r => r.Enabled && r.SuppressGeneric && r.ActionId == e.ActionId))
            return false;
        var cast = VisualCast.Capture(e);
        return _definition.Rules.Any(r => r.SuppressGeneric && VisualAoeBinding.Matches(r, cast)
            && TryTiming(e, r, DateTime.UtcNow, out _, out _, out _)
            && VisualAoeBinding.TryCreate(r, cast, out _, out _));
    }

    private void HandleCast(LogSystemCastStartEvent e)
    {
        if (!_active) return;
        var sequence = ++_sequence;
        _casts[e.SourceId] = sequence;
        if (!_definition.Rules.Any(r => r.Enabled && r.ActionId == e.ActionId)) return;
        var cast = VisualCast.Capture(e);
        foreach (var rule in _definition.Rules)
        {
            if (!VisualAoeBinding.Matches(rule, cast)) continue;
            if (!TryTiming(e, rule, DateTime.UtcNow, out var delay, out var duration, out var expiresUtc)) continue;
            if (!VisualAoeBinding.TryCreate(rule, cast, out var binding, out var error))
            {
                LogError($"{rule.Name}（读条 {e.ActionId}）：{error}");
                continue;
            }
            using var group = SafeZone.BeginDrawingGroup(rule.Name);
            var handle = SafeZone.AddDelayedHit(rule.CreateTemplate(), binding, delay, duration);
            if (handle.IsValid)
                _pending.Add(new PendingAoe(e.SourceId, e.ActionId, sequence, handle, expiresUtc));
        }
    }

    internal static bool TryTiming(LogSystemCastStartEvent cast, VisualMechanicRule rule, DateTime now,
        out float delay, out float duration, out DateTime expiresUtc)
    {
        delay = duration = 0f;
        expiresUtc = now;
        if (!float.IsFinite(cast.CastTime) || cast.CastTime < 0f) return false;
        var elapsed = Math.Max(0d, (now - cast.Timestamp.ToUniversalTime()).TotalSeconds);
        var untilHit = (double)cast.CastTime + rule.DelaySeconds - elapsed;
        var untilEnd = untilHit + rule.HitDurationSeconds;
        if (untilEnd <= 0d || untilEnd >= (DateTime.MaxValue - now).TotalSeconds) return false;
        delay = (float)Math.Max(0d, untilHit);
        duration = (float)(untilEnd - delay);
        expiresUtc = now.AddSeconds(untilEnd);
        return duration > 0f;
    }

    private void Cancel(ulong sourceId, uint actionId)
    {
        if (!_active || !_casts.TryGetValue(sourceId, out var sequence)) return;
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var aoe = _pending[i];
            if (aoe.SourceId != sourceId || aoe.ActionId != actionId || aoe.Sequence != sequence) continue;
            SafeZone.Remove(aoe.Handle);
            _pending.RemoveAt(i);
        }
    }

    protected override void OnUpdate(float deltaTime)
    {
        var now = DateTime.UtcNow;
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            if (now <= _pending[i].ExpiresUtc) continue;
            SafeZone.Remove(_pending[i].Handle);
            _pending.RemoveAt(i);
        }
    }

    protected override void OnStop()
    {
        _active = false;
        Clear();
    }

    private void Clear()
    {
        foreach (var aoe in _pending) SafeZone.Remove(aoe.Handle);
        _pending.Clear();
        _casts.Clear();
    }

    private sealed record PendingAoe(ulong SourceId, uint ActionId, long Sequence, SafeZoneAreaHandle Handle, DateTime ExpiresUtc);
}
