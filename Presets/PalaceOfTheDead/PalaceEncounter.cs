using System.Numerics;
using System.Text.Json;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

// Native state model; BMR PalaceFloorModule and DD160..DD190 are the MIT source.
internal sealed class PalaceEncounter
{
    private readonly Dictionary<ulong, ReplayActorState> actors = [];
    private readonly Dictionary<ulong, PalaceCast> casts = [];
    private readonly Dictionary<string, PalaceArea> pending = [];
    private readonly Dictionary<ulong, (DateTime Hit, Vector3 Position)> bombs = [];
    private readonly Dictionary<ulong, DateTime> spikes = [];
    private readonly Dictionary<ulong, DateTime> statusChanges = [];
    private readonly HashSet<ulong> observedSpikes = [];
    private readonly HashSet<(ulong, uint, uint)> effects = [];
    private readonly HashSet<ulong> resolvedBombs = [];
    private int rotationStep;

    internal IReadOnlyCollection<ReplayActorState> Actors => actors.Values;
    internal ReplayActorState? Boss => actors.Values.FirstOrDefault(a => PalaceCatalog.IsBoss(a.Oid) && !a.Dead && a.InCombat);
    internal bool BossActive => Boss != null;
    internal bool ManualBurstHold => Boss is { Oid: 0x181F, MaxHp: > 0 } boss && (float)boss.Hp / boss.MaxHp is > .15f and < .158f;
    internal string Phase => Boss is { Oid: 0x181F } boss ? boss.Hp switch
    {
        > 112063 => "30%",
        > 78444 => "21%",
        > 59767 => "16%",
        > 56031 => "15%",
        _ => "黄道陨石",
    } : "";

    internal void Synchronize(IEnumerable<ReplayActorState> observations, DateTime now)
    {
        var incoming = observations.Where(a => a.Id != 0).ToDictionary(a => a.Id);
        foreach (var id in actors.Keys.Except(incoming.Keys).ToArray()) RemoveActor(id);
        foreach (var (id, actor) in incoming)
        {
            if (!actors.ContainsKey(id) && actor.Oid == 0x18F3) bombs[id] = (now.AddSeconds(10), actor.Position);
            actors[id] = actor;
            if (actor.Dead) Cancel(id);
            if (actor.Statuses.Any(s => s.Id is 197 or 198 && s.ExpiresAt > now))
            {
                if (observedSpikes.Add(id)) spikes[id] = now.AddSeconds(10);
            }
            else if (!statusChanges.TryGetValue(id, out var changed) || now - changed > TimeSpan.FromSeconds(.5))
            {
                spikes.Remove(id);
                observedSpikes.Remove(id);
            }
        }
        foreach (var key in pending.Where(p => incoming.Values.Any(a => a.EventState != 7
            && a.Oid == (p.Value.Action == 7155 ? 0x1E858Eu : 0x1E9998u)
            && Vector3.DistanceSquared(a.Position, p.Value.Position) < 4)).Select(p => p.Key).ToArray()) pending.Remove(key);
        foreach (var key in casts.Where(p => p.Value.Hit.AddSeconds(.5) < now).Select(p => p.Key).ToArray()) casts.Remove(key);
        foreach (var key in pending.Where(p => p.Value.Until < now).Select(p => p.Key).ToArray()) pending.Remove(key);
        foreach (var key in spikes.Where(p => p.Value < now).Select(p => p.Key).ToArray()) spikes.Remove(key);
        foreach (var key in bombs.Where(p => p.Value.Hit.AddSeconds(2) < now).Select(p => p.Key).ToArray()) bombs.Remove(key);
    }

    internal void StartCast(PalaceCast cast)
    {
        if (!actors.TryGetValue(cast.Source, out var actor) || actor.Dead || !actor.Hostile && !PalaceCatalog.IsEnemy(actor.Oid)) return;
        casts[cast.Source] = cast;
    }

