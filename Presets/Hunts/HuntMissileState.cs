using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntMissileState
{
    private HuntPendingCast? area;
    private ulong target;
    private Vector3 location;
    private ReplayActorState? lastTarget;
    internal IEnumerable<HuntPendingCast> Areas => area == null ? [] : [area];

    internal void Start(HuntPendingCast cast, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        switch (cast.Rule.Action)
        {
            case 27517:
            case 27518:
                target = actors.ContainsKey(cast.TargetId) ? cast.TargetId : 0;
                lastTarget = target != 0 ? actors[target] : null;
                area = cast with
                {
                    Shape = cast.Rule.Action == 27517 ? new CircleTemplate(6) : new DonutTemplate(6, 20),
                    Activation = default, SequenceId = $"{cast.Source}/missile"
                };
                break;
            case 27299:
            case 27300:
                location = cast.Location;
                break;
        }
        Update(actors);
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (area?.Input == previous.Input && previous.Rule.Action is 27517 or 27518)
        {
            if (previous.TargetId != updated.TargetId)
            {
                Finish(previous.Rule.Action, actors);
                Start(updated, actors);
            }
            else
            {
                area = updated with { Shape = area.Shape, Activation = default, SequenceId = area.SequenceId };
                Update(actors);
            }
        }
        else if (previous.Rule.Action is 27299 or 27300)
        {
            location = updated.Location;
            Update(actors);
        }
    }

    internal void Finish(uint action, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (action is 27517 or 27518)
        {
            Update(actors);
            location = lastTarget?.Position ?? Vector3.Zero;
            target = 0;
            lastTarget = null;
            Update(actors);
        }
        else if (action is 27299 or 27300) area = null;
    }

    internal void Update(IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        if (target != 0)
        {
            if (actors.TryGetValue(target, out var actor)) lastTarget = actor;
            else target = 0;
        }
        var position = lastTarget?.Position ?? location;
        if (area != null && area.Location != position) area = area with { Location = position };
    }

    internal void Remove(ulong id)
    {
        if (target == id) target = 0;
    }
}
