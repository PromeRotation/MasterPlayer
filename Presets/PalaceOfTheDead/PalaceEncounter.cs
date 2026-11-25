using System.Numerics;
using System.Text.Json;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

// Native state model; BMR PalaceFloorModule and DD160..DD190 are the MIT source.
internal sealed partial class PalaceEncounter
{
    private readonly Dictionary<ulong, ReplayActorState> actors = [];
    private readonly Dictionary<ulong, PalaceCast> casts = [];
    private readonly Dictionary<string, PalaceArea> pending = [];
    private readonly Dictionary<ulong, (DateTime Hit, Vector3 Position)> bombs = [];
    private readonly Dictionary<ulong, DateTime> spikes = [];
    private readonly HashSet<(ulong, uint, uint)> effects = [];
    private readonly Dictionary<ulong, (uint Action, DateTime Start)> finishedBossCasts = [];
    private readonly Dictionary<ulong, ReplayActorState> initialWater = [];
    private readonly HashSet<ulong> detachedWater = [];
    private readonly HashSet<ulong> observedBossCasts = [];
    private readonly HashSet<ulong> chargeBaits = [];
    private readonly Dictionary<ulong, ReplayCast> cancelledPlayerCasts = [];
    private readonly List<ulong> lavaBombOrder = [];
    private ulong activeBossId;
    private int rotationStep;
    private int behemothPhase;

    internal IReadOnlyList<ReplayPartyMember> Party { get; set; } = [];
    internal bool ZoneMechanicsEnabled { get; set; } = true;
    internal bool BossMechanicsEnabled { get; set; } = true;
    internal IReadOnlyCollection<ReplayActorState> Actors => actors.Values;
    internal ReplayActorState? Boss => actors.GetValueOrDefault(activeBossId) is { Dead: false } boss ? boss : null;
    internal bool BossActive => Boss != null;
    internal bool ManualBurstHold => Boss is { Oid: 0x181F, MaxHp: > 0 } boss && (float)boss.Hp / boss.MaxHp is > .15f and < .158f;
    internal string Phase => Boss is { Oid: 0x181F } ? behemothPhase switch
    {
        0 => "30%",
        1 => "21%",
        2 => "16%",
        3 => "15%",
        _ => "黄道陨石",
    } : "";

