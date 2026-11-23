using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DateTime> lapisEyes = [];
    private readonly HashSet<ulong> lapisEyeUnconfirmed = [];
    private readonly HashSet<ulong> lapisEyeLosses = [];
    private readonly HashSet<ulong> lapisDoom = [];
    private readonly HashSet<ulong> lapisDoomUnconfirmed = [];
    private readonly HashSet<ulong> lapisDoomLosses = [];
    private DateTime lapisGazeAt;

    private void LapisStatus(ulong actor, uint status, bool present, DateTime? expires)
    {
        if (territory != LapisCatalog.TERRITORY || Active?.BossOid != 0x3971) return;
        if (status == 3511)
        {
            if (present && expires is { } activation)
            {
                lapisEyes[actor] = activation;
                lapisGazeAt = activation;
                lapisEyeUnconfirmed.Add(actor);
                lapisEyeLosses.Remove(actor);
            }
            else if (!present)
            {
                lapisEyes.Remove(actor);
                lapisEyeUnconfirmed.Remove(actor);
                lapisEyeLosses.Add(actor);
            }
        }
        if (status != 3364) return;
        if (present)
        {
            lapisDoom.Add(actor); lapisDoomUnconfirmed.Add(actor); lapisDoomLosses.Remove(actor);
        }
        else
        {
            lapisDoomUnconfirmed.Remove(actor); lapisDoomLosses.Add(actor);
        }
    }

    private void SynchronizeLapisStatuses()
    {
        if (Active?.BossOid != 0x3971) return;
        foreach (var actor in _actors.Values)
        {
            if (actor.Statuses?.Contains(3511u) == true)
            {
                if (!lapisEyeLosses.Contains(actor.Id) && actor.StatusExpirations?.GetValueOrDefault(3511u) is { } expires && expires != default)
                {
                    if (!lapisEyes.TryGetValue(actor.Id, out var old) || Math.Abs((old - expires).TotalSeconds) > .01)
                    {
                        lapisEyes[actor.Id] = expires;
                        lapisGazeAt = expires;
                    }
                    lapisEyeUnconfirmed.Remove(actor.Id);
                }
            }
            else
            {
                lapisEyeLosses.Remove(actor.Id);
                if (!lapisEyeUnconfirmed.Contains(actor.Id)) lapisEyes.Remove(actor.Id);
            }
            if (actor.Statuses?.Contains(3364u) == true)
            {
                lapisDoom.Add(actor.Id); lapisDoomUnconfirmed.Remove(actor.Id);
            }
            else if (lapisDoomLosses.Remove(actor.Id) || !lapisDoomUnconfirmed.Contains(actor.Id)) lapisDoom.Remove(actor.Id);
        }
        foreach (var id in lapisEyes.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
        {
            lapisEyes.Remove(id); lapisEyeUnconfirmed.Remove(id);
        }
        lapisDoom.RemoveWhere(id => !_actors.TryGetValue(id, out var a) || a.Dead);
    }

    private IEnumerable<DungeonPendingCast> LapisGazes()
    {
        if (territory != LapisCatalog.TERRITORY || lapisEyes.Count == 0 || _now < lapisGazeAt.AddSeconds(-10)) yield break;
        foreach (var id in lapisEyes.Keys)
            if (_actors.TryGetValue(id, out var eye))
                yield return new(id, new(31385, 0, "GlassyEyed", null, "背对视线"), eye.Position, 0, _now, lapisGazeAt);
    }

    internal DungeonActor? LapisCleanseTarget(uint job)
    {
        if (territory != LapisCatalog.TERRITORY || Active?.BossOid != 0x3971 || job is not (6 or 24 or 28 or 33 or 40 or 23)
            || !_actors.TryGetValue(PlayerId, out var player)) return null;
        return _actors.Values.Where(a => lapisDoom.Contains(a.Id) && !a.Dead && a.Targetable && a.PartySlot >= 0
            && Vector3.DistanceSquared(a.Position, player.Position) <= MathF.Pow(30 + a.HitboxRadius, 2))
            .OrderBy(a => a.Id != PlayerId).ThenBy(a => a.PartySlot).FirstOrDefault();
    }

    internal DungeonActor? LapisStunTarget(uint job)
    {
        if (!IsLapisCarriage || !lapisGriffinStage || LapisCatalog.StunAction(job) == 0
            || !_actors.TryGetValue(PlayerId, out var player)) return null;
        return _casts.Values.Where(c => c.Rule.Action == 32785).Select(c => _actors.GetValueOrDefault(c.Source))
            .OfType<DungeonActor>().FirstOrDefault(a => !a.Dead && a.Targetable
                && Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(player.Position.X, player.Position.Z))
                    <= MathF.Pow(3 + a.HitboxRadius + player.HitboxRadius, 2));
    }
}
