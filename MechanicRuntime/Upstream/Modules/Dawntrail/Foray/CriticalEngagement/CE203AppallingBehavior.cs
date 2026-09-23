using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE203AppallingBehavior;

public enum OID : uint
{
    Boss = 0x4D8F, // R3.0, BNpcName 14714, Pallmagia
    Pallkeeper = 0x4D90, // BNpcName 14715
    Anchor = 0x4D91, // non-targetable Pallmagia controller
    Helper = 0x233C,
    RouletteInnerGuide = 0x1EC02B, // event object; EAnim selects the two opposite inner sectors
    RouletteOuterGuide = 0x1EC02C, // event object; EAnim selects the two opposite outer sectors
    ChainGuide = 0x1EC02A // event object (keeper InstanceID - 4); EAnim stamps each keeper's instruction shape
}

public enum TetherID : uint
{
    EsotericOrder = 0xE, // Pallkeeper -> boss, emitted in execution order during C26D/C26E
    ChainSwap = 0xCF // Pallkeeper pair position swap during C26F polarity field (Reverse only)
}

public enum AID : uint
{
    ElectricBoundary = 0xC26B, // anchor, persistent arena-control pulse (not an 18-25y donut)
    Summon = 0xC26C,
    EsotericInstruction = 0xC26D,
    EsotericInstructionReverse = 0xC26E,
    ReversePolarity = 0xC26F,

    BadBreathKeeperVisual = 0xC270, // Pallkeeper, self-only visual immediately after C271 resolves
    BadBreathInstruction = 0xC271, // helper, 50y 100-degree cone
    PlaincrackerKeeperVisual = 0xC272, // Pallkeeper, self-only visual immediately after C273 resolves
    PlaincrackerInstruction = 0xC273, // helper, 30y circle

    SwapOpposites = 0xC278, // four Pallkeepers teleport to the opposite cardinal point
    SwapClockwise = 0xC279, // north/south Pallkeepers teleport clockwise
    SwapCounterclockwise = 0xC27A, // east/west Pallkeepers teleport counterclockwise

    Roulette = 0xC27B,
    RouletteCenter = 0xC27C, // helper, 5y center cell
    RouletteInner = 0xC27D, // helper, 5-12y 120-degree donut sector; two opposite helpers
    RouletteOuter = 0xC27E, // helper, 12-20y 90-degree donut sector; two opposite helpers

    LilliputianLyric = 0xC27F,
    LilliputianLyricAOE = 0xC280, // helper, 40y 180-degree cone
    MagicHammer = 0xC281,
    MagicHammerAOE = 0xC282, // helper->location, 8y circle
    OccultMissile = 0xC283,
    OccultMissileAOE = 0xC285, // helper->location, 6y circle
    GreatWhirlwind = 0xC286,
    GreatWhirlwindVisual = 0xC287,
    GreatWhirlwindHit = 0xC512,

    BadBreath = 0xC53A,
    BadBreathAOE = 0xC53B, // helper, 50y 100-degree cone
    Plaincracker = 0xC53C,
    PlaincrackerAOE = 0xC53D, // helper, 15y circle
    AutoAttack = 0xC53E
}

// Helpers can be teleported (and, after Reverse Polarity, swapped to the opposite keeper) between
// cast-start and effect. Keep the activation from the cast packet, but follow the live helper for
// self-targeted shapes instead of freezing the initial, often deliberately fake, coordinates.
sealed class AppallingAOEs(BossModule module) : Components.GenericAOEs(module)
{
    private readonly record struct AOEConfig(AOEShape Shape, bool LocationTargeted = false);
    private sealed record Pending(uint ActionID, ulong ActorID, AOEShape? Shape, WPos Origin, Angle Rotation, DateTime Activation, bool FollowCaster, bool PredictedInstruction = false, int InstructionSlot = -1);

    private static readonly AOEShapeCone BadBreath = new(50f, 50f.Degrees());
    private static readonly AOEShapeCircle PlaincrackerLarge = new(30f);
    private static readonly AOEShapeCircle PlaincrackerSmall = new(15f);
    private static readonly AOEShapeCone Lilliputian = new(40f, 90f.Degrees());
    private static readonly AOEShapeCircle Hammer = new(8f);
    private static readonly AOEShapeCircle Missile = new(6f);
    private readonly List<Pending> _pending = [];
    private readonly List<AOEInstance> _displayed = [];
    private readonly HashSet<uint> _seenSequences = [];
    private readonly HashSet<ulong> _instructionSources = [];
    // C26E does not identify its first shape. Keep its ordered tether slots hidden until the first
    // helper cast supplies the polarity, then calibrate all four slots without losing swap targets.
    private DateTime _instructionFirstActivation;
    private bool? _instructionCircleFirst;
    private int _instructionTethers;
    private int _resolvedInstructions;