    internal void Synchronize(IEnumerable<ReplayActorState> observations, DateTime now, bool observeCasts = true,
        Func<ReplayActorState, PalaceCast?>? resolveTrash = null)
    {
        var incoming = observations.Where(a => a.Id != 0).ToDictionary(a => a.Id);
        foreach (var id in actors.Keys.Except(incoming.Keys).ToArray()) RemoveActor(id);
        foreach (var (id, actor) in incoming)
        {
            if (actors.TryGetValue(id, out var previousActor) && (previousActor.Oid != actor.Oid
                || previousActor.GameObjectId != 0 && actor.GameObjectId != 0 && previousActor.GameObjectId != actor.GameObjectId
                || previousActor.ObjectIndex != null && actor.ObjectIndex != null && previousActor.ObjectIndex != actor.ObjectIndex))
                RemoveActor(id);
            var previousCast = actors.GetValueOrDefault(id)?.Cast;
            if (previousCast is { ActionId: 7172 } && actor.Cast is { ActionId: 7172 } currentCast
                && (previousCast.TargetId != currentCast.TargetId || previousCast.Location != currentCast.Location
                    || previousCast.Rotation != currentCast.Rotation || previousCast.TotalTime != currentCast.TotalTime))
                Cancel(id, 7172);
            if (!actors.ContainsKey(id) && actor.Oid == 0x18F3 && Boss?.Oid == 0x1820)
                bombs[id] = (now.AddSeconds(10), Quantized(actor.Position));
            if (!actors.ContainsKey(id) && actor.Oid == 0x18F2) lavaBombOrder.Add(id);
            actors[id] = actor;
            if (initialWater.ContainsKey(id) && !detachedWater.Contains(id)) initialWater[id] = actor;
            if (actor.Dead && PalaceCatalog.IsBoss(actor.Oid) && actor.Cast?.ActionId != 7172) Cancel(id);
            SynchronizeTrash(actor, now, observeCasts, resolveTrash);
            ObserveSpikes(actor, now, observeCasts);
        }
        if (activeBossId != 0 && Boss is not { Hp: > 0 }) EndBoss();
        if (BossMechanicsEnabled && activeBossId == 0 && actors.Values.FirstOrDefault(a => PalaceCatalog.IsBoss(a.Oid)
            && !a.Dead && a.Hp > 0 && a.Targetable && a.InCombat) is { } started)
        {
            activeBossId = started.Id;
            if (started.Oid == 0x181E)
                foreach (var puddle in actors.Values.Where(a => a.Oid == 0x1E9998 && a.EventState != 7))
                    initialWater[puddle.Id] = puddle;
            if (started.Oid == 0x1820)
                foreach (var bomb in actors.Values.Where(a => a.Oid == 0x18F3))
                    bombs[bomb.Id] = (now.AddSeconds(10), Quantized(bomb.Position));
        }
        if (Boss is { Oid: 0x181F } behemoth)
        {
            uint[] thresholds = [112063, 78444, 59767, 56031];
            while (behemothPhase < thresholds.Length && behemoth.Hp <= thresholds[behemothPhase])
                ++behemothPhase;
            if (behemothPhase == 4) rotationStep = 11;
        }
        foreach (var actor in incoming.Values.Where(a => a.EventState != 7))
        {
            var key = pending.FirstOrDefault(p => actor.Oid == (p.Value.Action == 7155 ? 0x1E858Eu : 0x1E9998u)
                && Vector2.DistanceSquared(new(actor.Position.X, actor.Position.Z), new(p.Value.Position.X, p.Value.Position.Z)) <= 4).Key;
            if (key != null) pending.Remove(key);
        }
        foreach (var cast in casts.Values.Where(c => observeCasts && PalaceCatalog.IsBossAction(c.Action)).ToArray())
        {
            var observed = actors.GetValueOrDefault(cast.Source)?.Cast;
            var matches = observed != null && observed.ActionId == cast.Action
                && Math.Abs((observed.StartedAt - cast.Start).TotalSeconds) < .25;
            if (matches) observedBossCasts.Add(cast.Source);
            else if (observedBossCasts.Contains(cast.Source) || now >= cast.Start.AddSeconds(.2))
                Cancel(cast.Source, cast.Action);
        }
        if (BossActive && observeCasts)
            foreach (var actor in actors.Values.Where(a => !a.Dead && a.Cast != null && PalaceCatalog.IsBossAction(a.Cast.ActionId)))
            {
                var observed = actor.Cast!;
                if (!casts.ContainsKey(actor.Id))
                    StartCast(new(actor.Id, observed.ActionId, observed.TargetId, observed.Location, actor.Position,
                        observed.Rotation, observed.StartedAt, observed.FinishAt, PalaceCatalog.SpecialShape(observed.ActionId)));
            }
        foreach (var key in spikes.Where(p => p.Value < now).Select(p => p.Key).ToArray()) spikes.Remove(key);
    }

    internal void StartCast(PalaceCast cast)
    {
        if (!BossMechanicsEnabled && PalaceCatalog.IsBossAction(cast.Action)) return;
        if (!actors.TryGetValue(cast.Source, out var actor) || actor.Dead || !actor.Hostile && !PalaceCatalog.IsEnemy(actor.Oid)) return;
        if (!PalaceCatalog.IsBossAction(cast.Action))
        {
            if (trashCasters.TryGetValue(cast.Source, out var source) && source.Start > cast.Start.AddSeconds(.25)) return;
            if (source != null && source.FinishedAction == cast.Action && cast.Start <= source.FinishedStart.AddSeconds(.25)) return;
            StartTrashResponse(cast);
        }
        if (PalaceCatalog.IsBossAction(cast.Action) && finishedBossCasts.TryGetValue(cast.Source, out var finished)
            && finished.Action == cast.Action && cast.Start < finished.Start.AddSeconds(.25)) return;
        if (cast.Action == 7166 && Phase != "黄道陨石") return;
        if (casts.TryGetValue(cast.Source, out var previous) && previous.Action == cast.Action
            && Math.Abs((previous.Start - cast.Start).TotalSeconds) < .25) return;
        observedBossCasts.Remove(cast.Source);
        if (actors[cast.Source].Cast is { } observed && observed.ActionId == cast.Action
            && Math.Abs((observed.StartedAt - cast.Start).TotalSeconds) < .25)
            observedBossCasts.Add(cast.Source);
        if (cast.Action == 7156) cast = cast with { SourcePosition = actor.Position };
        chargeBaits.Remove(cast.Source);
        if (cast.Action == 7156 && actors.ContainsKey(cast.Target)) chargeBaits.Add(cast.Source);
        casts[cast.Source] = cast;
    }

