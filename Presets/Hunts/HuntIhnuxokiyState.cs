using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntIhnuxokiyState(ulong owner)
{
    private static readonly GeometryTemplate circle = new CircleTemplate(12);
    private static readonly GeometryTemplate donut = new DonutTemplate(8, 40);
    private static readonly GeometryTemplate cone = new FanTemplate(40, MathF.PI);
    private GeometryTemplate? stored;
    private readonly List<HuntPendingCast> queue = [];
    private long serial;

    internal IEnumerable<HuntPendingCast> Areas
    {
        get
        {
            for (var i = 0; i < queue.Count; ++i)
                queue[i] = queue[i] with
                {
                    Risky = i == 0,
                    Color = i == 0 && queue.Count > 1 ? new Vector4(1, 1, 0, 1) : queue[i].Color
                };
            return queue.Select(cast => cast with { Source = owner });
        }
    }

    internal static bool Hit(uint action) => action is 39773 or 39828 or 39781 or 39780;

    internal void Start(HuntPendingCast cast)
    {
        if (cast.Rule.Action == 39778) stored = circle;
        else if (cast.Rule.Action == 39779) stored = donut;
        if (cast.Rule.Action is not (39773 or 39828)) return;
        queue.Add(cast with { Shape = cone, Stage = 0, SequenceId = $"{owner}/ihnuxokiy/{++serial}" });
        if (stored == null) return;
        queue.Add(cast with
        {
            Shape = stored, Rotation = 0, Stage = 1, Activation = cast.Activation.AddSeconds(2.2f),
            SequenceId = $"{owner}/ihnuxokiy/{++serial}"
        });
        stored = null;
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        for (var i = 0; i < queue.Count; ++i)
        {
            var old = queue[i];
            if (old.Order != previous.Order || old.Source != previous.Source) continue;
            queue[i] = old with
            {
                Location = updated.Location, Rotation = old.Stage == 0 ? updated.Rotation : 0,
                Activation = old.Stage == 0 ? updated.Activation : updated.Activation.AddSeconds(2.2f),
                Input = updated.Input
            };
        }
    }

    internal void Effect()
    {
        if (queue.Count != 0) queue.RemoveAt(0);
    }
}