    private static AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.BadBreathInstruction or (uint)AID.BadBreathAOE => new(BadBreath),
        (uint)AID.PlaincrackerInstruction => new(PlaincrackerLarge),
        (uint)AID.PlaincrackerAOE => new(PlaincrackerSmall),
        (uint)AID.LilliputianLyricAOE => new(Lilliputian),
        (uint)AID.MagicHammerAOE => new(Hammer, true),
        (uint)AID.OccultMissileAOE => new(Missile, true),
        _ => null
    };

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Prune();
        _displayed.Clear();
        var ordered = _pending.Where(p => p.Shape != null).OrderBy(p => p.Activation).ToArray();
        // Magic Hammer resolves as three one-second-spaced batches. If only the current batch is
        // risky, AI picks a technically safe point that is already covered by the second batch and
        // cannot leave the 8y circle in time. Keep the current and next batch forbidden so it plans
        // the step pattern; including all three at once would incorrectly erase every useful route.
        var riskWindow = ordered.Length > 0 && ordered[0].ActionID == (uint)AID.MagicHammerAOE ? 1.25d : 0.25d;
        var riskyDeadline = ordered.Length > 0 ? ordered[0].Activation.AddSeconds(riskWindow) : DateTime.MinValue;
        var instShown = 0;
        foreach (var pending in ordered)
        {
            var shape = pending.Shape!;
            var source = pending.FollowCaster ? WorldState.Actors.Find(pending.ActorID) : null;
            var origin = source?.Position ?? pending.Origin;
            var rotation = source?.Rotation ?? pending.Rotation;
            if (pending.InstructionSlot >= 0)
            {
                // Four-keeper chain: only show the next two to resolve (current deep-yellow, next
                // light-yellow); AI forbidden is limited to the final (fourth) resolution via Risky.
                // Reverse rounds keep this preview hidden until the swap resolves - the prep circle
                // guides the player meanwhile and the pre-swap preview is extra noise.
                if (InstructionPreviewHidden())
                    continue;
                if (instShown >= 2)
                    continue;
                var isCurrent = instShown == 0;
                _displayed.Add(new(shape, origin, rotation, pending.Activation,
                    isCurrent ? Colors.Danger : Colors.AOE, isCurrent && _resolvedInstructions >= 3, pending.ActorID, shape.Distance(origin, rotation)));
                ++instShown;
                continue;
            }
            var imminent = pending.Activation <= riskyDeadline;
            _displayed.Add(new(shape, origin, rotation, pending.Activation,
                imminent ? Colors.Danger : Colors.AOE, imminent, pending.ActorID, shape.Distance(origin, rotation)));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        Prune();
        var ordered = _pending.Where(p => p.Shape != null).OrderBy(p => p.Activation).ToArray();
        var riskyDeadline = ordered.Length > 0 ? ordered[0].Activation.AddSeconds(0.25d) : DateTime.MinValue;
        foreach (var pending in ordered)
        {
            // Four-keeper chain: the first three resolutions generate no forbidden zone - the AI
            // moves freely and follows the ChainSafeGuide green circle. Only the final (fourth)
            // resolution is forbidden as a fallback.
            if (pending.InstructionSlot >= 0 && _resolvedInstructions < 3)
                continue;

            var shape = pending.Shape!;
            var source = pending.FollowCaster ? WorldState.Actors.Find(pending.ActorID) : null;
            var origin = source?.Position ?? pending.Origin;
            var rotation = source?.Rotation ?? pending.Rotation;
            var risky = pending.InstructionSlot >= 0 || pending.Activation <= riskyDeadline;
            // Hammer and learned instructions are ordered movement puzzles. Later previews are not
            // currently dangerous for display purposes, but pathfinding needs their activation
            // times now or it can choose a dead-end safe spot for the preceding hit.
            if (risky || ReferenceEquals(shape, Hammer) || ReferenceEquals(shape, BadBreath) || ReferenceEquals(shape, PlaincrackerLarge))
                hints.AddForbiddenZone(shape.Distance(origin, rotation), pending.Activation);
        }
    }

    // Four-keeper chain: activation of the instruction pending occupying the given slot (0-based),
    // or null while that slot is not resolved yet. Consumed by ChainSafeGuide to publish the
    // circle target as an imminent forbidden zone.
    public DateTime? InstructionActivation(int slot)
    {
        for (var i = 0; i < _pending.Count; ++i)
            if (_pending[i].InstructionSlot == slot)
                return _pending[i].Activation;
        return null;
    }

    public override void Update()
    {
        SyncInstructionShapes();
        Prune();
    }

    // Reverse rounds keep the four-keeper preview hidden until the swap (TETH 207) resolves; the
    // ChainSafeGuide prep circle covers that window. Forward rounds show as usual.
    private bool InstructionPreviewHidden()
    {
        var sched = Module.FindComponent<ChainSchedule>();
        return sched is { Reverse: true } && !sched.SwapDone;
    }

    // The EANM-confirmed shape overrides the polar placeholder (the forward-run circleFirst guess
    // and the Reverse-run first-cast calibration are both fallbacks; recordings show the chain can
    // start with either circle or cone).
    // The keeper position follows the schedule too: after the TETH 207 swap the keepers physically
    // teleport only when the field cast resolves, so freeze the preview on the scheduled post-swap
    // position during the cast - the radar then agrees with the green circle right away (the
    // first real cast re-enables caster following with its authoritative position).
    private void SyncInstructionShapes()
    {
        var sched = Module.FindComponent<ChainSchedule>();
        if (sched == null)
            return;
        for (var i = 0; i < _pending.Count; ++i)
        {
            var pending = _pending[i];
            if (pending.InstructionSlot < 0)
                continue;
            var kind = sched.KindOf(pending.ActorID);
            var pos = sched.PosOf(pending.ActorID);
            if (kind == ChainSchedule.Kind.None && pos == null)
                continue;
            var actionID = kind == ChainSchedule.Kind.Circle ? (uint)AID.PlaincrackerInstruction : kind == ChainSchedule.Kind.Cone ? (uint)AID.BadBreathInstruction : pending.ActionID;
            var shape = kind == ChainSchedule.Kind.Circle ? PlaincrackerLarge : kind == ChainSchedule.Kind.Cone ? BadBreath : pending.Shape;
            var follow = pending.FollowCaster && !(sched.Reverse && sched.SwapDone);
            var origin = pos ?? pending.Origin;
            // While the swap cast freezes the preview, the cone must face the arena center (C->O,
            // the actual cast direction). The tether-time keeper rotation is not the cone heading -
            // without this the cone points the wrong way until the first real cast overrides it.
            var rotation = !follow && kind == ChainSchedule.Kind.Cone ? Angle.FromDirection(Arena.Center - origin) : pending.Rotation;
            _pending[i] = pending with { ActionID = actionID, Shape = shape, Origin = origin, Rotation = rotation, FollowCaster = follow };
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.EsotericInstruction or (uint)AID.EsotericInstructionReverse)
        {
            _pending.RemoveAll(p => p.InstructionSlot >= 0);
            _instructionSources.Clear();
            _instructionTethers = 0;
            _resolvedInstructions = 0;
            var reverse = spell.Action.ID == (uint)AID.EsotericInstructionReverse;
            _instructionCircleFirst = reverse ? null : true;
            _instructionFirstActivation = Module.CastFinishAt(spell, reverse ? 12.7d : 6.3d);
            return;
        }

        if (ConfigFor(spell.Action.ID) is not { } config || spell.EventHappened)
            return;

        var activation = Module.CastFinishAt(spell);
        if (activation <= WorldState.CurrentTime)
            return;

        if (spell.Action.ID is (uint)AID.BadBreathInstruction or (uint)AID.PlaincrackerInstruction && UpdateInstructionPrediction(caster, spell, config.Shape, activation))
            return;

        _pending.RemoveAll(p => p.ActionID == spell.Action.ID && p.ActorID == caster.InstanceID);
        var origin = config.LocationTargeted ? spell.LocXZ : caster.Position;
        _pending.Add(new(spell.Action.ID, caster.InstanceID, config.Shape, origin, spell.Rotation, activation, !config.LocationTargeted));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened && spell.Action.ID is not (uint)AID.BadBreathInstruction and not (uint)AID.PlaincrackerInstruction)
            _pending.RemoveAll(p => p.ActionID == spell.Action.ID && p.ActorID == caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.SwapOpposites or (uint)AID.SwapClockwise or (uint)AID.SwapCounterclockwise)
        {
            UpdateInstructionDestination(caster, spell);
            return;
        }

        if (ConfigFor(spell.Action.ID) == null || spell.GlobalSequence != 0 && !_seenSequences.Add(spell.GlobalSequence))
            return;

        var removed = _pending.RemoveAll(p => p.ActionID == spell.Action.ID && p.ActorID == caster.InstanceID);
        if (removed == 0 && spell.Action.ID is (uint)AID.BadBreathInstruction or (uint)AID.PlaincrackerInstruction)
        {
            var index = EarliestInstructionSlot();
            if (index >= 0)
                _pending.RemoveAt(index);
        }
        if (spell.Action.ID is (uint)AID.BadBreathInstruction or (uint)AID.PlaincrackerInstruction)
            ++_resolvedInstructions;
        ++NumCasts;
    }

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (tether.ID != (uint)TetherID.EsotericOrder || source.OID != (uint)OID.Pallkeeper || _instructionFirstActivation == default
            || _instructionTethers >= 4 || !_instructionSources.Add(source.InstanceID))
            return;

        var slot = _instructionTethers++;
        var circle = _instructionCircleFirst is { } circleFirst && circleFirst == (slot % 2 == 0);
        var actionID = _instructionCircleFirst == null ? 0 : circle ? (uint)AID.PlaincrackerInstruction : (uint)AID.BadBreathInstruction;
        AOEShape? shape = _instructionCircleFirst == null ? null : circle ? PlaincrackerLarge : BadBreath;
        var activation = _instructionFirstActivation.AddSeconds(4.5d * slot);
        _pending.Add(new(actionID, source.InstanceID, shape, source.Position, source.Rotation, activation, true, true, slot));
    }

    public override void OnActorDestroyed(Actor actor) => _pending.RemoveAll(p => p.ActorID == actor.InstanceID);

    private void Prune()
    {
        var now = WorldState.CurrentTime;
        _pending.RemoveAll(p => now > p.Activation.AddSeconds(2d));
    }

    private bool UpdateInstructionPrediction(Actor caster, ActorCastInfo spell, AOEShape shape, DateTime activation)
    {
        if (_instructionCircleFirst == null)
            CalibrateInstruction(spell.Action.ID == (uint)AID.PlaincrackerInstruction, activation);

        var index = EarliestInstructionSlot(predictedOnly: true);
        if (index < 0)
            return false;

        _pending[index] = _pending[index] with
        {
            ActionID = spell.Action.ID,
            ActorID = caster.InstanceID,
            Shape = shape,
            Origin = caster.Position,
            Rotation = spell.Rotation,
            Activation = activation,
            FollowCaster = true,
            PredictedInstruction = false
        };
        return true;
    }

    private void CalibrateInstruction(bool circleFirst, DateTime firstActivation)
    {
        _instructionCircleFirst = circleFirst;
        _instructionFirstActivation = firstActivation;
        for (var i = 0; i < _pending.Count; ++i)
        {
            var pending = _pending[i];
            if (pending.InstructionSlot < 0)
                continue;

            var circle = circleFirst == (pending.InstructionSlot % 2 == 0);
            _pending[i] = pending with
            {
                ActionID = circle ? (uint)AID.PlaincrackerInstruction : (uint)AID.BadBreathInstruction,
                Shape = circle ? PlaincrackerLarge : BadBreath,
                Activation = firstActivation.AddSeconds(4.5d * pending.InstructionSlot)
            };
        }
    }

    private void UpdateInstructionDestination(Actor caster, ActorCastEvent spell)
    {
        var index = _pending.FindIndex(p => p.PredictedInstruction && p.ActorID == caster.InstanceID);
        if (index < 0)
            return;

        var distance = spell.Action.ID == (uint)AID.SwapOpposites ? 40f : 20f * MathF.Sqrt(2f);
        var origin = caster.Position + distance * spell.Rotation.ToDirection();
        _pending[index] = _pending[index] with
        {
            Origin = origin,
            Rotation = Angle.FromDirection(Module.Arena.Center - origin),
            FollowCaster = false
        };
    }

    private int EarliestInstructionSlot(bool predictedOnly = false)
    {
        var result = -1;
        for (var i = 0; i < _pending.Count; ++i)
        {
            var candidate = _pending[i];
            if (candidate.InstructionSlot >= 0 && (!predictedOnly || candidate.PredictedInstruction)
                && (result < 0 || candidate.Activation < _pending[result].Activation))
                result = i;
        }
        return result;
    }
}

