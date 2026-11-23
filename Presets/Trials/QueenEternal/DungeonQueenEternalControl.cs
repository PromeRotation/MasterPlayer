using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<(ulong Actor, uint Status), DateTime> queenStatuses = [];
    private readonly Dictionary<(ulong Actor, uint Status), (bool Present, ushort Param, DateTime Expiry, ulong Source)> queenStatusRaw = [];
    private readonly Dictionary<(ulong Actor, uint Status), (bool Present, ushort Param, DateTime Expiry, ulong Source)> queenStatusPending = [];
    private readonly List<QueenEternalActorReference> queenEyes = [];
    private DateTime queenGazeAt;

    private bool QueenStatusPresent(ulong actor, uint status) => queenStatuses.ContainsKey((actor, status));
    private bool QueenRequireStop => IsQueenEternal && Active != null
        && _actors.GetValueOrDefault(PlayerId) is { Dead: false }
        && queenStatuses.TryGetValue((PlayerId, 4130), out var at) && at != default && at <= _now.AddSeconds(1);

    private void QueenStatus(ulong actor, uint status, bool present, DateTime? expires, ushort param = 0, ulong source = 0)
    {
        if (!IsQueenEternal || Active == null || status is not (3814 or 3815 or 4130)) return;
        var key = (actor, status);
        var previous = queenStatusPending.TryGetValue(key, out var pending) ? pending : queenStatusRaw.GetValueOrDefault(key);
        var next = (Present: present, Param: param, Expiry: expires ?? default, Source: source);
        QueenTransitionStatus(actor, status, previous, next);
        queenStatusPending[key] = next;
    }

    private void QueenTransitionStatus(ulong actor, uint status,
        (bool Present, ushort Param, DateTime Expiry, ulong Source) previous,
        (bool Present, ushort Param, DateTime Expiry, ulong Source) next)
    {
        if (previous.Present && (!next.Present || previous.Source != next.Source)) QueenApplyStatus(actor, status, false, default);
        if (next.Present && (!previous.Present || previous.Param != next.Param || previous.Source != next.Source
            || (next.Expiry - previous.Expiry).TotalSeconds > 1))
            QueenApplyStatus(actor, status, true, next.Expiry);
    }

    private void QueenApplyStatus(ulong actor, uint status, bool present, DateTime expires)
    {
        if (!present)
        {
            queenStatuses.Remove((actor, status));
            if (status == 3815 && queenActors.TryGetValue(actor, out var lost)) queenEyes.Remove(lost);
            return;
        }
        queenStatuses[(actor, status)] = expires;
        if (status != 3815 || !queenActors.TryGetValue(actor, out var reference)) return;
        queenGazeAt = expires;
        queenEyes.Add(reference);
    }

    private void QueenObserveStatuses()
    {
        foreach (var actor in _actors.Values)
            foreach (var status in new[] { 3814u, 3815u, 4130u })
            {
                var key = (actor.Id, status);
                var next = (Present: actor.Statuses?.Contains(status) == true,
                    Param: actor.StatusParams?.GetValueOrDefault(status) ?? 0,
                    Expiry: actor.StatusExpirations?.GetValueOrDefault(status) ?? default,
                    Source: actor.StatusSources?.GetValueOrDefault(status) ?? 0);
                if (queenStatusPending.TryGetValue(key, out var pending))
                {
                    if (next.Present != pending.Present || next.Present && (next.Param != pending.Param || next.Source != pending.Source)) continue;
                    queenStatusPending.Remove(key);
                    queenStatusRaw[key] = next;
                    continue;
                }
                QueenTransitionStatus(actor.Id, status, queenStatusRaw.GetValueOrDefault(key), next);
                queenStatusRaw[key] = next;
            }
    }

    private IEnumerable<DungeonPendingCast> QueenGazes()
    {
        if (!IsQueenEternal || Active == null || queenEyes.Count == 0 || _now.AddSeconds(10) < queenGazeAt) yield break;
        foreach (var eye in queenEyes)
            yield return new(eye.Actor.Id, new(36624, 0, "AuthoritysGaze", null, "背对视线"),
                eye.Actor.Position, 0, _now, queenGazeAt);
    }
}
