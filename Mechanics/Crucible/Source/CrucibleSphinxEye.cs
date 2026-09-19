// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

sealed class CrucibleSphinxRaidwide(BossModule module) : Components.CastHints(module, [49344u, 49351u, 49353u], "谜题：全屏伤害，准备减伤！");

// The four animals exchange positions during Change. Director battle talk 67,
// entries 3/4/5/6 (InstanceContentTextData 45103-45106), asks for avian/scalekin/
// beastkin/wavekin respectively. Entry 32 opens the answer window. Interact with
// the separate 2015459 event object at the original animal's CURRENT position.
sealed class CrucibleSphinxRiddles(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<ulong, uint> _animals = [];
    private readonly HashSet<ulong> _gathered = [];
    private bool _memory;
    private bool _answering;
    private uint _answerOID;
    private Actor? _interact;
    private DateTime _expires;

    private static string AnimalName(uint oid) => oid switch { 19711 => "渡渡鸟", 19712 => "陆鱼", 19713 => "奥猴", 19714 => "跳蜥", _ => "未知" };

    private void Begin()
    {
        if (!_memory)
        {
            _animals.Clear();
            _gathered.Clear();
            _answerOID = 0;
            _answering = false;
            _interact = null;
        }
        _memory = true;
        _expires = WorldState.FutureTime(65);
    }

    private void Finish()
    {
        _memory = _answering = false;
        _answerOID = 0;
        _interact = null;
        _gathered.Clear();
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 49343)
            Begin();
        else if (spell.Action.ID is 49348 or 49350)
            Finish();
    }

    public override void OnEventDirectorUpdate(uint updateID, uint param1, uint param2, uint param3, uint param4)
    {
        if (updateID != 0x80000027 || param3 != 14665 || param4 != Module.PrimaryActor.InstanceID)
            return;
        if (param1 == 2)
        {
            Finish();
            Begin();
        }
        else if (param1 is >= 3 and <= 6)
        {
            Begin();
            _answerOID = param1 switch { 3 => 19711u, 4 => 19714u, 5 => 19713u, _ => 19712u };
        }
        else if (param1 == 32 && _memory)
        {
            _answering = true;
            _expires = WorldState.FutureTime(20);
        }
        else if (param1 is 7 or 8 or 9)
            Finish();
    }

    public override void Update()
    {
        if (_expires <= WorldState.CurrentTime || Module.PrimaryActor.IsDeadOrDestroyed)
            Finish();
        if (!_memory)
            return;
        foreach (var a in WorldState.Actors)
            if (a.OID is >= 19711 and <= 19714 && !a.IsDeadOrDestroyed)
            {
                _animals.TryAdd(a.InstanceID, a.OID);
                if (a.Position.InCircle(Module.Center, 6))
                    _gathered.Add(a.InstanceID);
            }
        // Once the chosen interaction is consumed, never start a second answer.
        if (_interact is { } target && (target.IsDestroyed || !target.IsTargetable || target.EventState == 7))
            Finish();
    }

    private Actor? AnswerAnimal()
    {
        if (!_memory || _answerOID == 0 || _expires <= WorldState.CurrentTime)
            return null;
        return _animals.Where(a => a.Value == _answerOID).Select(a => WorldState.Actors.Find(a.Key))
            .FirstOrDefault(a => a is { IsDeadOrDestroyed: false });
    }

    private bool ReadyToApproach(Actor animal)
    {
        // Both ARR shuffles visit the outer corners a variable number of times,
        // gather at (+/-3, +/-3), then return to their final (+/-15, +/-15) corners.
        // The interaction objects appear ~4s later: use that time to walk over.
        var offset = animal.Position - Module.Center;
        return _answering || _gathered.Contains(animal.InstanceID)
            && MathF.Abs(MathF.Abs(offset.X) - 15) < 0.75f && MathF.Abs(MathF.Abs(offset.Z) - 15) < 0.75f;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        Update();
        if (AnswerAnimal() is not { } animal || !ReadyToApproach(animal))
            return;
        var target = _answering ? WorldState.Actors.FirstOrDefault(a => a.OID == 2015459 && a.IsTargetable && !a.IsDestroyed && a.EventState != 7 && a.Position.InCircle(animal.Position, 2)) : null;
        if (target != null)
        {
            _interact = target;
            hints.InteractWithTarget = target;
        }
        var destination = target?.Position ?? animal.Position;
        // Keep a continuous approach goal even when an external ACR supplies damage.
        hints.GoalPoints.Add((destination, 1.5f, 100));
        hints.MaxCastTime = 0;
        if (target != null && actor.Position.InCircle(destination, 2))
        {
            // Preserve the interaction progress while holding still.
            hints.ForcedMovement = Vector3.Zero;
            hints.AddSpecialMode(AIHints.SpecialMode.NoMovement, WorldState.CurrentTime);
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_memory)
            hints.Add(_answerOID != 0 ? $"记忆谜题：找到变化前的{AnimalName(_answerOID)}，等待换位结束后交互！" : "记忆谜题：等待题目，记住变化前的身份。", false);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (_memory)
            foreach (var (id, oid) in _animals)
                if (WorldState.Actors.Find(id) is { IsDeadOrDestroyed: false } a)
                    Arena.TextWorld(a.Position, AnimalName(oid), oid == _answerOID ? Colors.Safe : Colors.Object);
    }
}

