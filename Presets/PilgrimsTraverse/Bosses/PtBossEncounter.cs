using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

// Mechanic sequences are ported from BMR's PilgrimsTraverse modules (MIT).
internal abstract class PtBossEncounter(int floor, uint bossOid)
{
    internal sealed record Cast(ulong Source, uint Action, Vector3 Position, float? Rotation, DateTime Start, DateTime Hit, ulong Target, Vector3? SourcePosition = null);
    internal sealed record Area(string Key, string Name, GeometryTemplate Shape, Vector3 Position, float Rotation,
        DateTime From, DateTime Until, ulong Owner = 0, uint Action = 0, bool Safe = false,
        bool Obstacle = false, bool NavigationOnly = false, bool PreviewOnly = false, int Priority = 5, bool OutlineOnly = false,
        Vector4? Color = null)
    {
        public IGeometryShape Geometry => Shape.Resolve(Position, Rotation, null);
        public DangerWindow Window => new(From, Until == DateTime.MaxValue ? null : Until, From.AddSeconds(-5)) { HasExplicitTiming = true };
    }

    protected readonly Dictionary<string, Area> areas = [];
    protected readonly Dictionary<ulong, ReplayActorState> actors = [];
    private readonly Dictionary<(ulong, uint), ReplayStatus> statuses = [];
    private readonly Dictionary<(ulong, uint), DateTime> statusEvents = [];
    private readonly Dictionary<(ulong, uint), DateTime> casts = [];
    private readonly HashSet<(ulong, uint, uint)> effects = [];
    public int Floor { get; } = floor;
    public uint BossOid { get; } = bossOid;
    public bool Active { get; private set; }
    public virtual bool Preparing => false;
    public abstract uint[] Actions { get; }
    public Vector3 Center => new(Floor is 40 or 60 or 80 or 99 ? -600 : -300, 0, -300);
    public virtual IGeometryShape Boundary => PtArenaData.Boundary(Floor);
    public virtual bool HoldMovement(DateTime now, ulong player) => false;
    public virtual IEnumerable<string> Hints(DateTime now, ulong player) => [];
    public virtual IEnumerable<(string Key, Vector3 Start, Vector3 End)> DrawingLines(DateTime now) => [];
    public virtual IEnumerable<PtKnockbackTemplate> KnockbackSources(DateTime now, ulong player) => [];
    public virtual IReadOnlyList<ulong> ForbiddenDotTargets => [];
    protected virtual int TargetPriority(ReplayActorState actor) => actor.Oid == BossOid ? 0 : -1;
    public virtual ReplayActorState? SelectTarget(DateTime now, ulong player, ulong currentTarget)
    {
        if (!Active || !actors.TryGetValue(player, out var pc) || pc.Dead || !Boundary.Contains(pc.Position)) return null;
        return actors.Values.Where(a => !a.Dead && a.Targetable && a.Hp > 1 && TargetPriority(a) >= 0)
            .OrderByDescending(TargetPriority).ThenByDescending(a => a.Id == currentTarget)
            .ThenBy(a => Vector3.DistanceSquared(pc.Position, a.Position)).ThenBy(a => a.Id).FirstOrDefault();
    }
    protected ReplayActorState? Boss => actors.Values.FirstOrDefault(a => a.Oid == BossOid);

    public void Synchronize(IEnumerable<ReplayActorState> observations, DateTime now)
    {
        var incoming = observations.ToDictionary(a => a.Id);
        var boss = incoming.Values.FirstOrDefault(a => a.Oid == BossOid && !a.Dead);
        var active = boss?.InCombat == true;
        if (Active && !active) Reset();
        Active = active;
        actors.Clear();
        foreach (var (id, actor) in incoming) actors[id] = actor;
        if (!Active && !Preparing) return;
        foreach (var actor in incoming.Values)
        {
            foreach (var status in actor.Statuses)
                if (!statusEvents.TryGetValue((actor.Id, status.Id), out var changed) || now - changed > TimeSpan.FromSeconds(.5))
                    SetStatus(actor.Id, status, now);
            foreach (var key in statuses.Keys.Where(k => k.Item1 == actor.Id && !actor.Statuses.Any(s => s.Id == k.Item2)).ToArray())
                if (!statusEvents.TryGetValue(key, out var changed) || now - changed > TimeSpan.FromSeconds(.5))
                    RemoveStatus(key.Item1, key.Item2, now);
        }
        SynchronizeActors(now);
    }

    // Only cast-start events enter the sequence machine; snapshots never replay casts.
    public void StartCast(Cast cast)
    {
        if (!Actions.Contains(cast.Action) || !float.IsFinite(cast.Position.X) || !float.IsFinite(cast.Position.Z)) return;
        var key = (cast.Source, cast.Action);
        // Repeated observations can drift slightly; PT80's distinct jumps are only 0.4s apart.
        if (casts.TryGetValue(key, out var hit) && Math.Abs((cast.Hit - hit).TotalSeconds) < .1) return;
        casts[key] = cast.Hit;
        OnCast(cast);
    }

