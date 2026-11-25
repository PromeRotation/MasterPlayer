using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTreasureYan => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.SILVER_SACK;
    private readonly Dictionary<ulong, DungeonActor> treasureSacks = [];
    private readonly List<ulong> treasureSackCreatedOrder = [];
    private readonly HashSet<ulong> treasureSackDestroyed = [];
    private readonly Dictionary<ulong, uint> treasureSackPendingOid = [];
    private readonly List<(long Order, Vector3 Position, DateTime Activation)> treasureSackTowers = [];
    private long treasureSackOrder;
    internal IReadOnlyList<(long Order, Vector3 Position, DateTime Activation)> TreasureSackTowers => treasureSackTowers;

    private static bool IsTreasureSack(uint oid) => oid is NativeTreasureCatalog.SILVER_SACK or NativeTreasureCatalog.GOLD_SACK;
    private bool IsTreasureYanPrimary(DungeonActor actor) => territory == NativeTreasureCatalog.VAULT_ONEIRON
        && actor.Oid == NativeTreasureCatalog.SILVER_SACK && !treasureSackDestroyed.Contains(actor.Id);
    private bool TreasureYanAlive => IsTreasureYan
        && _actors.Values.Any(a => IsTreasureSack(a.Oid) && !treasureSackDestroyed.Contains(a.Id));

    private void TreasureYanObserve()
    {
        if (!IsTreasureYan) return;
        foreach (var (id, old) in treasureSacks.ToArray())
        {
            // A Create can arrive before sampling has replaced the old object at this ID.
            if (treasureSackPendingOid.TryGetValue(id, out var expected))
            {
                if (!_actors.TryGetValue(id, out var sample) || sample.Oid != expected) continue;
                treasureSackPendingOid.Remove(id);
            }
            if (_actors.TryGetValue(id, out var current) && current.Oid == old.Oid)
                treasureSacks[id] = current;
            else
                TreasureYanRemoved(id);
        }
        foreach (var actor in _actors.Values.Where(a => IsTreasureSack(a.Oid)))
            TreasureYanCreated(actor);
    }

    private void TreasureYanCreated(DungeonActor actor)
    {
        if (!IsTreasureYan || treasureSackDestroyed.Contains(actor.Id) || !treasureSacks.TryAdd(actor.Id, actor)) return;
        treasureSackCreatedOrder.Add(actor.Id);
        // GenericTowers stores creation position without quantization or ActorID.
        treasureSackTowers.Add((++treasureSackOrder, actor.Position, _now.AddSeconds(10)));
    }

    private void TreasureYanCreateEvent(ulong id, uint oid, Vector3? position, float rotation)
    {
        if (territory != NativeTreasureCatalog.VAULT_ONEIRON || !IsTreasureSack(oid)) return;
        treasureSackDestroyed.Remove(id);
        if (position is { } p)
        {
            TreasureYanCreated(new DungeonActor(id, oid, p, Rotation: rotation));
            treasureSackPendingOid[id] = oid;
        }
    }

    private void TreasureYanRemoved(ulong id)
    {
        if (!IsTreasureYan || !treasureSacks.Remove(id, out var old)) return;
        treasureSackPendingOid.Remove(id);
        treasureSackCreatedOrder.Remove(id);
        treasureSackDestroyed.Add(id);
        var current = _actors.GetValueOrDefault(id);
        var position = current?.Oid == old.Oid ? current.Position : old.Position;
        var index = treasureSackTowers.FindIndex(t => t.Position.X == position.X && t.Position.Z == position.Z);
        if (index >= 0) treasureSackTowers.RemoveAt(index);
    }

    private DungeonActor? TreasureYanGoal(DungeonActor player)
    {
        DungeonActor? closest = null;
        var best = float.MaxValue;
        foreach (var oid in new[] { NativeTreasureCatalog.GOLD_SACK, NativeTreasureCatalog.SILVER_SACK })
            foreach (var sack in treasureSackCreatedOrder.Select(id => treasureSacks[id]).Where(a => a.Oid == oid))
            {
                var dx = player.Position.X - sack.Position.X;
                var dz = player.Position.Z - sack.Position.Z;
                var score = (dx * dx + dz * dz) * (oid == NativeTreasureCatalog.GOLD_SACK ? 1 : 3);
                if (score >= best || _casts.Values.Any(c => c.Rule.Action == 44269
                    && c.Rule.Shape!.Resolve(c.Location, c.Rotation, null).Contains(sack.Position))) continue;
                closest = sack;
                best = score;
            }
        return closest;
    }

    private int TreasureYanSoakers(Vector3 position) => treasureParty.Count(id =>
        _actors.GetValueOrDefault(id) is { Dead: false } actor && TreasureYanInside(actor.Position, position));

    private static bool TreasureYanInside(Vector3 actor, Vector3 tower)
    {
        var dx = actor.X - tower.X;
        var dz = actor.Z - tower.Z;
        return dx * dx + dz * dz <= 1;
    }

    private IEnumerable<DungeonAmbientAoe> TreasureYanAoes()
    {
        if (!IsTreasureYan || !_actors.TryGetValue(PlayerId, out var player)) yield break;
        foreach (var tower in treasureSackTowers)
        {
            var inside = TreasureYanInside(player.Position, tower.Position);
            var safe = TreasureYanSoakers(tower.Position) < 1 || inside && TreasureYanSoakers(tower.Position) <= 1;
            yield return new($"treasure/sack/tower/{tower.Order}", "Collect Sacks / 单人塔",
                new CircleTemplate(1), tower.Position, Navigate: false, OutlineOnly: safe || inside,
                Safe: safe, Activation: tower.Activation);
        }
        if (TreasureYanGoal(player) is { } goal)
            yield return new("treasure/sack/goal", "Collect Sacks", new CircleTemplate(.5f + goal.HitboxRadius),
                goal.Position, Display: false, Safe: true, GoalPriority: 10, OverrideDangerMargin: false);
    }

    private IEnumerable<string> TreasureYanHints()
    {
        if (!IsTreasureYan || !_actors.TryGetValue(PlayerId, out var player)) yield break;
        foreach (var tower in treasureSackTowers)
            if (TreasureYanInside(player.Position, tower.Position))
            {
                yield return TreasureYanSoakers(tower.Position) > 1 ? "Too many soakers in the tower!" : "Soak the tower!";
                yield break;
            }
        if (treasureSackTowers.Any(t => TreasureYanSoakers(t.Position) < 1)) yield return "Soak the tower!";
    }

    private void ResetTreasureYan()
    {
        treasureSacks.Clear();
        treasureSackPendingOid.Clear();
        treasureSackCreatedOrder.Clear();
        treasureSackTowers.Clear();
        treasureSackOrder = 0;
        // Destroy receipts survive a fight reset; an actual Create or absence clears them.
    }
}