    internal void Apply(LogSystemEvent ev, DateTime now)
    {
        switch (ev)
        {
            case LogSystemActionEffectEvent effect:
                if (effect.GlobalSequence == 0 && effect.TargetIndex > 0) return;
                if (effect.GlobalSequence != 0 && !effects.Add((effect.SourceId, effect.GlobalSequence, effect.ActionId))) return;
                if (effects.Count > 2048) effects.Remove(effects.First());
                casts.TryGetValue(effect.SourceId, out var cast);
                if (cast?.Action == effect.ActionId) casts.Remove(effect.SourceId);
                var source = effect.SourcePosition ?? actors.GetValueOrDefault(effect.SourceId)?.Position;
                var origin = effect.ActionId == 7158 ? source
                    : effect.TargetPosition ?? actors.GetValueOrDefault(effect.TargetId)?.Position ?? effect.EffectPosition ?? cast?.Position;
                if (effect.ActionId is 7155 or 7158 && origin is { } position)
                {
                    var delay = effect.ActionId == 7155 ? 1.3 : .8;
                    var radius = effect.ActionId == 7155 ? 9 : 8;
                    var key = $"puddle/{effect.SourceId}/{effect.GlobalSequence}/{effect.ActionId}";
                    pending[key] = new(key, effect.ActionId == 7155 ? "愤慨殿" : "浸泡", new CircleTemplate(radius),
                        position, 0, now.AddSeconds(delay), now.AddSeconds(8), effect.SourceId, effect.ActionId, Telegraph: now);
                }
                if (effect.ActionId == 7172)
                {
                    resolvedBombs.UnionWith(bombs.Keys);
                    bombs.Clear();
                    foreach (var id in casts.Where(p => p.Value.Action == 7172).Select(p => p.Key).ToArray())
                    {
                        resolvedBombs.Add(id);
                        casts.Remove(id);
                    }
                }
                if (effect.ActionId is 7162 or 7163 or 7165) rotationStep = (rotationStep + 1) % 10;
                if (effect.ActionId == 7166) rotationStep = 11;
                break;
            case LogSystemCancelCastEvent cancel:
                Cancel(cancel.SourceId, CancelAction(cancel));
                break;
            case LogSystemInterruptCastEvent interrupt:
                Cancel(interrupt.SourceId, interrupt.ActionId);
                break;
            case LogSystemStatusAddEvent status when status.StatusId is 197 or 198:
                spikes[status.TargetId] = now.AddSeconds(10);
                observedSpikes.Add(status.TargetId);
                statusChanges[status.TargetId] = now;
                break;
            case LogSystemStatusRemoveEvent status when status.StatusId is 197 or 198:
                spikes.Remove(status.TargetId);
                observedSpikes.Remove(status.TargetId);
                statusChanges[status.TargetId] = now;
                break;
        }
    }

    internal void Cancel(ulong source, uint action = 0)
    {
        if (casts.TryGetValue(source, out var cast) && (action == 0 || cast.Action == action)) casts.Remove(source);
    }

    private static uint CancelAction(LogSystemCancelCastEvent cancel)
    {
        // Build 14251465 category 15 carries the action in Param3; PR's older capture exposes Param1.
        var raw = cancel.Raw;
        if (raw is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty("Param2", out var kind) && kind.TryGetUInt32(out var actionType) && actionType is 1 or 2
            && json.TryGetProperty("Param3", out var value) && value.TryGetUInt32(out var recordedAction) && recordedAction != 0)
            return recordedAction;
        if (raw?.GetType().GetProperty("Param2")?.GetValue(raw) is uint type && type is 1 or 2
            && raw.GetType().GetProperty("Param3")?.GetValue(raw) is uint action && action != 0) return action;
        return cancel.ActionId;
    }
    internal void RemoveActor(ulong id)
    {
        actors.Remove(id);
        casts.Remove(id);
        bombs.Remove(id);
        spikes.Remove(id);
        statusChanges.Remove(id);
        observedSpikes.Remove(id);
        resolvedBombs.Remove(id);
    }

