using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntExecutionState
{
    private static readonly GeometryTemplate circle = new CircleTemplate(10);
    private static readonly GeometryTemplate donut = new DonutTemplate(10, 40);
    private static readonly GeometryTemplate cross = new LocalPolygonTemplate([
        new(-5, -40), new(5, -40), new(5, -5), new(40, -5),
        new(40, 5), new(5, 5), new(5, 40), new(-5, 40),
        new(-5, 5), new(-40, 5), new(-40, -5), new(-5, -5)]);
    private readonly List<GeometryTemplate> recorded = [];
    private readonly List<HuntPendingCast> queue = [];
    private int serial;
    internal IEnumerable<HuntPendingCast> Areas => queue.Take(2);

    internal void Start(HuntPendingCast cast)
    {
        switch (cast.Rule.Action)
        {
            case 38454: recorded.Add(circle); return;
            case 38455: recorded.Add(donut); return;
            case 38456: recorded.Add(cross); return;
            case 38457:
            case 38458:
                for (var i = 0; i < recorded.Count; ++i) Add(cast, recorded[i], i * 2.6f);
                break;
            case 38459:
            case 38460:
                for (var i = recorded.Count - 1; i >= 0; --i) Add(cast, recorded[i], 5.2f - 2.6f * i);
                break;
            default: return;
        }
        recorded.Clear();
        Refresh();
    }

    private void Add(HuntPendingCast cast, GeometryTemplate shape, float delay)
        => queue.Add(cast with
        {
            Shape = shape, Rotation = cast.Rotation + (ReferenceEquals(shape, cross) ? MathF.PI / 4 : 0),
            Activation = cast.Activation.AddSeconds(delay), SequenceId = $"{cast.Source}/execution/{serial++}"
        });

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        for (var i = 0; i < queue.Count; ++i)
        {
            var item = queue[i];
            if (item.Input != previous.Input) continue;
            queue[i] = item with
            {
                Input = updated.Input, Location = updated.Location,
                Rotation = item.Rotation + updated.Rotation - previous.Rotation,
                Activation = updated.Activation.AddTicks((item.Activation - previous.Activation).Ticks)
            };
        }
    }

    internal void Effect(uint action, DateTime at)
    {
        if (action is < 38457 or > 38463 || queue.Count == 0 || at < queue[0].StartedAt) return;
        queue.RemoveAt(0);
        Refresh();
    }

    private void Refresh()
    {
        for (var i = 0; i < Math.Min(2, queue.Count); ++i)
        {
            var item = queue[i];
            if (i == 0) queue[i] = item with
            {
                Risky = true, Color = queue.Count > 1 ? new Vector4(1, 1, 0, 1) : item.Color
            };
            else if (!ReferenceEquals(item.Shape, cross)) queue[i] = item with { Risky = false };
        }
    }
}
