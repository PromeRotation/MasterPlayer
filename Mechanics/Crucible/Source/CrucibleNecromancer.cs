using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// ARR 2026-09-19 12:37:03: icon 707 -> 48181 -> ground-locked 48182.
// The target's R10 follows them only until the helper reveals the actual location.
sealed class CrucibleDeathDrive(BossModule module) : Components.GenericAOEs(module)
{
    private readonly Dictionary<ulong, DateTime> _targets = [];
    private readonly List<AOEInstance> _display = [];
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == 707) _targets.TryAdd(actor.InstanceID, WorldState.FutureTime(7.2));
    }
    public override void Update()
    {
        foreach (var (id, expiry) in _targets.ToArray())
            if (expiry.AddSeconds(1) < WorldState.CurrentTime || WorldState.Actors.Find(id) is not { IsDeadOrDestroyed: false }) _targets.Remove(id);
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48182) _targets.Clear();
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48181 or 48182 && spell.Interrupted) _targets.Clear();
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48182) _targets.Clear();
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor) _targets.Clear();
        else _targets.Remove(actor.InstanceID);
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
    private static readonly AOEShapeCircle TargetShape = new(10);
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Update();
        _display.Clear();
        foreach (var (id, expiry) in _targets)
            if (WorldState.Actors.Find(id) is { } target)
                _display.Add(new(TargetShape, target.Position, activation: expiry, risky: false, actorID: id));
        return CollectionsMarshal.AsSpan(_display);
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_targets.ContainsKey(actor.InstanceID)) hints.Add("死亡驱动点名：引导落点，圆圈固定后离开", false);
    }
}

// Spirit control gives Forward March (2161), then 1257 for 3s. This ARR moves
// 18y at 6y/s. Draw the whole path, since crossing miasma is unsafe even
// when its endpoint is clear. Facing is a player action, not a positional goal.
sealed class CrucibleNecromancerMarch(BossModule module) : BossComponent(module)
{
    internal (WPos End, bool Unsafe)? Projection(Actor actor)
    {
        var now = WorldState.CurrentTime;
        var active = actor.FindStatus(1257);
        var pending = actor.FindStatus(2161);
        if (actor.IsDeadOrDestroyed || Module.PrimaryActor.IsDeadOrDestroyed) return null;
        var seconds = active is { ExpireAt: var end } && end > now && end != DateTime.MaxValue
            ? Math.Clamp((float)(end - now).TotalSeconds, 0, 3)
            : pending is { ExpireAt: var expiry } && expiry > now && expiry != DateTime.MaxValue ? 3 : 0;
        if (seconds <= 0) return null;
        var direction = actor.Rotation.ToDirection();
        var destination = actor.Position + direction * (6 * seconds);
        var hazards = Module.Components.OfType<Components.GenericAOEs>()
            .SelectMany(c => c.ActiveAOEs(0, actor).ToArray()).Where(a => a.Risky && a.Activation == default).ToArray();
        var steps = Math.Max(1, (int)Math.Ceiling(6 * seconds / 0.25));
        for (var i = 0; i <= steps; ++i)
        {
            var point = actor.Position + direction * (6 * seconds * i / steps);
            if (!Arena.InBounds(point) || hazards.Any(a => a.Check(point))) return (destination, true);
        }
        return (destination, false);
    }
    public override void DrawArenaForeground(int slot, Actor actor)
    {
        if (Projection(actor) is not { } projection) return;
        var direction = actor.Rotation.ToDirection();
        var color = projection.Unsafe ? Colors.Danger : Colors.Safe;
        Arena.AddLine(actor.Position, projection.End, color);
        Arena.AddLine(projection.End, projection.End - direction * 1.5f + direction.OrthoR() * 0.8f, color);
        Arena.AddLine(projection.End, projection.End - direction * 1.5f - direction.OrthoR() * 0.8f, color);
        Arena.ZoneCircleOutline(projection.End, 0.6f, color);
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Projection(actor) is { } projection)
            hints.Add(projection.Unsafe ? "精神控制：调整面向，避开沿途瘴气和场外" : "精神控制：保持当前面向，等待强制前进", projection.Unsafe);
        if (actor.FindStatus(5424) is { Extra: var stacks } && stacks > 0)
            hints.Add($"渐渐混乱 {stacks}/12：优先击杀僵尸，避开瘴气", stacks >= 9);
    }
}
