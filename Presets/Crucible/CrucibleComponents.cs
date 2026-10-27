using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible.Components;

internal class GenericAOEs(BossModule module, uint aid = 0, string warningText = "离开危险范围！") : BossComponent(module)
{
    public uint WatchedAction { get; } = aid;
    public int NumCasts;
    public string WarningText { get; } = warningText;
    public struct AOEInstance(AOEShape shape, WPos origin, Angle rotation = default, DateTime activation = default, uint color = Colors.AOE, bool risky = true, ulong actorID = 0, ShapeDistance? shapeDistance = null)
    {
        public AOEShape Shape = shape;
        public WPos Origin = origin;
        public Angle Rotation = rotation;
        public DateTime Activation = activation;
        public uint Color = color;
        public bool Risky = risky;
        public ulong ActorID = actorID;
        public ShapeDistance? ShapeDistance = shapeDistance;
        public bool Check(WPos p) => (ShapeDistance ?? Shape.Distance(Origin, Rotation)).Distance(p) <= 0;
    }
    public virtual ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => [];
    public override void OnEventCast(Actor actor, ActorCastEvent spell) { if (WatchedAction != 0 && spell.Action.ID == WatchedAction) ++NumCasts; }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor)) if (aoe.Risky) hints.AddForbiddenZone(aoe.ShapeDistance ?? aoe.Shape.Distance(aoe.Origin, aoe.Rotation), aoe.Activation);
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor)) if (aoe.Risky && aoe.Check(actor.Position)) { hints.Add(WarningText); break; }
    }
}

internal class Voidzone(BossModule module, float radius, Func<BossModule, IEnumerable<Actor>> sources, float moveHintLength = 0) : GenericAOEs(module)
{
    public readonly AOEShapeCircle Shape = new(radius);
    public readonly Func<BossModule, IEnumerable<Actor>> Sources = sources;
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => Sources(Module).Select(a => new AOEInstance(Shape, a.Position, a.Rotation, actorID: a.InstanceID)).ToArray();
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor)) hints.AddForbiddenZone(aoe.Shape, aoe.Origin, aoe.Rotation, aoe.Activation);
        if (moveHintLength > 0)
            foreach (var source in Sources(Module))
                hints.AddForbiddenZone(new SDCapsule(source.Position, source.Rotation.ToDirection(), moveHintLength, radius), WorldState.FutureTime(1.1));
    }
}

internal class CastHints(BossModule module, uint[] actions, string text) : BossComponent(module)
{
    private readonly Dictionary<ulong, DateTime> _casts = [];
    public override void OnCastStarted(Actor actor, ActorCastInfo spell) { if (actions.Contains(spell.Action.ID)) _casts[actor.InstanceID] = spell.FinishAt; }
    public override void OnCastFinished(Actor actor, ActorCastInfo spell) { if (actions.Contains(spell.Action.ID)) _casts.Remove(actor.InstanceID); }
    public override void OnActorDestroyed(Actor actor) => _casts.Remove(actor.InstanceID);
    public override void AddGlobalHints(GlobalHints hints) { if (_casts.Values.Any(t => t.AddSeconds(0.5) >= WorldState.CurrentTime)) hints.Add(text); }
}

