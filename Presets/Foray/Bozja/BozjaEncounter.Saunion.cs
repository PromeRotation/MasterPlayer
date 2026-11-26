using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsSaunion => Definition.PrimaryOid == 0x31DD;
    internal BozjaActor? Dawon { get; private set; }
    internal int SaunionPhase { get; private set; }
    internal int SpiralNumCasts { get; private set; }
    private readonly List<BozjaRule> saunionRules = [];
    private readonly List<BozjaAoe> saunionSpirals = [];
    private readonly List<BozjaCast> saunionObey = [];
    private List<BozjaActor>? saunionDawons;
    private bool saunionOneMind;

    private void ClearSaunion()
    {
        Dawon = null;
        SaunionPhase = SpiralNumCasts = 0;
        saunionRules.Clear();
        saunionSpirals.Clear();
        saunionObey.Clear();
        saunionDawons = null;
        saunionOneMind = false;
    }

    private void ActivateSaunion(BozjaRule rule)
    {
        saunionRules.Add(rule);
        // Component activation replays current Created, Cast and all 60
        // status slots, including Pets. EAnim history is never replayed.
        foreach (var actor in Actors.Values)
        {
            if (actor.NativeNpc && rule.Kind == BozjaRuleKind.SaunionPlumes)
                SaunionCreated(actor, rule);
            if (actor.Cast != null) Start(actor, rule);
            if (rule.Kind == BozjaRuleKind.SaunionMind)
                foreach (var status in actor.Statuses.OfType<ReplayStatus>())
                    SaunionStatus(status, true);
            if (rule.Kind == BozjaRuleKind.SaunionKnockback)
                foreach (var status in actor.Statuses.OfType<ReplayStatus>())
                    ImmuneStatus(actor, status, true);
        }
    }

    private void BeginSaunion()
    {
        foreach (var rule in Definition.Rules.Take(8)) ActivateSaunion(rule);
    }

    private void UpdateSaunion()
    {
        if (!IsSaunion) return;
        if (SaunionPhase == 0 && (Primary!.Destroyed || Dawon?.Actor.Targetable == true))
        {
            SaunionPhase = 1;
            foreach (var rule in saunionRules.Where(r => r.Component is "MagitekHalo" or "MagitekCrossray" or "Touchdown").ToArray())
            {
                saunionRules.Remove(rule);
                Casts.RemoveAll(c => c.Rule == rule);
            }
            foreach (var rule in Definition.Rules.Skip(8)) ActivateSaunion(rule);
        }
        if (SaunionPhase == 1 && (Primary!.Destroyed || Primary.Actor.Dead)
            && (Dawon == null || Dawon.Destroyed || Dawon.Actor.Dead))
        {
            Active = false;
            Completed = true;
            Casts.Clear();
            saunionRules.Clear();
            return;
        }
        // StateMachine.Update precedes UpdateModule; keep the first OID
        // match's original reference, even after destruction/replacement.
        saunionDawons ??= Actors.Values.Where(a => a.Actor.Oid == 0x31DE).ToList();
        Dawon ??= saunionDawons.FirstOrDefault();
        if (Dawon == null || Dawon.Destroyed || Dawon.Actor.Dead) saunionObey.Clear();
    }

    private void SaunionCreated(BozjaActor actor, BozjaRule rule)
    {
        GeometryTemplate? shape = actor.Actor.Oid switch
        {
            0x31E1 => new DonutTemplate(3, 12),
            0x31E0 => new CircleTemplate(10),
            _ => null,
        };
        if (shape != null)
            Casts.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
                0, Now.AddSeconds(14.4d)) { ShapeOverride = shape });
    }

    private void SaunionCreated(BozjaActor actor)
    {
        if (IsSaunion && actor.Actor.Oid == 0x31DE) saunionDawons?.Add(actor);
        if (!IsSaunion || !Active || !actor.NativeNpc) return;
        foreach (var rule in saunionRules.Where(r => r.Kind == BozjaRuleKind.SaunionPlumes))
            SaunionCreated(actor, rule);
    }

    private void SaunionDestroyed(BozjaActor actor)
    {
        saunionDawons?.Remove(actor);
        if (IsSaunion && Active && actor.NativeNpc && actor.Actor.Oid is 0x1EB216 or 0x1EB217)
            saunionSpirals.Clear();
    }

    private void StartSaunion(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.SaunionMobile)
        {
            var halo = spell.ActionId is >= 23993 and <= 23996;
            var index = (int)(spell.ActionId - (halo ? 23993u : 23997u));
            Vector3[] offsets = [new(0, 0, 18), new(-18, 0, 0), new(18, 0, 0), new(0, 0, -18)];
            Casts.RemoveAll(c => c.Rule == rule);
            Casts.Add(new(++serial, rule, actor,
                DungeonEncounter.QuantizedDungeonPosition(Center + offsets[index]), 0, Activation(actor, 2.1d))
                { ShapeOverride = halo ? new DonutTemplate(9, 60) : new DungeonCrossTemplate(60, 8.5f) });
        }
        else if (rule.Kind == BozjaRuleKind.SaunionSpiral)
        {
            SpiralNumCasts = 0;
            for (var i = 0; i < 6; ++i)
            {
                var pattern = DalriadaSpirals.Patterns[i];
                if (!AlmostEqual(pattern[0], spell.Location)) continue;
                // The source uses the pattern index, not the point index:
                // every point in this pattern has the same activation.
                var activation = Activation(actor, .8d).AddSeconds(.6d * i);
                foreach (var point in pattern)
                    saunionSpirals.Add(new(new(++serial, rule, actor, point, 0, activation), false, true));
                break;
            }
        }
        else if (rule.Kind == BozjaRuleKind.SaunionBait)
        {
            if (Actors.GetValueOrDefault(spell.TargetId) is { } target)
                Casts.Add(new(++serial, rule, actor, spell.Location, 0, Activation(actor)) { Target = target });
        }
        else if (rule.Kind == BozjaRuleKind.SaunionKnockback)
            Casts.Add(new(++serial, rule, actor, spell.Location, 0, Activation(actor)));
    }

    private void SaunionEffect(uint action)
    {
        if (!IsSaunion) return;
        if (action is 23990 or 23992)
            Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.SaunionMobile);
        if (SaunionPhase != 1) return;
        if (action is 24011 or 24012 && saunionObey.Count > 0) saunionObey.RemoveAt(0);
        if (action == 23987) ++SpiralNumCasts;
    }

    private void SaunionAnimation(ulong source, uint state)
    {
        if (!Active || !IsSaunion || SaunionPhase != 1 || Dawon == null || Dawon.Destroyed || Dawon.Actor.Dead
            || state is not (0x10002 or 0x100020 or 0x400080)
            || Actors.GetValueOrDefault(source) is not { } actor
            || actor.Actor.Oid is not (0x1EB1E8 or 0x1EB1E7)) return;
        var cross = actor.Actor.Oid == 0x1EB1E8;
        var position = DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position);
        var previous = saunionObey.Count == 0 ? Dawon.Actor.Position : saunionObey[^1].Origin;
        var rotation = cross ? MathF.Atan2(position.X - previous.X, position.Z - previous.Z) : 0;
        var activation = saunionObey.Count == 0 ? Now.AddSeconds(16.2d)
            : saunionObey[0].Activation.AddSeconds(6.4d * saunionObey.Count);
        var rule = saunionRules.Single(r => r.Kind == BozjaRuleKind.SaunionObey);
        saunionObey.Add(new(++serial, rule, actor, position, rotation, activation)
            { ShapeOverride = cross ? new DungeonCrossTemplate(50, 7) : new DonutTemplate(12, 60) });
    }

    private void SaunionStatus(ReplayStatus status, bool gain)
    {
        if (IsSaunion && SaunionPhase == 1 && status.Id == 2553) saunionOneMind = gain;
    }

    private IEnumerable<BozjaAoe> SaunionAoes(BozjaActor? viewer, BozjaRule rule)
    {
        if (rule.Kind is BozjaRuleKind.SaunionMobile or BozjaRuleKind.SaunionPlumes)
            foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false, true);
        if (rule.Kind == BozjaRuleKind.SaunionObey && saunionObey.Count > 0)
            yield return new(saunionObey[0], false, true);
        if (rule.Kind == BozjaRuleKind.SaunionSpiral && SpiralNumCasts < saunionSpirals.Count)
        {
            saunionSpirals[SpiralNumCasts] = saunionSpirals[SpiralNumCasts] with { Danger = true };
            foreach (var aoe in saunionSpirals.Skip(SpiralNumCasts).Take(6)) yield return aoe;
        }
        if (rule.Kind == BozjaRuleKind.SaunionBait)
            foreach (var cast in Casts.Where(c => c.Rule == rule && !c.Source.Actor.Dead && c.Target!.Actor.Id != viewer?.Actor.Id))
                yield return new(SaunionBaitView(cast), false, true);
    }

    internal static BozjaCast SaunionBaitView(BozjaCast cast)
        => cast with { Origin = cast.Target!.Actor.Position };

    private IEnumerable<BozjaZone> SaunionNavigation(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind is BozjaRuleKind.SaunionMobile or BozjaRuleKind.SaunionPlumes or BozjaRuleKind.SaunionObey or BozjaRuleKind.SaunionSpiral)
            foreach (var aoe in SaunionAoes(viewer, rule))
                yield return new($"saunion/aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                    aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
        if (rule.Kind == BozjaRuleKind.SaunionMind && saunionOneMind && Dawon is { } dawon && Primary is { } primary
            && (viewer.Actor.Id == primary.Actor.TargetId ^ viewer.Actor.Id == dawon.Actor.TargetId))
        {
            var other = viewer.Actor.Id == primary.Actor.TargetId ? dawon : primary;
            var pos = other.Actor.Position;
            yield return new("saunion/mind", rule.Component, DateTime.MaxValue,
                new CircleTemplate(MathF.Sqrt(DistanceSquared(viewer.Actor.Position, pos)) + 2), pos, 0);
        }
        if (rule.Kind != BozjaRuleKind.SaunionBait) yield break;
        foreach (var cast in Casts.Where(c => c.Rule == rule && !c.Source.Actor.Dead))
        {
            if (!ReferenceEquals(cast.Target, viewer))
                yield return new($"saunion/bait/{cast.Generation}", rule.Component, cast.Activation, cast.Shape, cast.Target!.Actor.Position, 0);
            else if (!ReferenceEquals(cast.Source, cast.Target))
                foreach (var member in Party.Where(p => p.Slot is >= 0 and < 64 && p.EntityId != viewer.Actor.Id).OrderBy(p => p.Slot)
                    .Select(p => Actors.GetValueOrDefault(p.EntityId)).OfType<BozjaActor>().Where(a => !a.Actor.Dead))
                    yield return new($"saunion/bait/{cast.Generation}/{member.BirthOrder}", rule.Component, cast.Activation, cast.Shape, member.Actor.Position, 0);
        }
    }

    private IEnumerable<BozjaHint> SaunionHints(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind != BozjaRuleKind.SaunionBait) yield break;
        var active = Casts.Where(c => c.Rule == rule && !c.Source.Actor.Dead).ToArray();
        if (active.Any(c => c.Target!.Actor.Id == viewer.Actor.Id && Party.Where(p => p.Slot is >= 0 and < 64).Select(p => Actors.GetValueOrDefault(p.EntityId))
            .OfType<BozjaActor>().Any(a => a.Actor.Id != c.Target.Actor.Id && !a.Actor.Dead && SaunionBaitView(c).Contains(a.Actor.Position))))
            yield return new("Bait away from raid!");
        if (active.Any(c => c.Target!.Actor.Id != viewer.Actor.Id && SaunionBaitView(c).Contains(viewer.Actor.Position)))
            yield return new("GTFO from baited aoe!");
    }

    private IEnumerable<BozjaDamage> SaunionDamage(BozjaRule rule)
    {
        if (rule.Kind != BozjaRuleKind.SaunionBait) yield break;
        var list = Casts.Where(c => c.Rule == rule).ToArray();
        ulong mask = 0;
        foreach (var cast in list.Where(c => !c.Source.Actor.Dead))
        {
            var slot = Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot)
                .FirstOrDefault(p => p.EntityId == cast.Target!.Actor.Id)?.Slot ?? -1;
            if (slot is >= 0 and < 64) mask |= 1UL << slot;
        }
        if (mask != 0) yield return new(mask, list[0].Activation, "Tankbuster");
    }

    internal IEnumerable<(BozjaCast Cast, Vector3 From, Vector3 To)> SaunionMovements(BozjaActor viewer)
    {
        if (!Active) yield break;
        var from = viewer.Actor.Position;
        foreach (var cast in Casts.Where(c => c.Rule.Kind == BozjaRuleKind.SaunionKnockback))
        {
            if (WindImmune(viewer, cast.Activation)) continue;
            var dx = from.X - cast.Origin.X;
            var dz = from.Z - cast.Origin.Z;
            var length = MathF.Sqrt(dx * dx + dz * dz);
            if (!(length > 0)) continue;
            var inv = 1f / length;
            var direction = new Vector3(dx * inv, 0, dz * inv);
            var wall = SaunionWallDistance(from - Center, direction);
            var to = from + MathF.Min(30, wall - Math.Clamp(viewer.Actor.HitboxRadius - .499f, .5f - .499f, viewer.Actor.HitboxRadius - .499f)) * direction;
            yield return (cast, from, to);
            from = to;
        }
    }

    private static float SaunionWallDistance(Vector3 offset, Vector3 direction)
    {
        // Keep the source AABB slab arithmetic and NaN operand order at
        // axis-parallel rays and exact square edges.
        var invX = 1f / direction.X;
        var invZ = 1f / direction.Z;
        var tx1 = (-26.5f - offset.X) * invX;
        var tx2 = (26.5f - offset.X) * invX;
        var tz1 = (-26.5f - offset.Z) * invZ;
        var tz2 = (26.5f - offset.Z) * invZ;
        static float Min(float x, float y) => x < y ? x : y;
        static float Max(float x, float y) => x > y ? x : y;
        var min = -float.Epsilon;
        var max = float.MaxValue;
        min = Min(Max(tx1, min), Max(tx2, min));
        max = Max(Min(tx1, max), Min(tx2, max));
        min = Min(Max(tz1, min), Max(tz2, min));
        max = Max(Min(tz1, max), Min(tz2, max));
        return min > max ? float.MaxValue : min >= 0 ? min : max >= 0 ? max : float.MaxValue;
    }
}
