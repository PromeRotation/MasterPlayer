using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntTargetState
{
    private HuntPendingCast? cast;
    private ReplayActorState? target;
    private bool attached;
    private bool waitingForObservation;
    internal bool Active => cast != null && target != null && (cast.Rule.SpreadRadius == 0 || !target.Dead);
    internal HuntPendingCast? Cast => cast;
    internal ulong TargetId => target?.Id ?? 0;
    internal ulong AttachedTargetId => attached ? TargetId : 0;

    internal void Start(HuntPendingCast pending, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (pending.Rule.BaitRadius == 0 && pending.Rule.SpreadRadius == 0) return;
        cast = pending;
        target = actors.GetValueOrDefault(pending.TargetId);
        attached = target != null;
        waitingForObservation = pending.PacketPose && target == null;
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (cast?.Input != previous.Input) return;
        if (previous.TargetId != updated.TargetId) Start(updated, actors);
        else cast = updated;
    }

    internal void FirstObservation(HuntPendingCast observed, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (waitingForObservation) Start(observed, actors);
        waitingForObservation = false;
    }

    internal void Finish(uint action)
    {
        if (cast?.Rule.Action != action) return;
        cast = null;
        target = null;
        attached = false;
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

    internal HuntPendingCast? Preview(ReplayActorState? player)
    {
        if (!Active || cast == null || target == null) return null;
        var spread = cast.Rule.SpreadRadius > 0;
        return cast with
        {
            Shape = new CircleTemplate(spread ? cast.Rule.SpreadRadius : cast.Rule.BaitRadius),
            Location = spread ? Quantized(target.Position) : target.Position,
            SequenceId = $"{cast.Source}/target", Risky = false,
            OutlineOnly = spread || player?.Id == target.Id
        };
    }

    internal IEnumerable<HuntPendingCast> Dangers(ReplayActorState player, ReplayActorState[] party, HashSet<ulong> spreadTargets)
    {
        if (!Active || cast == null || target == null) yield break;
        var spread = cast.Rule.SpreadRadius > 0;
        if (spread && party.Length < 2) yield break;
        var radius = spread ? cast.Rule.SpreadRadius + 1 : cast.Rule.BaitRadius;
        if (!attached || player.Id != target.Id)
        {
            yield return cast with
            {
                Shape = new CircleTemplate(radius), Location = spread ? Quantized(target.Position) : target.Position,
                SequenceId = $"{cast.Source}/target-danger/{target.Id}"
            };
        }
        else
        {
            foreach (var ally in party.Where(a => a.Id != player.Id && (!spread || !spreadTargets.Contains(a.Id))))
                yield return cast with
                {
                    Shape = new CircleTemplate(radius), Location = spread ? Quantized(ally.Position) : ally.Position,
                    SequenceId = $"{cast.Source}/target-danger/{ally.Id}"
                };
        }
    }

    internal IEnumerable<string> Hints(ReplayActorState player, ReplayActorState[] party)
    {
        if (!Active || cast == null || target == null) yield break;
        var radius = MathF.Max(cast.Rule.BaitRadius, cast.Rule.SpreadRadius);
        var position = cast.Rule.SpreadRadius > 0 ? Quantized(target.Position) : target.Position;
        bool Inside(Vector3 point) => Vector2.DistanceSquared(new(point.X, point.Z), new(position.X, position.Z)) <= radius * radius;
        if (attached && player.Id == target.Id)
        {
            if (cast.Rule.SpreadRadius > 0) yield return "Spread!";
            else
            {
                if (party.Any(a => a.Id != player.Id && Inside(a.Position))) yield return "Bait away from raid!";
                if (cast.Rule.Action == 17858) yield return "Bait away + look away!";
                if (cast.Rule.Action == 16829) yield return "Bait away or interrupt!";
                if (cast.Rule.Action == 16972) yield return "Bait away!";
            }
        }
        else if (Inside(player.Position)) yield return cast.Rule.SpreadRadius > 0 ? "GTFO from spreads!" : "GTFO from baited aoe!";
    }

    private static Vector3 Quantized(Vector3 position)
    {
        const float gridSize = 2000f / 65535f;
        const float gridSizeInv = 1f / gridSize;
        float Quantize(float value) => ((int)MathF.Round(value * gridSizeInv) - .5f) * gridSize;
        return new(Quantize(position.X), position.Y, Quantize(position.Z));
    }
}
