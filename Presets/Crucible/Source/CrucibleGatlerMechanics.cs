using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// 48607 uses both knockback rows 234/235: the two sides of the central line
// are pushed outwards by 20y. Both endpoints must be on the irregular floor.
sealed class CrucibleGatlerTorrent(BossModule module) : Components.GenericKnockback(module, 48607u)
{
    private DateTime _activation;
    private ulong _caster;
    private Angle _rotation;
    private readonly Knockback[] _source = new Knockback[1];
    private LandingZone? _landing;

    private WDir Displacement(WPos p)
    {
        var normal = _rotation.ToDirection().OrthoR();
        return normal * ((p - Module.Center).Dot(normal) >= 0 ? 20 : -20);
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        if (_activation == default || _activation.AddSeconds(1) < WorldState.CurrentTime)
            return [];
        _source[0] = new(Module.Center, 20, _activation, direction: Angle.FromDirection(Displacement(actor.Position)), kind: Kind.DirForward);
        return _source;
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !CrucibleLaudaFloor.Contains(pos);
    private sealed class LandingZone(CrucibleGatlerTorrent component) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            return CrucibleLaudaFloor.SafeTravel(p, component.Displacement(p)) ? 1 : -1;
        }

        // The zone only changes with the cast rotation; avoid resampling the floor every frame.
        private FrozenConstraintGeometry? _geometry;
        public override MasterPlayer.Spatial.Geometry.IGeometryShape Geometry(float y)
            => _geometry != null && _geometry.Position.Y == y ? _geometry
                : _geometry = FrozenConstraintFactory.Create(p => Distance(p), component.Module.Center, y);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (ActiveKnockbacks(slot, actor).Length != 0 && !IsImmune(slot, _activation))
            hints.AddForbiddenConstraint(_landing ??= new LandingZone(this), _activation.AddSeconds(-0.5));
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && !spell.EventHappened && Module.CastFinishAt(spell) > WorldState.CurrentTime)
        {
            _activation = Module.CastFinishAt(spell);
            _rotation = spell.Rotation;
            _landing = null;
            _caster = caster.InstanceID;
        }
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && spell.Interrupted)
            _activation = default;
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            _activation = default;
            ++NumCasts;
        }
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == _caster || actor == Module.PrimaryActor)
            _activation = default;
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// This is one boss-to-player tether, not Lauda's three summoned swords.
// Keep the ray parallel to the long edges to miss the statues at X500/540.
sealed class CrucibleGatlerBait(BossModule module) : Components.GenericBaitAway(module)
{
    private WPos? _point;
    private DateTime _lockAt;
    private bool _moving;

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.OID != 19626 || tether.ID != 1 || WorldState.Actors.Find(tether.Target) is not { } target)
            return;
        if (CurrentBaits.Any(b => b.Source == source && b.Target == target))
            return;
        Clear();
        _point = new(Module.Center.X, Module.Center.Z + (target.Position.Z >= source.Position.Z ? 22.5f : -22.5f));
        _lockAt = WorldState.FutureTime(4.6);
        CurrentBaits.Add(new(source, target, new AOEShapeRect(60, 3), _lockAt));
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        if (spell.Action.ID == 48612)
        {
            // ARR fixtures carry the 19626 helper cast but not its tether event, so
            // the live-only OnTethered path never seeds the bait offline. The tether
            // and the 48612 cast share source/target; seed the same bait from the cast.
            if (_point == null && WorldState.Player is { IsDeadOrDestroyed: false } player)
                SetBait(caster, player, Module.CastFinishAt(spell, -0.4));
            else
                _lockAt = Module.CastFinishAt(spell, -0.4);
        }
        else if (spell.Action.ID == 48614)
            Clear(); // Helper start locks the ground line: stop holding and evade it.
    }
    private void SetBait(Actor source, Actor target, DateTime lockAt)
    {
        Clear();
        _point = new(Module.Center.X, Module.Center.Z + (target.Position.Z >= source.Position.Z ? 22.5f : -22.5f));
        _lockAt = lockAt;
        CurrentBaits.Add(new(source, target, new AOEShapeRect(60, 3), lockAt));
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48612 && spell.Interrupted)
            Clear();
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 48613 or 48614)
            Clear();
    }
    public override void Update()
    {
        if (_lockAt.AddSeconds(4) < WorldState.CurrentTime || CurrentBaits.Any(b => b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed))
            Clear();
    }
    private void Clear()
    {
        CurrentBaits.Clear();
        _point = null;
        _moving = false;
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (_point is not { } p || !IsBaitTarget(actor))
            return;
        _moving |= WorldState.FutureTime((p - actor.Position).Length() / 6 + 1) >= _lockAt;
        if (_moving)
        {
            hints.GoalPoints.Add((p, 0.3f, 100));
            hints.MaxCastTime = 0;
        }
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (IsBaitTarget(actor))
            hints.Add("支配魔刃：沿长边方向引导，避开两侧石像；锁线后躲开！", false);
        else
            base.AddHints(slot, actor, hints);
    }
    public override void DrawArenaForeground(int slot, Actor actor)
    {
        base.DrawArenaForeground(slot, actor);
        if (_point is { } p && IsBaitTarget(actor))
            Arena.ZoneCircleOutline(p, 0.8f, Colors.Safe);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (CurrentBaits.Any(b => b.Source == actor || b.Target == actor))
            Clear();
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// Icon471 follows the marked target until the hit. Do not chase one's own line.
sealed class CrucibleGatlerThunder(BossModule module) : Components.BaitAwayCast(module, 48620u, new AOEShapeRect(50, 3), endsOnCastEvent: true, tankbuster: true)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        CurrentBaits.RemoveAll(b => b.Source == caster);
        base.OnCastStarted(caster, spell);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && spell.Interrupted)
            CurrentBaits.RemoveAll(b => b.Source == caster);
    }
    public override void Update() => CurrentBaits.RemoveAll(b => b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed || b.Activation.AddSeconds(2) < WorldState.CurrentTime);
}

