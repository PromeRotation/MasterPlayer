using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsHades => territory == HadesCatalog.TERRITORY;
    private bool HadesPhaseAlive => IsHades && _actors.Values.Any(a => a.Oid == 0x294A && !a.Dead && !hadesDestroyed.Contains(a.Id));
    private int hadesKey;
    private readonly Dictionary<ulong, DungeonActor> hadesActors = [];
    private readonly HashSet<ulong> hadesSeen = [];
    private readonly HashSet<ulong> hadesDestroyed = [];
    private readonly HashSet<ulong> hadesWaitingRemoval = [];
    private readonly Dictionary<ulong, uint> hadesAnimations = [];
    private readonly List<DungeonAmbientAoe> hadesBrokenFaith = [];
    private readonly List<(Vector3 Position, int Key)> hadesTowers = [];
    private readonly List<HadesBait> hadesBaits = [];
    private readonly List<(DungeonPendingCast Cast, DungeonActor Target, bool Destroyed, int Key)> hadesStacks = [];
    private DungeonActor? HadesGaol => _actors.Values.FirstOrDefault(a => a.Oid == 0x294B && !hadesDestroyed.Contains(a.Id));
    private bool HadesGaolAlive => IsHades && HadesGaol is { Dead: false };
    private bool HadesForbidden(DungeonActor actor) => HadesGaolAlive && actor.Oid == 0x294A;

    private void HadesObserve()
    {
        if (!IsHades || Active == null) return;
        foreach (var id in hadesActors.Keys.Where(id => !_actors.ContainsKey(id) && !hadesDestroyed.Contains(id)).ToArray())
            HadesRemoved(id);
        hadesWaitingRemoval.RemoveWhere(id => !_actors.ContainsKey(id));
        foreach (var actor in _actors.Values)
        {
            if (hadesWaitingRemoval.Contains(actor.Id)) continue;
            hadesActors[actor.Id] = actor;
            if (!hadesSeen.Add(actor.Id)) continue;
            hadesDestroyed.Remove(actor.Id);
            if (actor.Oid == 0x1EAD29) hadesTowers.Add((actor.Position, ++hadesKey));
        }
        for (var i = 0; i < hadesBaits.Count; ++i)
        {
            var bait = hadesBaits[i];
            hadesBaits[i] = bait with
            {
                Source = !bait.SourceDestroyed && _actors.TryGetValue(bait.Source.Id, out var source) ? source : bait.Source,
                Target = !bait.TargetDestroyed && _actors.TryGetValue(bait.Target.Id, out var target) ? target : bait.Target,
            };
        }
        hadesBaits.RemoveAll(b => b.Action is 16723 or 16745 && b.Target.Dead
            || b.Action is 16728 or 16738 && (b.Source.Dead || b.SourceDestroyed || b.Target.Dead));
        for (var i = 0; i < hadesStacks.Count; ++i)
        {
            var stack = hadesStacks[i];
            if (!stack.Destroyed && _actors.TryGetValue(stack.Target.Id, out var target))
                hadesStacks[i] = stack with { Target = target };
        }
        HadesObserveTethers();
    }

    private void HadesAnimation(ulong source, uint state, bool eventInput)
    {
        if (!IsHades || Active == null || hadesWaitingRemoval.Contains(source)
            || !_actors.TryGetValue(source, out var actor) || actor.Oid != 0x1EAD28) return;
        if (!eventInput && hadesAnimations.TryGetValue(source, out var previous) && previous == state) return;
        hadesAnimations[source] = state;
        if (state == 0x00010002)
            hadesBrokenFaith.Add(new($"hades/faith/{++hadesKey}", "破碎信仰落圈", new CircleTemplate(10),
                actor.Position, actor.Rotation, Activation: _now.AddSeconds(8.5)));
    }

    private void HadesCast(DungeonPendingCast cast)
    {
        if (!IsHades) return;
        if (cast.Rule.Action == 17812 && _actors.TryGetValue(cast.TargetId, out var marked))
            hadesStacks.Add((cast, marked, false, ++hadesKey));
        if (cast.Rule.Action is 16728 or 16738 or 17811 or 16751 && _actors.TryGetValue(cast.Source, out var source)
            && _actors.TryGetValue(cast.TargetId, out var target))
            hadesBaits.Add(new(source, target, cast.Rule.Action, cast.FinishAt, ++hadesKey));
    }

    private void HadesFinish(DungeonPendingCast cast)
    {
        if (!IsHades) return;
        if (cast.Rule.Action is 17811 or 16751)
        {
            var index = hadesBaits.FindIndex(b => b.Action == cast.Rule.Action && b.Source.Id == cast.Source);
            if (index >= 0) hadesBaits.RemoveAt(index);
        }
        if (cast.Rule.Action != 17812) return;
        var stack = hadesStacks.FindIndex(s => s.Target.Id == cast.TargetId);
        if (stack >= 0) hadesStacks.RemoveAt(stack);
    }

    private void HadesIcon(ulong target, uint icon, DateTime at)
    {
        if (!IsHades || Active == null || icon is not (139 or 120) || !_actors.TryGetValue(target, out var marked)
            || !hadesActors.TryGetValue(PrimaryId, out var source)) return;
        hadesBaits.Add(new(source, marked, icon == 139 ? 16723u : 16745u, at.AddSeconds(icon == 139 ? 3 : 4.7),
            ++hadesKey, hadesDestroyed.Contains(source.Id)));
    }

    private void HadesConsumeBait(uint action)
    {
        var index = hadesBaits.FindIndex(b => b.Action == action);
        if (index >= 0) hadesBaits.RemoveAt(index);
    }

    private void HadesEffect(ulong source, uint action)
    {
        if (!IsHades) return;
        // DarkEruption's override calls its FIFO base and then consumes another entry.
        if (action == 16723) HadesConsumeBait(16723);
        if (action is 16723 or 16755) HadesConsumeBait(16723);
        if (action == 16745) HadesConsumeBait(16745);
        if (action is 16728 or 16738)
        {
            var index = hadesBaits.FindIndex(b => b.Action == action && b.Source.Id == source);
            if (index >= 0) hadesBaits.RemoveAt(index);
        }
        if (action != 16755) return;
        // The source resets its batch clock before pruning dead targets.
        if (!hadesBaits.Any(b => b.Action == 16755)) hadesTetherAt = default;
        else hadesBaits.RemoveAll(b => b.Action == 16755 && b.Target.Dead);
    }

    private void HadesRemoved(ulong source)
    {
        if (!IsHades || !hadesDestroyed.Add(source)) return;
        if (_actors.ContainsKey(source)) hadesWaitingRemoval.Add(source);
        if (_casts.TryGetValue(source, out var cast)) HadesFinish(cast);
        if (hadesActors.TryGetValue(source, out var actor))
        {
            if (actor.Oid == 0x1EAD29) hadesTowers.Clear();
            if (actor.Oid == 0x1EAD28)
                hadesBrokenFaith.RemoveAll(a => a.Position.X == actor.Position.X && a.Position.Z == actor.Position.Z);
        }
        if (hadesTethers.Remove(source, out var old)) HadesTetherChange(source, old, false);
        hadesTetherPending.Remove(source);
        for (var i = 0; i < hadesBaits.Count; ++i)
            hadesBaits[i] = hadesBaits[i] with
            {
                SourceDestroyed = hadesBaits[i].SourceDestroyed || hadesBaits[i].Source.Id == source,
                TargetDestroyed = hadesBaits[i].TargetDestroyed || hadesBaits[i].Target.Id == source,
            };
        hadesBaits.RemoveAll(b => b.Action is 16728 or 16738 && b.SourceDestroyed);
        for (var i = 0; i < hadesStacks.Count; ++i)
            if (hadesStacks[i].Target.Id == source) hadesStacks[i] = hadesStacks[i] with { Destroyed = true };
        hadesSeen.Remove(source); hadesAnimations.Remove(source);
    }

    private void ResetHades()
    {
        hadesKey = 0; hadesActors.Clear(); hadesSeen.Clear(); hadesDestroyed.Clear(); hadesWaitingRemoval.Clear();
        hadesAnimations.Clear(); hadesBrokenFaith.Clear(); hadesTowers.Clear(); hadesBaits.Clear(); hadesStacks.Clear();
        hadesTethers.Clear(); hadesTetherPending.Clear(); hadesTetherAt = default;
    }
}