// ARR: ActionEffect type 32/row 89 pulls to 1y from the helper, rather than
// moving a fixed 9y. The helper sits 2y outward from the hourglass NPC.
sealed class CrucibleEyeAttract(BossModule module) : Components.GenericKnockback(module, 49419)
{
    private DateTime _activation;
    private ulong _caster;
    private readonly Knockback[] _source = new Knockback[1];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 49418 && !spell.EventHappened && Module.CastFinishAt(spell) > WorldState.CurrentTime)
        {
            _activation = Module.CastFinishAt(spell, 1.1f);
            _caster = caster.InstanceID;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 49418 && spell.Interrupted && caster.InstanceID == _caster) _activation = default;
    }
    public override void OnActorDestroyed(Actor actor) { if (actor.InstanceID == _caster) _activation = default; }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        if (_activation == default || _activation.AddSeconds(1) < WorldState.CurrentTime)
            return [];
        var hourglass = Module.Enemies(19737).LastOrDefault(a => !a.IsDeadOrDestroyed);
        if (hourglass == null)
            return [];
        var origin = hourglass.Position + (hourglass.Position - Module.Center).Normalized() * 2;
        _source[0] = new(origin, 50, _activation, kind: Kind.TowardsOrigin, minDistance: 1);
        return _source;
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InCircle(Module.Center, 19.5f);

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            _activation = default;
            ++NumCasts;
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (ActiveKnockbacks(slot, actor).Length != 0)
            hints.Add("死亡轮盘：即将吸向沙漏，之后观察轮盘指向！", false);
    }
}

sealed class CrucibleEyeGazes(BossModule module) : Components.CastGazes(module, [49424u, 49434u], range: 60)
{
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Interrupted || spell.EventHappened) base.OnCastFinished(caster, spell);
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is not (49424 or 49434) || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        Eyes.RemoveAll(e => e.ActorID == caster.InstanceID);
        base.OnCastStarted(caster, spell);
    }

    public override void Update() => Eyes.RemoveAll(e => e.Activation.AddSeconds(1) < WorldState.CurrentTime || WorldState.Actors.Find(e.ActorID) is not { IsDeadOrDestroyed: false });

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 49424 or 49434)
            Eyes.RemoveAll(e => e.ActorID == caster.InstanceID);
    }
}

// Opposed casts identify the two sides. Guide toward the other brand, without
// treating both sides (or a gaze's sheet radius) as an arena-wide forbidden zone.
sealed class CrucibleEyeBrands(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<uint, (WPos origin, Angle rotation, DateTime activation, ulong caster)> _sides = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 49425 or 49426 && !spell.EventHappened && Module.CastFinishAt(spell) > WorldState.CurrentTime)
            _sides[spell.Action.ID] = (spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), caster.InstanceID);
    }

    private void Remove(Actor caster, uint action)
    {
        if (_sides.TryGetValue(action, out var side) && side.caster == caster.InstanceID) _sides.Remove(action);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    { if (spell.Interrupted || spell.EventHappened) Remove(caster, spell.Action.ID); }
    public override void OnEventCast(Actor caster, ActorCastEvent spell) => Remove(caster, spell.Action.ID);
    public override void OnActorDestroyed(Actor actor)
    {
        foreach (var id in _sides.Where(p => p.Value.caster == actor.InstanceID).Select(p => p.Key).ToArray()) _sides.Remove(id);
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
    public override void Update()
    {
        foreach (var (id, side) in _sides.ToArray())
            if (side.activation.AddSeconds(1) < WorldState.CurrentTime)
                _sides.Remove(id);
    }

    private uint SafeAction(Actor actor) => actor.FindStatus(5536) != null ? 49426u : actor.FindStatus(5537) != null ? 49425u : 0;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_sides.TryGetValue(SafeAction(actor), out var side))
        {
            // Stay clearly on the selected side; the angular edge is not measured yet.
            var goal = side.origin + side.rotation.ToDirection() * 8;
            hints.AddForbiddenConstraint(new SDInvertedCircle(goal, 3), side.activation.AddSeconds(-0.5), side.activation.AddSeconds(0.6));
            hints.GoalZones.Add(p => p.InCircle(goal, 2) ? 10 : 0);
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_sides.ContainsKey(SafeAction(actor)))
            hints.Add(SafeAction(actor) == 49426 ? "悲叹烙印：站愤怒视线一侧！" : "愤怒烙印：站悲叹视线一侧！", false);
    }
}

sealed class CrucibleEyeRaidwide(BossModule module) : Components.CastHints(module, [49428u, 49431u], "即死／核爆：注意宠物与减伤！");
