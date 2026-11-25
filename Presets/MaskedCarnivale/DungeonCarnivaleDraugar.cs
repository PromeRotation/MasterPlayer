using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private static bool CarnivaleDraugarAction(uint action) => action is 19053 or 19082 or 19059 or 19064 or 19067
        or 19084 or 19056 or 19057 or 19051 or 19055;
    internal CarnivaleDraugarState? CarnivaleDraugar => IsCarnivale && Active?.BossOid == 0x2CD2 ? carnivaleSelected?.Draugar : null;
    internal IEnumerable<(Vector3 From, Vector3 To, float Rotation)> CarnivaleDraugarMovements =>
        CarnivaleDraugar is { } state && _actors.GetValueOrDefault(PlayerId) is { } player
            ? state.March.Movements(new ReplayActorState { Id = player.Id, Position = player.Position, Rotation = player.Rotation }, _now) : [];

    private void CarnivaleDraugarStarted(DungeonPendingCast cast, bool correction)
    {
        if (!IsCarnivale || correction || cast.Rule.Action != 19055) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Draugar is { NeedsBootstrap: false }))
            module.Draugar!.Inversion = cast;
    }

    private void CarnivaleDraugarFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale || cast.Rule.Action != 19055) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Draugar != null))
            module.Draugar!.Inversion = null;
    }

    private void CarnivaleDraugarUpdate()
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Draugar is { NeedsBootstrap: true }))
        {
            var state = module.Draugar!;
            state.NeedsBootstrap = false;
            foreach (var actor in carnivaleActorOrder) state.Observe(actor.Actor);
            foreach (var cast in CarnivaleOrderedCasts)
            {
                if (cast.Rule.Action == 19055) state.Inversion = cast;
                if (cast.Rule.Action == 19053 && carnivaleActors.TryGetValue(cast.Source, out var source))
                    module.Interrupts.Add(new(++carnivaleInterruptGeneration, source, _casts[cast.Source]));
            }
        }
    }

    private IEnumerable<DungeonActor> CarnivaleNecrobaneSources =>
        CarnivaleEnemies(0x1EA9FA).Where(a => a.EventState != 7);

    private IEnumerable<DungeonAmbientAoe> CarnivaleDraugarAoes()
    {
        if (CarnivaleDraugar is not { } state) yield break;
        var pools = CarnivaleNecrobaneSources.ToArray();
        var inverted = state.Inversion != null;
        foreach (var pool in pools)
        {
            yield return new($"carnivale/necrobane/display/{pool.Id}", "Necrobane", new CircleTemplate(6),
                pool.Position, Navigate: false, Safe: inverted);
            if (!inverted)
                yield return new($"carnivale/necrobane/nav/{pool.Id}", "Necrobane", new CircleTemplate(6),
                    QuantizedDungeonPosition(pool.Position), Display: false);
        }
        if (inverted && pools.Length > 0)
            yield return new("carnivale/necrobane/inverted", "Mega Death / enter any Necrobane",
                new HuntStackJoinTemplate(pools.Select(p => QuantizedDungeonPosition(p.Position)).ToArray(), 6),
                Vector3.Zero, Display: false, Activation: state.Inversion!.FinishAt);
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var index = 0;
        foreach (var movement in CarnivaleDraugarMovements)
            foreach (var aoe in QuestKnockbackProjection($"carnivale/brainstorm/{index++}", player, movement.From, movement.To))
                yield return aoe with { Name = "Brainstorm / Forced march",
                    Rotation = aoe.Id.EndsWith("/player") ? movement.Rotation : aoe.Rotation };
    }

    private IEnumerable<string> CarnivaleDraugarHints(Vector3 position)
    {
        if (CarnivaleDraugar is not { } state) yield break;
        var inside = CarnivaleNecrobaneSources.Any(a => new CircleTemplate(6).Resolve(a.Position, 0, null).Contains(position, 0));
        if (state.Inversion != null) yield return inside ? "Stay in voidzone" : "Go to voidzone!";
        else if (inside) yield return "GTFO from voidzone!";
        if (CarnivaleSapAoes().Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(position, 0)))
            yield return "GTFO from aoe!";
        var last = CarnivaleDraugarMovements.LastOrDefault();
        if (last.From != last.To && (Arena?.Contains(last.To, 0) == false
            || OutputCasts.Any(c => c.Value.Rule.Action == 19082 && c.Value.Rule.Shape!.Resolve(c.Value.Location, 0, null).Contains(last.To, 0))))
            yield return "Aim for safe spot!";
    }

    private IEnumerable<DungeonDamagePrediction> CarnivaleDraugarDamage()
    {
        if (CarnivaleDraugar == null) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 19084 or 19056))
        {
            var current = _actors.GetValueOrDefault(cast.Source)?.Cast;
            var activation = current?.Action == cast.Rule.Action ? current.FinishAt : cast.FinishAt;
            if (activation < _now.AddSeconds((double).3f)) activation = _now.AddSeconds((double).3f);
            foreach (var actor in _actors.Values.Where(a => !a.Dead && a.PartySlot is >= 0 and < 64))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, activation, "Raidwide");
        }
    }
}
