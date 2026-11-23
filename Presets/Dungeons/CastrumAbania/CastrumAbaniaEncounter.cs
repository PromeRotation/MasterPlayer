using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Dungeons;

// Mechanic definitions ported from BMR's D05CastrumAbania modules (MIT).
internal sealed class CastrumAbaniaEncounter
{
    private readonly Dictionary<ulong, ReplayActorState> actors = [];
    private readonly Dictionary<string, CastrumCast> casts = [];
    // Keep completed observations until the pull ends so a stale snapshot cannot
    // re-arm a cast after ActionEffect or cancellation.
    private readonly List<CastrumCast> observedCasts = [];
    private readonly Dictionary<ulong, DateTime> spreadTargets = [];
    private readonly Dictionary<ulong, (ulong Target, DateTime Until)> baitTargets = [];
    private readonly Dictionary<ulong, (ulong Target, DateTime Until)> clawTethers = [];
    private readonly HashSet<(ulong Source, uint Action, uint Sequence)> effects = [];
    private readonly Dictionary<ulong, DateTime> clawTetherEvents = [];
    private readonly HashSet<ulong> cancelledClaws = [];
    private IReadOnlySet<ulong>? partyIds;

    internal bool Active { get; private set; }
    internal uint ActiveBossOid { get; private set; }
    internal IGeometryShape? Boundary { get; private set; }
    internal IReadOnlyDictionary<ulong, ReplayActorState> Actors => actors;
    internal IReadOnlyDictionary<ulong, (ulong Target, DateTime Until)> ClawTethers => clawTethers;
    internal bool HaywireActive => ActiveBossOid == CastrumAbaniaCatalog.MagnaRoader && casts.Values.Any(c => c.Action == 7960);
    internal ulong[] ForbiddenTargets => HaywireActive && !actors.Values.Any(a => a.Oid == CastrumAbaniaCatalog.Cannon && a.Targetable && !a.Dead)
        ? actors.Values.Where(a => a.Hostile && a.Targetable && !a.Dead).Select(a => a.Id).ToArray() : [];

    internal ReplayActorState? CannonTarget(Vector3 player)
        => HaywireActive ? actors.Values.Where(a => a.Oid == CastrumAbaniaCatalog.Cannon && a.Targetable && !a.Dead)
            .MinBy(a => Vector3.DistanceSquared(a.Position, player)) : null;

    internal ulong PriorityTarget(ulong current, Vector3 player)
    {
        if (!Active) return 0;
        var cannon = CannonTarget(player);
        if (cannon != null && Vector3.DistanceSquared(cannon.Position, player) < 12.25f) return cannon.Id;
        if (HaywireActive && cannon == null) return 0;
        var candidates = actors.Values.Where(a => !a.Dead && a.Targetable && a.Hostile
            && Boundary!.Contains(a.Position)).ToArray();
        int Priority(ReplayActorState actor) => (ActiveBossOid, actor.Oid) switch
        {
            (CastrumAbaniaCatalog.MagnaRoader, 0x1AAA) => 1,
            (CastrumAbaniaCatalog.Inferno, CastrumAbaniaCatalog.DeathClaw) => 2,
            (CastrumAbaniaCatalog.Inferno, 0x1AAF) => 1,
            _ => 0,
        };
        return candidates.OrderByDescending(Priority).ThenBy(a => a.Id != current)
            .ThenBy(a => Vector3.DistanceSquared(a.Position, player)).FirstOrDefault()?.Id ?? 0;
    }

