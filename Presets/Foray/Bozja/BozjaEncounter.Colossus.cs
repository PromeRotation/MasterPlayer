using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    private readonly List<(BozjaCast Cast, float Increment, int Remaining)> colossusSequences = [];
    private readonly List<BozjaAoe> colossusPublished = [];
    private int colossusVersion;
    private int colossusCount;
    internal int ColossusNumCasts { get; private set; }

    private void ClearColossus()
    {
        colossusSequences.Clear();
        colossusPublished.Clear();
        colossusVersion = colossusCount = ColossusNumCasts = 0;
    }

    private void StartColossus(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        var increment = (spell.ActionId == 24225 ? -60f : 60f) * (MathF.PI / 180f);
        colossusSequences.Add((new(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor)),
            increment, 6));
    }

    private void ColossusEffect(uint action)
    {
        if (!ActiveRules.Any(r => r.Kind == BozjaRuleKind.ColossusRotation) || action is not (24225 or 24226 or 24227)) return;
        // Unlike Train this source always advances index zero, even when
        // another caster or a later sequence produced the actual effect.
        ++ColossusNumCasts;
        if (colossusSequences.Count == 0) return;
        var sequence = colossusSequences[0];
        if (sequence.Remaining <= 1) colossusSequences.RemoveAt(0);
        else colossusSequences[0] = (sequence.Cast with
        {
            Rotation = sequence.Cast.Rotation + sequence.Increment,
            Activation = Now.AddSeconds(1.6d),
        }, sequence.Increment, sequence.Remaining - 1);
    }

    private void UpdateColossus()
    {
        if (colossusVersion == ColossusNumCasts && colossusCount == colossusSequences.Count) return;
        colossusVersion = ColossusNumCasts;
        colossusCount = colossusSequences.Count;
        PublishRotations(colossusSequences, colossusPublished, 1.6d);
    }

    private IEnumerable<BozjaZone> ColossusNavigation(BozjaRule rule)
    {
        if (colossusSequences.Count == 0)
            yield break;
        foreach (var aoe in colossusPublished)
            yield return new($"colossus/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
        if (colossusSequences.FirstOrDefault() is { Cast: { } first })
            yield return new("colossus/near", rule.Component, first.Activation, new CircleTemplate(5), first.Origin, 0, Goal: true);
    }
}
