using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(DungeonActor Target, bool Removed, bool IsSource, DateTime? Activation)> treasureBaits = [];
    private DungeonActor? treasureBaitPrimary;
    private bool treasureBaitPrimaryRemoved;
    private int treasureBaitStarts;
    private bool HasTreasureBaits => IsNativeTreasure && Active?.BossOid is NativeTreasureCatalog.ALTAR_CHIMERA
        or NativeTreasureCatalog.ALTAR_DIRESAUR or NativeTreasureCatalog.ALTAR_TOTEM or NativeTreasureCatalog.SECRET_WORM or NativeTreasureCatalog.GYMNASIOU_PITHEKOS;
    private bool IsTreasureWorm => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.SECRET_WORM;
    private bool IsTreasurePithekos => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.GYMNASIOU_PITHEKOS;
    private uint TreasureVoidzoneAction => IsNativeTreasure && Active != null ? NativeTreasureCatalog.VoidzoneAction(Active.BossOid) : 0;
    private float TreasureBaitRadius => Active?.BossOid switch
    {
        NativeTreasureCatalog.GYMNASIOU_PITHEKOS => 18,
        NativeTreasureCatalog.SECRET_WORM or NativeTreasureCatalog.ALTAR_TOTEM => 10,
        _ => 6,
    };

    private void TreasureBaitObserve()
    {
        if (!HasTreasureBaits) return;
        if (treasureBaitPrimary == null)
            treasureBaitPrimary = _actors.GetValueOrDefault(PrimaryId);
        else if (!treasureBaitPrimaryRemoved)
        {
            if (_actors.TryGetValue(treasureBaitPrimary.Id, out var source) && source.Oid == treasureBaitPrimary.Oid)
                treasureBaitPrimary = source;
            else
                treasureBaitPrimaryRemoved = true;
        }
        for (var i = 0; i < treasureBaits.Count; ++i)
        {
            var bait = treasureBaits[i];
            if (!bait.Removed && _actors.TryGetValue(bait.Target.Id, out var actor) && actor.Oid == bait.Target.Oid)
                treasureBaits[i] = (actor, false, bait.IsSource, bait.Activation);
            else
                treasureBaits[i] = (bait.Target, true, bait.IsSource, bait.Activation);
        }
    }

    private void TreasureBaitRemoved(ulong id)
    {
        if (treasureBaitPrimary?.Id == id) treasureBaitPrimaryRemoved = true;
        for (var i = 0; i < treasureBaits.Count; ++i)
            if (treasureBaits[i].Target.Id == id)
                treasureBaits[i] = (treasureBaits[i].Target, true, treasureBaits[i].IsSource, treasureBaits[i].Activation);
    }

    private void TreasureBaitIcon(ulong actorId, uint icon)
    {
        if (!HasTreasureBaits || icon != (IsTreasurePithekos ? 111 : 23) || treasureBaitPrimary == null || !_actors.TryGetValue(actorId, out var target))
            return;
        // Icon belongs to its source Actor; the packet target is a different field.
        DateTime? activation = null;
        if (IsTreasurePithekos) activation = _now.AddSeconds(6);
        else if (IsTreasureWorm) activation = _now.AddSeconds(5);
        treasureBaits.Add((target, false, !treasureBaitPrimaryRemoved && target.Id == treasureBaitPrimary.Id,
            activation));
    }

    private void TreasureBaitCast(DungeonPendingCast cast, bool correction)
    {
        var clearAction = TreasureVoidzoneAction;
        if (IsTreasurePithekos) clearAction = 32212;
        else if (IsTreasureWorm) clearAction = 21713;
        if (!HasTreasureBaits || correction || cast.Rule.Action != clearAction) return;
        // CastStarted, including cancelled casts and starts before any icon, advances this counter.
        if (IsTreasureWorm || IsTreasurePithekos || Active!.BossOid == NativeTreasureCatalog.ALTAR_CHIMERA || ++treasureBaitStarts == 3)
        {
            treasureBaits.Clear();
            treasureBaitStarts = 0;
        }
    }

    private IEnumerable<DungeonAmbientAoe> TreasureBaitAoes()
    {
        if (!HasTreasureBaits || Arena == null) yield break;
        var shape = new CircleTemplate(TreasureBaitRadius);
        if (treasureBaitPrimary is { Dead: false })
            for (var i = 0; i < treasureBaits.Count; ++i)
            {
                var bait = treasureBaits[i];
                var position = bait.Target.Position;
                yield return new($"treasure/bait/{i}/display", "诱导落圈", shape, position,
                    Navigate: false, OutlineOnly: bait.Target.Id == PlayerId, Activation: bait.Activation);
                if (bait.Removed || bait.Target.Id != PlayerId)
                    yield return new($"treasure/bait/{i}/avoid", "避让诱导落圈", shape, position,
                        Display: false, Activation: bait.Activation);
                else if (!bait.IsSource)
                    foreach (var id in treasureParty)
                        if (id != PlayerId && _actors.GetValueOrDefault(id) is { Dead: false } member)
                            yield return new($"treasure/bait/{i}/party/{id}", "诱导远离队友", shape,
                                member.Position, Display: false, Activation: bait.Activation);
            }
        // The derived source component reads CurrentBaits, even when Source.IsDead.
        if (treasureBaits.Count > 0 && !treasureBaits[0].Removed && treasureBaits[0].Target.Id == PlayerId)
            yield return new("treasure/bait/edge", "首目标去场边诱导", new CircleTemplate(17.5f), Arena.Position,
                Display: false, Activation: treasureBaits[0].Activation);
    }

    private IEnumerable<string> TreasureBaitHints(Vector3 player)
    {
        if (!HasTreasureBaits || treasureBaits.Count == 0) yield break;
        if (!treasureBaits[0].Removed && treasureBaits[0].Target.Id == PlayerId)
        {
            if (IsTreasurePithekos) yield return "Bait levinorb away!";
            else if (IsTreasureWorm) yield return "Bait bubble away!";
            else yield return Active!.BossOid == NativeTreasureCatalog.ALTAR_CHIMERA ? "Bait away!" : "Bait away! (3 times)";
            yield break;
        }
        if (treasureBaitPrimary is not { Dead: false }) yield break;
        var radiusSquared = TreasureBaitRadius * TreasureBaitRadius;
        static float DistanceSquared(Vector3 a, Vector3 b) => Vector2.DistanceSquared(new(a.X, a.Z), new(b.X, b.Z));
        if (treasureBaits.Any(b => b.Target.Id == PlayerId && treasureParty.Any(id => id != b.Target.Id
            && _actors.GetValueOrDefault(id) is { Dead: false } member && DistanceSquared(member.Position, b.Target.Position) <= radiusSquared)))
            yield return "Bait away from raid!";
        if (treasureBaits.Any(b => b.Target.Id != PlayerId && DistanceSquared(player, b.Target.Position) <= radiusSquared))
            yield return "GTFO from baited aoe!";
    }

    internal IEnumerable<ulong> TreasureInterruptTargets => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.ALTAR_CHIMERA
        ? _casts.Values.Where(c => c.Rule.Action is 13394 or 13395).Select(c => c.Source) : [];
}
