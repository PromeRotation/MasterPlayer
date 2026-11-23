using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using System.Numerics;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntPredictionState
{
    private HuntPendingCast? area;
    private float whimsyOffset;
    private bool waitingForWhimsy;
    private HuntPendingCast? conditional;
    private Vector3 conditionalOrigin;
    private bool conditionalConsumed;
    internal IEnumerable<HuntPendingCast> Areas => area == null ? [] : [area];

    internal void Start(HuntPendingCast cast, ReplayActorState actor, bool correction = false)
    {
        var action = cast.Rule.Action;
        GeometryTemplate? shape = null;
        var activation = cast.Activation;
        var rotation = cast.Rotation;
        if (actor.Oid == 0x28B4 && action == 17492 && !correction)
        {
            conditional = cast;
            conditionalOrigin = actor.Position;
            conditionalConsumed = false;
        }
        if (actor.Oid == 0x35DE && action is 27354 or 27355 or 27356)
        {
            shape = action switch
            {
                27354 => new FanTemplate(30, 3 * MathF.PI / 2),
                27355 => new DonutTemplate(10, 40),
                _ => new CircleTemplate(22)
            };
            activation = correction && area != null ? area.Activation : (cast.ObservedAt ?? cast.StartedAt).AddSeconds(10.4);
        }
        else if (actor.Oid == 0x360B && action == 27318)
        {
            if (!correction)
            {
                var status = actor.Statuses.FirstOrDefault(s => s.Id is 2840 or 2841 or 2842 or 2958);
                waitingForWhimsy = cast.PacketPose && status == null;
                whimsyOffset = status?.Id switch
                {
                    2840 => -MathF.PI / 2,
                    2841 => MathF.PI / 2,
                    2842 => MathF.PI,
                    _ => 0
                };
            }
            shape = new FanTemplate(40, 2 * MathF.PI / 3);
            rotation += whimsyOffset;
            activation = cast.Activation.AddSeconds(2);
        }
        else if (actor.Oid == 0x35DC && action is 27352 or 27346 or 27341 or 27353 or 27347 or 27342)
        {
            shape = action is 27352 or 27346 or 27341 ? new DonutTemplate(5, 40) : new CircleTemplate(10);
            if (action is 27341 or 27342) activation = activation.AddSeconds(5);
        }
        else if (actor.Oid == 0x28B4 && action == 17492
            && cast.Location.X == actor.Position.X && cast.Location.Z == actor.Position.Z)
        {
            shape = new CircleTemplate(8);
            activation = cast.Activation.AddSeconds(2.6);
        }
        else if (actor.Oid == 0x288F && action == 16967)
            shape = new CircleTemplate(8);
        else if (actor.Oid == 0x2853 && action == 17366)
        {
            shape = new CircleTemplate(8);
            activation = cast.Activation.AddSeconds(3.6d);
        }
        else if (actor.Oid == 0x35BF && action is 27230 or 27231)
        {
            shape = new CircleTemplate(18);
            if (action == 27230) activation = cast.Activation.AddSeconds(12.1f);
        }
        if (shape != null)
        {
            area = cast with { Shape = shape, Rotation = rotation, Activation = activation, SequenceId = $"{cast.Source}/prediction" };
            Update(actor);
        }
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, ReplayActorState actor)
    {
        if (actor.Oid == 0x28B4 && previous.Rule.Action == 17492 && conditional?.Input == previous.Input)
        {
            conditional = updated;
            if (conditionalConsumed) return;
            if (updated.Location.X == conditionalOrigin.X && updated.Location.Z == conditionalOrigin.Z)
                area = updated with
                {
                    Shape = new CircleTemplate(8), Activation = updated.Activation.AddSeconds(2.6),
                    SequenceId = $"{updated.Source}/prediction"
                };
            else if (area?.Input == previous.Input) area = null;
            return;
        }
        if (area?.Input == previous.Input) Start(updated, actor, true);
    }

    internal void FirstObservation(HuntPendingCast cast, ReplayActorState actor)
    {
        if (waitingForWhimsy && cast.Rule.Action == 27318) Start(cast, actor);
        waitingForWhimsy = false;
    }

    internal void Finish(uint oid, uint action)
    {
        if (oid == 0x35DE && action is 27358 or 27359 or 27360
            || oid == 0x360B && action == 27319
            || oid == 0x35DC && action is 27352 or 27346 or 27353 or 27347
            || oid == 0x35BF && action == 27231)
            area = null;
    }

    internal void Effect(uint oid, uint action, DateTime at)
    {
        if (oid == 0x28B4 && action == 17489 && conditional?.StartedAt <= at) conditionalConsumed = true;
        if ((oid == 0x28B4 && action == 17489 || oid == 0x288F && action == 18037
            || oid == 0x2853 && action == 17369) && area?.StartedAt <= at)
            area = null;
    }

    internal void Update(ReplayActorState actor)
    {
        if (area == null || actor.Oid is 0x35DC or 0x28B4 or 0x288F or 0x2853) return;
        var rotation = actor.Oid == 0x35DE ? actor.Cast?.Rotation ?? actor.Rotation : area.Rotation;
        if (area.Location != actor.Position || area.Rotation != rotation)
            area = area with { Location = actor.Position, Rotation = rotation };
    }
}
