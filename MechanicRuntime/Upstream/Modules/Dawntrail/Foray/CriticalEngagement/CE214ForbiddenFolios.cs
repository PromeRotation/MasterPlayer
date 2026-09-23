using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE214ForbiddenFolios;

public enum OID : uint
{
    Boss = 0x4BD3, // R6.0, BNpcName 14520, forbidden folios
    Pages64 = 0x4BD4, // R1.0, 64 pages - announces level-5 death sector
    Pages16 = 0x4BD5, // R1.0, 16 pages - announces level-3 flare sector
    Pages8 = 0x4BD6, // R1.0, 8 pages - announces level-4 holy sector
    Pages512 = 0x4BD7, // R1.0, 512 pages - announces prime-death sector
    BookTrap = 0x4BD8, // R1.0, book-drop trap
    Helper = 0x233C
}

public enum AID : uint
{
    KnowledgeLevelCorrectionVisual = 0xB8C0, // boss->self, 5.0s cast, applies correction statuses
    KnowledgeLevelCorrection = 0xB8C1, // helper->players, no cast, correction application
    BlotVisual = 0xB8C4, // boss->self, 3.0s cast, visual
    Blot = 0xB8C5, // helper->location, 8.0s cast, range 15 circle
    CoverToCoverFirst = 0xB8C6, // boss->self, 4.0s cast, range 30 180-degree cone
    CoverToCoverSecond = 0xB8C7, // boss->self, 1.0s cast, range 30 180-degree cone
    ArcaneRule = 0xB8C8, // boss->self, 6.0s cast, visual
    QuadRule = 0xB8C9, // boss->self, grid-rule visual
    HorizontalRule = 0xB8CA, // helper->location, range 50 width 12 rect
    SummonPages = 0xB8CB, // helper->location, page summon visual

    KnowledgeLevel3FlareWide = 0xB8CD, // helper->self, range 25 180-degree cone（已实测确认 2026-08-18：helper 233C 施放 47309 "知见3级核爆" 3级宽版，与 page 4BD5 的 47316 同步；已接 KnowledgeSectors.ConfigFor → Level3Wide/Sector180）
    KnowledgeLevel4HolyWide = 0xB8CE, // helper->self, range 25 180-degree cone
    KnowledgeLevel5Death = 0xB8CF, // helper->self, range 25 120-degree cone
    KnowledgeLevel5DeathBook = 0xB8CC, // two-book round: the 5级 sector is cast with this page-side AID (47308) instead of B8CF
    KnowledgeLevel3Flare = 0xB8D0, // helper->self, range 25 120-degree cone
    KnowledgeLevel4Holy = 0xB8D1, // helper->self, range 25 120-degree cone
    PrimeKnowledgeLevelDeath = 0xB8D2, // helper->self, range 25 120-degree cone
    PageLevel5Visual = 0xB8D3, // page->self, visual（实测 180° 轮 page 读条，2026-08-16 回放；对应 helper 47308/50554）
    PageLevel3Visual = 0xB8D4, // page->self, visual（已实测确认 2026-08-18：page 4BD5 施放 47316 "知见3级核爆" 3级宽版 visual，与 helper 233C 的 47309/50555 同步；已接 KnowledgeSectors.ConfigFor → Level3Wide/Sector180）
    PageLevel4Visual = 0xB8D5, // page->self, visual（已实测确认 2026-08-18：page 4BD6 施放 47317 "知见4级神圣" 4级宽版 visual，与 helper 233C 的 47310/50556 同步；已接 KnowledgeSectors.ConfigFor → Level4Wide/Sector180）
    PagePrimeVisual = 0xB8D6, // page->self, visual（实测 180° 轮 page 读条，2026-08-16 回放；对应 helper 49879/50561）
    BookDropVisual = 0xB8D7, // boss->self, visual
    BookDrop = 0xB8DA, // book trap->self, 8.0s cast, range 3 circle
    ThunderII = 0xB8DC, // helper->self, 4.0s cast, range 50 width 5 rect
    FireII = 0xB8DD, // helper->self, 5.0s cast, range 60 45-degree cone
    FireIIVisual = 0xB8DE, // boss->self, visual
    MarginaliaHit = 0xB8DF, // helper->players, duplicate raidwide damage
    Marginalia = 0xB8E0, // boss->self, 5.0s cast, raidwide visual

