using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntQueenHawkState
{
    private HuntPendingCast? aoe;
    internal IEnumerable<HuntPendingCast> Areas => aoe is { } area ? [area] : [];

    internal void Start(HuntPendingCast cast)
    {
        var shape = cast.Rule.Action switch
        {
            39482 => (GeometryTemplate)new CircleTemplate(12),
            39483 => new DonutTemplate(10, 40),
            _ => null
        };
        if (shape != null)
            aoe = cast with
            {
                Shape = shape, Rotation = 0, Activation = cast.Activation.AddSeconds(.8d),
                SequenceId = $"{cast.Source}/bee"
            };
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        if (aoe?.Order == previous.Order) Start(updated);
    }

    internal void Lose(DateTime? at = null)
    {
        if (aoe != null && !(at < aoe.StartedAt)) aoe = null;
    }
}
