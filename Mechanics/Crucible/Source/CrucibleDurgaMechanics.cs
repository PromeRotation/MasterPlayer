// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Icon 23 -> 49273 (4.2s) -> helper relocates to the target ~0.85s
// after the cast -> 49274 (2s). The hit references Knockback 68: 40y,
// away from the ground source. 49267 is Missile; 49270 is an autoattack.
sealed class CrucibleDurgaEdgeKnockback(BossModule module) : Components.GenericKnockback(module, 49274)
{
    private const float Fence = 19.3f;
    private readonly List<Knockback> _sources = [];
    private ulong _target;
    private WPos? _bait;
    private DateTime _lockAt, _expires;

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == 23)
            Stage(actor, WorldState.FutureTime(5.1));
    }

    private void Stage(Actor target, DateTime lockAt)
    {
        _target = target.InstanceID;
        var offset = target.Position - Module.Center;
        // A 40y push needs the diagonal of this 40x40 square. Lock a corner
        // once, so attack goals cannot pull the marked player back to the boss.
        _bait ??= Module.Center + new WDir(offset.X < 0 ? -17.5f : 17.5f, offset.Z < 0 ? -17.5f : 17.5f);
        _lockAt = lockAt;
        _expires = lockAt.AddSeconds(3);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        if (spell.Action.ID == 49273)
        {
            // The visual cast targets the boss itself; the icon names the player.
            if ((WorldState.Actors.Find(_target) ?? Raid.Player()) is { } target)
                Stage(target, Module.CastFinishAt(spell, 0.85f));
        }
        else if (spell.Action.ID == WatchedAction)
        {
            _bait = null;
            _sources.RemoveAll(s => s.ActorID == caster.InstanceID);
            _sources.Add(new(caster.Position, 40, Module.CastFinishAt(spell), actorID: caster.InstanceID));
        }
    }

    public override void Update()
    {
        if (Module.PrimaryActor.IsDeadOrDestroyed)
        {
            _bait = null;
            _sources.Clear();
        }
        if (_expires < WorldState.CurrentTime)
            _bait = null;
        _sources.RemoveAll(s => s.Activation.AddSeconds(0.8) < WorldState.CurrentTime
            || WorldState.Actors.Find(s.ActorID) is not { IsDeadOrDestroyed: false });
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Interrupted)
        {
            if (spell.Action.ID == 49273)
                _bait = null;
            else if (spell.Action.ID == WatchedAction)
                _sources.RemoveAll(s => s.ActorID == caster.InstanceID);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            ++NumCasts;
            _sources.RemoveAll(s => s.ActorID == caster.InstanceID);
            _bait = null;
            _target = 0;
        }
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) => CollectionsMarshal.AsSpan(_sources);

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
    {
        var o = pos - Module.Center;
        return MathF.Max(MathF.Abs(o.X), MathF.Abs(o.Z)) >= Fence;
    }

    private WPos Aim(WPos origin)
    {
        var offset = origin - Module.Center;
        var farCorner = Module.Center + new WDir(offset.X < 0 ? Fence : -Fence, offset.Z < 0 ? Fence : -Fence);
        return origin + (farCorner - origin).Normalized() * 2.5f;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_bait is { } bait && actor.InstanceID == _target)
        {
            hints.AddForbiddenConstraint(new SDInvertedCircle(bait, 1), _lockAt.AddSeconds(-0.4));
            hints.GoalPoints.Add((bait, 0.2f, 100));
        }
        foreach (var source in _sources)
        {
            if (IsImmune(slot, source.Activation))
                continue;
            hints.AddForbiddenConstraint(new LandingZone(Module.Center, source.Origin), source.Activation.AddSeconds(-0.4));
            hints.GoalPoints.Add((Aim(source.Origin), 0.2f, 100));
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_bait is { } bait && actor.InstanceID == _target)
            hints.Add("去场边角落放气化炸弹，落点出现前保持站位！", !actor.Position.InCircle(bait, 1));
        if (_sources.Count > 0)
            hints.Add("站到落点朝场内的一侧，沿最长对角线击退！", false);
        base.AddHints(slot, actor, hints);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (_bait is { } bait && pc.InstanceID == _target)
            new AOEShapeCircle(1).Outline(Arena, bait, default, Colors.Safe);
        foreach (var source in _sources)
        {
            new AOEShapeCircle(0.6f).Outline(Arena, source.Origin, default, Colors.Danger);
            new AOEShapeCircle(0.8f).Outline(Arena, Aim(source.Origin), default, Colors.Safe);
        }
        base.DrawArenaForeground(pcSlot, pc);
    }

    private sealed class LandingZone(WPos center, WPos origin) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            if ((p - origin).LengthSq() < 0.25f)
                return -1; // The exact source has no dependable direction.
            var landing = AwayFromSource(p, origin, 40) - center;
            return Fence - MathF.Max(MathF.Abs(landing.X), MathF.Abs(landing.Z));
        }
    }
}

// 杜尔迦场边会出现转盘堡小怪；小怪存在时应优先处理，避免 AI 继续锁定 boss。
sealed class CrucibleDurgaAdds(BossModule module) : BossComponent(module)
{
    private const uint AddOID = 19704u; // 奇子·转盘堡

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var adds = Module.Enemies(AddOID).Where(a => !a.IsDeadOrDestroyed && a.IsTargetable && a.HPMP.CurHP > 0).ToArray();
        foreach (var add in adds)
            hints.SetPriority(add, 10);
        // Explicitly select an add even when an external rotation handles attacks.
        if (adds.Length > 0)
            hints.ForcedTarget = adds.FirstOrDefault(a => a.InstanceID == actor.TargetID)
                ?? adds.MinBy(a => (a.Position - actor.Position).LengthSq());
        else if (Module.Enemies(19702u).FirstOrDefault(a => !a.IsDeadOrDestroyed && a.IsTargetable && a.HPMP.CurHP > 0) is { } boss)
            hints.ForcedTarget = boss;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Module.Enemies(AddOID).Any(a => !a.IsDeadOrDestroyed && a.IsTargetable && a.HPMP.CurHP > 0))
            hints.Add("优先击杀杜尔迦小怪", false);
    }
}