    UnknownBC76 = 0xBC76, // observed boss event/cleanup
    SummonVisual = 0xBF9F, // boss->self, summon visual
    AutoAttack = 0xBFA0, // boss->player, no cast, single-target
    UnboundInk = 0xC154, // boss->self, 4.0s cast, range 9 circle
    PrimeKnowledgeLevelDeathWide = 0xC2D7, // helper->self, range 25 180-degree cone

    KnowledgeLevel3FlareWideAlt = 0xC57B, // helper->self, duplicate of B8CD（已实测确认 2026-08-18：50555 即 3级宽版，与 47309 同源同步；已接 KnowledgeSectors.ConfigFor → Level3Wide/Sector180）
    KnowledgeLevel4HolyWideAlt = 0xC57C, // helper->self, duplicate of B8CE
    KnowledgeLevel5DeathAlt = 0xC57D, // helper->self, duplicate of B8CF
    KnowledgeLevel5DeathBookAlt = 0xC57A, // two-book round: duplicate of B8CC (50554)
    KnowledgeLevel3FlareAlt = 0xC57E, // helper->self, duplicate of B8D0
    KnowledgeLevel4HolyAlt = 0xC57F, // helper->self, duplicate of B8D1
    PrimeKnowledgeLevelDeathAlt = 0xC580, // helper->self, duplicate of B8D2
    PrimeKnowledgeLevelDeathWideAlt = 0xC581 // helper->self, duplicate of C2D7
}

public enum SID : uint
{
    Correction1 = 0x1396, // knowledge level +1
    Correction2 = 0x1397, // knowledge level +2
    Correction3 = 0x1398, // knowledge level +3
    Correction4 = 0x1399, // knowledge level +4
    Correction5 = 0x139A // knowledge level +5
}

// These location/self casts expose authoritative warning packets, including the initial cross
// writing and the four-yalm page landing circles.
sealed class BasicAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Blot = new(15f);
    private static readonly AOEShapeCircle SummonPages = new(4f);
    // Initial cross writing: 10y-wide arms (5 half-width) per user testing - the wider 6.5
    // trial value was confirmed too large, so the action-sheet width is kept.
    private static readonly AOEShapeCross QuadRule = new(25f, 5f);
    private static readonly AOEShapeCone FireII = new(60f, 22.5f.Degrees());

    // Blot exposes three rows of three circles at roughly two-second intervals. The opener is
    // "third into first": both of the first two rows must be forbidden so the third row is the
    // only pre-position, then the first row becomes available after it resolves. Replay cast-start
    // spacing reaches 2.026s, so a literal 2.0s cutoff incorrectly made the second row look safe.
    protected override double RiskyActivationWindow => 2.25d;

    // 溅墨三行 AI 紧迫值方案（2026-08-06 回放验证）：三行 9 圆覆盖全场（r24 场地四角距最近圆
    // 仅 3.3y），纯避让没有安全点。按"最后一组就位 → 第一组结算后进第一组"引导：
    // - 第一组：正常紧迫（activation 不变）
    // - 第二组：紧迫值恒 = now（G=0 硬禁飞，AI 永不进第二组）
    // - 第三组：第一组结算前不加禁区（AI 视为安全区，自然前往就位）；第一组结算后恢复正常
    //   （AI 被赶出第三组，唯一安全区 = 第一组结算后的区域）
    // 只影响 AI 层（ForbiddenZone），ActiveAOEs 显示层一行未动。
    private bool _sawFullSet;

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        ActiveAOEs(slot, actor); // 刷新 Risky 标记

        var blot = new List<PendingAOE>();
        foreach (var p in Pending)
        {
            if (p.ActionID == (uint)AID.Blot)
                blot.Add(p);
            else if (p.AOE.Risky)
                hints.AddForbiddenZone(p.AOE.ShapeDistance ?? p.AOE.Shape.Distance(p.AOE.Origin, p.AOE.Rotation), p.AOE.Activation);
        }
        if (blot.Count == 0)
        {
            _sawFullSet = false;
            return;
        }

        // 同组 3 圆同时 cast（activation 一致），间隔 >1s 分界
        blot.Sort((a, b) => a.AOE.Activation.CompareTo(b.AOE.Activation));
        var groups = new List<List<PendingAOE>>();
        foreach (var p in blot)
        {
            if (groups.Count == 0 || Math.Abs((p.AOE.Activation - groups[^1][0].AOE.Activation).TotalSeconds) > 1d)
                groups.Add([p]);
            else
                groups[^1].Add(p);
        }
        if (groups.Count >= 3)
            _sawFullSet = true;

        var now = WorldState.CurrentTime;
        // 第三组仅在完整三组且第一组未结算时隐藏；第一组结算后（组数回落或已过其 activation）恢复
        var hideThird = groups.Count == 3 && now < groups[0][0].AOE.Activation;
        for (var gi = 0; gi < groups.Count; ++gi)
        {
            var g = groups[gi];
            var act = g[0].AOE.Activation;
            if (act <= now)
                continue; // 已结算组：跳过，避免已结算 AOE 变 G=0 禁飞阻塞安全区

            bool second, third;
            if (groups.Count == 3)
            {
                second = gi == 1;
                third = gi == 2;
            }
            else if (groups.Count == 2 && _sawFullSet)
            {
                second = gi == 0; // 第一组已结算：剩余最早 = 第二组
                third = gi == 1;
            }
            else if (groups.Count == 2)
            {
                second = gi == 1; // 第三组尚未 cast：[第一, 第二]
                third = false;
            }
            else
            {
                second = third = false;
            }

            if (third && hideThird)
                continue;

            foreach (var p in g)
            {
                if (!p.AOE.Risky)
                    continue;
                var activation = second ? now : act;
                hints.AddForbiddenZone(p.AOE.ShapeDistance ?? p.AOE.Shape.Distance(p.AOE.Origin, p.AOE.Rotation), activation);
            }
        }
    }

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.Blot => new(Blot, true),
        (uint)AID.QuadRule => new(QuadRule, true),
        (uint)AID.SummonPages => new(SummonPages),
        (uint)AID.FireII => new(FireII),
        _ => null
    };
}

