using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsCoilNodes => territory is CoilNodesCatalog.FIRST or CoilNodesCatalog.SECOND && !IsCaduceus;
    internal IReadOnlyList<string> CurrentPartyAssignments { get; set; } = [];
    private readonly DateTime[] coilRotExpiry = new DateTime[8];
    private readonly DateTime[] coilImmunityExpiry = new DateTime[8];
    private readonly Dictionary<(ulong Actor, uint Status), (bool Present, DateTime Expiry, ushort Param, ulong Source)> coilRawStatus = [];
    private readonly Dictionary<(ulong Actor, uint Status), (bool Present, DateTime Expiry, ushort Param, ulong Source, DateTime At)> coilPendingStatus = [];
    private readonly Dictionary<(ulong Source, DateTime Start), ulong> coilRotCasts = [];
    private readonly List<(Vector3 Position, DateTime At, int Key)> coilGravity = [];
    private int coilRotHolder = -1;
    private int coilGravityKey;
    private bool coilWiping;

    private bool CoilCanStart(DungeonActor actor)
        => !coilWiping && actor.InCombat && actor.Hp != 0
            && (actor.Oid != 0x7D7 || actor.Hp is { } hp && actor.MaxHp is > 0 && hp > actor.MaxHp / 2)
            && (territory != CoilNodesCatalog.SECOND || actor.Oid == 0x7D9
                || !_actors.Values.Any(a => a.Oid == 0x7D9 && a.InCombat));

    private bool CoilHasRot => territory == CoilNodesCatalog.SECOND && Active?.BossOid is 0x7D9 or 0x7DA;
    private bool CoilHasGravity => IsCoilNodes && Active?.Casts.Any(c => c.Action == 1220) == true;

    private string CoilRole(DungeonActor? actor)
        => actor != null && actor.PartySlot >= 0 && actor.PartySlot < CurrentPartyAssignments.Count
            ? CurrentPartyAssignments[actor.PartySlot] : "Unassigned";

    private void CoilDirector(uint command)
    {
        if (territory is not (CoilNodesCatalog.FIRST or CoilNodesCatalog.SECOND)) return;
        if (command == 0x40000005) { coilWiping = true; ResetFight(); }
        if (command == 0x40000011) coilWiping = false;
    }

    private void CoilObserve()
    {
        if (!IsCoilNodes || Active == null) return;
        if (CoilHasGravity)
        {
            // GravityField's source deliberately includes EventState 7 objects.
            foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1E8728))
            {
                var index = coilGravity.FindIndex(p => Vector2.DistanceSquared(new(p.Position.X, p.Position.Z),
                    new(actor.Position.X, actor.Position.Z)) <= 4);
                if (index >= 0) coilGravity.RemoveAt(index);
            }
        }
        if (!CoilHasRot) return;
        foreach (var key in coilRawStatus.Keys.Concat(coilPendingStatus.Keys).Distinct().Where(k => !_actors.ContainsKey(k.Actor)).ToArray())
        {
            coilRawStatus.Remove(key);
            coilPendingStatus.Remove(key);
        }
        foreach (var actor in _actors.Values)
        {
            foreach (var status in new uint[] { 333, 334 })
            {
                var key = (actor.Id, status);
                var next = (Present: actor.Statuses?.Contains(status) == true,
                    Expiry: actor.StatusExpirations?.GetValueOrDefault(status) ?? default,
                    Param: actor.StatusParams?.GetValueOrDefault(status) ?? 0,
                    Source: actor.StatusSources?.GetValueOrDefault(status) ?? 0);
                if (coilPendingStatus.TryGetValue(key, out var pending))
                {
                    var confirms = pending.Present == next.Present && (!pending.Present
                        || pending.Param == next.Param && pending.Source == next.Source
                            && (pending.Expiry == default || Math.Abs((pending.Expiry - next.Expiry).TotalSeconds) <= 1));
                    if (!confirms && _now < pending.At.AddSeconds(.5)) continue;
                    coilPendingStatus.Remove(key);
                    if (confirms)
                    {
                        if (next.Present && pending.Expiry == default && next.Expiry != default)
                            CoilStatusGain(actor, status, next.Expiry);
                        coilRawStatus[key] = next;
                        continue;
                    }
                }
                if (next.Present && (!coilRawStatus.TryGetValue(key, out var previous) || !previous.Present
                    || previous.Param != next.Param || previous.Source != next.Source || (next.Expiry - previous.Expiry).TotalSeconds > 1))
                    CoilStatusGain(actor, status, next.Expiry);
                coilRawStatus[key] = next;
            }
        }
    }

    private void CoilStatus(ulong target, uint status, bool present, DateTime? expiresAt, ushort param, ulong source)
    {
        if (!CoilHasRot || status is not (333 or 334) || !_actors.TryGetValue(target, out var actor)) return;
        var expiry = expiresAt ?? default;
        if (present) CoilStatusGain(actor, status, expiry);
        // Source intentionally retains the last holder and immunity deadline on loss.
        coilPendingStatus[(target, status)] = (present, expiry, param, source, _now);
    }

    private void CoilRemoved(ulong actor)
    {
        foreach (var key in coilRawStatus.Keys.Concat(coilPendingStatus.Keys).Distinct().Where(k => k.Actor == actor).ToArray())
        {
            coilRawStatus.Remove(key);
            coilPendingStatus.Remove(key);
        }
        foreach (var key in coilRotCasts.Keys.Where(k => k.Source == actor).ToArray()) coilRotCasts.Remove(key);
    }

    private void CoilStatusGain(DungeonActor actor, uint status, DateTime expiry)
    {
        if (actor.PartySlot is < 0 or >= 8) return;
        if (status == 333) { coilRotHolder = actor.PartySlot; coilRotExpiry[actor.PartySlot] = expiry; }
        else coilImmunityExpiry[actor.PartySlot] = expiry;
    }

    private void CoilCast(DungeonPendingCast cast)
    {
        if (!CoilHasRot || cast.Rule.Action != 1218) return;
        var key = (cast.Source, cast.StartedAt);
        if (coilRotCasts.TryGetValue(key, out var previous) && previous == cast.TargetId) return;
        coilRotCasts[key] = cast.TargetId;
        coilRotHolder = _actors.GetValueOrDefault(cast.TargetId)?.PartySlot ?? -1;
        if (coilRotHolder is >= 0 and < 8) coilRotExpiry[coilRotHolder] = cast.FinishAt.AddSeconds(15);
    }

    private void CoilEffect(uint action, ulong target, Vector3? location)
    {
        if (!CoilHasGravity || action != 1220) return;
        if ((_actors.GetValueOrDefault(target)?.Position ?? location) is { } position)
            coilGravity.Add((position, _now.AddSeconds(1), ++coilGravityKey));
    }

    private DungeonActor? CoilRotReceiver()
    {
        if (!CoilHasRot || coilRotHolder is < 0 or >= 8 || (coilRotExpiry[coilRotHolder] - _now).TotalSeconds > 7
            || CurrentPartyAssignments.Count != 8 || CurrentPartyAssignments.Distinct().Count() != 8
            || CurrentPartyAssignments.Contains("Unassigned")) return null;
        foreach (var role in new[] { "R1", "M1", "M2", "H1", "H2", "R2" })
        {
            var next = _actors.Values.FirstOrDefault(a => CoilRole(a) == role && a.PartySlot != coilRotHolder
                && !a.Dead && a.PartySlot is >= 0 and < 8 && coilImmunityExpiry[a.PartySlot] < coilRotExpiry[coilRotHolder]);
            if (next != null) return next;
        }
        return null;
    }

    private IEnumerable<DungeonAmbientAoe> CoilAoes()
    {
        if (!IsCoilNodes || Active == null) yield break;
        if (territory == CoilNodesCatalog.SECOND)
        {
            var range = Active.BossOid == 0x7D9 ? 8.3f : 7.15f;
            foreach (var aoe in CoilCleaves(Active.BossOid, range, activeWhileCasting: false)) yield return aoe;
        }
        if (CoilHasGravity)
        {
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 1220))
                yield return new($"coil/gravity/cast/{cast.Source}", "重力场落点", new CircleTemplate(6),
                    _actors.GetValueOrDefault(cast.TargetId)?.Position ?? cast.Location, Activation: cast.FinishAt);
            foreach (var p in coilGravity)
                yield return new($"coil/gravity/event/{p.Key}", "重力场生成", new CircleTemplate(6), p.Position, Activation: p.At);
            foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1E8728))
                yield return new($"coil/gravity/live/{actor.Id}", "重力场", new CircleTemplate(6), QuantizedDungeonPosition(actor.Position));
        }
        if (!CoilHasRot || coilRotHolder is < 0 or >= 8) yield break;
        var holder = _actors.Values.FirstOrDefault(a => a.PartySlot == coilRotHolder);
        var player = _actors.GetValueOrDefault(PlayerId);
        if (holder == null || player == null) yield break;
        yield return new("coil/rot/holder", "亚拉戈病毒持有者", new CircleTemplate(3), holder.Position,
            Navigate: false, OutlineOnly: true);
        if (player.PartySlot == coilRotHolder) yield break;
        var receive = CoilRotReceiver()?.Id == PlayerId;
        var activation = default(DateTime);
        if (receive) activation = coilRotExpiry[coilRotHolder];
        else if (player.PartySlot is >= 0 and < 8) activation = coilImmunityExpiry[player.PartySlot];
        GeometryTemplate shape = new CircleTemplate(7);
        if (receive && Arena != null)
            shape = new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(holder.Position, 2)]);
        yield return new("coil/rot/personal", receive ? "接取亚拉戈病毒" : "远离病毒持有者",
            shape, holder.Position, Display: false, Activation: activation);
    }

    private IEnumerable<DungeonAmbientAoe> CoilCleaves(uint oid, float radius, bool activeWhileCasting)
    {
        foreach (var enemy in _actors.Values.Where(a => a.Oid == oid && !a.Dead && a.Targetable
            && (activeWhileCasting || !CoilCastActive(a))))
        {
            if (_actors.GetValueOrDefault(enemy.TargetId) is not { } target) continue;
            var origin = QuantizedDungeonPosition(enemy.Position);
            var angle = MathF.Atan2(target.Position.X - enemy.Position.X, target.Position.Z - enemy.Position.Z);
            yield return new($"coil/cleave/outline/{enemy.Id}", "顺劈", new FanTemplate(radius, MathF.PI * 2 / 3),
                origin, angle, Navigate: target.Id != PlayerId, OutlineOnly: true);
            if (target.Id != PlayerId) continue;
            foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
            {
                var direction = MathF.Atan2(buddy.Position.X - enemy.Position.X, buddy.Position.Z - enemy.Position.Z);
                yield return new($"coil/cleave/bait/{enemy.Id}/{buddy.Id}", "顺劈远离队友", new FanTemplate(100, MathF.PI * 2 / 3),
                    origin, direction, Display: false);
            }
        }
    }

    private bool CoilCastActive(DungeonActor actor)
        => _casts.ContainsKey(actor.Id) || actor.Cast is { } cast
            && (!_finished.TryGetValue(actor.Id, out var end) || cast.StartedAt > end.EndedAt
                || cast.StartedAt == end.EndedAt && !end.ActionsAtEnd.Contains(cast.Action));

    private int CoilPriority(DungeonActor actor)
    {
        if (actor.Id == PrimaryId) return 1;
        if (territory == CoilNodesCatalog.FIRST && actor.Oid is 0x888 or 0x889 or 0x88A)
            return CoilRole(_actors.GetValueOrDefault(PlayerId)) == "OT" ? 2 : 0;
        return 0;
    }

    internal DungeonActor? CoilInterruptTarget(uint job)
        => IsCoilNodes && Active != null && KtisisCatalog.InterruptAction(job) != 0
            && _casts.TryGetValue(PrimaryId, out var cast) && cast.Rule.Action is 1447 or 1216
                ? _actors.GetValueOrDefault(PrimaryId) : null;

    private IEnumerable<string> CoilHints()
    {
        if (!IsCoilNodes || Active == null) yield break;
        if (CoilHasRot && coilRotHolder is >= 0 and < 8)
            yield return $"亚拉戈病毒：{(coilRotExpiry[coilRotHolder] - _now).TotalSeconds:F1} 秒";
        if (territory == CoilNodesCatalog.SECOND && _actors.GetValueOrDefault(PrimaryId) is { } boss
            && _actors.GetValueOrDefault(boss.TargetId) is { } tank && tank.StatusParams?.GetValueOrDefault(202u) >= 4
            && CoilRole(_actors.GetValueOrDefault(PlayerId)) is "MT" or "OT"
            && boss.TargetId != PlayerId && _actors.GetValueOrDefault(PlayerId) is { } player && !HasStatus(player, 202))
            yield return "易伤达到4层：换坦挑衅";
        if (_actors.GetValueOrDefault(PlayerId) is { } pc && CoilAoes().Any(a => a.Id.StartsWith("coil/cleave/outline")
            && a.Navigate && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(pc.Position)))
            yield return "离开顺劈范围";
    }

    private void ResetCoilNodes()
    {
        Array.Clear(coilRotExpiry); Array.Clear(coilImmunityExpiry);
        coilRawStatus.Clear(); coilPendingStatus.Clear(); coilRotCasts.Clear(); coilGravity.Clear();
        coilRotHolder = -1; coilGravityKey = 0;
    }
}
