using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private const string CarnivaleGoldenBeamHint = "Use Diamondback outside of marked area!";
    private long carnivaleGoldorGeneration;
    private readonly Dictionary<ulong, int> carnivaleMarionetteCounts = [];
    private DungeonPendingCast[]? carnivaleBeamSnapshot;
    private GeometryTemplate? carnivaleBeamGeometry;
    private static bool CarnivaleGoldorAction(uint action) => action is 34462 or 34448 or 34449 or 34450 or 34463
        or 34446 or 34457 or 34458 or 34459 or 34460 or 34451 or 34453 or 34455 or 34589 or 34465 or 34464
        or 34466 or 34468 or 34467;
    internal CarnivaleGoldorState? CarnivaleGoldor => IsCarnivale && Active?.BossOid is 0x3FA5 or 0x3FA7
        ? carnivaleSelected?.Goldor : null;

    private void CarnivaleGoldorStarted(DungeonPendingCast cast, bool correction)
    {
        if (!IsCarnivale || !CarnivaleGoldorAction(cast.Rule.Action)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Goldor is { NeedsBootstrap: false }))
            CarnivaleGoldorStarted(module, cast, correction);
    }

    private void CarnivaleGoldorStarted(CarnivaleModuleState module, DungeonPendingCast cast, bool correction, bool bootstrap = false)
    {
        var state = module.Goldor!;
        if (module.Primary.Actor.Oid == 0x3FA5 && cast.Rule.Action == 34463
            && carnivaleActors.TryGetValue(cast.Source, out var ruptureCaster))
        {
            var index = correction ? state.Ruptures.FindIndex(c => ReferenceEquals(c.Caster, ruptureCaster)) : -1;
            if (index >= 0) state.Ruptures[index] = (ruptureCaster, cast);
            else if (!correction) state.Ruptures.Add((ruptureCaster, cast));
        }
        if (cast.Rule.Action == 34457)
        {
            var index = correction && state.QuakeStarts.TryGetValue(cast.Source, out var generation)
                ? state.Quakes.FindIndex(q => q.Generation == generation && q.Order == 0) : -1;
            if (!correction || index >= 0)
            {
                var sequence = new CarnivaleQuakeSequence(index >= 0 ? state.Quakes[index].Generation : ++carnivaleGoldorGeneration,
                    cast, cast.Location, cast.FinishAt);
                if (index >= 0) state.Quakes[index] = sequence;
                else state.Quakes.Add(sequence);
                state.QuakeStarts[cast.Source] = sequence.Generation;
            }
        }
        if (cast.Rule.Action == 34460 || module.Primary.Actor.Oid == 0x3FA7 && cast.Rule.Action is 34468 or 34466)
        {
            var index = correction ? module.CastKnockbacks.FindIndex(k => k.Cast.Source == cast.Source && k.Cast.Rule.Action == cast.Rule.Action) : -1;
            var value = new CarnivaleKnockbackCast(index >= 0 ? module.CastKnockbacks[index].Generation : ++carnivaleGoldorGeneration,
                cast, cast.Rule.Action == 34466 ? 30 : 10, false, null, Pull: cast.Rule.Action == 34466);
            if (index >= 0) module.CastKnockbacks[index] = value;
            else module.CastKnockbacks.Add(value);
        }
        if (correction) return;
        if (cast.Rule.Action == 34451) state.Gravity = cast;
        if (cast.Rule.Action == 34453) state.Thunder = cast;
        if (module.Primary.Actor.Oid == 0x3FA7 && cast.Rule.Action == 34464)
        {
            var count = !bootstrap && cast.Observed && carnivaleMarionetteCounts.TryGetValue(cast.Source, out var observed)
                ? observed : CarnivaleEnemies(0x3FA8).Count();
            if (count == 1) state.BeamOutput = [cast];
            else
            {
                state.BeamInputs.Add(cast);
                if (state.BeamInputs.Count == 4) state.BeamOutput = state.BeamInputs.ToArray();
            }
        }
    }

    private void CarnivaleGoldorFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Goldor is { NeedsBootstrap: false }))
        {
            var state = module.Goldor!;
            state.Ruptures.RemoveAll(c => ReferenceEquals(c.Cast, cast));
            if (cast.Rule.Action == 34457) state.QuakeStarts.Remove(cast.Source);
            var order = cast.Rule.Action switch { 34457 => 0, 34458 => 1, 34459 => 2, _ => -1 };
            if (order >= 0)
            {
                var index = state.Quakes.FindIndex(q => q.Order == order && MathF.Abs(q.Origin.X - cast.Location.X) <= 1
                    && MathF.Abs(q.Origin.Z - cast.Location.Z) <= 1);
                if (index >= 0)
                {
                    if (order == 2) state.Quakes.RemoveAt(index);
                    else state.Quakes[index] = state.Quakes[index] with { Order = order + 1, Activation = _now.AddSeconds(1.5d) };
                }
            }
            if (cast.Rule.Action == 34464)
            {
                state.BeamOutput = [];
                state.BeamInputs.Clear();
            }
        }
    }

    private void CarnivaleGoldorEffect(ulong source, uint action)
    {
        if (!IsCarnivale || action is not (34452 or 34454) || !_actors.ContainsKey(source)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Goldor != null))
        {
            if (action == 34452) module.Goldor!.Gravity = null;
            else module.Goldor!.Thunder = null;
        }
    }

    private void CarnivaleGoldorUpdate()
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Goldor is { NeedsBootstrap: true }))
        {
            module.Goldor!.NeedsBootstrap = false;
            foreach (var cast in CarnivaleOrderedCasts)
            {
                CarnivaleGoldorStarted(module, cast, false, true);
                if (cast.Rule.Action == 34589 && carnivaleActors.TryGetValue(cast.Source, out var caster))
                    module.Interrupts.Add(new(++carnivaleInterruptGeneration, caster, _casts[cast.Source]));
            }
        }
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleGoldorAoes()
    {
        if (CarnivaleGoldor is not { } state) yield break;
        foreach (var quake in state.Quakes)
        {
            GeometryTemplate shape = quake.Order switch
            {
                0 => new CircleTemplate(10),
                1 => new DonutTemplate(10, 20),
                _ => new DonutTemplate(20, 30),
            };
            yield return new($"carnivale/goldor/quake/{quake.Generation}", "Goldor Quake", shape,
                quake.Origin, Activation: quake.Activation);
        }
        if (state.BeamOutput.Length > 0)
        {
            var cast = state.BeamOutput[^1];
            if (!ReferenceEquals(carnivaleBeamSnapshot, state.BeamOutput))
            {
                carnivaleBeamSnapshot = state.BeamOutput;
                carnivaleBeamGeometry = state.BeamOutput.Length == 1 ? new FanTemplate(40, MathF.PI * 2 / 3)
                    : new DungeonShapeTemplate(CarnivaleGoldenBeamGeometry.Create(new(100, cast.Location.Y, 100), state.BeamOutput));
            }
            yield return new("carnivale/goldor/beam", "Golden Beam", carnivaleBeamGeometry!,
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
        }
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        foreach (var action in new uint[] { 34460, 34468, 34466 })
            foreach (var movement in CarnivaleCastKnockbackMovements(player, action))
                foreach (var preview in QuestKnockbackProjection($"carnivale/goldor/kb/{movement.Source.Generation}", player, movement.From, movement.To))
                    yield return preview;
    }

    private IEnumerable<string> CarnivaleGoldorHints(Vector3 position)
    {
        if (CarnivaleGoldor is not { } state) yield break;
        if (state.Gravity != null) yield return "Dmg + Heavy debuff";
        if (state.Thunder != null) yield return "Prepare to cleanse Electrocution";
        if (state.Ruptures.Count > 0)
        {
            var remaining = 0f;
            if (state.Ruptures[0].Caster.Actor.Cast is { } observed)
            {
                var total = Math.Max(.05f, observed.TotalTime ?? (float)(observed.FinishAt - observed.StartedAt).TotalSeconds - .3f);
                var elapsed = Math.Clamp((float)(_now - observed.StartedAt).TotalSeconds, 0, total);
                remaining = total + .3f - elapsed;
            }
            yield return $"Kill slime ASAP! (The Ram's Voice + Ultravibration) {remaining:f1}s left";
        }
        if (CarnivaleGoldorAoes().Where(a => a.Navigate).Concat(CarnivaleSapAoes())
            .Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position, 0))
            || OutputCasts.Any(c => c.Value.Rule.Shape?.Resolve(c.Value.Location, c.Value.Rotation, null).Contains(position, 0) == true))
            yield return "GTFO from aoe!";
        if (Active?.BossOid == 0x3FA7 && carnivalePrimary is { } primary && CarnivaleHasStatus(primary, 3621))
            yield return $"{primary.Name} is immune to magic damage! (Destroy crystal to remove buff)";
        if (state.BeamOutput.Length > 0 && CarnivaleEnemies(0x3FA8).Count() > 1)
            yield return CarnivaleGoldenBeamHint;
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (CarnivaleHasStatus(player, 3779)) yield return "Cleanse Electrocution!";
        if (CarnivaleHasStatus(player, 240)) yield return "Use Loom to dodge AOEs!";
        if (Active?.BossOid == 0x3FA5 && CarnivaleEnemies(0x3FA6).Any(a => !a.Dead))
            yield return "Destroy at least one fireball to create a safe spot!";
        foreach (var action in new uint[] { 34460, 34468, 34466 })
            foreach (var movement in CarnivaleCastKnockbackMovements(player, action))
            {
                var hazard = Active?.BossOid == 0x3FA5 ? 34446u : 34465u;
                if (Arena?.Contains(movement.To, 0) == false || action == 34460
                    && OutputCasts.Any(c => c.Value.Rule.Action == hazard && c.Value.Rule.Shape!.Resolve(c.Value.Location,
                        c.Value.Rotation, null).Contains(movement.To, 0)))
                    yield return "About to be knocked into danger!";
            }
    }

    private IEnumerable<DungeonDamagePrediction> CarnivaleGoldorDamage()
    {
        if (CarnivaleGoldor is not { } state) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 34460 || Active?.BossOid == 0x3FA7 && c.Rule.Action == 34468))
        {
            var current = _actors.GetValueOrDefault(cast.Source)?.Cast;
            var activation = current?.Action == cast.Rule.Action ? current.FinishAt : cast.FinishAt;
            if (activation < _now.AddSeconds((double).3f)) activation = _now.AddSeconds((double).3f);
            foreach (var actor in _actors.Values.Where(a => !a.Dead && a.PartySlot is >= 0 and < 64))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, activation, "Raidwide");
        }
        var delay = Active?.BossOid == 0x3FA5 ? (double).8f : .8d;
        foreach (var cast in new[] { state.Gravity, state.Thunder }.OfType<DungeonPendingCast>())
            foreach (var actor in _actors.Values.Where(a => !a.Dead && a.PartySlot is >= 0 and < 64))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, cast.FinishAt.AddSeconds(delay), "Raidwide");
    }
}
