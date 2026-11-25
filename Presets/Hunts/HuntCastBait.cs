using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntCastBait
{
    private readonly ulong owner;
    private readonly GeometryTemplate shape;
    private readonly bool centerAtTarget;
    private readonly bool dynamicLength;
    private ReplayActorState source;
    private ReplayActorState? target;
    private bool sourceAttached = true;
    private bool targetAttached = true;
    internal HuntPendingCast Cast { get; private set; }
    internal bool SourceDestroyed { get; private set; }
    internal bool SourceDead => source.Dead;
    internal bool TargetDead => target?.Dead == true;
    internal ulong TargetId => target?.Id ?? 0;
    internal bool Active => !source.Dead && target != null;

    internal HuntCastBait(ulong owner, HuntPendingCast cast, ReplayActorState source, ReplayActorState target,
        GeometryTemplate shape, bool centerAtTarget = false, bool dynamicLength = false)
    {
        this.owner = owner;
        Cast = cast;
        this.source = source;
        this.target = target;
        this.shape = shape;
        this.centerAtTarget = centerAtTarget;
        this.dynamicLength = dynamicLength;
    }

    internal void Update(IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (sourceAttached)
        {
            if (actors.TryGetValue(source.Id, out var current)) source = current;
            else
            {
                sourceAttached = false;
                SourceDestroyed = true;
            }
        }
        if (targetAttached && target != null)
        {
            if (actors.TryGetValue(target.Id, out var current)) target = current;
            else targetAttached = false;
        }
    }

    internal void Remove(ulong id)
    {
        if (source.Id == id)
        {
            sourceAttached = false;
            SourceDestroyed = true;
        }
        if (target?.Id == id) targetAttached = false;
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (Cast.Order != previous.Order || Cast.Source != previous.Source) return;
        if (Cast.TargetId != updated.TargetId)
        {
            target = actors.GetValueOrDefault(updated.TargetId);
            targetAttached = target != null;
        }
        Cast = updated;
    }

    private HuntPendingCast? Current()
    {
        if (!Active || target == null) return null;
        var delta = target.Position - source.Position;
        var rotation = source.Id == target.Id ? source.Rotation : MathF.Atan2(delta.X, delta.Z);
        var geometry = shape;
        if (dynamicLength && shape is RayRectangleTemplate rectangle)
            geometry = rectangle with { Length = new Vector2(delta.X, delta.Z).Length() };
        return Cast with
        {
            Source = owner, Shape = geometry, Location = centerAtTarget ? target.Position : source.Position,
            Rotation = rotation, SequenceId = $"{owner}/bait/{Cast.Source}/{Cast.Order}"
        };
    }

    internal HuntPendingCast? Preview(ReplayActorState? player)
        => Current() is { } current ? current with { Risky = false, OutlineOnly = player?.Id == TargetId } : null;

    internal IEnumerable<HuntPendingCast> Dangers(ReplayActorState player, ReplayActorState[] party)
    {
        if (Current() is not { } current || target == null) yield break;
        if (!targetAttached || player.Id != target.Id)
        {
            yield return current;
            yield break;
        }
        if (source.Id == target.Id) yield break;
        foreach (var ally in party.Where(a => a.Id != player.Id))
        {
            var delta = ally.Position - source.Position;
            GeometryTemplate? avoid = null;
            var origin = source.Position;
            var rotation = MathF.Atan2(delta.X, delta.Z);
            if (shape is CircleTemplate or DonutTemplate)
            {
                avoid = shape;
                origin = ally.Position;
                rotation = 0;
            }
            else if (shape is FanTemplate fan) avoid = new FanTemplate(100, fan.AngleRadians);
            else if (shape is RayRectangleTemplate rectangle)
            {
                var halfWidth = MathF.Max(MathF.Abs(rectangle.MinLocalX), MathF.Abs(rectangle.MaxLocalX));
                var angle = 2 * MathF.Asin(halfWidth / new Vector2(delta.X, delta.Z).Length());
                // The source's asin is undefined when an ally is inside the half-width.
                if (float.IsFinite(angle)) avoid = new FanTemplate(100, angle);
            }
            if (avoid != null)
                yield return current with
                {
                    Shape = avoid, Location = origin, Rotation = rotation,
                    SequenceId = $"{current.SequenceId}/avoid/{ally.Id}"
                };
        }
    }

    internal IEnumerable<string> Hints(ReplayActorState player, ReplayActorState[] party)
    {
        if (Current() is not { } current) yield break;
        var geometry = current.Shape!.Resolve(current.Location, current.Rotation, null);
        if (player.Id == TargetId)
        {
            if (party.Any(a => a.Id != player.Id && geometry.Contains(a.Position, 0))) yield return "Bait away from raid!";
        }
        else if (geometry.Contains(player.Position, 0)) yield return "GTFO from baited aoe!";
    }
}