    internal void Apply(LogSystemEvent ev, DateTime now)
    {
        switch (ev)
        {
            case LogSystemActionEffectEvent effect:
                if (!BossMechanicsEnabled) break;
                if (effect.GlobalSequence == 0 && effect.TargetIndex > 0) return;
                if (effect.GlobalSequence != 0 && !effects.Add((effect.SourceId, effect.GlobalSequence, effect.ActionId))) return;
                if (effects.Count > 2048) effects.Remove(effects.First());
                casts.TryGetValue(effect.SourceId, out var cast);
                var origin = actors.GetValueOrDefault(effect.TargetId)?.Position
                    ?? effect.TargetPosition ?? effect.EffectPosition ?? cast?.Position;
                if (effect.ActionId is 7155 or 7158 && origin is { } position)
                {
                    var delay = effect.ActionId == 7155 ? 1.3 : .8;
                    var radius = effect.ActionId == 7155 ? 9 : 8;
                    var key = $"puddle/{effect.SourceId}/{effect.GlobalSequence}/{effect.ActionId}";
                    pending[key] = new(key, effect.ActionId == 7155 ? "愤慨殿" : "浸泡", new CircleTemplate(radius),
                        position, 0, now.AddSeconds(delay), DateTime.MaxValue, effect.SourceId, effect.ActionId, Telegraph: now);
                }
                if (effect.ActionId is 7162 or 7163 or 7165 && behemothPhase < 4)
                {
                    ++rotationStep;
                    if (rotationStep == 10) rotationStep = 0;
                }
                if (effect.ActionId == 7166) rotationStep = 11;
                break;
            case LogSystemCancelCastEvent cancel:
                if (casts.TryGetValue(cancel.SourceId, out var cancelCurrent) && now < cancelCurrent.Start) break;
                CancelPacket(cancel.SourceId, CancelAction(cancel));
                break;
            case LogSystemInterruptCastEvent interrupt:
                if (casts.TryGetValue(interrupt.SourceId, out var interruptCurrent) && now < interruptCurrent.Start) break;
                CancelPacket(interrupt.SourceId, interrupt.ActionId);
                break;
            case LogSystemStatusAddEvent status when status.StatusId is 197 or 198:
                spikes[status.TargetId] = now.AddSeconds(10);
                break;
            case LogSystemStatusRemoveEvent status when status.StatusId is 197 or 198:
                spikes.Remove(status.TargetId);
                break;
        }
    }

    internal void Cancel(ulong source, uint action = 0)
    {
        if (casts.TryGetValue(source, out var cast) && (action == 0 || cast.Action == action))
        {
            casts.Remove(source);
            chargeBaits.Remove(source);
            observedBossCasts.Remove(source);
            if (PalaceCatalog.IsBossAction(cast.Action)) finishedBossCasts[source] = (cast.Action, cast.Start);
            if (cast.Action == 7172) bombs.Clear();
        }
        if (trashCasters.TryGetValue(source, out var trash) && (action == 0 || trash.Action == action))
        {
            ClearTrashCast(trash);
        }
    }

    private void CancelPacket(ulong source, uint action)
    {
        // Zone and generic hints consume raw CastInfo diffs. A cancellation
        // log alone cannot finish an already observed non-null NPC cast.
        if (trashCasters.GetValueOrDefault(source) is { Observed: true, Cast: not null }
            && !PalaceCatalog.IsBossAction(action)) return;
        Cancel(source, action);
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
        // Upstream ActorState emits CastFinished before destroying an actor.
        Cancel(id);
        trashCasters.Remove(id);
        observedBossCasts.Remove(id);
        if (initialWater.ContainsKey(id)) detachedWater.Add(id);
        lavaBombOrder.Remove(id);
        if (id == activeBossId) EndBoss();
        spikes.Remove(id);
        spikeStatusSlots.Remove(id);
        cancelledPlayerCasts.Remove(id);
        finishedBossCasts.Remove(id);
    }