// Chain of four keeper instructions (Esoteric Instruction). TETH 14 assigns the execution order,
// the co-timestamped EANM on the 1EC02A event object stamps each keeper's shape (0010->0020
// circle, 0001->0002 cone; 0004->0040/0008 are the corresponding release states), and TETH 207
// (Reverse only) swaps two keepers' positions. The resolved sequence is the ordered (shape,
// position) list that drives the display and the green-circle guide.
sealed class ChainSchedule(BossModule module) : BossComponent(module)
{
    public enum Kind : byte { None, Circle, Cone }
    private sealed record Keeper(ulong InstanceID, WPos Pos, Kind Kind = Kind.None, int Order = -1);
    private readonly Dictionary<ulong, Keeper> _keepers = [];
    private readonly List<ulong> _order = [];

    public bool Reverse { get; private set; }
    public bool SwapDone { get; private set; }
    public bool Ready => !Reverse ? ResolvedCount > 0 : SwapDone;

    public int ResolvedCount
    {
        get
        {
            var n = 0;
            foreach (var k in _keepers.Values)
                if (k.Kind != Kind.None)
                    ++n;
            return n;
        }
    }

    public Kind KindOf(ulong instanceID) => _keepers.TryGetValue(instanceID, out var k) ? k.Kind : Kind.None;

