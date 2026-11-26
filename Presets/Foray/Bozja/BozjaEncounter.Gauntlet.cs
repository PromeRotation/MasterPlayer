using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsGauntlet => Definition.PrimaryOid == 0x32BF;
    internal int GauntletPhase { get; private set; }
    internal BozjaActor? GauntletAugur { get; private set; }
    internal BozjaActor? GauntletAlkonost { get; private set; }
    internal BozjaActor? GauntletCrow { get; private set; }
    internal List<BozjaActor> GauntletTerminusCasters { get; } = [];
    internal List<BozjaCast> GauntletTerminus { get; } = [];
    internal float GauntletCommonAngle { get; private set; }
    private readonly List<BozjaRule> gauntletRules = [];
    private readonly List<BozjaActor> gauntletUnseenCasters = [];
    private readonly Dictionary<ulong, float> gauntletWeakpoints = [];
    private readonly Dictionary<uint, List<BozjaActor>> gauntletEnemies = [];

    private void ClearGauntletComponents()
    {
        GauntletTerminusCasters.Clear();
        GauntletTerminus.Clear();
        GauntletCommonAngle = 0;
        gauntletUnseenCasters.Clear();
        gauntletWeakpoints.Clear();
        gauntletRules.Clear();
        Array.Clear(immunity);
    }

    private void ClearGauntlet()
    {
        GauntletPhase = 0;
        GauntletAugur = GauntletAlkonost = GauntletCrow = null;
        gauntletEnemies.Clear();
        ClearGauntletComponents();
    }

    private BozjaActor? GauntletEnemy(uint oid)
    {
        if (!gauntletEnemies.TryGetValue(oid, out var actors))
            gauntletEnemies.Add(oid, actors = Actors.Values.Where(a => a.Actor.Oid == oid).ToList());
        return actors.FirstOrDefault();
    }

    private void ActivateGauntletPhase()
    {
        var offset = GauntletPhase == 0 ? 0 : GauntletPhase == 1 ? 6 : 11;
        var count = GauntletPhase == 0 ? 6 : 5;
        foreach (var rule in Definition.Rules.Skip(offset).Take(count))
        {
            gauntletRules.Add(rule);
            // Activation is component-major, then Created/Cast/Tether/60 slots
            // per original actor. In particular Unseen follows all Terminus
            // Created callbacks; it must not see a partially collected ten.
            foreach (var actor in Actors.Values)
            {
                if (actor.NativeNpc)
                {
                    GauntletCreated(actor, rule);
                    if (rule.Kind == BozjaRuleKind.Tower) CreatedTower(actor, rule);
                    if (actor.Cast != null) Start(actor, rule);
                }
                if (rule.Kind == BozjaRuleKind.GauntletStorm && actor.ObservedTether.Id != 0)
                    GauntletTethered(actor, actor.ObservedTether.Id);
                if (rule.Kind is BozjaRuleKind.GauntletUnseen or BozjaRuleKind.GauntletTurbine or BozjaRuleKind.GauntletWind)
                    foreach (var status in actor.Statuses.OfType<ReplayStatus>())
                    {
                        if (rule.Kind == BozjaRuleKind.GauntletUnseen) GauntletStatus(actor, status, true);
                        else ImmuneStatus(actor, status, true);
                    }
            }
        }
    }

    private void UpdateGauntlet()
    {
        if (!IsGauntlet) return;
        bool Transition() => GauntletPhase switch
        {
            0 => Primary!.Destroyed || GauntletAugur?.Actor.Targetable == true,
            1 => GauntletAugur == null || GauntletAugur.Destroyed || GauntletAlkonost?.Actor.Targetable == true,
            _ => (GauntletAlkonost == null || GauntletAlkonost.Destroyed || GauntletAlkonost.Actor.Dead)
                && (GauntletCrow == null || GauntletCrow.Destroyed || GauntletCrow.Actor.Dead),
        };
        while (Transition())
        {
            ++GauntletPhase;
            Casts.Clear();
            ClearGauntletComponents();
            if (GauntletPhase == 3)
            {
                Active = false;
                Completed = true;
                return;
            }
            ActivateGauntletPhase();
        }
        // Source UpdateModule runs after StateMachine.Update. References and
        // first-query enemy lists stay original through ID reuse/destruction.
        GauntletAugur ??= GauntletEnemy(0x32C8);
        if (GauntletPhase >= 1)
        {
            GauntletAlkonost ??= GauntletEnemy(0x32CB);
            GauntletCrow ??= GauntletEnemy(0x32CA);
        }
        Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.GauntletDonut && c.Target!.Actor.Dead);
    }

    private void GauntletCreated(BozjaActor actor)
    {
        if (!IsGauntlet) return;
        if (gauntletEnemies.TryGetValue(actor.Actor.Oid, out var enemies)) enemies.Add(actor);
        if (Active && actor.NativeNpc)
            foreach (var rule in gauntletRules) GauntletCreated(actor, rule);
    }

    private void GauntletCreated(BozjaActor actor, BozjaRule rule)
    {
        if (rule.Kind != BozjaRuleKind.GauntletTerminus || actor.Actor.Oid != 0x32C1) return;
        GauntletTerminusCasters.Add(actor);
        if (GauntletTerminusCasters.Count != 10) return;
        var angleA = GauntletTerminusCasters[0].Actor.Rotation;
        var angleB = 0f;
        var countA = 1;
        var countB = 0;
        foreach (var caster in GauntletTerminusCasters.Skip(1))
        {
            var angle = caster.Actor.Rotation;
            if (angle == angleA) ++countA;
            else if (countB == 0) { angleB = angle; countB = 1; }
            else if (angle == angleB) ++countB;
        }
        GauntletCommonAngle = countA > countB ? angleA : angleB;
        var leastCommon = countA > countB ? angleB : angleA;
        foreach (var caster in GauntletTerminusCasters.Where(c => c.Actor.Rotation == leastCommon))
            GauntletTerminus.Add(new(++serial, rule, caster,
                DungeonEncounter.QuantizedDungeonPosition(caster.Actor.Position), caster.Actor.Rotation, Now.AddSeconds(10d)));
    }

    private void StartGauntlet(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind is BozjaRuleKind.GauntletTerminus or BozjaRuleKind.GauntletUnseen or BozjaRuleKind.GauntletDonut) return;
        if (rule.Kind == BozjaRuleKind.GauntletArena)
        {
            if (Arena is RectangleGeometry { Width: > 46 })
            {
                Casts.RemoveAll(c => c.Rule == rule);
                Casts.Add(new(++serial, rule, actor, Center, 0, Activation(actor, 1.5d)));
            }
            return;
        }
        if (rule.Kind == BozjaRuleKind.GauntletStorm)
        {
            var index = Casts.FindIndex(c => c.Rule == rule && c.Source.Actor.Id == actor.Actor.Id);
            if (index >= 0) Casts[index] = new(++serial, rule, actor, spell.Location, 0, Activation(actor));
            return;
        }
        if (rule.Kind == BozjaRuleKind.GauntletWind) Casts.RemoveAll(c => c.Rule == rule);
        var extra = rule.Kind == BozjaRuleKind.GauntletWind ? .1d : 0;
        var cast = new BozjaCast(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor, extra));
        if (rule.Kind == BozjaRuleKind.GauntletPain)
            cast = cast with
            {
                ShapeOverride = spell.ActionId switch
                {
                    24362 or 24365 => new DonutTemplate(8, 25),
                    24363 or 24366 => new FanTemplate(35, 130f * (MathF.PI / 180f)),
                    _ => new CircleTemplate(20),
                },
            };
        Casts.Add(cast);
    }

    private void FinishGauntlet(BozjaActor actor, ReplayCast spell, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.GauntletTerminus && spell.ActionId == 24327) GauntletTerminus.Clear();
        if (rule.Kind == BozjaRuleKind.GauntletUnseen && spell.ActionId == 24327) gauntletUnseenCasters.Remove(actor);
        if (rule.Kind == BozjaRuleKind.GauntletStorm && spell.ActionId == 24359)
        {
            var index = Casts.FindIndex(c => c.Rule == rule);
            if (index >= 0) Casts.RemoveAt(index);
        }
        if (rule.Kind == BozjaRuleKind.GauntletPain
            || rule.Kind == BozjaRuleKind.GauntletTurbine && rule.Actions.Contains(spell.ActionId))
        {
            var index = Casts.FindIndex(c => c.Rule == rule && c.Source.Actor.Id == actor.Actor.Id);
            if (index >= 0) Casts.RemoveAt(index);
        }
    }

    private void GauntletIcon(BozjaActor actor, uint icon)
    {
        if (!Active || !IsGauntlet) return;
        var kind = icon == 261 ? BozjaRuleKind.GauntletImpact : BozjaRuleKind.GauntletDonut;
        if (icon is not (261 or 288) || gauntletRules.FirstOrDefault(r => r.Kind == kind) is not { } rule) return;
        Casts.Add(new(++serial, rule, actor, actor.Actor.Position, 0, Now.AddSeconds(icon == 261 ? 12d : 9d)) { Target = actor });
    }

    private void GauntletTethered(BozjaActor actor, uint id)
    {
        if (!Active || !IsGauntlet || id != 4
            || gauntletRules.FirstOrDefault(r => r.Kind == BozjaRuleKind.GauntletStorm) is not { } rule) return;
        var (sin, cos) = Math.SinCos(actor.Actor.Rotation);
        var position = DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position + 48 * new Vector3((float)sin, 0, (float)cos));
        Casts.Add(new(++serial, rule, actor, position, 0, Now.AddSeconds(actor.Actor.Oid == 0x32CC ? 20.9d : 13.7d)));
        var indices = Casts.Select((c, i) => (c, i)).Where(c => c.c.Rule == rule).Take(2).Select(c => c.i).ToArray();
        if (indices.Length == 2 && Casts[indices[0]].Activation > Casts[indices[1]].Activation)
            (Casts[indices[0]], Casts[indices[1]]) = (Casts[indices[1]], Casts[indices[0]]);
    }

    private void GauntletEffect(uint action, ulong target)
    {
        if (!IsGauntlet) return;
        if (action == 24334)
        {
            var index = Casts.FindIndex(c => c.Rule.Kind == BozjaRuleKind.GauntletImpact);
            if (index >= 0) Casts.RemoveAt(index);
        }
        if (action == 24343)
        {
            var index = Casts.FindIndex(c => c.Rule.Kind == BozjaRuleKind.GauntletDonut && c.Target!.Actor.Id == target);
            if (index >= 0) Casts.RemoveAt(index);
            else Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.GauntletDonut);
        }
        if (action is 24814 or 24815) Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.GauntletWind);
    }

    private void GauntletStatus(BozjaActor actor, ReplayStatus status, bool gain)
    {
        if (!IsGauntlet || !gauntletRules.Any(r => r.Kind == BozjaRuleKind.GauntletUnseen)
            || status.Id is not (1707 or 1708 or 1709)) return;
        if (!gain) { gauntletWeakpoints.Remove(actor.Actor.Id); return; }
        gauntletWeakpoints[actor.Actor.Id] = status.Id switch { 1708 => MathF.PI / 2, 1709 => MathF.PI, _ => 270f * (MathF.PI / 180f) };
        if (gauntletUnseenCasters.Count == 0)
            gauntletUnseenCasters.AddRange(GauntletTerminusCasters.Where(c => c.Actor.Rotation == GauntletCommonAngle));
    }

    internal IEnumerable<BozjaEye> GauntletEyes(BozjaActor viewer)
    {
        if (!Active || !IsGauntlet || !gauntletWeakpoints.TryGetValue(viewer.Actor.Id, out var forward)) yield break;
        BozjaActor? selected = null;
        var remaining = float.MaxValue;
        var rule = Definition.Rules.Single(r => r.Kind == BozjaRuleKind.GauntletUnseen);
        foreach (var caster in gauntletUnseenCasters)
        {
            var raw = caster.RawCast;
            var cast = new BozjaCast(0, rule, caster, caster.Actor.Position, raw?.Rotation ?? caster.Actor.Rotation, default);
            var next = raw == null ? 6.5f : BurnRawTotal(raw) - caster.RawElapsed;
            if (!cast.Contains(viewer.Actor.Position) || !(next < remaining)) continue;
            selected = caster;
            remaining = next;
        }
        if (selected != null)
        {
            var raw = selected.RawCast;
            yield return new(-300000 - selected.BirthOrder, selected.Actor.Position,
                raw == null ? default : Now.AddSeconds(BurnRawTotal(raw) + .3f - selected.RawElapsed),
                true, Forward: forward, Precise: true);
        }
    }

    private IEnumerable<BozjaAoe> GauntletAoes(BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.GauntletTerminus)
            foreach (var cast in GauntletTerminus) yield return new(cast, false, true);
        if (rule.Kind is BozjaRuleKind.GauntletArena or BozjaRuleKind.GauntletImpact or BozjaRuleKind.GauntletStorm or BozjaRuleKind.GauntletPain)
            foreach (var cast in Casts.Where(c => c.Rule == rule).Take(rule.Kind == BozjaRuleKind.GauntletImpact ? 2
                : rule.Kind == BozjaRuleKind.GauntletStorm ? 1 : int.MaxValue))
                yield return new(cast, false, true);
    }

    internal IEnumerable<BozjaCast> GauntletDonuts
        => Active && IsGauntlet ? Casts.Where(c => c.Rule.Kind == BozjaRuleKind.GauntletDonut)
            .Select(c => c with { Origin = c.Target!.Actor.Position, ShapeOverride = new DonutTemplate(4, 8) }) : [];

    private IEnumerable<BozjaZone> GauntletNavigation(BozjaActor viewer, BozjaRule rule)
    {
        if (!IsGauntlet) yield break;
        var wind = Casts.FirstOrDefault(c => c.Rule.Kind == BozjaRuleKind.GauntletWind);
        var displaced = wind != null && !WindImmune(viewer, wind.Activation);
        if (rule.Kind is BozjaRuleKind.GauntletStorm or BozjaRuleKind.GauntletPain && displaced) yield break;
        if (rule.Kind is BozjaRuleKind.GauntletArena or BozjaRuleKind.GauntletImpact or BozjaRuleKind.GauntletTerminus
            or BozjaRuleKind.GauntletStorm or BozjaRuleKind.GauntletPain)
            foreach (var aoe in GauntletAoes(rule))
                yield return new($"gauntlet/aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                    aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
        if (rule.Kind == BozjaRuleKind.GauntletDonut)
        {
            var stacks = Casts.Where(c => c.Rule == rule).ToArray();
            var join = stacks.Where(s => !ReferenceEquals(s.Target, viewer)).Select(s => s.Target!.Actor.Position).ToArray();
            if (join.Length != 0)
                yield return new($"gauntlet/donut/{stacks[0].Generation}", rule.Component, stacks[0].Activation,
                    null, default, 0, join, JoinRadius: 1);
        }
        if (rule.Kind is not (BozjaRuleKind.GauntletTurbine or BozjaRuleKind.GauntletWind)) yield break;
        var kb = Casts.FirstOrDefault(c => c.Rule == rule);
        if (kb == null || WindImmune(viewer, kb.Activation)) yield break;
        var aoes = rule.Kind == BozjaRuleKind.GauntletTurbine
            ? Casts.Where(c => c.Rule.Component == "FlamingCyclone").ToArray()
            : Casts.Where(c => c.Rule.Kind == BozjaRuleKind.GauntletPain).ToArray();
        if (rule.Kind == BozjaRuleKind.GauntletWind && aoes.Length == 0)
            aoes = Casts.Where(c => c.Rule.Kind == BozjaRuleKind.GauntletStorm).Take(1).ToArray();
        yield return new($"gauntlet/kb/{kb.Generation}", rule.Component, kb.Activation,
            new BozjaGauntletWindTemplate(Center, kb.Origin, kb.Rotation, rule.Kind == BozjaRuleKind.GauntletTurbine, aoes), Center, 0);
    }

    internal IEnumerable<(BozjaCast Cast, Vector3 From, Vector3 To)> GauntletMovements(BozjaActor viewer, BozjaRule rule)
    {
        if (!Active || !IsGauntlet) yield break;
        var from = viewer.Actor.Position;
        foreach (var kb in Casts.Where(c => c.Rule == rule))
        {
            if (WindImmune(viewer, kb.Activation)) continue;
            Vector3 direction;
            var distance = 40f;
            if (rule.Kind == BozjaRuleKind.GauntletTurbine)
            {
                var delta = from - kb.Origin;
                if (delta.X == 0 && delta.Z == 0) continue;
                var inverse = 1f / MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
                direction = new(delta.X * inverse, 0, delta.Z * inverse);
                distance = 15f;
            }
            else
            {
                var (sin, cos) = Math.SinCos(kb.Rotation);
                direction = new((float)sin, 0, (float)cos);
            }
            yield return (kb, from, from + distance * direction);
            yield break;
        }
    }

    private bool GauntletDestinationUnsafe(Vector3 point, BozjaRule rule)
    {
        var dangers = rule.Kind == BozjaRuleKind.GauntletTurbine
            ? Casts.Where(c => c.Rule.Component == "FlamingCyclone")
            : Casts.Where(c => c.Rule.Kind == BozjaRuleKind.GauntletPain)
                .Concat(Casts.Where(c => c.Rule.Kind == BozjaRuleKind.GauntletStorm).Take(1));
        return dangers.Any(c => c.Contains(point)) || !Arena.Contains(point, 0);
    }

    private IEnumerable<BozjaHint> GauntletHints(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.GauntletUnseen && GauntletEyes(viewer).Any(e => EyeDanger(e, viewer.Actor)))
            yield return new("Face open weakpoint to eye!");
        if (rule.Kind == BozjaRuleKind.GauntletDonut)
            foreach (var hint in StackHints(viewer, rule)) yield return hint;
        if (rule.Kind is BozjaRuleKind.GauntletTurbine or BozjaRuleKind.GauntletWind
            && GauntletMovements(viewer, rule).Any(m => GauntletDestinationUnsafe(m.To, rule)))
            yield return new("About to be knocked into danger!");
    }
}
