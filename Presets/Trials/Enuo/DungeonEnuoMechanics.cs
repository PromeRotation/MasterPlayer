using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsEnuo => territory == EnuoCatalog.TERRITORY;
    private readonly Dictionary<ulong, EnuoActorReference> enuoActors = [];
    private readonly List<DungeonPendingCast> enuoCones = [];
    private readonly List<(EnuoActorReference Source, EnuoActorReference Target, DateTime At, int Key)> enuoFreeze = [];
    private readonly List<(EnuoActorReference Target, DateTime At, int Key)> enuoStacks = [];
    private readonly List<(EnuoActorReference Target, DateTime At, int Key)> enuoSpreads = [];
    private readonly List<(EnuoActorReference Source, EnuoActorReference Target, DateTime At, int Key)> enuoLines = [];
    private readonly List<EnuoChaser> enuoChasers = [];
    private readonly List<DungeonPendingCast> enuoLosCasters = [];
    private readonly List<(DungeonLosGeometry Safe, DateTime At, int Key)> enuoLos = [];
    private Vector3? enuoLosOrigin;
    private int enuoTetherCount;
    private int enuoKey;

    private void EnuoObserve()
    {
        if (!IsEnuo || Active == null) return;
        foreach (var id in enuoActors.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) EnuoRemoved(id);
        foreach (var actor in _actors.Values)
            if (enuoActors.TryGetValue(actor.Id, out var reference)) reference.Actor = actor;
            else enuoActors[actor.Id] = new(actor);
        EnuoTick();
    }

    private void EnuoTick()
    {
        enuoChasers.RemoveAll(c => c.Remaining < 13 && (c.Target.Destroyed || c.Target.Actor.Dead));
        enuoFreeze.RemoveAll(b => b.Source.Destroyed || b.Source.Actor.Dead || b.Target.Actor.Dead);
    }

    private void EnuoCast(DungeonPendingCast cast)
    {
        if (!IsEnuo || Active == null || !enuoActors.TryGetValue(cast.Source, out var source)) return;
        if (cast.Rule.Action is 49952 or 49953)
        {
            enuoCones.Add(cast);
            if (enuoCones.Count >= 10) EnuoSortCones(0, enuoCones.Count - 1);
        }
        if (enuoActors.TryGetValue(cast.TargetId, out var target))
        {
            if (cast.Rule.Action == 49966) enuoFreeze.Add((source, target, cast.FinishAt, ++enuoKey));
            if (cast.Rule.Action == 49968) enuoStacks.Add((target, cast.FinishAt, ++enuoKey));
            if (cast.Rule.Action == 49964) enuoSpreads.Add((target, cast.FinishAt, ++enuoKey));
        }
        if (cast.Rule.Action == 49959)
        {
            enuoLosCasters.Add(cast);
            EnuoRefreshLos();
            var blockers = _actors.Values.Where(a => a.Oid == 0x4DBF).ToArray();
            if (enuoLosOrigin is { } origin && blockers.Length > 0)
                enuoLos.Add((new(origin, blockers, true, 40, false, 0), cast.FinishAt, ++enuoKey));
        }
    }

    private void EnuoFinish(DungeonPendingCast cast)
    {
        if (!IsEnuo) return;
        var cone = enuoCones.FindIndex(c => c.Source == cast.Source);
        if (cone >= 0) enuoCones.RemoveAt(cone);
        if (cast.Rule.Action == 49968)
        {
            var i = enuoStacks.FindIndex(s => s.Target.Actor.Id == cast.TargetId);
            if (i >= 0) enuoStacks.RemoveAt(i);
        }
        if (cast.Rule.Action == 49964)
        {
            var i = enuoSpreads.FindIndex(s => s.Target.Actor.Id == cast.TargetId);
            if (i >= 0) enuoSpreads.RemoveAt(i);
        }
        if (cast.Rule.Action == 49959)
        {
            var i = enuoLosCasters.FindIndex(c => c.Source == cast.Source);
            if (i >= 0) enuoLosCasters.RemoveAt(i);
            EnuoRefreshLos();
            if (enuoLos.Count > 0) enuoLos.RemoveAt(0);
        }
    }

    private void EnuoRefreshLos() =>
        enuoLosOrigin = enuoLosCasters.MinBy(c => c.FinishAt)?.Location;

    private void EnuoIcon(ulong actor, ulong target, uint icon)
    {
        if (!IsEnuo || Active == null) return;
        if (icon == 719 && enuoActors.TryGetValue(actor, out var source) && enuoActors.TryGetValue(target, out var victim))
            enuoLines.Add((source, victim, _now, ++enuoKey));
        // Icon172 only populates unused Targets/TargetsMask in the source; tether404 starts the actual chasers.
    }

    private void EnuoTether(ulong source, ulong target, uint tether, bool present)
    {
        if (!IsEnuo || Active == null || tether != 404 || !present || !enuoActors.TryGetValue(source, out var caster)) return;
        if (enuoTetherCount == 2) enuoChasers.Clear();
        if (!enuoActors.TryGetValue(target, out var victim)) return;
        enuoChasers.Add(new(victim, caster.Actor.Position, 0, 13, _now.AddSeconds(6), ++enuoKey));
        if (++enuoTetherCount == 4) enuoTetherCount = 0;
    }

    private void EnuoEffect(ulong source, uint action, ulong target, Vector3? location)
    {
        if (!IsEnuo || Active == null) return;
        if (action == 49966)
        {
            var i = enuoFreeze.FindIndex(b => b.Source.Actor.Id == source);
            if (i >= 0) enuoFreeze.RemoveAt(i);
        }
        if (action == 49969)
        {
            if (enuoLines.Count == 1 && enuoActors.TryGetValue(target, out var actual))
                enuoLines[0] = enuoLines[0] with { Target = actual };
            var i = enuoLines.FindIndex(b => b.Target.Actor.Id == target);
            if (i >= 0) enuoLines.RemoveAt(i);
        }
        if (action == 49940 && enuoChasers.Count > 0)
        {
            var position = target == source && enuoActors.TryGetValue(source, out var caster)
                ? QuantizedDungeonPosition(caster.Actor.Position)
                : enuoActors.GetValueOrDefault(target)?.Actor.Position ?? location ?? Vector3.Zero;
            var i = Enumerable.Range(0, enuoChasers.Count)
                .MinBy(n => Vector2.DistanceSquared(Xz(EnuoChasePosition(enuoChasers[n])), Xz(position)));
            var chase = enuoChasers[i];
            if (chase.Remaining == 1) enuoChasers.RemoveAt(i);
            else enuoChasers[i] = chase with
            {
                Previous = position, Distance = 2.9f, Remaining = chase.Remaining - 1, Activation = _now.AddSeconds(.7),
            };
        }
        static Vector2 Xz(Vector3 p) => new(p.X, p.Z);
    }

    private static Vector3 EnuoChasePosition(EnuoChaser chase)
    {
        var delta = chase.Target.Actor.Position - chase.Previous;
        delta.Y = 0;
        var length = delta.Length();
        return QuantizedDungeonPosition(length > chase.Distance
            ? chase.Previous + delta * (chase.Distance / length) : chase.Target.Actor.Position);
    }

    private void EnuoMap(uint index, uint state)
    {
        if (!IsEnuo || Active == null || Arena == null || index != 0) return;
        if (state == 0x00020001) Arena = new RectangleGeometry(Arena.Position, 0, 160, 160);
        if (state == 0x00080004) Arena = new CircleGeometry(Arena.Position, 20);
    }

    private void EnuoRemoved(ulong source)
    {
        if (!IsEnuo || !enuoActors.Remove(source, out var actor)) return;
        actor.Destroyed = true;
        if (_casts.TryGetValue(source, out var cast)) EnuoFinish(cast);
        if (actor.Actor.Oid == 0x4EB4) enuoChasers.Clear();
        EnuoTick();
    }

    // Match source RefSort's right-pivot partition, including ties at the ten-caster sorting threshold.
    private void EnuoSortCones(int left, int right)
    {
        while (left < right)
        {
            var store = left;
            for (var i = left; i < right; ++i)
                if (enuoCones[i].FinishAt < enuoCones[right].FinishAt)
                {
                    (enuoCones[store], enuoCones[i]) = (enuoCones[i], enuoCones[store]);
                    ++store;
                }
            (enuoCones[store], enuoCones[right]) = (enuoCones[right], enuoCones[store]);
            if (store - left < right - store) { EnuoSortCones(left, store - 1); left = store + 1; }
            else { EnuoSortCones(store + 1, right); right = store - 1; }
        }
    }

    private void ResetEnuo()
    {
        enuoActors.Clear(); enuoCones.Clear(); enuoFreeze.Clear(); enuoStacks.Clear(); enuoSpreads.Clear();
        enuoLines.Clear(); enuoChasers.Clear(); enuoLosCasters.Clear(); enuoLos.Clear(); enuoLosOrigin = null;
        enuoTetherCount = enuoKey = 0;
    }
}
