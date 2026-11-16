// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// High first board, ARR 2026-09-10/11 (groups 22/23/24/25/26/28/30/31).
// Only confirmed Action/Omen geometry belongs here. CastType 13 has no reliable
// cone angle; 48721/48808/48816 have no documented inner radius yet.
class HighCrucibleCastAOEs(BossModule module, Battle battle) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    // Native pending includes the cast/effect gap; do not restore the near-center
    // positioning constraint as soon as a moving add's cast bar disappears.
    internal bool HasPending(uint actionID)
    {
        foreach (ref readonly var pending in Pending)
            if (pending.ActionID == actionID) return true;
        return false;
    }
    internal bool HasPending(uint actionID, DateTime landsBy)
    {
        foreach (ref readonly var pending in Pending)
            if (pending.ActionID == actionID && pending.AOE.Activation <= landsBy) return true;
        return false;
    }    internal bool HasPendingAfter(uint actionID, DateTime landsAfter)
    {
        foreach (ref readonly var pending in Pending)
            if (pending.ActionID == actionID && pending.AOE.Activation > landsAfter) return true;
        return false;
    }
    internal bool HasPendingMorbolAcid
    {
        get
        {
            if (battle != Battle.B23) return false;
            foreach (ref readonly var pending in Pending)
                if (pending.ActionID == 48679) return true;
            return false;
        }
    }
    // Icefall waves arrive 2.2s apart and coexist in the cast list. Only the
    // first wave is forbidden; persistent ice is handled by its own component.
    protected override double RiskyActivationWindow => battle is Battle.B22 or Battle.B25 or Battle.B29 ? 0.5 : double.PositiveInfinity;
    // Use cast-packet locations for these replay-validated attacks: a moving
    // caster can still be short of the actual endpoint when casting starts.
    protected override AOEConfig? ConfigFor(uint actionID) => (battle, actionID) switch
    {
        (Battle.B22, 48656) => new(new AOEShapeCircle(10f), true), // 掉落 | Range 0
        (Battle.B22, 48665) => new(new AOEShapeCircle(15f), true), // 魔力爆散（魔导书 48664 前置），原来漏画 | Range 0
        (Battle.B23, 48679) => new(new AOEShapeCircle(6f), true), // 酸雾 | Range 0
        (Battle.B23, 48682) => new(new AOEShapeRect(13f, 4f), true), // 藤鞭穿刺 | Range 0
        (Battle.B24, 48693) => new(new AOEShapeCircle(6f), true), // 酸雨 | Range 100
        (Battle.B25, 48697 or 48699) => new(new AOEShapeRect(100f, 7.5f), true), // 冰寒折磨 | Range 0
        (Battle.B25, 48708) => new(new AOEShapeCircle(9f), true), // 无终的枯朽 | Range 100
        (Battle.B25, 48712) => new(new AOEShapeCircle(5f), true), // 寒冰覆盖 | Range 100
        (Battle.B26, 48715) => new(new AOEShapeCircle(6f), true), // Miasma placement; persistent mines have their own lifetime.
        (Battle.B26, 48727) => new(new AOEShapeRect(60f, 3.5f), true), // 荒芜新星 | Range 0
        (Battle.B26, 48729) => new(new AOEShapeCircle(6f), true), // 沥青海详情行 | Range 100
        (Battle.B28, 48750) => new(new AOEShapeRect(5f, 2.5f), true), // 冲击波 | Range 0
        (Battle.B28, 48753) => new(new AOEShapeDonut(5f, 50f), true), // 核心环光 gl_sircle_5005_j2 | Range 0
        (Battle.B28, 48755) => new(new AOEShapeCircle(20f), true), // 平原震裂 | Range 0
        (Battle.B28, 48768) => new(new AOEShapeCone(40f, 30f.Degrees()), true), // 岩突 gl_fan060 | Range 0
        (Battle.B30, 48791) => new(new AOEShapeCircle(5f), true),
        (Battle.B30, 48796) => new(new AOEShapeCone(40f, 10f.Degrees()), true),
        (Battle.B30, 48799) => new(new AOEShapeCone(40f, 90f.Degrees()), true), // 烈火怒骂 gl_fan180 | Range 0
        (Battle.B31, 48812 or 48819) => new(new AOEShapeCircle(6f), true), // 浓烟呛呕 / 蠕动的痰 | Range 100
        (Battle.B31, 48814) => new(new AOEShapeRect(48f, 10f), true), // 低温俯冲 | Range 0
        _ => null
    };

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // The margin affects navigation only, not the displayed client geometry.
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            if (battle != Battle.B22 || aoe.Risky)
                CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// 48690 has a 4.7s cast; 48691 is the actual 45x12 breath, with no cast.
