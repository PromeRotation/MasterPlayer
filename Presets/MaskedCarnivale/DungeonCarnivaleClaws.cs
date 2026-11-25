using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long carnivaleVoidzoneGeneration;
    private IEnumerable<DungeonActor> CarnivaleEnemies(uint oid) =>
        carnivaleActorOrder.Where(a => a.Actor.Oid == oid).Select(a => a.Actor);
    private IEnumerable<DungeonPendingCast> CarnivaleOrderedCasts =>
        carnivaleActorOrder.Select(a => _casts.GetValueOrDefault(a.Actor.Id)).OfType<DungeonPendingCast>()
            .Select(CarnivaleActivationCast);
    private DungeonPendingCast CarnivaleActivationCast(DungeonPendingCast cast)
    {
        // These actions have ExtraCastTime100ms=0 in the client sheet. The source
        // caps elapsed at TotalTime, retaining NPCFinishDelay after that cap.
        // A newly activated component locks its own clock; existing records keep
        // the original Start deadline even while raw clock fields drift.
        if (cast.Rule.Action is not (14691 or 14692 or 14693 or 14694 or 14710 or 14265 or 14363 or 14365 or 14364 or 15057 or 15048 or 15076 or 14873 or 15055 or 14704 or 14748 or 14706 or 14747 or 15081 or 14906 or 14907 or 15325 or 14771 or 19266 or 19055 or 18689 or 18961 or 18965 or 18691 or 18962 or 18963 or 18840 or 18842 or 23117 or 23126 or 23119 or 34457 or 34460 or 34451 or 34453 or 34464 or 34468 or 34466)) return cast;
        var earliest = _now.AddSeconds((double).3f);
        return cast.FinishAt < earliest ? cast with { FinishAt = earliest } : cast;
    }
    internal bool UsesCarnivaleNativeEffect(uint action) => IsCarnivale && action is 14364 or 15057 or 15048 or 15076 or 15081 or 14771 or 19259 or 19256 or 19257 or 18974 or 18973 or 18840 or 18986 or 23119 or 23127 or 34452 or 34454;
    private static bool CarnivaleEarlyAction(uint action) => action is 14265 or 14363 or 14364 or 14365 or 15057;
    private bool CarnivaleEarlyBootstrap(uint action) => CarnivaleEarlyAction(action)
        && carnivaleLoaded.Any(m => m.Started && (m.PuddingNeedsBootstrap || m.VoidzoneNeedsBootstrap)
            && Definitions.Single(d => d.BossOid == m.Primary.Actor.Oid).Casts.Any(c => c.Action == action));
    private static bool CarnivaleClawAction(uint action) => action is 14759 or 14760 or 15047 or 15048 or 15049;
    private static uint CarnivaleVoidzoneAction(CarnivaleModuleState module) =>
        module.Primary.Actor.Oid switch { 0x25D4 => 14364, 0x242D => 15057, 0x2721 => 15048, 0x2727 or 0x2728 => 15076, 0x2730 => 15081, 0x2680 => 14771, 0x2C6A or 0x2C6C => 18840, 0x30F7 => 23119, _ => 0 };
    private static uint CarnivaleVoidzoneOid(CarnivaleModuleState module) =>
        module.Primary.Actor.Oid switch { 0x25D4 => 0x1E8FEA, 0x242D => 0x1E9C9D, 0x2721 => 0x1E8D9B, 0x2730 => 0x1E972A, 0x2680 => 0x1EA8BB, 0x2C6A or 0x2C6C => 0x1E8D9B, 0x30F7 => 0x1E9684, _ => 0x1EA9F9 };
    private static double CarnivaleVoidzoneDelay(CarnivaleModuleState module) =>
        module.Primary.Actor.Oid switch { 0x25D4 => (double)1.2f, 0x242D => 1d, 0x2721 => (double)1.1f, 0x2727 => (double)1.6f, 0x2730 => .9d, 0x2680 => 1.2d, 0x2C6A or 0x2C6C or 0x30F7 => .8d, _ => 1.6d };
    internal IEnumerable<CarnivaleVoidzoneCast> CarnivaleRayCasts => IsCarnivale && Active?.BossOid is 0x25D4 or 0x242D or 0x2721 or 0x2727 or 0x2728 or 0x2730 or 0x2680 or 0x2C6A or 0x2C6C or 0x30F7
        ? carnivaleSelected!.RayCasts : [];
    internal IEnumerable<CarnivaleVoidzonePrediction> CarnivaleRayPredictions => IsCarnivale && Active?.BossOid is 0x25D4 or 0x242D or 0x2721 or 0x2727 or 0x2728 or 0x2730 or 0x2680 or 0x2C6A or 0x2C6C or 0x30F7
        ? carnivaleSelected!.RayPredictions : [];

    private void CarnivaleClawStarted(DungeonPendingCast cast, bool correction)
    {
        if (!IsCarnivale || !UsesCarnivaleNativeEffect(cast.Rule.Action)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.VoidzoneNeedsBootstrap && CarnivaleVoidzoneAction(m) == cast.Rule.Action))
            CarnivaleClawStarted(module, cast, correction);
    }

    private void CarnivaleClawStarted(CarnivaleModuleState module, DungeonPendingCast cast, bool correction)
    {
        if (!carnivaleActors.TryGetValue(cast.Source, out var caster)) return;
        var index = correction ? module.RayCasts.FindIndex(c => ReferenceEquals(c.Caster, caster)) : -1;
        var value = new CarnivaleVoidzoneCast(index >= 0 ? module.RayCasts[index].Generation : ++carnivaleVoidzoneGeneration,
            caster, cast);
        if (index >= 0) module.RayCasts[index] = value;
        else module.RayCasts.Add(value);
    }

    private void CarnivaleClawFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale || !UsesCarnivaleNativeEffect(cast.Rule.Action)) return;
        foreach (var module in carnivaleModules.Where(m => CarnivaleVoidzoneAction(m) == cast.Rule.Action))
        {
            var index = module.RayCasts.FindIndex(c => c.Cast.Source == cast.Source);
            if (index >= 0) module.RayCasts.RemoveAt(index);
        }
    }

    private void CarnivaleClawEffect(ulong source, uint action, uint sequence, ulong target, Vector3? location, DateTime now, DateTime occurredAt)
    {
        if (!UsesCarnivaleNativeEffect(action) || !_actors.ContainsKey(source)
            || (_actors.GetValueOrDefault(target)?.Position ?? location) is not { } position)
            return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && CarnivaleVoidzoneAction(m) == action))
            module.RayPredictions.Add(new(++carnivaleVoidzoneGeneration, source, target, action, sequence,
                occurredAt, now, now.AddSeconds(CarnivaleVoidzoneDelay(module)), position));
    }

    private void CarnivaleClawUpdate()
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.PuddingNeedsBootstrap))
        {
            module.PuddingNeedsBootstrap = false;
            foreach (var cast in CarnivaleOrderedCasts.Where(c => c.Rule.Action == 14265))
                if (carnivaleActors.TryGetValue(cast.Source, out var caster))
                    module.Interrupts.Add(new(++carnivaleInterruptGeneration, caster, _casts[cast.Source]));
        }
        foreach (var module in carnivaleLoaded.Where(m => m.Started && CarnivaleVoidzoneAction(m) != 0))
        {
            if (module.VoidzoneNeedsBootstrap)
            {
                module.VoidzoneNeedsBootstrap = false;
                foreach (var cast in CarnivaleOrderedCasts)
                {
                    if (cast.Rule.Action == CarnivaleVoidzoneAction(module)) CarnivaleClawStarted(module, cast, false);
                    if ((module.Primary.Actor.Oid == 0x25D4 && cast.Rule.Action == 14365
                        || module.Primary.Actor.Oid == 0x242D && cast.Rule.Action == 14265
                        || module.Primary.Actor.Oid == 0x2721 && cast.Rule.Action == 15049
                        || module.Primary.Actor.Oid is 0x2727 or 0x2728 && cast.Rule.Action == 15076
                        || module.Primary.Actor.Oid == 0x2730 && cast.Rule.Action == 15081) && carnivaleActors.TryGetValue(cast.Source, out var caster))
                        module.Interrupts.Add(new(++carnivaleInterruptGeneration, caster, _casts[cast.Source]));
                }
            }
            foreach (var actor in CarnivaleEnemies(CarnivaleVoidzoneOid(module)).Where(a => a.EventState != 7))
            {
                var index = module.RayPredictions.FindIndex(p => Vector2.DistanceSquared(
                    new(p.Position.X, p.Position.Z), new(actor.Position.X, actor.Position.Z)) <= 4);
                if (index >= 0) module.RayPredictions.RemoveAt(index);
            }
        }
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleClawAoes()
    {
        if (!IsCarnivale || carnivaleSelected is not { } module || CarnivaleVoidzoneAction(module) == 0 || Active == null) yield break;
        var name = module.Primary.Actor.Oid switch
        {
            0x25D4 => "Earthen Heart",
            0x242D => "Dark Voidzone",
            0x2721 => "Magitek Ray",
            0x2730 => "The Ram's Keeper",
            0x2680 => "Meteor Voidzone",
            0x30F7 => "Gogo Thunder III",
            0x2C6A or 0x2C6C => "Sparksteel Voidzone",
            _ => "Offal Breath",
        };
        var shape = new CircleTemplate(module.Primary.Actor.Oid switch { 0x242D => 4, 0x2730 => 9, 0x2680 => 10, _ => 6 });
        foreach (var ray in CarnivaleRayCasts)
        {
            var cast = ray.Caster.Actor.Cast;
            var target = cast?.TargetId ?? ray.Cast.TargetId;
            var position = _actors.GetValueOrDefault(target)?.Position ?? cast?.Location ?? ray.Cast.Location;
            yield return new($"carnivale/ray/cast/{ray.Generation}", name, shape, position, Activation: ray.Cast.FinishAt);
        }
        foreach (var prediction in CarnivaleRayPredictions)
            yield return new($"carnivale/ray/event/{prediction.Generation}", name, shape, prediction.Position,
                Activation: prediction.Activation);
        foreach (var actor in CarnivaleEnemies(CarnivaleVoidzoneOid(module)).Where(a => a.EventState != 7))
            yield return new($"carnivale/ray/live/{actor.Id}", name, shape, QuantizedDungeonPosition(actor.Position));
    }

    private IEnumerable<string> CarnivaleClawHints(Vector3 position)
    {
        if (Active?.BossOid == 0x2720)
        {
            if (carnivalePrimary?.Dead == false)
                yield return $"{carnivalePrimary.Name} counters magical damage!";
            if (CarnivaleEnemies(0x271F).FirstOrDefault() is { Dead: false } right)
                yield return $"{right.Name} counters physical damage!";
        }
        if (Active?.BossOid != 0x2721) yield break;
        if (CarnivaleEnemies(0x2722).FirstOrDefault(a => !a.Dead) is { } left)
            yield return $"{left.Name} counters magical damage!";
        if (CarnivaleEnemies(0x2723).FirstOrDefault(a => !a.Dead) is { } claw)
            yield return $"{claw.Name} counters physical damage!";
        if (CarnivaleClawAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position)))
            yield return "GTFO from voidzone!";
    }
}
