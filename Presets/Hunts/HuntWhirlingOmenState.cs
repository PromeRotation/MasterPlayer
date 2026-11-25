using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntWhirlingOmenState
{
    private static readonly GeometryTemplate cone = new FanTemplate(40, 5 * MathF.PI / 6);
    private readonly List<float> offsets = [];
    private readonly List<HuntPendingCast> queue = [];
    private int serial;

    internal IEnumerable<HuntPendingCast> Areas
    {
        get
        {
            // Upstream changes the stored first item's color when the queue is displayed.
            if (queue.Count > 1) queue[0] = queue[0] with { Color = new Vector4(1, 1, 0, 1) };
            return queue;
        }
    }

    internal void Start(HuntPendingCast cast)
    {
        if (offsets.Count == 0)
        {
            switch (cast.Rule.Action)
            {
                case 38626: offsets.Add(MathF.PI / 2); break;
                case 38627: offsets.Add(-MathF.PI / 2); break;
                case 38628: offsets.AddRange([MathF.PI / 2, -MathF.PI / 2]); break;
                case 38629: offsets.AddRange([MathF.PI / 2, MathF.PI / 2]); break;
                case 38630: offsets.AddRange([-MathF.PI / 2, MathF.PI / 2]); break;
                case 38631: offsets.AddRange([-MathF.PI / 2, -MathF.PI / 2]); break;
            }
            return;
        }

        var offset = offsets[0];
        switch (cast.Rule.Action)
        {
            case 38632: Add(cast, 0, offset + MathF.PI); break;
            case 38633: Add(cast, MathF.PI, offset); break;
            case 38634: Add(cast, offset, offset + MathF.PI); break;
            case 38635: Add(cast, offset + MathF.PI, offset); break;
            default: return;
        }
        offsets.RemoveAt(0);
    }

    private void Add(HuntPendingCast cast, float first, float second)
    {
        queue.Add(cast with
        {
            Shape = cone, Rotation = cast.Rotation + first, Activation = cast.Activation.AddSeconds(1.4f),
            SequenceId = $"{cast.Source}/whirling-omen/{serial++}"
        });
        queue.Add(cast with
        {
            Shape = cone, Rotation = cast.Rotation + second, Activation = cast.Activation.AddSeconds(3.8f),
            SequenceId = $"{cast.Source}/whirling-omen/{serial++}"
        });
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        for (var i = 0; i < queue.Count; ++i)
        {
            var item = queue[i];
            if (item.Order != previous.Order) continue;
            queue[i] = item with
            {
                Input = updated.Input, Order = updated.Order, Location = updated.Location,
                Rotation = item.Rotation + updated.Rotation - previous.Rotation,
                Activation = updated.Activation.AddTicks((item.Activation - previous.Activation).Ticks)
            };
        }
    }

    internal void Finish(uint action)
    {
        if (action is >= 38636 and <= 38641 && queue.Count != 0) queue.RemoveAt(0);
    }
}