// Keep the warning through the visual cast's end, until the damage event (~0.2s later).
sealed class CrucibleRottenStench(BossModule module) : Components.GenericAOEs(module, 48691u)
{
    private static readonly AOEShapeRect Shape = new(45f, 6f);
    private readonly List<AOEInstance> _aoes = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.RemoveAll(a => a.Activation.AddSeconds(2d) < WorldState.CurrentTime);
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != 48690 || spell.EventHappened)
            return;
        _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
        _aoes.Add(new(Shape, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell, 0.2d), actorID: caster.InstanceID));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48690 && spell.Interrupted)
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID); // Interrupted visual
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
            ++NumCasts;
        }
    }

    public override void OnActorDestroyed(Actor actor) => _aoes.RemoveAll(a => a.ActorID == actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// Miasma NPCs exist before their placement and initially share the boss position.
// 48715 is the initial placement blast. Once placed, avoid contact only: the
// R6 explosion is conditional on touching a ball, not a persistent ground AOE.
// 48716 consumes the ball and applies Gradual Panic (5178).
sealed class CrucibleMiasma(BossModule module) : Components.GenericAOEs(module, 48716u, "避开瘴气，防止恐慌叠层！")
{
    public const float ContactRadius = 1.8f; // Navigation clearance, not the explosion radius.
    // A 0.5y grid seals the narrow but walkable gap between a corner ball and
    // the fence. Refine this encounter's grid instead of shrinking the ball.
    private static readonly ArenaBoundsSquare MineNavigation = new(19.7f, mapResolution: 0.2f);
    public ArenaBounds? NavigationBounds => ArmedPositions().Any() ? MineNavigation : null;
    private readonly HashSet<ulong> _armed = [];
    private readonly HashSet<ulong> _resolved = [];
    private readonly HashSet<ulong> _relocating = [];

    public override void Update()
    {
        if (!_bookSeen && Module.Raid.Player()?.FindStatus(CrucibleGargoyleTetherStretch.DodgeBook) != null)
            _bookSeen = true;
        // Tether 426 links placed mines to the boss; recover them on late load.
        foreach (var mine in Module.Enemies(19651))
            if (mine.Tether.ID == 426 && !mine.IsDeadOrDestroyed && !_resolved.Contains(mine.InstanceID) && !_relocating.Contains(mine.InstanceID))
                _armed.Add(mine.InstanceID);
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => [];

    public IEnumerable<WPos> ArmedPositions() => _armed
        .Select(id => WorldState.Actors.Find(id))
        .Where(source => source is { IsDeadOrDestroyed: false })
        .Select(source => source!.Position);

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.OID == 19651 && tether.ID == 426 && !_resolved.Contains(source.InstanceID) && !_relocating.Contains(source.InstanceID))
            _armed.Add(source.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.OID != 19651u)
            return;
        if (spell.Action.ID == 48714)
        {
            _relocating.Remove(caster.InstanceID);
            _resolved.Remove(caster.InstanceID);
            _armed.Add(caster.InstanceID);
        }
        else if (spell.Action.ID == WatchedAction)
        {
            _resolved.Add(caster.InstanceID);
            _armed.Remove(caster.InstanceID);
            ++NumCasts;
            ++_collectedThisWave;
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (spell.Action.ID == 48714 && caster.OID == 19651)
        {
            // The same NPCs are reused for the next placement. Contact is armed
            // after transfer, not while they are casting and changing positions.
            _relocating.Add(caster.InstanceID);
            _armed.Remove(caster.InstanceID);
        }
        if (spell.Action.ID is 48713 or 48715)
        {
            _collectedThisWave = 0;
            if (_placedAt.AddSeconds(10) < WorldState.CurrentTime)
                ++_wave;
            if (spell.Action.ID == 48713 || _placedAt.AddSeconds(10) < WorldState.CurrentTime)
                _placedAt = WorldState.CurrentTime;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48714 && caster.OID == 19651u)
            _relocating.Remove(caster.InstanceID);
        // 补充布防路径：48714 的命中事件里 payload 施法者是石像鬼本体[19650]，所以上面那条
        // 按 19651 过滤的 OnEventCast 分支实际上永远不会触发；读条结束的施法者才是池子本体[19651]，
        // 且此时已经转移完毕（符合“只在转移后才布防”的要求）。
        if (spell.Action.ID == 48714 && (spell.EventHappened || spell.NPCRemainingTime <= 0.5f) && caster.OID == 19651u)
        {
            _resolved.Remove(caster.InstanceID);
            _armed.Add(caster.InstanceID);
        }
    }

    // 10 armed balls give the gargoyle 10 stacks of Damage Up (status 0x9F6) through their tethers.
    // Guides (ゆゆ) and the clear video collect a few balls; each touch adds Gradual Panic (5178,
    // 12s) and too many become Panic (4167). Touch one ball at a time while below the stack cap.
    internal const uint GradualPanic = 5178, Panic = 4167;
    internal const int PanicStackCap = 4; // Status sheet: Panic at 5 Gradual Panic stacks.
    internal const int CollectPriority = 90;
    // run1883: a tether ball 32m out (edge 27m) put the gargoyle outside Provoke's 25y, so the
    // pet's protection stayed up and the lamb ate the dash (4498). Keep 2y of slack inside it.
    internal const float TetherProvokeReach = 23f;

    internal Actor? CollectTarget(Actor actor)
    {
        if (PanicCapped(actor))
        {
            _committedBall = 0;
            return null;
        }
        // kano/run1867: the second dash lands right after the balls arm and hits 25–40% harder.
        // While stretching the tether, take balls that also sit past the colour-change distance.
        if (Module.FindComponent<CrucibleGargoyleTetherStretch>() is { Active: true } tether)
        {
            _committedBall = 0;
            return tether.Gargoyle is { } g && tether.SecondsLeft > 2.5 && !PositionalMechanicActive(actor, ignoreTether: true)
                ? Reachable(actor).Where(a => (a.Position - g.Position).Length() is var d
                        && d >= CrucibleGargoyleTetherStretch.SafeTether && d <= g.HitboxRadius + TetherProvokeReach)
                    .MinBy(a => (a.Position - actor.Position).LengthSq())
                : null;
        }
        if (PositionalMechanicActive(actor))
        {
            _committedBall = 0;
            return null;
        }
        // The Pitch Sea chase also binds: the first wave's balls expire before it (42+66s < 119s), but waves 2 and 3
        // overlap chasers that land where the player stands, and a detour to a ball would drop them on the ring approach.
        // Still finish a touch in progress: a ball turning back into an obstacle around a player beside it left no legal
        // start cell (replay: NoReachableSafeCell from the Pitch Sea cast on, standing through the chasers).
        return WorthwhileBall(actor, Reachable(actor).Where(a => Module.FindComponent<CrucibleGargoyleResidue>()?.PitchWave != true || a.Position.InCircle(actor.Position, FinishTouchRadius)).ToArray());
    }
    internal const float FinishTouchRadius = 4f;

    // kano: a ball is only worth the GCDs it costs. b26-ballpath README: one stack less saves the HP below (player + pet,
    // ACT, per-action stack factors) until the next placement; leaving melee costs about 430 damage/s (820 per 2.49s GCD
    // plus autos); 1 HP buys about 3.2 boss damage in the race against the third Grim Fate. Later waves have less left to
    // hit for, and the third is nearly worthless. A sweep is judged as a whole, so one far ball can pay for its neighbours.
    internal const float MeleeLossPerSecond = 430, HpToBossDamage = 3.2f, RunSpeed = 6, MeleeEdgeReach = 3;
    private ulong _committedBall;
    private int _wave;

    internal static float BallValueHp(int wave, double sincePlaced) => wave switch
    {
        <= 1 => sincePlaced < 30 ? 335 : sincePlaced < 50 ? 240 : 175,
        2 => sincePlaced < 40 ? 165 : 120,
        _ => 40
    };

    // Seconds outside melee this ball may add to the trip.
    // run2208/2212 (no 闪躲之书): book runs take 0 from two dashes and half of Grim Fate; without it the player took
    // 14.5–14.9k per try (dashes 7.6–8.4k, Grim Fate 4.3–4.5k at 5–10 stacks) against 6882 HP + all potions, and died to
    // the fourth dash with the boss at 6.9%/12.8%. Each Damage Up stack is about +10% on every hit until the next placement,
    // so with no book HP decides the try, not melee time: value each ball double.
    internal const float NoBookBallFactor = 1;
    private bool _bookSeen;
    private float AllowedDetourSeconds() => BallValueHp(_wave, _placedAt == default ? 0 : (WorldState.CurrentTime - _placedAt).TotalSeconds) * HpToBossDamage / MeleeLossPerSecond
        * (_bookSeen ? 1 : NoBookBallFactor);

    private Actor? WorthwhileBall(Actor actor, Actor[] balls)
    {
        // Keep a chosen ball until it is touched or stops being legal: no switching on the way, no half-run trips.
        var picked = balls.FirstOrDefault(b => b.InstanceID == _committedBall)
            ?? balls.Where(b => b.Position.InCircle(actor.Position, FinishTouchRadius)).MinBy(b => (b.Position - actor.Position).LengthSq());
        if (picked == null)
        {
            var slots = Math.Min(PanicStackCap - (actor.FindStatus(GradualPanic)?.Extra ?? 0), BallsPerWave - _collectedThisWave);
            var allowed = AllowedDetourSeconds();
            var best = 0f;
            foreach (var start in balls)
                if (SweepGain(actor.Position, start, balls, slots, allowed) is var gain && gain >= best)
                    (picked, best) = (start, gain);
        }
        _committedBall = picked?.InstanceID ?? 0;
        return picked;
    }

    // Best (allowance earned - seconds added outside melee) over sweeps that start at this ball and go on to the nearest
    // next one, ending with the walk back to the melee ring. Negative when no such sweep pays for itself.
    private float SweepGain(WPos from, Actor start, Actor[] balls, int slots, float allowed)
    {
        var center = Module.PrimaryActor.Position;
        var reach = Module.PrimaryActor.HitboxRadius + MeleeEdgeReach;
        float Back(WPos p) => MathF.Max(0, (p - center).Length() - reach);
        var left = balls.Where(b => b != start).ToList();
        var (position, outside, gain, count) = (from, 0f, float.MinValue, 0);
        for (var next = start; next != null && count < slots; next = left.MinBy(b => (b.Position - position).LengthSq()))
        {
            left.Remove(next);
            outside += OutsideCircle(position, next.Position, center, reach);
            position = next.Position;
            gain = MathF.Max(gain, ++count * allowed - (outside + Back(position) - Back(from)) / RunSpeed);
        }
        return gain;
    }

    private static float OutsideCircle(WPos a, WPos b, WPos center, float radius)
    {
        var length = (b - a).Length();
        if (length < 0.01f)
            return 0;
        var dir = (b - a) / length;
        var f = a - center;
        var along = f.Dot(dir);
        var disc = along * along - f.LengthSq() + radius * radius;
        if (disc <= 0)
            return length;
        var root = MathF.Sqrt(disc);
        return length - (Math.Clamp(root - along, 0, length) - Math.Clamp(-root - along, 0, length));
    }

    // run1878: pausing for every cast left 2 balls before the second five-hit (panic). Other AOEs
    // only exclude balls inside them; positional mechanics still pause collection.
    private IEnumerable<Actor> Reachable(Actor actor)
    {
        var hazards = Module.Components.OfType<Components.GenericAOEs>().Where(c => c != this)
            .SelectMany(c => c.ActiveAOEs(0, actor).ToArray()).Where(a => a.Risky).ToArray();
        // Residue tick = Panic (agent-panic README): never take a ball next to a live residue or one only reachable through it.
        var residue = Module.FindComponent<CrucibleGargoyleResidue>();
        return Armed().Where(a => !hazards.Any(h => h.Check(a.Position))
            && residue?.Covers(a.Position, ResidueBallClearance) != true && residue?.Reaches(actor.Position, a.Position, ResidueBallClearance) != false);
    }
    internal const float ResidueBallClearance = 1.5f;

    // run1879 cleared all 10 balls and still panicked: Panic (4167) lands when the second
    // five-hit combo (~83s after placement) hits a player who still carries Gradual Panic (12s).
    // run1870 touched none and never panicked. Collect only in the first 66s after placement.
    internal const int BallsPerWave = 10;
    internal const double CollectWindowSeconds = 66;
    private int _collectedThisWave;
    private DateTime _placedAt;
    // Grim Fate does not change this rule (agent-panic README): in 14 fights no Panic ever coincided with a Gradual
    // Panic gain, and 42/42 Panics came from a pitch residue tick. run1888's "second-wave touches become Panic" was
    // residue contact. Waves 2 and 3 are collected too: 10 balls hold the boss at about x1.45 damage (agent-b26-dps).
    private bool PanicCapped(Actor actor)
        => actor.FindStatus(Panic) != null || (actor.FindStatus(GradualPanic)?.Extra ?? 0) >= PanicStackCap
            || _collectedThisWave >= BallsPerWave
            || _placedAt != default && (WorldState.CurrentTime - _placedAt).TotalSeconds > CollectWindowSeconds;
    private IEnumerable<Actor> Armed() => _armed.Select(id => WorldState.Actors.Find(id)).Where(a => a is { IsDeadOrDestroyed: false })!;
    internal IEnumerable<Actor> ArmedBalls() => Armed();

    // Only collect in quiet windows: steel/ring, the charge cleaves, the five-hit knockback
    // and the tether all need the player's position and must keep every ball an obstacle.
    // The five-hit pocket only binds in its last seconds; before the first shock wave (~4s after
    // 50695 starts, ~8s before the push) a remaining ball outranks the pocket.
    internal const double KnockbackCollectSeconds = 6.0;
    private bool PositionalMechanicActive(Actor actor, bool ignoreTether = false)
        => Module.FindComponent<CrucibleGargoyleInOut>()?.ActiveAOEs(0, actor).Length > 0
            || Module.FindComponent<CrucibleGargoyleCharge>()?.ActiveAOEs(0, actor).Length > 0
            || Module.FindComponent<CrucibleGargoyleKnockback>()?.ActiveKnockbacks(0, actor).ToArray()
                .Any(k => (k.Activation - WorldState.CurrentTime).TotalSeconds <= KnockbackCollectSeconds) == true
            || !ignoreTether && Module.FindComponent<CrucibleGargoyleTetherStretch>()?.Active == true;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var target = CollectTarget(actor);
        foreach (var id in _armed)
            if (WorldState.Actors.Find(id) is { IsDeadOrDestroyed: false } mine && mine != target)
                hints.TemporaryObstacles.Add(new SDCircle(mine.Position, ContactRadius));
        if (target != null && (Module.FindComponent<CrucibleGargoyleTetherStretch>() is not { Active: true } tether
                || !tether.WaitToStretch(target.Position, actor)))
            hints.GoalPoints.Add((target.Position, 1f, CollectPriority));
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        var armed = false;
        foreach (var position in ArmedPositions())
        {
            armed = true;
            if (actor.Position.InCircle(position, ContactRadius))
            {
                hints.Add(WarningText, true);
                return;
            }
        }
        if (armed)
            hints.Add(WarningText, false);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        _armed.Remove(actor.InstanceID);
        _relocating.Remove(actor.InstanceID);
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// EObj 0x1EC0D2 shows its type ~10s before impact. 00010002 -> circle R8;
// 00400080 -> donut 3-11. 00040008/00040100 are their final animation phases.
// All six final casts last only 0.7s, so use EAnim for preparation and reconcile
// against the real cast position/time when available (also supports late loading).
sealed class CrucibleRockRain(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    private const uint RockOID = 0x1EC0D2;
    private static readonly AOEShapeCircle Circle = new(8f);
    private static readonly AOEShapeDonut Donut = new(3f, 11f);
    private readonly Dictionary<ulong, AOEInstance> _predicted = [];
    private readonly HashSet<ulong> _resolved = [];
    private readonly List<AOEInstance> _displayed = [];

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        48757 => new(Circle, true),
        48758 => new(Donut, true),
        _ => null
    };

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        foreach (var aoe in base.ActiveAOEs(slot, actor))
            _displayed.Add(aoe);
        foreach (var (id, aoe) in _predicted.ToArray())
        {
            if (aoe.Activation.AddSeconds(2d) < WorldState.CurrentTime)
                _predicted.Remove(id);
            else if (!_displayed.Any(a => a.Origin.AlmostEqual(aoe.Origin, 0.5f)))
                _displayed.Add(aoe);
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID != RockOID || _resolved.Contains(actor.InstanceID))
            return;
        AOEShape? shape = state switch
        {
            0x00010002 or 0x00040008 => Circle,
            0x00400080 or 0x00040100 => Donut,
            _ => null
        };
        if (shape == null)
            return;
        var finalPhase = state is 0x00040008 or 0x00040100;
        var activation = WorldState.FutureTime(finalPhase ? 3.1d : 10d);
        if (_predicted.TryGetValue(actor.InstanceID, out var previous) && previous.Activation < activation)
            activation = previous.Activation; // Duplicate/reordered EAnim must not postpone a warning.
        _predicted[actor.InstanceID] = new(shape, actor.Position, activation: activation, actorID: actor.InstanceID);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (ConfigFor(spell.Action.ID) != null)
            ResolveAt(spell.LocXZ);
        base.OnCastStarted(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (ConfigFor(spell.Action.ID) != null)
            ResolveAt(caster.Position);
        base.OnEventCast(caster, spell);
    }

    private void ResolveAt(WPos position)
    {
        foreach (var rock in Module.Enemies(RockOID))
            if (rock.Position.AlmostEqual(position, 0.5f))
            {
                _predicted.Remove(rock.InstanceID);
                _resolved.Add(rock.InstanceID);
            }
    }

    public override void OnActorCreated(Actor actor) => _resolved.Remove(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor)
    {
        _predicted.Remove(actor.InstanceID);
        _resolved.Remove(actor.InstanceID);
        base.OnActorDestroyed(actor);
    }

    public override void OnActorEState(Actor actor, ushort state)
    {
        if (actor.OID == RockOID && state == 7)
        {
            _predicted.Remove(actor.InstanceID);
            _resolved.Add(actor.InstanceID);
        }
    }

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// 48748 starts the two rows of walls. 48751 sweeps the 10y central corridor
// 13s later without a cast. The recorded arena and attack are fixed north/south.
// MapEffects 17..28 enable the twelve 5x5 footprints, in pairs from north to south.
sealed class CrucibleRockWalls(BossModule module) : Components.GenericAOEs(module, 48751u)
{
    private static readonly AOEShapeRect Collapse = new(30f, 5f);
    private static readonly AOEShapeRect Wall = new(2.5f, 2.5f, 2.5f);
    private readonly Dictionary<byte, WPos> _walls = [];
    private AOEInstance? _collapse;
    private ulong _casterID;
    private readonly List<AOEInstance> _aoes = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_collapse is { } expired && expired.Activation.AddSeconds(2d) < WorldState.CurrentTime)
            _collapse = null;
        _aoes.Clear();
        if (_collapse is { } collapse)
            _aoes.Add(collapse);
        foreach (var position in _walls.Values)
            _aoes.Add(new(Wall, position));
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48748 && !spell.EventHappened)
        {
            _casterID = caster.InstanceID;
            _collapse = new(Collapse, Module.Center - new WDir(0f, 15f), activation: Module.CastFinishAt(spell, 10d));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48748 && spell.Interrupted)
            _collapse = null;
    }

    public override void OnMapEffect(byte index, uint state)
    {
        if (index is < 17 or > 28)
            return;
        if (state == 0x00020001)
        {
            var offset = index - 17;
            _walls[index] = Module.Center + new WDir(offset % 2 == 0 ? 7.5f : -7.5f, -12.5f + 5f * (offset / 2));
        }
        else if (state == 0x00080004)
            _walls.Remove(index);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            _collapse = null;
            _walls.Clear();
            ++NumCasts;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == _casterID)
        {
            _collapse = null;
            _walls.Clear(); // 48751 命中事件由 9020 辅助体发出，可能缺失；施法者消失时一并清掉石墙脚印
        }
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        _ = ActiveAOEs(slot, actor);
        if (_collapse is { } collapse)
            CrucibleScorpionAI.Avoid(hints, collapse);
        foreach (var position in _walls.Values)
            hints.TemporaryObstacles.Add(Wall.Distance(position, default));
    }
}