    public void Apply(LogSystemEvent ev, DateTime now)
    {
        switch (ev)
        {
            case LogSystemActionEffectEvent effect when Actions.Contains(effect.ActionId):
                if (effect.GlobalSequence == 0 && effect.TargetIndex > 0) break;
                if (effect.GlobalSequence != 0 && !effects.Add((effect.SourceId, effect.GlobalSequence, effect.ActionId))) break;
                if (effects.Count > 1024) effects.Remove(effects.First());
                var resolved = areas.Values.Where(a => a.Key.StartsWith("cast/", StringComparison.Ordinal)
                    && a.Owner == effect.SourceId && a.Action == effect.ActionId).MinBy(a => a.From);
                if (resolved != null) areas.Remove(resolved.Key);
                OnEffect(effect, now);
                break;
            case LogSystemTargetIconEvent icon:
                OnIcon(icon.TargetId is 0 or 0xE0000000 ? icon.SourceId : icon.TargetId, icon.MarkerId, now);
                break;
            case LogSystemMapEffectEvent map:
                OnMap(map.Position, (uint)map.Param1 | ((uint)map.Param2 << 16), now);
                break;
            case LogSystemStatusAddEvent status:
                statusEvents[(status.TargetId, status.StatusId)] = now;
                SetStatus(status.TargetId, new(status.StatusId, status.Param, status.SourceId,
                    ReplayStatus.Expiration(now, status.Duration)), now);
                break;
            case LogSystemStatusRemoveEvent status:
                statusEvents[(status.TargetId, status.StatusId)] = now;
                RemoveStatus(status.TargetId, status.StatusId, now);
                break;
            case LogSystemCancelCastEvent cancel:
                Cancel(cancel.SourceId);
                break;
            case LogSystemInterruptCastEvent interrupt:
                Cancel(interrupt.SourceId);
                break;
        }
    }

    private void SetStatus(ulong actor, ReplayStatus status, DateTime now)
    {
        var key = (actor, status.Id);
        var changed = !statuses.TryGetValue(key, out var old) || old.Param != status.Param;
        statuses[key] = status;
        if (changed) OnStatus(actor, status, true, now);
    }

    private void RemoveStatus(ulong actor, uint id, DateTime now)
    {
        if (statuses.Remove((actor, id), out var old)) OnStatus(actor, old, false, now);
    }

    protected ReplayStatus? Status(ulong actor, uint id, DateTime now)
        => statuses.TryGetValue((actor, id), out var status) && status.ExpiresAt > now ? status : null;
    protected virtual void OnStatus(ulong actor, ReplayStatus status, bool gained, DateTime now) { }
    protected virtual void SynchronizeActors(DateTime now) { }
    protected abstract void OnCast(Cast cast);
    protected virtual void OnEffect(LogSystemActionEffectEvent effect, DateTime now) { }
    protected virtual void OnIcon(ulong target, uint icon, DateTime now) { }
    protected virtual void OnMap(uint index, uint state, DateTime now) { }
    protected virtual void AddPersonalAreas(List<Area> result, DateTime now, ulong player) { }

    public virtual IReadOnlyList<Area> Collect(DateTime now, ulong player, float safetyMargin = .5f)
    {
        // Expired sequence entries remain until their effect consumes them. A late
        // effect must never accidentally consume the following predicted hit.
        var result = areas.Values.Where(a => a.Until >= now).ToList();
        AddPersonalAreas(result, now, player);
        return result;
    }

    protected static void PreviewNextWave(List<Area> result, Func<Area, bool> belongs, int count = 1)
    {
        var visible = result.Where(belongs).OrderBy(a => a.From).Take(count).Select(a => a.Key).ToHashSet();
        for (var i = 0; i < result.Count; ++i)
            if (belongs(result[i]) && !visible.Contains(result[i].Key))
                result[i] = result[i] with { NavigationOnly = true };
    }

    public virtual void Cancel(ulong source)
    {
        foreach (var key in areas.Where(p => p.Value.Owner == source).Select(p => p.Key).ToArray()) areas.Remove(key);
    }

    protected void Hit(Cast cast, GeometryTemplate shape, string name, float extra = 0, float rotationOffset = 0)
    {
        if (shape is FanTemplate or RayRectangleTemplate or RectangleTemplate && cast.Rotation == null) return;
        AddHit(CastKey(cast.Source, cast.Action) + $"/{cast.Hit.Ticks}", name, shape, cast.Position, (cast.Rotation ?? 0) + rotationOffset,
            cast.Hit.AddSeconds(extra), cast.Source, cast.Action);
    }

    protected void AddHit(string key, string name, GeometryTemplate shape, Vector3 position, float rotation,
        DateTime hit, ulong owner = 0, uint action = 0, bool navigationOnly = false)
        => areas[key] = new(key, name, shape, position, rotation, hit.AddSeconds(-.2), hit.AddSeconds(.15), owner, action, NavigationOnly: navigationOnly);

    protected void RemoveGroup(string prefix)
    {
        foreach (var key in areas.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) areas.Remove(key);
    }

    protected void ResolveFirst(string prefix)
    {
        var first = areas.Values.Where(a => a.Key.StartsWith(prefix, StringComparison.Ordinal)).MinBy(a => a.From);
        if (first != null) areas.Remove(first.Key);
    }

    protected void Knockback(Cast cast, float distance, Vector3? direction = null)
    {
        var shape = new PtKnockbackTemplate(Boundary, cast.Position, distance, direction);
        AddHit(CastKey(cast.Source, cast.Action) + $"/{cast.Hit.Ticks}", "击退落点", shape, Center, 0, cast.Hit, cast.Source, cast.Action, true);
    }

    protected bool KnockbackImmune(ulong player, DateTime hit)
        => Status(player, 160, hit) != null || Status(player, 1209, hit) != null || Status(player, 2663, hit) != null;
    protected static string CastKey(ulong source, uint action) => $"cast/{source}/{action}";
    protected static float Rad(float degrees) => degrees * MathF.PI / 180;
    protected static Vector3 Direction(float rotation) => new(MathF.Sin(rotation), 0, MathF.Cos(rotation));
    protected virtual void ResetMechanics() { }

    public void Reset()
    {
        Active = false;
        areas.Clear(); actors.Clear(); statuses.Clear(); statusEvents.Clear(); casts.Clear(); effects.Clear();
        ResetMechanics();
    }
}
