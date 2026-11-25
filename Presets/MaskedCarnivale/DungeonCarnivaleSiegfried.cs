using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long carnivaleSiegfriedGeneration;
    private readonly List<(CarnivaleModuleState Module, AlphaActorState Actor, Vector3 Origin)> carnivaleSiegfriedCreates = [];
    private static bool CarnivaleSiegfriedAction(uint action) => action is 18890 or 18836 or 18837 or 18846 or 18847
        or 18888 or 18838 or 18839 or 18842 or 18843 or 18844 or 18840 or 18841 or 18897 or 19027;
    internal CarnivaleSiegfriedState? CarnivaleSiegfried => IsCarnivale && Active?.BossOid is 0x2C67 or 0x2C6A or 0x2C6C
        ? carnivaleSelected?.Siegfried : null;

    private void CarnivaleSiegfriedCreated(CarnivaleModuleState module, AlphaActorState actor, Vector3 origin)
    {
        var state = module.Siegfried!;
        var oid = module.Primary.Actor.Oid;
        if (actor.Actor.Oid == 0x2C68 && oid is 0x2C67 or 0x2C6C)
        {
            state.Bombs.Add(new(++carnivaleSiegfriedGeneration, actor, _now, _now.AddSeconds(8.4d),
                QuantizedDungeonPosition(actor.Actor.Position), 8));
            state.RubberBullet = new(++carnivaleSiegfriedGeneration, actor, _now, _now.AddSeconds(6.3d),
                origin, 20);
        }
        if (actor.Actor.Oid == 0x2CC6 && oid is 0x2C6A or 0x2C6C)
            state.Boulders.Add(new(++carnivaleSiegfriedGeneration, actor, _now, _now.AddSeconds(8.4d),
                QuantizedDungeonPosition(actor.Actor.Position), 10));
    }

    private void CarnivaleSiegfriedInput(AlphaActorState actor, bool created)
    {
        if (!created) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Siegfried is { NeedsBootstrap: false }))
            carnivaleSiegfriedCreates.Add((module, actor, module.Primary.Actor.Position));
    }

    private void CarnivaleSiegfriedObserveCreated(ulong actor)
    {
        foreach (var item in carnivaleSiegfriedCreates.Where(c => c.Actor.Actor.Id == actor))
            CarnivaleSiegfriedCreated(item.Module, item.Actor, item.Origin);
        carnivaleSiegfriedCreates.RemoveAll(c => c.Actor.Actor.Id == actor);
    }

    private void CarnivaleSiegfriedStarted(DungeonPendingCast cast, bool correction)
    {
        if (!IsCarnivale || !CarnivaleSiegfriedAction(cast.Rule.Action)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Siegfried is { NeedsBootstrap: false }))
            CarnivaleSiegfriedStarted(module, cast, correction);
    }

    private void CarnivaleSiegfriedStarted(CarnivaleModuleState module, DungeonPendingCast cast, bool correction)
    {
        var state = module.Siegfried!;
        var oid = module.Primary.Actor.Oid;
        List<(long Generation, DungeonPendingCast Cast)>? group = null;
        var sortThreshold = 99;
        if (oid is 0x2C67 or 0x2C6C && cast.Rule.Action is 18836 or 18837) group = state.Hyperdrives;
        if (oid is 0x2C6A or 0x2C6C)
        {
            if (cast.Rule.Action is 18838 or 18839) group = state.Torches;
            if (cast.Rule.Action is 18841 or 18897)
            {
                group = state.Sparksteels;
                sortThreshold = 8;
            }
            if (cast.Rule.Action == 18842)
            {
                var index = correction ? module.CastKnockbacks.FindIndex(k => k.Cast.Source == cast.Source && k.Cast.Rule.Action == 18842) : -1;
                var value = new CarnivaleKnockbackCast(index >= 0 ? module.CastKnockbacks[index].Generation : ++carnivaleSiegfriedGeneration,
                    cast, 10, false, null);
                if (index >= 0) module.CastKnockbacks[index] = value;
                else module.CastKnockbacks.Add(value);
            }
        }
        if (group == null) return;
        var previous = correction ? group.FindIndex(c => c.Cast.Source == cast.Source) : -1;
        if (previous >= 0) group[previous] = (group[previous].Generation, cast);
        else group.Add((++carnivaleSiegfriedGeneration, cast));
        if (group.Count >= sortThreshold) group.Sort((a, b) => a.Cast.FinishAt.CompareTo(b.Cast.FinishAt));
    }

    private void CarnivaleSiegfriedFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Siegfried is { NeedsBootstrap: false }))
        {
            var state = module.Siegfried!;
            foreach (var group in new[] { state.Hyperdrives, state.Torches, state.Sparksteels })
            {
                var index = group.FindIndex(c => c.Cast.Source == cast.Source);
                if (index >= 0) group.RemoveAt(index);
            }
            if (cast.Rule.Action == 18847) state.RubberBullet = null;
            if (cast.Rule.Action == 18888) state.Bombs.Clear();
        }
    }

    private void CarnivaleSiegfriedEffect(ulong source, uint action)
    {
        if (!IsCarnivale || action != 18986 || !_actors.ContainsKey(source)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Siegfried != null))
            module.Siegfried!.Boulders.Clear();
    }

    private void CarnivaleSiegfriedUpdate()
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Siegfried is { NeedsBootstrap: true }))
        {
            module.Siegfried!.NeedsBootstrap = false;
            foreach (var actor in carnivaleActorOrder) CarnivaleSiegfriedCreated(module, actor, module.Primary.Actor.Position);
            foreach (var cast in CarnivaleOrderedCasts) CarnivaleSiegfriedStarted(module, cast, false);
        }
    }

    internal Vector3? CarnivaleRubberBulletLanding(DungeonActor player)
    {
        if (CarnivaleSiegfried?.RubberBullet is not { } kb
            || carnivaleSelected!.ChargeImmunity.ImmuneAt(CarnivaleSlot(player.Id), kb.Activation)) return null;
        var direction = player.Position - kb.Origin;
        direction.Y = 0;
        return direction.LengthSquared() == 0 ? null : player.Position + Vector3.Normalize(direction) * kb.Distance;
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleSiegfriedAoes()
    {
        if (CarnivaleSiegfried is not { } state) yield break;
        foreach (var item in state.Hyperdrives)
            yield return new($"carnivale/siegfried/hyperdrive/{item.Generation}", "Hyperdrive", new CircleTemplate(5),
                item.Cast.Location, Activation: item.Cast.FinishAt);
        foreach (var item in state.Torches)
            yield return new($"carnivale/siegfried/torch/{item.Generation}", "Law of the Torch", new FanTemplate(34, MathF.PI / 9),
                item.Cast.Location, item.Cast.Rotation, Activation: item.Cast.FinishAt);
        if (state.Sparksteels.Count > 0)
        {
            var deadline = state.Sparksteels[0].Cast.FinishAt.AddSeconds(1d);
            foreach (var item in state.Sparksteels)
            {
                if (item.Cast.FinishAt >= deadline) break;
                yield return new($"carnivale/siegfried/sparksteel/{item.Generation}", "Sparksteel", new CircleTemplate(8),
                    item.Cast.Location, Activation: item.Cast.FinishAt);
            }
        }
        foreach (var item in state.Bombs.Concat(state.Boulders))
            yield return new($"carnivale/siegfried/spawn/{item.Generation}", item.Radius == 8 ? "Explosion" : "Sphere Shatter",
                new CircleTemplate(item.Radius), item.Position, Activation: item.Activation);
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        foreach (var movement in CarnivaleCastKnockbackMovements(player, 18842))
            foreach (var aoe in QuestKnockbackProjection($"carnivale/siegfried/swift/kb/{movement.Source.Generation}", player, movement.From, movement.To))
                yield return aoe;
        if (CarnivaleRubberBulletLanding(player) is { } to)
            foreach (var aoe in QuestKnockbackProjection("carnivale/siegfried/rubber/kb", player, player.Position, to))
                yield return aoe;
    }

    private IEnumerable<string> CarnivaleSiegfriedHints(Vector3 position)
    {
        if (CarnivaleSiegfried is not { } state) yield break;
        if (Active?.BossOid is 0x2C67 or 0x2C6C && carnivalePrimary is { } boss)
        {
            if (CarnivaleHasStatus(boss, 2166)) yield return $"{boss.Name} will reflect all magic damage!";
            if (_actors.GetValueOrDefault(PlayerId) is { } player && CarnivaleHasStatus(player, 564))
                yield return "You were bound! Cleanse it with Exuviation.";
        }
        if (CarnivaleSiegfriedAoes().Any(a => a.Navigate && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position, 0)))
            yield return "GTFO from aoe!";
        if (CarnivaleClawAoes().Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(position, 0)))
            yield return "GTFO from voidzone!";
        if (_actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;
        foreach (var movement in CarnivaleCastKnockbackMovements(viewer, 18842))
            if (Arena?.Contains(movement.To, 0) == false || OutputCasts.Any(c => c.Value.Rule.Action is 18843 or 18844
                && c.Value.Rule.Shape!.Resolve(c.Value.Location, c.Value.Rotation, null).Contains(movement.To, 0)))
                yield return "About to be knocked into danger!";
        if (CarnivaleRubberBulletLanding(viewer) is { } landing && (Arena?.Contains(landing, 0) == false
            || state.Bombs.Any(b => new CircleTemplate(b.Radius).Resolve(b.Position, 0, null).Contains(landing, 0))))
            yield return "About to be knocked into danger!";
    }
}