    internal IReadOnlyList<PalaceArea> Collect(DateTime now, ulong playerId)
    {
        var result = new List<PalaceArea>();
        foreach (var actor in actors.Values)
        {
            if (actor.OpenedTreasure) continue;
            if (actor.Oid is >= 0x1EA08E and <= 0x1EA092 or 0x1EA9A0 or 0x1EB864 or 0x1EBEDB)
                result.Add(new($"trap/{actor.Id}", "已揭示陷阱", new CircleTemplate(2), actor.Position, 0, now, DateTime.MaxValue, actor.Id));
            if (actor.Dead || actor.EventState == 7) continue;
            var radius = actor.Oid switch { 0x1E858E => 9, 0x1E9998 => 8, 0x18F0 => 10, _ => 0 };
            if (radius > 0) result.Add(new($"object/{actor.Id}", "持续地面", new CircleTemplate(radius), actor.Position,
                0, now, DateTime.MaxValue, actor.Id));
        }
        foreach (var area in pending.Values.Where(a => a.Until >= now))
        {
            if (actors.Values.Any(a => a.EventState != 7 && a.Oid == (area.Action == 7155 ? 0x1E858Eu : 0x1E9998u)
                && Vector3.DistanceSquared(a.Position, area.Position) < 4)) continue;
            result.Add(area);
        }
        foreach (var (id, prediction) in bombs)
        {
            if (!actors.TryGetValue(id, out var actor) || actor.Dead || resolvedBombs.Contains(id) || casts.ContainsKey(id)) continue;
            var hit = prediction.Hit;
            result.Add(new($"bomb/{id}", "冰碎预告", new CircleTemplate(6.6f), prediction.Position, 0,
                hit.AddSeconds(-.2), hit.AddSeconds(.5), id, 7172, Telegraph: hit.AddSeconds(-10)));
        }
        foreach (var cast in casts.Values.Where(c => c.Hit.AddSeconds(.2) >= now))
        {
            if (cast.Action is 6365 or 7103 or 7166 or 7171 || PalaceCatalog.IsGaze(cast.Action)) continue;
            var shape = PalaceCatalog.SpecialShape(cast.Action) ?? cast.Shape;
            if (shape == null) continue;
            if (shape is FanTemplate or RayRectangleTemplate or RectangleTemplate && cast.Rotation == null) continue;
            var position = cast.Position;
            var rotation = cast.Rotation ?? 0;
            if (cast.Action == 7156) continue;
            result.Add(Hit(cast, shape, position, rotation));
        }
        if (Boss is { } boss && actors.TryGetValue(playerId, out var player))
        {
            Cleave(result, boss, player, now);
            foreach (var cast in casts.Values.Where(c => c.Action == 7156 && c.Hit.AddSeconds(.2) >= now))
            {
                if (!actors.TryGetValue(cast.Target, out var target) || !actors.TryGetValue(cast.Source, out var source)) continue;
                var delta = target.Position - source.Position;
                delta.Y = 0;
                var length = delta.Length();
                if (length > .1f)
                    result.Add(Hit(cast, RayRectangleTemplate.Symmetric(6, length), source.Position, MathF.Atan2(delta.X, delta.Z))
                        with { PreviewOnly = cast.Target == playerId, OutlineOnly = cast.Target == playerId });
                if (cast.Target == playerId)
                    foreach (var ally in actors.Values.Where(a => a.Kind == 1 && !a.Dead && a.Id != playerId))
                    {
                        var offset = ally.Position - source.Position;
                        offset.Y = 0;
                        var distance = offset.Length();
                        var halfAngle = distance >= 3 ? MathF.Asin(3 / distance) : MathF.PI;
                        result.Add(new($"charge-bait/{cast.Source}/{ally.Id}", "冲锋远离队友", new FanTemplate(100, halfAngle * 2),
                            source.Position, MathF.Atan2(offset.X, offset.Z), cast.Hit.AddSeconds(-.2), cast.Hit.AddSeconds(.2),
                            cast.Source, cast.Action, NavigationOnly: true));
                    }
                if (cast.Target == playerId && !player.Statuses.Any(s => s.Id is 160 or 1209 or 1096 or 1512 && s.ExpiresAt > cast.Hit))
                {
                    var dangers = result.Where(a => !a.Safe && !a.PreviewOnly && !a.NavigationOnly
                        && a.Until >= cast.Hit).Select(a => a.Geometry).ToArray();
                    result.Add(Hit(cast, new PalaceLandingTemplate(cast.SourcePosition, dangers), PalaceArena.Center, 0)
                        with { Key = $"landing/{cast.Source}", NavigationOnly = true });
                }
            }
            if (boss.Oid == 0x181E && BossInWater(boss))
            {
                result.Add(new($"puddle-boss/{boss.Id}", "泡水首领轮廓", new CircleTemplate(boss.HitboxRadius), boss.Position,
                    0, now, now.AddSeconds(.5), boss.Id, PreviewOnly: true, OutlineOnly: true));
                if (boss.TargetId == playerId) AddPullGoal(result, boss, now);
            }
        }
        return result;
    }

    private static PalaceArea Hit(PalaceCast cast, GeometryTemplate shape, Vector3 position, float rotation)
        => new($"cast/{cast.Source}/{cast.Action}", string.IsNullOrWhiteSpace(cast.Name) ? $"技能 {cast.Action}" : cast.Name,
            shape, position, rotation, cast.Hit.AddSeconds(-.2), cast.Hit.AddSeconds(.2), cast.Source, cast.Action, Telegraph: cast.Start);