    internal void Synchronize(IEnumerable<ReplayActorState> observations, DateTime now, IReadOnlySet<ulong>? party = null)
    {
        var incoming = observations.Where(a => a.Id != 0).ToDictionary(a => a.Id);
        var boss = incoming.Values.FirstOrDefault(a => CastrumAbaniaCatalog.IsBoss(a.Oid) && !a.Dead && a.InCombat);
        if (boss == null)
        {
            Reset();
            foreach (var (id, actor) in incoming) actors[id] = actor;
            return;
        }

        if (!Active || ActiveBossOid != boss.Oid || !actors.ContainsKey(boss.Id))
        {
            Reset();
            Active = true;
            ActiveBossOid = boss.Oid;
            Boundary = CastrumAbaniaArena.For(boss.Oid, boss.Position.Y);
        }
        actors.Clear();
        partyIds = party;
        foreach (var (id, actor) in incoming) actors[id] = actor;
        foreach (var actor in incoming.Values)
        {
            if (actor.Dead)
            {
                Cancel(actor.Id);
                continue;
            }
            if (actor.Cast is { EffectSeen: false } cast)
                StartCast(actor.Id, actor.Oid, cast.ActionId, cast.Location, cast.Rotation,
                    cast.StartedAt, cast.FinishAt, cast.TargetId, actor.Position);
            foreach (var tether in actor.Tethers)
                if (ActiveBossOid == CastrumAbaniaCatalog.Inferno && actor.Oid == CastrumAbaniaCatalog.DeathClaw
                    && tether.Id == CastrumAbaniaCatalog.ClawTether)
                    if (!cancelledClaws.Contains(actor.Id) && (!clawTetherEvents.TryGetValue(actor.Id, out var changed) || now >= changed.AddSeconds(.5)))
                        clawTethers[actor.Id] = (tether.TargetId, DateTime.MaxValue);
            if (actor.Oid == CastrumAbaniaCatalog.DeathClaw && !actor.Tethers.Any(t => t.Id == CastrumAbaniaCatalog.ClawTether)
                && (!clawTetherEvents.TryGetValue(actor.Id, out var observed) || now >= observed.AddSeconds(.5)))
            {
                clawTethers.Remove(actor.Id);
                cancelledClaws.Remove(actor.Id);
            }
        }
        if (boss.Statuses.Any(s => s.Id == 1399 && s.ExpiresAt > now))
            ClearHaywire();
        foreach (var key in casts.Where(p => !incoming.ContainsKey(p.Value.Source)
            || incoming[p.Value.Source].Dead || p.Value.Action != 7960
                && (now > p.Value.Finish.AddSeconds(.5)
                    || incoming[p.Value.Source].Cast?.ActionId != p.Value.Action && now >= p.Value.Start.AddSeconds(.5))).Select(p => p.Key).ToArray())
            casts.Remove(key);
        foreach (var id in spreadTargets.Keys.Where(id => !incoming.TryGetValue(id, out var a) || a.Dead).ToArray())
            spreadTargets.Remove(id);
        foreach (var id in baitTargets.Where(p => !incoming.ContainsKey(p.Key) || !incoming.ContainsKey(p.Value.Target)
            || incoming[p.Key].Dead || incoming[p.Value.Target].Dead).Select(p => p.Key).ToArray())
            baitTargets.Remove(id);
        foreach (var id in clawTethers.Where(p => now > p.Value.Until
            || !incoming.ContainsKey(p.Key) || !incoming.ContainsKey(p.Value.Target)
            || incoming[p.Key].Dead || incoming[p.Value.Target].Dead).Select(p => p.Key).ToArray())
            clawTethers.Remove(id);
    }

    internal void StartCast(ulong source, uint sourceOid, uint action, Vector3 location, float? rotation,
        DateTime started, DateTime finish, ulong targetId, Vector3? sourcePosition = null)
    {
        if (!Active || !IsHandledCast(sourceOid, action) || finish < started)
            return;
        if (IsDirectional(action) && (rotation == null || !float.IsFinite(rotation.Value)))
            return;
        var position = IsLocationTargeted(action) ? location
            : sourcePosition ?? actors.GetValueOrDefault(source)?.Position ?? location;
        if (!IsFinite(position))
            return;
        if (observedCasts.Any(c => c.Source == source && c.Action == action
            && Math.Abs((c.Finish - finish).TotalSeconds) < .2))
            return;
        var cast = new CastrumCast(source, action, position, rotation ?? 0, started, finish, Normalize(targetId));
        observedCasts.Add(cast);
        casts[cast.Key] = cast;
    }

