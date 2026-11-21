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

    internal bool Active { get; private set; }
    internal uint ActiveBossOid { get; private set; }
    internal IGeometryShape? Boundary { get; private set; }
    internal IReadOnlyDictionary<ulong, ReplayActorState> Actors => actors;
    internal IReadOnlyDictionary<ulong, (ulong Target, DateTime Until)> ClawTethers => clawTethers;

    internal void Synchronize(IEnumerable<ReplayActorState> observations, DateTime now)
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
                    clawTethers[actor.Id] = (tether.TargetId, now.AddSeconds(15));
        }
        if (boss.Statuses.Any(s => s.Id == 1399 && s.ExpiresAt > now))
            ClearHaywire();
        foreach (var key in casts.Where(p => !incoming.ContainsKey(p.Value.Source)
            || incoming[p.Value.Source].Dead || now > p.Value.Finish.AddSeconds(.5)).Select(p => p.Key).ToArray())
            casts.Remove(key);
        foreach (var id in spreadTargets.Where(p => now > p.Value.AddSeconds(.3)).Select(p => p.Key).ToArray())
            spreadTargets.Remove(id);
        foreach (var id in baitTargets.Where(p => now > p.Value.Until.AddSeconds(.3)
            || !incoming.ContainsKey(p.Key) || !incoming.ContainsKey(p.Value.Target)).Select(p => p.Key).ToArray())
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
            var first = casts.Values.Where(c => c.Action == 7960 && c.Finish <= now.AddSeconds(1)).MinBy(c => c.Finish);
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
            clawTethers[tether.SourceId] = (tether.TargetId, now.AddSeconds(15));
    }

    internal void TetherCancel(LogSystemTetherCancelEvent tether) => clawTethers.Remove(tether.SourceId);

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
        var party = actors.Values.Where(a => a.Kind == 1 && !a.Dead && Boundary!.Contains(a.Position)).ToArray();
        var assignedTower = AssignedTower(playerId, party, now);
        foreach (var cast in casts.Values.Where(c => c.Finish.AddSeconds(.3) >= now))
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
                    result.Add(Area(cast, "Ice Grid", RayRectangleTemplate.Symmetric(4, 40), hit));
                    break;
                case 33461:
                    AddSpread(result, cast.Key, "Blizzard II 分散", cast.TargetId, 5, hit, now, playerId, party, cast.Source, cast.Action);
                    break;
                case 33462:
                    result.Add(Area(cast, "Fire II 分摊", new CircleTemplate(5), hit) with
                    {
                        Position = TargetPosition(cast), Safe = true, From = now,
                    });
                    break;
                case 33463:
                    result.Add(Area(cast, "Triflame", new FanTemplate(60, Rad(60)), hit));
                    break;
                case 33464:
                    result.Add(Area(cast, "Thunder II 塔", new CircleTemplate(5), hit) with
                    {
                        Safe = true, From = now, PreviewOnly = assignedTower != cast.Key,
                    });
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
        foreach (var (target, hit) in spreadTargets)
            AddSpread(result, $"spread/{target}", "Rahu Ray 分散", target, 10, hit, now, playerId, party, target, 7978);
        foreach (var (source, bait) in baitTargets)
        {
            if (bait.Until.AddSeconds(.3) < now || !actors.TryGetValue(source, out var caster)
                || !actors.TryGetValue(bait.Target, out var target) || caster.Dead || target.Dead)
                continue;
            result.Add(new($"bait/{source}", "Sparking Current 引导", RayRectangleTemplate.Symmetric(6, 20),
                caster.Position, GeometryHelper.AngleTo(caster.Position, target.Position),
                bait.Until.AddSeconds(-.2), bait.Until.AddSeconds(.3), source, 33467,
                PreviewOnly: bait.Target == playerId));
            if (bait.Target == playerId)
                result.Add(new($"bait-aim/{source}", "Sparking Current 引导",
                    new CastrumBaitTemplate(Boundary!, caster.Position,
                        party.Where(a => a.Id != playerId).Select(a => a.Position).ToArray()),
                    caster.Position, 0, bait.Until.AddSeconds(-.2), bait.Until.AddSeconds(.3), source, 33467,
                    NavigationOnly: true));
        }
        foreach (var (source, tether) in clawTethers)
            if (tether.Target == playerId && actors.TryGetValue(source, out var claw) && !claw.Dead)
                result.Add(new($"claw/{source}", "Death Grip 风筝", new CircleTemplate(10), claw.Position,
                    0, now, tether.Until, source, NavigationOnly: true));
        AddKnockbacks(result, now, playerId);
        if (ActiveBossOid == CastrumAbaniaCatalog.MagnaRoader && casts.Values.Any(c => c.Action == 7960))
        {
            var player = actors.GetValueOrDefault(playerId);
            var turret = actors.Values.Where(a => a.Oid == CastrumAbaniaCatalog.Cannon && a.Targetable && !a.Dead)
                .MinBy(a => Vector3.DistanceSquared(a.Position, player?.Position ?? Boundary!.Position));
            if (turret != null)
                result.Add(new($"cannon/{turret.Id}", "炮台交互", new CircleTemplate(1.5f),
                    turret.Position, 0, now, now.AddSeconds(.5), turret.Id, Safe: true));
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
    }

    private void ClearHaywire()
    {
        foreach (var key in casts.Where(p => p.Value.Action == 7960).Select(p => p.Key).ToArray())
            casts.Remove(key);
    }

    private static CastrumArea Area(CastrumCast cast, string name, GeometryTemplate shape, DateTime hit)
        => new(cast.Key, name, shape, cast.Position, cast.Rotation, hit.AddSeconds(-.2),
            hit.AddSeconds(.3), cast.Source, cast.Action);

    private void AddSpread(List<CastrumArea> result, string key, string name, ulong target, float radius,
        DateTime hit, DateTime now, ulong player, ReplayActorState[] party, ulong owner, uint action)
    {
        if (hit.AddSeconds(.3) < now || !actors.TryGetValue(target, out var actor) || actor.Dead) return;
        result.Add(new(key, name, new CircleTemplate(radius), actor.Position, 0,
            hit.AddSeconds(-.2), hit.AddSeconds(.3), owner, action, PreviewOnly: target == player));
        if (target != player) return;
        foreach (var other in party.Where(a => a.Id != player))
            result.Add(new($"{key}/avoid/{other.Id}", name, new CircleTemplate(radius), other.Position, 0,
                hit.AddSeconds(-.2), hit.AddSeconds(.3), owner, action, NavigationOnly: true));
    }

    private string? AssignedTower(ulong player, ReplayActorState[] party, DateTime now)
    {
        var towers = casts.Values.Where(c => c.Action == 33464 && c.Finish >= now).ToArray();
        var usedPlayers = new HashSet<ulong>();
        var usedTowers = new HashSet<string>();
        var pairs = from tower in towers from member in party
                    orderby Vector3.DistanceSquared(tower.Position, member.Position), member.Id, tower.Source
                    select (tower, member);
        foreach (var (tower, member) in pairs)
        {
            if (usedPlayers.Contains(member.Id) || usedTowers.Contains(tower.Key)) continue;
            usedPlayers.Add(member.Id);
            usedTowers.Add(tower.Key);
            if (member.Id == player) return tower.Key;
        }
        return null;
    }

    private void AddKnockbacks(List<CastrumArea> result, DateTime now, ulong player)
    {
        foreach (var cast in casts.Values.Where(c => c.Action is 8328 or 8329 && c.Finish >= now))
        {
            if (actors.GetValueOrDefault(player)?.Statuses.Any(s => s.Id is 160 or 1209 && s.ExpiresAt > cast.Finish) == true)
                continue;
            var landingDangers = result.Where(a => a.Action is 7975 or 7976
                && a.Until >= cast.Finish && a.From <= cast.Finish.AddSeconds(1)).Select(a => a.Geometry).ToArray();
            result.Add(Area(cast, "Rahu Comet 击退落点",
                new CastrumKnockbackTemplate(Boundary!, cast.Position, cast.Action == 8328 ? 5 : 10, landingDangers),
                cast.Finish) with { Key = $"knockback/{cast.Key}", NavigationOnly = true });
            if (landingDangers.Length == 0)
                result.Add(new($"knockback-goal/{cast.Key}", "Rahu Comet 击退站位",
                    new FanTemplate(20, Rad(40)), Boundary!.Position,
                    GeometryHelper.AngleTo(cast.Position, Boundary.Position), now, cast.Finish,
                    cast.Source, cast.Action, Safe: true));
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