    // Current keeper position - the TETH 207 swap resolves it before the keeper physically
    // teleports (the field cast finish), so consumers can show the post-swap cardinal early.
    public WPos? PosOf(ulong instanceID) => _keepers.TryGetValue(instanceID, out var keeper) ? keeper.Pos : null;

    // Index-th resolved entry (0-based execution order): shape + position; null if not confirmed yet.
    public (AID Kind, WPos Pos)? Entry(int index)
    {
        if (index < 0 || index >= _order.Count)
            return null;
        var keeper = _keepers[_order[index]];
        if (keeper.Kind == Kind.None)
            return null;
        return (keeper.Kind == Kind.Circle ? AID.PlaincrackerInstruction : AID.BadBreathInstruction, keeper.Pos);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.EsotericInstruction or (uint)AID.EsotericInstructionReverse)
        {
            _keepers.Clear();
            _order.Clear();
            SwapDone = false;
            Reverse = spell.Action.ID == (uint)AID.EsotericInstructionReverse;
        }
    }

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.OID != (uint)OID.Pallkeeper)
            return;

        if (tether.ID == (uint)TetherID.EsotericOrder)
        {
            if (_keepers.TryGetValue(source.InstanceID, out var existing))
                _keepers[source.InstanceID] = existing with { Pos = source.Position, Order = _order.Count };
            else
                _keepers[source.InstanceID] = new(source.InstanceID, source.Position, Kind.None, _order.Count);
            _order.Add(source.InstanceID);
        }
        else if (tether.ID == (uint)TetherID.ChainSwap)
        {
            // 207: source <-> target swap positions; the shape stays with the keeper OID.
            var target = WorldState.Actors.Find(tether.Target);
            if (target != null && _keepers.TryGetValue(source.InstanceID, out var s) && _keepers.TryGetValue(target.InstanceID, out var t))
            {
                _keepers[source.InstanceID] = s with { Pos = t.Pos };
                _keepers[target.InstanceID] = t with { Pos = s.Pos };
            }
            SwapDone = true;
        }
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID != (uint)OID.ChainGuide)
            return;
        var kind = (state & 0xFFFF) switch
        {
            0x20 or 0x40 => Kind.Circle,
            0x02 or 0x08 => Kind.Cone,
            _ => Kind.None
        };
        if (kind == Kind.None)
            return;
        var keeperID = actor.InstanceID + 4; // the event object is the keeper's InstanceID - 4
        if (_keepers.TryGetValue(keeperID, out var keeper))
            _keepers[keeperID] = keeper with { Kind = kind };
        else
            _keepers[keeperID] = new(keeperID, actor.Position, kind, -1);
    }
}

