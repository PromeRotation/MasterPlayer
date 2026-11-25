using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long carnivaleGogoGeneration;
    private static bool CarnivaleGogoAction(uint action) => action is 23097 or 23101 or 23105 or 23113 or 23112 or 23110
        or 23114 or 23115 or 23098 or 23100 or 23102 or 23103 or 23128 or 23130 or 23121 or 23131 or 23122 or 20055
        or 23123 or 23129 or 23124 or 23119 or 23126 or 23117 or 23118;
    internal CarnivaleGogoState? CarnivaleGogo => IsCarnivale && Active?.BossOid is 0x30F5 or 0x30F7
        ? carnivaleSelected?.Gogo : null;

    private void CarnivaleGogoStarted(DungeonPendingCast cast, bool correction)
    {
        if (!IsCarnivale || !CarnivaleGogoAction(cast.Rule.Action)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Gogo is { NeedsBootstrap: false }))
            CarnivaleGogoStarted(module, cast, correction);
    }

    private void CarnivaleGogoStarted(CarnivaleModuleState module, DungeonPendingCast cast, bool correction)
    {
        if (module.Primary.Actor.Oid != 0x30F7) return;
        var state = module.Gogo!;
        if (cast.Rule.Action is 23123 or 23129)
        {
            var index = correction ? state.LargeMeteors.FindIndex(c => c.Cast.Source == cast.Source) : -1;
            if (index >= 0) state.LargeMeteors[index] = (state.LargeMeteors[index].Generation, cast);
            else state.LargeMeteors.Add((++carnivaleGogoGeneration, cast));
            if (state.LargeMeteors.Count >= 99)
                state.LargeMeteors.Sort((a, b) => a.Cast.FinishAt.CompareTo(b.Cast.FinishAt));
        }
        if (correction) return;
        if (cast.Rule.Action == 23117)
        {
            state.Blizzard = cast;
            state.Pyretic.Started(cast);
        }
        if (cast.Rule.Action == 23126) state.Icestorm = cast;
    }

    private void CarnivaleGogoFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Gogo is { NeedsBootstrap: false }))
        {
            var state = module.Gogo!;
            var index = state.LargeMeteors.FindIndex(c => c.Cast.Source == cast.Source);
            if (index >= 0) state.LargeMeteors.RemoveAt(index);
            if (cast.Rule.Action == 23118) state.Blizzard = null;
        }
    }

    private void CarnivaleGogoEffect(ulong source, uint action)
    {
        if (!IsCarnivale || action != 23127 || !_actors.ContainsKey(source)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Gogo != null))
            module.Gogo!.Icestorm = null;
    }

    private void CarnivaleGogoUpdate()
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Gogo is { NeedsBootstrap: true }))
        {
            module.Gogo!.NeedsBootstrap = false;
            foreach (var actor in carnivaleActorOrder) module.Gogo.Pyretic.Observe(actor.Actor);
            foreach (var cast in CarnivaleOrderedCasts)
            {
                CarnivaleGogoStarted(module, cast, false);
                if (module.Primary.Actor.Oid == 0x30F5 && cast.Rule.Action == 23114
                    && carnivaleActors.TryGetValue(cast.Source, out var source))
                    module.Interrupts.Add(new(++carnivaleInterruptGeneration, source, _casts[cast.Source]));
            }
        }
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleGogoAoes()
    {
        if (CarnivaleGogo is not { } state) yield break;
        foreach (var item in state.LargeMeteors)
            yield return new($"carnivale/gogo/meteor/{item.Generation}", item.Cast.Rule.Name,
                new CircleTemplate(16), item.Cast.Location, Activation: item.Cast.FinishAt);
        if (state.Blizzard is { } blizzard)
            yield return new("carnivale/gogo/blizzard", "Gogo Blizzard III", new CircleTemplate(8),
                blizzard.Location, Activation: blizzard.FinishAt.AddSeconds(5.1d));
        if (Active?.BossOid == 0x30F7)
            foreach (var actor in CarnivaleEnemies(0x30F9))
                yield return new($"carnivale/gogo/maelstrom/{actor.Id}", "Maelstrom", new CircleTemplate(8),
                    actor.Position, actor.Rotation, Obstacle: true);
    }

    private IEnumerable<string> CarnivaleGogoHints(Vector3 position)
    {
        if (CarnivaleGogo is not { } state) yield break;
        if (Active?.BossOid == 0x30F5 && carnivalePrimary is { } primary)
        {
            if (CarnivaleHasStatus(primary, 2450)) yield return "Do no damage!";
            if (CarnivaleHasStatus(primary, 1797)) yield return "Dispel buff!";
        }
        if (CarnivaleGogoAoes().Where(a => !a.Obstacle).Concat(CarnivaleSapAoes())
            .Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position, 0)))
            yield return "GTFO from aoe!";
        if (CarnivaleClawAoes().Concat(CarnivaleGogoAoes().Where(a => a.Obstacle))
            .Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(position, 0)))
            yield return "GTFO from voidzone!";
        if (state.Icestorm != null) yield return "Raidwide + Frostbite + Heavy";
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (Active?.BossOid == 0x30F7 && (CarnivaleHasStatus(player, 1107) || CarnivaleHasStatus(player, 268)))
            yield return "Cleanse debuffs!";
        var slot = CarnivaleSlot(player.Id);
        if (slot is >= 0 and < 64 && state.Pyretic.Requirements[slot] is { } requirement)
        {
            if (requirement.FinishAt <= _now.AddSeconds(1000d)) yield return "Stop everything!";
            if (player.Dead) state.Pyretic.Requirements[slot] = null;
            else yield return $"SpecialMode: Pyretic activation={requirement.FinishAt:O}";
        }
    }

    private IEnumerable<DungeonDamagePrediction> CarnivaleGogoDamage()
    {
        if (CarnivaleGogo is not { } state || Active?.BossOid != 0x30F7) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 23128 or 23130 or 23124))
        {
            var current = _actors.GetValueOrDefault(cast.Source)?.Cast;
            var activation = current?.Action == cast.Rule.Action ? current.FinishAt : cast.FinishAt;
            if (activation < _now.AddSeconds((double).3f)) activation = _now.AddSeconds((double).3f);
            foreach (var actor in _actors.Values.Where(a => !a.Dead && a.PartySlot is >= 0 and < 64))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, activation, "Raidwide");
        }
        if (state.Icestorm is { } ice)
            foreach (var actor in _actors.Values.Where(a => !a.Dead && a.PartySlot is >= 0 and < 64))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, ice.FinishAt.AddSeconds(.9d), "Raidwide");
    }
}