// Effect payloads reference Knockback rows 3 (20y) and 243 (17y), both away
// from the source. Use the safe field, not the larger electric-fence display bounds.
abstract class HighCrucibleKnockback(BossModule module, uint actionID, float distance) : Components.SimpleKnockbacks(module, actionID, distance)
{
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        // Cast completion can precede the effect packet; keep the landing constraint until impact.
        if (spell.Action.ID == WatchedAction && spell.Interrupted)
            Casters.RemoveAll(kb => kb.ActorID == caster.InstanceID);
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        Casters.RemoveAll(kb => kb.Activation.AddSeconds(2d) < WorldState.CurrentTime);
        return base.ActiveKnockbacks(slot, actor);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        Casters.RemoveAll(kb => kb.ActorID == caster.InstanceID);
        base.OnCastStarted(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
            Casters.RemoveAll(kb => kb.ActorID == caster.InstanceID);
        base.OnEventCast(caster, spell);
    }

    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(kb => kb.ActorID == actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

sealed class CrucibleGargoyleKnockback(BossModule module) : HighCrucibleKnockback(module, 48725u, 20f)
{
    private int _predictionStage;

    // The fifth hit arrives 12s after 50695 starts (four ARR sequences).
    // Predict from the opener, then replace it with the final helper cast.
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return;
        var (stage, delay) = spell.Action.ID switch
        {
            50695 => (1, 9.5),
            50696 => (2, 9.0),
            48724 => (3, 0.6),
            48725 => (4, 0.0),
            _ => (0, 0.0)
        };
        if (stage == 0)
            return;
        _ = ActiveKnockbacks(0, caster);
        if (Casters.Count > 0 && stage < _predictionStage)
            return;
        var activation = Module.CastFinishAt(spell, delay);
        if (activation <= WorldState.CurrentTime)
            return;
        Casters.Clear();
        Casters.Add(new(spell.LocXZ, Distance, activation, actorID: caster.InstanceID));
        _predictionStage = stage;
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        // Keep the landing constraint through normal cast end until the hit.
        if (spell.Interrupted
            && spell.Action.ID is 50695 or 50696 or 48724 or 48725)
            Casters.Clear();
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48725)
        {
            Casters.Clear();
            _predictionStage = 0;
            ++NumCasts;
        }
        else if (spell.Action.ID == 50696 && Casters.Count == 0)
        {
            Casters.Add(new(caster.Position, Distance, WorldState.FutureTime(9), actorID: caster.InstanceID));
            _predictionStage = 2;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == 19650)
            Casters.Clear();
        else
            base.OnActorDestroyed(actor);
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        if (ActiveKnockbacks(0, Module.PrimaryActor).Length > 0)
            hints.Add("五连斩最后一击20米击退：提前靠中，避开瘴气与电网！");
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
    {
        if (!pos.InRect(Module.Center, 19.7f, 19.7f))
            return true;
        return Module.FindComponent<CrucibleGargoyleResidue>()?.Covers(pos, 0.5f) == true
            || (Module.FindComponent<CrucibleMiasma>()?.ArmedPositions()
                .Any(p => pos.InCircle(p, CrucibleMiasma.ContactRadius)) ?? false);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var positions = Module.FindComponent<CrucibleMiasma>()?.ArmedPositions().ToArray() ?? [];
        var residues = Module.FindComponent<CrucibleGargoyleResidue>()?.Live().ToArray() ?? [];
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
            {
                var route = new SafeRoute(Module.Center, kb.Origin, Distance, positions, residues);
                // The ~1.3y diagonal pockets are narrower than the hard safety margin;
                // only an explicit goal lets the solver use their interior.
                // run1873: Pitch Sea (48729) chases the player three times, 2s apart. Holding the
                // pocket fed it every puddle; kite freely until the last 3.5s before the push.
                // The chasers start ~4s after the Pitch Sea (48728) cast; leave the pocket during that cast
                // already, or the first residue lands on it (agent-panic README).
                var pitch = Module.FindComponent<CrucibleGargoyleResidue>()?.PitchWave == true;
                var kiting = pitch
                    && (kb.Activation - WorldState.CurrentTime).TotalSeconds > PocketReturnSeconds
                    // run1878: a ball left at the first shock wave means Panic; clear it before holding the pocket.
                    || (kb.Activation - WorldState.CurrentTime).TotalSeconds > CrucibleMiasma.KnockbackCollectSeconds
                        && Module.FindComponent<CrucibleMiasma>()?.CollectTarget(actor) != null;
                // Chasing-puddle replays of run1899/1902: with the landing constraint registered 12s early, a puddle
                // pending under the player left no legal cell (everything but the pockets is forbidden) and the
                // player froze through two puddles. While kiting, the puddles alone steer; the constraint returns
                // with the pocket goal for the final seconds.
                if (!kiting)
                {
                    // run1861 ten-ball layout: one residue can cover the whole corner of the only mine-safe
                    // diagonal. Keeping it binding left no reachable cell, MP released control and the push
                    // went into the fence or a ball. Landing in Pitch Sea is the least bad outcome left, so
                    // drop the residue from the hard constraint only when no residue-free pocket exists.
                    // The sealed key avoids rescanning both routes every frame while the seal holds.
                    var routeKey = route.ContentKey();
                    var pocket = routeKey == _sealedRouteKey ? null : Pocket(route, slot, actor);
                    if (pocket == null && residues.Length > 0)
                    {
                        _sealedRouteKey = routeKey;
                        route = new SafeRoute(Module.Center, kb.Origin, Distance, positions, []);
                        pocket = Pocket(route, slot, actor);
                    }
                    hints.AddForbiddenConstraint(route, kb.Activation.AddSeconds(-1));
                    if (pocket is { } held)
                        hints.GoalPoints.Add((held, PocketRadius, PocketPriority));
                }
                // agent-panic README: each chaser leaves a 33s pitch residue where the player stood at its cast
                // start, and the residues under the pocket caused the second five-hit Panic in 12/14 fights.
                // Kite on a ≥13y circle around the push origin, ≥45° off the pocket's push line, so the residues
                // land outside every pocket (all within ~8y) and off the 20y knockback path.
                if (kiting && pitch && KitePoint(route, kb.Origin, slot, actor) is { } kite)
                    hints.GoalPoints.Add((kite, KiteRadius, KitePriority));
            }
        // Pitch Sea before the five-hit prediction exists: kite around the boss as well.
        if (ActiveKnockbacks(slot, actor).Length == 0
            && Module.FindComponent<CrucibleGargoyleResidue>()?.PitchCastWindow == true
            && Module.FindComponent<CrucibleGargoyleInOut>()?.ActiveAOEs(slot, actor).Length is null or 0
            && KitePoint(null, Module.PrimaryActor.Position, slot, actor) is { } early)
            hints.GoalPoints.Add((early, KiteRadius, KitePriority));
    }

    internal const float KiteDistance = 14f, KiteRadius = 1f;
    internal const float KiteLineClearance = 31 * MathF.PI / 180; // a residue (R6.5) at 14y stays ≥7.2y off a push line 31° away
    internal const int KitePriority = 60;
    private WPos? KitePoint(SafeRoute? route, WPos origin, int slot, Actor actor)
    {
        // Keep every candidate pocket's push line free, not just the one nearest the player now: the final pocket
        // is picked from where the kiting ends (B26 replay: residue on the chosen push line, landing walled in).
        var mines = Module.FindComponent<CrucibleMiasma>()?.ArmedPositions().ToArray() ?? [];
        // Before the five-hit prediction exists the push origin is the boss itself: the pockets are already known.
        // Protect every mine-safe push line while kiting, including lines whose landing is
        // temporarily covered by a previous residue. Otherwise changing the hard constraint
        // shrinks this set and the next chaser can seal the future return pocket.
        Pocket(new SafeRoute(Module.Center, origin, Distance, mines, []), slot, actor);
        var lines = _pockets.Where(p => (p.Point - origin).LengthSq() > 0.01f)
            .Select(p => Angle.FromDirection(p.Point - origin).Rad).ToArray();
        bool Blocked(float angle) => lines.Any(line => MathF.Abs(MathF.IEEERemainder(angle - line, 2 * MathF.PI)) < KiteLineClearance);
        var hazards = Module.Components.OfType<Components.GenericAOEs>().Where(c => c is not CrucibleBoundary)
            .SelectMany(c => c.ActiveAOEs(slot, actor).ToArray()).Where(a => a.Risky)
            .Select(a => a.Shape.Distance(a.Origin, a.Rotation)).ToArray();
        // Walk around the circle, never across a push line: a chaser cast while crossing drops its residue on it.
        var from = Angle.FromDirection(actor.Position - origin).Rad;
        bool ArcFree(float to)
        {
            if (Blocked(from) || (actor.Position - origin).Length() < 9)
                return true;
            var delta = MathF.IEEERemainder(to - from, 2 * MathF.PI);
            for (var k = 1; k <= 12; ++k)
                if (Blocked(from + delta * k / 12))
                    return false;
            return true;
        }
        return Enumerable.Range(0, 72)
            .Select(i => origin + KiteDistance * (i * 5f).Degrees().ToDirection())
            .Where(p => p.InRect(Module.Center, 17.5f, 17.5f) && (p - Module.Center).Length() >= 13
                && !Blocked(Angle.FromDirection(p - origin).Rad) && ArcFree(Angle.FromDirection(p - origin).Rad)
                && hazards.All(h => h.Distance(p) > 1.5f)
                && mines.All(m => !p.InCircle(m, CrucibleMiasma.ContactRadius + 1)))
            .Select(p => (WPos?)p).MinBy(p => (p!.Value - actor.Position).LengthSq());
    }

    internal const float PocketRadius = 0.5f;
    internal const double PocketReturnSeconds = 3.5;
    private string? _sealedRouteKey;
    internal const int PocketPriority = 180;
    private string? _pocketKey;
    private (WPos Point, float Clearance)[] _pockets = [];

    // Safe samples with the widest clearance; prefer the one nearest the player.
    private WPos? Pocket(SafeRoute route, int slot, Actor actor)
    {
        var key = route.ContentKey();
        if (key != _pocketKey)
        {
            _pocketKey = key;
            var found = new List<(WPos, float)>();
            for (var z = -8f; z <= 8f; z += 0.1f)
                for (var x = -8f; x <= 8f; x += 0.1f)
                {
                    var p = Module.Center + new WDir(x, z);
                    if (route.Distance(p) <= 0)
                        continue;
                    var clearance = 0.8f;
                    for (var r = 0.2f; r <= 0.8f && clearance == 0.8f; r += 0.2f)
                        for (var i = 0; i < 12; ++i)
                            if (route.Distance(p + (i * 30f).Degrees().ToDirection() * r) <= 0)
                            {
                                clearance = r - 0.2f;
                                break;
                            }
                    if (clearance >= 0.4f)
                        found.Add((p, clearance));
                }
            _pockets = found.ToArray();
        }
        if (_pockets.Length == 0)
            return null;
        // run1872: two Pitch Seas (48729, R6) landed on the held pocket during the five-hit
        // combo; each added Damage Down + Gradual Panic until Panic ran the player into the fence.
        // Prefer pockets outside every pending risky AOE, then the widest, then the nearest.
        var hazards = Module.Components.OfType<Components.GenericAOEs>()
            .SelectMany(c => c.ActiveAOEs(slot, actor).ToArray()).Where(a => a.Risky).ToArray();
        var clear = _pockets.Where(p => !hazards.Any(h => h.Check(p.Point)
            || Enumerable.Range(0, 8).Any(i => h.Check(p.Point + (i * 45f).Degrees().ToDirection() * 0.8f)))).ToArray();
        // B26 replay with pitch residues: a push across a residue lands the player between it and the fence, walled
        // in until the next steel/ring (NoTemporalPath for 8s). Prefer pockets whose 20y push path clears them.
        // The landing must also keep a residue-free walk back to the boss's 13.5y staging circle.
        if (Module.FindComponent<CrucibleGargoyleResidue>() is { } residue && residue.Live().ToArray() is { Length: > 0 } residues
            && clear.Length > 0 && ActiveKnockbacks(slot, actor).ToArray() is { Length: > 0 } pushes)
        {
            var origin = pushes[0].Origin;
            var reach = new Dictionary<(int, int), bool>();
            var open = clear.Where(p =>
            {
                var dir = (p.Point - origin).Normalized();
                var landing = p.Point + Distance * dir;
                var key = ((int)MathF.Round(landing.X), (int)MathF.Round(landing.Z));
                if (!reach.TryGetValue(key, out var ok))
                    reach[key] = ok = !residue.Covers(landing, 0.5f) && residue.Reaches(landing, Module.PrimaryActor.Position, 13.5f);
                return ok && residues.All(r => Math.Clamp((r - p.Point).X * dir.X + (r - p.Point).Z * dir.Z, 0, Distance) is var along
                    && !r.InCircle(p.Point + along * dir, CrucibleGargoyleResidue.Radius + 0.5f));
            }).ToArray();
            if (open.Length > 0)
                clear = open;
        }
        var pool = clear.Length > 0 ? clear : _pockets;
        var widest = pool.Max(p => p.Clearance);
        return pool.Where(p => p.Clearance >= widest - 0.2f).MinBy(p => (p.Point - actor.Position).LengthSq()).Point;
    }

    // Check the whole displacement segment so the route cannot clip a ball
    // even if its final landing point is clear. Use contact clearance only.
    // run1861: the safe pockets are ~1y diagonal slivers near the boss; the default
    // 0.5y conservative raster erased them (NoReachableSafeCell, MP released control).
    private sealed class SafeRoute(WPos center, WPos origin, float distance, WPos[] mines, WPos[] residues) : ShapeDistance
    {
        private FrozenConstraintGeometry? _geometry;
        public override MasterPlayer.Spatial.Geometry.IGeometryShape Geometry(float y)
            => _geometry != null && _geometry.Position.Y == y ? _geometry
                : _geometry = FrozenConstraintFactory.Create(p => Distance(p), center, y, radius: 21, step: 0.1f);
        public override string? ContentKey()
            => $"GKB|{origin.X:F2},{origin.Z:F2}|{distance:F1}|{string.Join(';', mines.Select(m => $"{m.X:F1},{m.Z:F1}"))}|pitch:{string.Join(';', residues.Select(r => $"{r.X:F1},{r.Z:F1}"))}";

        public override float Distance(in WPos p)
        {
            var offset = p - origin;
            var length = offset.Length();
            if (length < 0.5f)
                return 0;
            var direction = offset / length;
            var landing = p + distance * direction;
            if (!landing.InRect(center, 19.7f, 19.7f))
                return 0;
            foreach (var mine in mines)
            {
                var toMine = mine - p;
                var along = Math.Clamp(toMine.X * direction.X + toMine.Z * direction.Z, 0, distance);
                if (mine.InCircle(p + along * direction, CrucibleMiasma.ContactRadius))
                    return 0;
            }
            // run2031-battle4: a legal fence/ball pocket still pushed into live Pitch Sea.
            // Knockback resolves at the landing point; preserve narrow diagonal routes
            // while making a residue-free landing binding even when a soft goal falls back.
            foreach (var residue in residues)
                if (landing.InCircle(residue, CrucibleGargoyleResidue.Radius + 0.5f))
                    return 0;
            return 1;
        }
    }
}

sealed class CrucibleGolemKnockback(BossModule module) : HighCrucibleKnockback(module, 50649u, 17f)
{
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InRect(Module.Center, 20f, 15f);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
                hints.AddForbiddenConstraint(new SDKnockbackInAABBRectAwayFromOrigin(
                    Module.Center, kb.Origin, Distance, 19.7f, 14.7f), kb.Activation.AddSeconds(-0.25d));
    }
}