// Prep circle (Reverse: from instruction cast until the swap) plus a 2f guide circle aimed at the
// next four-keeper AOE's safe spot; steps forward on every resolved effect and stops after four.
// Geometry (replay-verified): circle safe spot at |OP|=16.5 rotated +-130 deg from the O->C axis
// (130 >= 122.2 deg limit), cone spot rotated +80 deg (past the -88.8..-11.2 deg danger wedge).
sealed class ChainSafeGuide(BossModule module) : BossComponent(module)
{
    private const float PrepRadius = 4f, GuideRadius = 1f, SpotDist = 18f, PrepWeight = 20f, GuideWeight = 30f;
    // Kicker: when the Nth instruction resolves, the old green-circle spot becomes a max-urgency
    // forbidden zone (activation=now -> G=0) for 0.5s, driving the AI out of the spot at once
    // (interrupting greedy casts) while the guide moves on to the next spot.
    private const float KickerRadius = 1.5f;
    private WPos? _kickSpot;
    private DateTime _kickUntil;
    private bool _kickHadCast; // player was casting last frame while the kick zone was active
    private readonly HashSet<uint> _seenSequences = [];
    private bool _preparing;
    private int _resolved;

    private ChainSchedule? Schedule => Module.FindComponent<ChainSchedule>();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.EsotericInstruction or (uint)AID.EsotericInstructionReverse)
        {
            _preparing = spell.Action.ID == (uint)AID.EsotericInstructionReverse;
            _resolved = 0;
            _seenSequences.Clear();
        }
    }

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (tether.ID == (uint)TetherID.ChainSwap)
            _preparing = false; // swap resolves the whole chain - the guide takes over
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.BadBreathInstruction or (uint)AID.PlaincrackerInstruction)
        {
            if (spell.GlobalSequence != 0 && !_seenSequences.Add(spell.GlobalSequence))
                return;
            // The resolved spot becomes a max-urgency kicker for 0.5s: the AI must leave it at
            // once (activation=now -> G=0), interrupting greedy casts, while the guide moves on.
            // No kick after the third resolution - the fourth spot has no next stop to rush to.
            if (_resolved < 3 && SafeSpot(_resolved) is { } oldSpot)
            {
                _kickSpot = oldSpot;
                _kickUntil = WorldState.CurrentTime.AddSeconds(0.5d);
            }
            ++_resolved;
        }
    }

    private WPos? SafeSpot(int index)
    {
        var sched = Schedule;
        if (sched == null || !sched.Ready || sched.Entry(index) is not { } entry)
            return null;
        var dir = Angle.FromDirection(entry.Pos - Arena.Center);
        if (entry.Kind == AID.PlaincrackerInstruction)
        {
            // Circle: rotate +-115 deg - above the measured SpotSafe interception boundary
            // (~109.2 deg, |CP|>=31 clears the danger test) and below the 122.2 deg safety limit,
            // so the guide shows while staying outside the big circle (|CP| ~= 32.1).
            var p1 = Arena.Center + SpotDist * (dir + 115f.Degrees()).ToDirection();
            var p2 = Arena.Center + SpotDist * (dir - 115f.Degrees()).ToDirection();
            return PickSafeSide(p1, p2, index);
        }
        // Cone: the danger wedge sits on the negative side of the axis, so +-45 deg are the two
        // candidate sides (still far past the wedge boundary, keeping the guide clear of it). A
        // single candidate could land inside the next AOE's zone with no fallback (live feedback),
        // so both sides are tested like the circle branch.
        var c1 = Arena.Center + SpotDist * (dir + 45f.Degrees()).ToDirection();
        var c2 = Arena.Center + SpotDist * (dir - 45f.Degrees()).ToDirection();
        return PickSafeSide(c1, c2, index);
    }

    // Shared candidate selection: prefer a side outside the current and next danger zones; when
    // both are safe, prefer the side nearer to the next resolution cardinal; when both are
    // dangerous, suppress the guide entirely (none is better than a wrong one).
    private WPos? PickSafeSide(WPos p1, WPos p2, int index)
    {
        var sched = Schedule;
        if (sched == null)
            return null;
        var safe1 = SpotSafe(p1, index);
        var safe2 = SpotSafe(p2, index);
        if (safe1 && !safe2)
            return p1;
        if (safe2 && !safe1)
            return p2;
        if (!safe1 && !safe2)
            return null;
        if (sched.Entry(index + 1) is { } next)
        {
            var nextDir = Angle.FromDirection(next.Pos - Arena.Center);
            return Math.Abs((Angle.FromDirection(p1 - Arena.Center) - nextDir).Normalized().Rad)
                <= Math.Abs((Angle.FromDirection(p2 - Arena.Center) - nextDir).Normalized().Rad) ? p1 : p2;
        }
        // The next cardinal is unknown - suppress the guide rather than guess the side; the
        // forward-run first connection must not show a possibly mirrored green circle yet.
        return null;
    }

    // The spot must stay outside the danger zones of the current and the next (if known) AOE.
    private bool SpotSafe(WPos spot, int index)
    {
        var sched = Schedule;
        if (sched == null)
            return false;
        for (var i = index; i <= index + 1; ++i)
        {
            if (sched.Entry(i) is not { } entry)
                continue; // unknown entry: not tested (the next cardinal may not be resolved yet)
            if (IsInDanger(spot, entry.Kind, entry.Pos))
                return false;
        }
        return true;
    }

    // Danger-zone containment: circle by distance (R=30 + 1f margin), cone by the angle between
    // the spot and the cone axis (the O->C direction); the 50y cone radius always covers the arena.
    private bool IsInDanger(WPos spot, AID kind, WPos center)
    {
        var dist = (spot - center).Length();
        if (kind == AID.PlaincrackerInstruction)
            return dist <= 31f;
        // The cone is cast toward the arena center (C->O, matching the keeper rotation in the
        // recordings), so the axis is the opposite of the O->C direction used for the guide.
        var delta = (Angle.FromDirection(spot - center) - Angle.FromDirection(Arena.Center - center)).Normalized();
        return Math.Abs(delta.Rad) <= 50f.Degrees().Rad;
    }

    // Drawn on the foreground layer: the background layer is covered by the AOE fills of other
    // components (live recording feedback), so the outline circles must stay on top to be visible.
    // The guide is removed once the first three resolutions are done - the fourth is covered by
    // the forbidden fallback zone instead.
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (_preparing)
            Arena.ZoneCircleOutline(Arena.Center, PrepRadius, Colors.Safe);
        if (_resolved < 3 && SafeSpot(_resolved) is { } target)
            Arena.ZoneCircleOutline(target, GuideRadius, Colors.Safe);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Kicker zone: the old green-circle spot is hard-forbidden (activation=now -> G=0) for
        // 0.5s, so the AI abandons it immediately even mid-cast. It ends early once the AI is
        // actually gone (outside the zone) or its cast was broken (casting last frame, not now),
        // so the path to the next green circle is not detoured around the fading zone. The 0.5s
        // deadline remains the fallback for idle players (standing still, no cast to break).
        var now = WorldState.CurrentTime;
        if (_kickSpot is { } spot)
        {
            var casting = actor.CastInfo != null;
            var castBroken = _kickHadCast && !casting;
            var leftZone = (actor.Position - spot).Length() > KickerRadius + 0.5f;
            if (castBroken || leftZone)
                _kickUntil = now; // kick done - zone vanishes this frame
            _kickHadCast = casting;
            if (now < _kickUntil)
                hints.AddForbiddenZone(new AOEShapeCircle(KickerRadius).Distance(spot, default), now);
        }
        else
        {
            _kickHadCast = false;
        }
        if (_preparing)
            hints.GoalZones.Add(AIHints.GoalSingleTarget(Arena.Center, PrepRadius, PrepWeight));
        if (_resolved < 3 && SafeSpot(_resolved) is { } target)
        {
            hints.GoalZones.Add(AIHints.GoalSingleTarget(target, GuideRadius, GuideWeight));
            // Circle target: publish the circle's danger zone into the AI view (activation from
            // the matching instruction pending, falling back to now) so the AI treats it as
            // imminent and leaves the circle at once - a second layer next to the green-circle
            // guide (the cone needs no zone: the side selection already keeps the guide outside).
            if (Schedule?.Entry(_resolved) is { Kind: AID.PlaincrackerInstruction, Pos: var center })
            {
                var activation = Module.FindComponent<AppallingAOEs>()?.InstructionActivation(_resolved) ?? WorldState.CurrentTime;
                hints.AddForbiddenZone(new AOEShapeCircle(30f).Distance(center, default), activation);
            }
        }
    }
}

