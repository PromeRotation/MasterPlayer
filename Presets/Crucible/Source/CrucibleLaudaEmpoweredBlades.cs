// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// ARR 22:01:47: 49472 spawns two 20152/14937 magic-empowered blades at
// Z=-403/-437. Kill both before moving to the edge for 49477 -> 49480.
// kano 11-09：暴食战斧阶段也有两把「物理强化魔刃」，要先打掉再打劳妲。
// run2249~2261 劳妲都没打到这一段（最低 66%），物理剑的 OID 未实测：
// 按名字含「强化魔刃」或 OID 20151/20153 识别；场边躲避只给魔法剑（49477/49480）。
// run2295 实测：49472 强化魔刃 → 物理强化魔刃 OID 19746 / NameId 14697（接 49473 暴食战斧，伤害 49474/49475 多段），
// 或魔法强化魔刃 OID 20152 / NameId 14937（接 49477 灼焰战斧）。49472 本身不区分物理/魔法，按刷出的剑定。
sealed class CrucibleLaudaEmpoweredBlades(BossModule module) : BossComponent(module)
{
    private const uint MagicBlade = 20152, PhysicalBlade = 19746;
    private readonly HashSet<ulong> _blades = [];
    private DateTime _expires;
    private WPos? _edge;
    private bool _finishing;
    private bool _magic;

    private static bool IsBlade(Actor a) => a.Type == ActorType.Enemy && (a.OID is 20151 or MagicBlade or 20153 or PhysicalBlade
        || a.Name != null && a.Name.Contains("强化魔刃"));

    public override void OnActorCreated(Actor actor)
    {
        if (IsBlade(actor))
        {
            if (_expires <= WorldState.CurrentTime)
            {
                _blades.Clear();
                _magic = false;
            }
            _blades.Add(actor.InstanceID);
            _magic |= actor.OID == MagicBlade || actor.Name?.Contains("魔法") == true;
            _expires = WorldState.FutureTime(45);
            _edge = null;
            _finishing = false;
        }
    }

    // 名字可能在创建后才同步：每帧补扫一次在场的强化魔刃。
    public override void Update()
    {
        foreach (var a in WorldState.Actors)
            if (!a.IsDeadOrDestroyed && a.IsTargetable && !_blades.Contains(a.InstanceID) && IsBlade(a))
                OnActorCreated(a);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return;
        if (spell.Action.ID == 49472)
        {
            _blades.Clear();
            _magic = false;
            foreach (var blade in Module.Enemies(MagicBlade).Concat(Module.Enemies(PhysicalBlade)))
                if (!blade.IsDeadOrDestroyed)
                {
                    _blades.Add(blade.InstanceID);
                    _magic |= blade.OID == MagicBlade;
                }
            _edge = null;
            _finishing = false;
            _expires = WorldState.FutureTime(45);
        }
        else if (spell.Action.ID == 49477)
        {
            _magic = true;
            _finishing = true;
            _expires = Module.CastFinishAt(spell, 3.5);
        }
        else if (spell.Action.ID == 49480)
        {
            _magic = true;
            _finishing = true;
            _expires = Module.CastFinishAt(spell, 0.5);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 49480)
            _expires = default;
    }

    private IEnumerable<Actor> Remaining => _blades.Select(id => WorldState.Actors.Find(id))
        .OfType<Actor>().Where(a => !a.IsDeadOrDestroyed);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_expires <= WorldState.CurrentTime)
        {
            RestoreBoss(actor, hints);
            return;
        }
        var remaining = Remaining.ToArray();
        if (remaining.Length == 0 && !_magic)
        {
            RestoreBoss(actor, hints);
            return;
        }
        if (remaining.Length > 0)
        {
            foreach (var blade in remaining)
                if (blade.IsTargetable && blade.HPMP.CurHP > 0)
                    hints.SetPriority(blade, 10);
            hints.ForcedTarget = remaining.FirstOrDefault(a => a.IsTargetable && a.HPMP.CurHP > 0 && a.InstanceID == actor.TargetID)
                ?? remaining.Where(a => a.IsTargetable && a.HPMP.CurHP > 0).MinBy(a => (a.Position - actor.Position).LengthSq());
        }
        else if (_magic && (_blades.Count >= 2 || _finishing))
        {
            RestoreBoss(actor, hints);
            _edge ??= Module.Center + new WDir(0, actor.Position.Z < Module.Center.Z ? -22.5f : 22.5f);
            var edge = _edge.Value;
            hints.GoalPoints.Add((edge, 0.2f, 100));
            hints.AddForbiddenConstraint(new SDInvertedCircle(edge, 1), _expires.AddSeconds(-0.8));
            hints.MaxCastTime = 0;
        }
    }

    private void RestoreBoss(Actor actor, AIHints hints)
    {
        if (_blades.Contains(actor.TargetID) && Module.PrimaryActor is { IsTargetable: true, IsDeadOrDestroyed: false } boss)
            hints.ForcedTarget = boss;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_expires > WorldState.CurrentTime && (_blades.Count > 0 || _finishing))
            hints.Add(Remaining.Any() ? (_magic ? "先击杀两把魔法强化魔刃！" : "先击杀两把物理强化魔刃！")
                : _magic ? "魔刃已清：到场地最外侧躲避！" : "魔刃已清：回打劳妲", false);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 49472 or 49477 or 49480 && spell.Interrupted)
        {
            _expires = default;
            _edge = null;
            _finishing = false;
        }
    }
}