// Cover to Cover sweeps one half first, then the opposite half roughly four seconds later. The
// second sweep's own cast is only 0.7s, which automation cannot react to, so publish the second
// sweep's danger zone from the moment the first sweep resolves (replay: 16 victims in the second
// sweep because it appeared too late).
sealed class CoverToCoverSequence(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Shape = new(30f, 90f.Degrees());
    private const double SecondResolveDelay = 4.2d;
    private readonly List<AOEInstance> _displayed = [with(2)];
    private AOEInstance? _first;
    private AOEInstance? _second;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        if (_first is { } first)
            _displayed.Add(first);
        if (_second is { } second)
            _displayed.Add(second);
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return;

        switch (spell.Action.ID)
        {
            case (uint)AID.CoverToCoverFirst:
                _first = new(Shape, caster.Position, spell.Rotation, Module.CastFinishAt(spell), Colors.Danger, true, caster.InstanceID, Shape.Distance(caster.Position, spell.Rotation));
                break;
            case (uint)AID.CoverToCoverSecond:
                _second = new(Shape, caster.Position, spell.Rotation, Module.CastFinishAt(spell), Colors.Danger, true, caster.InstanceID, Shape.Distance(caster.Position, spell.Rotation));
                break;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        var now = WorldState.CurrentTime;
        switch (spell.Action.ID)
        {
            case (uint)AID.CoverToCoverFirst:
                _first = null;
                // The first half is now swept; warn about the opposite half until the second sweep lands.
                var predictedRotation = spell.Rotation + 180f.Degrees();
                _second = new(Shape, caster.Position, predictedRotation, now.AddSeconds(SecondResolveDelay), Colors.Danger, true, caster.InstanceID, Shape.Distance(caster.Position, predictedRotation));
                break;
            case (uint)AID.CoverToCoverSecond:
                _second = null;
                break;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_first is { ActorID: var firstID } && firstID == actor.InstanceID)
            _first = null;
        if (_second is { ActorID: var secondID } && secondID == actor.InstanceID)
            _second = null;
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        if (_first is { Activation: var firstAct } && now > firstAct.AddSeconds(1d))
            _first = null;
        if (_second is { Activation: var secondAct } && now > secondAct.AddSeconds(1d))
            _second = null;
    }
}

