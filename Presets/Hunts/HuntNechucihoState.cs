using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntNechucihoState(ulong owner)
{
    private static readonly GeometryTemplate cone = new FanTemplate(30, MathF.PI);
    private readonly List<(HuntPendingCast Cast, bool UsesCastRotation)> queue = [];
    internal IEnumerable<HuntPendingCast> Areas
    {
        get
        {
            if (queue.Count == 0) return [];
            var (head, dependency) = queue[0];
            queue[0] = (head with
            {
                Risky = true, Color = queue.Count > 1 ? new Vector4(1, 1, 0, 1) : head.Color
            }, dependency);
            if (queue.Count > 1 && MathF.Abs(MathF.IEEERemainder(head.Rotation - queue[1].Cast.Rotation - MathF.PI,
                MathF.Tau)) <= MathF.PI / 180)
                queue[1] = (queue[1].Cast with { Risky = false }, queue[1].UsesCastRotation);
            return queue.Take(2).Select(s => s.Cast with { Source = owner });
        }
    }

    internal static bool Hit(uint action) => action is >= 39785 and <= 39788 or >= 42164 and <= 42171;

    internal void Start(HuntPendingCast cast)
    {
        float[] offsets = cast.Rule.Action switch
        {
            39493 => [0, -MathF.PI / 2, MathF.PI / 2, MathF.PI],
            39494 => [MathF.PI / 2, 0, MathF.PI, -MathF.PI / 2],
            39495 => [MathF.PI, MathF.PI / 2, -MathF.PI / 2, 0],
            _ => []
        };
        var initialCount = queue.Count;
        for (var i = 0; i < offsets.Length; ++i)
        {
            // Preserve the source's global-list dependency when Whispers overlap.
            var rotation = (i == 0 ? cast.Rotation : queue[i - 1].Cast.Rotation) + offsets[i];
            var usesRotation = i == 0 || i - 1 >= initialCount && queue[i - 1].UsesCastRotation;
            queue.Add((cast with
            {
                Shape = cone, Rotation = rotation, Activation = cast.Activation.AddSeconds(9.3f + 2f * i),
                Stage = i, SequenceId = $"{owner}/nechuciho/{cast.Order}/{i}"
            }, usesRotation));
        }
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        for (var i = 0; i < queue.Count; ++i)
        {
            var (cast, usesRotation) = queue[i];
            if (cast.Source != previous.Source || cast.Order != previous.Order) continue;
            queue[i] = (cast with
            {
                Rotation = cast.Rotation + (usesRotation ? updated.Rotation - previous.Rotation : 0),
                Location = updated.Location, Activation = updated.Activation.AddSeconds(9.3f + 2f * cast.Stage),
                Input = updated.Input
            }, usesRotation);
        }
    }

    internal void Effect()
    {
        if (queue.Count != 0) queue.RemoveAt(0);
    }
}