    private void Cleave(List<PalaceArea> result, ReplayActorState boss, ReplayActorState player, DateTime now)
    {
        var spec = boss.Oid switch { 0x181D => (11.92f, 90f), 0x181E => (15.75f, 90f), 0x1820 => (11.75f, 120f), _ => (0f, 0f) };
        if (spec.Item1 == 0 || casts.ContainsKey(boss.Id) || !actors.TryGetValue(boss.TargetId, out var target)) return;
        var delta = target.Position - boss.Position;
        var rotation = MathF.Atan2(delta.X, delta.Z);
        result.Add(new($"cleave/{boss.Id}", "无读条顺劈", new FanTemplate(spec.Item1, spec.Item2 * MathF.PI / 180),
            boss.Position, rotation, now, now.AddSeconds(.3), boss.Id, PreviewOnly: boss.TargetId == player.Id));
        if (boss.TargetId == player.Id)
            foreach (var ally in actors.Values.Where(a => a.Kind == 1 && a.Id != player.Id && !a.Dead))
            {
                var heading = MathF.Atan2(ally.Position.X - boss.Position.X, ally.Position.Z - boss.Position.Z);
                result.Add(new($"cleave-bait/{boss.Id}/{ally.Id}", "顺劈远离队友", new FanTemplate(100, spec.Item2 * MathF.PI / 180),
                    boss.Position, heading, now, now.AddSeconds(.3), boss.Id, NavigationOnly: true));
            }
    }

    private bool BossInWater(ReplayActorState boss)
        => actors.Values.Any(a => a.Oid == 0x1E9998 && a.EventState != 7
            && Vector3.DistanceSquared(a.Position, boss.Position) <= MathF.Pow(8 + boss.HitboxRadius, 2));

    private void AddPullGoal(List<PalaceArea> result, ReplayActorState boss, DateTime now)
    {
        var puddles = actors.Values.Where(a => a.Oid == 0x1E9998 && a.EventState != 7).ToArray();
        if (!puddles.Any(p => Vector3.Distance(p.Position, boss.Position) < 8 + boss.HitboxRadius)) return;
        var radius = 8 + 2 * boss.HitboxRadius + 3;
        result.Add(new("pull/170", "把 Boss 拉出水圈", new PalacePullTemplate(puddles.Select(p => p.Position).ToArray(), radius),
            PalaceArena.Center, 0, now, now.AddSeconds(.5), Safe: true, NavigationOnly: true));
    }

    internal float? Facing(DateTime now, ulong playerId)
    {
        if (!actors.TryGetValue(playerId, out var player)) return null;
        var directions = ActiveGazes(now, player).Select(gaze => gaze.Direction).ToArray();
        if (directions.Length == 0) return null;
        // 安全区可能只剩很窄的一段；枚举所有禁向边界之间的中点，避免固定角度余量跨过它。
        const float halfAngle = MathF.PI / 4 + .01f;
        var boundaries = directions.SelectMany(d => new[] { d - halfAngle, d + halfAngle })
            .Select(a => (a % MathF.Tau + MathF.Tau) % MathF.Tau).Order().ToArray();
        var candidates = new List<float> { player.Rotation };
        for (var i = 0; i < boundaries.Length; ++i)
        {
            var end = i + 1 < boundaries.Length ? boundaries[i + 1] : boundaries[0] + MathF.Tau;
            candidates.Add((boundaries[i] + end) * .5f);
        }
        foreach (var candidate in candidates)
            if (directions.All(d => MathF.Abs(MathF.Atan2(MathF.Sin(candidate - d), MathF.Cos(candidate - d))) > halfAngle))
                return candidate;
        return null;
    }

    internal (bool BlockCasts, bool CancelCast) CastRestrictions(DateTime now, ulong playerId, bool avoidGaze)
    {
        if (!actors.TryGetValue(playerId, out var player)) return default;
        var gazes = avoidGaze ? ActiveGazes(now, player).ToArray() : [];
        var mustTurn = gazes.Any(gaze => gaze.Hit <= now.AddSeconds(.5)
            && MathF.Abs(MathF.Atan2(MathF.Sin(player.Rotation - gaze.Direction),
                MathF.Cos(player.Rotation - gaze.Direction))) <= MathF.PI / 4 + .01f);
        var cancel = player.Cast is { EffectSeen: false } cast
            && (mustTurn || ForbiddenTargets(now).Contains(cast.TargetId));
        return (gazes.Length > 0, cancel);
    }

    private IEnumerable<(float Direction, DateTime Hit)> ActiveGazes(DateTime now, ReplayActorState player)
    {
        foreach (var cast in casts.Values.OrderBy(c => c.Hit))
        {
            if (!PalaceCatalog.IsGaze(cast.Action) || cast.Hit < now || cast.Hit > now.AddSeconds(.75)) continue;
            var shape = cast.Action == 6356 ? (GeometryTemplate)new FanTemplate(8.2f, MathF.PI / 2)
                : new CircleTemplate(cast.Action == 6351 ? 100 : 30);
            if (cast.Action == 6356 && cast.Rotation == null) continue;
            if (shape.Resolve(cast.SourcePosition, cast.Rotation ?? 0, null).Contains(player.Position))
                yield return (MathF.Atan2(cast.SourcePosition.X - player.Position.X, cast.SourcePosition.Z - player.Position.Z), cast.Hit);
        }
    }