    internal void Apply(LogSystemActionEffectEvent effect, DateTime now)
    {
        if (!Active || !CastrumAbaniaCatalog.Actions.Contains(effect.ActionId))
            return;
        if (effect.GlobalSequence == 0 && effect.TargetIndex > 0)
            return;
        var duplicate = effect.GlobalSequence != 0
            && !effects.Add((effect.SourceId, effect.ActionId, effect.GlobalSequence));
        // Rahu Ray can hit several marked targets in one packet.
        if (effect.ActionId == 7978)
            spreadTargets.Remove(effect.TargetId);
        if (duplicate)
            return;
        var cast = casts.Values.Where(c => c.Source == effect.SourceId && c.Action == effect.ActionId
            && c.Finish <= now.AddSeconds(1)).MinBy(c => c.Finish);
        if (cast != null) casts.Remove(cast.Key);
        if (effect.ActionId == 8184)
        {
            var first = casts.Values.FirstOrDefault(c => c.Action == 7960);
            if (first != null) casts.Remove(first.Key);
        }
        if (ActiveBossOid == CastrumAbaniaCatalog.SubjectNumberXXIV)
        {
            if (effect.ActionId == 33467 && Normalize(effect.TargetId) != 0)
                baitTargets[effect.SourceId] = (effect.TargetId, now.AddSeconds(5));
            else if (effect.ActionId == 33466)
                baitTargets.Clear();
        }
    }

    internal void Status(LogSystemStatusAddEvent status)
    {
        if (status.StatusId == 1399 && actors.GetValueOrDefault(status.TargetId)?.Oid == CastrumAbaniaCatalog.MagnaRoader)
            ClearHaywire();
    }

    internal void Icon(LogSystemTargetIconEvent icon, DateTime now)
    {
        if (ActiveBossOid != CastrumAbaniaCatalog.Inferno || icon.MarkerId != CastrumAbaniaCatalog.TargetIconSpread)
            return;
        var target = Normalize(icon.TargetId);
        if (target == 0) target = Normalize(icon.SourceId);
        if (target != 0 && !spreadTargets.ContainsKey(target))
            spreadTargets[target] = now.AddSeconds(4.1);
    }

    internal void Tether(LogSystemTetherEvent tether, DateTime now)
    {
        if (ActiveBossOid == CastrumAbaniaCatalog.Inferno && tether.TetherId == CastrumAbaniaCatalog.ClawTether
            && Normalize(tether.TargetId) != 0)
        {
            clawTetherEvents[tether.SourceId] = now;
            cancelledClaws.Remove(tether.SourceId);
            clawTethers[tether.SourceId] = (tether.TargetId, DateTime.MaxValue);
        }
    }

    internal void TetherCancel(LogSystemTetherCancelEvent tether)
    {
        clawTetherEvents[tether.SourceId] = tether.Timestamp.ToUniversalTime();
        cancelledClaws.Add(tether.SourceId);
        clawTethers.Remove(tether.SourceId);
    }

    internal void Cancel(ulong source, uint action = 0)
    {
        foreach (var key in casts.Where(p => p.Value.Source == source && (action == 0 || p.Value.Action == action))
            .Select(p => p.Key).ToArray())
            casts.Remove(key);
        if (action != 0) return;
        spreadTargets.Remove(source);
        baitTargets.Remove(source);
        clawTethers.Remove(source);
    }

