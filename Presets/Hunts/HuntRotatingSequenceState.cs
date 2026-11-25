using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntRotatingSequenceState
{
    private readonly List<HuntPendingCast> queue = [];
    private int serial;
    private bool imminentDanger;
    internal IEnumerable<HuntPendingCast> Areas => queue.GroupBy(a => a.Order)
        .SelectMany(g => Sugaar(g.First().Rule.Action) ? g.Take(2).Reverse() : g.Take(Shown(g.First().Rule.Action)));

    internal void Start(HuntPendingCast cast)
    {
        var action = cast.Rule.Action;
        if (action is not (27493 or 27494 or 27497 or 27498 or 27521 or 27522 or 18100 or 18101)) return;
        var count = action is 27493 or 27494 ? 2 : 8;
        var increment = action is 27493 or 27494 ? MathF.PI : action is 27521 or 27522 ? -MathF.PI / 2 : MathF.PI / 2;
        var shape = new FanTemplate(30, MathF.PI);
        var activation = cast.Activation;
        var rotation = cast.Rotation;
        if (Sugaar(action))
        {
            count = 3;
            increment = (action == 18100 ? 2 : -2) * MathF.PI / 3;
            shape = new(action == 18100 ? 13 : 18, 2 * MathF.PI / 3);
            rotation += action == 18101 ? MathF.PI : 0;
            activation = activation.AddSeconds(1.1f);
        }
        imminentDanger = count > 2;
        for (var i = 0; i < count; ++i)
            queue.Add(cast with
            {
                Shape = shape, Rotation = rotation + increment * i,
                Activation = activation.AddSeconds(Interval(action) * i), Stage = i,
                SequenceId = $"{cast.Source}/rotation/{serial++}"
            });
        Refresh(cast.ObservedAt ?? cast.StartedAt);
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        var unadvanced = queue.Any(item => item.Order == previous.Order && item.Stage == 0);
        for (var i = 0; i < queue.Count; ++i)
        {
            var item = queue[i];
            if (item.Order != previous.Order) continue;
            queue[i] = item with
            {
                Input = updated.Input, Order = updated.Order, Location = updated.Location, Rotation = item.Rotation + updated.Rotation - previous.Rotation,
                Activation = unadvanced ? updated.Activation.AddSeconds((Sugaar(item.Rule.Action) ? (double)1.1f : 0)
                    + Interval(item.Rule.Action) * item.Stage) : item.Activation
            };
        }
        if (Sugaar(updated.Rule.Action)) Refresh(updated.ObservedAt ?? updated.StartedAt);
    }

    internal void Finish(uint action, DateTime at)
    {
        if (queue.Count == 0 || action is not (27493 or 27494 or 27495 or 27496 or 27497 or 27498 or 27499 or 27500 or 27521 or 27522 or 18098 or 18099)) return;
        if (Sugaar(queue[0].Rule.Action) && action is not (18098 or 18099)) return;
        var order = queue[0].Order;
        var interval = Interval(queue[0].Rule.Action);
        queue.RemoveAt(0);
        var stage = 0;
        for (var i = 0; i < queue.Count && queue[i].Order == order; ++i)
            queue[i] = queue[i] with { Activation = at.AddSeconds(interval * ++stage) };
        Refresh(at);
    }

    private void Refresh(DateTime now)
    {
        foreach (var group in queue.Select((cast, index) => (cast, index)).GroupBy(p => p.cast.Order))
        {
            var first = true;
            foreach (var (cast, index) in group)
            {
                Vector4? color = first && group.Count() > 1 && imminentDanger ? new(1, 1, 0, 1) : null;
                if (cast.Color != color) queue[index] = cast with { Color = color };
                if (!first && Sugaar(cast.Rule.Action))
                {
                    var head = queue[group.First().index];
                    queue[index] = queue[index] with
                    {
                        Activation = (head.Activation > now ? head.Activation : now)
                            .AddSeconds((double)2.7f * (cast.Stage - head.Stage))
                    };
                }
                first = false;
            }
        }
    }

    private static int Shown(uint action) => action is 27493 or 27494 ? 1 : 2;
    private static bool Sugaar(uint action) => action is 18100 or 18101;
    private static float Interval(uint action) => Sugaar(action) ? 2.7f : 3.6f;
}
