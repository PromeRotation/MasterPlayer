using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsBlackRider => territory == HeavenswardIntroCatalog.BLACK_RIDER;
    private long blackRiderGeneration;
    internal IReadOnlyList<PalaceBlackRiderCharge> BlackRiderCharges => IsBlackRider
        ? palaceGuideLoaded?.BlackRiderCharges ?? [] : [];
    internal PalaceBlackRiderKnockback? BlackRiderKnockback => IsBlackRider ? palaceGuideLoaded?.BlackRiderKnockback : null;

    private void BlackRiderRestoreCasts()
    {
        if (!IsBlackRider || Active == null || palaceGuideLoaded is not { Active: true } module) return;
        // Output resets do not reconstruct a surviving source component. Restore
        // its input receipt before observing the next real cast change/finish.
        foreach (var charge in module.BlackRiderCharges) _casts.TryAdd(charge.Cast.Source, charge.Cast);
        if (module.BlackRiderKnockback is { } kb) _casts.TryAdd(kb.Cast.Source, kb.Cast);
        foreach (var (source, observation) in module.BlackRiderObservedCasts)
            treasureObservedCasts.TryAdd(source, observation);
    }

    private void BlackRiderCast(DungeonPendingCast cast, bool correction, bool firstRaw, bool raw)
    {
        if (!IsBlackRider || cast.Rule.Action != 7089 || palaceGuideLoaded is not { Active: true } module
            || !palaceGuideActors.TryGetValue(cast.Source, out var source)) return;
        if (raw && treasureObservedCasts.TryGetValue(cast.Source, out var observation))
            module.BlackRiderObservedCasts[cast.Source] = observation;
        var existing = module.BlackRiderInputs.TryGetValue(cast.Source, out var generation);
        if (!correction || !existing)
        {
            generation = ++blackRiderGeneration;
            module.BlackRiderInputs[cast.Source] = generation;
        }
        var index = module.BlackRiderCharges.FindIndex(c => c.Generation == generation);
        if (firstRaw || !correction || !existing)
        {
            if (palaceGuideActors.TryGetValue(cast.TargetId, out var target))
            {
                var charge = new PalaceBlackRiderCharge(generation, source, target, cast);
                if (index < 0) module.BlackRiderCharges.Add(charge);
                else module.BlackRiderCharges[index] = charge;
            }
            else if (index >= 0) module.BlackRiderCharges.RemoveAt(index);
            module.BlackRiderKnockback = new(generation, source, cast,
                cast.SourcePosition ?? source.Actor.Position,
                raw ? module.BlackRiderEventRoster.GetValueOrDefault(cast.TargetId, -1) : PalaceEarlyPairSlot(cast.TargetId));
        }
        else
        {
            if (index >= 0) module.BlackRiderCharges[index] = module.BlackRiderCharges[index] with { Cast = cast };
            if (module.BlackRiderKnockback?.Generation == generation)
                module.BlackRiderKnockback = module.BlackRiderKnockback with { Cast = cast };
        }
    }

    private void BlackRiderFinish(DungeonPendingCast cast)
    {
        if (!IsBlackRider || cast.Rule.Action != 7089 || palaceGuideLoaded is not { } module) return;
        var index = module.BlackRiderCharges.FindIndex(c => c.Cast.Source == cast.Source);
        if (index >= 0) module.BlackRiderCharges.RemoveAt(index);
        module.BlackRiderInputs.Remove(cast.Source);
        module.BlackRiderObservedCasts.Remove(cast.Source);
        module.BlackRiderKnockback = null;
    }

    internal Vector3 BlackRiderLanding(DungeonActor actor)
    {
        if (BlackRiderKnockback is not { TargetSlot: >= 0 } kb || palaceGuideLoaded == null || Arena == null
            || PalaceEarlyPairSlot(actor.Id) != kb.TargetSlot
            || palaceGuideLoaded.BlackRiderImmunity.ImmuneAt(kb.TargetSlot, kb.Cast.FinishAt)) return actor.Position;
        var delta = actor.Position - kb.Origin;
        delta.Y = 0;
        if (delta.LengthSquared() == 0) return actor.Position;
        var direction = Vector3.Normalize(delta);
        var inset = MathF.Max(.5f - .499f, actor.HitboxRadius - .499f);
        var distance = MathF.Min(kb.Distance, QuestWindRay(actor.Position, direction) - inset);
        return actor.Position + direction * distance;
    }

    private IEnumerable<DungeonAmbientAoe> BlackRiderAoes()
    {
        if (!IsBlackRider || Active == null) yield break;
        foreach (var charge in BlackRiderCharges.Where(c => !c.Source.Actor.Dead))
        {
            var shape = RayRectangleTemplate.Symmetric(charge.HalfWidth * 2, charge.Length);
            var origin = charge.Source.Actor.Position;
            var prefix = $"blackrider/charge/{charge.Generation}";
            var targetIsViewer = charge.Target.Actor.Id == PlayerId;
            yield return new(prefix + "/display", "Valfodr", shape, origin, charge.Rotation,
                Navigate: false, OutlineOnly: targetIsViewer);
            var currentTarget = !charge.Target.Removed && palaceGuideActors.TryGetValue(PlayerId, out var current)
                && ReferenceEquals(current, charge.Target);
            if (!currentTarget)
                yield return new(prefix + "/avoid", "Valfodr", shape, origin, charge.Rotation,
                    Display: false, Activation: charge.Cast.FinishAt);
            else if (!ReferenceEquals(charge.Source, charge.Target))
                foreach (var member in _actors.Values.Where(a => a.PartySlot is >= 0 and < 64 && !a.Dead && a.Id != PlayerId))
                {
                    var offset = member.Position - origin;
                    var halfAngle = MathF.Asin(3 / new Vector2(offset.X, offset.Z).Length());
                    // The source union ignores its NaN cone terms; do not clamp them into new hazards.
                    if (!float.IsFinite(halfAngle)) continue;
                    yield return new($"{prefix}/party/{member.Id}", "Bait away from raid", new FanTemplate(100, 2 * halfAngle),
                        origin, MathF.Atan2(offset.X, offset.Z), Display: false, Activation: charge.Cast.FinishAt);
                }
        }
        foreach (var source in BlackRiderCleaveSources)
        {
            var target = _actors[source.TargetId];
            var angle = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            var origin = QuantizedDungeonPosition(source.Position);
            var prefix = $"blackrider/cleave/{source.Id}";
            yield return new(prefix + "/display", "Cleave", new FanTemplate(11.92f, MathF.PI / 2), origin, angle,
                Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new(prefix + "/avoid", "Cleave", new FanTemplate(11.92f, MathF.PI / 2),
                    origin, angle, Display: false);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot is >= 0 and < 8 && !a.Dead && a.Id != PlayerId))
                    yield return new($"{prefix}/party/{member.Id}", "Do not cleave party", new FanTemplate(100, MathF.PI / 2),
                        origin, MathF.Atan2(member.Position.X - source.Position.X, member.Position.Z - source.Position.Z), Display: false);
        }
        if (BlackRiderKnockback is not { TargetSlot: >= 0 } kb || PalaceEarlyPairSlot(PlayerId) != kb.TargetSlot) yield break;
        var boss = palaceGuideLoaded!.Primary.Actor;
        foreach (var danger in _actors.Values.Where(a => a.Oid == 0x1E858E).Select(a => (a.Position, Radius: 9f, Key: $"pool/{a.Id}"))
            .Concat(_casts.Values.Where(c => c.Rule.Action == 7157).Select(c => (c.Location, Radius: 7f, Key: $"infatuation/{c.Source}"))))
        {
            var offset = danger.Item1 - boss.Position;
            var halfAngle = MathF.Asin(danger.Radius / new Vector2(offset.X, offset.Z).Length());
            if (!float.IsFinite(halfAngle)) continue;
            yield return new($"blackrider/kb/avoid/{danger.Key}", "Avoid knockback into danger", new FanTemplate(100, 2 * halfAngle),
                boss.Position, MathF.Atan2(offset.X, offset.Z), Display: false, Activation: kb.Cast.FinishAt);
        }
        // These directional constraints remain above the immunity gate, as in ValfodrKB.AddAIHints.
        if (_actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;
        foreach (var aoe in QuestKnockbackProjection("blackrider/kb", viewer, viewer.Position, BlackRiderLanding(viewer)))
            yield return aoe;
    }

    private IEnumerable<DungeonActor> BlackRiderCleaveSources => IsBlackRider && Active != null
        ? _actors.Values.Where(a => a.Oid == 0x1814 && !a.Dead && a.Targetable && a.Cast == null
            && !_casts.ContainsKey(a.Id) && _actors.ContainsKey(a.TargetId)) : [];

    private IEnumerable<string> BlackRiderHints(Vector3? position)
    {
        if (!IsBlackRider || Active == null || position is not { } player) yield break;
        foreach (var charge in BlackRiderCharges.Where(c => !c.Source.Actor.Dead))
        {
            var shape = RayRectangleTemplate.Symmetric(6, charge.Length).Resolve(charge.Source.Actor.Position, charge.Rotation, null);
            if (charge.Target.Actor.Id == PlayerId && _actors.Values.Any(a => a.PartySlot is >= 0 and < 64
                && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position))) yield return "Bait away from raid!";
            if (charge.Target.Actor.Id != PlayerId && shape.Contains(player)) yield return "GTFO from baited aoe!";
        }
        foreach (var source in BlackRiderCleaveSources.Where(a => a.TargetId != PlayerId))
        {
            var target = _actors[source.TargetId];
            var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            if (new FanTemplate(11.92f, MathF.PI / 2).Resolve(source.Position, rotation, null).Contains(QuantizedDungeonPosition(player)))
                yield return "GTFO from cleave!";
        }
        if (TrialVoidzoneAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(QuantizedDungeonPosition(player))))
            yield return "GTFO from aoe!";
        if (_actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;
        var landing = BlackRiderLanding(viewer);
        if (landing != viewer.Position && (Arena?.Contains(landing) == false
            || _casts.Values.Where(c => c.Rule.Action == 7157).Any(c => c.Rule.Shape!.Resolve(c.Location, c.Rotation, null).Contains(landing))
            || TrialVoidzoneAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(landing))))
            yield return "About to be knocked into danger!";
    }

    private IEnumerable<DungeonDamagePrediction> BlackRiderDamage()
    {
        if (!IsBlackRider || Active == null || BlackRiderCharges.Count == 0) yield break;
        foreach (var target in BlackRiderCharges.Where(c => !c.Source.Actor.Dead).Select(c => c.Target.Actor).DistinctBy(a => a.Id))
        {
            var slot = PalaceEarlyPairSlot(target.Id);
            if (slot is >= 0 and < 64)
                yield return new(target.Id, slot, target.PartyRole, target.Name, BlackRiderCharges[0].Cast.FinishAt, "Tankbuster");
        }
    }
}
