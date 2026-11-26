using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    private readonly List<BozjaAoe> dahuShockwaves = [];
    private readonly List<(BozjaCast Cast, float Increment, int Remaining)> dahuSequences = [];
    private readonly List<BozjaAoe> dahuRotatingAoes = [];
    internal List<BozjaRush> DahuBaits { get; } = [];
    private float dahuRotation;
    private float dahuIncrement;
    private DateTime dahuActivation;
    private int dahuRotationVersion;
    private int dahuRotationCount;
    internal int NumDahuRotations { get; private set; }
    internal int NumDahuCharges { get; private set; }

    private void ClearDahu()
    {
        dahuShockwaves.Clear();
        dahuSequences.Clear();
        dahuRotatingAoes.Clear();
        DahuBaits.Clear();
        dahuRotation = dahuIncrement = 0;
        dahuActivation = default;
        dahuRotationVersion = dahuRotationCount = NumDahuRotations = NumDahuCharges = 0;
    }

    private void StartDahu(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.DahuShockwave && Array.IndexOf(rule.Actions, spell.ActionId) < 2)
        {
            dahuShockwaves.Add(new(new(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor)), false));
            dahuShockwaves.Add(new(new(++serial, rule, actor, spell.Location,
                (spell.Rotation ?? 0) + 180f * (MathF.PI / 180), Activation(actor, 2.6f)), false));
        }
        if (rule.Kind == BozjaRuleKind.DahuRotation && spell.ActionId == rule.Actions[0])
        {
            dahuRotation = spell.Rotation ?? 0;
            dahuActivation = Activation(actor, .7f);
            InitDahuRotation(actor, rule);
        }
        if (rule.Kind == BozjaRuleKind.DahuKnockback)
            Casts.Add(new(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor)));
        if (rule.Kind == BozjaRuleKind.DahuBait && Actors.GetValueOrDefault(spell.TargetId) is { } target)
            DahuBaits.Add(new(++serial, rule, actor, target, 0, Activation(actor)));
    }

    private void FinishDahu(BozjaActor actor, BozjaRule rule, ReplayCast spell)
    {
        if (rule.Kind == BozjaRuleKind.DahuShockwave && dahuShockwaves.Count > 0)
            dahuShockwaves.RemoveAt(0);
        if (rule.Kind == BozjaRuleKind.DahuRotation && spell.ActionId == rule.Actions[1])
        {
            // The source increments even for an empty sequence, then consumes
            // index zero without testing caster identity, location or angle.
            ++NumDahuRotations;
            if (dahuSequences.Count > 0)
            {
                var sequence = dahuSequences[0];
                if (sequence.Remaining <= 1) dahuSequences.RemoveAt(0);
                else dahuSequences[0] = (sequence.Cast with
                {
                    Rotation = sequence.Cast.Rotation + sequence.Increment,
                    Activation = Now.AddSeconds(2d),
                }, sequence.Increment, sequence.Remaining - 1);
            }
        }
        if (rule.Kind == BozjaRuleKind.DahuKnockback)
        {
            var index = Casts.FindIndex(c => c.Rule == rule && c.Source.Actor.Id == actor.Actor.Id);
            if (index >= 0) Casts.RemoveAt(index);
        }
        // HeatBreath deliberately ends on its actual Effect, not Finish.
    }

    private void DahuIcon(BozjaActor actor, uint icon)
    {
        if (!Active || ActiveRules.FirstOrDefault(r => r.Kind == BozjaRuleKind.DahuRotation) is not { } rule) return;
        // Unknown icons clear the pending increment, including player icons.
        dahuIncrement = icon switch { 167 => -90f * (MathF.PI / 180), 168 => 90f * (MathF.PI / 180), _ => 0 };
        InitDahuRotation(actor, rule);
    }

    private void InitDahuRotation(BozjaActor actor, BozjaRule rule)
    {
        if (dahuRotation == 0 || dahuIncrement == 0) return;
        dahuSequences.Add((new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
            dahuRotation, dahuActivation), dahuIncrement, 5));
        dahuRotation = dahuIncrement = 0;
    }

    private void DahuEffect(BozjaActor caster, uint action)
    {
        if (ActiveRules.Any(r => r.Kind == BozjaRuleKind.Charge && r.Component == "HotCharge" && r.Actions.Contains(action)))
            ++NumDahuCharges;
        if (Definition.Rules.FirstOrDefault(r => r.Kind == BozjaRuleKind.DahuBait && r.Actions.Contains(action)) is not { } rule) return;
        var index = DahuBaits.FindIndex(b => b.Rule == rule && b.Source.Actor.Id == caster.Actor.Id);
        if (index >= 0) DahuBaits.RemoveAt(index);
    }

    private void UpdateDahu()
    {
        DahuBaits.RemoveAll(b => b.Source.Destroyed || b.Source.Actor.Dead || b.Target.Actor.Dead);
        if (dahuRotationVersion == NumDahuRotations && dahuRotationCount == dahuSequences.Count) return;
        dahuRotationVersion = NumDahuRotations;
        dahuRotationCount = dahuSequences.Count;
        PublishRotations(dahuSequences, dahuRotatingAoes, 2d);
    }

    private IEnumerable<BozjaAoe> DahuAoes(BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.DahuRotation)
            foreach (var aoe in dahuRotatingAoes) yield return aoe;
        if (rule.Kind != BozjaRuleKind.DahuShockwave) yield break;
        // ActiveAOEs mutates the source list. In particular a lone promoted
        // tail retains color zero; an earlier Danger head stays Danger.
        for (var i = 0; i < dahuShockwaves.Count; ++i)
        {
            var aoe = dahuShockwaves[i];
            dahuShockwaves[i] = aoe with { Danger = aoe.Danger || i == 0 && dahuShockwaves.Count > 1, Risky = i == 0 };
            yield return dahuShockwaves[i];
        }
    }

    internal (Vector3 Origin, float Radius, float Weight)? DahuChargeGoal()
    {
        if (!Active || Definition.Key != "DRN2Dahu") return null;
        return (++NumDahuCharges & 1) == 0 ? null : (Primary?.Cast?.Location ?? Center, 6, 5);
    }

    private IEnumerable<BozjaZone> DahuNavigation(BozjaActor viewer, BozjaRule rule)
    {
        foreach (var aoe in DahuAoes(rule).Where(a => a.Risky))
            yield return new($"dahu/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
        if (rule.Kind == BozjaRuleKind.DahuRotation && dahuSequences.Count > 0 && Primary != null)
            yield return new("dahu/rotation/near", rule.Component, default, null, default, 0,
                [Primary.Actor.Position], JoinRadius: 5, Goal: true);
        if (rule.Kind is BozjaRuleKind.DahuKnockback or BozjaRuleKind.DahuSavageKnockback
            && Casts.FirstOrDefault(c => c.Rule == rule) is { } kb && Primary != null)
        {
            var claws = Casts.Where(c => c.Rule.Component == "HuntersClaw").ToArray();
            if (claws.Length > 0)
                yield return new("dahu/howl/claws", rule.Component, kb.Activation,
                    new BozjaDahuClawsTemplate(Primary.Actor.Position, claws.Select(c => c.Origin).ToArray()),
                    Primary.Actor.Position, 0);
        }
        if (rule.Kind != BozjaRuleKind.DahuBait) yield break;
        foreach (var bait in DahuBaits.Where(b => !b.Source.Actor.Dead))
        {
            var cast = HornView(bait);
            if (!ReferenceEquals(bait.Target, viewer))
                yield return new($"dahu/bait/{bait.Generation}", rule.Component, bait.Activation,
                    cast.Shape, cast.Origin, cast.Rotation);
            else if (!ReferenceEquals(bait.Source, bait.Target))
                foreach (var member in Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot))
                {
                    if (Actors.GetValueOrDefault(member.EntityId) is not { Actor.Dead: false } actor
                        || ReferenceEquals(actor, viewer)) continue;
                    var delta = actor.Actor.Position - bait.Source.Actor.Position;
                    yield return new($"dahu/bait/{bait.Generation}/{member.Slot}", rule.Component, bait.Activation,
                        new FanTemplate(100, ((FanTemplate)cast.Shape).AngleRadians), cast.Origin, MathF.Atan2(delta.X, delta.Z));
                }
        }
    }

    internal IEnumerable<(BozjaCast Cast, Vector3 From, Vector3 To)> DahuMovements(BozjaActor viewer)
    {
        var from = viewer.Actor.Position;
        foreach (var cast in Casts.Where(c => c.Rule.Kind is BozjaRuleKind.DahuKnockback or BozjaRuleKind.DahuSavageKnockback))
        {
            var dx = from.X - cast.Origin.X;
            var dz = from.Z - cast.Origin.Z;
            var length = MathF.Sqrt(dx * dx + dz * dz);
            if (length == 0) continue;
            var inv = 1f / length;
            var direction = new Vector3(dx * inv, 0, dz * inv);
            var distance = MathF.Min(30, DelubrumDahuGeometry.WallDistance(Arena, from, direction)
                - Math.Clamp(viewer.Actor.HitboxRadius - .499f, .5f - .499f, viewer.Actor.HitboxRadius - .499f));
            var to = from + distance * direction;
            yield return (cast, from, to);
            from = to;
        }
    }

    private IEnumerable<BozjaHint> DahuHints(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.DahuKnockback && DahuMovements(viewer)
            .Any(m => Casts.Any(c => c.Rule.Component == "HuntersClaw" && c.Contains(m.To))))
            yield return new("About to be knocked into danger!");
        if (rule.Kind != BozjaRuleKind.DahuBait) yield break;
        var active = DahuBaits.Where(b => !b.Source.Actor.Dead).ToArray();
        if (active.Any(b => b.Target.Actor.Id == viewer.Actor.Id
            && Party.Any(p => p.EntityId != b.Target.Actor.Id
                && Actors.GetValueOrDefault(p.EntityId) is { Actor.Dead: false } member
                && HornView(b).Contains(member.Actor.Position)))) yield return new("Bait away from raid!");
        if (active.Any(b => b.Target.Actor.Id != viewer.Actor.Id && HornView(b).Contains(viewer.Actor.Position)))
            yield return new("GTFO from baited aoe!");
    }

    private IEnumerable<BozjaDamage> DahuDamage()
    {
        ulong mask = 0;
        foreach (var bait in DahuBaits.Where(b => !b.Source.Actor.Dead))
        {
            var slot = Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot).FirstOrDefault(p => p.EntityId == bait.Target.Actor.Id)?.Slot;
            if (slot is { } i) mask |= 1UL << i;
        }
        if (mask != 0) yield return new(mask, DahuBaits[0].Activation, "Tankbuster");
    }
}