    internal IReadOnlyList<CastrumArea> Collect(DateTime now, ulong playerId)
    {
        var result = new List<CastrumArea>();
        if (!Active) return result;
        var party = actors.Values.Where(a => (partyIds?.Contains(a.Id) ?? a.Kind == 1) && !a.Dead && Boundary!.Contains(a.Position)).ToArray();
        var visibleGrid = casts.Values.Where(c => c.Action == 33460).Take(10).Select(c => c.Key).ToHashSet();
        var visibleFlame = casts.Values.Where(c => c.Action == 33463).Take(3).Select(c => c.Key).ToHashSet();
        foreach (var cast in casts.Values)
        {
            var hit = cast.Action == 7960 ? cast.Finish.AddSeconds(-.2) : cast.Finish;
            switch (cast.Action)
            {
                case 7957:
                    result.Add(Area(cast, "Magitek Fire II", new CircleTemplate(5), hit));
                    break;
                case 7960:
                    result.Add(Area(cast, "Haywire Telegraph", RayRectangleTemplate.Symmetric(6, 40.5f), hit));
                    break;
                case 8336:
                    result.Add(Area(cast, "Magitek Pulse", new CircleTemplate(6), hit));
                    break;
                case 33460:
                    if (visibleGrid.Contains(cast.Key)) result.Add(Area(cast, "Ice Grid", RayRectangleTemplate.Symmetric(4, 40), hit));
                    break;
                case 33461:
                    AddSpread(result, cast.Key, "Blizzard II 分散", cast.TargetId, 5, hit, now, playerId, party, cast.Source, cast.Action);
                    break;
                case 33462:
                    result.Add(Area(cast, "Fire II 分摊", new CircleTemplate(5), hit) with
                    {
                        Position = TargetPosition(cast), Safe = true, PreviewOnly = true,
                    });
                    if (cast.TargetId != playerId)
                        result.Add(Area(cast, "Fire II 集合", new SohrKhaiWallHintTemplate(Boundary!,
                            [new CircleGeometry(TargetPosition(cast), 5)]), hit) with
                        { Key = cast.Key + "/enter", Position = Boundary!.Position, NavigationOnly = true });
                    break;
                case 33463:
                    if (visibleFlame.Contains(cast.Key)) result.Add(Area(cast, "Triflame", new FanTemplate(60, Rad(60)), hit));
                    break;
                case 33464:
                    break;
                case 7975:
                    result.Add(Area(cast, "Ketu Cutter", new FanTemplate(20.5f, Rad(20)), hit));
                    break;
                case 7976:
                    result.Add(Area(cast, "Ketu Wave", new CircleTemplate(10), hit));
                    break;
                case 7977 or 8334 or 8335:
                    result.Add(Area(cast, "Rahu Blaster", RayRectangleTemplate.Symmetric(6, 44.5f), hit));
                    break;
                case 7979 or 8328 or 8329:
                    result.Add(Area(cast, "Rahu Comet", new CircleTemplate(15), hit));
                    break;
            }
        }
        AddTowers(result, party, playerId);
        foreach (var (target, hit) in spreadTargets)
            AddSpread(result, $"spread/{target}", "Rahu Ray 分散", target, 10, hit, now, playerId, party, target, 7978);
        foreach (var (source, bait) in baitTargets)
        {
            if (!actors.TryGetValue(source, out var caster)
                || !actors.TryGetValue(bait.Target, out var target) || caster.Dead || target.Dead)
                continue;
            result.Add(new($"bait/{source}", "Sparking Current 引导", RayRectangleTemplate.Symmetric(6, 20),
                caster.Position, GeometryHelper.AngleTo(caster.Position, target.Position),
                bait.Until, DateTime.MaxValue, source, 33467,
                PreviewOnly: bait.Target == playerId, OutlineOnly: true));
            if (bait.Target == playerId)
                result.Add(new($"bait-aim/{source}", "Sparking Current 引导",
                    new CastrumBaitTemplate(Boundary!, caster.Position,
                        party.Where(a => a.Id != playerId).Select(a => a.Position).ToArray()),
                    caster.Position, 0, bait.Until, DateTime.MaxValue, source, 33467,
                    NavigationOnly: true));
        }
        foreach (var (source, tether) in clawTethers)
            if (tether.Target == playerId && actors.TryGetValue(source, out var claw) && !claw.Dead)
                result.Add(new($"claw/{source}", "Death Grip 风筝", new CircleTemplate(10), claw.Position,
                    0, now, tether.Until, source, NavigationOnly: true));
        AddKnockbacks(result, now, playerId);
        if (ActiveBossOid == CastrumAbaniaCatalog.MagnaRoader && casts.Values.Any(c => c.Action == 7960))
        {
            foreach (var turret in actors.Values.Where(a => a.Oid == CastrumAbaniaCatalog.Cannon && a.Targetable && !a.Dead))
            {
                result.Add(new($"cannon/{turret.Id}", "炮台交互", new CircleTemplate(1.5f),
                    turret.Position, 0, now, now.AddSeconds(.5), turret.Id, Safe: true, NavigationOnly: true));
                result.Add(new($"cannon/{turret.Id}/display", "炮台交互范围", new CircleTemplate(3),
                    turret.Position, 0, now, DateTime.MaxValue, turret.Id, Safe: true, PreviewOnly: true, OutlineOnly: true));
            }
        }
        return result;
    }

