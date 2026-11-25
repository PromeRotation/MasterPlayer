using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsNativeTreasure => NativeTreasureCatalog.IsTerritory(territory);
    private bool IsTreasureRoom => IsNativeTreasure && Active != null && NativeCenoteRoomCatalog.IsRoom(Active.BossOid);
    private bool IsTreasureRoomPrimary(DungeonActor actor) => territory == NativeTreasureCatalog.CENOTE_JA_JA_GURAL
        && NativeCenoteRoomCatalog.IsRoom(actor.Oid);
    private bool TreasureRoomCanStart(DungeonActor actor) => actor.EventState != 7
        && (!treasureCompletedRooms.TryGetValue(actor.Id, out var completed) || completed.Oid != actor.Oid)
        && treasureRoomInstances.GetValueOrDefault(actor.Id).Pulled;
    private bool IsTreasureTigris => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.GYMNASIOU_TIGRIS;
    private bool HasTreasureGaze => IsNativeTreasure && Active?.Casts.Any(c => NativeTreasureCatalog.GazeAction(c.Action)) == true;
    private bool IsTreasureTriton => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.GYMNASIOU_TRITON;
    private bool IsTreasureSphinx => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.GYMNASIOU_SPHINX;
    private bool HasTreasureSpreadTargets => IsTreasureSphinx || IsNativeTreasure
        && Active?.BossOid is NativeTreasureCatalog.LUCKY_FACE or NativeTreasureCatalog.GYMNASIOU_MEGANEREIS or NativeTreasureCatalog.GREAT_GIMME_CAT or NativeTreasureCatalog.DAEN_ULTROS or NativeTreasureCatalog.SECRET_CLADOSELACHE or NativeTreasureCatalog.HIPPOMENES or NativeTreasureCatalog.NARKISSOS or NativeTreasureCatalog.GOLDEN_MOLTER or NativeTreasureCatalog.PAEONIA or NativeTreasureCatalog.SPIRIT_OF_THUNDER or NativeTreasureCatalog.MOBILE_FORTRESS;
    private bool IsTreasureMegakantha => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.GYMNASIOU_MEGAKANTHA;
    private bool IsTreasureFuath => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.FUATH_TROUBLEMAKER;
    private bool HasTreasureEightStack => IsTreasureFuath || IsNativeTreasure && Active?.BossOid is NativeTreasureCatalog.GOLIATH or NativeTreasureCatalog.ALTAR_DULLAHAN or NativeTreasureCatalog.LYSSA_CHRYSINE or NativeTreasureCatalog.DAEN_ECHIDNA or NativeTreasureCatalog.DAEN_TYPHON;
    private bool HasTreasureCachedBoss => IsNativeTreasure && Active != null && NativeTreasureCatalog.CachedBossOid(Active.BossOid) != 0;
    private DungeonActor? treasureCachedBoss;
    private bool treasureCachedBossRemoved;
    private DungeonPendingCast? treasureOdious;
    private int treasureOdiousHits;
    private bool UsesObservedCastEnd => IsNativeTrial || IsNativeQuestBoss || IsCarnivale || IsNativeTreasure || IsHeavenOnHigh || IsEurekaOrthos;
    private bool TreasureGroupAlive => IsNativeTreasure && Active != null
        && (IsTreasureYan ? TreasureYanAlive
            : IsTreasureRoom ? _actors.GetValueOrDefault(PrimaryId) is { EventState: not 7 } room && room.Oid == Active.BossOid
            : _actors.Values.Any(a => (HasTreasureCachedBoss || !a.Dead) && NativeTreasureCatalog.EncounterOids(Active.BossOid).Contains(a.Oid))
                || HasTreasureCachedBoss && treasureCachedBoss is { Dead: false } && !treasureCachedBossRemoved);
    private readonly List<TreasureDelayedHit> treasureDelayedHits = [];
    private readonly HashSet<(ulong Source, uint Action, uint Sequence)> treasureEffects = [];
    private readonly Dictionary<ulong, TreasureDelayedHit> treasureCurrentCasts = [];
    private readonly Dictionary<ulong, DungeonCastObservation> treasureObservedCasts = [];
    private readonly List<ulong> treasureLightOrder = [];
    private ulong[] treasureParty = [];
    internal IReadOnlyList<ulong> TreasurePartyInput { get; set; } = [];

    private void TreasureCachedBossObserve(bool allowInitialCache)
    {
        if (!HasTreasureCachedBoss) return;
        var oid = NativeTreasureCatalog.CachedBossOid(Active!.BossOid);
        if (treasureCachedBoss == null)
        {
            if (allowInitialCache) treasureCachedBoss = _actors.Values.FirstOrDefault(a => a.Oid == oid);
        }
        else if (!treasureCachedBossRemoved)
        {
            if (_actors.TryGetValue(treasureCachedBoss.Id, out var current) && current.Oid == oid)
                treasureCachedBoss = current;
            else
                treasureCachedBossRemoved = true;
        }
    }

    private static bool NativeCastChanged(DungeonCastObservation previous, DungeonCastObservation current)
        => previous.Action != current.Action || previous.TargetId != current.TargetId
            || previous.Location != current.Location || previous.Rotation != current.Rotation
            || previous.TotalTime != current.TotalTime;

    private void TreasureCastStarted(DungeonPendingCast cast, bool correction, bool initialRaw)
    {
        TreasureBaitCast(cast, correction);
        TreasureSequenceStart(cast, correction);
        TreasureRotationStart(cast, correction);
        TreasureGymnasionStart(cast, correction);
        TreasureNarkissosStart(cast, correction);
        TreasureMolterStart(cast, correction);
        TreasureBitterStart(cast, correction);
        TreasureFortressStart(cast, correction);
        TreasureHexStart(cast, correction);
        TreasureIceStart(cast, correction);
        TreasureCastBaitStart(cast, correction, initialRaw);
        TreasureVaultGroupStart(cast, correction);
        TreasurePlayerGazeStart(cast, correction);
        TreasureWallKnockbackStart(cast, correction);
        TreasureBuffetStart(cast, correction, initialRaw);
        if (IsTreasureMegakantha && cast.Rule.Action == 32242
            && (!correction || treasureOdious is { } currentOdious
                && currentOdious.Source == cast.Source && currentOdious.StartedAt == cast.StartedAt))
            treasureOdious = cast;
        if (IsNativeTreasure && cast.Rule.Action is 32294 or 38271 && !treasureLightOrder.Contains(cast.Source))
            treasureLightOrder.Add(cast.Source);
        if (!IsNativeTreasure || !NativeTreasureCatalog.DelayableAction(cast.Rule.Action)) return;
        // A correction belongs to the accepted live cast, even after its queued hit was consumed.
        // Finish removes this receipt without removing the delayed prediction.
        var index = -1;
        if (correction)
        {
            if (!treasureCurrentCasts.TryGetValue(cast.Source, out var current))
            {
                if (!initialRaw) return;
            }
            else
            {
                index = treasureDelayedHits.IndexOf(current);
                if (index < 0) return;
            }
        }
        var targetId = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId;
        if (!_actors.ContainsKey(targetId)) return;
        var slot = Array.IndexOf(treasureParty, targetId);
        if (index >= 0 && treasureDelayedHits[index].TargetId == targetId)
            slot = treasureDelayedHits[index].CapturedSlot;
        var hit = new TreasureDelayedHit(cast.Source, cast.Rule.Action, cast.StartedAt, targetId, slot, cast.FinishAt);
        if (index >= 0) treasureDelayedHits[index] = hit;
        else treasureDelayedHits.Add(hit);
        treasureCurrentCasts[cast.Source] = hit;
    }

    internal void TreasureEffect(ulong source, uint action, uint sequence, ulong mainTarget, DateTime? now = null, Vector3? location = null)
    {
        if (!IsNativeTreasure || Active == null || !(NativeTreasureCatalog.DelayableAction(action) || action == TreasureVoidzoneAction || NativeTreasureCatalog.OdiousEffect(action) || action is 21672 or 21730 or 21706 or 21707 or 21781 or 21691 or 32345 or 33165 or 32346 or 32348 or 32349 or 32347 || action == 43771 || NativeTreasureCatalog.CastBaitAction(action))
            || !treasureEffects.Add((source, action, sequence)))
            return;
        if (IsTreasureHexapod && action == 43771)
        {
            var mineIndex = treasureHexMines.FindIndex(m => m.Source == source);
            if (mineIndex >= 0) treasureHexMines.RemoveAt(mineIndex);
            return;
        }
        if (action is 32345 or 33165 or 32346 or 32348 or 32349 or 32347)
        {
            TreasureBlasterEffect(action);
            return;
        }
        if (action is 21706 or 21707 or 21781 or 21691)
        {
            TreasureRotationEffect(source, action, now ?? _now);
            return;
        }
        if (NativeTreasureCatalog.CastBaitAction(action))
        {
            TreasureCastBaitEffect(source, action);
            return;
        }
        if (action is 21672 or 21730)
        {
            TreasureSequenceEffect(source, action, now ?? _now);
            return;
        }
        if (NativeTreasureCatalog.OdiousEffect(action))
        {
            // This is a component-wide counter: a new Start replaces the AOE without resetting it.
            if (IsTreasureMegakantha && ++treasureOdiousHits == 5)
            {
                treasureOdious = null;
                treasureOdiousHits = 0;
            }
            return;
        }
        if (action == TreasureVoidzoneAction)
        {
            TrialVoidzoneEffect(action, mainTarget, location, now ?? _now);
            return;
        }
        var index = treasureDelayedHits.FindIndex(h => h.Action == action && h.TargetId == mainTarget);
        if (index >= 0) treasureDelayedHits.RemoveAt(index);
    }

    private IEnumerable<DungeonDamagePrediction> TreasureDamage()
    {
        if (!IsNativeTreasure || Active == null) yield break;
        foreach (var prediction in TreasureEarthquakeDamage()) yield return prediction;
        foreach (var prediction in TreasureCastBaitDamage()) yield return prediction;
        foreach (var hit in treasureDelayedHits)
            if (hit.CapturedSlot >= 0 && hit.CapturedSlot < treasureParty.Length
                && _actors.GetValueOrDefault(treasureParty[hit.CapturedSlot]) is { } target)
                yield return new(target.Id, hit.CapturedSlot, target.PartyRole, target.Name, hit.Activation, "Tankbuster");
        // SingleTargetCast reads the current cast target, unlike the captured delayable lists.
        foreach (var cast in _casts.Values.Where(c => NativeTreasureCatalog.SingleTargetAction(c.Rule.Action)))
        {
            var source = _actors.GetValueOrDefault(cast.Source);
            var current = source?.Cast is { } observed && observed.Action == cast.Rule.Action ? observed : null;
            var targetId = current?.TargetId ?? cast.TargetId;
            if (targetId == cast.Source) targetId = source?.TargetId ?? 0;
            var slot = Array.IndexOf(treasureParty, targetId);
            if (slot >= 0 && _actors.GetValueOrDefault(targetId) is { } target)
                yield return new(target.Id, slot, target.PartyRole, target.Name, current?.FinishAt ?? cast.FinishAt, "Tankbuster");
        }
        foreach (var cast in _casts.Values.Where(c => NativeTreasureCatalog.RaidwideAction(c.Rule.Action)))
        {
            var current = _actors.GetValueOrDefault(cast.Source)?.Cast;
            var activation = current?.Action == cast.Rule.Action ? current.FinishAt : cast.FinishAt;
            for (var slot = 0; slot < treasureParty.Length; ++slot)
                if (_actors.GetValueOrDefault(treasureParty[slot]) is { Dead: false } target)
                    yield return new(target.Id, slot, target.PartyRole, target.Name, activation, "Raidwide");
        }
    }

    private IEnumerable<DungeonAmbientAoe> TreasureObstacles()
    {
        if (!IsNativeTreasure) yield break;
        if (Active?.BossOid == NativeTreasureCatalog.GYMNASIOU_SATYROS)
            // The source enumerates every StormsGrip, including dead/untargetable/EventState 7 actors.
            foreach (var actor in _actors.Values.Where(a => a.Oid == 0x3D2F))
                yield return new($"treasure/wind/{actor.Id}", "Wind Cutter", new CircleTemplate(4), actor.Position, Obstacle: true);
        if (Active?.BossOid == NativeTreasureCatalog.SECRET_SERPENT)
            foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1EA7D5 && a.EventState != 7))
                yield return new($"treasure/water/{actor.Id}", "Douse", new CircleTemplate(7.5f), actor.Position, Obstacle: true);
        if (Active?.BossOid == NativeTreasureCatalog.GOLIATH)
            foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1E9998 && a.EventState != 7))
                yield return new($"treasure/water/{actor.Id}", "Wellbore", new CircleTemplate(8), actor.Position, Obstacle: true);
    }

    private IEnumerable<DungeonAmbientAoe> TreasureOdiousAoes()
    {
        if (IsTreasureMegakantha && treasureOdious is { } cast)
            yield return new("treasure/odious", cast.Rule.Name, new FanTemplate(40, MathF.PI), cast.Location,
                cast.Rotation, Activation: cast.FinishAt);
    }

    private IEnumerable<(DungeonActor Source, DungeonActor Target, float Angle)> TreasureCleaveOrigins()
    {
        if (!IsNativeTreasure || Active == null) yield break;
        var sourceOid = NativeTreasureCatalog.CleaveSourceOid(Active.BossOid);
        if (sourceOid == 0) yield break;
        // Cleave remains active while casting and enumerates every live targetable source.
        foreach (var source in _actors.Values.Where(a => a.Oid == sourceOid && !a.Dead && a.Targetable))
            if (_actors.GetValueOrDefault(source.TargetId) is { } target)
                yield return (source, target, MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z));
    }

    private IEnumerable<DungeonAmbientAoe> TreasureCleaves()
    {
        foreach (var (source, target, angle) in TreasureCleaveOrigins())
        {
            var origin = QuantizedDungeonPosition(source.Position);
            var keeper = Active?.BossOid == NativeTreasureCatalog.SECRET_KEEPER;
            var name = keeper ? "Moldy Sneeze" : "Spin";
            var shape = new FanTemplate(keeper ? 12 : 9.42f, 2 * MathF.PI / 3);
            yield return new($"treasure/cleave/{source.Id}/display", name, shape, origin, angle,
                Navigate: false, OutlineOnly: true);
            if (PlayerId != target.Id)
                yield return new($"treasure/cleave/{source.Id}/avoid", name, shape, origin, angle, Display: false);
            else
                foreach (var id in treasureParty)
                    if (id != PlayerId && _actors.GetValueOrDefault(id) is { Dead: false } member)
                    {
                        var direction = MathF.Atan2(member.Position.X - source.Position.X, member.Position.Z - source.Position.Z);
                        yield return new($"treasure/cleave/{source.Id}/party/{id}", $"{name} / 避免面向队友",
                            new FanTemplate(100, 2 * MathF.PI / 3), origin, direction, Display: false);
                    }
        }
    }

    private bool TreasureCleaveDanger(Vector3 player) => TreasureCleaveOrigins().Any(e => e.Target.Id != PlayerId
        && new FanTemplate(Active?.BossOid == NativeTreasureCatalog.SECRET_KEEPER ? 12 : 9.42f, 2 * MathF.PI / 3).Resolve(e.Source.Position, e.Angle, null).Contains(QuantizedDungeonPosition(player)));

    private IEnumerable<DungeonAmbientAoe> TreasureLights()
    {
        if (!IsNativeTreasure || Active?.BossOid is not (NativeTreasureCatalog.LAMPAS_CHRYSINE or NativeTreasureCatalog.GOLDEN_MOLTER)) yield break;
        // SimpleAOEs limits by insertion order, independently of predicted finish time.
        for (var i = 0; i < Math.Min(4, treasureLightOrder.Count); ++i)
        {
            var source = treasureLightOrder[i];
            if (!_casts.TryGetValue(source, out var cast) || cast.Rule.Action is not (32294 or 38271)) continue;
            Vector4? color = null;
            if (treasureLightOrder.Count > 2 && i < 2)
                color = new Vector4(1, 1, 0, 1);
            yield return new($"treasure/light/{source}", cast.Rule.Name, cast.Rule.Shape!, cast.Location,
                cast.Rotation, Activation: cast.FinishAt, Color: color);
        }
    }

    private ulong TreasurePriorityTarget(ulong current, Vector3 position)
    {
        // Yan has no priority setter. Its old MP bridge did not consume the base
        // enemy priorities as PR targets, so collecting sacks must not acquire one.
        if (IsTreasureYan) return 0;
        // Every candidate receives the source module's explicit setter, including priority zero.
        // Only Pointless resists that setter; the source checks height/FATE before PendingDead.
        bool Pointless(DungeonActor actor) => MathF.Abs(actor.Position.Y - position.Y) <= 12
            && !(actor.FateId is > 0 && actor.FateId != AllowedFateId)
            && actor.NameId != 541 && actor.Hp is { } hp
            && (long)hp + (actor.IncomingEffects?.Sum(e => (long)e.HpDelta) ?? 0) <= 1;

        return _actors.Values.Where(a => Active != null && a.IsEnemy == true && a.Targetable && !a.Dead
                && a.ObjectIndex is { } index && index < 200 && (index & 1) == 0 && !Pointless(a))
            .OrderByDescending(a => NativeTreasureCatalog.Priority(Active!.BossOid, a.Oid))
            .ThenByDescending(a => a.Id == current)
            .ThenBy(a => NativeEnemyDistanceSquared(a.Position, position))
            .Select(a => a.Id).FirstOrDefault();
    }

    private IEnumerable<DungeonAmbientAoe> TreasureEnemies()
    {
        if (!IsNativeTreasure || Active == null || Arena == null) yield break;
        var shape = new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]);
        foreach (var actor in _actors.Values)
        {
            if (IsTreasureYan && (actor.Oid != NativeTreasureCatalog.GOLD_SACK || treasureSackDestroyed.Contains(actor.Id))) continue;
            if (IsTreasureRoom && actor.Id == PrimaryId) continue;
            if (!IsTreasureYan && (actor.Id != PrimaryId && (actor.Oid == Active.BossOid && !IsTreasureMegakantha && !HasTreasureCachedBoss && Active.BossOid != NativeTreasureCatalog.AIRAVATA
                    || !NativeTreasureCatalog.EncounterOids(Active.BossOid).Contains(actor.Oid)
                        && !(HasTreasureCachedBoss && !treasureCachedBossRemoved && actor.Id == treasureCachedBoss?.Id))
                || (actor.Dead || !actor.Targetable) && !(IsTreasureMolter && actor.Id == PrimaryId)))
                continue;
            var outside = !Arena.Contains(actor.Position);
            var position = actor.Position;
            if (outside && territory == NativeTreasureCatalog.VAULT_ONEIRON)
                position = new(Math.Clamp(position.X, 80.5f, 119.5f), position.Y, Math.Clamp(position.Z, 80.5f, 119.5f));
            else if (outside && MathF.Abs(position.X - Arena.Position.X) >= .1f)
            {
                var point = new Vector2(position.X, position.Z);
                var nearest = Arena.GetContours().SelectMany(c => Enumerable.Range(0, c.Count).Select(i =>
                {
                    var a = new Vector2(c[i].X, c[i].Z);
                    var b = new Vector2(c[(i + 1) % c.Count].X, c[(i + 1) % c.Count].Z);
                    var edge = b - a;
                    var t = edge.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0, 1) : 0;
                    return a + edge * t;
                })).MinBy(p => Vector2.DistanceSquared(p, point));
                position = new(nearest.X, position.Y, nearest.Y);
            }
            yield return new($"treasure/enemy/{actor.Id}", "敌人及奖励怪", shape, position, actor.Rotation,
                Navigate: false, OutlineOnly: outside, Unclipped: true,
                Color: IsTreasureYan ? new Vector4(1, 1, 0, 1) : NativeTreasureCatalog.Vulnerable(Active.BossOid, actor.Oid)
                    ? new Vector4(1, 0, 1, 1) : new Vector4(1, 0, 0, 1));
        }
    }
}
