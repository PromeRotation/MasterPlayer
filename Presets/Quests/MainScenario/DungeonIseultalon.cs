using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsIseultalon => territory == MainScenarioQuestCatalog.ISEULTALON;
    private readonly List<DungeonQuestTower> electrocutionTowers = [];
    private readonly List<DungeonQuestTower> electrocution2Towers = [];
    private readonly List<(DungeonActor Target, bool Removed)> thunderboltSpreads = [];
    private readonly Dictionary<ulong, DungeonPendingCast> iseultalonStarted = [];
    private readonly Dictionary<ulong, uint> iseultalonOids = [];
    private readonly Dictionary<ulong, DungeonActor> iseultalonActorInputs = [];
    private readonly Dictionary<ulong, Vector3> electrocutionDestroyPositions = [];
    private readonly HashSet<ulong> iseultalonCompleted = [];
    private DateTime? deathRayActivation;
    private int electrocutionNumCasts;
    private long electrocutionGeneration;
    private bool iseultalonWiping;
    internal int ElectrocutionNumCasts => electrocutionNumCasts;
    internal IReadOnlyList<DungeonQuestTower> ElectrocutionTowers => electrocutionTowers;
    internal IReadOnlyList<DungeonQuestTower> Electrocution2Towers => electrocution2Towers;

    private void IseultalonInput()
    {
        if (!IsIseultalon) return;
        // Feed destroys in persistent world order before moving any survivors in this frame.
        foreach (var id in iseultalonActorInputs.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
        {
            if (_casts.TryGetValue(id, out var cast) && cast.Rule.Action is 14609 or 14610)
                electrocutionDestroyPositions[id] = ElectrocutionPosition(cast, iseultalonActorInputs);
            // A destroyed cast finishes before any surviving/new actor starts in this raw frame.
            Finish(id);
            iseultalonActorInputs.Remove(id);
        }
        foreach (var id in iseultalonOids.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
        {
            iseultalonOids.Remove(id);
            iseultalonCompleted.Remove(id);
        }
        foreach (var actor in _actors.Values.ToArray())
        {
            if (iseultalonOids.TryGetValue(actor.Id, out var oid)) _actors[actor.Id] = actor with { Oid = oid };
            else iseultalonOids[actor.Id] = actor.Oid;
            if (actor.Oid == 0x252C && (Active != null && actor.Id == PrimaryId || actor.Targetable && actor.InCombat)
                && (actor.Dead || actor.Hp == 0)) iseultalonCompleted.Add(actor.Id);
            iseultalonActorInputs[actor.Id] = _actors[actor.Id];
        }
    }

    private void IseultalonObserve()
    {
        if (!IsIseultalon || Active == null) return;
        for (var i = 0; i < thunderboltSpreads.Count; ++i)
        {
            var mark = thunderboltSpreads[i];
            var target = !mark.Removed ? _actors.GetValueOrDefault(mark.Target.Id) : null;
            thunderboltSpreads[i] = (target ?? mark.Target, mark.Removed || target == null);
        }
    }

    private Vector3 ElectrocutionPosition(DungeonPendingCast cast, IReadOnlyDictionary<ulong, DungeonActor>? actors = null)
    {
        actors ??= _actors;
        return cast.TargetId == cast.Source ? actors.GetValueOrDefault(cast.Source)?.Position ?? cast.SourcePosition ?? cast.Location
            : actors.GetValueOrDefault(cast.TargetId)?.Position ?? cast.Location;
    }

    private void IseultalonCast(DungeonPendingCast cast)
    {
        if (!IsIseultalon) return;
        var correction = iseultalonStarted.TryGetValue(cast.Source, out var previous)
            && previous.Rule.Action == cast.Rule.Action && previous.StartedAt == cast.StartedAt;
        if (cast.Rule.Action == 14602 && !correction) deathRayActivation = _now.AddSeconds(6.1d);
        var towers = cast.Rule.Action switch { 14609 => electrocutionTowers, 14981 => electrocution2Towers, _ => null };
        if (towers != null)
        {
            var index = correction ? towers.FindIndex(t => ReferenceEquals(t.Cast, previous)) : -1;
            if (correction && index < 0)
            {
                iseultalonStarted[cast.Source] = cast;
                return;
            }
            var position = cast.Rule.Action == 14609 ? ElectrocutionPosition(cast) : cast.Location;
            var forbiddenSlot = -1;
            if (index >= 0)
                forbiddenSlot = towers[index].ForbiddenSlot;
            else if (cast.Rule.Action == 14609 && electrocutionNumCasts > 0)
                forbiddenSlot = _actors.GetValueOrDefault(PlayerId)?.PartySlot ?? -1;
            var tower = new DungeonQuestTower(index >= 0 ? towers[index].Id : ++electrocutionGeneration, cast, position,
                cast.Rule.Action == 14609 ? default : cast.FinishAt, forbiddenSlot);
            if (index >= 0) towers[index] = tower;
            else towers.Add(tower);
        }
        iseultalonStarted[cast.Source] = cast;
    }

    private void IseultalonFinish(DungeonPendingCast cast)
    {
        if (!IsIseultalon) return;
        if (cast.Rule.Action is 14609 or 14610)
        {
            var position = electrocutionDestroyPositions.Remove(cast.Source, out var destroyedPosition)
                ? destroyedPosition : ElectrocutionPosition(cast);
            electrocutionTowers.RemoveAll(t => MathF.Abs(t.Position.X - position.X) <= 1 && MathF.Abs(t.Position.Z - position.Z) <= 1);
            ++electrocutionNumCasts;
        }
        if (cast.Rule.Action == 14981)
        {
            var index = electrocution2Towers.FindIndex(t => t.Cast.Source == cast.Source);
            if (index >= 0) electrocution2Towers.RemoveAt(index);
        }
        iseultalonStarted.Remove(cast.Source);
    }

    private void IseultalonIcon(ulong actor)
    {
        if (IsIseultalon && Active != null && _actors.TryGetValue(actor, out var target))
            thunderboltSpreads.Add((target, false));
    }

    private IEnumerable<DungeonAmbientAoe> IseultalonAoes()
    {
        if (!IsIseultalon || Active == null) yield break;
        if (deathRayActivation is { } activation && _actors.GetValueOrDefault(PrimaryId) is { } primary)
            yield return new("iseultalon/death-ray", "Death Ray", RayRectangleTemplate.Symmetric(22, 40),
                primary.Position, primary.Rotation, Activation: activation);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 14603))
            yield return new($"iseultalon/narrow-ray/{cast.Source}", "Death Ray", RayRectangleTemplate.Symmetric(2, 40),
                cast.Location, cast.Rotation, Activation: cast.FinishAt, Color: new(1, 1, 0, 1));
        foreach (var aoe in QuestTowerAoes("iseultalon/tower", electrocutionTowers)) yield return aoe;
        foreach (var aoe in QuestTowerAoes("iseultalon/tower2", electrocution2Towers, true)) yield return aoe;
        var party = QuestTowerParty;
        var spreads = thunderboltSpreads.Where(s => !s.Target.Dead).ToArray();
        foreach (var (mark, index) in spreads.Select((s, i) => (s, i)))
        {
            var position = QuantizedDungeonPosition(mark.Target.Position);
            yield return new($"iseultalon/spread/{index}/display", "Thunderbolt", new CircleTemplate(5), position,
                Navigate: false, OutlineOnly: true);
            if (party.Length < 2) continue;
            if (mark.Removed || mark.Target.Id != PlayerId)
                yield return new($"iseultalon/spread/{index}/avoid", "Thunderbolt", new CircleTemplate(6), position,
                    Display: false, Activation: default(DateTime));
            else
                foreach (var member in party.Where(a => !spreads.Any(s => !s.Removed && s.Target.Id == a.Id)))
                    yield return new($"iseultalon/spread/{index}/party/{member.Id}", "Thunderbolt", new CircleTemplate(6),
                        QuantizedDungeonPosition(member.Position), Display: false, Activation: default(DateTime));
        }
    }

    private IEnumerable<DungeonDamagePrediction> IseultalonDamage()
    {
        if (!IsIseultalon || Active == null) yield break;
        foreach (var prediction in QuestTowerDamage(electrocutionTowers)) yield return prediction;
        foreach (var mark in thunderboltSpreads.Where(s => !s.Target.Dead).DistinctBy(s => s.Target.Id))
            if (_actors.GetValueOrDefault(mark.Target.Id) is { PartySlot: >= 0 } actor)
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, default, "Raidwide");
    }

    private IEnumerable<string> IseultalonHints(Vector3? position)
    {
        if (!IsIseultalon || Active == null || position is not { } player) yield break;
        if (IseultalonAoes().Any(a => (a.Id == "iseultalon/death-ray" || a.Id.StartsWith("iseultalon/narrow-ray/"))
            && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player)))
            yield return "GTFO from aoe!";
        foreach (var hint in QuestTowerHints(electrocutionTowers, player)) yield return hint;
        foreach (var hint in QuestTowerHints(electrocution2Towers, player)) yield return hint;
        var spreads = thunderboltSpreads.Where(s => !s.Target.Dead).ToArray();
        if (spreads.Any(s => !s.Removed && s.Target.Id == PlayerId)) yield return "Spread!";
        if (spreads.Any(s => (s.Removed || s.Target.Id != PlayerId)
            && NativeEnemyDistanceSquared(player, QuantizedDungeonPosition(s.Target.Position)) <= 25))
            yield return "GTFO from spreads!";
    }

    private void IseultalonDirector(uint update)
    {
        if (!IsIseultalon || update is not (0x40000005 or 0x40000011)) return;
        iseultalonWiping = update == 0x40000005;
        if (!iseultalonWiping) return;
        ResetFight();
        iseultalonCompleted.Clear();
    }

    private void ResetIseultalon()
    {
        electrocutionTowers.Clear();
        electrocution2Towers.Clear();
        thunderboltSpreads.Clear();
        iseultalonStarted.Clear();
        electrocutionDestroyPositions.Clear();
        deathRayActivation = null;
        electrocutionNumCasts = 0;
    }
}