    internal IEnumerable<string> Hints(DateTime now, ulong playerId)
    {
        foreach (var cast in casts.Values.Where(c => c.Finish >= now))
        {
            if (cast.Action is 7958 or >= 33448 and <= 33453) yield return "全屏伤害：准备减伤";
            if (cast.Action == 33462) yield return "Fire II：集合分摊";
            if (cast.Action == 33464) yield return "Thunder II：每塔一人";
            if (cast.Action == 33461) yield return "Blizzard II：分散";
            if (cast.Action is 7974 or 8331 or 8332) yield return "Ketu Slash：坦克死刑";
            if (cast.Action is 8328 or 8329) yield return "Rahu Comet：准备击退";
            if (cast.Action == 7960 && actors.Values.Any(a => a.Oid == CastrumAbaniaCatalog.Cannon && a.Targetable))
                yield return "使用炮台魔导脉冲击晕 Boss";
        }
        if (spreadTargets.TryGetValue(playerId, out var until) && until >= now)
            yield return "Rahu Ray：与队友分散";
        if (clawTethers.Values.Any(t => t.Target == playerId && t.Until >= now))
            yield return "Death Grip：远离死亡之爪至少 10 码";
        if (baitTargets.Values.Any(t => t.Target == playerId && t.Until >= now))
            yield return "Sparking Current：将直线引离队友";
    }

    internal void Reset()
    {
        Active = false;
        ActiveBossOid = 0;
        Boundary = null;
        casts.Clear();
        observedCasts.Clear();
        actors.Clear();
        spreadTargets.Clear();
        baitTargets.Clear();
        clawTethers.Clear();
        effects.Clear();
        clawTetherEvents.Clear();
        cancelledClaws.Clear();
        partyIds = null;
    }

    private void ClearHaywire()
    {
        foreach (var key in casts.Where(p => p.Value.Action == 7960).Select(p => p.Key).ToArray())
            casts.Remove(key);
    }

    private static CastrumArea Area(CastrumCast cast, string name, GeometryTemplate shape, DateTime hit)
        => new(cast.Key, name, shape, cast.Position, cast.Rotation, hit,
            DateTime.MaxValue, cast.Source, cast.Action);

    private void AddSpread(List<CastrumArea> result, string key, string name, ulong target, float radius,
        DateTime hit, DateTime now, ulong player, ReplayActorState[] party, ulong owner, uint action)
    {
        if (!actors.TryGetValue(target, out var actor) || actor.Dead) return;
        result.Add(new(key, name, new CircleTemplate(radius), actor.Position, 0,
            hit, DateTime.MaxValue, owner, action, PreviewOnly: target == player, OutlineOnly: target == player));
        if (target != player) return;
        foreach (var other in party.Where(a => a.Id != player))
            result.Add(new($"{key}/avoid/{other.Id}", name, new CircleTemplate(radius), other.Position, 0,
                hit, DateTime.MaxValue, owner, action, NavigationOnly: true));
    }

    private void AddTowers(List<CastrumArea> result, ReplayActorState[] party, ulong player)
    {
        var towers = casts.Values.Where(c => c.Action == 33464).ToArray();
        var available = new List<IGeometryShape>();
        foreach (var tower in towers)
        {
            var inside = party.Where(a => Vector3.DistanceSquared(a.Position, tower.Position) <= 25).ToArray();
            var safe = inside.Length == 0 || inside.Length == 1 && inside[0].Id == player;
            result.Add(Area(tower, "Thunder II 塔", new CircleTemplate(5), tower.Finish) with
            { Safe = safe, PreviewOnly = true, OutlineOnly = safe });
            if (safe) available.Add(new CircleGeometry(tower.Position, 5));
            else result.Add(Area(tower, "避开已满塔", new CircleTemplate(5), tower.Finish) with
            { Key = tower.Key + "/avoid", NavigationOnly = true });
        }
        if (available.Count != 0)
            result.Add(Area(towers[0], "进入未满塔", new SohrKhaiWallHintTemplate(Boundary!, available.ToArray()), towers[0].Finish) with
            { Key = "tower/enter", Position = Boundary!.Position, NavigationOnly = true });
    }

