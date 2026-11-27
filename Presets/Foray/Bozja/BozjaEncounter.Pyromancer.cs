using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsPyromancer => Definition.Key == "PyromancerSupreme";
    internal Dictionary<BozjaActor, PyromancerConstruction> PyromancerConstructions { get; } = [];
    internal bool HasConstructionDrawing => IsPyromancer && Primary != null
        && PyromancerConstructions.GetValueOrDefault(Primary)?.ThermalActive == true;

    internal IEnumerable<BozjaCast> ConstructionCasts => HasConstructionDrawing
        ? PyromancerConstructions[Primary!].Casts : [];

    private void PyromancerCreated(BozjaActor actor)
    {
        if (!IsPyromancer || actor.Actor.Oid != Definition.PrimaryOid) return;
        if (Primary is { Destroyed: true } old && old.Actor.Id == actor.Actor.Id
            && PyromancerConstructions.Remove(old, out var existing))
        {
            // Manager rebinds the loaded module's PrimaryActor before Update.
            PyromancerConstructions[actor] = existing;
            return;
        }
        ReconstructPyromancer(actor);
    }

    private void ReconstructPyromancer(BozjaActor primary)
    {
        if (!IsPyromancer) return;
        var construction = new PyromancerConstruction();
        PyromancerConstructions[primary] = construction;
        foreach (var actor in Actors.Values) PyromancerThermalStart(actor, construction);
    }

    private void PyromancerThermalStart(BozjaActor actor, PyromancerConstruction construction)
    {
        if (!construction.ThermalActive || !actor.NativeNpc || actor.Cast is not { ActionId: 21022 } cast) return;
        construction.Casts.Add(new(++serial, BozjaPyromancerCatalog.ThermalShock, actor,
            cast.Location, cast.Rotation ?? 0, Activation(actor)) { Action = cast.ActionId });
    }

    private void PyromancerThermalStart(BozjaActor actor)
    {
        foreach (var construction in PyromancerConstructions.Values) PyromancerThermalStart(actor, construction);
    }

    private void PyromancerThermalFinish(BozjaActor actor, ReplayCast spell)
    {
        if (spell.ActionId != 21022) return;
        foreach (var construction in PyromancerConstructions.Values.Where(c => c.ThermalActive))
        {
            var index = construction.Casts.FindIndex(c => c.Source.Actor.Id == actor.Actor.Id);
            if (index >= 0) construction.Casts.RemoveAt(index);
        }
    }

    private void PyromancerThermalEffect(ulong source, uint action)
    {
        if (action != 21022 || Actors.GetValueOrDefault(source) is not { NativeNpc: true }) return;
        foreach (var construction in PyromancerConstructions.Values.Where(c => c.ThermalActive)) ++construction.NumCasts;
    }

    private void PyromancerEnterPhase()
    {
        if (!IsPyromancer || Primary == null) return;
        var construction = PyromancerConstructions[Primary];
        construction.ThermalActive = false;
        construction.Casts.Clear();
    }

    private bool PrimaryPhaseComplete(BozjaActor primary)
    {
        if (IsCll1) return Cll1Complete(primary);
        if (primary.Destroyed || primary.Actor.Dead) return true;
        if (IsPyromancer) return !primary.Actor.Targetable;
        if (Definition.PrimaryOid == 0x2F06) return primary.Actor.Hp == 1;
        return primary.Actor.Hp == 0;
    }
}
