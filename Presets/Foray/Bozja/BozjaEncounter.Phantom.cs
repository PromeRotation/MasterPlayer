using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal int NumPhantomLinesFinished { get; private set; }

    private void StartPhantom(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.PhantomExaflare && spell.ActionId == rule.Actions[0])
        {
            // This source takes current Actor direction, cast location, and
            // ordinary Finish callbacks; Hashmal and Cloud consume Effects.
            var (sin, cos) = Math.SinCos(actor.Actor.Rotation);
            Exaflares.Add(new(new(++serial, rule, actor, spell.Location, 0, Activation(actor)),
                new(6 * (float)sin, 0, 6 * (float)cos), 8, 2, 1.6d));
        }
        if (rule.Kind == BozjaRuleKind.PhantomKnockback)
            Casts.Add(new(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor)));
    }

    private void FinishPhantom(BozjaActor actor, BozjaRule rule, ReplayCast spell)
    {
        if (rule.Kind == BozjaRuleKind.PhantomExaflare)
        {
            var index = Exaflares.FindIndex(l => l.Cast.Rule == rule && AlmostEqual(l.Cast.Origin, spell.Location));
            if (index < 0) return;
            var line = Exaflares[index];
            line.Cast = line.Cast with { Origin = spell.Location + line.Advance, Activation = Now.AddSeconds(line.Interval) };
            if (--line.Left == 0)
            {
                Exaflares.RemoveAt(index);
                ++NumPhantomLinesFinished;
                if (IsSavagePhantom) phantomFinished[rule] = phantomFinished.GetValueOrDefault(rule) + 1;
            }
            ++exaflareVersion;
            if (IsSavagePhantom) phantomVersions[rule] = phantomVersions.GetValueOrDefault(rule) + 1;
        }
        if (rule.Kind == BozjaRuleKind.PhantomKnockback)
        {
            var index = Casts.FindIndex(c => c.Rule == rule && c.Source.Actor.Id == actor.Actor.Id);
            if (index >= 0) Casts.RemoveAt(index);
        }
    }

    private void PhantomAnimation(ulong source, uint state)
    {
        if (!Active || Actors.GetValueOrDefault(source) is not { Actor.Oid: 0x1EA1A1 } actor
            || ActiveRules.FirstOrDefault(r => r.Kind == BozjaRuleKind.PhantomArena) is not { } rule) return;
        if (state == 0x00010002)
        {
            Casts.RemoveAll(c => c.Rule == rule);
            Casts.Add(new(++serial, rule, actor, Center, 0, Now.AddSeconds(4d))
            {
                ShapeOverride = new BozjaPhantomArenaTemplate(Center),
            });
        }
        else if (state == 0x00080010)
        {
            Casts.RemoveAll(c => c.Rule == rule);
            Center = new(202, 0, -370);
            Arena = new RectangleGeometry(Center, 0, 47, 48);
        }
    }

    internal IEnumerable<(BozjaCast Cast, Vector3 From, Vector3 To)> PhantomMovements(BozjaActor viewer)
    {
        var from = viewer.Actor.Position;
        foreach (var cast in Casts.Where(c => c.Rule.Kind == BozjaRuleKind.PhantomKnockback))
        {
            var (sin, cos) = Math.SinCos(cast.Rotation);
            var to = from + new Vector3(30 * (float)sin, 0, 30 * (float)cos);
            yield return (cast, from, to);
            from = to;
        }
    }

    private bool PhantomLandingUnsafe(Vector3 position)
        => !Arena.Contains(position, 0) || exaflarePublished.Any(a => a.Danger && a.Cast.Contains(position));

    private IEnumerable<BozjaHint> PhantomHints(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.PhantomKnockback && PhantomMovements(viewer).Any(m => PhantomLandingUnsafe(m.To)))
            yield return new("About to be knocked into danger!");
    }

    private IEnumerable<BozjaZone> PhantomNavigation(BozjaRule rule)
    {
        if (rule.Kind != BozjaRuleKind.PhantomKnockback || Casts.FirstOrDefault(c => c.Rule == rule) is not { } kb) yield break;
        if (Exaflares.Count == 0)
        {
            var (sin, cos) = Math.SinCos(kb.Rotation);
            var direction = new Vector3((float)sin, 0, (float)cos);
            yield return new("phantom/hatred/rect", rule.Component, kb.Activation,
                new BozjaPhantomRectTemplate(Center + 24 * direction, Center - 16 * direction), default, 0);
        }
        else
            yield return new("phantom/hatred/shelters", rule.Component, kb.Activation,
                new BozjaPhantomShelterTemplate(Exaflares.Select(l => l.Cast.Origin + l.Advance * 6.66f).ToArray()),
                default, 0);
    }
}
