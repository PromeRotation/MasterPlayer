using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed partial class SimpleHuntEncounter
{
    private readonly Dictionary<ulong, ReplayActorState> actors = [];
    private readonly Dictionary<ulong, HuntPendingCast> casts = [];
    private readonly Dictionary<ulong, HuntPendingCast> rips = [];
    private readonly Dictionary<ulong, HuntComboState> combos = [];
    private readonly Dictionary<ulong, HuntChestThumpState> thumps = [];
    private readonly Dictionary<ulong, HuntPredictionState> predictions = [];
    private readonly Dictionary<ulong, HuntStoredSequenceState> storedSequences = [];
    private readonly Dictionary<ulong, HuntRotatingSequenceState> rotations = [];
    private readonly Dictionary<ulong, HuntSneezeState> sneezes = [];
    private readonly Dictionary<ulong, HuntMissileState> missiles = [];
    private readonly Dictionary<ulong, HuntCompressorState> compressors = [];
    private readonly Dictionary<ulong, HuntTargetState> targets = [];
    private readonly Dictionary<ulong, HuntAegeirosState> aegeiros = [];
    private readonly Dictionary<ulong, List<HuntStackState>> stacks = [];
    private readonly Dictionary<(ulong Source, uint Action), HuntStackJoinTemplate> stackJoins = [];
    private readonly Dictionary<ulong, HuntNarrowRiftState> narrowRifts = [];
    private readonly Dictionary<ulong, HuntExecutionState> executions = [];
    private readonly Dictionary<ulong, HuntTarchiaState> tarchias = [];
    private readonly Dictionary<ulong, HuntWhirlingOmenState> omens = [];
    private readonly Dictionary<ulong, HuntWingbladeState> wingblades = [];
    private readonly Dictionary<ulong, HuntMarchingSambaState> sambas = [];
    private readonly Dictionary<ulong, HuntForecastState> forecasts = [];
    private readonly Dictionary<ulong, HuntTygerState> tygers = [];
    private readonly Dictionary<ulong, HuntQueenHawkState> queens = [];
    private readonly Dictionary<ulong, HuntRaintrillerState> raintrillers = [];
    private readonly Dictionary<(ulong Source, uint Action), DateTime> rainFinishes = [];
    private readonly HashSet<(ulong Source, uint Action, long Order)> castComponentFinishes = [];
    private readonly Dictionary<ulong, HuntKerState> kers = [];
    private readonly Dictionary<ulong, HuntGunittState> gunitts = [];
    private readonly Dictionary<ulong, HuntAtticusState> atticus = [];
    private readonly Dictionary<ulong, HuntHeshualaState> heshualas = [];
    private readonly Dictionary<ulong, HuntIhnuxokiyState> ihnuxokiy = [];
    private readonly Dictionary<ulong, HuntNeyoozoteelState> neyoos = [];
    private readonly Dictionary<ulong, HuntNechucihoState> nechucihos = [];
    private readonly Dictionary<ulong, HuntPedantryState> pedantries = [];
    private readonly Dictionary<ulong, HuntIxtabState> ixtabs = [];
    private readonly Dictionary<ulong, HuntSlipperyState> slippery = [];
    private readonly Dictionary<ulong, HuntRebellionState> rebellions = [];
    private readonly Dictionary<ulong, HuntSugrivaState> sugrivas = [];
    private IEnumerable<HuntCastBait> CastBaits => pedantries.Values.SelectMany(p => p.Baits)
        .Concat(ixtabs.Values.SelectMany(p => p.Baits)).Concat(sugrivas.Values.SelectMany(s => s.Baits)).Concat(sansheyas.Values.SelectMany(s => s.Baits));
    private readonly Dictionary<(ulong Owner, ulong Source, long Order), HuntPendingCast> componentStarts = [];
    private readonly HashSet<ulong> initializingComponentOwners = [];
    private readonly Dictionary<(ulong Actor, uint Status), ulong> beeStatuses = [];
    private readonly Dictionary<(ulong Actor, uint Status), (DateTime At, ReplayStatus? Previous)> beeRemovals = [];
    private readonly Dictionary<(ulong Actor, uint Status), ReplayStatus> beeGains = [];
    private readonly Dictionary<(ulong Actor, uint Status), DateTime> beeWatermarks = [];
    private readonly HuntMarchState march = new();
    private readonly HuntMarchState threeSecondMarch = new(2161, 2162, 2163, 2164, 3);
    private readonly HuntMarchState poisonMarch = new(2161, 2162, 2163, 2164, 3, 5);
    private readonly Dictionary<ulong, bool> stackStatusEvents = [];
    private readonly Dictionary<ulong, DateTime> stackStatusWatermarks = [];
    private ReplayPartyMember[] partyMembers = [];
    private readonly Dictionary<ulong, List<HuntPendingCast>> delayedDamage = [];
    private readonly HashSet<ulong> hasted = [];
    private readonly HashSet<(ulong Source, uint Action, uint Sequence, DateTime At)> effects = [];
    private readonly Dictionary<(ulong Target, ulong Source), ReplayStatus?> pyreticEvents = [];
    private readonly Dictionary<(ulong Target, ulong Source), DateTime> pyreticWatermarks = [];
    private readonly Dictionary<(ulong Target, uint Status), ReplayStatus?> bossStatusEvents = [];
    private readonly Dictionary<(ulong Target, uint Status), DateTime> bossStatusWatermarks = [];
    private readonly Dictionary<ulong, (uint Action, DateTime StartedAt)> ended = [];
    private readonly HashSet<ulong> engaged = [];
    private readonly HashSet<ulong> active = [];
    private long castOrder;
    private DateTime? observedTime;
    private readonly HuntMisdirectionState misdirection = new();
    private IEnumerable<HuntStackState> StackStates => stacks.Values.SelectMany(s => s)
        .Concat(gunitts.Values.SelectMany(g => g.Stacks)).Concat(atticus.Values.SelectMany(a => a.Stacks))
        .Concat(neyoos.Values.SelectMany(n => n.Stacks)).Concat(ixtabs.Values.SelectMany(i => i.Stacks))
        .Concat(sugrivas.Values.SelectMany(s => s.Stacks)).Concat(aethereaters.Values.SelectMany(s => s.Stacks))
        .Concat(sansheyas.Values.SelectMany(s => s.Stacks)).Concat(urnas.Values.SelectMany(s => s.Stacks));
    internal IReadOnlyCollection<HuntPendingCast> Casts => casts.Values;
    internal IEnumerable<HuntPendingCast> Areas => casts.Values.Concat(rips.Values).Concat(combos.Values.SelectMany(c => c.Areas))
        .Concat(predictions.Values.SelectMany(p => p.Areas)).Concat(storedSequences.Values.SelectMany(s => s.Areas))
        .Concat(rotations.Values.SelectMany(r => r.Areas)).Concat(sneezes.Values.SelectMany(s => s.Areas))
        .Concat(missiles.Values.SelectMany(m => m.Areas)).Concat(compressors.Values.SelectMany(c => c.Areas))
        .Concat(aegeiros.SelectMany(p => p.Value.Areas(actors[p.Key], casts.GetValueOrDefault(p.Key))))
        .Concat(narrowRifts.SelectMany(p => p.Value.Areas(actors[p.Key], casts.GetValueOrDefault(p.Key))))
        .Concat(executions.Values.SelectMany(e => e.Areas))
        .Concat(omens.Values.SelectMany(o => o.Areas))
        .Concat(wingblades.Values.SelectMany(w => w.Areas))
        .Concat(sambas.Values.SelectMany(s => s.Areas))
        .Concat(forecasts.Values.SelectMany(f => f.Areas))
        .Concat(tygers.Values.SelectMany(t => t.Areas))
        .Concat(queens.Values.SelectMany(q => q.Areas))
        .Concat(raintrillers.Values.SelectMany(r => r.Areas))
        .Concat(atticus.Values.SelectMany(a => a.Areas))
        .Concat(heshualas.Values.SelectMany(h => h.Areas))
        .Concat(ihnuxokiy.Values.SelectMany(i => i.Areas))
        .Concat(neyoos.Values.SelectMany(n => n.Areas))
        .Concat(nechucihos.Values.SelectMany(n => n.Areas))
        .Concat(pedantries.Values.SelectMany(p => p.Areas))
        .Concat(ixtabs.Values.SelectMany(i => i.Areas))
        .Concat(slippery.Values.SelectMany(s => s.Areas))
        .Concat(rebellions.Values.SelectMany(r => r.Areas))
        .Concat(aethereaters.Values.SelectMany(s => s.Areas)).Concat(sansheyas.Values.SelectMany(s => s.Areas))
        .Concat(urnas.Values.SelectMany(s => s.Areas))
        .Concat(aglaopes.Values.SelectMany(s => s.Areas))
        .Concat(tarchias.Where(p => active.Contains(p.Key) || actors[p.Key].Targetable)
            .SelectMany(p => p.Value.Areas.Select(a => active.Contains(p.Key) ? a : a with { Risky = false })));
    internal IEnumerable<ReplayActorState> DrawingActors => ActiveActors
        .Concat(tarchias.Keys.Where(id => !active.Contains(id)).Select(id => actors[id]));
    internal IEnumerable<ReplayActorState> ActiveActors => active.Select(id => actors[id]);
    internal bool Active => active.Count > 0;

    internal void Synchronize(IEnumerable<ReplayActorState> observations, Func<string, bool> enabled, DateTime? now = null,
        IEnumerable<ReplayPartyMember>? party = null)
    {
        observedTime = now;
        var current = observations.ToArray();
        aglaopePending.Observe(current, now ?? DateTime.UnixEpoch);
        var currentIds = current.Select(a => a.Id).ToHashSet();
        ObserveFinalMissing(currentIds);
        foreach (var sugriva in sugrivas.Values) sugriva.RemoveMissing(currentIds);
        foreach (var id in actors.Keys.Where(id => !currentIds.Contains(id)).ToArray())
            foreach (var gunitt in gunitts.Values) gunitt.Remove(id, partyMembers);
        var existingGunitts = gunitts.Keys.ToHashSet();
        // Feed applies actor status changes before updating the roster; activation replays use the new roster.
        foreach (var gunitt in gunitts.Values)
            foreach (var actor in current) gunitt.Observe(actor, partyMembers, now);
        if (party != null) partyMembers = party.ToArray();
        foreach (var cast in casts.Values.Where(c => c.Observed && c.Definition.Oid is 0x457F or 0x2852
            && !currentIds.Contains(c.Source)).ToArray())
            FinishComponents(cast.Source, cast.Rule.Action, now);
        foreach (var cast in casts.Values.Where(c => (c.Observed || c.Definition.Oid is 0x4544 or 0x43DD or 0x416F or 0x281E) && c.Definition.Oid is 0x416D or 0x416E or 0x4233 or 0x298A or 0x2838 or 0x43DC or 0x28B6 or 0x35FC or 0x4544 or 0x43DD or 0x416F or 0x281E
            && !currentIds.Contains(c.Source)).ToArray())
            Finish(cast.Source, at: now);
        foreach (var cast in casts.Values.Where(c => !c.Observed && c.Definition.Oid == 0x416E
            && !currentIds.Contains(c.Source)).ToArray())
            foreach (var heshuala in heshualas.Values) heshuala.RetractUnobserved(cast);
        foreach (var actor in current)
            if (actors.TryGetValue(actor.Id, out var old) && old.Oid != actor.Oid) Remove(actor.Id, now);
        actors.Clear();
        foreach (var actor in current)
        {
            var merged = actor;
            foreach (var (key, pending) in bossStatusEvents.Where(p => p.Key.Target == actor.Id).ToArray())
            {
                var present = merged.Statuses.FirstOrDefault(s => s.Id == key.Status);
                if (pending == null && present == null || pending != null && (present == pending || pending.ExpiresAt <= now))
                    bossStatusEvents.Remove(key);
                else merged = WithStatus(merged, key.Status, pending);
            }
            actors[actor.Id] = merged;
        }
        foreach (var id in tarchias.Keys.Where(id => !actors.TryGetValue(id, out var actor) || actor.Dead || actor.Hp == 0
            || active.Contains(id) && !actor.InCombat || !enabled("Tarchia")).ToArray()) tarchias.Remove(id);
        foreach (var actor in actors.Values.Where(a => a.Oid == 0x2873 && !a.Dead && a.Hp != 0 && enabled("Tarchia")))
            if (!tarchias.ContainsKey(actor.Id)) tarchias[actor.Id] = new(actor, now ?? DateTime.UnixEpoch);
        engaged.RemoveWhere(id => !actors.TryGetValue(id, out var actor) || !Eligible(actor));
        foreach (var actor in actors.Values.Where(a => Eligible(a) && a.Targetable)) engaged.Add(actor.Id);
        active.Clear();
        active.UnionWith(engaged.Where(id => enabled(SimpleHuntCatalog.Find(actors[id].Oid)!.Name)));
        if (!ActiveActors.Any(a => a.Oid is 0x41FC or 0x2873 or 0x35CF)) misdirection.Clear();
        foreach (var key in bossStatusEvents.Keys.Where(k => !active.Contains(k.Target)).ToArray()) bossStatusEvents.Remove(key);
        foreach (var key in bossStatusWatermarks.Keys.Where(k => !active.Contains(k.Target)).ToArray()) bossStatusWatermarks.Remove(key);
        foreach (var id in rips.Keys.Where(id => !active.Contains(id)).ToArray()) rips.Remove(id);
        foreach (var id in combos.Keys.Where(id => !active.Contains(id)).ToArray()) combos.Remove(id);
        foreach (var id in thumps.Keys.Where(id => !active.Contains(id)).ToArray()) thumps.Remove(id);
        foreach (var id in predictions.Keys.Where(id => !active.Contains(id)).ToArray()) predictions.Remove(id);
        foreach (var id in storedSequences.Keys.Where(id => !active.Contains(id)).ToArray()) storedSequences.Remove(id);
        foreach (var id in rotations.Keys.Where(id => !active.Contains(id)).ToArray()) rotations.Remove(id);
        foreach (var id in sneezes.Keys.Where(id => !active.Contains(id)).ToArray()) sneezes.Remove(id);
        foreach (var id in missiles.Keys.Where(id => !active.Contains(id)).ToArray()) missiles.Remove(id);
        foreach (var id in compressors.Keys.Where(id => !active.Contains(id)).ToArray()) compressors.Remove(id);
        foreach (var id in targets.Keys.Where(id => !active.Contains(id)).ToArray()) targets.Remove(id);
        foreach (var id in aegeiros.Keys.Where(id => !active.Contains(id)).ToArray()) aegeiros.Remove(id);
        foreach (var id in stacks.Keys.Where(id => !active.Contains(id)
            && !(actors.ContainsKey(id) && ActiveActors.Any(a => a.Oid == 0x457F)
                && stacks[id].All(s => s.Cast?.Definition.Oid == 0x457F))).ToArray()) stacks.Remove(id);
        foreach (var key in stackJoins.Keys.Where(k => !stacks.ContainsKey(k.Source)).ToArray()) stackJoins.Remove(key);
        foreach (var id in narrowRifts.Keys.Where(id => !active.Contains(id)).ToArray()) narrowRifts.Remove(id);
        foreach (var id in executions.Keys.Where(id => !active.Contains(id)).ToArray()) executions.Remove(id);
        foreach (var id in omens.Keys.Where(id => !active.Contains(id)).ToArray()) omens.Remove(id);
        foreach (var id in wingblades.Keys.Where(id => !active.Contains(id)).ToArray()) wingblades.Remove(id);
        foreach (var id in sambas.Keys.Where(id => !active.Contains(id)).ToArray()) sambas.Remove(id);
        foreach (var id in forecasts.Keys.Where(id => !active.Contains(id)).ToArray()) forecasts.Remove(id);
        foreach (var id in tygers.Keys.Where(id => !active.Contains(id)).ToArray()) tygers.Remove(id);
        foreach (var id in queens.Keys.Where(id => !active.Contains(id)).ToArray()) queens.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x452B)) queens.TryAdd(actor.Id, new());
        foreach (var id in raintrillers.Keys.Where(id => !active.Contains(id)).ToArray()) raintrillers.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x457F)) raintrillers.TryAdd(actor.Id, new(actor.Id));
        foreach (var id in kers.Keys.Where(id => !active.Contains(id)).ToArray()) kers.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x35CF)) kers.TryAdd(actor.Id, new());
        foreach (var ker in kers.Values) ker.RemoveMissingActors(actors, partyMembers);
        foreach (var id in gunitts.Keys.Where(id => !active.Contains(id)).ToArray()) gunitts.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x2852)) gunitts.TryAdd(actor.Id, new());
        foreach (var gunitt in gunitts.Values) gunitt.RemoveMissingActors(actors, partyMembers);
        foreach (var id in atticus.Keys.Where(id => !active.Contains(id)).ToArray()) atticus.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x416D))
        {
            if (!atticus.TryAdd(actor.Id, new(actor.Id))) continue;
            initializingComponentOwners.Add(actor.Id);
        }
        foreach (var id in heshualas.Keys.Where(id => !active.Contains(id)).ToArray()) heshualas.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x416E))
        {
            if (!heshualas.TryAdd(actor.Id, new(actor.Id))) continue;
            initializingComponentOwners.Add(actor.Id);
        }
        foreach (var heshuala in heshualas.Values) heshuala.Observe(actors, now ?? DateTime.UnixEpoch);
        foreach (var id in ihnuxokiy.Keys.Where(id => !active.Contains(id)).ToArray()) ihnuxokiy.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x4582))
        {
            if (!ihnuxokiy.TryAdd(actor.Id, new(actor.Id))) continue;
            initializingComponentOwners.Add(actor.Id);
        }
        foreach (var id in neyoos.Keys.Where(id => !active.Contains(id)).ToArray()) neyoos.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x4233))
        {
            if (!neyoos.TryAdd(actor.Id, new(actor.Id))) continue;
            initializingComponentOwners.Add(actor.Id);
        }
        foreach (var id in nechucihos.Keys.Where(id => !active.Contains(id)).ToArray()) nechucihos.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x452C))
        {
            if (!nechucihos.TryAdd(actor.Id, new(actor.Id))) continue;
            initializingComponentOwners.Add(actor.Id);
        }
        foreach (var id in pedantries.Keys.Where(id => !active.Contains(id)).ToArray()) pedantries.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x298A))
        {
            if (!pedantries.TryAdd(actor.Id, new(actor.Id))) continue;
            initializingComponentOwners.Add(actor.Id);
        }
        foreach (var id in ixtabs.Keys.Where(id => !active.Contains(id)).ToArray()) ixtabs.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x2838))
        {
            if (!ixtabs.TryAdd(actor.Id, new(actor.Id))) continue;
            initializingComponentOwners.Add(actor.Id);
        }
        foreach (var id in slippery.Keys.Where(id => !active.Contains(id)).ToArray()) slippery.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x43DC))
        {
            if (!slippery.TryAdd(actor.Id, new(actor.Id))) continue;
            initializingComponentOwners.Add(actor.Id);
        }
        foreach (var id in rebellions.Keys.Where(id => !active.Contains(id)).ToArray()) rebellions.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x28B6))
        {
            if (!rebellions.TryAdd(actor.Id, new(actor.Id))) continue;
            initializingComponentOwners.Add(actor.Id);
        }
        foreach (var rebellion in rebellions.Values) rebellion.Observe(actors);
        foreach (var id in sugrivas.Keys.Where(id => !active.Contains(id)).ToArray()) sugrivas.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x35FC))
        {
            if (!sugrivas.TryAdd(actor.Id, new(actor.Id))) continue;
            initializingComponentOwners.Add(actor.Id);
        }
        InitializeFinalHunts();
        if (slippery.Count != 0) poisonMarch.Synchronize(actors);
        else poisonMarch.Clear();
        foreach (var key in componentStarts.Keys.Where(k => !active.Contains(k.Owner)).ToArray()) componentStarts.Remove(key);
        if (queens.Count != 0 || ihnuxokiy.Count != 0) threeSecondMarch.Synchronize(actors);
        else threeSecondMarch.Clear();
        if (queens.Count != 0)
        {
            SynchronizeBeeStatuses();
        }
        else
        {
            beeStatuses.Clear();
            beeRemovals.Clear();
            beeGains.Clear();
            beeWatermarks.Clear();
        }
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x288E)) tygers.TryAdd(actor.Id, new());
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x4580)) sambas.TryAdd(actor.Id, new());
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x4397)) forecasts.TryAdd(actor.Id, new());
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x43DB)) omens.TryAdd(actor.Id, new());
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x4232)) wingblades.TryAdd(actor.Id, new());
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x4395)) executions.TryAdd(actor.Id, new());
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x35DB)) narrowRifts.TryAdd(actor.Id, new());
        if (ActiveActors.Any(a => a.Oid is 0x35BF or 0x35DB)) march.Synchronize(actors);
        else march.Clear();
        if (!ActiveActors.Any(a => a.Oid == 0x2890))
        {
            stackStatusEvents.Clear();
            stackStatusWatermarks.Clear();
        }
        foreach (var (id, present) in stackStatusEvents.ToArray())
            if (actors.TryGetValue(id, out var actor) && actor.Statuses.Any(s => s.Id == 1435) == present)
                stackStatusEvents.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x3671)) aegeiros.TryAdd(actor.Id, new());
        foreach (var id in delayedDamage.Keys.Where(id => !active.Contains(id)).ToArray()) delayedDamage.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x35BE)) compressors.TryAdd(actor.Id, new());
        hasted.RemoveWhere(id => !active.Contains(id));
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x283A)) thumps.TryAdd(actor.Id, new());
        if (!ActiveActors.Any(a => a.Oid == 0x1AB2))
        {
            pyreticEvents.Clear();
            pyreticWatermarks.Clear();
        }
        foreach (var (key, pending) in pyreticEvents.ToArray())
        {
            if (!actors.TryGetValue(key.Target, out var player) || player.Dead)
            {
                pyreticEvents.Remove(key);
                continue;
            }
            var observed = player.Statuses.FirstOrDefault(s => s.Id == 960 && s.SourceId == key.Source);
            // Log duration and object sampling can differ by a frame; an older pre-refresh expiry must not confirm a new gain.
            if (pending == null && observed == null || pending != null && observed != null
                && observed.ExpiresAt >= pending.ExpiresAt.AddMilliseconds(-100))
                pyreticEvents.Remove(key);
        }
        foreach (var id in casts.Keys.Where(id => !actors.TryGetValue(id, out var actor)
            || !active.Contains(id) && !(raintrillers.Count != 0 && casts[id].Definition.Oid == 0x457F
                || kers.Count != 0 && casts[id].Definition.Oid == 0x35CF
                || gunitts.Count != 0 && casts[id].Definition.Oid == 0x2852
                || atticus.Count != 0 && casts[id].Definition.Oid == 0x416D
                || heshualas.Count != 0 && casts[id].Definition.Oid == 0x416E
                || ihnuxokiy.Count != 0 && casts[id].Definition.Oid == 0x4582
                || neyoos.Count != 0 && casts[id].Definition.Oid == 0x4233
                || nechucihos.Count != 0 && casts[id].Definition.Oid == 0x452C
                || pedantries.Count != 0 && casts[id].Definition.Oid == 0x298A
                || ixtabs.Count != 0 && casts[id].Definition.Oid == 0x2838
                || slippery.Count != 0 && casts[id].Definition.Oid == 0x43DC
                || rebellions.Count != 0 && casts[id].Definition.Oid == 0x28B6
                || sugrivas.Count != 0 && casts[id].Definition.Oid == 0x35FC
                || FinalHuntOwnerActive(casts[id].Definition.Oid))).ToArray())
            casts.Remove(id);
        foreach (var id in ended.Keys.Where(id => !actors.ContainsKey(id)).ToArray()) ended.Remove(id);
        foreach (var actor in actors.Values)
        {
            var observeCast = active.Contains(actor.Id) || BroadcastAction(actor.Cast?.ActionId ?? 0)
                || casts.TryGetValue(actor.Id, out var helperCast) && (helperCast.Definition.Oid == 0x457F && raintrillers.Count != 0
                    || helperCast.Definition.Oid == 0x35CF && kers.Count != 0
                    || helperCast.Definition.Oid == 0x2852 && gunitts.Count != 0
                    || helperCast.Definition.Oid == 0x416D && atticus.Count != 0
                    || helperCast.Definition.Oid == 0x416E && heshualas.Count != 0
                    || helperCast.Definition.Oid == 0x4582 && ihnuxokiy.Count != 0
                    || helperCast.Definition.Oid == 0x4233 && neyoos.Count != 0
                    || helperCast.Definition.Oid == 0x452C && nechucihos.Count != 0
                    || helperCast.Definition.Oid == 0x298A && pedantries.Count != 0
                    || helperCast.Definition.Oid == 0x2838 && ixtabs.Count != 0
                    || helperCast.Definition.Oid == 0x43DC && slippery.Count != 0
                    || helperCast.Definition.Oid == 0x28B6 && rebellions.Count != 0
                    || helperCast.Definition.Oid == 0x35FC && sugrivas.Count != 0
                    || FinalHuntOwnerActive(helperCast.Definition.Oid));
            if (observeCast && actor.Cast is { } cast)
            {
                if (!BroadcastAction(cast.ActionId) && casts.TryGetValue(actor.Id, out var previousCast)
                    && previousCast.Observed && previousCast.Definition.Oid is 0x457F or 0x35CF or 0x2852 or 0x416D or 0x416E or 0x4582 or 0x4233 or 0x452C or 0x298A or 0x2838 or 0x43DC or 0x28B6 or 0x35FC or 0x4544 or 0x43DD or 0x416F or 0x281E)
                    Finish(actor.Id, at: now);
                Start(actor.Id, cast, false, observedAt: now);
            }
            else if (observeCast && casts.TryGetValue(actor.Id, out var previous) && previous.Observed) Finish(actor.Id);
            if (casts.TryGetValue(actor.Id, out var currentCast) && currentCast.Rule.FollowCasterRotation && currentCast.Rotation != actor.Rotation)
                casts[actor.Id] = currentCast with { Rotation = actor.Rotation };
            if (combos.TryGetValue(actor.Id, out var combo)) combo.Update(actor);
            if (predictions.TryGetValue(actor.Id, out var prediction)) prediction.Update(actor);
            if (storedSequences.TryGetValue(actor.Id, out var stored)) stored.Update(actor);
            if (sneezes.TryGetValue(actor.Id, out var sneeze)) sneeze.Update(actor);
            if (tygers.TryGetValue(actor.Id, out var tyger)) tyger.Update(actor);
            if (missiles.TryGetValue(actor.Id, out var missile)) missile.Update(actors);
            if (targets.TryGetValue(actor.Id, out var target)) target.Update(actors);
            if (stacks.TryGetValue(actor.Id, out var sourceStacks))
                foreach (var stack in sourceStacks) stack.Update(actors);
            ObserveFinalActor(actor.Id, now ?? DateTime.UnixEpoch);
            // The source feed destroys absent actors first, then applies each actor's cast before its statuses.
            foreach (var sugriva in sugrivas.Values)
                sugriva.ObserveActor(actors, actor.Id, now ?? DateTime.UnixEpoch);
            foreach (var ker in kers.Values) ker.Observe(actor, partyMembers);
            foreach (var gunitt in gunitts.Where(p => !existingGunitts.Contains(p.Key)).Select(p => p.Value))
                gunitt.Observe(actor, partyMembers, now);
        }
        UpdateFinalParty();
        // New components replay current raw casts in actor order after Finish/Start reconciliation.
        // Receipts also fill owners activated before a packet's first raw confirmation, exactly once.
        initializingComponentOwners.Clear();
        foreach (var actor in actors.Values)
            if (actor.Cast is { } raw && casts.TryGetValue(actor.Id, out var cast) && cast.Observed
                && raw.ActionId == cast.Rule.Action && Math.Abs((raw.StartedAt - cast.StartedAt).TotalSeconds) < .5)
                StartBroadcastComponents(cast, raw);
        UpdateComponents();
    }

    private static bool RaintrillerAction(uint action) => action is 39758 or 39751 or 39749 or 39750 or 39752 or 39753 or 39754;

    private bool KerAction(uint action) => kers.Count != 0 && SimpleHuntCatalog.Find(0x35CF)!.Casts.Any(c => c.Action == action);
    private bool GunittAction(uint action) => gunitts.Count != 0 && SimpleHuntCatalog.Find(0x2852)!.Casts.Any(c => c.Action == action);
    private bool AtticusAction(uint action) => atticus.Count != 0 && SimpleHuntCatalog.Find(0x416D)!.Casts.Any(c => c.Action == action);
    private bool HeshualaAction(uint action) => heshualas.Count != 0 && action is >= 39095 and <= 39105;
    private bool IhnuxokiyAction(uint action) => ihnuxokiy.Count != 0 && SimpleHuntCatalog.Find(0x4582)!.Casts.Any(c => c.Action == action);
    private bool NeyoozoteelAction(uint action) => neyoos.Count != 0 && SimpleHuntCatalog.Find(0x4233)!.Casts.Any(c => c.Action == action);
    private bool NechucihoAction(uint action) => nechucihos.Count != 0 && SimpleHuntCatalog.Find(0x452C)!.Casts.Any(c => c.Action == action);
    private bool PedantryAction(uint action) => pedantries.Count != 0 && SimpleHuntCatalog.Find(0x298A)!.Casts.Any(c => c.Action == action);
    private bool IxtabAction(uint action) => ixtabs.Count != 0 && SimpleHuntCatalog.Find(0x2838)!.Casts.Any(c => c.Action == action);
    private bool SlipperyAction(uint action) => slippery.Count != 0 && SimpleHuntCatalog.Find(0x43DC)!.Casts.Any(c => c.Action == action);
    private bool RebellionAction(uint action) => rebellions.Count != 0 && SimpleHuntCatalog.Find(0x28B6)!.Casts.Any(c => c.Action == action);
    private bool SugrivaAction(uint action) => sugrivas.Count != 0 && SimpleHuntCatalog.Find(0x35FC)!.Casts.Any(c => c.Action == action);
    private bool BroadcastAction(uint action) => raintrillers.Count != 0 && RaintrillerAction(action) || KerAction(action)
        || GunittAction(action) || AtticusAction(action) || HeshualaAction(action) || IhnuxokiyAction(action)
        || NeyoozoteelAction(action) || NechucihoAction(action) || PedantryAction(action) || IxtabAction(action) || SlipperyAction(action) || RebellionAction(action)
        || SugrivaAction(action) || FinalHuntAction(action);

    internal void UpdateComponents(DateTime? now = null)
    {
        aglaopePending.Advance(now ?? observedTime ?? DateTime.UnixEpoch);
        UpdateFinalComponents();
        foreach (var gunitt in gunitts.Values) gunitt.Update(actors, partyMembers);
        foreach (var sequence in atticus.Values) sequence.Update(actors);
        foreach (var heshuala in heshualas.Values) heshuala.Update(now ?? observedTime ?? DateTime.UnixEpoch);
        foreach (var neyoo in neyoos.Values) neyoo.Update(actors, now ?? observedTime ?? DateTime.UnixEpoch);
        foreach (var pedantry in pedantries.Values) pedantry.Update(actors);
        foreach (var ixtab in ixtabs.Values) ixtab.Update(actors, now ?? observedTime ?? DateTime.UnixEpoch);
        foreach (var rebellion in rebellions.Values) rebellion.Update(now ?? observedTime ?? DateTime.UnixEpoch);
        foreach (var sugriva in sugrivas.Values) sugriva.Update(actors, now ?? observedTime ?? DateTime.UnixEpoch);
    }

    private static bool RaintrillerCaster(ReplayActorState actor)
        => !(actor.Kind == 1 && actor.SubKind == 4 || actor.Kind == 2 && actor.SubKind is 2 or 3 or 9);

    internal bool Handles(ulong source, uint action, Func<string, bool> enabled)
        => BroadcastAction(action)
            ? actors.TryGetValue(source, out var caster) && RaintrillerCaster(caster)
            : actors.TryGetValue(source, out var actor) && active.Contains(source)
            && SimpleHuntCatalog.Find(actor.Oid) is { } definition && enabled(definition.Name)
            && definition.Casts.Any(c => c.Action == action);

    internal void Start(ulong source, ReplayCast cast, bool packetPose, Vector3? packetSourcePosition = null, DateTime? observedAt = null)
    {
        if (!actors.TryGetValue(source, out var actor) || cast.FinishAt < cast.StartedAt) return;
        var rainInput = raintrillers.Count != 0 && RaintrillerAction(cast.ActionId);
        var kerInput = KerAction(cast.ActionId);
        var gunittInput = GunittAction(cast.ActionId);
        var atticusInput = AtticusAction(cast.ActionId);
        var heshualaInput = HeshualaAction(cast.ActionId);
        var ihnuxokiyInput = IhnuxokiyAction(cast.ActionId);
        var neyooInput = NeyoozoteelAction(cast.ActionId);
        var nechucihoInput = NechucihoAction(cast.ActionId);
        var pedantryInput = PedantryAction(cast.ActionId);
        var ixtabInput = IxtabAction(cast.ActionId);
        var slipperyInput = SlipperyAction(cast.ActionId);
        var rebellionInput = RebellionAction(cast.ActionId);
        var sugrivaInput = SugrivaAction(cast.ActionId);
        var finalInput = FinalHuntDefinition(cast.ActionId);
        if ((rainInput || kerInput || gunittInput || atticusInput || heshualaInput || ihnuxokiyInput || neyooInput || nechucihoInput || pedantryInput || ixtabInput || slipperyInput || rebellionInput || sugrivaInput || finalInput != null)
            && !RaintrillerCaster(actor)) return;
        var definition = SimpleHuntCatalog.Find(actor.Oid);
        if (rainInput) definition = SimpleHuntCatalog.Find(0x457F);
        else if (kerInput) definition = SimpleHuntCatalog.Find(0x35CF);
        else if (gunittInput) definition = SimpleHuntCatalog.Find(0x2852);
        else if (atticusInput) definition = SimpleHuntCatalog.Find(0x416D);
        else if (heshualaInput) definition = SimpleHuntCatalog.Find(0x416E);
        else if (ihnuxokiyInput) definition = SimpleHuntCatalog.Find(0x4582);
        else if (neyooInput) definition = SimpleHuntCatalog.Find(0x4233);
        else if (nechucihoInput) definition = SimpleHuntCatalog.Find(0x452C);
        else if (pedantryInput) definition = SimpleHuntCatalog.Find(0x298A);
        else if (ixtabInput) definition = SimpleHuntCatalog.Find(0x2838);
        else if (slipperyInput) definition = SimpleHuntCatalog.Find(0x43DC);
        else if (rebellionInput) definition = SimpleHuntCatalog.Find(0x28B6);
        else if (sugrivaInput) definition = SimpleHuntCatalog.Find(0x35FC);
        else if (finalInput != null) definition = finalInput;
        if (definition == null || !rainInput && !kerInput && !gunittInput && !atticusInput && !heshualaInput
            && !ihnuxokiyInput && !neyooInput && !nechucihoInput && !pedantryInput && !ixtabInput && !slipperyInput && !rebellionInput && !sugrivaInput && finalInput == null && !active.Contains(source)) return;
        if (ended.TryGetValue(source, out var old) && (cast.StartedAt < old.StartedAt
            || old.Action == cast.ActionId && (cast.StartedAt - old.StartedAt).TotalSeconds < .5)) return;
        if (casts.TryGetValue(source, out var previous))
        {
            if ((previous.StartedAt - cast.StartedAt).TotalSeconds >= .5) return;
            if (previous.Rule.Action == cast.ActionId && Math.Abs((previous.StartedAt - cast.StartedAt).TotalSeconds) < .5)
            {
                if (cast.Rotation == null && previous.Input.Rotation is { } knownRotation)
                    cast = cast with { Rotation = knownRotation };
                var observationChanged = !packetPose && previous.Observation is { } observation
                    && Changed(observation, cast);
                var aglaopeFirstRaw = definition.Oid == 0x281E && !packetPose && previous.Observation == null;
                // A raw CastInfo change is Finish -> Start even when a packet already corrected the output pose.
                var updated = previous;
                if (packetPose && (!previous.PacketPose || Changed(previous.Input, cast)
                        || definition.Oid == 0x4544 && cast.ActionId is 39509 or 39508 or 39510 or 39507 || definition.Oid == 0x43DD && cast.ActionId is 39292 or 39290 or 39289 or 39291 or 39601 or 39602 || definition.Oid == 0x43DC && cast.ActionId == 38648
                        && packetSourcePosition is { } packetPosition && previous.SourcePosition != packetPosition)
                    || observationChanged || aglaopeFirstRaw)
                    updated = Create(source, definition, previous.Rule, actor, cast, packetPose, packetSourcePosition, observedAt,
                        observationChanged ? null : previous.Order)
                        with { Observed = previous.Observed || !packetPose, Observation = packetPose ? previous.Observation : cast };
                else if (!packetPose && updated.Observation != cast)
                    updated = updated with { Observed = true, Observation = cast };
                if (!observationChanged && IsGroupedHuntAoe(previous) && previous.Shape == null)
                    updated = updated with { Shape = null };
                if (!observationChanged && previous.GazeCleared) updated = updated with { GazeCleared = true };
                if (updated != previous)
                {
                    casts[source] = updated;
                    if (observationChanged && tarchias.TryGetValue(source, out var observedTarchia))
                        observedTarchia.Finish(previous, observedAt ?? observedTime ?? previous.Activation);
                    if (observationChanged) AddDelayedDamage(updated);
                    else if (previous.Input != updated.Input) CorrectDelayedDamage(previous, updated);
                    UpdateRip(updated);
                    if (combos.TryGetValue(source, out var combo) && (observationChanged || previous.Input != updated.Input))
                    {
                        if (observationChanged)
                        {
                            combo.Finish(previous.Definition.Oid, previous.Rule.Action);
                            combo.Start(updated, actor);
                        }
                        else combo.Correct(previous, updated, actor);
                    }
                    if (thumps.TryGetValue(source, out var thump) && cast.ActionId == 17859
                        && (observationChanged || previous.Input != updated.Input))
                    {
                        if (observationChanged) thump.Finish();
                        thump.Start(updated.Activation);
                    }
                    if (predictions.TryGetValue(source, out var prediction))
                    {
                        if (observationChanged)
                        {
                            prediction.Finish(actor.Oid, previous.Rule.Action);
                            prediction.Start(updated, actor);
                        }
                        else if (previous.Input != updated.Input) prediction.Correct(previous, updated, actor);
                    }
                    if (aegeiros.TryGetValue(source, out var tree))
                    {
                        if (observationChanged)
                        {
                            tree.Finish(previous.Rule.Action);
                            tree.Start(updated);
                        }
                        else if (previous.Input != updated.Input) tree.Correct(previous, updated);
                    }
                    if (storedSequences.TryGetValue(source, out var stored))
                    {
                        if (observationChanged)
                        {
                            stored.Finish(actor.Oid, previous.Rule.Action);
                            stored.Start(updated, actor);
                        }
                        else if (previous.Input != updated.Input) stored.Correct(previous, updated);
                    }
                    if (!packetPose && previous.Observation == null)
                    {
                        if (predictions.TryGetValue(source, out var initialPrediction)) initialPrediction.FirstObservation(updated, actor);
                        if (storedSequences.TryGetValue(source, out var initialStored)) initialStored.FirstObservation(updated, actor);
                        if (targets.TryGetValue(source, out var initialTarget)) initialTarget.FirstObservation(updated, actors);
                    }
                    if (rotations.TryGetValue(source, out var rotation))
                    {
                        if (observationChanged)
                        {
                            rotation.Finish(previous.Rule.Action, observedAt ?? cast.StartedAt);
                            rotation.Start(updated);
                        }
                        else if (previous.Input != updated.Input) rotation.Correct(previous, updated);
                    }
                    if (sneezes.TryGetValue(source, out var sneeze))
                    {
                        if (observationChanged)
                        {
                            sneeze.Finish(previous.Rule.Action, observedAt ?? cast.StartedAt, actor);
                            sneeze.Start(updated, actor);
                        }
                        else if (previous.Input != updated.Input) sneeze.Correct(previous, updated, actor);
                    }
                    if (missiles.TryGetValue(source, out var missile))
                    {
                        if (observationChanged)
                        {
                            missile.Finish(previous.Rule.Action, actors);
                            missile.Start(updated, actors);
                        }
                        else if (previous.Input != updated.Input) missile.Correct(previous, updated, actors);
                    }
                    if (compressors.TryGetValue(source, out var compressor))
                    {
                        if (observationChanged) compressor.Start(updated);
                        else if (previous.Input != updated.Input) compressor.Correct(previous, updated);
                    }
                    if (targets.TryGetValue(source, out var target))
                    {
                        if (observationChanged)
                        {
                            target.Finish(previous.Rule.Action);
                            target.Start(updated, actors);
                        }
                        else if (previous.Input != updated.Input) target.Correct(previous, updated, actors);
                    }
                    if (observationChanged)
                    {
                        FinishStacks(source, previous.Rule.Action, previous.TargetId, observedAt);
                        StartStack(updated);
                    }
                    else if (stacks.TryGetValue(source, out var sourceStacks))
                    {
                        foreach (var stack in sourceStacks.ToArray())
                        {
                            if (previous.Input != updated.Input) stack.Correct(previous, updated, actors);
                            if (!packetPose && previous.Observation == null) stack.FirstObservation(updated, actors);
                            // StackWithCastTargets never creates an entry when the corrected start has no target.
                            if (stack.TargetId == 0 && updated.Definition.Oid is 0x4396 or 0x452A or 0x457F) sourceStacks.Remove(stack);
                        }
                    }
                    if (observationChanged && narrowRifts.TryGetValue(source, out var narrow))
                    {
                        narrow.Finish();
                        narrow.Start(updated);
                    }
                    if (omens.TryGetValue(source, out var omen))
                    {
                        if (observationChanged)
                        {
                            omen.Finish(previous.Rule.Action);
                            omen.Start(updated);
                        }
                        else if (previous.Input != updated.Input) omen.Correct(previous, updated);
                    }
                    if (wingblades.TryGetValue(source, out var wingblade))
                    {
                        if (observationChanged) wingblade.Start(updated);
                        else if (previous.Input != updated.Input) wingblade.Correct(previous, updated);
                    }
                    if (sambas.TryGetValue(source, out var samba))
                    {
                        if (observationChanged)
                        {
                            samba.Finish(previous.Rule.Action);
                            samba.Start(updated);
                        }
                        else if (previous.Input != updated.Input) samba.Correct(previous, updated);
                    }
                    if (forecasts.TryGetValue(source, out var forecast))
                    {
                        if (observationChanged) forecast.Start(updated);
                        else if (previous.Input != updated.Input) forecast.Correct(previous, updated);
                    }
                    if (tygers.TryGetValue(source, out var tyger))
                    {
                        if (observationChanged) tyger.Start(updated, actor);
                        else if (previous.Input != updated.Input) tyger.Correct(previous, updated);
                    }
                    if (queens.TryGetValue(source, out var queen))
                    {
                        if (observationChanged) queen.Start(updated);
                        else if (previous.Input != updated.Input) queen.Correct(previous, updated);
                    }
                    if (observationChanged) FinishComponents(source, previous.Rule.Action, observedAt, previous);
                    foreach (var raintriller in raintrillers.Values)
                    {
                        if (observationChanged)
                        {
                            raintriller.Start(updated);
                        }
                        else if (previous.Input != updated.Input) raintriller.Correct(previous, updated);
                    }
                    foreach (var ker in kers.Values)
                    {
                        if (observationChanged) ker.Start(updated, actors, partyMembers);
                        else if (previous.Input != updated.Input) ker.Correct(previous, updated);
                    }
                    if (observationChanged)
                    {
                        FinishBroadcastStacks(previous.Rule.Action, previous);
                    }
                    else if (previous.Input != updated.Input || previous.SourcePosition != updated.SourcePosition
                        || previous.Activation != updated.Activation)
                        CorrectBroadcastComponents(previous, updated);
                    StartBroadcastComponents(updated, packetPose ? null : cast);
                    if (executions.TryGetValue(source, out var execution))
                    {
                        if (observationChanged) execution.Start(updated);
                        else if (previous.Input != updated.Input) execution.Correct(previous, updated);
                    }
                }
                return;
            }
            Finish(source);
        }
        var rule = definition.Casts.FirstOrDefault(c => c.Action == cast.ActionId);
        // EmptyPromise consumes every primary cast finish, including casts without a dedicated rule.
        if (rule == null && actor.Oid == 0x35DB) rule = new(cast.ActionId, "OtherCast", null);
        if (rule == null
            || cast.Rotation == null && (rule.Shape is not (null or CircleTemplate or DonutTemplate) && !rule.FollowCasterRotation
                || actor.Oid == 0x2854 && cast.ActionId is 17423 or 17424
                || actor.Oid == 0x28B5 && cast.ActionId is 17495 or 17497 or 17498
                || actor.Oid == 0x360B && cast.ActionId == 27318
                || actor.Oid == 0x3670 && cast.ActionId is 27624 or 27625
                || actor.Oid == 0x361B && cast.ActionId is 27493 or 27494 or 27497 or 27498 or 27521 or 27522
                || actor.Oid == 0x360A && cast.ActionId is 27307 or 27308 or 27692 or 27309
                || actor.Oid == 0x35BE && cast.ActionId == 27472
                || actor.Oid == 0x4395 && cast.ActionId is >= 38457 and <= 38460
                || actor.Oid == 0x2875 && cast.ActionId is 18100 or 18101
                || actor.Oid == 0x43DB && cast.ActionId is >= 38632 and <= 38635
                || actor.Oid == 0x4232 && cast.ActionId is 37164 or 37165 or 37166 or 37167 or 37170
                || actor.Oid == 0x4580 && cast.ActionId is 39864 or 39797
                || actor.Oid == 0x4397 && cast.ActionId is 38524 or 38526 or 38528 or 38530
                || actor.Oid == 0x288E && cast.ActionId is 16962 or 16963
                || definition.Oid == 0x457F && cast.ActionId is 39758 or 39751 or 39749
                || definition.Oid == 0x35CF && (HuntKerState.Flare(cast.ActionId) || cast.ActionId is 27927 or 27928)
                || definition.Oid == 0x416E && cast.ActionId is 39099 or 39100 or 39101
                || definition.Oid == 0x4582 && cast.ActionId is 39773 or 39828
                || definition.Oid == 0x4233 && cast.ActionId is 37377 or 37376 or 37378 or 37379 or 37373 or 37374
                || definition.Oid == 0x452C && cast.ActionId is 39493 or 39494 or 39495
                || definition.Oid == 0x298A && cast.ActionId is 17446 or 17448
                || definition.Oid == 0x4544 && cast.ActionId is 39509 or 39508 or 39510 or 39507 || definition.Oid == 0x43DD && cast.ActionId is 39292 or 39290 or 39289 or 39291 or 39601 or 39602 || definition.Oid == 0x43DC && cast.ActionId == 38648
                || definition.Oid == 0x28B6 && cast.ActionId is 17628 or 17629 or 18080 or 18078 or 18079)) return;
        var pending = Create(source, definition, rule, actor, cast, packetPose, packetSourcePosition, observedAt);
        casts[source] = pending;
        AddDelayedDamage(pending);
        UpdateRip(pending, true);
        if (actor.Oid == 0x2874 && cast.ActionId == 18020) hasted.Add(source);
        if (actor.Oid == 0x283A && cast.ActionId == 17859) thumps[source].Start(pending.Activation);
        if (actor.Oid is 0x2854 or 0x28B5)
        {
            if (!combos.TryGetValue(source, out var combo)) combos[source] = combo = new();
            combo.Start(pending, actor);
        }
        if (actor.Oid is 0x35DE or 0x360B or 0x35DC or 0x28B4 or 0x288F or 0x2853 or 0x35BF)
        {
            if (!predictions.TryGetValue(source, out var prediction)) predictions[source] = prediction = new();
            prediction.Start(pending, actor);
        }
        if (actor.Oid is 0x35BD or 0x3670)
        {
            if (!storedSequences.TryGetValue(source, out var stored)) storedSequences[source] = stored = new();
            stored.Start(pending, actor);
        }
        if (actor.Oid is 0x361B or 0x2875)
        {
            if (!rotations.TryGetValue(source, out var rotation)) rotations[source] = rotation = new();
            rotation.Start(pending);
        }
        if (actor.Oid == 0x360A)
        {
            if (!sneezes.TryGetValue(source, out var sneeze)) sneezes[source] = sneeze = new();
            sneeze.Start(pending, actor);
        }
        if (actor.Oid == 0x3609)
        {
            if (!missiles.TryGetValue(source, out var missile)) missiles[source] = missile = new();
            missile.Start(pending, actors);
        }
        if (compressors.TryGetValue(source, out var compressorState)) compressorState.Start(pending);
        if (aegeiros.TryGetValue(source, out var aegeirosState)) aegeirosState.Start(pending);
        if (narrowRifts.TryGetValue(source, out var narrowState)) narrowState.Start(pending);
        if (executions.TryGetValue(source, out var executionState)) executionState.Start(pending);
        if (omens.TryGetValue(source, out var omenState)) omenState.Start(pending);
        if (wingblades.TryGetValue(source, out var wingbladeState)) wingbladeState.Start(pending);
        if (sambas.TryGetValue(source, out var sambaState)) sambaState.Start(pending);
        if (forecasts.TryGetValue(source, out var forecastState)) forecastState.Start(pending);
        if (tygers.TryGetValue(source, out var tygerState)) tygerState.Start(pending, actor);
        if (queens.TryGetValue(source, out var queenState)) queenState.Start(pending);
        foreach (var raintrillerState in raintrillers.Values) raintrillerState.Start(pending);
        foreach (var ker in kers.Values) ker.Start(pending, actors, partyMembers);
        StartBroadcastComponents(pending);
        StartStack(pending);
        if (rule.BaitRadius > 0 || rule.SpreadRadius > 0)
        {
            if (!targets.TryGetValue(source, out var target)) targets[source] = target = new();
            target.Start(pending, actors);
        }
    }

    private void StartBroadcastComponents(HuntPendingCast cast, ReplayCast? raw = null)
    {
        // Existing owners retain their accepted packet pose. A newly initialized component
        // receives the current raw pose, and later corrections use its own delivered baseline.
        if (raw != null && actors.TryGetValue(cast.Source, out var actor))
        {
            if (raw.Rotation == null) raw = raw with { Rotation = cast.Rotation };
            cast = Create(cast.Source, cast.Definition, cast.Rule, actor, raw, false, null, observedTime, cast.Order);
        }
        StartFinalComponents(cast);
        switch (cast.Definition.Oid)
        {
            case 0x416D:
                foreach (var (owner, state) in atticus)
                    if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                        state.Start(cast, actors);
                break;
            case 0x416E:
                foreach (var (owner, state) in heshualas)
                    if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                        state.Start(cast);
                break;
            case 0x4582:
                foreach (var (owner, state) in ihnuxokiy)
                    if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                        state.Start(cast);
                break;
            case 0x4233:
                foreach (var (owner, state) in neyoos)
                    if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                        state.Start(cast, actors);
                break;
            case 0x452C:
                foreach (var (owner, state) in nechucihos)
                    if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                        state.Start(cast);
                break;
            case 0x298A:
                foreach (var (owner, state) in pedantries)
                    if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                        state.Start(cast, actors);
                break;
            case 0x2838:
                foreach (var (owner, state) in ixtabs)
                    if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                        state.Start(cast, actors);
                break;
            case 0x43DC:
                foreach (var (owner, state) in slippery)
                    if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                        state.Start(cast);
                break;
            case 0x28B6:
                foreach (var (owner, state) in rebellions)
                    if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                        state.Start(cast, actors[cast.Source]);
                break;
            case 0x35FC:
                foreach (var (owner, state) in sugrivas)
                    if (!initializingComponentOwners.Contains(owner) && componentStarts.TryAdd((owner, cast.Source, cast.Order), cast))
                        state.Start(cast, actors);
                break;
        }
    }

    private void CorrectBroadcastComponents(HuntPendingCast previous, HuntPendingCast updated)
    {
        foreach (var key in componentStarts.Keys.Where(k => k.Source == previous.Source && k.Order == previous.Order).ToArray())
        {
            var delivered = componentStarts[key];
            CorrectFinalComponent(key.Owner, delivered, updated);
            switch (updated.Definition.Oid)
            {
                case 0x416D:
                    if (atticus.TryGetValue(key.Owner, out var breath)) breath.Correct(delivered, updated, actors);
                    break;
                case 0x416E:
                    if (heshualas.TryGetValue(key.Owner, out var shock)) shock.Correct(delivered, updated);
                    break;
                case 0x4582:
                    if (ihnuxokiy.TryGetValue(key.Owner, out var smog)) smog.Correct(delivered, updated);
                    break;
                case 0x4233:
                    if (neyoos.TryGetValue(key.Owner, out var sap)) sap.Correct(delivered, updated, actors);
                    break;
                case 0x452C:
                    if (nechucihos.TryGetValue(key.Owner, out var whispers)) whispers.Correct(delivered, updated);
                    break;
                case 0x298A:
                    if (pedantries.TryGetValue(key.Owner, out var pedantry)) pedantry.Correct(delivered, updated, actors);
                    break;
                case 0x2838:
                    if (ixtabs.TryGetValue(key.Owner, out var ixtab)) ixtab.Correct(delivered, updated, actors);
                    break;
                case 0x43DC:
                    if (slippery.TryGetValue(key.Owner, out var slipperyState)) slipperyState.Correct(delivered, updated);
                    break;
                case 0x28B6:
                    if (rebellions.TryGetValue(key.Owner, out var rebellion)) rebellion.Correct(delivered, updated, actors[updated.Source]);
                    break;
                case 0x35FC:
                    if (sugrivas.TryGetValue(key.Owner, out var sugriva)) sugriva.Correct(delivered, updated, actors);
                    break;
            }
            componentStarts[key] = updated;
        }
    }

    private void FinishBroadcastStacks(uint action, HuntPendingCast? cast, ulong? target = null)
    {
        foreach (var (owner, state) in atticus)
        {
            var delivered = cast != null ? componentStarts.GetValueOrDefault((owner, cast.Source, cast.Order)) : null;
            state.FinishStack(action, target ?? delivered?.TargetId ?? cast?.TargetId ?? 0);
        }
        foreach (var (owner, state) in neyoos)
        {
            var delivered = cast != null ? componentStarts.GetValueOrDefault((owner, cast.Source, cast.Order)) : null;
            state.FinishStack(action, target ?? delivered?.TargetId ?? cast?.TargetId ?? 0);
        }
    }

    private void UpdateRip(HuntPendingCast cast, bool starting = false)
    {
        if (cast.Definition.Oid == 0x1AC0 && cast.Rule.Action == 8191 && (starting || rips.ContainsKey(cast.Source)))
            rips[cast.Source] = cast with { Rule = new(8192, "Rip", cast.Shape), Followup = true };
    }

    private void StartStack(HuntPendingCast pending)
    {
        if (pending.Definition.Oid == 0x2890 && pending.Rule.Action == 16971)
        {
            var stack = new HuntStackState();
            stack.Start(pending, actors, FrozenForbidden());
            stacks[pending.Source] = [stack];
        }
        else if ((pending.Definition.Oid == 0x452A && pending.Rule.Action == 39480
            || pending.Definition.Oid == 0x4396 && pending.Rule.Action is 38515 or 39668
            || pending.Definition.Oid == 0x457F && pending.Rule.Action == 39754) && actors.ContainsKey(pending.TargetId))
        {
            var stack = new HuntStackState(int.MaxValue, pending.Definition.Oid == 0x4396 ? 8 : 6,
                pending.Definition.Oid == 0x457F ? 2 : 8);
            stack.Start(pending, actors, []);
            if (!stacks.TryGetValue(pending.Source, out var sourceStacks)) stacks[pending.Source] = sourceStacks = [];
            sourceStacks.Add(stack);
        }
    }

    private void FinishStacks(ulong source, uint action, ulong target, DateTime? at)
    {
        if (!stacks.TryGetValue(source, out var sourceStacks)) return;
        var index = sourceStacks.FindIndex(s => s.Cast is { } cast && !(at < cast.StartedAt)
            && (cast.Definition.Oid == 0x2890 && action == 16971
                || cast.Definition.Oid == 0x452A && action == 39480 && s.TargetId == target
                || cast.Definition.Oid == 0x4396 && action == cast.Rule.Action && s.TargetId == target
                || cast.Definition.Oid == 0x457F && action == 39754 && s.TargetId == target));
        if (index >= 0) sourceStacks.RemoveAt(index);
        if (sourceStacks.Count == 0) stacks.Remove(source);
    }

    private static bool IsGroupedHuntAoe(HuntPendingCast cast)
        => cast.Definition.Oid == 0x452A && cast.Rule.Action is
            39466 or 39477 or 39473 or 39467 or 39538 or 39877 or 39464 or 39459 or 39484 or 39463 or 39465 or 39875
            || cast.Definition.Oid == 0x288E && cast.Rule.Action is 16957 or 16958 or 16959
            || cast.Definition.Oid == 0x43DC && cast.Rule.Action is 40063 or 38651
            || cast.Definition.Oid == 0x35FC && cast.Rule.Action is 27225 or 27226
            || cast.Definition.Oid == 0x4544 && cast.Rule.Action is 39829 or 39837 or 39838 or 39830
            || cast.Definition.Oid == 0x35CF && cast.Rule.Action is
                27637 or 27652 or 27659 or 27646 or 27639 or 27653 or 27641 or 27642 or 27643 or 27644 or 27661 or 27662 or 27663 or 27664;

    private IEnumerable<ulong> FrozenForbidden()
        => partyMembers.Where(p => p.Slot is >= 0 and < 64 && p.EntityId is not (0 or 0xE0000000))
            .Select(p => p.EntityId).Distinct().Where(id => stackStatusEvents.TryGetValue(id, out var present)
                ? present : actors.TryGetValue(id, out var actor) && actor.Statuses.Any(s => s.Id == 1435));

    internal void StackStatus(ulong target, DateTime at, bool present)
    {
        if (!ActiveActors.Any(a => a.Oid == 0x2890) || stackStatusWatermarks.TryGetValue(target, out var previous) && at < previous) return;
        stackStatusWatermarks[target] = at;
        stackStatusEvents[target] = present;
    }

    internal void MarchStatus(ulong target, uint status, DateTime at, ReplayStatus? value)
    {
        if (ActiveActors.Any(a => a.Oid is 0x35BF or 0x35DB)) march.Status(target, status, at, value);
        foreach (var rebellion in rebellions.Values) rebellion.Status(target, status, at, value);
        if (queens.Count != 0 || ihnuxokiy.Count != 0) threeSecondMarch.Status(target, status, at, value);
        if (slippery.Count != 0) poisonMarch.Status(target, status, at, value);
    }

    internal void NpcYell(ulong source, ushort id, DateTime at)
    {
        if (!actors.TryGetValue(source, out var actor)) return;
        foreach (var raintriller in raintrillers.Values) raintriller.Yell(id, at);
        foreach (var sequence in atticus.Values) sequence.Yell(actor, id, at);
    }

    private void FinishComponents(ulong source, uint action, DateTime? at, HuntPendingCast? finishedCast = null, ulong? finishedTarget = null)
    {
        if (!actors.TryGetValue(source, out var actor) || !RaintrillerCaster(actor)
            || action is not (39750 or 39752 or 39753 or 17361 or 39099 or 39100 or 39101 or 39102 or 39103 or 17444 or 17848 or 17844 or 17628 or 17629 or 18080 or 17601 or 17602 or 27219) && !FinalHuntFinishAction(action)) return;
        var timestamp = at ?? observedTime ?? default;
        casts.TryGetValue(source, out var current);
        current = finishedCast ?? current;
        if (current != null && timestamp < current.StartedAt) return;
        var key = (source, action);
        if ((action is 17848 or 17844 or 17628 or 17629 or 18080 or 17601 or 17602 or 27219 || FinalHuntFinishAction(action)) && current?.Rule.Action == action)
        {
            // A raw replacement and its destruction can finish two distinct orders in the same frame.
            if (!castComponentFinishes.Add((source, action, current.Order))) return;
        }
        else
        {
            if (rainFinishes.TryGetValue(key, out var previous) && timestamp <= previous) return;
            // A packet can follow the same snapshot finish by a frame. Later trusted finishes may have missed their Start.
            if (!casts.ContainsKey(source) && ended.TryGetValue(source, out var finished) && finished.Action == action
                && rainFinishes.TryGetValue(key, out previous) && timestamp <= previous.AddSeconds(.5)) return;
        }
        rainFinishes[key] = timestamp;
        FinishFinalComponents(source, action, finishedTarget ?? current?.Observation?.TargetId ?? current?.TargetId ?? 0);
        foreach (var raintriller in raintrillers.Values) raintriller.Finish(action, at);
        if (action == 17361)
            foreach (var gunitt in gunitts.Values) gunitt.Finish();
        var rotation = current?.Rule.Action == action ? current.Rotation : actor.Cast?.Rotation;
        foreach (var (owner, heshuala) in heshualas)
        {
            if (initializingComponentOwners.Contains(owner)) continue;
            var delivered = current?.Rule.Action == action
                ? componentStarts.GetValueOrDefault((owner, source, current.Order)) : null;
            heshuala.Finish(action, actor, delivered?.Rotation ?? rotation, timestamp);
        }
        foreach (var (owner, pedantry) in pedantries)
        {
            if (initializingComponentOwners.Contains(owner)) continue;
            // Finished carries the ending CastInfo target, shared by every owner.
            // Started receipts may retain an earlier packet target.
            var target = finishedTarget ?? current?.Observation?.TargetId ?? current?.TargetId ?? 0;
            pedantry.Finish(actor, action, target, timestamp, actors);
        }
        foreach (var (owner, ixtab) in ixtabs)
        {
            if (initializingComponentOwners.Contains(owner)) continue;
            ixtab.Finish(source, action, finishedTarget ?? current?.Observation?.TargetId ?? current?.TargetId ?? 0);
        }
        foreach (var (owner, rebellion) in rebellions)
            if (!initializingComponentOwners.Contains(owner)) rebellion.Finish(source, action, timestamp);
        foreach (var (owner, sugriva) in sugrivas)
            if (!initializingComponentOwners.Contains(owner))
                sugriva.Finish(action, finishedTarget ?? current?.Observation?.TargetId ?? current?.TargetId ?? 0);
    }

    internal void HeshualaStatus(ulong actor, uint id, ReplayStatus? status, DateTime at)
    {
        foreach (var heshuala in heshualas.Values) heshuala.Status(actor, id, status, at);
    }

    internal void GunittStatus(ulong actor, ReplayStatus? status, DateTime at)
    {
        foreach (var gunitt in gunitts.Values) gunitt.Status(actor, status, at, partyMembers);
    }

    internal void Icon(ulong actor, uint icon, DateTime at)
    {
        if (actors.TryGetValue(actor, out var iconActor))
            foreach (var rebellion in rebellions.Values) rebellion.Icon(iconActor, icon);
        if (icon != 93 || !actors.ContainsKey(actor)) return;
        var definition = SimpleHuntCatalog.Find(0x2852)!;
        var rule = definition.Casts.Single(c => c.Action == 17361);
        foreach (var (source, gunitt) in gunitts)
        {
            var input = new ReplayCast(17361, actor, actors[actor].Position, 0, at, at.AddSeconds(5d), false, false);
            gunitt.Icon(new(source, definition, rule, input.Location, 0, at, input.FinishAt,
                actor, false, true, null, input, null, Order: ++castOrder), actors, partyMembers);
        }
    }

    internal IEnumerable<(Vector3 From, Vector3 To, float Rotation)> MarchMovements(ReplayActorState player)
        => march.Movements(player, observedTime ?? default).Concat(threeSecondMarch.Movements(player, observedTime ?? default))
            .Concat(poisonMarch.Movements(player, observedTime ?? default))
            .Concat(rebellions.Values.SelectMany(r => r.Movements(player, observedTime ?? default)));

    internal void BeeStatusLost(ulong actor, uint status, DateTime at)
    {
        if (queens.Count == 0 || status is not (4147 or 4148)) return;
        var key = (actor, status);
        if (beeWatermarks.TryGetValue(key, out var latest) && at < latest) return;
        if (beeRemovals.TryGetValue(key, out var previous) && at <= previous.At) return;
        beeWatermarks[key] = at;
        beeGains.Remove(key);
        var observed = actors.GetValueOrDefault(actor)?.Statuses.FirstOrDefault(s => s.Id == status);
        beeRemovals[key] = (at, observed);
        beeStatuses.Remove(key);
        foreach (var queen in queens.Values) queen.Lose(at);
    }

    internal void BeeStatusGained(ulong actor, ReplayStatus status, DateTime at)
    {
        if (queens.Count == 0 || status.Id is not (4147 or 4148)) return;
        var key = (actor, status.Id);
        if (beeWatermarks.TryGetValue(key, out var latest) && at < latest) return;
        beeWatermarks[key] = at;
        beeGains[key] = status;
        beeRemovals.Remove(key);
        if (beeStatuses.TryGetValue(key, out var previous) && previous != status.SourceId)
            foreach (var queen in queens.Values) queen.Lose(at);
        beeStatuses[key] = status.SourceId;
    }

    private void SynchronizeBeeStatuses()
    {
        var current = actors.Values.SelectMany(a => a.Statuses.Where(s => s.Id is 4147 or 4148)
            .Select(s => (Key: (Actor: a.Id, Status: s.Id), Status: s)))
            .GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.First().Status);
        foreach (var (key, gain) in beeGains.ToArray())
        {
            if (!actors.ContainsKey(key.Actor) || gain.ExpiresAt <= observedTime)
                beeGains.Remove(key);
            else if (current.TryGetValue(key, out var status) && status.SourceId == gain.SourceId
                && status.Param == gain.Param && Math.Abs((status.ExpiresAt - gain.ExpiresAt).TotalSeconds) <= .1)
                beeGains.Remove(key);
            else current[key] = gain;
        }
        foreach (var (key, removal) in beeRemovals.ToArray())
            if (!current.TryGetValue(key, out var status) || removal.Previous is { } previous
                && (previous.SourceId != status.SourceId || previous.Param != status.Param
                    || Math.Abs((previous.ExpiresAt - status.ExpiresAt).TotalSeconds) > .1))
                beeRemovals.Remove(key);
        foreach (var key in beeRemovals.Keys) current.Remove(key);
        if (beeStatuses.Any(p => !current.TryGetValue(p.Key, out var status) || status.SourceId != p.Value))
            foreach (var queen in queens.Values) queen.Lose(observedTime);
        beeStatuses.Clear();
        foreach (var pair in current) beeStatuses[pair.Key] = pair.Value.SourceId;
    }

    private void AddDelayedDamage(HuntPendingCast cast)
    {
        if (cast.Definition.Oid != 0x288F || cast.Rule.Action != 16964) return;
        var target = cast.TargetId == cast.Source ? actors[cast.Source].TargetId : cast.TargetId;
        if (!actors.ContainsKey(target)) return;
        if (!delayedDamage.TryGetValue(cast.Source, out var damage)) delayedDamage[cast.Source] = damage = [];
        damage.Add(cast with { TargetId = target });
    }

    private void CorrectDelayedDamage(HuntPendingCast previous, HuntPendingCast updated)
    {
        if (!delayedDamage.TryGetValue(previous.Source, out var damage)) return;
        var index = damage.FindIndex(c => c.Order == previous.Order);
        if (index < 0) return;
        var target = damage[index].TargetId;
        if (previous.Input.TargetId != updated.Input.TargetId)
            target = updated.TargetId == updated.Source ? actors[updated.Source].TargetId : updated.TargetId;
        if (previous.Input.TargetId == updated.Input.TargetId || actors.ContainsKey(target))
            damage[index] = updated with { TargetId = target };
        else damage.RemoveAt(index);
    }

    internal void Effect(ulong source, uint action, DateTime at, uint sequence = 0, ulong target = 0)
    {
        if (sansheyas.Count != 0 && HuntSansheyaState.Hit(action))
        {
            if (actors.TryGetValue(source, out var caster) && RaintrillerCaster(caster)
                && effects.Add((source, action, sequence, sequence == 0 ? at : default)))
                foreach (var state in sansheyas.Values) state.Effect();
            return;
        }
        if (sugrivas.Count != 0 && action == 27223)
        {
            if (actors.TryGetValue(source, out var caster) && RaintrillerCaster(caster)
                && effects.Add((source, action, sequence, sequence == 0 ? at : default)))
                foreach (var sugriva in sugrivas.Values) sugriva.Effect(source);
            return;
        }
        if (rebellions.Count != 0 && action is 18126 or 18127 or 18128)
        {
            if (actors.TryGetValue(source, out var caster) && RaintrillerCaster(caster)
                && effects.Add((source, action, sequence, sequence == 0 ? at : default)))
                foreach (var rebellion in rebellions.Values) rebellion.Effect(action, at);
            return;
        }
        if (slippery.Count != 0 && action is 39559 or 38650)
        {
            if (actors.TryGetValue(source, out var caster) && RaintrillerCaster(caster)
                && effects.Add((source, action, sequence, sequence == 0 ? at : default)))
                foreach (var sequenceState in slippery.Values) sequenceState.Effect();
            return;
        }
        if (ixtabs.Count != 0 && HuntIxtabState.Hit(action))
        {
            if (actors.TryGetValue(source, out var caster) && RaintrillerCaster(caster)
                && effects.Add((source, action, sequence, sequence == 0 ? at : default)))
                foreach (var ixtab in ixtabs.Values) ixtab.Effect(action);
            return;
        }
        if (pedantries.Count != 0 && HuntPedantryState.Hit(action))
        {
            if (actors.TryGetValue(source, out var caster) && RaintrillerCaster(caster)
                && effects.Add((source, action, sequence, sequence == 0 ? at : default)))
                foreach (var pedantry in pedantries.Values) pedantry.Effect(caster, action, target, at, actors);
            return;
        }
        if (nechucihos.Count != 0 && HuntNechucihoState.Hit(action))
        {
            if (actors.TryGetValue(source, out var caster) && RaintrillerCaster(caster)
                && effects.Add((source, action, sequence, sequence == 0 ? at : default)))
                foreach (var nechuciho in nechucihos.Values) nechuciho.Effect();
            return;
        }
        if (neyoos.Count != 0 && (HuntNeyoozoteelState.SapHit(action) || HuntNeyoozoteelState.RootsHit(action)))
        {
            if (actors.TryGetValue(source, out var caster) && RaintrillerCaster(caster)
                && effects.Add((source, action, sequence, sequence == 0 ? at : default)))
                foreach (var neyoo in neyoos.Values) neyoo.Effect(action, at);
            return;
        }
        if (ihnuxokiy.Count != 0 && HuntIhnuxokiyState.Hit(action))
        {
            if (actors.TryGetValue(source, out var caster) && RaintrillerCaster(caster)
                && effects.Add((source, action, sequence, sequence == 0 ? at : default)))
                foreach (var smog in ihnuxokiy.Values) smog.Effect();
            return;
        }
        if (atticus.Count != 0 && HuntAtticusState.Hit(action))
        {
            if (actors.TryGetValue(source, out var caster) && RaintrillerCaster(caster)
                && effects.Add((source, action, sequence, sequence == 0 ? at : default)))
                foreach (var breath in atticus.Values) breath.Effect();
            return;
        }
        if (tarchias.TryGetValue(source, out var tarchia))
        {
            if (effects.Add((source, action, sequence, sequence == 0 ? at : default))) tarchia.Effect(action, at);
            return;
        }
        if (action == 8192 && rips.TryGetValue(source, out var rip) && at >= rip.StartedAt)
            rips.Remove(source);
        if (!active.Contains(source) || action is not (17421 or 17422 or 18042 or 17495 or 17497 or 17498 or 17863
                or 26878 or 26879 or 26880 or 26881 or 26915 or 26916 or 26167 or 26168
                or 27470 or 27471 or 27472 or 27473 or 27474 or 17489 or 16964 or 18037 or 17369
                or 27333 or 27516 or 38457 or 38458 or 38459 or 38460 or 38461 or 38462 or 38463
                or 37164 or 37165 or 37166 or 37167 or 37171 or 37172
                or 38524 or 38525 or 38526 or 38527 or 38528 or 38529 or 38530 or 38531 or 16961)
            || !actors.TryGetValue(source, out var actor) || !effects.Add((source, action, sequence, sequence == 0 ? at : default))) return;
        if (combos.TryGetValue(source, out var combo)) combo.Effect(actor.Oid, action, at);
        if (action == 17863 && thumps.TryGetValue(source, out var thump)) thump.Effect();
        if (storedSequences.TryGetValue(source, out var stored)) stored.Effect(actor.Oid, action, at);
        if (compressors.TryGetValue(source, out var compressor)) compressor.Effect(action, at);
        if (predictions.TryGetValue(source, out var prediction)) prediction.Effect(actor.Oid, action, at);
        if (narrowRifts.TryGetValue(source, out var narrow)) narrow.Effect(actor, action, at);
        if (executions.TryGetValue(source, out var execution)) execution.Effect(action, at);
        if (wingblades.TryGetValue(source, out var wingblade)) wingblade.Effect(action, at);
        if (forecasts.TryGetValue(source, out var forecast)) forecast.Effect(action, at);
        if (tygers.TryGetValue(source, out var tyger)) tyger.Effect(action, at);
        if (actor.Oid == 0x288F && action == 16964 && delayedDamage.TryGetValue(source, out var damage))
        {
            var index = damage.FindIndex(c => c.TargetId == target && c.StartedAt <= at);
            if (index >= 0) damage.RemoveAt(index);
        }
    }

    internal void KerEffect(ulong source, uint action, DateTime at, uint sequence, IEnumerable<ulong> targets)
    {
        if (!HuntKerState.Flare(action) || !actors.TryGetValue(source, out var actor) || !RaintrillerCaster(actor)
            || !effects.Add((source, action, sequence, sequence == 0 ? at : default))) return;
        foreach (var ker in kers.Values) ker.Effect(at, targets, partyMembers);
    }

    internal void KerStatus(ulong target, uint id, DateTime at, ReplayStatus? status)
    {
        if (!actors.ContainsKey(target)) return;
        foreach (var ker in kers.Values) ker.Status(target, id, status, at, partyMembers);
    }

    internal void Pyretic(ulong target, DateTime at, ReplayStatus? status, ulong source = 0)
    {
        var key = (target, status?.SourceId ?? source);
        if (!ActiveActors.Any(a => a.Oid == 0x1AB2) || actors.TryGetValue(target, out var actor) && actor.Dead
            || pyreticWatermarks.TryGetValue(key, out var old) && at < old) return;
        pyreticWatermarks[key] = at;
        pyreticEvents[key] = status;
    }

    internal void BossStatus(ulong target, uint status, DateTime at, ReplayStatus? value)
    {
        if (!actors.TryGetValue(target, out var actor) || !active.Contains(target)
            || !(actor.Oid == 0x360B && status is 2840 or 2841 or 2842 or 2958
                || actor.Oid == 0x3670 && status is >= 2835 and <= 2838)) return;
        var key = (target, status);
        if (bossStatusWatermarks.TryGetValue(key, out var old) && at < old) return;
        bossStatusWatermarks[key] = at;
        bossStatusEvents[key] = value;
        actors[target] = WithStatus(actor, status, value);
    }

    internal void IxtabStatus(ulong target, ReplayStatus? value, ulong source, DateTime at)
    {
        if (!actors.TryGetValue(target, out var actor)) return;
        foreach (var ixtab in ixtabs.Values) ixtab.Status(actor, value, source, at);
    }

    internal void SugrivaStatus(ulong target, ReplayStatus? value, ulong source, DateTime at)
    {
        if (!actors.TryGetValue(target, out var actor)) return;
        foreach (var sugriva in sugrivas.Values) sugriva.Status(actor, value, source, at);
    }

    private static ReplayActorState WithStatus(ReplayActorState actor, uint status, ReplayStatus? value)
    {
        var statuses = actor.Statuses.ToList();
        var index = statuses.FindIndex(s => s.Id == status);
        if (index >= 0)
        {
            if (value == null) statuses.RemoveAt(index);
            else statuses[index] = value;
        }
        else if (value != null) statuses.Add(value);
        return actor with { Statuses = statuses.ToArray() };
    }

    internal bool StopEverything(ReplayActorState? player, IEnumerable<ReplayPartyMember> party, DateTime now)
        => player != null && kers.Values.Any(k => k.Hold(player, partyMembers, now))
            || player is { Dead: false } && ActiveActors.Any(a => a.Oid == 0x1AB2)
            && party.Any(p => p.EntityId == player.Id)
            && (pyreticEvents.Any(p => p.Key.Target == player.Id && p.Value?.ExpiresAt > now)
                || player.Statuses.Any(s => s.Id == 960 && s.ExpiresAt > now && !pyreticEvents.ContainsKey((player.Id, s.SourceId))));

    internal void ObserveMisdirection(IReadOnlyList<ReplayPartyMember> party)
    {
        if (ActiveActors.Any(a => a.Oid is 0x41FC or 0x2873 or 0x35CF)) misdirection.Observe(actors, party);
    }

    internal void Misdirection(ulong target, uint status, bool present, IReadOnlyList<ReplayPartyMember> party)
    {
        if (ActiveActors.Any(a => a.Oid is 0x41FC or 0x2873 or 0x35CF)) misdirection.Status(target, status, present, party);
    }

    internal bool Misdirected(ReplayActorState? player, IReadOnlyList<ReplayPartyMember> party)
        => player != null && ActiveActors.Any(a => a.Oid is 0x41FC or 0x2873 or 0x35CF) && misdirection.Active(player.Id, party);

    internal void Finish(ulong source, uint action = 0, DateTime? at = null, ulong? targetId = null)
    {
        casts.TryGetValue(source, out var cast);
        FinishComponents(source, action == 0 ? cast?.Rule.Action ?? 0 : action, at, finishedTarget: targetId);
        if (!(at < cast?.StartedAt) && actors.TryGetValue(source, out var caster) && RaintrillerCaster(caster))
        {
            FinishBroadcastStacks(action == 0 ? cast?.Rule.Action ?? 0 : action, cast, targetId);
        }
        if (targetId is { } endingTarget)
            FinishStacks(source, action == 0 ? cast?.Rule.Action ?? 0 : action, endingTarget, at);
        if (cast == null || at < cast.StartedAt) return;
        if (action != 0 && action != cast.Rule.Action)
        {
            // SimpleAOEGroups accepts any actual finish from this caster; other components retain AID matching.
            if (IsGroupedHuntAoe(cast)) casts[source] = cast with { Shape = null };
            if (cast.Definition.Oid == 0x35CF && cast.Rule.GazeRange > 0) casts[source] = cast with { GazeCleared = true };
            return;
        }
        if (targetId == null) FinishStacks(source, cast.Rule.Action, cast.TargetId, at);
        ended[source] = (cast.Rule.Action, cast.StartedAt);
        casts.Remove(source);
        if (combos.TryGetValue(source, out var combo)) combo.Finish(cast.Definition.Oid, cast.Rule.Action);
        if (cast.Rule.Action == 17859 && thumps.TryGetValue(source, out var thump)) thump.Finish();
        if (predictions.TryGetValue(source, out var prediction)) prediction.Finish(cast.Definition.Oid, cast.Rule.Action);
        if (storedSequences.TryGetValue(source, out var stored)) stored.Finish(cast.Definition.Oid, cast.Rule.Action);
        if (rotations.TryGetValue(source, out var rotation)) rotation.Finish(cast.Rule.Action, at ?? observedTime ?? cast.Activation);
        if (sneezes.TryGetValue(source, out var sneeze)) sneeze.Finish(cast.Rule.Action, at ?? observedTime ?? cast.Activation, actors[source]);
        if (missiles.TryGetValue(source, out var missile)) missile.Finish(cast.Rule.Action, actors);
        if (targets.TryGetValue(source, out var target)) target.Finish(cast.Rule.Action);
        if (aegeiros.TryGetValue(source, out var tree)) tree.Finish(cast.Rule.Action);
        if (narrowRifts.TryGetValue(source, out var narrow)) narrow.Finish();
        if (omens.TryGetValue(source, out var omen)) omen.Finish(cast.Rule.Action);
        if (sambas.TryGetValue(source, out var samba)) samba.Finish(cast.Rule.Action);
        if (tarchias.TryGetValue(source, out var tarchia)) tarchia.Finish(cast, at ?? observedTime ?? cast.Activation);
    }

    internal IEnumerable<HuntDamagePrediction> PredictedDamage(IEnumerable<ReplayPartyMember> party)
    {
        var members = party.ToArray();
        var ids = members.Where(p => p.EntityId is not (0 or 0xE0000000)).Select(p => p.EntityId).Distinct().ToArray();
        foreach (var cast in casts.Values.Where(c => c.Rule.DamageType != null))
            foreach (var target in cast.Rule.DamageType == "Raidwide"
                ? ids.Where(id => actors.TryGetValue(id, out var actor) && !actor.Dead)
                : ids.Where(id => id == (cast.TargetId == cast.Source ? actors[cast.Source].TargetId : cast.TargetId)))
                yield return new(cast.Source, target, cast.Activation, cast.Rule.DamageType!);
        foreach (var (source, thump) in thumps)
            foreach (var target in members.Where(p => p.Slot is >= 0 and < 24).Select(p => p.EntityId).Distinct()
                .Where(id => actors.TryGetValue(id, out var actor) && !actor.Dead))
                yield return new(source, target, thump.Activation, "Raidwide");
        foreach (var target in targets.Values.Where(t => t.Active && t.Cast?.Rule.BaitRadius > 0 && ids.Contains(t.TargetId)))
            yield return new(target.Cast!.Source, target.TargetId, target.Cast.Activation, "Raidwide");
        foreach (var cast in delayedDamage.Values.SelectMany(d => d).Where(c => ids.Contains(c.TargetId)))
            yield return new(cast.Source, cast.TargetId, cast.Activation, "Tankbuster");
        foreach (var prediction in gunitts.Values.SelectMany(g => g.PredictedDamage(members))) yield return prediction;
        foreach (var prediction in pedantries.Values.SelectMany(p => p.PredictedDamage(members))) yield return prediction;
        foreach (var prediction in ixtabs.Values.SelectMany(i => i.PredictedDamage(members))) yield return prediction;
        foreach (var prediction in sugrivas.Values.SelectMany(s => s.PredictedDamage(members))) yield return prediction;
        foreach (var prediction in sansheyas.Values.SelectMany(s => s.PredictedDamage(members))) yield return prediction;
        foreach (var stack in StackStates.Where(s => s.Active && s.Cast?.Definition.Oid != 0x2852 && s.Forbidden.Count != 0))
        {
            // Each boss owns a separate upstream component and full-mask prediction guard.
            var cast = stack.Cast!;
            foreach (var id in members.Where(p => p.Slot is >= 0 and < 64).Select(p => p.EntityId).Distinct()
                .Where(id => id is not (0 or 0xE0000000) && !stack.Forbidden.Contains(id)))
                yield return new(cast.Source, id, cast.Activation, "Shared");
        }
    }

    internal IEnumerable<HuntPendingCast> TargetPreviews(ReplayActorState? player)
        => targets.Values.Select(t => t.Preview(player)).OfType<HuntPendingCast>().Concat(Cleaves())
            .Concat(CastBaits.Select(b => b.Preview(player)).OfType<HuntPendingCast>())
            .Concat(StackStates.Select(s => s.Preview(player, PartyActors(partyMembers), StackTargets(s, true))).OfType<HuntPendingCast>());

    internal IEnumerable<HuntPendingCast> TargetDangers(ReplayActorState? player, IEnumerable<ReplayPartyMember> party)
    {
        if (player == null) return [];
        var members = PartyActors(party);
        return targets.Values.SelectMany(t => t.Dangers(player, members, targets.Values
            .Where(other => other.Active && other.Cast?.Definition.Oid == t.Cast?.Definition.Oid
                && other.Cast?.Rule.Action == t.Cast?.Rule.Action && other.Cast?.Rule.SpreadRadius > 0
                && other.AttachedTargetId != 0)
            .Select(other => other.AttachedTargetId).ToHashSet()))
            .Concat(Cleaves().SelectMany(c => HuntAegeirosState.Dangers(c, actors[c.Source], player, members)))
            .Concat(StackNavigation(player, members))
            .Concat(CastBaits.SelectMany(b => b.Dangers(player, members)))
            .Concat(CloseRangeNavigation()).Concat(UrnaNavigation(player));
    }

    private IEnumerable<HuntPendingCast> CloseRangeNavigation()
    {
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x4580))
        {
            var definition = SimpleHuntCatalog.Find(actor.Oid)!;
            var rule = new HuntCastRule(0, "StayClose", null);
            var input = new ReplayCast(0, actor.Id, actor.Position, 0, DateTime.UnixEpoch, DateTime.UnixEpoch, false, false);
            yield return new(actor.Id, definition, rule, actor.Position, 0, DateTime.UnixEpoch, DateTime.UnixEpoch,
                actor.Id, false, true, new HuntStackJoinTemplate([actor.Position], 14), input, null,
                SequenceId: $"{actor.Id}/pkuucha-range");
        }
    }

    private IEnumerable<HuntPendingCast> StackNavigation(ReplayActorState player, ReplayActorState[] members)
    {
        foreach (var component in StackStates.GroupBy(s => (Source: s.Cast!.Source, Action: s.Cast.Rule.Action)))
        {
            var (source, action) = component.Key;
            var activeStacks = component.Where(s => s.Active).ToArray();
            if (activeStacks.Length == 0) continue;
            var radius = activeStacks[0].Radius;
            var navigation = activeStacks.SelectMany(s => s.Navigation(player, members, StackTargets(s))).ToArray();
            var joinAreas = navigation.Where(a => a.Shape is HuntStackJoinTemplate join && join.Radius == radius).ToArray();
            foreach (var area in navigation.Except(joinAreas)) yield return area;
            if (joinAreas.Length == 0) continue;
            var first = joinAreas[0] with { Activation = activeStacks[0].Cast!.Activation };
            if (joinAreas.Length == 1)
            {
                yield return first;
                continue;
            }
            var centers = joinAreas.SelectMany(a => ((HuntStackJoinTemplate)a.Shape!).Centers).ToArray();
            if (!stackJoins.TryGetValue(component.Key, out var combined) || !combined.Centers.SequenceEqual(centers))
                stackJoins[component.Key] = combined = new(centers, radius);
            yield return first with { Shape = combined, SequenceId = $"{source}/stack/join/combined/{action}" };
        }
    }

    internal IEnumerable<string> TargetHints(ReplayActorState? player, IEnumerable<ReplayPartyMember> party)
        => player == null ? [] : targets.Values.SelectMany(t => t.Hints(player, PartyActors(party)))
            .Concat(CastBaits.SelectMany(b => b.Hints(player, PartyActors(party))))
            .Concat(pedantries.Values.Any(p => p.TankbusterHint) ? ["Tankbuster cleave"] : [])
            .Concat(ixtabs.Values.Any(i => i.TankbusterHint) ? ["Tankbuster cleave"] : [])
            .Concat(sugrivas.Values.Any(s => s.TankbusterHint) ? ["Tankbuster cleave"] : [])
            .Concat(ixtabs.Values.SelectMany(i => i.Hints(player)))
            .Concat(Cleaves().Any(c => HuntAegeirosState.Hit(c, player)) ? ["GTFO from cleave!"] : [])
            .Concat(StackStates.SelectMany(s => s.Hints(player, PartyActors(party)))).Distinct();

    private HashSet<ulong> StackTargets(HuntStackState stack, bool includeDead = false)
        => StackStates.Where(s => s.Cast?.Source == stack.Cast?.Source && s.Cast?.Rule.Action == stack.Cast?.Rule.Action)
            .Select(s => includeDead ? s.ReferenceTargetId : s.AttachedTargetId).Where(id => id != 0).ToHashSet();

    private IEnumerable<HuntPendingCast> Cleaves()
    {
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x3671))
        {
            var casting = casts.ContainsKey(actor.Id) || actor.Cast is { } observed
                && !(ended.TryGetValue(actor.Id, out var finish) && finish.Action == observed.ActionId
                    && Math.Abs((finish.StartedAt - observed.StartedAt).TotalSeconds) < .5);
            if (HuntAegeirosState.Cleave(actor, actors.GetValueOrDefault(actor.TargetId), casting) is { } cleave)
                yield return cleave;
        }
    }

    private ReplayActorState[] PartyActors(IEnumerable<ReplayPartyMember> party)
        => party.Where(p => p.Slot is >= 0 and < 64).Select(p => p.EntityId).Distinct()
            .Where(id => actors.TryGetValue(id, out var actor) && !actor.Dead).Select(id => actors[id]).ToArray();

    internal IEnumerable<HuntMovementProjection> Knockbacks(ReplayActorState player)
    {
        foreach (var movement in aglaopes.Values.SelectMany(s => s.Knockbacks(player))) yield return movement;
        foreach (var movement in sugrivas.Values.SelectMany(s => s.Knockbacks(player))) yield return movement;
        foreach (var movement in urnas.SelectMany(p => p.Value.Knockbacks(player, actors[p.Key]))) yield return movement;
        foreach (var component in casts.Values.Where(c => c.Rule.KnockbackDistance > 0 || c.Rule.PullDistance > 0).OrderBy(c => c.Order)
            .GroupBy(c => (c.Definition.Oid, c.Rule.Action, Instance: c.Rule.PullFromCaster ? c.Source : 0)))
        {
            // Each upstream SimpleKnockbacks component has its own source list.
            var from = player.Position;
            foreach (var cast in component)
            {
                if (player.Statuses.Any(s => s.ExpiresAt > cast.Activation && s.Id is 3054 or 160 or 1209 or 1722 or 2663 or 2345)) continue;
                var origin = cast.Rule.PullFromCaster ? actors[cast.Source].Position : cast.Input.Location;
                var shape = cast.Rule.PullDistance > 0 ? new CircleTemplate(30) : cast.Shape;
                if (shape?.Resolve(origin, cast.Input.Rotation ?? 0, null).Contains(from, 0) != true) continue;
                var direction = from - origin;
                direction.Y = 0;
                if (direction.LengthSquared() == 0) continue;
                var distance = cast.Rule.KnockbackDistance;
                if (cast.Rule.PullDistance > 0)
                {
                    var minimum = cast.Rule.PullFromCaster ? actors[cast.Source].HitboxRadius + player.HitboxRadius : 0;
                    distance = -MathF.Min(cast.Rule.PullDistance, direction.Length() - minimum);
                    if (distance >= 0) continue;
                }
                var to = from + Vector3.Normalize(direction) * distance;
                yield return new(cast.Source, from, to);
                from = to;
            }
        }
    }

    internal float? Facing(ReplayActorState player, DateTime now)
    {
        var directions = Gazes(player).Where(c => c.Activation <= now.AddSeconds(.75)
            && InGazeRange(c, player) && (c.Input.Location.X != player.Position.X || c.Input.Location.Z != player.Position.Z))
            .Select(c => (Direction: Direction(c, player) + (c.Rule.InvertedGaze ? MathF.PI : 0),
                HalfAngle: c.Rule.InvertedGaze ? 3 * MathF.PI / 4 : MathF.PI / 4)).ToArray();
        if (directions.Length == 0) return null;
        var boundaries = directions.SelectMany(d => new[] { d.Direction - d.HalfAngle, d.Direction + d.HalfAngle })
            .Select(a => (a % MathF.Tau + MathF.Tau) % MathF.Tau).Order().ToArray();
        var candidates = new List<float> { player.Rotation };
        for (var i = 0; i < boundaries.Length; ++i)
        {
            var end = i + 1 < boundaries.Length ? boundaries[i + 1] : boundaries[0] + MathF.Tau;
            candidates.Add((boundaries[i] + end) * .5f);
        }
        return candidates.Cast<float?>().FirstOrDefault(candidate => directions.All(d =>
            MathF.Abs(MathF.Atan2(MathF.Sin(candidate!.Value - d.Direction), MathF.Cos(candidate.Value - d.Direction))) > d.HalfAngle));
    }

    internal IEnumerable<string> Hints(ReplayActorState? player)
    {
        if (AglaopePayloadMissing && aglaopes.Count > 0)
            yield return "Aglaope：旧效果输入缺少原始状态槽，魅惑待确认窗口无法还原。";
        foreach (var hint in casts.Values.Select(c => c.Rule.Hint).Where(h => !string.IsNullOrEmpty(h)).Distinct()) yield return hint!;
        foreach (var hint in thumps.Values.Select(t => t.Hint).Where(h => h != null).Distinct()) yield return hint!;
        foreach (var hint in wingblades.Values.Select(w => w.Hint).Where(h => h != null).Distinct()) yield return hint!;
        foreach (var hint in raintrillers.Values.Select(r => r.Hint).Where(h => h != null).Distinct()) yield return hint!;
        foreach (var hint in kers.Values.Select(k => k.GlobalHint).Where(h => h != null).Distinct()) yield return hint!;
        if (sugrivas.Values.Any(s => s.StackKnockbackHint)) yield return "Stack and knockback";
        if (hasted.Count != 0) yield return "Getting hit by the needle attack will instantly kill you from now on!";
        if (delayedDamage.Values.Any(d => d.Count != 0)) yield return "Tankbuster";
        if (casts.Values.Any(c => c.Definition.Oid == 0x1AB2 && c.Rule.Action == 7913))
            yield return "Applies Pyretic - STOP everything until it runs out!";
        if (player == null) yield break;
        foreach (var hint in FinalHuntHints(player)) yield return hint;
        foreach (var hint in kers.Values.Select(k => k.Hint(player, partyMembers, observedTime ?? default)).Where(h => h != null).Distinct())
            yield return hint!;
        if (Areas.Any(c => c.Risky && c.Shape?.Resolve(c.Location, c.Rotation, null).Contains(player.Position, 0) == true))
            yield return "GTFO from aoe!";
        if (Gazes(player).Any(c => !c.Rule.InvertedGaze && InGazeRange(c, player) && HitByGaze(c, player)))
            yield return "Turn away from gaze!";
        if (Gazes(player).Any(c => c.Rule.InvertedGaze && InGazeRange(c, player) && !HitByGaze(c, player)))
            yield return "Face the eye!";
        // SimpleBossModule's R30 is a local display/search view, not a physical wall.
        if (Knockbacks(player).Any(m => UnsafeLanding(m, player)))
            yield return "About to be knocked into danger!";
        var lastMarch = march.Movements(player, observedTime ?? default).LastOrDefault();
        if (lastMarch.From != lastMarch.To && ActiveActors.Any(a => a.Oid == 0x35BF
                ? Vector2.DistanceSquared(new(lastMarch.To.X, lastMarch.To.Z), new(a.Position.X, a.Position.Z)) <= 324
                : a.Oid == 0x35DB && Vector2.DistanceSquared(new(lastMarch.To.X, lastMarch.To.Z), new(player.Position.X, player.Position.Z)) > 900))
            yield return "Aim for safe spot!";
        if (rebellions.Values.Any(r => r.UnsafeMarch(player, observedTime ?? default)))
            yield return "Aim for safe spot!";
        var lastQueenMarch = threeSecondMarch.Movements(player, observedTime ?? default).LastOrDefault();
        if (lastQueenMarch.From != lastQueenMarch.To && queens.Values.SelectMany(q => q.Areas)
            .Any(a => a.Shape!.Resolve(a.Location, a.Rotation, null).Contains(lastQueenMarch.To, 0)))
            yield return "Aim outside of the AOE!";
        if (ihnuxokiy.Count != 0 && lastQueenMarch.From != lastQueenMarch.To
            && Vector2.DistanceSquared(new(lastQueenMarch.To.X, lastQueenMarch.To.Z), new(player.Position.X, player.Position.Z)) > 900)
            yield return "Aim for safe spot!";
        var poisonLanding = poisonMarch.Movements(player, observedTime ?? default).LastOrDefault();
        if (poisonLanding.From != poisonLanding.To && slippery.Values.Any(s => s.Unsafe(poisonLanding.To)))
            yield return "Aim for green safe spot!";
    }

    private bool UnsafeLanding(HuntMovementProjection movement, ReplayActorState player)
    {
        if (urnas.TryGetValue(movement.Source, out var urna)) return urna.Unsafe(movement.To);
        IEnumerable<HuntPendingCast> aoes;
        if (actors[movement.Source].Oid == 0x2853)
            aoes = predictions.TryGetValue(movement.Source, out var flood) ? flood.Areas : [];
        else if (actors[movement.Source].Oid == 0x2875)
            aoes = rotations.TryGetValue(movement.Source, out var rotation) ? rotation.Areas : [];
        else
            return Vector2.DistanceSquared(new(movement.To.X, movement.To.Z), new(player.Position.X, player.Position.Z)) > 900;
        return aoes.Any(a => a.Shape?.Resolve(a.Location, a.Rotation, null).Contains(movement.To, 0) == true);
    }

    internal IEnumerable<HuntPendingCast> Gazes(ReplayActorState player)
    {
        foreach (var cast in casts.Values.Where(c => c.Rule.GazeRange > 0 && !c.GazeCleared)) yield return cast;
        foreach (var sourceStacks in stacks.Values)
        {
            // BloodshotGaze reads each component's raw first entry, including a dead or detached target.
            var normal = sourceStacks.FirstOrDefault(s => s.Cast is { Definition.Oid: 0x4396, Rule.Action: 38515 });
            var selected = normal != null && !normal.IsTarget(player.Id) ? normal
                : sourceStacks.FirstOrDefault(s => s.Cast is { Definition.Oid: 0x4396, Rule.Action: 39668 });
            if (selected?.Cast is not { } cast || selected.IsTarget(player.Id) || selected.TargetPosition is not { } position) continue;
            yield return cast with
            {
                Input = cast.Input with { Location = position },
                Rule = cast.Rule with { GazeRange = 10000, InvertedGaze = cast.Rule.Action == 39668 },
                SequenceId = $"{cast.Source}/bloodshot/{cast.Order}"
            };
        }
    }

    internal static bool InGazeRange(HuntPendingCast gaze, ReplayActorState player)
        => Vector2.DistanceSquared(new(player.Position.X, player.Position.Z), new(gaze.Input.Location.X, gaze.Input.Location.Z))
            <= gaze.Rule.GazeRange * gaze.Rule.GazeRange;

    internal static bool HitByGaze(HuntPendingCast gaze, ReplayActorState player)
    {
        var delta = new Vector2(gaze.Input.Location.X - player.Position.X, gaze.Input.Location.Z - player.Position.Z);
        return delta.LengthSquared() > 0 && Vector2.Dot(new(MathF.Sin(player.Rotation), MathF.Cos(player.Rotation)),
            Vector2.Normalize(delta)) >= .707107f;
    }

    private static float Direction(HuntPendingCast gaze, ReplayActorState player)
        => MathF.Atan2(gaze.Input.Location.X - player.Position.X, gaze.Input.Location.Z - player.Position.Z);

    internal void Remove(ulong source, DateTime? at = null)
    {
        if (casts.TryGetValue(source, out var unobserved) && !unobserved.Observed && unobserved.Definition.Oid == 0x416E)
            foreach (var heshuala in heshualas.Values) heshuala.RetractUnobserved(unobserved);
        if (casts.TryGetValue(source, out var atticusCast)
            && (atticusCast.Observed || atticusCast.Definition.Oid is 0x4544 or 0x43DD or 0x416F or 0x281E)
            && atticusCast.Definition.Oid is 0x416D or 0x416E or 0x4233 or 0x298A or 0x2838 or 0x43DC or 0x28B6 or 0x35FC or 0x4544 or 0x43DD or 0x416F or 0x281E)
            Finish(source, at: at ?? (observedTime >= atticusCast.StartedAt ? observedTime : atticusCast.StartedAt));
        if (casts.TryGetValue(source, out var removedCast) && removedCast.Observed
            && removedCast.Definition.Oid is 0x457F or 0x2852)
            FinishComponents(source, removedCast.Rule.Action, at
                ?? (observedTime >= removedCast.StartedAt ? observedTime : removedCast.StartedAt));
        foreach (var ker in kers.Values) ker.Remove(source, partyMembers);
        foreach (var gunitt in gunitts.Values) gunitt.Remove(source, partyMembers);
        foreach (var sequence in atticus.Values) sequence.Remove(source);
        foreach (var neyoo in neyoos.Values) neyoo.Remove(source);
        foreach (var pedantry in pedantries.Values) pedantry.Remove(source);
        foreach (var ixtab in ixtabs.Values) ixtab.Remove(source);
        foreach (var rebellion in rebellions.Values) rebellion.Remove(source);
        foreach (var sugriva in sugrivas.Values) sugriva.Remove(source);
        RemoveFinalActor(source);
        if (beeStatuses.Keys.Any(k => k.Actor == source))
            foreach (var queen in queens.Values) queen.Lose(observedTime);
        foreach (var key in beeStatuses.Keys.Where(k => k.Actor == source).ToArray()) beeStatuses.Remove(key);
        foreach (var key in beeGains.Keys.Where(k => k.Actor == source).ToArray()) beeGains.Remove(key);
        foreach (var key in beeRemovals.Keys.Where(k => k.Actor == source).ToArray()) beeRemovals.Remove(key);
        foreach (var key in beeWatermarks.Keys.Where(k => k.Actor == source).ToArray()) beeWatermarks.Remove(key);
        foreach (var missile in missiles.Values) missile.Remove(source);
        foreach (var target in targets.Values) target.Remove(source);
        foreach (var stack in StackStates) stack.Remove(source);
        actors.Remove(source);
        casts.Remove(source);
        rips.Remove(source);
        combos.Remove(source);
        thumps.Remove(source);
        predictions.Remove(source);
        storedSequences.Remove(source);
        rotations.Remove(source);
        sneezes.Remove(source);
        missiles.Remove(source);
        compressors.Remove(source);
        targets.Remove(source);
        aegeiros.Remove(source);
        stacks.Remove(source);
        foreach (var key in stackJoins.Keys.Where(k => k.Source == source).ToArray()) stackJoins.Remove(key);
        narrowRifts.Remove(source);
        executions.Remove(source);
        omens.Remove(source);
        wingblades.Remove(source);
        sambas.Remove(source);
        forecasts.Remove(source);
        tygers.Remove(source);
        queens.Remove(source);
        raintrillers.Remove(source);
        kers.Remove(source);
        gunitts.Remove(source);
        atticus.Remove(source);
        heshualas.Remove(source);
        ihnuxokiy.Remove(source);
        neyoos.Remove(source);
        nechucihos.Remove(source);
        pedantries.Remove(source);
        ixtabs.Remove(source);
        slippery.Remove(source);
        rebellions.Remove(source);
        sugrivas.Remove(source);
        foreach (var key in componentStarts.Keys.Where(k => k.Owner == source || k.Source == source).ToArray()) componentStarts.Remove(key);
        initializingComponentOwners.Remove(source);
        if (queens.Count == 0 && ihnuxokiy.Count == 0) threeSecondMarch.Clear();
        if (slippery.Count == 0) poisonMarch.Clear();
        tarchias.Remove(source);
        march.Remove(source);
        threeSecondMarch.Remove(source);
        poisonMarch.Remove(source);
        stackStatusEvents.Remove(source);
        stackStatusWatermarks.Remove(source);
        delayedDamage.Remove(source);
        hasted.Remove(source);
        effects.RemoveWhere(e => e.Source == source);
        castComponentFinishes.RemoveWhere(c => c.Source == source);
        foreach (var key in pyreticEvents.Keys.Where(k => k.Target == source).ToArray()) pyreticEvents.Remove(key);
        foreach (var key in pyreticWatermarks.Keys.Where(k => k.Target == source).ToArray()) pyreticWatermarks.Remove(key);
        foreach (var key in bossStatusEvents.Keys.Where(k => k.Target == source).ToArray()) bossStatusEvents.Remove(key);
        foreach (var key in bossStatusWatermarks.Keys.Where(k => k.Target == source).ToArray()) bossStatusWatermarks.Remove(key);
        ended.Remove(source);
        engaged.Remove(source);
        active.Remove(source);
        foreach (var id in casts.Keys.Where(id => !active.Contains(id) && casts[id].Definition.Oid is 0x457F or 0x35CF or 0x2852 or 0x416D or 0x416E or 0x4582 or 0x4233 or 0x452C or 0x298A or 0x2838 or 0x43DC
            && !ActiveActors.Any(a => a.Oid == casts[id].Definition.Oid)).ToArray()) casts.Remove(id);
    }

    internal void Reset()
    {
        actors.Clear();
        casts.Clear();
        rips.Clear();
        combos.Clear();
        thumps.Clear();
        predictions.Clear();
        storedSequences.Clear();
        rotations.Clear();
        sneezes.Clear();
        missiles.Clear();
        compressors.Clear();
        targets.Clear();
        aegeiros.Clear();
        stacks.Clear();
        stackJoins.Clear();
        narrowRifts.Clear();
        executions.Clear();
        omens.Clear();
        wingblades.Clear();
        sambas.Clear();
        forecasts.Clear();
        tygers.Clear();
        queens.Clear();
        raintrillers.Clear();
        kers.Clear();
        gunitts.Clear();
        atticus.Clear();
        heshualas.Clear();
        ihnuxokiy.Clear();
        neyoos.Clear();
        nechucihos.Clear();
        pedantries.Clear();
        ixtabs.Clear();
        slippery.Clear();
        rebellions.Clear();
        sugrivas.Clear();
        ResetFinalHunts();
        componentStarts.Clear();
        initializingComponentOwners.Clear();
        rainFinishes.Clear();
        castComponentFinishes.Clear();
        beeStatuses.Clear();
        beeRemovals.Clear();
        beeGains.Clear();
        beeWatermarks.Clear();
        tarchias.Clear();
        march.Clear();
        threeSecondMarch.Clear();
        poisonMarch.Clear();
        stackStatusEvents.Clear();
        stackStatusWatermarks.Clear();
        partyMembers = [];
        delayedDamage.Clear();
        hasted.Clear();
        effects.Clear();
        pyreticEvents.Clear();
        pyreticWatermarks.Clear();
        bossStatusEvents.Clear();
        bossStatusWatermarks.Clear();
        ended.Clear();
        engaged.Clear();
        active.Clear();
        castOrder = 0;
        observedTime = null;
        misdirection.Clear();
    }

    private static bool Eligible(ReplayActorState actor)
        => actor.InCombat && !actor.Dead && actor.Hp != 0 && SimpleHuntCatalog.Owns(actor.Oid);

    private static bool Changed(ReplayCast before, ReplayCast after)
    {
        // Preserve the original float TotalTime. Older recordings only have reconstructed DateTimes,
        // whose elapsed-time subtraction can vary by a few 100ns ticks between otherwise identical samples.
        var durationChanged = before.TotalTime is { } previousTotal && after.TotalTime is { } currentTotal
            ? previousTotal != currentTotal
            : Math.Abs(((before.FinishAt - before.StartedAt) - (after.FinishAt - after.StartedAt)).TotalSeconds) > .000001;
        return before.TargetId != after.TargetId || before.Location != after.Location || before.Rotation != after.Rotation || durationChanged;
    }

    private HuntPendingCast Create(ulong source, HuntDefinition definition, HuntCastRule rule,
        ReplayActorState actor, ReplayCast cast, bool packetPose, Vector3? sourcePosition, DateTime? observedAt, long? order = null)
    {
        var location = cast.Location;
        var rotation = rule.FollowCasterRotation ? actor.Rotation : cast.Rotation ?? 0;
        var shape = rule.Shape;
        if (rule.ChargeHalfWidth > 0)
        {
            var origin = sourcePosition ?? actor.Position;
            var direction = cast.Location - origin;
            direction.Y = 0;
            const float gridSize = 2000f / 65535f;
            const float gridSizeInv = 1f / gridSize;
            float Quantize(float value) => ((int)MathF.Round(value * gridSizeInv) - .5f) * gridSize;
            location = new(Quantize(origin.X), origin.Y, Quantize(origin.Z));
            rotation = MathF.Atan2(direction.X, direction.Z);
            shape = direction.LengthSquared() > 0
                ? RayRectangleTemplate.Symmetric(rule.ChargeHalfWidth * 2, direction.Length()) : null;
        }
        var activation = cast.FinishAt;
        if (definition.Oid == 0x281E && !packetPose && observedAt is { } now)
        {
            // A new native CastInfo clamps elapsed to TotalTime; its NPC clock
            // still has the original float 0.3-second remainder after saturation.
            var total = cast.TotalTime ?? MathF.Max(0, (float)(cast.FinishAt - cast.StartedAt).TotalSeconds - .3f);
            var elapsed = Math.Clamp((float)(now - cast.StartedAt).TotalSeconds, 0, total);
            activation = now.AddSeconds((total + .3f) - elapsed);
        }
        return new(source, definition, rule, location, rotation, cast.StartedAt, activation,
            cast.TargetId, packetPose, !packetPose, shape, cast, packetPose ? null : cast, Order: order ?? ++castOrder,
            ObservedAt: observedAt, SourcePosition: sourcePosition ?? actor.Position);
    }
}