// 48809/48810 leave EObj 0x1EB704 (b0371). These objects appear before the
// next damage effect and persist; do not clear them when the boss finishes casting.
// Routing reacts immediately; hazards wait 2 seconds except for the fourth confirmed bait.
sealed class CrucibleDragonPoison(BossModule module) : Components.GenericAOEs(module, warningText: "离开毒池！")
{
    internal const uint PoisonOID = 0x1EB704;
    private static readonly AOEShapeCircle Shape = new(6f);
    private readonly Dictionary<Actor, DateTime> _activations = [];

    public void ActivateImmediately(Actor pool) => _activations[pool] = WorldState.CurrentTime;

    private void ObservePool(Actor actor)
    {
        if (actor.OID != PoisonOID)
            return;
        if (actor.EventState == 7 || actor.IsDeadOrDestroyed)
            _activations.Remove(actor);
        else
            _activations.TryAdd(actor, WorldState.FutureTime(2));
    }

    public override void Update()
    {
        foreach (var pool in Module.Enemies(PoisonOID))
            ObservePool(pool);
    }

    public override void OnActorCreated(Actor actor) => ObservePool(actor);
    public override void OnActorEventStateChange(Actor actor, byte value) => ObservePool(actor);
    public override void OnActorDeath(Actor actor) => _activations.Remove(actor);
    public override void OnActorDestroyed(Actor actor) => _activations.Remove(actor);

