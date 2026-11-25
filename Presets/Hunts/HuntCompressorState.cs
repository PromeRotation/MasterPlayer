using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntCompressorState
{
    private static readonly GeometryTemplate Shape = new LocalPolygonTemplate([
        new(-3.5f, -50), new(3.5f, -50), new(3.5f, -3.5f), new(50, -3.5f),
        new(50, 3.5f), new(3.5f, 3.5f), new(3.5f, 50), new(-3.5f, 50),
        new(-3.5f, 3.5f), new(-50, 3.5f), new(-50, -3.5f), new(-3.5f, -3.5f)]);
    private readonly List<(HuntPendingCast Cast, float Increment, int Remaining)> sequences = [];
    private readonly List<HuntPendingCast> areas = [];
    private float increment;
    private int numCasts;
    internal IEnumerable<HuntPendingCast> Areas => areas;

    internal void Start(HuntPendingCast cast)
    {
        if (cast.Rule.Action != 27472) return;
        numCasts = 0;
        sequences.Add((cast with { Shape = Shape, Stage = 0 }, increment, 10));
        Refresh(cast.ObservedAt ?? cast.StartedAt);
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        for (var i = 0; i < sequences.Count; ++i)
        {
            var item = sequences[i];
            if (item.Cast.Order != previous.Order) continue;
            item.Cast = item.Cast with
            {
                Input = updated.Input, Order = updated.Order, Location = updated.Location,
                Rotation = item.Cast.Rotation + updated.Rotation - previous.Rotation,
                Activation = item.Cast.Stage == 0 ? updated.Activation : item.Cast.Activation
            };
            sequences[i] = item;
        }
        Refresh(updated.ObservedAt ?? updated.StartedAt);
    }

    internal void Effect(uint action, DateTime at)
    {
        if (action is 27470 or 27471)
        {
            increment = action == 27470 ? -MathF.PI / 6 : MathF.PI / 6;
            return;
        }
        if (action is not (27472 or 27473 or 27474) || sequences.Count == 0) return;
        ++numCasts;
        var item = sequences[0];
        if (--item.Remaining == 0) sequences.RemoveAt(0);
        else
        {
            // The fifth hit advances using the old direction before reversing it.
            item.Cast = item.Cast with
            {
                Rotation = item.Cast.Rotation + item.Increment,
                Activation = at.AddSeconds(2.1f),
                Stage = item.Cast.Stage + 1
            };
            sequences[0] = item;
        }
        if (numCasts == 5 && sequences.Count > 0)
        {
            // Advancement can remove the old head; reversal applies to the resulting head.
            item = sequences[0];
            item.Increment = -item.Increment;
            item.Cast = item.Cast with { Activation = at.AddSeconds(3.6) };
            sequences[0] = item;
        }
        Refresh(at);
    }

    private void Refresh(DateTime now)
    {
        areas.Clear();
        foreach (var (cast, step, remaining) in sequences)
        {
            for (var i = 0; i < Math.Min(2, remaining); ++i)
                areas.Add(cast with
                {
                    Rotation = cast.Rotation + step * i,
                    Activation = i == 0 ? cast.Activation : (cast.Activation > now ? cast.Activation : now).AddSeconds(2.1f),
                    SequenceId = $"{cast.Source}/compressor/{cast.Order}/{cast.Stage + i}",
                    Color = i == 0 && remaining > 1 ? new Vector4(1, 1, 0, 1) : null
                });
        }
    }
}