// Thunder II arrives in two batches, two seconds apart. Draw both batches for planning while only
// making the earliest simultaneous set risky; otherwise automation sees the complete square as
// forbidden and oscillates. Each helper's origin and rotation are the actual lane geometry.
sealed class ThunderII(BossModule module) : ReplayValidatedCastAOEs(module)
{
    // Hit reconstruction puts every confirmed target within 2.74y of the lane center (including
    // player hitbox) while non-targets begin at the same boundary. A 5y-wide lane leaves the
    // intended 5y gaps between helpers spaced 10y apart; width 10 falsely tiles the whole arena.
    private static readonly AOEShapeRect Shape = new(50f, 2.5f);
    // Both batches resolve two seconds apart. Widening the risk window to cover both at once made
    // the full lane frame leave no safe cell (regression: noSafeFrames), so the second batch must
    // stay preview until the first resolves; the 0.25s tail is unavoidable without a per-batch
    // deadline. The "three-through-one" weave the operator reported is the ink grid, not thunder.
    protected override double RiskyActivationWindow => 0.25d;
    protected override DateTime? CompetingActivation => Module.FindComponent<BasicAOEs>()?.EarliestActivation;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == (uint)AID.ThunderII ? new(Shape) : null;
}

// Quad Rule emits four waves at roughly two-second intervals. Unlike ordinary self AOEs, B8CA's
// facing points away from its location target in the recording, so derive the lane direction from
// source -> LocXZ. The fixed 50-yalm length intentionally extends to the arena edge.
sealed class HorizontalRule(BossModule module) : Components.GenericAOEs(module)
{
    // 6y-wide lanes (3 half-width): the grid requires the lanes to tile without overlap or gaps
    // (batch step is 6y, so the lane width must be 6y too), and the replay hits peak at 2.94y
    // off-axis (5y half-width is excluded by that sample). lengthBack=50 makes the lane span the
    // whole arena both ways along its axis (vertical lanes north-south, horizontal east-west) -
    // the default 0 left only the half toward the cast direction, showing short lanes.
    private static readonly AOEShapeRect Shape = new(50f, 3f, 50f);
    private const double EventResolveTolerance = 0.5d;
    private const double ExpireDelay = 2d;
    private readonly List<AOEInstance> _pending = [with(16)];
    private readonly List<AOEInstance> _displayed = [with(16)];
    private readonly HashSet<uint> _seenGlobalSequences = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        if (_pending.Count == 0)
            return CollectionsMarshal.AsSpan(_displayed);

        var deadline = _pending[0].Activation.AddSeconds(0.25d);
        foreach (var source in _pending)
        {
            var aoe = source;
            var imminent = aoe.Activation <= deadline;
            aoe.Color = imminent ? Colors.Danger : Colors.AOE;
            aoe.Risky = imminent;
            _displayed.Add(aoe);
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.HorizontalRule || spell.EventHappened)
            return;

        PruneExpired();
        var activation = Module.CastFinishAt(spell);
        var direction = spell.LocXZ - caster.Position;
        if (activation <= WorldState.CurrentTime || direction.LengthSq() < 0.01f)
            return;

        // This component only serves the cursive-writing lanes, so a new batch fully replaces the
        // previous one. The four helpers of one batch cast at the same time (identical activation),
        // so clear only when a new batch starts (~2s apart) - an unconditional clear would run once
        // per helper callback and leave only the last lane of the batch (user-verified "1 lane").
        // Per-InstanceID removal was unsafe too: the helpers reuse instance IDs across batches
        // (batch 1/3 and batch 2/4 share the same IDs), leaving the old batch alongside the new one.
        if (_pending.Count != 0 && Math.Abs((_pending[0].Activation - activation).TotalSeconds) > 0.5d)
            _pending.Clear();
        var rotation = Angle.FromDirection(direction);
        // The float coordinates (LocXZ vs caster position) skew the direction by a fraction of a
        // degree; snap to the nearest cardinal so vertical lanes run exactly north-south and
        // horizontal lanes exactly east-west (no pixel-level tilt).
        var snapped = MathF.Round(rotation.Rad / (MathF.PI / 2f)) * (MathF.PI / 2f);
        rotation = new Angle(snapped);
        _pending.Add(new(Shape, caster.Position, rotation, activation, actorID: caster.InstanceID, shapeDistance: Shape.Distance(caster.Position, rotation)));
        _pending.Sort((left, right) => left.Activation.CompareTo(right.Activation));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.HorizontalRule && (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime.AddSeconds(EventResolveTolerance)))
            // Only the already-resolving entry may be removed: the four helpers reuse instance IDs
            // across batches, so a late finish/effect event of the previous batch must not delete
            // the freshly created next-batch entries (their activation is still in the future).
            _pending.RemoveAll(aoe => aoe.ActorID == caster.InstanceID && aoe.Activation <= WorldState.CurrentTime.AddSeconds(EventResolveTolerance));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.HorizontalRule || spell.GlobalSequence != 0 && !_seenGlobalSequences.Add(spell.GlobalSequence))
            return;

        // Same instance-ID reuse guard as OnCastFinished.
        _pending.RemoveAll(aoe => aoe.ActorID == caster.InstanceID && aoe.Activation <= WorldState.CurrentTime.AddSeconds(EventResolveTolerance));
        ++NumCasts;
    }

    public override void OnActorDeath(Actor actor) => _pending.RemoveAll(aoe => aoe.ActorID == actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _pending.RemoveAll(aoe => aoe.ActorID == actor.InstanceID);

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _pending.RemoveAll(aoe => now > aoe.Activation.AddSeconds(ExpireDelay));
    }
}

