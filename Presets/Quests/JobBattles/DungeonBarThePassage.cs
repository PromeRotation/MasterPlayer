using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsBarPassage => territory == JobQuestCatalog.BAR_THE_PASSAGE;
    internal bool IsBarPassageP1 => IsBarPassage && Active?.BossOid == 0x465D;
    internal bool IsBarPassageP2 => IsBarPassage && Active?.BossOid == 0x4660;
    internal bool IsBarPassageP3 => IsBarPassage && Active?.BossOid == 0x466A;
    internal uint CurrentCfcId { get; set; }
    private readonly Dictionary<ulong, DungeonActor> barPassageInputs = [];
    private readonly HashSet<ulong> barPassageCompleted = [];
    private readonly List<ulong> barPassageLoadOrder = [];
    private readonly List<ulong> barPassagePending = [];
    private readonly HashSet<ulong> barPassageStarted = [];
    private readonly Dictionary<ulong, DungeonActor> barPassageModules = [];
    private ulong barPassagePartyZero;
    private const float BAR_PASSAGE_LOAD_DISTANCE_SQUARED = 500 * 500;
    private readonly HashSet<(ulong Source, uint Action, uint Sequence, DateTime At)> barP2Effects = [];
    private bool barPassageWiping;
    private DungeonActor? barPassagePrimary;
    private (ulong Source, Vector3 Position, float Rotation, DateTime Activation)? barP2Shield;
    private int barP2Hits;
    internal int BarP2Hits => barP2Hits;
    internal bool BarP2SecondArena { get; private set; }
    private bool BarPassageCanPull(DungeonActor actor) => !barPassageWiping && barPassageStarted.Contains(actor.Id);
    private bool BarPassageStageStarted(uint oid) => IsBarPassage
        && barPassageStarted.Any(id => barPassageModules.GetValueOrDefault(id)?.Oid == oid);
    private DungeonDefinition? BarPassageCastDefinition(uint action) => Definitions.FirstOrDefault(d =>
        BarPassageStageStarted(d.BossOid) && d.Casts.Any(c => c.Action == action))
        ?? Definitions.FirstOrDefault(d => BarPassageStageStarted(d.BossOid));
    private bool BarPassageCastVisible(DungeonPendingCast cast) => !IsBarPassage
        || Active?.Casts.Any(c => c.Action == cast.Rule.Action) == true;

    private void BarPassageInput()
    {
        if (!IsBarPassage) return;
        var previousPlayer = _actors.ContainsKey(barPassagePartyZero) ? barPassageInputs.GetValueOrDefault(barPassagePartyZero) : null;
        foreach (var id in barPassageInputs.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
        {
            Finish(id);
            barPassagePending.Remove(id);
            barPassageCompleted.Remove(id);
            treasureObservedCasts.Remove(id);
        }
        foreach (var actor in _actors.Values.ToArray())
        {
            if (barPassageInputs.TryGetValue(actor.Id, out var previous))
                _actors[actor.Id] = actor with { Oid = previous.Oid };
            else
            {
                barPassageCompleted.Remove(actor.Id);
                if (actor.Oid is 0x465D or 0x4660 or 0x466A && !barPassageLoadOrder.Contains(actor.Id))
                {
                    // Feed creates actors before applying this frame's party table.
                    // Loaded siblings block the same OID, while pending siblings do not.
                    if (previousPlayer != null && Vector3.DistanceSquared(previousPlayer.Position, actor.Position) <= BAR_PASSAGE_LOAD_DISTANCE_SQUARED
                        && !barPassageLoadOrder.Any(id => BarPassageLoadedOid(id) == actor.Oid))
                    {
                        barPassageLoadOrder.Add(actor.Id);
                        barPassageModules[actor.Id] = actor;
                    }
                    else
                        barPassagePending.Add(actor.Id);
                }
            }
            barPassageInputs[actor.Id] = _actors[actor.Id];
            if (actor.Id == barPassagePartyZero) previousPlayer = _actors[actor.Id];
        }
        barPassagePartyZero = _actors.Values.FirstOrDefault(a => a.PartySlot == 0)?.Id ?? 0;
        if (_actors.GetValueOrDefault(barPassagePartyZero) is { } currentPlayer)
        {
            // Source Manager promotes pending modules backwards before updating loaded ones.
            for (var i = barPassagePending.Count - 1; i >= 0; --i)
            {
                var id = barPassagePending[i];
                if (!_actors.TryGetValue(id, out var candidate) || candidate.Dead)
                {
                    barPassagePending.RemoveAt(i);
                    barPassageCompleted.Add(id);
                }
                else if (Vector3.DistanceSquared(currentPlayer.Position, candidate.Position) <= BAR_PASSAGE_LOAD_DISTANCE_SQUARED
                    && !barPassageLoadOrder.Any(loaded => BarPassageLoadedOid(loaded) == candidate.Oid))
                {
                    barPassagePending.RemoveAt(i);
                    barPassageLoadOrder.Add(id);
                    barPassageModules[id] = candidate;
                }
            }
            foreach (var id in barPassageLoadOrder.ToArray())
            {
                var current = _actors.GetValueOrDefault(id);
                if (current != null) barPassageModules[id] = current;
                var module = barPassageModules[id];
                if (!barPassageWiping && !barPassageStarted.Contains(id) && current != null && (module.Oid == 0x466A ? BarP3Pull() : current.Targetable && current.InCombat))
                {
                    barPassageStarted.Add(id);
                    if (module.Oid == 0x465D) AlphaStarted();
                }
                if (!barPassageWiping && barPassageStarted.Contains(id) && (module.Oid == 0x466A ? current == null || module.Dead || module.Hp == 0 : !module.Targetable || CurrentCfcId != 1016))
                {
                    BarPassageComplete(id);
                    continue;
                }
                if (!barPassageStarted.Contains(id)
                    && (current == null || Vector3.DistanceSquared(currentPlayer.Position, module.Position) > BAR_PASSAGE_LOAD_DISTANCE_SQUARED))
                {
                    barPassageLoadOrder.Remove(id);
                    barPassageModules.Remove(id);
                    if (current != null) barPassagePending.Add(id);
                }
            }
        }
        if (!barPassageLoadOrder.Any(id => BarPassageLoadedOid(id) == 0x466A)) barP3Mycotenders = null;
        foreach (var id in barPassageInputs.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
            barPassageInputs.Remove(id);
        if (Active != null && _actors.GetValueOrDefault(PrimaryId) is { } primary && primary.Oid == Active.BossOid)
            barPassagePrimary = primary;
    }

    private uint BarPassageLoadedOid(ulong id) => barPassageModules.GetValueOrDefault(id)?.Oid ?? 0;

    private IEnumerable<DungeonActor> BarPassageCandidates => _actors.Values.Concat(barPassageModules.Values
        .Where(a => barPassageStarted.Contains(a.Id) && !_actors.ContainsKey(a.Id)));

    private int BarPassageOrder(DungeonActor actor) => barPassageLoadOrder.IndexOf(actor.Id);

    private void BarPassageStarted(DungeonActor primary)
    {
        if (IsBarPassage) barPassagePrimary = primary;
    }

    private void BarPassageObserve()
    {
        if (!IsBarPassage || Active == null) return;
        if (!barPassageStarted.Contains(PrimaryId)) BarPassageUnselect();
        else if (IsBarPassageP3)
            Arena = JobQuestCatalog.BarPassageArena(barPassagePrimary?.Position.Y ?? 0, BarP3SmallArena);
        else if (IsBarPassageP2 && BarP2SecondArena)
            Arena = new RectangleGeometry(new(0, barPassagePrimary?.Position.Y ?? 0, 57), 0, 69.2f, 48);
    }

    private void BarPassageUnselect()
    {
        Active = null;
        PrimaryId = 0;
        Arena = null;
        barPassagePrimary = null;
    }

    private void BarPassageComplete(ulong id)
    {
        var oid = barPassageModules[id].Oid;
        barPassageCompleted.Add(id);
        barPassageStarted.Remove(id);
        barPassageLoadOrder.Remove(id);
        barPassageModules.Remove(id);
        foreach (var cast in _casts.Values.Where(c => Definitions.First(d => d.BossOid == oid).Casts.Any(r => r.Action == c.Rule.Action)).ToArray())
            _casts.Remove(cast.Source);
        if (oid == 0x465D) ResetAlpha();
        else if (oid == 0x4660) ResetBarP2();
        else ResetBarP3();
        if (PrimaryId == id) BarPassageUnselect();
    }

    private void BarP2Map(uint index, uint state)
    {
        if (!BarPassageStageStarted(0x4660) || index != 3 || state != 0x00020001) return;
        BarP2SecondArena = true;
        if (IsBarPassageP2)
            Arena = new RectangleGeometry(new(0, barPassagePrimary?.Position.Y ?? 0, 57), 0, 69.2f, 48);
    }

    internal bool UsesBarP2NativeEffect(uint action) => BarPassageStageStarted(0x4660) && action is 40866 or 40907 or 40908;

    private void BarP2Effect(ulong source, uint action, uint sequence, DateTime at)
    {
        if (!BarPassageStageStarted(0x4660) || !_actors.TryGetValue(source, out var caster)
            || !barP2Effects.Add((source, action, sequence, sequence == 0 ? at : default))) return;
        if (action == 40866)
            barP2Shield = (source, caster.Position, caster.Rotation + MathF.PI, default);
        else if (action is 40907 or 40908 && ++barP2Hits == 5)
            barP2Shield = null;
    }

    private IEnumerable<DungeonAmbientAoe> BarP2Aoes()
    {
        if (!IsBarPassageP2 || Active == null || barP2Shield is not { } shield) yield break;
        // The existing MP bridge converts this inverted cone into a persistent safe goal.
        yield return new("bar-p2/shield", "Ice Aegis", new FanTemplate(5, MathF.PI * 2 / 3),
            shield.Position, shield.Rotation, Safe: true, Activation: shield.Activation, GoalPriority: 1);
    }

    internal bool BarP2ShieldUnsafe(Vector3 position)
    {
        if (barP2Shield is not { } shield) return false;
        var delta = position - shield.Position;
        delta.Y = 0;
        var distanceSquared = delta.LengthSquared();
        if (distanceSquared == 0 || distanceSquared > 25) return true;
        var forward = new Vector3(MathF.Sin(shield.Rotation), 0, MathF.Cos(shield.Rotation));
        return Vector3.Dot(delta / MathF.Sqrt(distanceSquared), forward) < (float)Math.Cos(60 * (MathF.PI / 180));
    }

    private IEnumerable<string> BarP2Hints(Vector3? player)
    {
        if (!IsBarPassageP2 || Active == null) yield break;
        if (barP2Shield != null) yield return "Go behind shield!";
        if (player is { } p && _casts.Values.Any(c => BarPassageCastVisible(c) && c.Rule.Shape != null && c.Rule.Shape.Resolve(c.Location, c.Rotation, null).Contains(p)))
            yield return "GTFO from aoe!";
    }

    private void ResetBarPassage()
    {
        barPassagePrimary = null;
        barPassageStarted.Clear();
        ResetBarP2();
        ResetBarP3();
        barP3Mycotenders = null;
    }

    private void ResetBarP2()
    {
        barP2Shield = null;
        barP2Hits = 0;
        barP2Effects.Clear();
        BarP2SecondArena = false;
    }

    private void BarPassageDirector(uint command)
    {
        if (!IsBarPassage) return;
        if (command == 0x40000005)
        {
            barPassageWiping = true;
            // Manager ForceUnload removes loaded modules. Recommence only releases the
            // gate; an unchanged actor is not an ActorAdded event and cannot recreate one.
            barPassageCompleted.UnionWith(barPassageLoadOrder);
            barPassageLoadOrder.Clear();
            barPassageModules.Clear();
            ResetFight();
        }
        else if (command == 0x40000011)
            barPassageWiping = false;
    }
}