    private readonly List<AOEInstance> _aoes = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        foreach (var (pool, activation) in _activations)
            if (WorldState.CurrentTime >= activation && pool.EventState != 7 && !pool.IsDeadOrDestroyed)
                _aoes.Add(new(Shape, pool.Position, pool.Rotation));
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Keep waiting pools out of navigation entirely: a future forbidden zone
        // could make the AI leave early instead of following the next bait point.
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            hints.TemporaryObstacles.Add(new SDCircle(aoe.Origin, Shape.Radius));
    }
}

// Landing hits outside first (48816), then the R6 center (48828) ~1.5s later.
// The donut's inner radius is not in the client table. Keep the confirmed center
// preview non-risky until the outer hit, so it doesn't push players out too early.
// run2033: 48819 lands where the player stood when it started, 5.3s into 48817, and spawns the poison add there. It walks to
// the nearest pool at ~1y/s and explodes for ~2000 within 5y of it (9y away: 7.6s, 14y: 12s). From 28y it took 22s, long
// enough to kill its 23692 HP. Be as far from every pool as the arena allows when the spit starts.
sealed class CrucibleDragonSpit(BossModule module) : BossComponent(module)
{
    internal const uint Slime = 48817u, Spit = 48819u;
    internal const int Priority = 110;
    private const float StandRadius = 1.5f, Step = 2f, FenceMargin = 2f;
    private DateTime _until;
    private WPos? _stand;
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == Slime && !spell.EventHappened)
            (_until, _stand) = (Module.CastFinishAt(spell), null);
        else if (spell.Action.ID == Spit)
        {
            _until = default;
            (_spitAt, _spitLoc) = (Module.CastFinishAt(spell, 0.5), spell.LocXZ);
        }
    }

    // run2210 04:40:37: the pet followed the player to the stand point and took the R6 spit (粘液怪 2867).
    // From the slime cast until the spit lands, send the pet out of the R6 circle toward the boss.
    private DateTime _spitAt;
    private WPos? _spitLoc;
    private const float PetSpitClearance = 9f;
    private void MovePetOutOfSpit(Actor actor, AIHints hints)
    {
        var spot = _spitAt > WorldState.CurrentTime ? _spitLoc : _until > WorldState.CurrentTime ? _stand ?? actor.Position : null;
        if (spot is not { } s || Module.PrimaryActor.IsDeadOrDestroyed)
            return;
        var toBoss = Module.PrimaryActor.Position - s;
        var length = toBoss.Length();
        var dir = length > 0.5f ? toBoss * (1f / length) : (Module.Center - s).LengthSq() > 0.25f ? (Module.Center - s).Normalized() : new WDir(0, 1);
        var target = s + dir * Math.Max(PetSpitClearance, length - 3f);
        var fromCenter = target - Module.Center;
        if (fromCenter.Length() > ArenaRadius - FenceMargin)
            target = Module.Center + fromCenter.Normalized() * (ArenaRadius - FenceMargin);
        hints.PetMoveTo = target;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        MovePetOutOfSpit(actor, hints);
        if (_until <= WorldState.CurrentTime)
            return;
        var pools = Module.Enemies(CrucibleDragonPoison.PoisonOID).Where(p => p.EventState != 7 && !p.IsDeadOrDestroyed).Select(p => p.Position).ToArray();
        if (pools.Length == 0)
            return;
        // run2054: pools placed after the slime started left the frozen point right beside one, so the add blew up at once.
        if (_stand is not { } current || pools.Any(p => (current - p).Length() < RepickDistance))
            _stand = Farthest(pools);
        if (_stand is { } stand)
            hints.GoalPoints.Add((stand, StandRadius, Priority));
    }
    private const float RepickDistance = 12f;
    // run2206 03:14:07: the single farthest cell was 25y from the boss (13.7y walked out, the same back).
    // kano: the add must still be led far from the pools. Accept any cell within PoolSlack of the farthest
    // pool distance (add walks ~1y/s, so <=3s less walk) and take the one nearest the boss.
    private const float PoolSlack = 3f;
    private WPos? Farthest(WPos[] pools)
    {
        var radius = ArenaRadius - FenceMargin;
        var cells = new List<(WPos P, float D)>();
        var bestDistance = 0f;
        for (var x = -radius; x <= radius; x += Step)
            for (var z = -radius; z <= radius; z += Step)
            {
                var p = Module.Center + new WDir(x, z);
                if ((p - Module.Center).Length() > radius)
                    continue;
                var distance = pools.Min(o => (p - o).Length());
                cells.Add((p, distance));
                bestDistance = Math.Max(bestDistance, distance);
            }
        if (cells.Count == 0)
            return null;
        var boss = Module.PrimaryActor;
        var candidates = cells.Where(c => c.D >= bestDistance - PoolSlack);
        return boss.IsDeadOrDestroyed
            ? cells.MaxBy(c => c.D).P
            : candidates.MinBy(c => (c.P - boss.Position).LengthSq()).P;
    }
    private const float ArenaRadius = 19.75f;
}

