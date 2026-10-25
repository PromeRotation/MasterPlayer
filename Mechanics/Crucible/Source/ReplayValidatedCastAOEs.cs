using System.Runtime.InteropServices;
namespace MasterPlayer.Mechanics.Crucible.CriticalEngagement;

// Cast packets in accelerated/replayed encounters can be duplicated or resynchronized. This
// component keys warnings by action + caster and keeps short tombstones for already resolved casts,
// so a stale cast-start packet cannot resurrect an AOE from the previous wave.
abstract class ReplayValidatedCastAOEs(BossModule module) : Components.GenericAOEs(module)
{
    protected readonly record struct AOEConfig(AOEShape Shape, bool LocationTargeted = false);

    private const double EventResolveTolerance = 0.5d;
    private const double CastMatchTolerance = 0.75d;
    private const double TombstoneWindow = 1d;
    private const double EventDedupWindow = 2d;
    private const double ExpireDelay = 2d;

    protected sealed class PendingAOE(uint actionID, AOEInstance aoe)
    {
        public readonly uint ActionID = actionID;
        public AOEInstance AOE = aoe;
    }

    protected ReadOnlySpan<PendingAOE> Pending => CollectionsMarshal.AsSpan(_pending);

    private readonly record struct ResolvedCast(uint ActionID, ulong ActorID, DateTime Activation, DateTime ExpiresAt);
    private readonly record struct EventKey(uint GlobalSequence, uint ActionID, ulong ActorID);

    private readonly List<PendingAOE> _pending = [];
    private readonly List<AOEInstance> _displayed = [];
    private readonly List<ResolvedCast> _resolved = [];
    private readonly Dictionary<EventKey, DateTime> _seenEvents = [];

    protected abstract AOEConfig? ConfigFor(uint actionID);
    internal (AOEShape Shape, bool LocationTargeted)? Describe(uint actionID) => ConfigFor(actionID) is { } c ? (c.Shape, c.LocationTargeted) : null;
    protected virtual int MaxDisplayed => int.MaxValue;
    protected virtual int MaxRisky => int.MaxValue;
    protected virtual double RiskyActivationWindow => double.PositiveInfinity;
    // Some action groups reveal their whole sequence at once (e.g. three simultaneous cones).
    // When RiskyByOrder matches, risk is graded purely by draw order (i < RiskyCountByOrder is
    // dangerous) instead of by activation time. Defaults keep every other encounter unchanged.
    protected virtual bool RiskyByOrder(uint actionID) => false;
    protected virtual int RiskyCountByOrder => int.MaxValue;
    // Some mechanics split one timeline across several components. Let a component contribute an
    // earlier activation so later previews stay visible without becoming forbidden too soon.
    protected virtual DateTime? CompetingActivation => null;
    // Some telegraphs keep a fixed display color regardless of the risk-window grading (e.g.
    // CE210's CycloneCrossing cross: user-requested pale yellow 2026-08-02). Returning true pins
    // the color; the risky flag still follows the framework's window grading.
    protected virtual bool FixedColor(uint actionID, out uint color)
    {
        color = default;
        return false;
    }