    internal IReadOnlyList<ulong> ForbiddenTargets(DateTime now)
    {
        var forbidden = new HashSet<ulong>();
        if (ManualBurstHold && Boss is { } boss) forbidden.Add(boss.Id);
        if (Boss is { Oid: 0x1820 } godfather)
            foreach (var bomb in actors.Values.Where(a => a.Oid == 0x18F2 && !a.Dead && Vector3.Distance(a.Position, godfather.Position) <= 7.2f))
                forbidden.Add(bomb.Id);
        return forbidden.ToArray();
    }

    internal IReadOnlyList<ulong> ForbiddenAutoAttackTargets(DateTime now)
        => spikes.Where(p => p.Value > now).Select(p => p.Key).ToArray();

    internal ulong PreferredTarget(ulong playerId, DateTime now)
    {
        var forbidden = ForbiddenTargets(now).ToHashSet();
        return actors.Values.Where(a => !a.Dead && a.InCombat && a.Targetable && a.Oid == 0x1842
            && a.TargetId == playerId && !forbidden.Contains(a.Id)).Select(a => a.Id).FirstOrDefault();
    }

    internal IEnumerable<string> Hints(DateTime now)
    {
        foreach (var cast in casts.Values.Where(c => c.Hit >= now))
        {
            if (PalaceCatalog.Interrupts.Contains(cast.Action)) yield return $"打断 {cast.Action} {cast.Name}";
            if (PalaceCatalog.IsGaze(cast.Action)) yield return "视线判定前背对施法者";
            if (cast.Action == 7103) yield return "特大爆炸：保留灰炸弹，让震撼弹炸晕 Boss";
            if (cast.Action == 7171) yield return "冰爆：尽快击杀 Remedy Bomb";
            if (cast.Action == 7166) yield return "黄道陨石：80% 最大生命伤害，准备恢复并尽快击杀";
        }
        if (ManualBurstHold) yield return "Boss 血量 15%–15.8%：自动攻击暂停，手动准备爆发";
        if (Boss is { Oid: 0x181E } waterBoss && BossInWater(waterBoss)) yield return "把 Boss 拉出水圈";
        if (Boss is { Oid: 0x181F })
        {
            yield return $"180层阶段：{Phase}";
            yield return "180层：南北墙呵斥时靠近 Boss；15% 血量进入黄道陨石";
            if (Boss is { MaxHp: > 0 } behemoth && (float)behemoth.Hp / behemoth.MaxHp is > .15f and < .20f)
                yield return "180层：接近15%门槛，准备手动爆发；15%–15.8%自动禁攻Boss";
            var next = rotationStep switch
            {
                0 => "霹雳 → 漩涡×2 → 霹雳 → 南墙呵斥",
                1 => "漩涡×2 → 霹雳 → 南墙呵斥",
                2 => "漩涡 → 霹雳 → 南墙呵斥",
                3 => "霹雳 → 南墙呵斥",
                4 => "Boss即将前往南墙呵斥",
                5 => "漩涡×2 → 霹雳 → 漩涡 → 北墙呵斥",
                6 => "漩涡 → 霹雳 → 漩涡 → 北墙呵斥",
                7 => "霹雳 → 漩涡 → 北墙呵斥",
                8 => "漩涡 → 北墙呵斥",
                9 => "Boss即将前往北墙呵斥",
                _ => null,
            };
            if (next != null) yield return $"180层循环 {rotationStep + 1}/10：{next}";
        }
        if (spikes.Any(p => p.Value > now)) yield return "反伤生效：暂停攻击反伤目标";
    }

    internal bool HasResponse(uint action, DateTime now)
        => casts.Values.Any(c => c.Action == action && c.Hit.AddSeconds(.2) >= now
            && (c.Shape != null || PalaceCatalog.SpecialShape(action) != null || PalaceCatalog.IsGaze(action)
                || PalaceCatalog.Interrupts.Contains(action) || action is 6365 or 7103 or 7166 or 7171));

    internal void Reset()
    {
        actors.Clear();
        casts.Clear();
        pending.Clear();
        bombs.Clear();
        spikes.Clear();
        statusChanges.Clear();
        observedSpikes.Clear();
        effects.Clear();
        resolvedBombs.Clear();
        rotationStep = 0;
    }
}
