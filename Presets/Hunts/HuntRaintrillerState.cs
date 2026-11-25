using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntRaintrillerState(ulong owner)
{
    private static readonly GeometryTemplate circle = new CircleTemplate(12);
    private static readonly GeometryTemplate donut = new DonutTemplate(10, 40);
    private static readonly GeometryTemplate cone = new FanTemplate(40, 3 * MathF.PI / 2);
    private readonly List<HuntPendingCast> aoes = [];
    private GeometryTemplate[] shapes = [];
    private DateTime lastYell;
    internal string? Hint => aoes.Count == 0 ? null
        : string.Join(" -> ", aoes.Select(a => a.Shape is CircleTemplate ? "Out" : a.Shape is DonutTemplate ? "In" : "Back"));

    internal IEnumerable<HuntPendingCast> Areas
    {
        get
        {
            for (var i = 0; i < Math.Min(aoes.Count, 2); ++i)
            {
                if (i == 0)
                    aoes[i] = aoes[i] with { Risky = true, Color = aoes.Count > 1 ? new(1, 1, 0, 1) : aoes[i].Color };
                else if (aoes[i].Shape != cone)
                    aoes[i] = aoes[i] with { Risky = false };
                yield return aoes[i];
            }
        }
    }

    internal void Yell(ushort id, DateTime at)
    {
        if (at < lastYell) return;
        lastYell = at;
        shapes = id switch
        {
            17815 => [circle],
            17719 => [circle, donut],
            17725 => [circle, cone, donut],
            17721 => [circle, donut, cone],
            17734 => [circle, cone, circle],
            17720 => [donut, circle, cone],
            17723 => [donut, cone, circle],
            17793 => [donut, donut, circle],
            17722 => [cone, donut, circle],
            17724 => [cone, circle, donut],
            _ => []
        };
    }

    internal void Start(HuntPendingCast cast)
    {
        if (cast.Rule.Action is not (39758 or 39751 or 39749)) return;
        for (var i = 0; i < shapes.Length; ++i)
            aoes.Add(cast with
            {
                Shape = shapes[i], Activation = cast.Activation.AddSeconds(1.2f + i * 3.2f),
                SequenceId = $"{owner}/raintriller/{cast.Source}/{cast.Order}/{i}", Stage = i
            });
        shapes = [];
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        for (var i = 0; i < aoes.Count; ++i)
            if (aoes[i].Order == previous.Order)
                aoes[i] = aoes[i] with
                {
                    Input = updated.Input, Location = updated.Location, Rotation = updated.Rotation,
                    Activation = updated.Activation.AddSeconds(1.2f + aoes[i].Stage * 3.2f)
                };
    }

    internal void Finish(uint action, DateTime? at)
    {
        if (action is 39750 or 39752 or 39753 && aoes.Count != 0 && !(at < aoes[0].StartedAt))
            aoes.RemoveAt(0);
    }
}
