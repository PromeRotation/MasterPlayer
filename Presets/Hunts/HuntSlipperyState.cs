using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntSlipperyState(ulong owner)
{
    private static readonly GeometryTemplate rectangle = RayRectangleTemplate.Symmetric(10, 20);
    private static readonly GeometryTemplate donut = new DonutTemplate(10, 40);
    private readonly List<HuntPendingCast> queue = [];
    internal IEnumerable<HuntPendingCast> Areas
    {
        get
        {
            if (queue.Count > 1) queue[0] = queue[0] with { Color = new(1, 1, 0, 1) };
            return queue.Select(c => c with { Source = owner });
        }
    }

    internal void Start(HuntPendingCast cast)
    {
        if (cast.Rule.Action != 38648) return;
        queue.Add(cast with
        {
            Shape = rectangle, Activation = cast.Activation.AddSeconds(.2d), Stage = 0,
            SequenceId = $"{owner}/slippery/{cast.Source}/{cast.Order}/0"
        });
        queue.Add(cast with
        {
            Shape = donut, Location = Landing(cast), Rotation = 0, Activation = cast.Activation.AddSeconds(2.8d), Stage = 1,
            SequenceId = $"{owner}/slippery/{cast.Source}/{cast.Order}/1"
        });
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        for (var i = 0; i < queue.Count; ++i)
        {
            var cast = queue[i];
            if (cast.Source != previous.Source || cast.Order != previous.Order) continue;
            queue[i] = cast with
            {
                Location = cast.Stage == 0 ? updated.Location : Landing(updated),
                Rotation = cast.Stage == 0 ? updated.Rotation : 0,
                Activation = updated.Activation.AddSeconds(cast.Stage == 0 ? .2d : 2.8d),
                Input = updated.Input, SourcePosition = updated.SourcePosition
            };
        }
    }

    internal void Effect()
    {
        if (queue.Count != 0) queue.RemoveAt(0);
    }

    internal bool Unsafe(Vector3 position) => Areas.Any(c => c.Shape!.Resolve(c.Location, c.Rotation, null).Contains(position, 0));

    private static Vector3 Landing(HuntPendingCast cast)
    {
        var from = cast.SourcePosition!.Value;
        var to = from + new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation)) * 20;
        const float grid = 2000f / 65535f;
        const float inverse = 1f / grid;
        return new(((int)MathF.Round(to.X * inverse) - .5f) * grid, to.Y, ((int)MathF.Round(to.Z * inverse) - .5f) * grid);
    }
}
