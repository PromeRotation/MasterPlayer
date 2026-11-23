using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsUltimaExtreme => territory == UltimaExtremeCatalog.TERRITORY;
    private bool ultimaWiping;
    private DungeonActor? ultimaGaruda;
    private DateTime ultimaMistralAt;
    private bool ultimaBurstImminent;
    private DungeonActor? ultimaIfrit;
    private DateTime ultimaCycloneAt;
    private DungeonActor? ultimaEye;
    private DungeonActor? ultimaGeocrush;
    private readonly List<DungeonActor> ultimaFreefires = [];
    private DateTime ultimaFreefireAt;
    private int ultimaFreefireHits;
    private int ultimaPurgeHits;
    private readonly HashSet<ulong> ultimaKiters = [];
    private readonly HashSet<ulong> ultimaExploded = [];
    private int ultimaBoomHits;
    private bool ultimaWaitingOrbs;
    private readonly List<ulong> ultimaActiveOrbs = [];
    private readonly List<ulong> ultimaPopOrbs = [];
    private bool UltimaMistralActive => ultimaGaruda != null && (ultimaMistralAt - _now).TotalSeconds <= 6;
    private bool UltimaBoomCasting => IsUltimaExtreme && _casts.Values.Any(c => c.Rule.Action == 1511);
    private bool UltimaOrbsActive => ultimaWaitingOrbs || ultimaPopOrbs.Count > 0;
    private bool UltimaTankSwap => IsUltimaExtreme && _actors.GetValueOrDefault(PrimaryId) is { } boss
        && _actors.GetValueOrDefault(boss.TargetId) is { PartySlot: >= 0 } tank && HasStatus(tank, 369) && UltimaStacks(tank) >= 4;

    private int UltimaStacks(DungeonActor actor)
        => statusChanges.TryGetValue((actor.Id, 369), out var change) && _now < change.ChangedAt.AddSeconds(.5)
            ? change.Param : actor.StatusParams?.GetValueOrDefault(369u) ?? 0;

    private void UltimaDirector(uint command)
    {
        if (!IsUltimaExtreme) return;
        if (command == 0x40000005) { ultimaWiping = true; ResetFight(); }
        if (command == 0x40000011) ultimaWiping = false;
    }

    private void UltimaObserve()
    {
        if (!IsUltimaExtreme || Active == null) return;
        DungeonActor? Refresh(DungeonActor? actor) => actor == null ? null : _actors.GetValueOrDefault(actor.Id) ?? actor;
        ultimaGaruda = Refresh(ultimaGaruda); ultimaIfrit = Refresh(ultimaIfrit);
        ultimaEye = Refresh(ultimaEye); ultimaGeocrush = Refresh(ultimaGeocrush);
        for (var i = 0; i < ultimaFreefires.Count; ++i) ultimaFreefires[i] = Refresh(ultimaFreefires[i])!;
        ultimaActiveOrbs.RemoveAll(id => !_actors.ContainsKey(id));
        ultimaPopOrbs.RemoveAll(id => !_actors.ContainsKey(id));
        if (!ultimaWaitingOrbs && UltimaBoomCasting) ultimaWaitingOrbs = true;
        if (!ultimaWaitingOrbs) return;
        var orbs = _actors.Values.Where(a => a.Oid == 0x914).ToArray();
        if (orbs.Length != 2 * (ultimaBoomHits + 1)) return;
        ultimaActiveOrbs.AddRange(orbs.Select(a => a.Id));
        void Add(Func<DungeonActor, bool> match) => ultimaPopOrbs.AddRange(orbs.Where(match).Select(a => a.Id));
        switch (ultimaBoomHits)
        {
            case 1: Add(a => a.Position.Z > 17); Add(a => a.Position.X > 17); break;
            case 2:
                Add(a => a.Position.Z > 8); Add(a => a.Position.X > 15 && a.Position.Z > 0); Add(a => a.Position.X > 15 && a.Position.Z < 0);
                break;
            case 3:
                Add(a => a.Position.Z > 9 && a.Position.X < 0); Add(a => a.Position.Z > 9 && a.Position.X > 0);
                Add(a => a.Position.Z < -9 && a.Position.X > 0); Add(a => a.Position.Z < -9 && a.Position.X < 0);
                break;
        }
        ultimaWaitingOrbs = false;
    }

    private void UltimaCast(DungeonPendingCast cast)
    {
        if (!IsUltimaExtreme || _actors.GetValueOrDefault(cast.Source) is not { } source) return;
        switch (cast.Rule.Action)
        {
            case 1511: ultimaWaitingOrbs = true; break;
            case 1517: ultimaGaruda = source; ultimaMistralAt = cast.FinishAt; ultimaBurstImminent = true; break;
            case 1525: ultimaIfrit = source; ultimaCycloneAt = cast.FinishAt; break;
            case 1519: ultimaEye = source; break;
            case 1520: ultimaGeocrush = source; break;
        }
    }

    private void UltimaFinish(uint action)
    {
        if (!IsUltimaExtreme) return;
        switch (action)
        {
            case 1517: ultimaGaruda = null; break;
            case 1525: ultimaIfrit = null; break;
            case 1519: ultimaEye = null; break;
            case 1520: ultimaGeocrush = null; break;
        }
    }

    private void UltimaTimeline(ulong source, uint timeline, DateTime at)
    {
        if (!IsUltimaExtreme || _actors.GetValueOrDefault(source) is not { } actor) return;
        if (actor.Oid == 0x912 && timeline == 0x0588)
        {
            ultimaGaruda = actor; ultimaMistralAt = at.AddSeconds(6.6); ultimaBurstImminent = true;
        }
        if (actor.Oid == 0x910 && timeline == 0x008D) { ultimaIfrit = actor; ultimaCycloneAt = at.AddSeconds(5); }
        if (actor.Oid == 0x1B2 && timeline == 0x0449) { ultimaFreefires.Add(actor); ultimaFreefireAt = at.AddSeconds(6); }
    }

    private void UltimaEffect(ulong source, uint action, ulong target)
    {
        if (!IsUltimaExtreme) return;
        switch (action)
        {
            case 1518: ultimaBurstImminent = false; break;
            case 1515:
                ++ultimaFreefireHits;
                var index = ultimaFreefires.FindIndex(a => a.Id == source);
                if (index >= 0) ultimaFreefires.RemoveAt(index);
                break;
            case 1514: ++ultimaPurgeHits; break;
            case 1511: ++ultimaBoomHits; break;
            case 1512: ultimaActiveOrbs.Remove(source); ultimaPopOrbs.Remove(source); break;
            case 1650:
                if (_actors.GetValueOrDefault(target) is { PartySlot: >= 0 }) ultimaKiters.Add(target);
                break;
            case 1510:
                ultimaExploded.Add(source);
                if (ultimaExploded.Count == ultimaKiters.Count) { ultimaKiters.Clear(); ultimaExploded.Clear(); }
                break;
        }
    }

    private void ResetUltima()
    {
        ultimaGaruda = ultimaIfrit = ultimaEye = ultimaGeocrush = null;
        ultimaFreefires.Clear(); ultimaKiters.Clear(); ultimaExploded.Clear(); ultimaActiveOrbs.Clear(); ultimaPopOrbs.Clear();
        ultimaFreefireHits = ultimaPurgeHits = ultimaBoomHits = 0;
        ultimaBurstImminent = ultimaWaitingOrbs = false;
    }
}
