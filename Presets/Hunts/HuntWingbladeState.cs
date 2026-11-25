using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntWingbladeState
{
    private static readonly GeometryTemplate cone = new FanTemplate(25, MathF.PI);
    private readonly List<float> offsets = [];
    private readonly List<HuntPendingCast> queue = [];
    private int serial;

    internal string? Hint => offsets.Count == 0 ? null
        : $"Sequence: {string.Join(" -> ", offsets.Select(o => o < 0 ? "Right" : "Left"))}";

    internal IEnumerable<HuntPendingCast> Areas
    {
        get
        {
            if (queue.Count == 0) return [];
            var first = queue[0];
            queue[0] = first with
            {
                Risky = true, Color = queue.Count > 1 ? new Vector4(1, 1, 0, 1) : first.Color
            };
            if (queue.Count > 1)
            {
                var difference = first.Rotation - (queue[1].Rotation + MathF.PI);
                while (difference < -MathF.PI) difference += MathF.Tau;
                while (difference > MathF.PI) difference -= MathF.Tau;
                // The source only clears Risky here. A later pose correction does not restore it.
                if (MathF.Abs(difference) <= MathF.PI / 180)
                    queue[1] = queue[1] with { Risky = false };
            }
            return queue.Take(2);
        }
    }

    internal void Start(HuntPendingCast cast)
    {
        switch (cast.Rule.Action)
        {
            case 37164:
            case 37165:
                offsets.Clear();
                offsets.Add(cast.Rule.Action == 37164 ? -MathF.PI / 2 : MathF.PI / 2);
                Add(cast, 0, 0);
                break;
            case 37166:
            case 37167:
                offsets.Add(cast.Rule.Action == 37166 ? -MathF.PI / 2 : MathF.PI / 2);
                Add(cast, 0, 0);
                break;
            case 37170:
                for (var i = 0; i < offsets.Count; ++i) Add(cast, offsets[i], .4f + i * 2f);
                break;
        }
    }

    private void Add(HuntPendingCast cast, float offset, float delay)
        => queue.Add(cast with
        {
            Shape = cone, Rotation = cast.Rotation + offset, Activation = cast.Activation.AddSeconds(delay),
            SequenceId = $"{cast.Source}/wingblade/{serial++}"
        });

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

    internal void Effect(uint action, DateTime at)
    {
        if (queue.Count == 0 || at < queue[0].StartedAt
            || action is not (37164 or 37165 or 37166 or 37167 or 37171 or 37172)) return;
        queue.RemoveAt(0);
        if (action is 37171 or 37172 && offsets.Count != 0) offsets.RemoveAt(0);
    }
}