// The three (sometimes two) page actors each announce a sector type via NPC yell, then a helper
// casts the corresponding cone. The cone originates at the page's own position (12.5y from center)
// and faces the arena center; replay victims all sit inside a 25y cone from the page toward the
// center (half-angle 60 for 120-degree sectors, 90 for the 180-degree wide variants). A player's
// final knowledge level is the absolute account-wide progress (ForayInfo.Level, 20-40) plus the
// per-round correction status; a sector is dangerous only when that final level satisfies the
// sector's rule (final % N == 0, or prime for the prime sectors). This must remain a per-player
// ActiveAOEs calculation; globally painting every sector red is mechanically wrong.
sealed class KnowledgeSectors(BossModule module) : Components.GenericAOEs(module)
{
    private enum SectorKind { Level3, Level3Wide, Level4, Level4Wide, Level5, Prime, PrimeWide }
    private readonly record struct SectorConfig(SectorKind Kind, AOEShape Shape, OID PageOID);

    private sealed class PendingSector(SectorKind kind, AOEShape shape, Angle rotation, DateTime activation, ulong casterID)
    {
        public readonly SectorKind Kind = kind;
        public readonly AOEShape Shape = shape;
        public readonly Angle Rotation = rotation;
        public readonly DateTime Activation = activation;
        public readonly HashSet<ulong> Casters = [casterID];
    }

    private static readonly AOEShapeCone Sector120 = new(25f, 60f.Degrees());
    private static readonly AOEShapeCone Sector180 = new(25f, 90f.Degrees());
    private readonly List<PendingSector> _pending = [with(6)];
    private readonly List<AOEInstance> _displayed = [with(6)];
    private readonly HashSet<uint> _seenGlobalSequences = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        var correction = Correction(actor);
        // If the knowledge level or the correction status is unavailable (e.g. ForayInfo memory
        // read failed and Level stayed 0), we cannot tell which sectors are safe for this player.
        // Never return empty - fall back to painting every sector dangerous so the player still
        // gets warned.
        var unknown = actor.ForayInfo.Level <= 0 || correction == 0;
        var level = actor.ForayInfo.Level + correction;