// C26B is the persistent electric fence at the arena edge. The official Action sheet (0xC26B,
// eff=10 donut, xAxis=25) puts the outer kill ring at 25y; keep the inner edge at the 20y
// walkable circle so the danger band sits exactly on the fence.
sealed class ElectricBoundary(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeDonut Shape = new(19.5f, 25f);
    private readonly AOEInstance[] _aoe = [new(Shape, module.Arena.Center)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;
}

// Death Roulette is a polar grid, not the C26B 18-25y donut that used to be drawn here. ARR v5
// recordings show five fixed helpers owned by the boss: boss+6 is the center, boss+37/+38 are the
// opposite inner sectors and boss+39/+40 the opposite outer sectors. Their 0x022A movement packets
// update position/rotation immediately before the five consecutive C27C-C27E effects, so build the
// cells from the live actors and clear the whole snapshot atomically after all five sequences.
sealed class DeathRouletteGrid(BossModule module) : Components.GenericAOEs(module)
{
    // Keep a small movement margin around the replay-verified action geometry. Roulette resolves
    // as five almost consecutive effects, so aiming exactly at a cell edge is not reliable for AI.
    private static readonly AOEShapeCircle CenterCell = new(5.5f);
    // Replay hit coordinates put inner-ring victims as far as 56.1 degrees from the helper's
    // facing. The action sectors are therefore 120/135 degrees wide (the shape API takes a
    // half-angle): inner keeps 120 + 4 margin = 62 half-angle, outer widened to 135 + 4 margin
    // = 139 half-angle (69.5) per live recording confirmation of 1-safe-3-dead outer sectors.
    private static readonly AOEShapeDonutSector InnerCell = new(4.5f, 12.5f, 62f.Degrees());
    private static readonly AOEShapeDonutSector OuterCell = new(11.5f, 20.5f, 69.5f.Degrees());
    private readonly List<AOEInstance> _displayed = [];
    private readonly HashSet<uint> _seenSequences = [];
    private readonly Dictionary<ulong, Angle> _orientationBaseline = [];
    private Angle? _innerDirection;
    private Angle? _outerDirection;
    private DateTime _activation;
    private int _resolvedCells;
    private bool _armed;
    private bool _directionsFresh;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Prune();
        _displayed.Clear();
        if (!_armed)
            return CollectionsMarshal.AsSpan(_displayed);

        // The center cell is unconditional and must be available from the roulette cast even if
        // one of the four sector helpers has not spawned/streamed in yet.
        Add(CenterCell, default, true);
        var inner1 = Helper(37);
        var inner2 = Helper(38);
        var outer1 = Helper(39);
        var outer2 = Helper(40);
        if (inner1 == null || inner2 == null || outer1 == null || outer2 == null)
            return CollectionsMarshal.AsSpan(_displayed);

        if (_innerDirection is { } inner && _outerDirection is { } outer)
        {
            Add(InnerCell, inner, true, inner1.InstanceID);
            Add(InnerCell, inner + 180f.Degrees(), true, inner2.InstanceID);
            Add(OuterCell, outer, true, outer1.InstanceID);
            Add(OuterCell, outer + 180f.Degrees(), true, outer2.InstanceID);
        }
        else
        {
            UpdateDirectionFreshness(inner1, inner2, outer1, outer2);
            if (_directionsFresh)
            {
                Add(InnerCell, inner1.Rotation, true, inner1.InstanceID);
                Add(InnerCell, inner2.Rotation, true, inner2.InstanceID);
                Add(OuterCell, outer1.Rotation, true, outer1.InstanceID);
                Add(OuterCell, outer2.Rotation, true, outer2.InstanceID);
            }
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        if (_armed)
            hints.Add("Death roulette: watch the polar grid");
    }

    public override void Update() => Prune();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Roulette && !spell.EventHappened)
            Arm(Module.CastFinishAt(spell, 14.38f));
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        var p2 = state & 0xFFFF;
        if (actor.OID == (uint)OID.RouletteInnerGuide)
        {
            _innerDirection = p2 switch
            {
                0x20 => actor.Rotation + 60f.Degrees(),
                0x10 => actor.Rotation,
                _ => _innerDirection
            };
        }
        else if (actor.OID == (uint)OID.RouletteOuterGuide)
        {
            _outerDirection = p2 switch
            {
                0x10 => actor.Rotation - 45f.Degrees(),
                0x20 => actor.Rotation - 90f.Degrees(),
                _ => _outerDirection
            };
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Roulette)
        {
            if (!_armed)
                Arm(WorldState.FutureTime(14.38d));
            return;
        }

        if (spell.Action.ID is < (uint)AID.RouletteCenter or > (uint)AID.RouletteOuter || spell.GlobalSequence != 0 && !_seenSequences.Add(spell.GlobalSequence))
            return;

        if (++_resolvedCells >= 5)
            Clear();
        ++NumCasts;
    }

    private Actor? Helper(ulong offset)
    {
        var actor = WorldState.Actors.Find(Module.PrimaryActor.InstanceID + offset);
        return actor?.OID == (uint)OID.Helper ? actor : null;
    }

    private void Add(AOEShape shape, Angle rotation, bool risky, ulong actorID = 0)
        => _displayed.Add(new(shape, Module.Arena.Center, rotation, _activation, risky ? Colors.Danger : Colors.AOE, risky, actorID, shape.Distance(Module.Arena.Center, rotation)));

    private void Arm(DateTime activation)
    {
        _armed = true;
        _activation = activation;
        _resolvedCells = 0;
        _seenSequences.Clear();
        _orientationBaseline.Clear();
        _innerDirection = null;
        _outerDirection = null;
        foreach (var offset in new ulong[] { 37, 38, 39, 40 })
            if (Helper(offset) is { } helper)
                _orientationBaseline[helper.InstanceID] = helper.Rotation;
        _directionsFresh = false;
    }

    private void Clear()
    {
        _armed = false;
        _resolvedCells = 0;
        _displayed.Clear();
        _orientationBaseline.Clear();
        _innerDirection = null;
        _outerDirection = null;
        _directionsFresh = false;
    }

    private void UpdateDirectionFreshness(params Actor[] helpers)
    {
        if (_directionsFresh || _orientationBaseline.Count != 4)
            return;

        _directionsFresh = helpers.All(helper => _orientationBaseline.TryGetValue(helper.InstanceID, out var baseline)
            && Math.Abs((helper.Rotation - baseline).Normalized().Rad) > 1f.Degrees().Rad);
    }

    private void Prune()
    {
        if (_armed && WorldState.CurrentTime > _activation.AddSeconds(1d))
            Clear();
    }
}

// The three C512 helper casts each hit the raid; the boss cast is the stable warning packet.
sealed class GreatWhirlwind(BossModule module) : Components.RaidwideCast(module, (uint)AID.GreatWhirlwind);

sealed class AppallingBehaviorStates : StateMachineBuilder
{
    public AppallingBehaviorStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ElectricBoundary>()
            .ActivateOnEnter<ChainSchedule>()
            .ActivateOnEnter<ChainSafeGuide>()
            .ActivateOnEnter<AppallingAOEs>()
            .ActivateOnEnter<DeathRouletteGrid>()
            .ActivateOnEnter<GreatWhirlwind>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(AppallingBehaviorStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    TetherIDType = typeof(TetherID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 59u,
    SortOrder = 2)]
public sealed class AppallingBehavior(WorldState ws, Actor primary) : BossModule(ws, primary, new(807f, -562f), new ArenaBoundsCircle(20f))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.Pallkeeper));
    }
}
