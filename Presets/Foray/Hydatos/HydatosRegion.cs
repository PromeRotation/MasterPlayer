using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Hydatos;

// All loaded encounters receive the world/events. Only Selected publishes
// output; a tie preserves the existing selection, as in BossModuleManager.
internal sealed class HydatosRegion
{
    internal HydatosEncounter[] Encounters { get; } = HydatosCatalog.Definitions.Select(d => new HydatosEncounter(d)).ToArray();
    internal List<HydatosEncounter> Loaded { get; } = [];
    internal HydatosEncounter? Selected { get; private set; }
    private readonly Dictionary<HydatosEncounter, long> versions = [];

    internal void Synchronize(ReplayActorState[] actors, ReplayPartyMember[] party, ulong sourcePlayer, DateTime now)
    {
        foreach (var model in Encounters) model.Synchronize(actors, now, sourcePlayer, party);
        foreach (var old in Loaded.Where(m => m.Primary is not { Destroyed: false } || m.Completed
            || versions.GetValueOrDefault(m) != m.LoadVersion).ToArray())
        {
            Loaded.Remove(old);
            if (ReferenceEquals(Selected, old)) Selected = null;
        }
        foreach (var model in Encounters.Where(m => m.Primary is { Destroyed: false } && !m.Completed
            && !Loaded.Contains(m)).OrderBy(m => m.LoadOrder))
        {
            Loaded.Add(model);
            versions[model] = model.LoadVersion;
        }
        var best = Loaded.OrderByDescending(Priority).FirstOrDefault();
        if (Priority(best) > Priority(Selected)) Selected = best;
    }

    private static int Priority(HydatosEncounter? model)
        => model == null ? 0 : model.Active ? 4
            : model.Primary is { Destroyed: false, Actor: { Dead: false, Targetable: true } } ? 3 : 1;

    internal void Icon(ulong source, uint icon, ulong target)
    {
        foreach (var model in Loaded) model.Icon(source, icon, target);
    }

    internal void Effect(ulong source, uint action, float? rotation, ulong target)
    {
        foreach (var model in Loaded) model.Effect(source, action, rotation, target);
    }

    internal void Clear()
    {
        foreach (var model in Encounters) model.Clear();
        Loaded.Clear();
        versions.Clear();
        Selected = null;
    }
}