        foreach (var sector in _pending)
        {
            // The knowledge cone radiates from the boss (arena center) toward the announced
            // direction; the page merely announces which rule the sector uses.
            var direction = sector.Rotation;
            if (!unknown && SatisfiesRule(level, sector.Kind))
                continue; // safe sector: no zone drawn (the in-arena green guide was removed per user feedback)

            _displayed.Add(new(sector.Shape, Module.Arena.Center, direction, sector.Activation,
                actorID: sector.Casters.FirstOrDefault(), shapeDistance: sector.Shape.Distance(Module.Arena.Center, direction)));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (_pending.Count == 0)
            return;

        var correction = Correction(actor);
        var unknown = actor.ForayInfo.Level <= 0 || correction == 0;
        if (unknown)
        {
            hints.Add("Knowledge level unavailable - all sectors marked dangerous", true);
            return;
        }

        var level = actor.ForayInfo.Level + correction;
        hints.Add($"Knowledge level {level} (base {actor.ForayInfo.Level} + {correction})");
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (ConfigFor(spell.Action.ID) is not { } config || spell.EventHappened)
            return;

        PruneExpired();
        var activation = Module.CastFinishAt(spell);
        if (activation <= WorldState.CurrentTime)
            return;

        var existing = _pending.FirstOrDefault(sector => sector.Kind == config.Kind
            && Math.Abs((sector.Activation - activation).TotalSeconds) <= 0.25d
            && sector.Rotation.AlmostEqual(spell.Rotation, Angle.DegToRad));
        if (existing != null)
        {
            existing.Casters.Add(caster.InstanceID);
            return;
        }

        _pending.Add(new(config.Kind, config.Shape, spell.Rotation, activation, caster.InstanceID));
        _pending.Sort((left, right) => left.Activation.CompareTo(right.Activation));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (ConfigFor(spell.Action.ID) != null)
            RemoveCaster(caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (ConfigFor(spell.Action.ID) == null || spell.GlobalSequence != 0 && !_seenGlobalSequences.Add(spell.GlobalSequence))
            return;

        RemoveCaster(caster.InstanceID);
        ++NumCasts;
    }

    public override void OnActorDeath(Actor actor) => RemoveCaster(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => RemoveCaster(actor.InstanceID);

    private static SectorConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.KnowledgeLevel3Flare or (uint)AID.KnowledgeLevel3FlareAlt => new(SectorKind.Level3, Sector120, OID.Pages16),
        (uint)AID.KnowledgeLevel3FlareWide or (uint)AID.KnowledgeLevel3FlareWideAlt or (uint)AID.PageLevel3Visual => new(SectorKind.Level3Wide, Sector180, OID.Pages16),
        (uint)AID.KnowledgeLevel4Holy or (uint)AID.KnowledgeLevel4HolyAlt => new(SectorKind.Level4, Sector120, OID.Pages8),
        (uint)AID.KnowledgeLevel4HolyWide or (uint)AID.KnowledgeLevel4HolyWideAlt or (uint)AID.PageLevel4Visual => new(SectorKind.Level4Wide, Sector180, OID.Pages8),
        (uint)AID.KnowledgeLevel5Death or (uint)AID.KnowledgeLevel5DeathAlt => new(SectorKind.Level5, Sector120, OID.Pages64),
        // The two-book rounds cast the 5级 sector with the page-side AIDs B8CC/50554 instead of
        // B8CF/C57D, and each book covers a full 180-degree sector (not the 120 used by the
        // three-book rounds); without these mappings that round showed no sector at all.
        (uint)AID.KnowledgeLevel5DeathBook or (uint)AID.KnowledgeLevel5DeathBookAlt => new(SectorKind.Level5, Sector180, OID.Pages64),
        (uint)AID.PrimeKnowledgeLevelDeath or (uint)AID.PrimeKnowledgeLevelDeathAlt => new(SectorKind.Prime, Sector120, OID.Pages512),
        (uint)AID.PrimeKnowledgeLevelDeathWide or (uint)AID.PrimeKnowledgeLevelDeathWideAlt => new(SectorKind.PrimeWide, Sector180, OID.Pages512),
        _ => null
    };

    private static int Correction(Actor actor)
    {
        if (actor.FindStatus((uint)SID.Correction1) != null) return 1;
        if (actor.FindStatus((uint)SID.Correction2) != null) return 2;
        if (actor.FindStatus((uint)SID.Correction3) != null) return 3;
        if (actor.FindStatus((uint)SID.Correction4) != null) return 4;
        if (actor.FindStatus((uint)SID.Correction5) != null) return 5;
        return 0;
    }

    // Replay-verified: the sectors are named 知见3级核爆 / 知见4级神圣 / 知见5级即死 / 知见质数即死,
    // and every recorded victim died in a sector whose condition their final absolute knowledge
    // level satisfied. The sector is therefore SAFE only when the condition does NOT hold.
    private static bool SatisfiesRule(int level, SectorKind kind) => kind switch
    {
        SectorKind.Level3 or SectorKind.Level3Wide => level % 3 != 0,
        SectorKind.Level4 or SectorKind.Level4Wide => level % 4 != 0,
        SectorKind.Level5 => level % 5 != 0,
        SectorKind.Prime or SectorKind.PrimeWide => !IsPrime(level),
        _ => false
    };

    private static bool IsPrime(int value)
    {
        if (value < 2)
            return false;
        for (var divisor = 2; divisor * divisor <= value; ++divisor)
            if (value % divisor == 0)
                return false;
        return true;
    }

    private void RemoveCaster(ulong casterID)
    {
        for (var i = _pending.Count - 1; i >= 0; --i)
        {
            var sector = _pending[i];
            if (sector.Casters.Remove(casterID) && sector.Casters.Count == 0)
                _pending.RemoveAt(i);
        }
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _pending.RemoveAll(sector => now > sector.Activation.AddSeconds(2d));
    }
}

// Replay/operator correction: Unbound Ink (泼墨) is a steel-style avoidable 9y circle - victims
// stood inside it and died - not a soak tower. BookDrop (丢书) is the actual tower players must
// stand in (victims cluster inside each 3y book). Draw Unbound Ink as a red circle and BookDrop
// as a tower.
sealed class UnboundInk(BossModule module) : Components.SimpleAOEs(module, (uint)AID.UnboundInk, new AOEShapeCircle(9f));
// 丢书塔: 所有书塔都需要显示，但 AI 只从同一波中固定选一个前往，不能把显示列表
// 直接交给 GenericTowers，否则它会尝试同时补满每一个塔。
sealed class BookDropTower(BossModule module) : Components.GenericTowers(module, (uint)AID.BookDrop)
{
    private const float Radius = 3f;
    private const int MaxTowersPerWave = 1;
    private const int WaveSize = 5;
    private readonly List<(ulong ID, WPos Pos, DateTime Act)> _pending = [with(8)];
    private readonly List<Components.GenericTowers.Tower> _visibleTowers = [with(8)];
    private readonly Random _rng = new();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction)
            return;
        var tower = (caster.InstanceID, spell.LocXZ, Module.CastFinishAt(spell));
        _pending.Add(tower);
        _visibleTowers.Add(new(tower.Item2, Radius, 1, 2, activation: tower.Item3, actorID: tower.Item1));
        if (_pending.Count < WaveSize)
            return;

