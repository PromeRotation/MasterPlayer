using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntForecastState
{
    private static readonly GeometryTemplate circle = new CircleTemplate(10);
    private static readonly GeometryTemplate rect = RayRectangleTemplate.Symmetric(40, 40);
    private static readonly GeometryTemplate donut = new DonutTemplate(5, 40);
    internal static readonly GeometryTemplate Cross = new LocalPolygonTemplate([
        new(-2.5f, -40), new(2.5f, -40), new(2.5f, -2.5f), new(40, -2.5f),
        new(40, 2.5f), new(2.5f, 2.5f), new(2.5f, 40), new(-2.5f, 40),
        new(-2.5f, 2.5f), new(-40, 2.5f), new(-40, -2.5f), new(-2.5f, -2.5f)]);
    private GeometryTemplate[] recorded = [];
    private readonly List<HuntPendingCast> queue = [];
    private int serial;

    internal IEnumerable<HuntPendingCast> Areas
    {
        get
        {
            if (queue.Count == 0) return [];
            var first = queue[0];
            queue[0] = first with { Risky = true, Color = queue.Count > 1 ? new Vector4(1, 1, 0, 1) : first.Color };
            if (queue.Count > 1 && (first.Shape == circle || first.Shape == donut)
                && (queue[1].Shape == circle || queue[1].Shape == donut))
                queue[1] = queue[1] with { Risky = false };
            return queue.Take(2);
        }
    }

    internal void Start(HuntPendingCast cast)
    {
        switch (cast.Rule.Action)
        {
            case 38521: recorded = [circle, rect, donut]; break;
            case 38523: recorded = [rect, donut, circle]; break;
            case 38520: recorded = [donut, circle, Cross]; break;
            case 38522: recorded = [Cross, circle, rect]; break;
            case 39128: Replace(rect, Cross); break;
            case 39125: Replace(Cross, donut); break;
            case 39127: Replace(circle, rect); break;
            case 39126: Replace(donut, circle); break;
            case 38526:
            case 38528:
            case 38524:
            case 38530:
                for (var i = 0; i < recorded.Length; ++i)
                    queue.Add(cast with
                    {
                        Shape = recorded[i], Activation = cast.Activation.AddSeconds(i switch { 0 => 0, 1 => 3.1f, _ => 6.1f }),
                        SequenceId = $"{cast.Source}/forecast/{serial++}"
                    });
                recorded = [];
                break;
        }
    }

    private void Replace(GeometryTemplate from, GeometryTemplate to)
    {
        var index = Array.IndexOf(recorded, from);
        if (index >= 0) recorded[index] = to;
    }

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

    internal void Effect(uint action, DateTime at)
    {
        if (queue.Count != 0 && at >= queue[0].StartedAt && action is >= 38524 and <= 38531)
            queue.RemoveAt(0);
    }
}