    internal IReadOnlyList<PalaceArea> Collect(DateTime now, ulong playerId)
    {
        PruneZoneResponses();
        var result = new List<PalaceArea>();
        foreach (var actor in actors.Values)
        {
            if (actor.OpenedTreasure) continue;
            if (actor.Oid is >= 0x1EA08E and <= 0x1EA092 or 0x1EA9A0 or 0x1EB864 or 0x1EBEDB)
                result.Add(new($"trap/{actor.Id}", "已揭示陷阱", new CircleTemplate(2), actor.Position, 0, now, DateTime.MaxValue, actor.Id));
            var radius = actor.Oid switch { 0x1E858E => 9, 0x1E9998 => 8, 0x18F0 => 10, _ => 0 };
            if (BossMechanicsEnabled && radius > 0 && (actor.Oid == 0x18F0 || actor.EventState != 7))
                result.Add(new($"object/{actor.Id}", "持续地面", new CircleTemplate(radius), Quantized(actor.Position),
                0, now, DateTime.MaxValue, actor.Id));
        }
        foreach (var area in pending.Values.Where(a => a.Until >= now))
        {
            result.Add(area);
        }
        foreach (var (id, prediction) in bombs)
        {
            var hit = prediction.Hit;
            result.Add(new($"bomb/{id}", "冰碎预告", new CircleTemplate(6.6f), prediction.Position, 0,
                hit, DateTime.MaxValue, id, 7172, Telegraph: hit.AddSeconds(-10)));
        }
        foreach (var cast in casts.Values)
        {
            if (BossActive && !PalaceCatalog.IsBossAction(cast.Action)) continue;
            if (cast.Action is 7103 or 7166 or 7171 or 7172 || PalaceCatalog.IsGaze(cast.Action) || cast.GenericGaze) continue;
            if (ZoneMechanicsEnabled && !BossActive && !PalaceCatalog.IsBossAction(cast.Action)
                && trashCasters.TryGetValue(cast.Source, out var disabled)
                && cast.Charge && trashHintDisabled.Contains(disabled)) continue;
            if (cast.Action is 7155 or 7158)
            {
                var observed = ObservedCast(cast);
                var target = observed?.TargetId ?? cast.Target;
                var location = observed?.Location ?? cast.Position;
                result.Add(Hit(cast, new CircleTemplate(cast.Action == 7155 ? 9 : 8),
                    actors.GetValueOrDefault(target)?.Position ?? location, 0));
                continue;
            }
            var shape = PalaceCatalog.SpecialShape(cast.Action) ?? cast.Shape;
            if (shape == null) continue;
            if (shape is FanTemplate or RayRectangleTemplate or RectangleTemplate && cast.Rotation == null) continue;
            var position = cast.Position;
            var rotation = cast.Rotation ?? 0;
            if (!PalaceCatalog.IsBossAction(cast.Action) && trashCasters.GetValueOrDefault(cast.Source) is { Cast: { } raw } dynamicSource)
            {
                position = raw.Location;
                rotation = raw.Rotation ?? rotation;
                if (cast.Charge && shape is RayRectangleTemplate rect)
                {
                    position = Quantized(dynamicSource.Actor.Position);
                    var delta = raw.Location - position;
                    delta.Y = 0;
                    // The source's zero-length SDRect has no finite signed distance.
                    // Keep the cast and resolve it once endpoints separate.
                    if (delta.LengthSquared() == 0) continue;
                    shape = RayRectangleTemplate.Symmetric(rect.MaxLocalX - rect.MinLocalX, delta.Length());
                    rotation = MathF.Atan2(delta.X, delta.Z);
                }
            }
            if (cast.Action == 7156) continue;
            var area = Hit(cast, shape, position, rotation);
            if (!PalaceCatalog.IsBossAction(cast.Action) && trashCasters.TryGetValue(cast.Source, out var source))
                area = area with { From = TrashActivation(source, now), Until = DateTime.MaxValue };
            result.Add(area);
        }
        if (Boss is { } boss && actors.TryGetValue(playerId, out var player))
        {
            Cleave(result, boss, player, now);
            foreach (var cast in casts.Values.Where(c => c.Action == 7156))
            {
                if (!actors.TryGetValue(cast.Target, out var target) || !actors.TryGetValue(cast.Source, out var source)) continue;
                var sourcePosition = source.Position;
                var delta = target.Position - sourcePosition;
                delta.Y = 0;
                var length = delta.Length();
                if (length > 0 && chargeBaits.Contains(cast.Source))
                    result.Add(Hit(cast, RayRectangleTemplate.Symmetric(6, length), sourcePosition, MathF.Atan2(delta.X, delta.Z))
                        with { PreviewOnly = cast.Target == playerId, OutlineOnly = cast.Target == playerId });
                if (cast.Target == playerId && chargeBaits.Contains(cast.Source))
                    foreach (var ally in PartyActors().Where(a => a.Id != playerId))
                    {
                        var offset = ally.Position - sourcePosition;
                        offset.Y = 0;
                        var distance = offset.Length();
                        var halfAngle = distance >= 3 ? MathF.Asin(3 / distance) : MathF.PI;
                        result.Add(new($"charge-bait/{cast.Source}/{ally.Id}", "冲锋远离队友", new FanTemplate(100, halfAngle * 2),
                            sourcePosition, MathF.Atan2(offset.X, offset.Z), cast.Hit, DateTime.MaxValue,
                            cast.Source, cast.Action, NavigationOnly: true));
                    }
                if (cast.Target == playerId && !player.Statuses.Any(s => s.Id is 3054 or 160 or 1209 or 1722 or 2663 or 2345 && s.ExpiresAt > cast.Hit))
                {
                    result.Add(Hit(cast, new PalaceLandingTemplate(cast.SourcePosition, KnockbackDangers(), player.HitboxRadius),
                        PalaceArena.Center, 0)
                        with { Key = $"landing/{cast.Source}", NavigationOnly = true });
                }
                if (cast.Target == playerId) AddKnockbackDirections(result, cast, boss);
            }
            if (boss.Oid == 0x181E && BossInWater(boss))
            {
                result.Add(new($"puddle-boss/{boss.Id}", "泡水首领轮廓", new CircleTemplate(boss.HitboxRadius), boss.Position,
                    0, now, now.AddSeconds(.5), boss.Id, PreviewOnly: true, OutlineOnly: true));
                if (boss.TargetId == playerId) AddPullGoal(result, boss, now);
            }
        }
        AddActorMarkers(result, now);
        if (actors.GetValueOrDefault(playerId) is { } gazeViewer)
            result.AddRange(TrashGazeMarkers(now, gazeViewer));
        return result;
    }