internal class GenericBaitAway(BossModule module, uint aid = 0, bool centerAtTarget = false) : GenericAOEs(module, aid)
{
    public struct Bait(Actor source, Actor target, AOEShape shape, DateTime activation = default)
    {
        public Actor Source = source;
        public Actor Target = target;
        public AOEShape Shape = shape;
        public DateTime Activation = activation;
        public Angle Rotation => Angle.FromDirection(Target.Position - Source.Position);
    }
    public readonly List<Bait> CurrentBaits = [];
    public IEnumerable<Bait> ActiveBaits => CurrentBaits.Where(b => !b.Source.IsDeadOrDestroyed && !b.Target.IsDeadOrDestroyed);
    public bool IsBaitTarget(Actor actor) => ActiveBaits.Any(b => b.Target == actor);
    public WPos BaitOrigin(ref Bait bait) => centerAtTarget ? bait.Target.Position : bait.Source.Position;
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => ActiveBaits.Select(b => new AOEInstance(b.Shape, centerAtTarget ? b.Target.Position : b.Source.Position, b.Rotation, b.Activation, risky: b.Target != actor, actorID: b.Source.InstanceID)).ToArray();
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (IsBaitTarget(actor)) hints.Add("被点名：引导攻击，避免波及其他人", false);
        else base.AddHints(slot, actor, hints);
    }
    public override void OnActorDestroyed(Actor actor) => CurrentBaits.RemoveAll(b => b.Source == actor || b.Target == actor);
    public override void OnEventCast(Actor actor, ActorCastEvent spell)
    {
        base.OnEventCast(actor, spell);
        if (WatchedAction != 0 && spell.Action.ID == WatchedAction) CurrentBaits.RemoveAll(b => b.Source == actor);
    }
}
internal class BaitAwayCast(BossModule module, uint aid, AOEShape shape, bool endsOnCastEvent = false, bool tankbuster = false) : GenericBaitAway(module, aid)
{
    public override void Update() => CurrentBaits.RemoveAll(b => b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed || b.Activation.AddSeconds(1) < WorldState.CurrentTime);
    public override void OnCastStarted(Actor actor, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && WorldState.Actors.Find(spell.TargetID) is { } target)
        {
            CurrentBaits.RemoveAll(b => b.Source == actor);
            CurrentBaits.Add(new(actor, target, shape, spell.FinishAt));
        }
    }
    public override void OnCastFinished(Actor actor, ActorCastInfo spell) { if (spell.Action.ID == WatchedAction && (!endsOnCastEvent || spell.Interrupted)) CurrentBaits.RemoveAll(b => b.Source == actor); }
    public override void AddHints(int slot, Actor actor, TextHints hints) { if (tankbuster && IsBaitTarget(actor)) hints.Add("死刑点名：准备减伤", false); else base.AddHints(slot, actor, hints); }
}