sealed class CrucibleDragonLanding(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    private DateTime _outerActivation;
    private bool _outerPending;
    private ulong _outerCasterID;
    private WPos _outerOrigin;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == 48828
        ? new(new AOEShapeCircle(6f), true) : null;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _ = base.ActiveAOEs(slot, actor);
        var waitForOuter = _outerPending && WorldState.CurrentTime <= _outerActivation.AddSeconds(0.5d);
        foreach (var pending in Pending)
            pending.AOE.Risky = !waitForOuter;
        // Base returns value copies, so refresh after changing the pending risk flags.
        return base.ActiveAOEs(slot, actor);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48816 && !spell.EventHappened)
        {
            _outerPending = true;
            _outerCasterID = caster.InstanceID;
            _outerActivation = Module.CastFinishAt(spell);
            _outerOrigin = spell.LocXZ;
        }
        base.OnCastStarted(caster, spell);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48816 && spell.Interrupted)
            _outerPending = false;
        base.OnCastFinished(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48816)
            _outerPending = false;
        base.OnEventCast(caster, spell);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == _outerCasterID)
            _outerPending = false;
        base.OnActorDestroyed(actor);
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_outerPending && WorldState.CurrentTime <= _outerActivation.AddSeconds(0.5))
        {
            // Conservative inner destination, not a claim about the unknown
            // ring's exact inner edge. Prefer the outside of the inner pocket
            // promptly after the outer damage packet releases this constraint.
            hints.AddForbiddenConstraint(new SDInvertedCircle(_outerOrigin, 5), _outerActivation.AddSeconds(-0.5), _outerActivation.AddSeconds(0.2));
            // Stage near the inner rim before the ring: leaving the exact center
            // after the packet cannot absorb route/dispatch latency before 48828.
            // This is a positioning constraint, not additional damage geometry.
            hints.AddForbiddenConstraint(new SDCircle(_outerOrigin, 3.2f), _outerActivation.AddSeconds(-0.5), _outerActivation.AddSeconds(0.2));
            // run1856 replay with the cloud capsules: the 48814 clouds radiate from the centre during
            // this cast, and a weak goal zone left the player 18y out, behind them, until the outer hit.
            // Head inward from cast start; the clouds have left the centre by the outer hit.
            hints.GoalPoints.Add((_outerOrigin, 4, 100));
        }
        base.AddAOEForbiddenZones(slot, actor, assignment, hints);
    }
}

