using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long carnivaleEyeStackGeneration;
    private Dictionary<ulong, int> carnivaleEyePreviousRoster = [];
    internal ulong? CarnivaleSourcePlayerId { get; set; }
    private Dictionary<ulong, int> CarnivaleEyeRoster()
    {
        var roster = new Dictionary<ulong, int>();
        if (CarnivaleSourcePlayerId is { } source)
        {
            // Carnivale is a solo duty. The source feed reserves Raid[0] for the
            // recorded/local player, independently of PR's configurable role slot.
            if (source is not (0 or 0xE0000000)) roster.Add(source, 0);
            return roster;
        }
        foreach (var member in CurrentPartyMembers) roster.TryAdd(member.EntityId, member.Slot);
        foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0)) roster.TryAdd(actor.Id, actor.PartySlot);
        return roster;
    }
    private int CarnivaleEyeSlot(ulong actor) => CarnivaleEyeRoster().GetValueOrDefault(actor, -1);
    private static bool CarnivaleEyeAction(uint action) => action is 14691 or 14692 or 14693 or 14694;
    private static bool CarnivaleConstructorAction(uint action) => action is 14691 or 14693;
    private bool CarnivaleEyeBootstrap(uint action) => CarnivaleEyeAction(action)
        && carnivaleModules.Any(m => m.Eyes is { } eyes
            && (CarnivaleConstructorAction(action) ? eyes.ConstructorNeedsBootstrap : m.Started && eyes.CombatNeedsBootstrap));
    private CarnivaleEyeState? VisibleCarnivaleEyes => IsCarnivale && carnivaleEnabled && !carnivaleWiping
        && (Active != null || CarnivalePrecombatVisible) ? carnivaleSelected?.Eyes : null;
    internal IEnumerable<CarnivaleEyeStack> CarnivaleEyeStacks => VisibleCarnivaleEyes?.Stacks ?? [];
    private IEnumerable<DungeonPendingCast> CarnivaleDemonEyes => VisibleCarnivaleEyes is { } eyes
        && !eyes.DemonBlindness.Blinded(CarnivaleEyeSlot(PlayerId)) ? eyes.Demons : [];

    private void CarnivaleEyeStarted(DungeonPendingCast cast, bool correction)
    {
        if (!IsCarnivale || !CarnivaleEyeAction(cast.Rule.Action)) return;
        foreach (var module in carnivaleModules.Where(m => m.Eyes is { } eyes
            && (CarnivaleConstructorAction(cast.Rule.Action) ? !eyes.ConstructorNeedsBootstrap : m.Started && !eyes.CombatNeedsBootstrap)))
            CarnivaleEyeStarted(module, cast, correction);
    }

    private void CarnivaleEyeStarted(CarnivaleModuleState module, DungeonPendingCast cast, bool correction)
    {
        var eyes = module.Eyes!;
        if (cast.Rule.Action == 14693)
        {
            var index = correction ? eyes.Stacks.FindIndex(s => s.Cast.Source == cast.Source) : -1;
            if (index >= 0)
            {
                var stack = eyes.Stacks[index];
                if (stack.Target.Actor.Id == cast.TargetId)
                    eyes.Stacks[index] = stack with { Cast = cast };
                else if (carnivaleActors.TryGetValue(cast.TargetId, out var correctedTarget))
                    eyes.Stacks[index] = stack with { Target = correctedTarget, Cast = cast };
                else
                    eyes.Stacks.RemoveAt(index);
            }
            // A target-FIFO Finish may already have consumed this source's stack.
            // Corrections update a surviving entry; they never replay its Start.
            else if (!correction && carnivaleActors.TryGetValue(cast.TargetId, out var target))
                eyes.Stacks.Add(new(++carnivaleEyeStackGeneration, target, cast));
            return;
        }
        var list = cast.Rule.Action switch
        {
            14691 => eyes.Demons,
            14692 when module.Started => eyes.Cold,
            14694 when module.Started && module.Primary.Actor.Oid == 0x26FF => eyes.Dread,
            _ => null,
        };
        if (list == null) return;
        var previous = correction ? list.FindIndex(c => c.Source == cast.Source) : -1;
        if (previous >= 0) list[previous] = cast;
        else list.Add(cast);
    }

    private void CarnivaleEyeFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale || !CarnivaleEyeAction(cast.Rule.Action)) return;
        foreach (var module in carnivaleModules.Where(m => m.Eyes != null))
        {
            var eyes = module.Eyes!;
            if (cast.Rule.Action == 14693)
            {
                var i = eyes.Stacks.FindIndex(s => s.Target.Actor.Id == cast.TargetId);
                if (i >= 0) eyes.Stacks.RemoveAt(i);
            }
            else
            {
                var list = cast.Rule.Action switch { 14691 => eyes.Demons, 14692 => eyes.Cold, _ => eyes.Dread };
                var i = list.FindIndex(c => c.Source == cast.Source);
                if (i >= 0) list.RemoveAt(i);
            }
        }
    }

    private void CarnivaleEyeUpdate()
    {
        if (!IsCarnivale) return;
        var roster = CarnivaleEyeRoster();
        foreach (var module in carnivaleModules.Where(m => m.Eyes != null))
        {
            var eyes = module.Eyes!;
            var constructor = eyes.ConstructorNeedsBootstrap;
            eyes.ConstructorNeedsBootstrap = false;
            if (constructor)
                foreach (var cast in CarnivaleOrderedCasts.Where(c => CarnivaleConstructorAction(c.Rule.Action)))
                    CarnivaleEyeStarted(module, cast, false);
            eyes.DemonBlindness.Observe(_actors.Values, roster, constructor, carnivaleEyePreviousRoster);
            eyes.TearyBlindness.Observe(_actors.Values, roster, constructor, carnivaleEyePreviousRoster);
            if (!module.Started) continue;
            var combat = eyes.CombatNeedsBootstrap;
            eyes.CombatNeedsBootstrap = false;
            if (combat)
                foreach (var cast in CarnivaleOrderedCasts.Where(c => c.Rule.Action == 14692))
                    CarnivaleEyeStarted(module, cast, false);
            eyes.ColdBlindness.Observe(_actors.Values, roster, combat);
            if (module.Primary.Actor.Oid != 0x26FF) continue;
            if (combat)
                foreach (var cast in CarnivaleOrderedCasts.Where(c => c.Rule.Action == 14694))
                    CarnivaleEyeStarted(module, cast, false);
            eyes.DreadBlindness.Observe(_actors.Values, roster, combat);
        }
        carnivaleEyePreviousRoster = roster;
    }

    private void CarnivaleEyeStatus(ulong actor, uint status, bool present)
    {
        if (!IsCarnivale || status != 571) return;
        var slot = CarnivaleEyeSlot(actor);
        foreach (var module in carnivaleModules.Where(m => m.Eyes != null))
        {
            var eyes = module.Eyes!;
            if (!eyes.ConstructorNeedsBootstrap)
            {
                eyes.DemonBlindness.Status(slot, present);
                eyes.TearyBlindness.Status(slot, present);
            }
            if (!module.Started || eyes.CombatNeedsBootstrap) continue;
            eyes.ColdBlindness.Status(slot, present);
            if (module.Primary.Actor.Oid == 0x26FF) eyes.DreadBlindness.Status(slot, present);
        }
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleEyeAoes()
    {
        if (VisibleCarnivaleEyes is not { } eyes) yield break;
        var slot = CarnivaleEyeSlot(PlayerId);
        foreach (var (casts, blind, radius) in new[] { (eyes.Cold, eyes.ColdBlindness, 42.53f), (eyes.Dread, eyes.DreadBlindness, 7.35f) })
            if (!blind.Blinded(slot))
                foreach (var cast in casts)
                    yield return new($"carnivale/eyes/cone/{cast.Rule.Action}/{cast.Source}", cast.Rule.Name,
                        new FanTemplate(radius, MathF.PI / 2), cast.Location, cast.Rotation, Activation: cast.FinishAt);
        var stacks = eyes.Stacks.Where(s => !s.Target.Actor.Dead).ToArray();
        var marked = stacks.Any(s => s.Target.Actor.Id == PlayerId && !s.Target.Removed);
        foreach (var stack in stacks)
            yield return new($"carnivale/eyes/stack/{stack.Generation}", "Teary Twirl", new CircleTemplate(6.3f),
                QuantizedDungeonPosition(stack.Target.Actor.Position), Navigate: false, OutlineOnly: true,
                Safe: !marked || stack.Target.Actor.Id == PlayerId && !stack.Target.Removed);
        // The host publishes component navigation only during ActivePhase.
        // A solo raid has no stack navigation even if the target is a mandragora.
        if (Active == null || Arena == null || !carnivaleSelected!.Started) yield break;
        var party = _actors.Values.Where(a => a.PartySlot is >= 0 and < 64 && !a.Dead).ToArray();
        if (party.Length < 2) yield break;
        foreach (var stack in stacks.Where(s => s.Target.Actor.Id == PlayerId && !s.Target.Removed))
        {
            var buddies = party.Where(a => !stacks.Any(s => !s.Target.Removed && s.Target.Actor.Id == a.Id))
                .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 6.3f * .5f)).ToArray();
            if (buddies.Length > 0)
                yield return new($"carnivale/eyes/stack/{stack.Generation}/buddy", "寻找分摊同伴",
                    new SohrKhaiWallHintTemplate(Arena, buddies), Arena.Position, Display: false, Activation: stack.Cast.FinishAt);
        }
        if (marked)
        {
            foreach (var stack in stacks.Where(s => s.Target.Actor.Id != PlayerId || s.Target.Removed))
                yield return new($"carnivale/eyes/stack/{stack.Generation}/avoid", "避开其他分摊",
                    new CircleTemplate(12.6f), QuantizedDungeonPosition(stack.Target.Actor.Position), Display: false, Activation: stack.Cast.FinishAt);
        }
        else
        {
            var candidates = stacks.Select(s => (IGeometryShape)new CircleGeometry(QuantizedDungeonPosition(s.Target.Actor.Position), 6.3f)).ToArray();
            if (candidates.Length > 0)
                yield return new("carnivale/eyes/stack/join", "进入任一分摊圈", new SohrKhaiWallHintTemplate(Arena, candidates),
                    Arena.Position, Display: false, Activation: stacks[0].Cast.FinishAt);
        }
    }

    private IEnumerable<string> CarnivaleEyeHints(Vector3 position)
    {
        if (VisibleCarnivaleEyes is not { } eyes) yield break;
        yield return eyes.TearyBlindness.Blinded(CarnivaleEyeSlot(PlayerId))
            ? "Kill mandragoras last incase you need to get blinded again." : "Stack to get blinded!";
        if (Active?.BossOid == 0x26FF) yield return "The eyes are weak to lightning spells.";
        if (CarnivaleEyeAoes().Any(a => a.Id.Contains("/cone/") && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position, 0)))
            yield return "GTFO from aoe!";
    }
}