internal class GenericKnockback(BossModule module, uint aid = 0) : BossComponent(module)
{
    public readonly uint WatchedAction = aid;
    public int NumCasts;
    public enum Kind { AwayFromOrigin, TowardsOrigin, DirForward, DirLeft, DirRight }
    public readonly struct Knockback(WPos origin, float distance, DateTime activation, AOEShape? shape = null, Angle direction = default, Kind kind = Kind.AwayFromOrigin, float minDistance = 0, bool ignoreImmunes = false, ulong actorID = 0)
    {
        public readonly WPos Origin = origin;
        public readonly float Distance = distance;
        public readonly DateTime Activation = activation;
        public readonly AOEShape? Shape = shape;
        public readonly Angle Direction = direction;
        public readonly Kind Kind = kind;
        public readonly float MinDistance = minDistance;
        public readonly bool IgnoreImmunes = ignoreImmunes;
        public readonly ulong ActorID = actorID;
    }
    public virtual ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) => [];
    public virtual bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !Arena.InBounds(pos);
    public bool IsImmune(int slot, DateTime activation) => WorldState.Player?.Statuses.Any(s => s.ID is 160 or 1209 or 3054 or 1722 or 2663 or 2345 && s.ExpireAt > activation) == true;
    public static WPos AwayFromSource(WPos p, WPos source, float distance) => p + (p - source).Normalized() * distance;
    public override void OnEventCast(Actor actor, ActorCastEvent spell) { if (spell.Action.ID == WatchedAction) ++NumCasts; }
    public override void AddHints(int slot, Actor actor, TextHints hints) { if (ActiveKnockbacks(slot, actor).Length > 0) hints.Add("即将击退／吸引：注意落点", false); }
    protected virtual bool LandingUnsafe(int slot, Actor actor, WPos landing, DateTime activation)
    {
        if (DestinationUnsafe(slot, actor, landing)) return true;
        foreach (var component in Module.Components.OfType<GenericAOEs>())
            foreach (var a in component.ActiveAOEs(slot, actor))
                if (a.Risky && a.Check(landing)
                    && (a.Activation == default || a.Activation >= activation.AddSeconds(-0.5) && a.Activation <= activation.AddSeconds(0.6)))
                    return true;
        return false;
    }

    public override void DrawArenaForeground(int slot, Actor actor)
    {
        foreach (var kb in ActiveKnockbacks(slot, actor))
        {
            if ((!kb.IgnoreImmunes && IsImmune(slot, kb.Activation)) || kb.Shape != null && !kb.Shape.Check(actor.Position, kb.Origin, kb.Direction)) continue;
            var radial = actor.Position - kb.Origin;
            if (kb.Kind is Kind.AwayFromOrigin or Kind.TowardsOrigin && radial.LengthSq() < 0.0001f)
            {
                Arena.ZoneCircleOutline(actor.Position, 0.6f, Colors.Danger);
                continue;
            }
            var distance = kb.Kind == Kind.TowardsOrigin ? Math.Min(kb.Distance, Math.Max(0, radial.Length() - kb.MinDistance)) : kb.Distance;
            var direction = kb.Kind switch
            {
                Kind.TowardsOrigin => -radial.Normalized(),
                Kind.DirForward => kb.Direction.ToDirection(),
                Kind.DirLeft => -kb.Direction.ToDirection().OrthoR(),
                Kind.DirRight => kb.Direction.ToDirection().OrthoR(),
                _ => radial.Normalized()
            };
            var landing = actor.Position + direction * distance;
            var color = LandingUnsafe(slot, actor, landing, kb.Activation) ? Colors.Danger : Colors.Safe;
            Arena.AddLine(actor.Position, landing, color);
            Arena.AddLine(landing, landing - direction * 1.5f + direction.OrthoR() * 0.8f, color);
            Arena.AddLine(landing, landing - direction * 1.5f - direction.OrthoR() * 0.8f, color);
            Arena.ZoneCircleOutline(landing, 0.6f, color);
        }
    }
}
internal class SimpleKnockbacks(BossModule module, uint aid, float distance) : GenericKnockback(module, aid)
{
    public readonly float Distance = distance;
    protected readonly List<Knockback> Casters = [];
    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) => CollectionsMarshal.AsSpan(Casters);
    public override void OnCastStarted(Actor actor, ActorCastInfo spell) { if (spell.Action.ID == WatchedAction) Casters.Add(new(spell.LocXZ, Distance, spell.FinishAt, actorID: actor.InstanceID)); }
    public override void OnCastFinished(Actor actor, ActorCastInfo spell) { if (spell.Action.ID == WatchedAction) Casters.RemoveAll(b => b.ActorID == actor.InstanceID); }
}
internal class GenericGaze(BossModule module, uint aid = 0, float range = float.MaxValue) : BossComponent(module)
{
    public readonly uint WatchedAction = aid;
    protected readonly float Range = range;
    public int NumCasts;
    public readonly struct Eye(WPos position, DateTime activation, ulong actorID = 0, float range = float.MaxValue)
    {
        public readonly WPos Position = position;
        public readonly DateTime Activation = activation;
        public readonly ulong ActorID = actorID;
        public readonly float Range = range;
    }
    public virtual ReadOnlySpan<Eye> ActiveEyes(int slot, Actor actor) => [];
    protected static bool HitByEye(ref Actor actor, Eye eye) => actor.Rotation.ToDirection().Dot((eye.Position - actor.Position).Normalized()) >= 0.707107f;
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        foreach (var eye in ActiveEyes(slot, actor))
            if (actor.Position.InCircle(eye.Position, Math.Min(Range, eye.Range)))
                hints.Add("视线判定：背对眼睛／施法者！", HitByEye(ref actor, eye));
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var eye in ActiveEyes(slot, actor))
            if (actor.Position.InCircle(eye.Position, Math.Min(Range, eye.Range)))
                hints.ForbiddenDirections.Add((Angle.FromDirection(eye.Position - actor.Position), 45.Degrees(), eye.Activation));
    }
    public override void DrawArenaForeground(int slot, Actor actor) { foreach (var e in ActiveEyes(slot, actor)) Arena.AddLine(actor.Position, e.Position, Colors.Danger); }
}
internal class CastGazes(BossModule module, uint[] actions, float range = 60) : GenericGaze(module, range: range)
{
    protected readonly List<Eye> Eyes = [];
    public override ReadOnlySpan<Eye> ActiveEyes(int slot, Actor actor)
    {
        Eyes.RemoveAll(e => e.Activation.AddSeconds(1) < WorldState.CurrentTime);
        return CollectionsMarshal.AsSpan(Eyes);
    }
    public override void OnCastStarted(Actor actor, ActorCastInfo spell)
    {
        if (actions.Contains(spell.Action.ID) && !spell.EventHappened)
        {
            Eyes.RemoveAll(e => e.ActorID == actor.InstanceID);
            Eyes.Add(new(spell.LocXZ, spell.FinishAt, actor.InstanceID, Range));
        }
    }
    public override void OnCastFinished(Actor actor, ActorCastInfo spell) { if (actions.Contains(spell.Action.ID) && (spell.Interrupted || spell.EventHappened)) Eyes.RemoveAll(e => e.ActorID == actor.InstanceID); }
    public override void OnEventCast(Actor actor, ActorCastEvent spell) { if (actions.Contains(spell.Action.ID)) { Eyes.RemoveAll(e => e.ActorID == actor.InstanceID); ++NumCasts; } }
    public override void OnActorDestroyed(Actor actor) => Eyes.RemoveAll(e => e.ActorID == actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}
internal class CastGaze(BossModule module, uint aid, float range = 60) : CastGazes(module, [aid], range)
{
    public new readonly uint WatchedAction = aid;
}