    private void AddKnockbacks(List<CastrumArea> result, DateTime now, ulong player)
    {
        foreach (var cast in casts.Values.Where(c => c.Action is 8328 or 8329 && c.Finish >= now))
        {
            if (actors.GetValueOrDefault(player)?.Statuses.Any(s => s.Id is 160 or 1209 && s.ExpiresAt > cast.Finish) == true)
                continue;
            var landingDangers = result.Where(a => a.Action is 7975 or 7976
                && a.Until >= cast.Finish && a.From <= cast.Finish.AddSeconds(1)).Select(a => a.Geometry).ToArray();
            var template = new CastrumKnockbackTemplate(Boundary!, cast.Position, cast.Action == 8328 ? 5 : 10, landingDangers);
            result.Add(Area(cast, "Rahu Comet 击退落点",
                template,
                cast.Finish) with { Key = $"knockback/{cast.Key}", NavigationOnly = true });
            if (actors.TryGetValue(player, out var viewer))
            {
                var landing = template.Landing(viewer.Position);
                result.Add(Area(cast, "Rahu Comet 个人落点", new CircleTemplate(.4f), cast.Finish) with
                { Key = $"knockback-landing/{cast.Key}", Position = landing, PreviewOnly = true,
                    Safe = !template.Resolve(Vector3.Zero, 0, null).Contains(viewer.Position), OutlineOnly = true });
            }
            if (!casts.Values.Any(c => c.Action == 7976))
                result.Add(new($"knockback-goal/{cast.Key}", "Rahu Comet 击退站位",
                    new SohrKhaiWallHintTemplate(Boundary!, [new FanTemplate(20, Rad(40)).Resolve(Boundary!.Position,
                        GeometryHelper.AngleTo(cast.Position, Boundary.Position), null)]), Boundary.Position, 0,
                    cast.Finish, DateTime.MaxValue, cast.Source, cast.Action, NavigationOnly: true));
        }
    }

    private Vector3 TargetPosition(CastrumCast cast) => actors.GetValueOrDefault(cast.TargetId)?.Position ?? cast.Position;

    internal bool IsHandledCast(uint casterOid, uint action)
        => ActiveBossOid switch
        {
            CastrumAbaniaCatalog.MagnaRoader => action switch
            {
                7957 or 7958 or 8318 => casterOid == CastrumAbaniaCatalog.MagnaRoader,
                7960 or 8336 => casterOid == CastrumAbaniaCatalog.MagnaHelper,
                _ => false,
            },
            CastrumAbaniaCatalog.SubjectNumberXXIV => action switch
            {
                >= 33448 and <= 33458 or >= 33747 and <= 33750 => casterOid == CastrumAbaniaCatalog.SubjectNumberXXIV,
                >= 33460 and <= 33464 => casterOid == CastrumAbaniaCatalog.SubjectHelper,
                _ => false,
            },
            CastrumAbaniaCatalog.Inferno => action switch
            {
                7973 or 7974 or 7977 or 8331 or 8332 or 8334 or 8335 => casterOid == CastrumAbaniaCatalog.Inferno,
                7975 or 7976 or 7979 or 8328 or 8329 => casterOid == CastrumAbaniaCatalog.InfernoHelper,
                _ => false,
            },
            _ => false,
        };

    internal static bool IsDirectional(uint action) => action is 7960 or 33460 or 33463 or 7975 or 7977 or 8334 or 8335;
    private static bool IsLocationTargeted(uint action) => action is 7957 or 7960 or 8336 or 7976 or 7979 or 8328 or 8329;
    private static ulong Normalize(ulong id) => id is 0 or 0xE0000000 ? 0 : id;
    private static bool IsFinite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
    private static float Rad(float degrees) => degrees * MathF.PI / 180;
}