    private static PalaceArea Hit(PalaceCast cast, GeometryTemplate shape, Vector3 position, float rotation)
        => new($"cast/{cast.Source}/{cast.Action}", string.IsNullOrWhiteSpace(cast.Name) ? $"技能 {cast.Action}" : cast.Name,
            shape, position, rotation, PalaceCatalog.IsBossAction(cast.Action) ? cast.Hit : cast.Hit.AddSeconds(-.2),
            PalaceCatalog.IsBossAction(cast.Action) ? DateTime.MaxValue : cast.Hit.AddSeconds(.2), cast.Source, cast.Action, Telegraph: cast.Start);

    private void Cleave(List<PalaceArea> result, ReplayActorState boss, ReplayActorState player, DateTime now)
    {
        var spec = boss.Oid switch { 0x181D => (11.92f, 90f), 0x181E => (15.75f, 90f), 0x1820 => (11.75f, 120f), _ => (0f, 0f) };
        if (spec.Item1 == 0 || !boss.Targetable || boss.Cast != null || casts.ContainsKey(boss.Id) || !actors.TryGetValue(boss.TargetId, out var target)) return;
        var delta = target.Position - boss.Position;
        var rotation = MathF.Atan2(delta.X, delta.Z);
        result.Add(new($"cleave/{boss.Id}", "无读条顺劈", new FanTemplate(spec.Item1, spec.Item2 * MathF.PI / 180),
            Quantized(boss.Position), rotation, now, now.AddSeconds(.3), boss.Id, PreviewOnly: true, OutlineOnly: true));
        if (boss.TargetId != player.Id)
            result.Add(new($"cleave-nav/{boss.Id}", "无读条顺劈", new FanTemplate(spec.Item1, spec.Item2 * MathF.PI / 180),
                Quantized(boss.Position), rotation, now, now.AddSeconds(.3), boss.Id, NavigationOnly: true));
        if (boss.TargetId == player.Id)
            foreach (var ally in PartyActors().Where(a => a.Id != player.Id))
            {
                var origin = Quantized(boss.Position);
                var heading = MathF.Atan2(ally.Position.X - boss.Position.X, ally.Position.Z - boss.Position.Z);
                result.Add(new($"cleave-bait/{boss.Id}/{ally.Id}", "顺劈远离队友", new FanTemplate(100, spec.Item2 * MathF.PI / 180),
                    origin, heading, now, now.AddSeconds(.3), boss.Id, NavigationOnly: true));
            }
    }