        for (var i = 0; i < MaxTowersPerWave; ++i)
        {
            var index = _rng.Next(_pending.Count);
            var t = _pending[index];
            Towers.Add(new(t.Pos, Radius, 1, 2, activation: t.Act, actorID: t.ID));
            _pending.RemoveAt(index);
        }
        _pending.Clear();
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            Towers.RemoveAll(t => t.ActorID == caster.InstanceID);
            _visibleTowers.RemoveAll(t => t.ActorID == caster.InstanceID);
            _pending.RemoveAll(t => t.ID == caster.InstanceID);
        }
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        var towers = CollectionsMarshal.AsSpan(_visibleTowers);
        for (var i = 0; i < towers.Length; ++i)
        {
            ref var tower = ref towers[i];
            Components.GenericTowers.DrawTower(Arena, ref tower, safe: true);
        }
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        Towers.RemoveAll(t => now > t.Activation.AddSeconds(2d));
        _visibleTowers.RemoveAll(t => now > t.Activation.AddSeconds(2d));
        _pending.RemoveAll(t => now > t.Act.AddSeconds(2d));
        base.Update();
    }
}

// The three B8DF helpers carry duplicate damage packets; the boss cast is the stable warning.
sealed class Marginalia(BossModule module) : Components.RaidwideCast(module, (uint)AID.Marginalia);

sealed class ForbiddenFoliosStates : StateMachineBuilder
{
    public ForbiddenFoliosStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<BasicAOEs>()
            .ActivateOnEnter<CoverToCoverSequence>()
            .ActivateOnEnter<ThunderII>()
            .ActivateOnEnter<HorizontalRule>()
            .ActivateOnEnter<KnowledgeSectors>()
            .ActivateOnEnter<UnboundInk>()
            .ActivateOnEnter<BookDropTower>()
            .ActivateOnEnter<Marginalia>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(ForbiddenFoliosStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 52u,
    SortOrder = 13)]
// Circular arena, radius 24y: 2026-08-06 replay measured a player hugging the wall at 24.22y
// from center (stops, probes, turns back), so the previous r20 excluded the outer ring from AI
// pathfinding; 24f keeps 0.2y margin over the measured 24.22y. The Horizontal Rule lanes are
// projected from outside (r26-36), which previously misled the bounds into a 25y square.
public sealed class ForbiddenFolios(WorldState ws, Actor primary) : BossModule(ws, primary, new(659f, 659f), new ArenaBoundsCircle(24f))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.Pages8));
        Arena.Actors(Enemies((uint)OID.Pages16));
        Arena.Actors(Enemies((uint)OID.Pages64));
        Arena.Actors(Enemies((uint)OID.Pages512));
    }
}