    public DateTime? EarliestActivation
    {
        get
        {
            PruneExpired();
            return _pending.Count > 0 ? _pending[0].AOE.Activation : null;
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        var count = Math.Min(_pending.Count, MaxDisplayed);
        var useRiskLimit = MaxRisky != int.MaxValue || !double.IsPositiveInfinity(RiskyActivationWindow);
        var riskReference = count > 0 ? _pending[0].AOE.Activation : DateTime.MaxValue;
        if (count > 0 && CompetingActivation is { } competing && competing < riskReference)
            riskReference = competing;
        var riskyDeadline = count > 0 && !double.IsPositiveInfinity(RiskyActivationWindow)
            ? riskReference.AddSeconds(RiskyActivationWindow)
            : DateTime.MaxValue;
        for (var i = 0; i < count; ++i)
        {
            var aoe = _pending[i].AOE;
            var byOrder = RiskyByOrder(_pending[i].ActionID);
            if (byOrder || useRiskLimit)
            {
                var imminent = byOrder ? i < RiskyCountByOrder : i < MaxRisky && aoe.Activation <= riskyDeadline;
                aoe.Color = imminent ? Colors.Danger : Colors.AOE;
                aoe.Risky = imminent;
            }
            if (FixedColor(_pending[i].ActionID, out var fixedColor))
                aoe.Color = fixedColor; // pinned display color wins; risky keeps the grading
            _displayed.Add(aoe);
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (ConfigFor(spell.Action.ID) is not { } config)
        {
            return;
        }

        PruneExpired();
        var activation = Module.CastFinishAt(spell);
        if (spell.EventHappened || activation <= WorldState.CurrentTime || WasRecentlyResolved(spell.Action.ID, caster.InstanceID, activation))
        {
            return;
        }

        var origin = config.LocationTargeted ? spell.LocXZ : caster.Position;
        AddOrRefresh(spell.Action.ID, config.Shape, caster.InstanceID, origin, spell.Rotation, activation);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (ConfigFor(spell.Action.ID) == null)
        {
            return;
        }

        var now = WorldState.CurrentTime;
        var activation = Module.CastFinishAt(spell);
        // A completed cast bar is not a damage event. Keep the pending hit
        // through its real window; cancellation, effect, death and expiry clean it.
        if (!spell.Interrupted && !spell.EventHappened)
            return;
        RemoveMatchingCast(spell.Action.ID, caster.InstanceID, activation);
        if (!spell.Cancelled && (spell.EventHappened || activation <= now.AddSeconds(EventResolveTolerance)))
        {
            RememberResolved(spell.Action.ID, caster.InstanceID, activation, now);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (ConfigFor(spell.Action.ID) == null)
        {
            return;
        }

        var now = WorldState.CurrentTime;
        PruneExpired();
        if (IsDuplicateEvent(spell.GlobalSequence, spell.Action.ID, caster.InstanceID, now))
        {
            return;
        }

        ++NumCasts;
        var activation = RemoveResolvedByEvent(spell.Action.ID, caster.InstanceID, now) ?? now;
        RememberResolved(spell.Action.ID, caster.InstanceID, activation, now);
    }

    // AI 避让入口：默认把所有 Risky AOE 加为禁区（与 GenericAOEs 行为一致）。子类可 override
    // AddAOEForbiddenZones 定制 AI 层紧迫值（如 CE214 溅墨的分组方案），显示层 ActiveAOEs 不受影响。
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
        => AddAOEForbiddenZones(slot, actor, assignment, hints);

    protected virtual void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aoes = ActiveAOEs(slot, actor);
        var len = aoes.Length;
        for (var i = 0; i < len; ++i)
        {
            ref readonly var c = ref aoes[i];
            if (c.Risky)
                hints.AddForbiddenZone(c.ShapeDistance ?? c.Shape.Distance(c.Origin, c.Rotation), c.Activation);
        }
    }

    public override void OnActorDeath(Actor actor) => RemoveActor(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => RemoveActor(actor.InstanceID);

    private void AddOrRefresh(uint actionID, AOEShape shape, ulong actorID, WPos origin, Angle rotation, DateTime activation)
    {
        var aoe = new AOEInstance(shape, origin, rotation, activation, actorID: actorID, shapeDistance: shape.Distance(origin, rotation));
        var duplicate = _pending.FindIndex(entry => entry.ActionID == actionID
            && entry.AOE.ActorID == actorID
            && Math.Abs((entry.AOE.Activation - activation).TotalSeconds) <= CastMatchTolerance);
        if (duplicate >= 0)
            _pending[duplicate].AOE = aoe;
        else
            _pending.Add(new(actionID, aoe));
        _pending.Sort((left, right) => left.AOE.Activation.CompareTo(right.AOE.Activation));
    }

    private DateTime? RemoveResolvedByEvent(uint actionID, ulong actorID, DateTime now)
    {
        var index = -1;
        for (var i = 0; i < _pending.Count; ++i)
        {
            var entry = _pending[i];
            if (entry.ActionID == actionID && entry.AOE.ActorID == actorID && entry.AOE.Activation <= now.AddSeconds(EventResolveTolerance))
            {
                index = i;
                break;
            }
        }
        if (index < 0)
            return null;

        var activation = _pending[index].AOE.Activation;
        _pending.RemoveAt(index);
        return activation;
    }

    private DateTime? RemoveMatchingCast(uint actionID, ulong actorID, DateTime activation)
    {
        var index = -1;
        var bestDelta = double.MaxValue;
        for (var i = 0; i < _pending.Count; ++i)
        {
            var entry = _pending[i];
            if (entry.ActionID != actionID || entry.AOE.ActorID != actorID)
                continue;

            var delta = Math.Abs((entry.AOE.Activation - activation).TotalSeconds);
            if (delta <= CastMatchTolerance && delta < bestDelta)
            {
                index = i;
                bestDelta = delta;
            }
        }
        if (index < 0)
            return null;

        var matchedActivation = _pending[index].AOE.Activation;
        _pending.RemoveAt(index);
        return matchedActivation;
    }

    private bool WasRecentlyResolved(uint actionID, ulong actorID, DateTime activation)
        => _resolved.Any(resolved => resolved.ActionID == actionID && resolved.ActorID == actorID && Math.Abs((resolved.Activation - activation).TotalSeconds) <= TombstoneWindow);

    private void RememberResolved(uint actionID, ulong actorID, DateTime activation, DateTime now)
    {
        _resolved.RemoveAll(resolved => resolved.ActionID == actionID
            && resolved.ActorID == actorID
            && Math.Abs((resolved.Activation - activation).TotalSeconds) <= CastMatchTolerance);
        _resolved.Add(new(actionID, actorID, activation, now.AddSeconds(TombstoneWindow)));
    }

    private bool IsDuplicateEvent(uint globalSequence, uint actionID, ulong actorID, DateTime now)
    {
        if (globalSequence == 0)
            return false;

        var key = new EventKey(globalSequence, actionID, actorID);
        if (_seenEvents.TryGetValue(key, out var expiresAt) && now <= expiresAt)
            return true;

        _seenEvents[key] = now.AddSeconds(EventDedupWindow);
        return false;
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        for (var i = _pending.Count - 1; i >= 0; --i)
            if (now > _pending[i].AOE.Activation.AddSeconds(ExpireDelay)) _pending.RemoveAt(i);
        for (var i = _resolved.Count - 1; i >= 0; --i)
            if (now > _resolved[i].ExpiresAt) _resolved.RemoveAt(i);
        foreach (var (key, expiresAt) in _seenEvents)
            if (now > expiresAt) _seenEvents.Remove(key);
    }

    private void RemoveActor(ulong actorID)
    {
        _pending.RemoveAll(entry => entry.AOE.ActorID == actorID);
        _resolved.RemoveAll(entry => entry.ActorID == actorID);
        foreach (var key in _seenEvents.Keys.Where(key => key.ActorID == actorID).ToArray())
            _seenEvents.Remove(key);
    }
}