    private bool BossInWater(ReplayActorState boss)
        => initialWater.Values.Any(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(boss.Position.X, boss.Position.Z))
            <= MathF.Pow(8 + boss.HitboxRadius, 2));

    private void AddPullGoal(List<PalaceArea> result, ReplayActorState boss, DateTime now)
    {
        var puddles = initialWater.Values.ToArray();
        var radius = 8 + 2 * boss.HitboxRadius + 3;
        result.Add(new("pull/170", "把 Boss 拉出水圈", new PalacePullTemplate(puddles.Select(p => p.Position).ToArray(), radius),
            PalaceArena.Center, 0, now, now.AddSeconds(.5), Safe: true, NavigationOnly: true, OverrideDangerMargin: false));
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
        var blocksFacing = gazes.Any(gaze =>
            MathF.Abs(MathF.Atan2(MathF.Sin(player.Rotation - gaze.Direction),
                MathF.Cos(player.Rotation - gaze.Direction))) <= MathF.PI / 4 + .01f);
        return (blocksFacing, cancel);
    }

    internal bool TakeCastCancellation(ulong playerId)
    {
        if (actors.GetValueOrDefault(playerId)?.Cast is not { } current) return false;
        if (cancelledPlayerCasts.GetValueOrDefault(playerId) is { } previous
            && previous.ActionId == current.ActionId && previous.TargetId == current.TargetId
            && Math.Abs((previous.StartedAt - current.StartedAt).TotalSeconds) < .25) return false;
        cancelledPlayerCasts[playerId] = current;
        return true;
    }

    private IEnumerable<(float Direction, DateTime Hit)> ActiveGazes(DateTime now, ReplayActorState player)
    {
        foreach (var (gaze, origin, hit) in TrashEyes(now, player))
        {
            if (hit > now.AddSeconds(.75)) continue;
            yield return (MathF.Atan2(origin.X - player.Position.X, origin.Z - player.Position.Z), hit);
        }
    }

    internal IReadOnlyList<ulong> ForbiddenTargets(DateTime now)
    {
        var forbidden = new HashSet<ulong>();
        if (ManualBurstHold && Boss is { } boss) forbidden.Add(boss.Id);
        if (Boss is { Oid: 0x1820 } godfather && lavaBombOrder.FirstOrDefault() is var first
            && actors.GetValueOrDefault(first) is { } bomb
            && Vector2.DistanceSquared(new(bomb.Position.X, bomb.Position.Z), new(godfather.Position.X, godfather.Position.Z)) <= 7.2f * 7.2f)
            forbidden.Add(bomb.Id);
        return forbidden.ToArray();
    }

    internal IReadOnlyList<ulong> ForbiddenAutoAttackTargets(DateTime now)
        => ZoneMechanicsEnabled && !BossActive ? spikes.Where(p => p.Value > now).Select(p => p.Key).ToArray() : [];

    internal IEnumerable<string> Hints(DateTime now, ulong playerId = 0)
    {
        PruneZoneResponses();
        foreach (var cast in casts.Values)
        {
            var finish = ObservedCast(cast)?.FinishAt ?? cast.Hit;
            if (cast.Action == 7103) yield return $"Enrage! Stun boss with the Lavabomb! {Math.Max(0, (finish - now).TotalSeconds):f1}s left";
            if (cast.Action == 7171) yield return $"Remedy bomb is enraging! {Math.Max(0, (finish - now).TotalSeconds):f1}s left";
            if (cast.Action == 7166) yield return "黄道陨石：80% 最大生命伤害，准备恢复并尽快击杀";
        }
        if (ZoneMechanicsEnabled && !BossActive)
            foreach (var source in trashInterrupts)
                yield return $"打断 {source.Action} {casts.GetValueOrDefault(source.Actor.Id)?.Name}";
        if (actors.GetValueOrDefault(playerId) is { } gazePlayer && TrashEyes(now, gazePlayer).Any())
            yield return "视线判定前背对施法者";
        if (ZoneMechanicsEnabled && !BossActive && actors.Values.Any(a => a.Oid == 0x1842 && a.InCombat && a.TargetId == playerId))
            yield return "狂暴怪正在攻击当前角色：优先处理（源优先级10）";
        if (ManualBurstHold) yield return "Boss 血量 15%–15.8%：自动攻击暂停，手动准备爆发";
        if (Boss is { Oid: 0x181E } waterBoss && waterBoss.TargetId == playerId && BossInWater(waterBoss))
            yield return "把 Boss 拉出水圈";
        if (Boss is { Oid: 0x181F })
        {
            yield return $"180层阶段：{Phase}";
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
            if (behemothPhase < 4 && next != null) yield return $"180层循环 {rotationStep + 1}/10：{next}";
        }
        else if (!BossActive && actors.Values.Any(a => a.Oid == 0x181F && !a.Dead && a.Hp > 0))
            yield return "180层：南北墙呵斥时靠近 Boss；15% 血量进入黄道陨石";
        if (actors.GetValueOrDefault(playerId) is { } player)
        {
            var areas = Collect(now, playerId);
            if (areas.Any(a => a.Key.StartsWith("charge-bait/") && a.Geometry.Contains(player.Position)))
                yield return "冲锋远离队友";
            if (areas.Any(a => !a.PreviewOnly && !a.NavigationOnly && !a.Safe && a.Geometry.Contains(player.Position)))
                yield return "离开危险区域";
            if (areas.Any(a => a.Key.StartsWith("cleave-nav/") && a.Geometry.Contains(player.Position)))
                yield return "离开顺劈范围";
            if (Knockbacks(player).Any(k => k.Unsafe)) yield return "即将被击退至危险区域";
        }
        if (spikes.Any(p => p.Value > now)) yield return "反伤生效：暂停对反伤目标的自动攻击";
    }

    internal bool HasResponse(uint action, DateTime now)
        => casts.Values.Any(c => c.Action == action
            && (c.Shape != null || PalaceCatalog.SpecialShape(action) != null || PalaceCatalog.IsGaze(action)
                || PalaceCatalog.Interrupts.Contains(action) || action is 6365 or 7103 or 7166 or 7171));

    internal void Reset()
    {
        EndBoss();
        actors.Clear();
        casts.Clear();
        pending.Clear();
        bombs.Clear();
        spikes.Clear();
        spikeStatusSlots.Clear();
        effects.Clear();
        finishedBossCasts.Clear();
        observedBossCasts.Clear();
        cancelledPlayerCasts.Clear();
        lavaBombOrder.Clear();
        trashCasters.Clear();
        trashGazes.Clear();
        genericGazes.Clear();
        trashInterrupts.Clear();
        trashHintDisabled.Clear();
        Party = [];
        rotationStep = 0;
    }

    private static Vector3 Quantized(Vector3 position)
    {
        const float grid = 2000f / 65535f;
        return new(((int)MathF.Round(position.X / grid) - .5f) * grid, position.Y,
            ((int)MathF.Round(position.Z / grid) - .5f) * grid);
    }

    private void EndBoss()
    {
        activeBossId = 0;
        initialWater.Clear();
        detachedWater.Clear();
        pending.Clear();
        bombs.Clear();
        rotationStep = 0;
        behemothPhase = 0;
        chargeBaits.Clear();
        foreach (var source in casts.Where(c => PalaceCatalog.IsBossAction(c.Value.Action)).Select(c => c.Key).ToArray())
            casts.Remove(source);
    }
}
