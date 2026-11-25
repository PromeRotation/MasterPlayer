using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long carnivaleRedGeneration;
    private void CarnivalePyreticObserve(DungeonActor actor)
    {
        if (!IsCarnivale) return;
        // Feed applies each actor's cast before its status losses, in actor order.
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Red is { NeedsBootstrap: false }))
            module.Red!.Pyretic.Observe(actor);
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Gogo is { NeedsBootstrap: false }))
            module.Gogo!.Pyretic.Observe(actor);
    }

    private static bool CarnivaleRedAction(uint action) => action is 18689 or 18694 or 18691 or 18692 or 18696 or 18895
        or 18690 or 18698 or 18699 or 18961 or 18965 or 19022 or 19028 or 18964 or 18971 or 18984 or 18963 or 18962 or 18969 or 18985;
    internal CarnivaleRedState? CarnivaleRed => IsCarnivale && Active?.BossOid is 0x2C5B or 0x2C5D ? carnivaleSelected?.Red : null;

    private void CarnivaleRedStarted(DungeonPendingCast cast, bool correction)
    {
        if (!IsCarnivale || !CarnivaleRedAction(cast.Rule.Action)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Red is { NeedsBootstrap: false }))
            CarnivaleRedStarted(module, cast, correction);
    }

    private void CarnivaleRedStarted(CarnivaleModuleState module, DungeonPendingCast cast, bool correction)
    {
        var oid = module.Primary.Actor.Oid;
        if (oid == 0x2C5B && cast.Rule.Action == 18689 || oid == 0x2C5D && cast.Rule.Action is 18961 or 18965)
        {
            var index = correction ? module.CastKnockbacks.FindIndex(k => k.Cast.Source == cast.Source && k.Cast.Rule.Action == cast.Rule.Action) : -1;
            var value = new CarnivaleKnockbackCast(index >= 0 ? module.CastKnockbacks[index].Generation : ++carnivaleRedGeneration,
                cast, cast.Rule.Action == 18965 ? 25 : 50, false, null, cast.Rule.Action != 18965);
            if (index >= 0) module.CastKnockbacks[index] = value;
            else module.CastKnockbacks.Add(value);
        }
        if (correction) return;
        if (oid == 0x2C5B && cast.Rule.Action == 18691) module.Red!.Pyretic.Started(cast);
        if (oid == 0x2C5D && cast.Rule.Action is 18963 or 18962)
        {
            var value = new CarnivaleFerrofluidCast(++carnivaleRedGeneration, cast, 6, cast.Rule.Action == 18962);
            module.Red!.FerrofluidKnockback = value;
            module.Red.FerrofluidAoe = value;
        }
    }

    private void CarnivaleRedFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale || cast.Rule.Action is not (18963 or 18962)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Red != null))
            module.Red!.FerrofluidKnockback = null;
    }

    private void CarnivaleRedEffect(ulong source, uint action)
    {
        if (!IsCarnivale || action is not (18974 or 18973) || !_actors.ContainsKey(source)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Red != null))
            module.Red!.FerrofluidAoe = null;
    }

    private void CarnivaleRedUpdate()
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Red is { NeedsBootstrap: true }))
        {
            module.Red!.NeedsBootstrap = false;
            foreach (var actor in carnivaleActorOrder) module.Red.Pyretic.Observe(actor.Actor);
            foreach (var cast in CarnivaleOrderedCasts)
            {
                CarnivaleRedStarted(module, cast, false);
                if ((module.Primary.Actor.Oid == 0x2C5B && cast.Rule.Action == 18689
                    || module.Primary.Actor.Oid == 0x2C5D && cast.Rule.Action == 18961)
                    && carnivaleActors.TryGetValue(cast.Source, out var source))
                    module.Interrupts.Add(new(++carnivaleInterruptGeneration, source, _casts[cast.Source]));
            }
        }
    }

    internal Vector3? CarnivaleFerrofluidLanding(DungeonActor player)
    {
        if (CarnivaleRed?.FerrofluidKnockback is not { } kb
            || carnivaleSelected!.ChargeImmunity.ImmuneAt(CarnivaleSlot(player.Id), kb.Cast.FinishAt)) return null;
        var direction = player.Position - kb.Cast.Location;
        direction.Y = 0;
        var length = direction.Length();
        if (length == 0) return null;
        direction /= length;
        return kb.Pull ? player.Position - direction * MathF.Min(kb.Distance, length) : player.Position + direction * kb.Distance;
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleRedAoes()
    {
        if (CarnivaleRed is not { } state) yield break;
        if (state.FerrofluidAoe is { } aoe)
            yield return new($"carnivale/ferrofluid/aoe/{aoe.Generation}", "Fluid Convection / Dynamic",
                aoe.Pull ? new CircleTemplate(6) : new DonutTemplate(10, 40), aoe.Cast.Location, Activation: aoe.Cast.FinishAt);
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        foreach (var action in new uint[] { 18689, 18961, 18965 })
            foreach (var movement in CarnivaleCastKnockbackMovements(player, action))
                foreach (var preview in QuestKnockbackProjection($"carnivale/red/kb/{movement.Source.Generation}", player, movement.From, movement.To))
                    yield return preview;
        if (CarnivaleFerrofluidLanding(player) is { } to)
            foreach (var preview in QuestKnockbackProjection("carnivale/ferrofluid/kb", player, player.Position, to))
                yield return preview;
    }

    private IEnumerable<string> CarnivaleRedHints(Vector3 position)
    {
        if (CarnivaleRed is not { } state) yield break;
        if (Active?.BossOid == 0x2C5D)
        {
            var left = CarnivaleEnemies(0x2C5F).FirstOrDefault();
            var right = CarnivaleEnemies(0x2C5E).FirstOrDefault();
            if (left is { Dead: false }) yield return $"{left.Name} will drain all your MP, kill it fast!";
            else if (right is { Dead: false }) yield return $"{right.Name} will do multiple raidwides, kill it fast!";
        }
        var aoes = CarnivaleRedAoes().Where(a => a.Navigate).Concat(CarnivaleSapAoes()).ToArray();
        if (aoes.Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position, 0))) yield return "GTFO from aoe!";
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (Active?.BossOid == 0x2C5D && CarnivaleHasStatus(player, 700))
            yield return "You were doomed! Cleanse it with Exuviation.";
        foreach (var action in new uint[] { 18689, 18961, 18965 })
            if (CarnivaleCastKnockbackMovements(player, action).Any(m => Arena?.Contains(m.To, 0) == false))
                yield return "About to be knocked into danger!";
        if (CarnivaleFerrofluidLanding(player) is { } to && (Arena?.Contains(to, 0) == false
            || aoes.Where(a => a.Id.StartsWith("carnivale/ferrofluid/aoe/")).Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(to, 0))))
            yield return "About to be knocked into danger!";
        var slot = CarnivaleSlot(player.Id);
        if (slot is >= 0 and < 64 && state.Pyretic.Requirements[slot] is { } requirement)
        {
            if (requirement.FinishAt <= _now.AddSeconds(1000d)) yield return "Stop everything!";
            if (player.Dead) state.Pyretic.Requirements[slot] = null;
            else yield return $"SpecialMode: Pyretic activation={requirement.FinishAt:O}";
        }
    }

    private IEnumerable<DungeonDamagePrediction> CarnivaleRedDamage()
    {
        if (CarnivaleRed == null || Active?.BossOid != 0x2C5D) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 18965 or 19022))
        {
            var current = _actors.GetValueOrDefault(cast.Source)?.Cast;
            var activation = current?.Action == cast.Rule.Action ? current.FinishAt : cast.FinishAt;
            if (activation < _now.AddSeconds((double).3f)) activation = _now.AddSeconds((double).3f);
            foreach (var actor in _actors.Values.Where(a => !a.Dead && a.PartySlot is >= 0 and < 64))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, activation, "Raidwide");
        }
    }
}
