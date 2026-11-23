using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, (bool West, bool East)> tsukuyomiMoonStates = [];
    private readonly Dictionary<(ulong Actor, uint Status), (bool Present, ushort Extra, DateTime? Expires)> tsukuyomiMoonRaw = [];
    private readonly Dictionary<(ulong Actor, uint Status), (bool Present, ushort Extra, DateTime? Expires)> tsukuyomiMoonPending = [];

    private void TsukuyomiStatus(ulong target, uint status, bool present, ushort extra, DateTime? expires = null, bool snapshot = false)
    {
        if (!IsTsukuyomi || Active == null || status is not (1538 or 1539) || Arena == null) return;
        if (!snapshot)
        {
            var beforeStatus = tsukuyomiMoonPending.GetValueOrDefault((target, status), tsukuyomiMoonRaw.GetValueOrDefault((target, status)));
            if (present && beforeStatus.Present && beforeStatus.Extra == extra
                && (expires == null || beforeStatus.Expires is { } previousEnd && (expires.Value - previousEnd).TotalSeconds <= 1))
                return;
            tsukuyomiMoonPending[(target, status)] = (present, extra, expires);
        }
        if (!present)
        {
            tsukuyomiMoonStates.Remove(target);
            return;
        }
        if (extra < 3 || _actors.GetValueOrDefault(target) is not { } actor) return;
        var before = tsukuyomiMoonStates.GetValueOrDefault(target);
        // Retain both latches: east has precedence until either moon status actually leaves.
        tsukuyomiMoonStates[target] = (before.West || actor.Position.X < Arena.Position.X,
            before.East || actor.Position.X > Arena.Position.X);
    }

    private void TsukuyomiObserveMoon()
    {
        foreach (var actor in _actors.Values)
        {
            foreach (var status in new[] { 1538u, 1539u })
            {
                var key = (actor.Id, status);
                var present = actor.StatusExpirations?.ContainsKey(status) == true
                    || actor.StatusParams?.ContainsKey(status) == true || actor.Statuses?.Contains(status) == true;
                var extra = actor.StatusParams?.GetValueOrDefault(status) ?? 0;
                DateTime? expires = actor.StatusExpirations is { } expirations && expirations.TryGetValue(status, out var end) ? end : null;
                var current = (present, extra, expires);
                if (tsukuyomiMoonPending.TryGetValue(key, out var pending))
                {
                    if (pending.Present != present || present && pending.Extra != extra) continue;
                    tsukuyomiMoonPending.Remove(key);
                    tsukuyomiMoonRaw[key] = current;
                    if (present && pending.Expires is { } expected && expires is { } actual && (actual - expected).TotalSeconds > 1)
                        TsukuyomiStatus(actor.Id, status, true, extra, expires, snapshot: true);
                    continue;
                }
                var before = tsukuyomiMoonRaw.GetValueOrDefault(key);
                tsukuyomiMoonRaw[key] = current;
                if (!present && !before.Present || present && before.Present && extra == before.Extra
                    && (expires == null || before.Expires is { } previousEnd && (expires.Value - previousEnd).TotalSeconds <= 1)) continue;
                TsukuyomiStatus(actor.Id, status, present, extra, expires, snapshot: true);
            }
        }
    }

    private void TsukuyomiRemoveMoon(ulong target)
    {
        tsukuyomiMoonStates.Remove(target);
        foreach (var status in new[] { 1538u, 1539u })
        {
            tsukuyomiMoonRaw.Remove((target, status));
            tsukuyomiMoonPending.Remove((target, status));
        }
    }

    private IEnumerable<DungeonAmbientAoe> TsukuyomiMoonAoes()
    {
        if (Arena == null || !tsukuyomiMoonStates.TryGetValue(PlayerId, out var state)) yield break;
        var present = new[] { 1538u, 1539u }.Any(status =>
            tsukuyomiMoonPending.TryGetValue((PlayerId, status), out var pending)
                ? pending.Present : tsukuyomiMoonRaw.GetValueOrDefault((PlayerId, status)).Present);
        if (!present || !state.East && !state.West) yield break;
        var offset = state.East ? 10.25f : -10.25f;
        yield return new("tsukuyomi/moon/avoid", "月相三层换边", new RectangleTemplate(40, 20.5f),
            Arena.Position + new Vector3(offset, 0, 0), MathF.PI / 2, Display: false);
    }
}
