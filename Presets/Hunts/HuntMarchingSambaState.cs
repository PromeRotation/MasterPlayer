using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntMarchingSambaState
{
    private static readonly GeometryTemplate circle = new CircleTemplate(12);
    private static readonly GeometryTemplate cone = new FanTemplate(40, MathF.PI);
    private readonly List<HuntPendingCast> queue = [];
    private int serial;

    internal IEnumerable<HuntPendingCast> Areas
    {
        get
        {
            if (queue.Count > 1) queue[0] = queue[0] with { Color = new Vector4(1, 1, 0, 1) };
            return queue;
        }
    }

    internal void Start(HuntPendingCast cast)
    {
        switch (cast.Rule.Action)
        {
            case 39863: Add(cast, circle, 0); break;
            case 39864: Add(cast, cone, 0); break;
            case 39797:
                Add(cast, circle, 1.7f);
                Add(cast, cone, 5.7f);
                break;
        }
    }

    private void Add(HuntPendingCast cast, GeometryTemplate shape, float delay)
        => queue.Add(cast with
        {
            Shape = shape, Activation = cast.Activation.AddSeconds(delay),
            SequenceId = $"{cast.Source}/marching-samba/{serial++}"
        });

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        for (var i = 0; i < queue.Count; ++i)
        {
            var item = queue[i];
            if (item.Order != previous.Order) continue;
            queue[i] = item with
            {
                Input = updated.Input, Order = updated.Order, Location = updated.Location, Rotation = updated.Rotation,
                Activation = updated.Activation.AddTicks((item.Activation - previous.Activation).Ticks)
            };
        }
    }

    internal void Finish(uint action)
    {
        if (queue.Count != 0 && action is 39863 or 39864 or 39755 or 39756) queue.RemoveAt(0);
    }
}