// Cast hints cover confirmed non-geometric mechanics without treating raidwides,
// pulls, or unknown donut/cone dimensions as an all-arena forbidden zone.
sealed class CrucibleOwlGustHint(BossModule module) : Components.CastHints(module, [48667u], "暴风：准备击退，注意电网边界！");

// ARR 18:02:48 effect 31 references Knockback row 242: 25y, away from source.
// This exceeds the arena radius; prioritize the feather before cast completion.
sealed class CrucibleOwlGust(BossModule module) : HighCrucibleKnockback(module, 48667u, 25f)
{
    internal const uint FeatherOid = 19640u;
    // run1896: landing computed at 18.5y still bled (feather at 10y, player 3.5y behind it); keep 2.5y slack.
    internal const float SafeRadius = 17f;
    // run2201 00:51:39: feather knocked to the far side (pocket 15.5y away). The 4s cap started the walk 4s out,
    // MP found no path that reached the pocket by the constraint deadline (activation-1s), released, and the 25y
    // push landed in the fence (bleed). 2194 hit the same cast with a 12y pocket and made it. Scale the lead with
    // distance (2s + d/5) and only cap it at 7s so a far pocket still starts early enough.
    internal const double PocketLeadSeconds = 7, PocketArrivalSeconds = 2;
    private const float PocketWalkSpeed = 5f;
    private bool _pocketStarted;
    internal const float PocketRadius = 0.6f;
    internal const int PocketPriority = 180;
    private string? _pocketKey;
    private WPos[] _pockets = [];

    // The feather spawns at the centre, so its 25y push always crosses the R20 fence.
    // Guide (インフィ): Diremite's release knocks the feather out first; then stand
    // between the feather and the centre. Track the feather's live position, not the
    // cast-start snapshot, and steer into the pocket that lands inside the fence.
    // run1868: the cast bar ends ~0.5s before the push lands; dropping the hint at cast end
    // let melee follow pull the player back to the centre. Hold until the gust event.
    private Actor? _feather;
    private DateTime _activation;
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (spell.Action.ID == WatchedAction && caster.OID == FeatherOid)
            (_feather, _activation, _pocketStarted) = (caster, Module.CastFinishAt(spell), false);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == WatchedAction && caster == _feather)
            _feather = null;
    }
    private Actor? Feather()
    {
        if (_feather is { IsDeadOrDestroyed: true } || _activation.AddSeconds(2) < WorldState.CurrentTime)
            _feather = null;
        return _feather;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Feather() is not { } feather || IsImmune(slot, _activation))
            return;
        var activation = _activation;
        var landing = new Landing(Module.Center, feather.Position, Distance, SafeRadius);
        var key = $"{feather.Position.X:F1},{feather.Position.Z:F1}";
        if (key != _pocketKey)
        {
            _pocketKey = key;
            var found = new List<(WPos P, float C)>();
            for (var z = -20f; z <= 20f; z += 0.25f)
                for (var x = -20f; x <= 20f; x += 0.25f)
                {
                    var p = Module.Center + new WDir(x, z);
                    if (!p.InCircle(Module.Center, SafeRadius) || landing.Distance(p) <= 0)
                        continue;
                    var clearance = 1f;
                    for (var r = 0.25f; r <= 1f && clearance == 1f; r += 0.25f)
                        for (var i = 0; i < 12; ++i)
                            if (landing.Distance(p + (i * 30f).Degrees().ToDirection() * r) <= 0) { clearance = r - 0.25f; break; }
                    found.Add((p, clearance));
                }
            var best = found.Count == 0 ? 0 : found.Max(f => f.C);
            _pockets = found.Where(f => f.C >= best).Select(f => f.P).ToArray();
        }
        // While the feather is still central no landing is safe; a full-field ban would only
        // make the planner drop the falling-book dodges, so constrain only once a pocket exists.
        if (_pockets.Length == 0)
            return;
        // kano 1897: walking to the pocket as soon as the feather moved lost ~8s of boss uptime.
        // The pocket sits within a few yards of the boss's feather side; go only for the last seconds.
        // run2031: a fixed 4s lead left the owl 3.4s before the push for a pocket 3y away.
        var pocket = _pockets.MinBy(p => (p - actor.Position).LengthSq());
        var lead = Math.Min(PocketLeadSeconds, PocketArrivalSeconds + (pocket - actor.Position).Length() / PocketWalkSpeed);
        if ((activation - WorldState.CurrentTime).TotalSeconds > lead && !_pocketStarted)
            return;
        _pocketStarted = true;
        hints.AddForbiddenConstraint(landing, activation.AddSeconds(-1));
        hints.GoalPoints.Add((pocket, PocketRadius, PocketPriority));
    }

    internal sealed class Landing(WPos center, WPos origin, float distance, float radius) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            var offset = p - origin;
            var length = offset.Length();
            if (length < 0.5f)
                return -1;
            return radius - (p + offset / length * distance - center).Length();
        }
        public override string? ContentKey() => $"OWL|{origin.X:F2},{origin.Z:F2}|{distance:F1}|{radius:F1}";
    }
}

