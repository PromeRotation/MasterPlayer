// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Icon 234 precedes the fixed 49377/49378/49379 casts by ~6.1s.
// The slime must be inside 49379: a 7x4 rectangle starting 3y in front
// of the boss, not merely inside the 40x8 player-damage rectangle.
sealed class CrucibleGiantSlimes(BossModule module) : BossComponent(module)
{
    private readonly HashSet<ulong> _seen = [];
    private readonly HashSet<ulong> _handled = [];
    private Actor? _boss, _baitSlime, _attackSlime;
    private uint _firstColour;
    private ulong _target;
    private DateTime _expires;
    private bool _locked, _pullAway;

    private static bool LivingSlime(Actor a) => a.OID is 19717 or 19719 && !a.IsDeadOrDestroyed;

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != 234)
            return;
        Update();
        if (_locked)
            return;
        _target = (WorldState.Actors.Find(targetID) ?? actor).InstanceID;
        _expires = WorldState.FutureTime(7);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is not (49377 or 49378 or 49379) || spell.EventHappened)
            return;
        // Hydration may deliver a helper before the main cast or first Update.
        Update();
        if (_locked)
            return;
        var available = WorldState.Actors.Where(a => LivingSlime(a) && !_handled.Contains(a.InstanceID)).ToArray();
        if (_baitSlime != null && _firstColour != 0)
            _attackSlime = available.FirstOrDefault(a => a.OID != _baitSlime.OID);
        if (_baitSlime != null && _firstColour == 0)
            _firstColour = _baitSlime.OID;
        foreach (var slime in available)
            _handled.Add(slime.InstanceID);
        _locked = true;
        _baitSlime = null;
        _target = 0;
        _expires = WorldState.FutureTime(40);
    }

    public override void Update()
    {
        _boss = WorldState.Actors.FirstOrDefault(a => a.NameID == 14670 && a.OID != 9020 && !a.IsDeadOrDestroyed);
        if (_boss == null)
        {
            _baitSlime = _attackSlime = null;
            _target = 0;
            return;
        }
        var available = WorldState.Actors.Where(a => LivingSlime(a) && !_handled.Contains(a.InstanceID)).OrderBy(a => a.InstanceID).ToArray();
        var newWave = false;
        foreach (var slime in available)
            newWave |= _seen.Add(slime.InstanceID);
        if (newWave)
        {
            _locked = false;
            _pullAway = false;
            _attackSlime = null;
            _expires = WorldState.FutureTime(12);
        }
        if (_expires < WorldState.CurrentTime)
        {
            foreach (var slime in available)
                _handled.Add(slime.InstanceID);
            _baitSlime = _attackSlime = null;
            _target = 0;
            return;
        }
        if (!_locked && (_baitSlime == null || _baitSlime.IsDeadOrDestroyed))
            _baitSlime = available.FirstOrDefault(a => _firstColour == 0 || a.OID != _firstColour);
        if (_baitSlime != null)
        {
            var distance = (_baitSlime.Position - _boss.Position).Length();
            if (distance < 3.5f)
                _pullAway = true;
            else if (distance >= 6)
                _pullAway = false;
        }
        if (_attackSlime is { IsDeadOrDestroyed: true })
            _attackSlime = null;
    }

    private bool NeedsPull() => _boss != null && _baitSlime != null
        && (_pullAway || (_baitSlime.Position - _boss.Position).Length() > 8);

    private WPos PullPoint()
    {
        var slime = _baitSlime!.Position;
        if (!_pullAway)
            return slime;
        // If the boss is on top of the slime, first pull it back into stomp range.
        var direction = (Module.Center - slime).Normalized();
        if (direction.LengthSq() < 0.5f)
            direction = new WDir(0, 1);
        return slime + direction * 12;
    }

    private AimZone? Aim() => _boss != null && _baitSlime != null ? new(_boss.Position, _baitSlime.Position) : null;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_boss == null || actor.IsDeadOrDestroyed)
            return;
        if (_locked)
        {
            // Keep the other colour as the attack target after lock. The fixed
            // stomp AOEs still take precedence over movement toward this target.
            if (_attackSlime is { IsDeadOrDestroyed: false, IsTargetable: true } attack)
            {
                hints.SetPriority(attack, 10);
                hints.ForcedTarget = attack;
                hints.GoalZones.Add(AIHints.GoalSingleTarget(attack, 2.6f));
            }
            return;
        }
        if (_baitSlime == null)
            return;
        hints.SetPriority(_baitSlime, AIHints.Enemy.PriorityUndesirable);
        if (_boss.IsTargetable)
            hints.ForcedTarget = _boss;
        if (NeedsPull())
        {
            if (_boss.TargetID != 0 && _boss.TargetID != actor.InstanceID)
                return;
            var point = PullPoint();
            hints.GoalPoints.Add((point, 0.2f, 100));
            hints.MaxCastTime = 0;
        }
        else if ((_target == actor.InstanceID || _target == 0 && _boss.TargetID == actor.InstanceID) && Aim() is { } aim)
        {
            hints.GoalZones.Add(p => MathF.Max(0, 100 + 10 * aim.Distance(p)));
            hints.MaxCastTime = 0;
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_locked)
        {
            if (_attackSlime is { IsDeadOrDestroyed: false, IsTargetable: true })
                hints.Add("躲开重踏，攻击另一只史莱姆！", false);
            return;
        }
        if (_baitSlime == null)
            return;
        var colour = _baitSlime.OID == 19717 ? "雷" : "火";
        if (NeedsPull())
            hints.Add($"把巨人拉近{colour}史莱姆，让前方小矩形打中它！", true);
        else if (actor.InstanceID == _target)
            hints.Add($"用前方小矩形瞄准{colour}史莱姆，锁定后侧移！", Aim()?.Distance(actor.Position) < 0);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (_boss == null || _baitSlime == null || _locked)
            return;
        Arena.Actor(_baitSlime, Colors.Object);
        if (NeedsPull())
        {
            var point = PullPoint();
            new AOEShapeCircle(0.8f).Outline(Arena, point, default, Colors.Safe);
            Arena.AddLine(_boss.Position, _baitSlime.Position, Colors.Safe);
        }
        else if (pc.InstanceID == _target)
        {
            var rotation = Angle.FromDirection(pc.Position - _boss.Position);
            new AOEShapeRect(7, 2).Outline(Arena, _boss.Position + rotation.ToDirection() * 3, rotation,
                Aim()?.Distance(pc.Position) >= 0 ? Colors.Safe : Colors.Danger);
        }
    }

    private sealed class AimZone(WPos boss, WPos slime) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            var offset = p - boss;
            var length = offset.Length();
            if (length < 0.1f)
                return -1;
            var direction = offset / length;
            var toSlime = slime - boss;
            var along = toSlime.Dot(direction);
            var clearance = MathF.Min(length - 1.5f, 3.25f - length);
            clearance = MathF.Min(clearance, MathF.Min(along - 3.5f, 9.5f - along));
            return MathF.Min(clearance, 1.5f - MathF.Abs(toSlime.Cross(direction)));
        }
    }
}
