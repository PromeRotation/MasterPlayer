using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal List<BozjaMine> Mines { get; } = [];
    private readonly List<(Vector3 Position, int Charges)> bartizans = [];

    private void ClearPatriot()
    {
        Mines.Clear();
        bartizans.Clear();
    }

    private void PatriotEffect(ulong source, uint action)
    {
        if (!Active || !Definition.Rules.Any(r => r.Kind == BozjaRuleKind.LightningRod)
            || Actors.GetValueOrDefault(source) is not { NativeNpc: true } actor) return;
        if (action == 20448)
            Mines.Add(new(++serial, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position)));
        else if (action is 20449 or 20450)
        {
            var index = Mines.FindIndex(m => AlmostEqual(m.Position, actor.Actor.Position));
            if (index >= 0) Mines.RemoveAt(index);
        }
        else if (action == 20443)
            Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.ElectrochemicalReaction);
    }

    private void ObservePatriotTether(BozjaActor actor)
    {
        var tether = actor.Tether;
        var next = (tether?.Id ?? 0, tether?.TargetId ?? 0);
        var creating = actor.CreationTetherPending;
        actor.CreationTetherPending = false;
        if (actor.ObservedTether == next) return;
        if (actor.ObservedTether.Target != 0)
        {
            Cll1Tether(actor, actor.ObservedTether, true);
            CavalryUntethered(actor, actor.ObservedTether);
            SeekerTether(actor, actor.ObservedTether, true);
            OwainTether(actor, actor.ObservedTether, true);
            RaidenTether(actor, actor.ObservedTether, true);
            VirtueTether(actor, actor.ObservedTether, true);
        }
        actor.ObservedTether = next;
        if (next.Item2 is not (0 or 0xE0000000)) Cll1Tether(actor, next, false);
        if (next.Item2 is not (0 or 0xE0000000)) CavalryTethered(actor, next, creating);
        if (next.Item2 is not (0 or 0xE0000000)) SeekerTether(actor, next, false);
        if (next.Item2 is not (0 or 0xE0000000)) OwainTether(actor, next, false);
        if (next.Item2 is not (0 or 0xE0000000)) RaidenTether(actor, next, false);
        if (next.Item2 is not (0 or 0xE0000000)) VirtueTether(actor, next, false);
        if (Active && tether != null && next.Item2 is not (0 or 0xE0000000)) PatriotTethered(actor, tether.Id);
    }

    internal void Tether(ulong source, uint id, ulong target)
    {
        if (!Actors.TryGetValue(source, out var actor)) return;
        target = target == 0xE0000000 ? 0 : target;
        if (actor.ObservedTether.Target != 0)
        {
            Cll1Tether(actor, actor.ObservedTether, true);
            CavalryUntethered(actor, actor.ObservedTether);
            SeekerTether(actor, actor.ObservedTether, true);
            OwainTether(actor, actor.ObservedTether, true);
            RaidenTether(actor, actor.ObservedTether, true);
            VirtueTether(actor, actor.ObservedTether, true);
        }
        actor.ObservedTether = (id, target);
        // A real OpTether dispatches even when the value is unchanged. Only
        // frame snapshots are deduplicated; a zero target is Untethered only.
        if (target != 0) Cll1Tether(actor, (id, target), false);
        if (target != 0) PatriotTethered(actor, id);
        if (target != 0) CavalryTethered(actor, (id, target));
        if (target != 0) SeekerTether(actor, (id, target), false);
        if (target != 0) OwainTether(actor, (id, target), false);
        if (target != 0) RaidenTether(actor, (id, target), false);
        if (target != 0) VirtueTether(actor, (id, target), false);
    }

    internal void PatriotTethered(BozjaActor actor, uint id)
    {
        GauntletTethered(actor, id);
        var rule = Definition.Rules.FirstOrDefault(r => r.Kind == BozjaRuleKind.ElectrochemicalReaction);
        if (!Active || rule == null || id is not (114 or 115)) return;
        var position = actor.Actor.Position;
        if (bartizans.Count != 4 && !bartizans.Any(b => AlmostEqual(b.Position, position)))
            bartizans.Add((DungeonEncounter.QuantizedDungeonPosition(position), 0));
        for (var i = 0; i < bartizans.Count; ++i)
        {
            var laser = bartizans[i];
            if (!AlmostEqual(laser.Position, position)) continue;
            var charges = laser.Charges + 1;
            if (charges == 2)
            {
                Casts.Add(new(++serial, rule, actor, laser.Position, actor.Actor.Rotation, Now.AddSeconds(10.2d)));
                charges = 0;
            }
            bartizans[i] = (laser.Position, charges);
            return;
        }
    }

    private static bool AlmostEqual(Vector3 a, Vector3 b)
        => MathF.Abs(a.X - b.X) <= 1 && MathF.Abs(a.Z - b.Z) <= 1;

    private void UpdateMines()
    {
        if (Mines.Count == 0) return;
        ulong forbidden = 0;
        foreach (var member in Party.Where(p => p.Slot is >= 0 and < 8))
            if (Actors.GetValueOrDefault(member.EntityId) is { } actor
                && (byte)actor.Actor.ClassJob is not (1 or 3 or 19 or 21 or 32 or 37) && actor.Actor.Hp < 40000)
                forbidden |= 1UL << member.Slot;
        foreach (var mine in Mines) mine.ForbiddenSoakers = forbidden;
    }

    private int ViewerSlot(BozjaActor viewer) => Party.FirstOrDefault(p => p.EntityId == viewer.Actor.Id)?.Slot ?? -1;
    private static bool MineForbidden(BozjaMine mine, int slot) => slot is >= 0 and < 64 && (mine.ForbiddenSoakers & (1UL << slot)) != 0;
    private float MineRadius(BozjaMine mine, BozjaActor viewer) => MineForbidden(mine, ViewerSlot(viewer)) ? 8 : 4;
    private static bool MineInside(BozjaMine mine, BozjaActor actor, float radius)
        => DistanceSquared(mine.Position, actor.Actor.Position) <= radius * radius;

    private int MineCount(BozjaMine mine, float radius)
        => Party.Count(p => p.Slot is >= 0 and < 64 && !MineForbidden(mine, p.Slot)
            && Actors.GetValueOrDefault(p.EntityId) is { Actor.Dead: false } actor && MineInside(mine, actor, radius));

    internal IEnumerable<BozjaMineView> MineViews(BozjaActor viewer)
    {
        if (!Active) yield break;
        foreach (var mine in Mines)
        {
            var forbidden = MineForbidden(mine, ViewerSlot(viewer));
            var radius = MineRadius(mine, viewer);
            var inside = MineInside(mine, viewer, radius);
            var count = MineCount(mine, radius);
            var safe = count < 1 || inside && count <= 1;
            yield return new(mine, radius, forbidden || !inside && count >= 1,
                !forbidden && (safe || inside && count > 1), safe);
        }
    }

    private IEnumerable<BozjaHint> MineHints(BozjaActor viewer)
    {
        var slot = ViewerSlot(viewer);
        if (Mines.Any(m => MineForbidden(m, slot) && MineInside(m, viewer, MineRadius(m, viewer))))
            yield return new("GTFO from tower!");
        else if (Mines.FirstOrDefault(m => !MineForbidden(m, slot) && MineInside(m, viewer, MineRadius(m, viewer))) is { } own)
        {
            var count = MineCount(own, MineRadius(own, viewer));
            yield return count == 1 ? new("Soak the tower!", false)
                : new(count > 1 ? "Too many soakers in the tower!" : "Too few soakers in the tower!");
        }
        else if (Mines.Any(m => !MineForbidden(m, slot) && MineCount(m, MineRadius(m, viewer)) < 1))
            yield return new("Soak the tower!");
    }

    private IEnumerable<BozjaZone> MineNavigation(BozjaActor viewer)
    {
        if (Mines.Count == 0) yield break;
        var slot = ViewerSlot(viewer);
        var inTower = Mines.Any(m => MineInside(m, viewer, MineRadius(m, viewer)) && MineCount(m, MineRadius(m, viewer)) == 1);
        var join = new List<Vector3>();
        var avoid = new List<BozjaMine>();
        foreach (var mine in Mines)
        {
            var radius = MineRadius(mine, viewer);
            var inside = MineInside(mine, viewer, radius);
            var count = MineCount(mine, radius);
            if (!MineForbidden(mine, slot) && (count < 1 || inside && count == 1)) join.Add(mine.Position);
            else if (MineForbidden(mine, slot) || count > 1 || !inside && count == 1) avoid.Add(mine);
        }
        if (join.Count > 0)
            yield return new($"mine/{Mines[0].Generation}/join", "LightningRod", default, null, default, 0, join.ToArray(), 4);
        else if (!inTower)
            foreach (var mine in avoid)
                yield return new($"mine/{mine.Generation}/avoid", "LightningRod", default, new CircleTemplate(MineRadius(mine, viewer)), mine.Position, 0);
    }

    private BozjaDamage MineDamage(BozjaActor viewer)
    {
        ulong mask = 0;
        foreach (var member in Party.Where(p => p.Slot is >= 0 and < 64))
            if (Actors.GetValueOrDefault(member.EntityId) is { Actor.Dead: false } actor
                && Mines.Any(m => !MineForbidden(m, member.Slot) && MineInside(m, actor, MineRadius(m, viewer))))
                mask |= 1UL << member.Slot;
        return new(mask, default, "Raidwide");
    }
}
