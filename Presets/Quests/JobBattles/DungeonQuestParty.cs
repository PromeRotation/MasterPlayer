using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<int, ulong> jobQuestNpcSlots = [];
    private readonly List<(DungeonActor Target, DungeonPendingCast Cast, bool Removed)> jobQuestStacks = [];
    private readonly List<(DungeonActor Target, DungeonPendingCast Cast, bool Removed)> jobQuestSpreads = [];
    private readonly Dictionary<ulong, DungeonPendingCast> jobQuestPartyStarted = [];
    private readonly HashSet<ulong> treasureMissingStackTargets = [];
    private bool IsNpcPartyQuest => IsCarnivale || IsBarPassage || IsMainScenarioQuest || IsNovice || IsHeroesPretenders || IsDikaiosyne || territory is JobQuestCatalog.SAGES_FOCUS or JobQuestCatalog.ORPHANS_AND_BROKEN_BLADE or JobQuestCatalog.OUR_UNSUNG_HEROES or JobQuestCatalog.LIFE_EPHEMERAL or JobQuestCatalog.STEEL_AGAINST_STEEL or JobQuestCatalog.POWER_TO_PROTECT or JobQuestCatalog.GAMBOLING_FOR_GIL;
    private bool UsesQuestPartyMarks => IsNpcPartyQuest || IsHeavenOnHigh || HasTreasureSpreadTargets || HasTreasureEightStack;
    private static bool JobQuestStackAction(uint action) => action is 8457 or 26847 or 26860 or 17558 or 30025 or 34890 or 21737 or 17877 or 13315 or 32314 or 21760 or 21688 or 19615 or 38199 or 37464 or 14606 or 16134 or 40930;
    private static bool JobQuestPartyAction(uint action) => JobQuestStackAction(action) || action is 12207 or 11902 or 26553 or 26848 or 26867 or 30023 or 27449 or 27457 or 34893 or 32271 or 28002 or 32260 or 43696 or 21747 or 21708 or 32354 or 32340 or 38580 or 43714 or 43782 or 43742 or 19613 or 38198 or 37463 or 17431 or 40932;
    private static float JobQuestStackRadius(uint action) => action is 8457 or 14606 ? 5 : 6;
    private static float JobQuestSpreadRadius(uint action) => action switch { 12207 => 3, 11902 or 26848 or 30023 or 27457 or 32271 or 28002 or 32354 or 32340 or 38580 or 43714 or 43742 or 38198 or 17431 or 40932 => 6, 34893 or 32260 or 21747 or 21708 or 19613 => 8, _ => 5 };

    private void ObserveJobQuestParty()
    {
        if (!UsesQuestPartyMarks) return;
        foreach (var (slot, id) in jobQuestNpcSlots.ToArray())
            if (!_actors.TryGetValue(id, out var actor) || !GraoullyFriendlyNpc(actor))
                jobQuestNpcSlots.Remove(slot);
        foreach (var actor in _actors.Values.ToArray())
        {
            if (!IsNpcPartyQuest || !GraoullyFriendlyNpc(actor) || actor.PartySlot is >= 0 and < 24) continue;
            var slot = jobQuestNpcSlots.FirstOrDefault(p => p.Value == actor.Id).Key;
            if (slot < 24)
            {
                slot = actor.PartySlot is >= 24 and < 64 ? actor.PartySlot
                    : Enumerable.Range(24, 40).FirstOrDefault(s => !jobQuestNpcSlots.ContainsKey(s)
                        && !_actors.Values.Any(a => a.PartySlot == s));
                if (slot < 24) continue;
                jobQuestNpcSlots[slot] = actor.Id;
            }
            _actors[actor.Id] = actor with { PartySlot = slot };
        }
        foreach (var marks in new[] { jobQuestStacks, jobQuestSpreads })
            for (var i = 0; i < marks.Count; ++i)
            {
                var mark = marks[i];
                if (!mark.Removed && _actors.TryGetValue(mark.Target.Id, out var actor) && actor.Oid == mark.Target.Oid)
                    marks[i] = (actor, mark.Cast, false);
                else
                    marks[i] = (mark.Target, mark.Cast, true);
            }
    }

    private void JobQuestPartyCast(DungeonPendingCast cast)
    {
        if (!UsesQuestPartyMarks || !JobQuestPartyAction(cast.Rule.Action)) return;
        var marks = JobQuestStackAction(cast.Rule.Action) ? jobQuestStacks : jobQuestSpreads;
        if (jobQuestPartyStarted.TryGetValue(cast.Source, out var previous)
            && previous.Rule.Action == cast.Rule.Action
            && Math.Abs((previous.StartedAt - cast.StartedAt).TotalSeconds) < .25
            && Math.Abs((previous.FinishAt - cast.FinishAt).TotalSeconds) < .75)
        {
            var index = marks.FindIndex(m => ReferenceEquals(m.Cast, previous));
            if (index >= 0)
            {
                var mark = marks[index];
                if (IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.DAEN_ECHIDNA
                    && cast.Rule.Action == 21760 && mark.Target.Id != cast.TargetId)
                {
                    if (_actors.TryGetValue(cast.TargetId, out var correctedTarget))
                    {
                        marks[index] = (correctedTarget, cast, false);
                        treasureMissingStackTargets.Remove(cast.Source);
                    }
                    else
                    {
                        marks.RemoveAt(index);
                        treasureMissingStackTargets.Add(cast.Source);
                    }
                }
                else
                    marks[index] = (mark.Target, cast, mark.Removed);
            }
            else if (treasureMissingStackTargets.Contains(cast.Source) && previous.TargetId != cast.TargetId
                && _actors.TryGetValue(cast.TargetId, out var correctedTarget))
            {
                marks.Add((correctedTarget, cast, false));
                treasureMissingStackTargets.Remove(cast.Source);
            }
        }
        else if (_actors.TryGetValue(cast.TargetId, out var target))
            marks.Add((target, cast, false));
        else if (IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.DAEN_ECHIDNA && cast.Rule.Action == 21760)
            treasureMissingStackTargets.Add(cast.Source);
        jobQuestPartyStarted[cast.Source] = cast;
    }

    private void JobQuestPartyFinish(DungeonPendingCast cast)
    {
        if (!UsesQuestPartyMarks) return;
        if (JobQuestPartyAction(cast.Rule.Action) && cast.Rule.Action != 8457)
        {
            var marks = JobQuestStackAction(cast.Rule.Action) ? jobQuestStacks : jobQuestSpreads;
            var index = marks.FindIndex(m => m.Cast.Rule.Action == cast.Rule.Action && m.Target.Id == cast.TargetId);
            if (index >= 0) marks.RemoveAt(index);
        }
        jobQuestPartyStarted.Remove(cast.Source);
        treasureMissingStackTargets.Remove(cast.Source);
    }

    private void JobQuestPartyRemoved(ulong source)
    {
        foreach (var marks in new[] { jobQuestStacks, jobQuestSpreads })
            for (var i = 0; i < marks.Count; ++i)
                if (marks[i].Target.Id == source)
                    marks[i] = (marks[i].Target, marks[i].Cast, true);
    }

    private IEnumerable<DungeonAmbientAoe> JobQuestPartyAoes()
    {
        if (!UsesQuestPartyMarks || Active == null || Arena == null || IsBarPassage && !IsBarPassageP3) yield break;
        if (IsHeroesPretenders)
        {
            foreach (var aoe in HeroesPretendersPartyAoes()) yield return aoe;
            yield break;
        }
        if (HasTreasureEightStack)
        {
            foreach (var aoe in TreasureEightStacks()) yield return aoe;
            yield break;
        }
        var party = _actors.Values.Where(a => (HasTreasureSpreadTargets ? treasureParty.Contains(a.Id) : a.PartySlot >= 0) && !a.Dead).ToArray();
        var spreads = jobQuestSpreads.Where(m => !m.Target.Dead).ToArray();
        foreach (var (mark, index) in spreads.Select((m, i) => (m, i)))
        {
            var position = QuantizedDungeonPosition(mark.Target.Position);
            var radius = JobQuestSpreadRadius(mark.Cast.Rule.Action);
            yield return new($"quest/spread/{index}/display", mark.Cast.Rule.Name, new CircleTemplate(radius), position,
                Navigate: false, OutlineOnly: true);
            if (party.Length < 2) continue;
            if (mark.Target.Id != PlayerId || mark.Removed)
                yield return new($"quest/spread/{index}/avoid", mark.Cast.Rule.Name, new CircleTemplate(radius + 1), position,
                    Display: false, Activation: mark.Cast.FinishAt);
            else
                foreach (var member in party.Where(a => !spreads.Any(s => s.Cast.Rule.Action == mark.Cast.Rule.Action && !s.Removed && s.Target.Id == a.Id)))
                    yield return new($"quest/spread/{index}/separate/{member.Id}", mark.Cast.Rule.Name, new CircleTemplate(radius + 1),
                        QuantizedDungeonPosition(member.Position), Display: false, Activation: mark.Cast.FinishAt);
        }
        var stacks = jobQuestStacks.Where(m => !m.Target.Dead).ToArray();
        var exactStack = IsSleep || IsOtis;
        var viewerMarked = stacks.Any(m => m.Target.Id == PlayerId && !m.Removed);
        foreach (var (mark, index) in stacks.Select((m, i) => (m, i)))
        {
            var position = QuantizedDungeonPosition(mark.Target.Position);
            var radius = JobQuestStackRadius(mark.Cast.Rule.Action);
            var safe = !viewerMarked || mark.Target.Id == PlayerId && !mark.Removed;
            yield return new($"quest/stack/{index}/display", mark.Cast.Rule.Name, new CircleTemplate(radius), position,
                Navigate: false, Safe: safe, OutlineOnly: true);
            if (party.Length < 2) continue;
            if (mark.Target.Id == PlayerId && !mark.Removed)
            {
                var buddies = party.Where(a => !stacks.Any(s => s.Cast.Rule.Action == mark.Cast.Rule.Action && !s.Removed && s.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, radius * .5f)).ToArray();
                if (buddies.Length > 0)
                    yield return new($"quest/stack/{index}/buddy", "寻找分摊同伴",
                        IsBarPassageP3 ? new DungeonStackBuddyTemplate(Arena, buddies.Select(b => b.Position).ToArray(), radius * .5f)
                            : new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: mark.Cast.FinishAt);
            }
        }
        if (exactStack && viewerMarked)
            foreach (var (mark, index) in stacks.Select((m, i) => (m, i)))
                if (mark.Target.Id != PlayerId || mark.Removed)
                    yield return new($"quest/stack/{index}/avoid", mark.Cast.Rule.Name,
                        new CircleTemplate(JobQuestStackRadius(mark.Cast.Rule.Action) * 2),
                        QuantizedDungeonPosition(mark.Target.Position), Display: false, Activation: mark.Cast.FinishAt);
        if (party.Length >= 2 && stacks.Length > 0 && (!exactStack || !viewerMarked))
        {
            var candidates = stacks.Where(m => m.Target.Id != PlayerId || m.Removed)
                .Select(m => (IGeometryShape)new CircleGeometry(QuantizedDungeonPosition(m.Target.Position), JobQuestStackRadius(m.Cast.Rule.Action))).ToArray();
            if (candidates.Length > 0)
                yield return new("quest/stack/join", "进入任一分摊圈", new SohrKhaiWallHintTemplate(Arena, candidates),
                    Arena.Position, Display: false, Activation: stacks[0].Cast.FinishAt);
        }
    }

    private IEnumerable<string> JobQuestPartyHints(Vector3? player)
    {
        if (!UsesQuestPartyMarks || player is not { } position || IsBarPassage && !IsBarPassageP3) yield break;
        if (IsHeroesPretenders) yield break;
        if (HasTreasureEightStack)
        {
            foreach (var hint in TreasureEightStackHints(position)) yield return hint;
            yield break;
        }
        var party = _actors.Values.Where(a => (HasTreasureSpreadTargets ? treasureParty.Contains(a.Id) : a.PartySlot >= 0) && !a.Dead).ToArray();
        var spreads = jobQuestSpreads.Where(m => !m.Target.Dead).ToArray();
        if (spreads.Any(m => m.Target.Id == PlayerId && !m.Removed))
            yield return "Spread!";
        if (spreads.Any(m => (m.Target.Id != PlayerId || m.Removed)
            && NativeEnemyDistanceSquared(position, QuantizedDungeonPosition(m.Target.Position)) <= MathF.Pow(JobQuestSpreadRadius(m.Cast.Rule.Action), 2)))
            yield return "GTFO from spreads!";
        var stacks = jobQuestStacks.Where(m => !m.Target.Dead).ToArray();
        var count = stacks.Count(m => NativeEnemyDistanceSquared(position, QuantizedDungeonPosition(m.Target.Position)) <= MathF.Pow(JobQuestStackRadius(m.Cast.Rule.Action), 2));
        if (count > 0 || stacks.Any(m => 1 + party.Count(a => (a.Id != m.Target.Id || m.Removed)
            && NativeEnemyDistanceSquared(a.Position, QuantizedDungeonPosition(m.Target.Position)) <= MathF.Pow(JobQuestStackRadius(m.Cast.Rule.Action), 2)) < (m.Cast.Rule.Action == 16134 ? 1 : 2)))
            yield return "Stack!";
    }

    private ulong JobQuestPriorityTarget(ulong current, Vector3 position)
    {
        int Priority(DungeonActor actor)
        {
            if ((IsIo || IsSteel || IsGamboling || IsRaisingSword) && (MathF.Abs(actor.Position.Y - position.Y) > 12
                || actor.NameId != 541 && actor.Hp is { } hp && (long)hp + (actor.IncomingEffects?.Sum(e => (long)e.HpDelta) ?? 0) <= 1))
                return -2;
            if (territory == JobQuestCatalog.ORPHANS_AND_BROKEN_BLADE && actor.Oid == 0x1C60) return 5;
            return actor.AggroPlayer || actor.InCombat && _actors.GetValueOrDefault(actor.TargetId)?.PartySlot >= 0 ? 0 : -3;
        }
        return _actors.Values.Where(a => Active != null && a.IsEnemy == true && a.Targetable && !a.Dead
                && a.ObjectIndex is { } index && index < 200 && (index & 1) == 0 && Priority(a) >= 0)
            .OrderByDescending(Priority).ThenByDescending(a => a.Id == current)
            .ThenBy(a => NativeEnemyDistanceSquared(a.Position, position)).Select(a => a.Id).FirstOrDefault();
    }
}
