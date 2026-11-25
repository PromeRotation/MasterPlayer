using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsPalaceGuideBoss => IsNybeth || IsGudanna;
    internal bool IsPalaceManagedBoss => IsPalaceGuideBoss || IsGodmother || IsBlackRider || IsYaquaru;
    private readonly Dictionary<ulong, AlphaActorState> palaceGuideActors = [];
    private PalaceGuideModule? palaceGuideLoaded;
    private readonly List<PalaceGuideModule> palaceGuidePending = [];
    private bool palaceGuideWiping;
    private bool palaceGuideEnabled;

    private void PalaceGuideObserve(Func<string, bool>? enabled)
    {
        if (!IsPalaceManagedBoss) return;
        palaceGuideEnabled = enabled?.Invoke(Definitions[0].Name) != false;
        PalaceGuideInput();
        var player = _actors.Values.FirstOrDefault(a => a.PartySlot == 0);
        if (player == null) return;
        for (var i = palaceGuidePending.Count - 1; i >= 0; --i)
        {
            var module = palaceGuidePending[i];
            if (module.Primary.Removed || module.Primary.Actor.Dead)
                palaceGuidePending.RemoveAt(i);
            else if (palaceGuideLoaded == null && Vector3.DistanceSquared(player.Position, module.Primary.Actor.Position) <= 500 * 500)
            {
                palaceGuideLoaded = module;
                palaceGuidePending.RemoveAt(i);
            }
        }
        if (palaceGuideLoaded is not { } loaded) return;
        var wasActive = loaded.Active;
        var primary = loaded.Primary.Actor;
        if (!palaceGuideWiping)
        {
            if (!loaded.Started && primary.Targetable && primary.InCombat) loaded.Started = true;
            if (loaded.Active && (primary.Dead || loaded.Primary.Removed || primary.Hp == 0)) loaded.Finished = true;
        }
        if (!loaded.Active && Vector3.DistanceSquared(player.Position, primary.Position) > 500 * 500)
        {
            palaceGuideLoaded = null;
            if (!loaded.Primary.Removed) palaceGuidePending.Add(new(loaded.Primary));
        }
        else if (!loaded.Active && (wasActive || loaded.Primary.Removed))
            palaceGuideLoaded = null;
    }

    private void PalaceGuideInput()
    {
        if (!IsPalaceManagedBoss) return;
        foreach (var id in palaceGuideActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
            PalaceGuideRemoved(id);
        foreach (var actor in _actors.Values)
        {
            if (palaceGuideActors.TryGetValue(actor.Id, out var state)) state.Actor = actor;
            else
            {
                var created = new AlphaActorState(actor);
                palaceGuideActors.Add(actor.Id, created);
                if (actor.Oid != Definitions[0].BossOid) continue;
                // Added rebinds a still-loaded same-ID module; it does not reconstruct its components.
                if (palaceGuideLoaded?.Primary.Actor.Id == actor.Id) palaceGuideLoaded.Primary = created;
                else if (palaceGuideLoaded == null && _actors.Values.FirstOrDefault(a => a.PartySlot == 0) is { } player
                    && Vector3.DistanceSquared(player.Position, actor.Position) <= 500 * 500)
                    palaceGuideLoaded = new(created);
                else palaceGuidePending.Add(new(created));
            }
        }
    }

    private void PalaceGuideRemoved(ulong id)
    {
        if (palaceGuideActors.Remove(id, out var state)) state.Removed = true;
    }

    internal string? PalaceGuidePrecombatHint
    {
        get
        {
            if (!IsPalaceGuideBoss || !palaceGuideEnabled || palaceGuideLoaded is not { Started: false }) return null;
            return IsNybeth ? HeavenswardIntroCatalog.NYBETH_GUIDE : GudannaGuide;
        }
    }

    private bool PalaceGuideCanPull(DungeonActor actor) => palaceGuideLoaded is { Active: true } loaded
        && loaded.Primary.Actor.Id == actor.Id;

    internal void PalaceGuideDirector(uint update)
    {
        if (!IsPalaceManagedBoss) return;
        if (update == 0x40000005)
        {
            ResetFight();
            palaceGuideLoaded = null;
            palaceGuideWiping = true;
        }
        else if (update == 0x40000011) palaceGuideWiping = false;
    }

}
