using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed partial class SimpleHuntEncounter
{
    private readonly Dictionary<ulong, HuntArchAethereaterState> aethereaters = [];
    private readonly Dictionary<ulong, HuntSansheyaState> sansheyas = [];
    private readonly Dictionary<ulong, HuntUrnaVariabilisState> urnas = [];
    private IEnumerable<HuntStayMoveState> StayMoveStates => aethereaters.Values.SelectMany(s => new[] { s.Heat, s.Cold })
        .Concat(sansheyas.Values.Select(s => s.Boiling));
    private bool FinalHuntOwnerActive(uint oid) => oid switch
    {
        0x4544 => aethereaters.Count > 0,
        0x43DD => sansheyas.Count > 0,
        0x416F => urnas.Count > 0,
        0x281E => aglaopes.Count > 0,
        _ => false
    };
    private HuntDefinition? FinalHuntDefinition(uint action) => new uint[] { 0x4544, 0x43DD, 0x416F, 0x281E }
        .Where(FinalHuntOwnerActive).Select(SimpleHuntCatalog.Find).FirstOrDefault(d => d!.Casts.Any(c => c.Action == action));
    private bool FinalHuntAction(uint action) => FinalHuntDefinition(action) != null;
    private void InitializeFinalHunts()
    {
        InitializeAglaope();
        foreach (var id in aethereaters.Keys.Where(id => !active.Contains(id)).ToArray()) aethereaters.Remove(id);
        foreach (var id in sansheyas.Keys.Where(id => !active.Contains(id)).ToArray()) sansheyas.Remove(id);
        foreach (var id in urnas.Keys.Where(id => !active.Contains(id)).ToArray()) urnas.Remove(id);
        foreach (var actor in ActiveActors)
        {
            var added = actor.Oid switch
            {
                0x4544 => aethereaters.TryAdd(actor.Id, new(actor.Id)),
                0x43DD => sansheyas.TryAdd(actor.Id, new(actor.Id)),
                0x416F => urnas.TryAdd(actor.Id, new(actor.Id)),
                _ => false
            };
            if (!added) continue;
            initializingComponentOwners.Add(actor.Id);
            if (aethereaters.TryGetValue(actor.Id, out var arch))
            {
                arch.Heat.SetParty(partyMembers);
                arch.Cold.SetParty(partyMembers);
            }
            if (sansheyas.TryGetValue(actor.Id, out var sansheya)) sansheya.Boiling.SetParty(partyMembers);
            if (urnas.TryGetValue(actor.Id, out var urna)) urna.SetParty(partyMembers);
        }
    }
    private void UpdateFinalParty()
    {
        foreach (var state in StayMoveStates) state.SetParty(partyMembers);
        foreach (var state in urnas.Values) state.SetParty(partyMembers);
    }
    private void ObserveFinalMissing(IReadOnlySet<ulong> ids)
    {
        foreach (var state in StayMoveStates) state.RemoveMissing(ids);
        foreach (var state in urnas.Values) state.RemoveMissing(ids);
    }
    private void ObserveFinalActor(ulong actor, DateTime now)
    {
        foreach (var state in StayMoveStates) state.Observe(actors, actor, now);
        foreach (var state in urnas.Values) state.Observe(actors, actor, now);
    }
    internal void FinalHuntStatus(ulong actor, uint status, ReplayStatus? value, ulong source, DateTime at)
    {
        if (!actors.TryGetValue(actor, out var current)) return;
        if (status is 4141 or 960)
            foreach (var state in aethereaters.Values) state.Heat.Status(current, status, value, source, at);
        if (status is 4142 or 2540)
            foreach (var state in aethereaters.Values) state.Cold.Status(current, status, value, source, at);
        if (status is 4140 or 960)
            foreach (var state in sansheyas.Values) state.Boiling.Status(current, status, value, source, at);
        if (status is 4071 or 4072)
            foreach (var state in urnas.Values) state.Status(current, status, value, source, at);
    }
    private void StartFinalComponents(HuntPendingCast cast)
    {
        if (cast.Definition.Oid == 0x281E)
            foreach (var (owner, state) in aglaopes)
                if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                    state.Start(cast);
        if (cast.Definition.Oid == 0x4544)
            foreach (var (owner, state) in aethereaters)
                if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                    state.Start(cast, actors);
        if (cast.Definition.Oid == 0x43DD)
            foreach (var (owner, state) in sansheyas)
                if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                    state.Start(cast, actors);
        if (cast.Definition.Oid == 0x416F)
            foreach (var (owner, state) in urnas)
                if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                    state.Start(cast, actors);
    }
    private void CorrectFinalComponent(ulong owner, HuntPendingCast previous, HuntPendingCast updated)
    {
        if (aglaopes.TryGetValue(owner, out var aglaope)) aglaope.Correct(previous, updated);
        if (aethereaters.TryGetValue(owner, out var arch)) arch.Correct(previous, updated, actors);
        if (sansheyas.TryGetValue(owner, out var sansheya)) sansheya.Correct(previous, updated, actors);
        if (urnas.TryGetValue(owner, out var urna)) urna.Correct(previous, updated, actors);
    }
    private static bool FinalHuntFinishAction(uint action) => HuntArchAethereaterState.ComboFinish(action)
        || HuntUrnaVariabilisState.Magnetic(action) || HuntUrnaVariabilisState.CircleFinish(action)
        || action is 39515 or 39285 or 39296 or 39115 or 18056;
    private void FinishFinalComponents(ulong source, uint action, ulong target)
    {
        foreach (var (owner, state) in aglaopes)
            if (!initializingComponentOwners.Contains(owner)) state.Finish(source, action);
        foreach (var (owner, state) in aethereaters)
            if (!initializingComponentOwners.Contains(owner)) state.Finish(action, target);
        foreach (var (owner, state) in sansheyas)
            if (!initializingComponentOwners.Contains(owner)) state.Finish(source, action, target);
        foreach (var (owner, state) in urnas)
            if (!initializingComponentOwners.Contains(owner)) state.Finish(action, target);
    }
    private void UpdateFinalComponents()
    {
        foreach (var state in aglaopes.Values) state.Update(aglaopePending);
        foreach (var state in aethereaters.Values) state.Update(actors);
        foreach (var state in sansheyas.Values) state.Update(actors);
        foreach (var state in urnas.Values) state.Update(actors);
    }
    internal IEnumerable<HuntPendingCast> NavigationAreas(ReplayActorState? player)
        => Areas.Where(c => !urnas.TryGetValue(c.Source, out var state) || state.UseOrdinaryNavigation(player));
    private IEnumerable<HuntPendingCast> UrnaNavigation(ReplayActorState player)
        => urnas.SelectMany(p => p.Value.Navigation(player, actors[p.Key]));
    private IEnumerable<string> FinalHuntHints(ReplayActorState player)
        => StayMoveStates.SelectMany(s => s.Hints(player, observedTime ?? DateTime.UnixEpoch));
    private void RemoveFinalActor(ulong actor)
    {
        foreach (var state in aethereaters.Values) state.Remove(actor);
        foreach (var state in sansheyas.Values) state.Remove(actor);
        foreach (var state in urnas.Values) state.Remove(actor);
        aethereaters.Remove(actor);
        sansheyas.Remove(actor);
        urnas.Remove(actor);
        aglaopes.Remove(actor);
        aglaopePending.Remove(actor);
    }
    private void ResetFinalHunts()
    {
        aethereaters.Clear();
        sansheyas.Clear();
        urnas.Clear();
        aglaopes.Clear();
        aglaopePending.Clear();
        AglaopePayloadMissing = false;
    }
}
