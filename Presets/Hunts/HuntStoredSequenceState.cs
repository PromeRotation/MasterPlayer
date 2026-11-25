using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntStoredSequenceState
{
    private readonly List<HuntPendingCast> queue = [];
    private bool active;
    private int serial;
    private bool waitingForBearings;
    internal IEnumerable<HuntPendingCast> Areas => active ? queue.Take(1) : [];

    internal void Start(HuntPendingCast cast, ReplayActorState actor)
    {
        var action = cast.Rule.Action;
        if (actor.Oid == 0x35BD)
        {
            if (action is 26876 or 26877)
                Add(cast, action == 26876 ? new CircleTemplate(8) : new DonutTemplate(8, 40), 0);
            else if (action is 26878 or 26879 or 26915 or 26916)
            {
                if (action is 26915 or 26916) queue.Reverse();
                active = true;
            }
        }
        else if (actor.Oid == 0x3670 && action is 27624 or 27625)
        {
            waitingForBearings = cast.PacketPose && !actor.Statuses.Any(s => s.Id is >= 2835 and <= 2838);
            queue.Clear();
            foreach (var status in actor.Statuses.Where(s => s.Id is >= 2835 and <= 2838).OrderBy(s => s.Id))
            {
                var offset = status.Id switch { 2836 => MathF.PI, 2837 => MathF.PI / 2, 2838 => -MathF.PI / 2, _ => 0 };
                var rotation = cast.Rotation + offset + (action == 27625 ? MathF.PI : 0);
                Add(cast, new FanTemplate(40, MathF.PI), rotation);
                Add(cast, new FanTemplate(40, MathF.PI), rotation + MathF.PI);
            }
            active = true;
        }
        Update(actor);
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        for (var i = 0; i < queue.Count; ++i)
        {
            var item = queue[i];
            if (item.Order != previous.Order) continue;
            queue[i] = item with { Input = updated.Input, Order = updated.Order, Rotation = item.Rotation + updated.Rotation - previous.Rotation };
        }
    }

    internal void Finish(uint oid, uint action)
    {
        if (oid == 0x3670 && (action is >= 27626 and <= 27633 or 27714 or 27715)) Pop();
    }

    internal void FirstObservation(HuntPendingCast cast, ReplayActorState actor)
    {
        if (waitingForBearings && cast.Rule.Action is 27624 or 27625) Start(cast, actor);
        waitingForBearings = false;
    }

    internal void Effect(uint oid, uint action, DateTime at)
    {
        if (oid == 0x35BD && action is 26878 or 26879 or 26880 or 26881 or 26915 or 26916 or 26167 or 26168
            && queue.Count > 0 && at >= queue[0].StartedAt) Pop();
    }

    internal void Update(ReplayActorState actor)
    {
        for (var i = 0; i < queue.Count; ++i)
            if (queue[i].Location != actor.Position) queue[i] = queue[i] with { Location = actor.Position };
    }

    private void Add(HuntPendingCast cast, GeometryTemplate shape, float rotation)
        => queue.Add(cast with { Shape = shape, Rotation = rotation, Activation = default, SequenceId = $"{cast.Source}/stored/{serial++}" });

    private void Pop()
    {
        if (queue.Count > 0) queue.RemoveAt(0);
        active = queue.Count > 0;
    }
}