sealed class CrucibleOwlGustPriority(BossModule module) : BossComponent(module)
{
    private IEnumerable<Actor> Sources() => Module.Enemies(19640).Where(a => !a.IsDeadOrDestroyed
        && a.IsTargetable && a.CastInfo is { Action.ID: 48667, EventHappened: false } cast
        && Module.CastFinishAt(cast).AddSeconds(0.5) >= WorldState.CurrentTime);
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var source in Sources())
            if (hints.FindEnemy(source) is { Priority: >= 0 } enemy)
                enemy.Priority = Math.Max(enemy.Priority, 8);
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Sources().Any())
            hints.Add("暴风：25米击退，优先击杀正在读条的羽毛！", true);
    }
}
sealed class CrucibleOwlChapterHint(BossModule module) : Components.CastHints(module, [48657u, 48658u, 48669u], "章节魔法：利用场地效果应对，注意减伤！");
sealed class CrucibleFlowerTrapHint(BossModule module) : Components.CastHints(module, [48683u], "鲜花陷阱：准备吸引与捕食！");
sealed class CrucibleGargoyleSlashHint(BossModule module) : Components.CastHints(module, [48717u], "突进后连续斩击，注意首领朝向！");
sealed class CrucibleGargoyleRingHint(BossModule module) : Components.CastHints(module, [48721u], "钢铁后靠近首领，躲避月环！");
sealed class CrucibleGargoyleKnockbackHint(BossModule module) : Components.CastHints(module, [48725u], "20米击退：靠近中心，注意电网与瘴气！");
sealed class CrucibleGolemKnockbackHint(BossModule module) : Components.CastHints(module, [50649u], "17米击退：调整站位，注意电网！");
sealed class CrucibleGolemSelfDestructHint(BossModule module) : Components.CastHints(module, [48766u], "自爆：优先击杀正在读条的巨像！");
sealed class CrucibleDragonBreathHint(BossModule module) : Components.CastHints(module, [48807u], "猛毒吐息：注意连续毒浪！");
sealed class CrucibleDragonLandingHint(BossModule module) : Components.CastHints(module, [48816u], "空降：先靠近首领，再离开中央圆圈！");

// run2216 06:45:40: three 48812 浓烟呛呕 R6 circles start 2s apart at fixed ground points (908,-420), (928.5,-411.5),
// (928.5,-428.5); 粘液怪 tanked 5.1y from the first and took 2917 (2685 HP, dead). Its death dropped 被保护 and the
// unprotected player took C56F 2535+2528. The breath pocket PetMoveTo (CrucibleDragonWaves) only covers 48807.
// While a burst is pending, move a pet that stands inside one to the nearest point clear of every pending circle.
// 被保护 only covers the player while the pet stays close (Cover-style range): replays of the first version sent the
// pet 8.5y+ off the circles away from the player and the player took three more unprotected C56F (50543). Keep the
// pet within CoverRange of the player; if no such point is clear, leave it (one 2.9k hit beats losing cover).
sealed class CrucibleDragonCloudPet(BossModule module) : BossComponent(module)
{
    private const uint Burst = 48812u;
    private const float Radius = 6f, Inside = Radius + 1.5f, Clear = Radius + 1.5f, FenceMargin = 2f, ArenaRadius = 19.75f, CoverRange = 6f;
    private readonly List<(WPos Center, DateTime At)> _bursts = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == Burst && !spell.EventHappened)
            _bursts.Add((spell.LocXZ, Module.CastFinishAt(spell, 0.5)));
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        _bursts.RemoveAll(b => b.At <= WorldState.CurrentTime);
        if (_bursts.Count == 0 || hints.PetMoveTo != null)
            return;
        var pet = CrucibleNightmare.Pets(WorldState, actor).FirstOrDefault(p => _bursts.Any(b => p.Position.InCircle(b.Center, Inside)));
        if (pet == null)
            return;
        WPos? best = null;
        var bestCost = float.MaxValue;
        for (var r = 2f; r <= 16f; r += 2f)
            for (var deg = 0; deg < 360; deg += 15)
            {
                var p = pet.Position + ((float)deg).Degrees().ToDirection() * r;
                if ((p - Module.Center).Length() > ArenaRadius - FenceMargin || !p.InCircle(actor.Position, CoverRange)
                    || _bursts.Any(b => p.InCircle(b.Center, Clear)))
                    continue;
                // Short walk first, then stay by the player (cover) and the boss (tanking).
                var cost = r + 0.5f * (p - actor.Position).Length() + 0.25f * (p - Module.PrimaryActor.Position).Length();
                if (cost < bestCost)
                    (best, bestCost) = (p, cost);
            }
        if (best != null)
            hints.PetMoveTo = best;
    }
}

// run2218 尾王 两试：有毒物质(19673) 出生就以出场魔兽为目标（魔兽有吸引注意的仇恨），约 0.75m/s 走过去，
// 贴到目标约 22s 后大爆炸(BEB5) 只打它的目标：粘液怪 2125、迷途羊羔 1851（run2216 1974、run2210 1961、run2188 1800/2116），
// 每试吃掉魔兽总血量约 1/4。目标是玩家时 MP 会带走；这里让被盯的魔兽在它靠近时先挪开，保持在玩家身边（被保护有距离限制）。
sealed class CrucibleDragonSlugPet(BossModule module) : BossComponent(module)
{
    private const uint SlugOID = 19673u;
    private const float Trigger = 5f, Keep = 9f, PoolClear = 7f, FenceMargin = 2f, ArenaRadius = 19.75f, CoverRange = 6f, LooseCoverRange = 10f;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (hints.PetMoveTo != null)
            return;
        var slugs = Module.Enemies(SlugOID).Where(s => !s.IsDeadOrDestroyed && s.HPMP.CurHP > 0).ToArray();
        if (slugs.Length == 0)
            return;
        foreach (var pet in CrucibleNightmare.Pets(WorldState, actor))
        {
            if (!slugs.Any(s => s.TargetID == pet.InstanceID && s.Position.InCircle(pet.Position, Trigger)))
                continue;
            var pools = Module.Enemies(CrucibleDragonPoison.PoisonOID).Where(p => p.EventState != 7 && !p.IsDeadOrDestroyed).Select(p => p.Position).ToArray();
            hints.PetMoveTo = Pick(actor, pet, slugs, pools, CoverRange) ?? Pick(actor, pet, slugs, pools, LooseCoverRange);
            return;
        }
    }

    private WPos? Pick(Actor actor, Actor pet, Actor[] slugs, WPos[] pools, float coverRange)
    {
        WPos? best = null;
        var bestCost = float.MaxValue;
        for (var r = 2f; r <= 18f; r += 2f)
            for (var deg = 0; deg < 360; deg += 15)
            {
                var p = actor.Position + ((float)deg).Degrees().ToDirection() * (r * 0.5f);
                if (!p.InCircle(actor.Position, coverRange) || (p - Module.Center).Length() > ArenaRadius - FenceMargin
                    || slugs.Any(s => s.Position.InCircle(p, Keep)) || pools.Any(c => c.InCircle(p, PoolClear)))
                    continue;
                // Short pet walk, then stay by the boss so it keeps attacking.
                var cost = (p - pet.Position).Length() + 0.5f * (p - Module.PrimaryActor.Position).Length();
                if (cost < bestCost)
                    (best, bestCost) = (p, cost);
            }
        return best;
    }
}