// 669 fixes an R6 drop after ~5.4s, then a distance-falloff explosion.
// The R80 sheet range is NOT an arena-wide forbidden zone.
sealed class CrucibleGatlerFireKnife(BossModule module) : BossComponent(module)
{
    private ulong _target, _caster;
    private WPos? _drop, _explosion;
    private DateTime _dropUntil, _explodeAt;

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != 669)
            return;
        _target = actor.InstanceID;
        WPos[] tips = [new(520, -442.5f), new(520, -397.5f), new(507.5f, -422.5f), new(533, -417.5f)];
        _drop = tips.MinBy(p => (p - actor.Position).LengthSq());
        _dropUntil = WorldState.FutureTime(8);
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        if (spell.Action.ID is 48618 or 48619)
        {
            _drop = null;
            _explosion = spell.LocXZ;
            _caster = caster.InstanceID;
            _explodeAt = Module.CastFinishAt(spell, spell.Action.ID == 48618 ? 9 : 0);
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48619)
            _explosion = null;
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48615 or 48618 or 48619 && spell.Interrupted)
        {
            _drop = null;
            _explosion = null;
        }
    }
    public override void Update()
    {
        if (_dropUntil < WorldState.CurrentTime)
            _drop = null;
        if (_explodeAt.AddSeconds(1) < WorldState.CurrentTime)
            _explosion = null;
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_drop is { } drop && _target == actor.InstanceID)
        {
            hints.GoalPoints.Add((drop, 0.3f, 100));
            hints.MaxCastTime = 0;
        }
        if (_explosion is { } explosion)
            hints.GoalZones.Add(p => Math.Min(12, (p - explosion).Length() / 4));
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_drop != null && _target == actor.InstanceID)
            hints.Add("火焰魔刃：放到边缘安全凸台，落点固定后远离！", false);
        if (_explosion != null)
            hints.Add("魔刃爆焰：远离飞刀，距离越远伤害越低！", false);
    }
    public override void DrawArenaForeground(int slot, Actor actor)
    {
        if (_drop is { } drop && _target == actor.InstanceID)
            Arena.ZoneCircleOutline(drop, 0.8f, Colors.Safe);
        if (_explosion is { } explosion)
            Arena.ZoneCircleOutline(explosion, 6, Colors.Danger);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == _target || actor == Module.PrimaryActor)
            _drop = null;
        if (actor.InstanceID == _caster || actor == Module.PrimaryActor)
            _explosion = null;
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// [low3-gatler-20261110] 新增：霹雳(48620) 读条期间把宠物移出直线。
// 依据（按劳妲类推，加特勒无实录）：
//  - Low3.md：48620 霹雳 Rect(50,3)、读条 5s、麻痹[5388]，与劳妲 49470 同形状同读条。
//  - 劳妲 run2278 两遍：霹雳读完时宠物站在劳妲与玩家之间，胡蜂两次被这一下打死，之后无宠。
//    run2295 两遍 4 次霹雳都只打到人物、没有打到宠物（劳妲那边的同名组件当时是否已部署，本次没有核对部署记录）。
//  - 加特勒攻略配队含胡蜂，同样怕这一下。
// 实现是 CrucibleLaudaThunderPetDodge 的逐行拷贝，只改了技能 ID（49470 -> 48620）和类名，
// 并去掉了 [SkipLocalsInit]（本文件没有 using System.Runtime.CompilerServices）。
// 场地用同一个 CrucibleLaudaFloor（f1x3 加特勒与 f1x5 劳妲共用 gim05 地板，见 CrucibleLaudaFloor.cs 注释）。
// 需要在 CrucibleCatalog.cs 第 21 组里登记后才生效；不登记时本类不会被构造，对现有行为零影响。
sealed class CrucibleGatlerThunderPetDodge(BossModule module) : BossComponent(module)
{
    private const uint Thunder = 48620;
    private const float HalfWidth = 3f;
    private const float Clear = 6.5f;
    private readonly List<(Actor Caster, ulong Target, DateTime End)> _casts = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != Thunder || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        _casts.RemoveAll(c => c.Caster == caster);
        _casts.Add((caster, spell.TargetID, Module.CastFinishAt(spell, 0.8f)));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == Thunder)
            _casts.RemoveAll(c => c.Caster == caster);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == Thunder && spell.Interrupted)
            _casts.RemoveAll(c => c.Caster == caster);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        _casts.RemoveAll(c => c.End <= WorldState.CurrentTime || c.Caster.IsDeadOrDestroyed);
        if (_casts.Count == 0 || hints.PetMoveTo != null)
            return;
        var (caster, targetId, _) = _casts[0];
        var target = WorldState.Actors.Find(targetId) ?? actor;
        var dirVec = target.Position - caster.Position;
        if (dirVec.LengthSq() < 0.01f)
            return;
        var dir = dirVec.Normalized();
        var perp = dir.OrthoL();
        foreach (var pet in CrucibleNightmare.Pets(WorldState, actor))
        {
            if (targetId == pet.InstanceID)
                continue; // 宠物自己被点名时躲不开
            var rel = pet.Position - caster.Position;
            var along = rel.Dot(dir);
            var side = rel.Dot(perp);
            if (along < -2f || MathF.Abs(side) >= HalfWidth + 1.5f)
                continue;
            var sign = side >= 0 ? 1f : -1f;
            WPos? pick = null;
            foreach (var s in new[] { sign, -sign })
            {
                var p = caster.Position + perp * (s * Clear) + dir * -1f;
                if (CrucibleLaudaFloor.Contains(p)) { pick = p; break; }
            }
            if (pick != null)
                hints.PetMoveTo = pick;
            return;
        }
    }
}
