using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntStackState
{
    private HuntPendingCast? cast;
    private ReplayActorState? target;
    private bool attached;
    private bool waitingForObservation;
    private HashSet<ulong> forbidden = [];
    private HuntStackJoinTemplate? join;
    internal bool Active => cast != null && target is { Dead: false };
    internal HuntPendingCast? Cast => cast;
    internal ulong AttachedTargetId => Active && attached ? target!.Id : 0;
    internal IReadOnlySet<ulong> Forbidden => forbidden;

    internal void Start(HuntPendingCast pending, IReadOnlyDictionary<ulong, ReplayActorState> actors, IEnumerable<ulong> frozenForbidden)
    {
        cast = pending;
        target = actors.GetValueOrDefault(pending.TargetId);
        attached = target != null;
        waitingForObservation = pending.PacketPose && target == null;
        forbidden = frozenForbidden.ToHashSet();
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (cast?.Input != previous.Input) return;
        if (previous.TargetId != updated.TargetId)
        {
            target = actors.GetValueOrDefault(updated.TargetId);
            attached = target != null;
            waitingForObservation = updated.PacketPose && target == null;
        }
        cast = updated;
    }

    internal void FirstObservation(HuntPendingCast pending, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (waitingForObservation)
        {
            target = actors.GetValueOrDefault(pending.TargetId);
            attached = target != null;
        }
        waitingForObservation = false;
    }

    internal void Update(IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (!attached || target == null) return;
        if (actors.TryGetValue(target.Id, out var current)) target = current;
        else attached = false;
    }

    internal void Remove(ulong id)
    {
        if (target?.Id == id) attached = false;
    }

    internal HuntPendingCast? Preview(ReplayActorState? player, ReplayActorState[] party, HashSet<ulong> stackTargets)
    {
        if (!Active || cast == null || target == null) return null;
        var inside = player != null && Inside(player.Position);
        var safe = player != null && !forbidden.Contains(player.Id)
            && (attached && target.Id == player.Id || !stackTargets.Contains(player.Id) && (inside ? Count(party) <= 8 : Count(party) < 8));
        return cast with
        {
            Shape = new CircleTemplate(6), Location = Quantize(target.Position),
            SequenceId = $"{cast.Source}/stack/preview", Risky = false, OutlineOnly = true, Safe = safe
        };
    }

    internal IEnumerable<HuntPendingCast> Navigation(ReplayActorState player, ReplayActorState[] party, HashSet<ulong> stackTargets)
    {
        if (!Active || cast == null || target == null || party.Length < 2) yield break;
        if (attached && player.Id == target.Id)
        {
            var centers = party.Where(a => a.Id != player.Id && !forbidden.Contains(a.Id) && !stackTargets.Contains(a.Id))
                .Select(a => a.Position).ToArray();
            if (centers.Length != 0)
                yield return cast with
                {
                    Shape = Join(centers, 3), Location = Vector3.Zero,
                    SequenceId = $"{cast.Source}/stack/gather"
                };
        }
        else
        {
            var ownStack = stackTargets.Contains(player.Id);
            var safe = !forbidden.Contains(player.Id) && !ownStack && (Inside(player.Position) ? Count(party) <= 8 : Count(party) < 8);
            yield return cast with
            {
                Shape = safe ? Join([Quantize(target.Position)], 6) : new CircleTemplate(ownStack ? 12 : 6),
                Location = safe ? Vector3.Zero : Quantize(target.Position),
                SequenceId = $"{cast.Source}/stack/join"
            };
        }
    }

    private HuntStackJoinTemplate Join(Vector3[] centers, float radius)
    {
        if (join == null || join.Radius != radius || !join.Centers.SequenceEqual(centers)) join = new(centers, radius);
        return join;
    }

    internal IEnumerable<string> Hints(ReplayActorState player, ReplayActorState[] party)
    {
        if (!Active || target == null) yield break;
        if (attached && target.Id == player.Id)
            yield return "Stack!";
        else if (forbidden.Contains(player.Id))
        {
            if (Inside(player.Position)) yield return "GTFO from forbidden stacks!";
        }
        else if (Inside(player.Position) || 1 + party.Count(a => a.Id != target.Id && Inside(a.Position)) < 8)
            yield return "Stack!";
    }

    private int Count(ReplayActorState[] party) => party.Count(a => !forbidden.Contains(a.Id) && Inside(a.Position));
    private bool Inside(Vector3 position)
        => target != null && Vector2.DistanceSquared(new(position.X, position.Z), new(Quantize(target.Position).X, Quantize(target.Position).Z)) <= 36;

    private static Vector3 Quantize(Vector3 position)
    {
        const float grid = 2000f / 65535f;
        const float inverse = 1f / grid;
        return new(((int)MathF.Round(position.X * inverse) - .5f) * grid, position.Y,
            ((int)MathF.Round(position.Z * inverse) - .5f) * grid);
    }
}
