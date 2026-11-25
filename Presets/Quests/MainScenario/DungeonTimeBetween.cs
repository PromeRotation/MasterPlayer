using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsTimeBetween => territory == MainScenarioQuestCatalog.TIME_BETWEEN;
    private DungeonActor? timePrimary;
    private bool timePrimaryRemoved;
    private bool timeWiping;
    private readonly HashSet<ulong> timeCompleted = [];
    private readonly HashSet<ulong> timeKnownFlames = [];
    private readonly Dictionary<ulong, uint> timeActorOids = [];
    private readonly List<(ulong Source, int Generation, Vector3 Origin, DateTime Activation)> timeFlames = [];
    private int timeFlameGeneration;
    internal int TimeBetweenPhase { get; private set; }
    internal IReadOnlyList<(ulong Source, int Generation, Vector3 Origin, DateTime Activation)> TimeBetweenFlames => timeFlames;

    private void TimeBetweenInput()
    {
        if (!IsTimeBetween) return;
        // The source feed creates/destroys actors from complete object frames, not PR's
        // independent object-list events. Only an observed absence proves a new generation.
        foreach (var id in timeActorOids.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
        {
            timeActorOids.Remove(id);
            timeKnownFlames.Remove(id);
            timeCompleted.Remove(id);
        }
        foreach (var actor in _actors.Values.ToArray())
        {
            if (timeActorOids.TryGetValue(actor.Id, out var oid))
                _actors[actor.Id] = actor with { Oid = oid };
            else
                timeActorOids[actor.Id] = actor.Oid;
        }
    }

    private void TimeBetweenStarted(DungeonActor primary)
    {
        if (!IsTimeBetween) return;
        timePrimary = primary;
        timePrimaryRemoved = false;
    }

    private void TimeBetweenObserve()
    {
        if (!IsTimeBetween) return;
        timeCompleted.RemoveWhere(id => _actors.GetValueOrDefault(id)?.Oid != 0x1A36);
        if (Active == null) return;
        if (_actors.GetValueOrDefault(PrimaryId) is { Oid: 0x1A36 } primary)
        {
            // BossModuleManager.ActorAdded rebinds a still-loaded module to the same ID/OID.
            timePrimary = primary;
            timePrimaryRemoved = false;
        }
        else
            timePrimaryRemoved = true;
        if (TimeBetweenPhase == 0 && timePrimary?.Targetable == false)
        {
            TimeBetweenPhase = 1;
            // The source recreates all four common components on phase entry.
            _casts.Clear();
            // Input receipts survive component replacement: a late P1 packet is not a new P2 cast.
            timeFlames.Clear();
            timeKnownFlames.Clear();
        }
        if (TimeBetweenPhase == 1 && !_actors.Values.Any(a => a.Oid == 0x1EBC))
        {
            timeCompleted.Add(PrimaryId);
            ResetFight();
            return;
        }
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1CED))
            if (timeKnownFlames.Add(actor.Id))
                TimeBetweenAddFlame(actor.Id, actor.Position, _now);
    }

    private void TimeBetweenAddFlame(ulong source, Vector3 origin, DateTime createdAt)
    {
        // Source AOEInstance omits ActorID (zero), so nonzero cast/finish IDs never clear or retime it.
        // Preserve that behavior while retaining the actual source/generation for model diagnostics.
        timeFlames.Add((source, ++timeFlameGeneration, QuantizedDungeonPosition(origin), createdAt.AddSeconds(7d)));
    }

    private IEnumerable<DungeonAmbientAoe> TimeBetweenAoes()
    {
        if (!IsTimeBetween || Active == null) yield break;
        foreach (var flame in timeFlames)
            yield return new($"time/flame/{flame.Generation}", "Lightless Flame", new CircleTemplate(11),
                flame.Origin, Activation: flame.Activation);
    }

    private void TimeBetweenDirector(uint update)
    {
        if (!IsTimeBetween || update is not (0x40000005 or 0x40000011)) return;
        timeWiping = update == 0x40000005;
        if (!timeWiping) return;
        ResetFight();
        timeCompleted.Clear();
    }

    private void ResetTimeBetween()
    {
        timePrimary = null;
        timePrimaryRemoved = false;
        TimeBetweenPhase = 0;
        timeFlames.Clear();
        timeKnownFlames.Clear();
    }
}
