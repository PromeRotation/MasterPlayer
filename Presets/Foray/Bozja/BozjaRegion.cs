using System.Numerics;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed class BozjaRegion(uint territory = BozjaCatalog.TERRITORY)
{
    internal BozjaEncounter[] Encounters { get; } = BozjaCatalog.Definitions
        .Where(d => d.Territory == territory).Select(d => new BozjaEncounter(d)).ToArray();
    internal List<BozjaEncounter> Loaded { get; } = [];
    internal BozjaEncounter? Selected { get; private set; }
    private readonly Dictionary<BozjaEncounter, long> versions = [];
    private readonly Dictionary<ulong, (ReplayActorState Actor, long Order)> actors = [];
    private long order;
    private ulong managerPlayer;

    internal void Synchronize(ReplayActorState[] input, ReplayPartyMember[] party, ulong source,
        DateTime now, bool update, Action? beforeUpdate = null, ReplayActiveFate? fate = null)
    {
        if (!update)
        {
            // Event sampling is not a Feed frame. Keep actor creation order,
            // roster and callbacks at the single Framework commit boundary.
            foreach (var model in Encounters) model.Advance(now);
            return;
        }
        var models = Loaded.Concat(Encounters).Distinct().ToArray();
        foreach (var model in models) model.Advance(now);
        var present = input.ToDictionary(a => a.Id);
        foreach (var old in actors.Where(kv => !present.TryGetValue(kv.Key, out var next)
            || BozjaEncounter.NewActor(kv.Value.Actor, next)).Select(kv => kv.Key).ToArray())
        {
            foreach (var model in models) model.PrepareRemoveActor(old);
            foreach (var model in models) model.FinishRemovedActor(old);
            foreach (var model in models) model.CompleteRemoveActor(old);
            actors.Remove(old);
        }
        // Feed commits one actor across all subscribers before the next.
        // A failing Finish must block later input in every encounter.
        foreach (var value in input)
        {
            var nextOrder = !actors.TryGetValue(value.Id, out var previous)
                || BozjaEncounter.NewActor(previous.Actor, value) ? ++order : previous.Order;
            var created = !actors.ContainsKey(value.Id);
            // Feed installs a new tracked snapshot before Created/Start; an
            // existing snapshot commits only after every callback succeeds.
            if (created) actors[value.Id] = (value, nextOrder);
            foreach (var model in models) model.PrepareActor(value, nextOrder);
            var castChanged = BozjaEncounter.InputCastChanged(created ? null : previous.Actor, value);
            foreach (var model in models) model.FinishActorCast(value, castChanged);
            // ActorState installs CastInfo for every subscriber before Start.
            // A failed Start retains that physical cast, while Feed keeps the
            // prior committed input and may retry its operation next frame.
            var castChanges = models.Select(model => model.InstallActorCast(value, castChanged)).ToArray();
            for (var i = 0; i < models.Length; ++i)
                models[i].CompleteActor(value, installedCastChanged: castChanges[i]);
            foreach (var model in models) model.CommitActorInput(value);
            actors[value.Id] = (value, nextOrder);
        }
        foreach (var model in models)
        {
            model.CompleteSynchronize(party, source);
            model.SetFate(fate);
        }
        managerPlayer = party.FirstOrDefault(p => p.Slot == 0)?.EntityId ?? 0;
        Reconcile();
        beforeUpdate?.Invoke();
        Update(now);
    }

    internal void Update(DateTime now)
    {
        foreach (var model in Loaded.Concat(Encounters).Distinct().ToArray())
            model.Update(now, () => ++order);
        Reconcile();
        if (!actors.ContainsKey(managerPlayer)) return;
        var best = Loaded.OrderByDescending(Priority).FirstOrDefault();
        if (Priority(best) > Priority(Selected)) Selected = best;
    }

    private void Reconcile()
    {
        foreach (var old in Loaded.Where(m => m.Primary == null
            || versions.GetValueOrDefault(m) != m.LoadVersion).ToArray())
        {
            Loaded.Remove(old);
            if (ReferenceEquals(Selected, old)) Selected = null;
        }
        foreach (var model in Encounters.Where(m => m.Primary != null && !Loaded.Contains(m))
            .OrderBy(m => m.LoadedFromReset)
            .ThenBy(m => m.LoadedFromPending)
            .ThenBy(m => m.LoadedFromPending ? -m.LoadOrder : m.LoadOrder))
        {
            Loaded.Add(model);
            versions[model] = model.LoadVersion;
        }
    }

    private static int Priority(BozjaEncounter? model)
        => model == null ? 0 : model.Active ? 4
            : model.Primary is { Destroyed: false, Actor: { Dead: false, Targetable: true } } ? 3 : 1;

    internal void Director(uint command)
    {
        foreach (var model in Encounters) model.Director(command);
        Reconcile();
    }

    internal void Effect(ulong source, uint action, ulong target = 0, Vector3? position = null)
    {
        foreach (var model in Encounters) model.Effect(source, action, target, position);
    }

    internal void Tether(ulong source, uint id, ulong target)
    {
        foreach (var model in Encounters) model.Tether(source, id, target);
    }

    internal void EAnim(ulong source, uint state)
    {
        foreach (var model in Encounters) model.EAnim(source, state);
    }

    internal void Icon(ulong source, uint icon, ulong target)
    {
        foreach (var model in Encounters) model.Icon(source, icon, target);
    }

    internal void Clear()
    {
        foreach (var model in Encounters) model.Clear();
        Loaded.Clear();
        versions.Clear();
        actors.Clear();
        Selected = null;
        order = 0;
        managerPlayer = 0;
    }

    internal void MapEffect(byte index, uint state)
    {
        foreach (var model in Encounters) model.MapEffect(index, state);
    }
}
