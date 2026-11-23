using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsShinryu => territory == ShinryuCatalog.TERRITORY;
    private bool shinryuLightning;
    private bool shinryuFire;
    private DateTime shinryuWaterAt;
    private readonly List<(Vector3 Position, DateTime At, int Key)> shinryuIce = [];
    private readonly List<(DungeonActor Target, uint Icon, DateTime At, int Key)> shinryuIcons = [];
    private readonly List<DungeonPendingCast> shinryuCastOrder = [];
    private readonly Dictionary<ulong, DungeonActor> shinryuActors = [];
    private readonly HashSet<ulong> shinryuFettered = [];
    private readonly Dictionary<ulong, bool> shinryuFetterStates = [];
    private readonly Dictionary<ulong, bool> shinryuFetterPending = [];
    private readonly Dictionary<ulong, bool> shinryuSlidePending = [];
    private readonly Dictionary<ulong, uint> shinryuAnimations = [];
    private readonly List<Vector3> shinryuBroken = [];
    private Vector3? shinryuPlatformCenter;
    private DungeonPendingCast? shinryuTankCast;
    private DungeonActor? shinryuTankTarget;
    private int shinryuKey;

    private void ShinryuCast(DungeonPendingCast cast)
    {
        if (!IsShinryu) return;
        var index = shinryuCastOrder.FindIndex(c => c.Source == cast.Source);
        if (index < 0) shinryuCastOrder.Add(cast);
        else shinryuCastOrder[index] = cast;
        if (cast.Rule.Action == 8091) { shinryuLightning = true; shinryuWaterAt = cast.StartedAt.AddSeconds(6); }
        if (cast.Rule.Action == 8107) { shinryuFire = true; shinryuWaterAt = cast.StartedAt.AddSeconds(10); }
        if (cast.Rule.Action == 8100)
        {
            shinryuTankCast = cast;
            shinryuTankTarget = _actors.GetValueOrDefault(cast.TargetId);
        }
    }

    private void ShinryuFinish(DungeonPendingCast cast)
    {
        if (!IsShinryu) return;
        shinryuCastOrder.RemoveAll(c => c.Source == cast.Source);
        if (cast.Rule.Action == 8091) shinryuLightning = false;
        if (cast.Rule.Action == 8107) shinryuFire = false;
        if (shinryuTankCast?.Source == cast.Source) { shinryuTankCast = null; shinryuTankTarget = null; }
        if (cast.Rule.Action == 8097 && (_actors.GetValueOrDefault(cast.Source) ?? shinryuActors.GetValueOrDefault(cast.Source)) is { } caster)
        {
            var position = QuantizedDungeonPosition(caster.Position);
            shinryuIce.RemoveAll(a => a.Position.X == position.X && a.Position.Z == position.Z);
        }
    }

    private void ShinryuEffect(ulong source, uint action, ulong target, DateTime at, Vector3? sourcePosition)
    {
        if (!IsShinryu) return;
        if (action == 8096 && (sourcePosition ?? _actors.GetValueOrDefault(source)?.Position) is { } p)
            shinryuIce.Add((QuantizedDungeonPosition(p), at, ++shinryuKey));
        var icon = action switch { 8092 => 24u, 8090 => 62u, 8093 => 23u, _ => 0u };
        if (icon == 0) return;
        var index = icon == 23 ? shinryuIcons.FindIndex(b => b.Icon == icon)
            : shinryuIcons.FindIndex(b => b.Icon == icon && b.Target.Id == target);
        if (index < 0) index = shinryuIcons.FindIndex(b => b.Icon == icon);
        if (index >= 0) shinryuIcons.RemoveAt(index);
    }

    private void ShinryuIcon(ulong actorId, ulong targetId, uint icon, DateTime at)
    {
        if (!IsShinryu || Active == null || icon is not (23 or 24 or 62)) return;
        var actor = icon == 23 ? _actors.GetValueOrDefault(targetId) ?? _actors.GetValueOrDefault(actorId)
            : _actors.GetValueOrDefault(actorId);
        if (actor != null) shinryuIcons.Add((actor, icon, at.AddSeconds(icon == 23 ? 5.1 : 6), ++shinryuKey));
    }

    private void ShinryuObserve()
    {
        if (!IsShinryu || Active == null) return;
        foreach (var id in shinryuActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) ShinryuRemoved(id);
        foreach (var actor in _actors.Values) shinryuActors[actor.Id] = actor;
        for (var i = 0; i < shinryuIcons.Count; ++i)
        {
            var bait = shinryuIcons[i];
            if (_actors.TryGetValue(bait.Target.Id, out var actor)) shinryuIcons[i] = (actor, bait.Icon, bait.At, bait.Key);
        }
        shinryuIcons.RemoveAll(b => b.Icon != 62 && b.Target.Dead);
        if (shinryuTankTarget != null && _actors.TryGetValue(shinryuTankTarget.Id, out var tank)) shinryuTankTarget = tank;
        foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0))
        {
            var present = ShinryuRawStatus(actor, 667);
            if (shinryuFetterPending.TryGetValue(actor.Id, out var pending))
            {
                if (present != pending) continue;
                shinryuFetterPending.Remove(actor.Id);
            }
            if (shinryuFetterStates.TryGetValue(actor.Id, out var before) && before != present)
                ShinryuStatus(actor.Id, 667, present, snapshot: true);
            else
            {
                shinryuFetterStates[actor.Id] = present;
                if (present) shinryuFettered.Add(actor.Id);
            }
        }
        foreach (var actor in _actors.Values)
            if (shinryuSlidePending.TryGetValue(actor.Id, out var pending) && ShinryuRawStatus(actor, 911) == pending)
                shinryuSlidePending.Remove(actor.Id);
        ShinryuObserveTethers();
    }

    private static bool ShinryuRawStatus(DungeonActor actor, uint status)
        => actor.StatusExpirations?.ContainsKey(status) == true || actor.Statuses?.Contains(status) == true;

    private void ShinryuStatus(ulong target, uint status, bool present, bool snapshot = false)
    {
        if (!IsShinryu || status is not (667 or 911) || Arena == null) return;
        if ((_actors.GetValueOrDefault(target) ?? shinryuActors.GetValueOrDefault(target)) is not { PartySlot: >= 0 }) return;
        if (status == 911) { shinryuSlidePending[target] = present; return; }
        if (!snapshot) shinryuFetterPending[target] = present;
        if (shinryuFetterStates.TryGetValue(target, out var before) && before == present) return;
        shinryuFetterStates[target] = present;
        if (present) shinryuFettered.Add(target);
        else
        {
            shinryuFettered.Remove(target);
            Arena = new RectangleGeometry(Arena.Position, 0, 60, 60);
        }
    }

    private void ShinryuAnimation(ulong source, uint state, bool eventInput)
    {
        if (!IsShinryu || Active == null || Arena == null) return;
        if (!eventInput && shinryuAnimations.TryGetValue(source, out var previous) && previous == state) return;
        shinryuAnimations[source] = state;
        if (state == 0x00040008 && shinryuPlatformCenter == null) shinryuPlatformCenter = Arena.Position;
        if (state != 0x00400080 || !_actors.TryGetValue(source, out var actor) || actor.Oid != 0x1EA1A1
            || MathF.Abs(actor.Position.X) <= 1 && MathF.Abs(actor.Position.Z) <= 1) return;
        shinryuBroken.Add(new(MathF.Round(actor.Position.X), actor.Position.Y, MathF.Round(actor.Position.Z)));
        Arena = ShinryuArenas.Broken(shinryuPlatformCenter, shinryuBroken, Arena.Position.Y);
    }

    private void ShinryuRemoved(ulong source)
    {
        if (!IsShinryu) return;
        shinryuSlidePending.Remove(source);
        shinryuTetherPending.Remove(source);
        foreach (var pending in shinryuTetherPending.Where(p => p.Value == source).ToArray())
            shinryuTetherPending.Remove(pending.Key);
        shinryuTethers.Remove(source);
        foreach (var pair in shinryuTethers.Where(p => p.Value.Player == source || p.Value.Enemy == source).ToArray())
            shinryuTethers.Remove(pair.Key);
        if (_casts.TryGetValue(source, out var cast)) ShinryuFinish(cast);
        if (shinryuFettered.Contains(source)) ShinryuStatus(source, 667, false);
        shinryuAnimations.Remove(source);
    }

    private IEnumerable<DungeonDamagePrediction> ShinryuDamage()
    {
        if (!IsShinryu) yield break;
        if (shinryuTankCast is { } tank)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && ThornTank(a)))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, tank.FinishAt, "Tankbuster");
        if (_casts.Values.FirstOrDefault(c => c.Rule.Action == 9291) is { } meteor)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, meteor.FinishAt, "Raidwide");
        var spreads = shinryuIcons.Where(b => b.Icon == 24 && !b.Target.Dead).ToArray();
        if (spreads.Length > 0)
            foreach (var bait in spreads.Where(b => !b.Target.Dead && b.Target.PartySlot >= 0))
                yield return new(bait.Target.Id, bait.Target.PartySlot, bait.Target.PartyRole, bait.Target.Name, spreads.Min(b => b.At), "Raidwide");
        var breath = shinryuIcons.Where(b => b.Icon == 23).ToArray();
        if (breath.Length > 0 && _actors.GetValueOrDefault(PrimaryId) is { Dead: false })
            foreach (var bait in breath.Where(b => !b.Target.Dead && b.Target.PartySlot >= 0))
                yield return new(bait.Target.Id, bait.Target.PartySlot, bait.Target.PartyRole, bait.Target.Name, breath[0].At, "Raidwide");
    }

    private void ResetShinryu()
    {
        shinryuLightning = false; shinryuFire = false; shinryuWaterAt = default;
        shinryuIce.Clear(); shinryuIcons.Clear(); shinryuActors.Clear(); shinryuFettered.Clear();
        shinryuFetterStates.Clear(); shinryuFetterPending.Clear();
        shinryuSlidePending.Clear(); shinryuCastOrder.Clear();
        shinryuAnimations.Clear(); shinryuBroken.Clear(); shinryuPlatformCenter = null;
        shinryuTankCast = null; shinryuTankTarget = null; shinryuKey = 0;
        shinryuTethers.Clear(); shinryuTetherPending.Clear();
    }
}
