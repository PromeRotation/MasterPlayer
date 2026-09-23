using BossMod.Dawntrail.Foray.CriticalEngagement;
using static BossMod.Components.GenericKnockback;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE206RebelliousFamiliar;

public enum OID : uint
{
    Boss = 0x4C4F, // R3.8, BNpcName 14791, cornered gemstone
    YellowGem = 0x4C50,
    BoundaryController = 0x4D88, // non-targetable controller at arena center
    RubyWallTian = 0x1EC045, // baseid 2015301 (decimal), 田 ruby wall (EAnim 2 = appear)
    RubyWallL = 0x1EC046, // baseid 2015302 (decimal), L/¬ ruby wall (EAnim 20/200 = moving)
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 0xC6A4, // boss->player, no cast, single-target
    LethalBoundary = 0xBFD0, // controller, persistent out-of-bounds kill field
    YellowGemstones = 0xBC98,
    YellowGemActiveVisual = 0xBC99, // yellow gem->location, 3.0s cast, no damage event
    TopazRay = 0xBC9A, // yellow gem->location, 3.0s cast, range 4 circle
    RubyLight = 0xBC9C,
    RubyReflectionShort = 0xBC9D, // helper, 20y long, 20y wide rect
    RubyReflectionLong1 = 0xBC9E, // helper, 40y long, 40y wide rect
    RubyReflectionLong2 = 0xBC9F,
    CircularKnockbackTelegraph = 0xBCA0, // helper, 60y circle; resolves as 30y away knockback
    KnockAsideTelegraph = 0xBCA1, // helper, 40y long, 60y wide rect; resolves as 15y source-left/right knockback per target side
    RavenousGods = 0xBCA3,
    RavenousGodsSecond = 0xBCA4,
    ClawThenTail = 0xBCA6, // 45y 180-degree cone
    TailThenClaw = 0xBCA7, // 40y 180-degree cone
    ClawThenTailSecond = 0xBCA8,
    TailThenClawSecond = 0xBCA9,
    Howl = 0xBCAA,
    ComboEndVisual = 0xBCAB, // boss, no targets/effects; animation-only combo terminator
    RubyOuterReflection = 0xC4F2,
    RevertModel = 0xC51D, // boss, model-state reset after the claw/tail sequence
    RubyGlowHit = 0xC5CD, // helpers, split packets for the Ruby Light raidwide
    HowlAlt = 0xC161,
    RavenousGodsCircleHit = 0xC162,
    RavenousGodsAsideHit = 0xC163
}


sealed class ClawTailCombo(BossModule module) : ReplayValidatedOppositeAOEs(module)
{
    private static readonly AOEShapeCone Claw = new(45f, 90f.Degrees());
    private static readonly AOEShapeCone Tail = new(40f, 90f.Degrees());

    protected override SequenceConfig? ConfigFor(uint firstActionID) => firstActionID switch
    {
        (uint)AID.ClawThenTail => new(Claw, Tail, (uint)AID.ClawThenTailSecond, 2d),
        // Replay-verified: even though the boss visually spins around before the first hit lands,
        // both first hits resolve centered on the cast-start rotation (hits within +-90 deg of it)
        // and both second hits resolve on the opposite half. No rotation offset for either combo.
        (uint)AID.TailThenClaw => new(Tail, Claw, (uint)AID.TailThenClawSecond, 2d),
        _ => null
    };
}

sealed class TopazRay(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(4f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID == (uint)AID.TopazRay ? new(Shape, true) : null;
}

sealed class LethalBoundary(BossModule module) : Components.GenericAOEs(module)
{
    // Arena radius is 20y (4x4 grid of 10y cells, trigger XML R=20); the kill fence is that square.
    private static readonly AOEShapeRect Shape = new(20f, 0.5f, 20f);
    private static readonly AOEShapeRect AIShape = new(20f, 1f, 20f);
    private readonly AOEInstance[] _aoes = Build(module.Arena.Center);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoes;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in _aoes)
            hints.AddForbiddenZone(AIShape.Distance(aoe.Origin, aoe.Rotation));
    }

    private static AOEInstance[] Build(WPos center)
    {
        var result = new AOEInstance[4];
        for (var i = 0; i < result.Length; ++i)
        {
            var normal = (i * 90f).Degrees().ToDirection();
            var rotation = Angle.FromDirection(normal.OrthoL());
            var origin = center + 20f * normal;
            result[i] = new(Shape, origin, rotation, color: Colors.Danger, risky: false, shapeDistance: Shape.Distance(origin, rotation));
        }
        return result;
    }
}

// North-Horn trigger XML logic (北岛.xml, "08. 叛逆使魔——负隅宝石兽"):
// - Arena is a 4x4 grid of 10y cells (R=20). Ruby walls (baseid 2015301 田 / 2015302 L-shaped,
//   EAnim 2 = appear, 20/200 = moving; facing parity selects the table) define four candidate
//   dangerous regions of four whole cells.
// - Yellow gemstones (0x4C50) near a cell edge (max |dx|,|dy| >= 2.9y) each contribute one
//   ColRowDir4 token. Triggernometry's VecToDir uses the game heading convention (x,z),
//   indexed N=0, W=1, S=2, E=3.
// - The dangerous region is the candidate whose edge-token set intersects the observed tokens;
//   announce it as soon as the wall appears so AI can route away before the line resolves.
internal static class RubyReflectionData
{
    // Triggernometry Roundvec indexes cardinal directions from north, counter-clockwise.
    // (N=0, W=1, S=2, E=3.)
    public static int VecToDir(float dx, float dz)
        => (int)MathF.Round((MathF.Atan2(dx, dz) / MathF.PI + 1f) * 2f) & 3;

    // Triggernometry 2.x RadToDir (renamed from roundir): north is a segment point, indexes are
    // N=0, W=1, S=2, E=3 for n=4.  Implementation mirrors MathParser.ProcessRoundir:
    //   dir = (rad/pi + 1)/2 * n;  dir = mod(dir + 0.5, n) - 0.5;  round (banker's).
    public static int RadToDir(float rad, int segments)
    {
        var dir = (rad / MathF.PI + 1f) / 2f * segments;
        dir = Mod(dir + 0.5f, segments) - 0.5f;
        return (int)MathF.Round(dir, MidpointRounding.ToEven);
    }

    private static float Mod(float a, float n) => a - MathF.Floor(a / n) * n;

    public static int[] SelectCells(string tableName, IEnumerable<string> edgeGems)
    {
        var tokens = edgeGems.ToHashSet(StringComparer.Ordinal);
        var table = tableName switch
        {
            "tian" => WallTableTian,
            "l20p0" => WallTableL20P0,
            "l20p1" => WallTableL20P1,
            "l200p0" => WallTableL200P0,
            "l200p1" => WallTableL200P1,
            _ => null
        };
        if (table == null)
            return [];

        var cells = new HashSet<int>();
        foreach (var zone in table)
            if (zone.Keys.Any(tokens.Contains))
                cells.UnionWith(zone.Cells);
        return [.. cells.Order()];
    }

    private static readonly (string[] Keys, int[] Cells)[] WallTableTian =
    [
        (["122", "213", "222", "223"], [11, 12, 21, 22]),
        (["130", "230", "233", "243"], [13, 14, 23, 24]),
        (["311", "321", "322", "422"], [31, 32, 41, 42]),
        (["330", "331", "341", "430"], [33, 34, 43, 44]),
    ];
    private static readonly (string[] Keys, int[] Cells)[] WallTableL20P0 =
    [
        (["113", "122", "220", "222", "320", "322", "323"], [11, 12, 22, 32]),
        (["130", "133", "240", "340", "343"], [13, 14, 24, 34]),
        (["211", "212", "312", "421", "422"], [21, 31, 41, 42]),
        (["230", "231", "232", "330", "332", "430", "441"], [23, 33, 43, 44]),
    ];
    private static readonly (string[] Keys, int[] Cells)[] WallTableL20P1 =
    [
        (["123", "132", "133", "212", "213"], [11, 12, 13, 21]),
        (["140", "220", "221", "223", "231", "233", "243"], [14, 22, 23, 24]),
        (["311", "321", "323", "331", "332", "333", "412"], [31, 32, 33, 41]),
        (["340", "341", "420", "421", "431"], [34, 42, 43, 44]),
    ];
    private static readonly (string[] Keys, int[] Cells)[] WallTableL200P0 =
    [
        (["112", "213", "221", "223", "231", "232", "233"], [11, 21, 22, 23]),
        (["120", "123", "133", "240", "243"], [12, 13, 14, 24]),
        (["311", "312", "421", "431", "432"], [31, 41, 42, 43]),
        (["320", "321", "323", "331", "333", "341", "440"], [32, 33, 34, 44]),
    ];
    private static readonly (string[] Keys, int[] Cells)[] WallTableL200P1 =
    [
        (["122", "123", "212", "312", "313"], [11, 12, 21, 31]),
        (["130", "143", "230", "232", "330", "332", "333"], [13, 23, 33, 14]),
        (["220", "221", "222", "320", "322", "411", "422"], [22, 32, 41, 42]),
        (["240", "241", "340", "430", "431"], [24, 34, 43, 44]),
    ];
}

sealed class RubyReflection(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect Cell = new(5f, 5f, 5f);
    private const float EdgeGemMinOffset = 2.9f;
    // The trigger XML draws the danger region for t:15; keep the same lifetime so old zones
    // disappear once the mechanic resolves instead of lingering on the floor.
    private const double DisplayTimeout = 15d;
    private readonly List<AOEInstance> _displayed = [with(8)];
    private sealed class GemBatch(DateTime created)
    {
        public readonly DateTime Created = created;
        public readonly HashSet<string> Tokens = [];
    }

    private readonly List<GemBatch> _gemBatches = [];
    private GemBatch? _currentBatch;
    private readonly HashSet<string> _edgeGems = [];
    private readonly List<(int[] Cells, DateTime Activation)> _zones = [];
    private int _wallKind = -1;
    private Angle _wallRot;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();

        foreach (var zone in _zones)
        {
            foreach (var cell in zone.Cells)
            {
                var col = cell / 10;
                var row = cell % 10;
                // Triggernometry's PictoACT position is in the same world X/Z basis as WPos.
                // Pos=(col-2.5)*10,(row-2)*10 is the lower edge of a 10y cell; AOEShapeRect is
                // centered, so use the cell center (row-2.5) here and do not rotate the grid.
                var center = Arena.Center + new WDir((col - 2.5f) * 10f, (row - 2.5f) * 10f);
                _displayed.Add(new(Cell, center, activation: zone.Activation, color: Colors.Danger, risky: true,
                    shapeDistance: Cell.Distance(center, default)));
            }
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            hints.AddForbiddenZone(aoe.ShapeDistance ?? aoe.Shape.Distance(aoe.Origin, aoe.Rotation), aoe.Activation);
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        _zones.RemoveAll(z => now > z.Activation.AddSeconds(DisplayTimeout));
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID is (uint)OID.RubyWallTian or (uint)OID.RubyWallL)
            Service.Logger.Information($"[CE206] wall EAnim oid={actor.OID:X} p1={(state >> 16) & 0xFFFF:X4} p2={state & 0xFFFF:X4} rot={actor.Rotation.Rad:f3}");
        var anim = state & 0xFFF;
        if (actor.OID == (uint)OID.RubyWallTian && anim == 2)
            SetWall(0, actor.Rotation);
        // The ACT log line prints these anim values in hex: "2" == 0x02 (appear), "20" == 0x20 (L
        // moving), "200" == 0x200 (¬ moving). Decimal 20/200 are accepted too as a belt-and-braces
        // fallback in case a client reports them as raw decimal.
        else if (actor.OID == (uint)OID.RubyWallL && (anim is 0x20 or 0x200 or 20 or 200))
            SetWall(anim is 0x20 or 20 ? 1 : 3, actor.Rotation);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.YellowGemstones)
        {
            // Keep a snapshot per BC98. A second BC98 may arrive while the wall from the
            // previous snapshot is still resolving; overwriting one shared set loses that
            // wall's real gemstones.
            _currentBatch = new(WorldState.CurrentTime);
            _gemBatches.Add(_currentBatch);
            if (_gemBatches.Count > 8)
                _gemBatches.RemoveAt(0);
            _edgeGems.Clear();
            _zones.Clear();
            Service.Logger.Information($"[CE206] BC98 batch created idx={_gemBatches.Count - 1}");
        }
    }

    public override void OnActorPlayActionTimelineEvent(Actor actor, ushort id)
    {
        if (actor.OID != (uint)OID.YellowGem || id != 0x2489)
            return;

        var pos = actor.Position;
        // Triggernometry rounds half-values away from zero for this positive grid coordinate.
        var col = (int)MathF.Floor((pos.X + 25f - Arena.Center.X) / 10f + 0.5f);
        var row = (int)MathF.Floor((pos.Z + 25f - Arena.Center.Z) / 10f + 0.5f);
        if (col is < 1 or > 4 || row is < 1 or > 4)
            return;

        var dx = pos.X - Arena.Center.X - (col - 2.5f) * 10f;
        var dz = pos.Z - Arena.Center.Z - (row - 2.5f) * 10f;
        if (MathF.Max(MathF.Abs(dx), MathF.Abs(dz)) >= EdgeGemMinOffset)
        {
            // VecToDir(dx, dz, 4) uses atan2(x,z), matching Angle.FromDirection/WPos heading.
            // Using atan2(z,x)+2 (the old code) rotates every edge token and selects the wrong
            // region or no region at all.
            var dir = RubyReflectionData.VecToDir(dx, dz);
            var token = $"{col}{row}{dir}";
            _edgeGems.Add(token);
            _currentBatch?.Tokens.Add(token);
            Service.Logger.Information($"[CE206] gem PAT id={actor.InstanceID:X} pos=({pos.X:f2},{pos.Z:f2}) cell={col}{row} d=({dx:f2},{dz:f2}) dir={dir} token={col}{row}{dir}");
        }
    }

    private void SetWall(int kind, Angle rotation)
    {
        _wallKind = kind;
        _wallRot = rotation;
        // Use the most recent gem batch created within the last 15s that actually collected PAT
        // tokens (gems may be repositioned in several PAT waves before the wall appears; the last
        // wave is the one the wall resolves). Fall back to the shared edge set if no batch has
        // tokens yet (e.g. the wall EAnim arrived before the gem PAT events).
        var now = WorldState.CurrentTime;
        var batchIndex = _gemBatches.Count - 1;
        while (batchIndex >= 0 && (now - _gemBatches[batchIndex].Created).TotalSeconds > DisplayTimeout)
            --batchIndex;
        while (batchIndex >= 0 && _gemBatches[batchIndex].Tokens.Count == 0)
            --batchIndex;
        var tokens = (batchIndex >= 0 ? _gemBatches[batchIndex].Tokens : _edgeGems).ToArray();
        _edgeGems.Clear();
        _edgeGems.UnionWith(tokens);
        var cells = Recalculate();
        Service.Logger.Information($"[CE206] wall set kind={kind} rot={rotation.Rad:f3} tokens=[{string.Join(",", _edgeGems)}] cells=[{string.Join(",", cells)}]");
        if (cells.Length != 0)
        {
            _zones.Add((cells, WorldState.CurrentTime));
            ++NumCasts;
        }
    }

    private int[] Recalculate()
    {
        if (_wallKind < 0)
            return [];

        // Triggernometry RadToDir(h,4) % 2 (h follows the game heading convention: 0 = south,
        // CCW, so east = +pi/2 - identical to BossMod's Angle.Rad).
        var parity = RubyReflectionData.RadToDir(_wallRot.Rad, 4) % 2;
        var tableName = _wallKind switch
        {
            0 => "tian",
            1 => parity == 0 ? "l20p0" : "l20p1",
            3 => parity == 0 ? "l200p0" : "l200p1",
            _ => ""
        };
        return RubyReflectionData.SelectCells(tableName, _edgeGems);
    }
}

static class KnockbackGeometry
{
    // C163 row 90 is SourceRight and row 91 is SourceLeft. The server chooses the row from
    // the target's side of the helper's facing axis, so this must be evaluated at each candidate.
    public static WDir AsideDirection(WPos point, WPos origin, WDir facing)
    {
        var right = facing.OrthoR();
        return (point - origin).Dot(right) >= 0f ? right : -right;
    }
}

sealed class SDAsideKnockbackInAABBSquare(WPos center, WPos origin, WDir facing, float distance, float halfWidth) : ShapeDistance
{
    public override bool Contains(in WPos p)
        => !(p + distance * KnockbackGeometry.AsideDirection(p, origin, facing)).InSquare(center, halfWidth);

    public override float Distance(in WPos p) => Contains(p) ? 0f : 1f;

    public override bool RowIntersectsShape(WPos rowStart, WDir dx, float width, float cushion = default) => true;
}

sealed class SDAsideThenAwayFromOriginInAABBSquare(WPos center, WPos asideOrigin, WDir asideFacing, float asideDistance, WPos circleOrigin, float circleDistance, float halfWidth) : ShapeDistance
{
    public override bool Contains(in WPos p)
    {
        var p1 = p + asideDistance * KnockbackGeometry.AsideDirection(p, asideOrigin, asideFacing);
        if (!p1.InSquare(center, halfWidth))
            return true;

        // The second hit pushes the player 30y away from the BCA0 caster (the second warning's
        // helper position; live-verified: (234.7,362.7) -> ~(224,334), landing inside the 20y
        // square with the 19.5 margin). A landing outside the square is lethal.
        var away = p1 - circleOrigin;
        var p2 = away == default ? p1 : p1 + circleDistance * away.Normalized();
        return !p2.InSquare(center, halfWidth);
    }

    public override float Distance(in WPos p) => Contains(p) ? 0f : 1f;

    public override bool RowIntersectsShape(WPos rowStart, WDir dx, float width, float cushion = default) => true;
}

// BCA0 resolves about six seconds after the telegraph as a 30y knockback away from the BCA0
// caster (the second warning's helper position, live-verified 30y: (234.7,362.7) -> ~(224,334)).
// The landing must stay inside the 20y square (electric fence); the AI avoids starts whose
// landing would exit it.
sealed class CircularKnockback(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeCircle Shape = new(60f);
    internal const float Distance = 30f;
    private const float SafeHalfWidth = 19.5f; // 20y square minus margin
    private const double HitDelay = 6.0d;
    private readonly List<Knockback> _casters = [];
    private readonly List<Knockback> _displayed = [with(4)];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        foreach (var kb in _casters)
            _displayed.Add(kb);
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (_casters.Count == 0)
            return;
        var kb = _casters[0];
        var aside = Module.FindComponent<KnockAside>();
        if (aside?.AsideFor(kb) is { } asideData)
        {
            // First knockback not resolved yet: draw the 15y lateral shove arrow (away from the
            // BCA1 helper axis, side chosen by the player's position) followed by the 30y push
            // arrow away from the BCA0 caster, connected at the first arrow's end.
            var asideDir = KnockbackGeometry.AsideDirection(pc.Position, asideData.AsidePos, asideData.Facing.ToDirection());
            var p1 = pc.Position + KnockAside.Distance * asideDir;
            DrawArrow(pc.Position, p1);
            var away = p1 - kb.Origin;
            var p2 = away == default ? p1 : p1 + kb.Distance * away.Normalized();
            DrawArrow(p1, p2);
        }
        else
        {
            // First knockback already resolved (the aside sources are cleared by C163): seamlessly
            // switch to the 30y second arrow drawn from the player's live position.
            var away = pc.Position - kb.Origin;
            var p2 = away == default ? pc.Position : pc.Position + kb.Distance * away.Normalized();
            DrawArrow(pc.Position, p2);
        }
    }

    private void DrawArrow(WPos from, WPos to)
    {
        var dir = to - from;
        if (dir.LengthSq() < 1e-4f)
            return;
        var nd = dir.Normalized();
        Arena.AddLine(from, to, Colors.Safe, 2f);
        const float headLen = 1.5f;
        var base1 = to - nd * headLen + nd.OrthoR() * 0.8f;
        var base2 = to - nd * headLen - nd.OrthoR() * 0.8f;
        Arena.AddTriangleFilled(base1, to, base2, Colors.Safe);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aside = Module.FindComponent<KnockAside>();
        foreach (var kb in _casters)
        {
            // Before C163, solve both hits from the candidate start: the first displacement can
            // differ by side even for two players in the same packet. After C163, only C162 remains.
            if (aside == null || !aside.AddCombinedAIHint(kb, hints))
                hints.AddForbiddenZone(new SDKnockbackInAABBSquareAwayFromOrigin(Arena.Center, kb.Origin, kb.Distance, SafeHalfWidth), kb.Activation);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        // The same action can arrive with extra high bytes (e.g. 0x4BCA0); only the low 16 bits
        // are the real action id.
        if ((spell.Action.ID & 0xFFFF) == (uint)AID.CircularKnockbackTelegraph)
        {
            _casters.RemoveAll(k => k.ActorID == caster.InstanceID);
            // Origin = the BCA0 caster (helper) position: the 30y push is directed away from it
            // (live-verified; the origin is available ~8.5s before the hit resolves).
            _casters.Add(new(spell.LocXZ, Distance, Module.CastFinishAt(spell).AddSeconds(HitDelay), Shape, spell.Rotation, Kind.AwayFromOrigin, actorID: caster.InstanceID));
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if ((spell.Action.ID & 0xFFFF) == (uint)AID.RavenousGodsCircleHit)
        {
            _casters.Clear();
            ++NumCasts;
        }
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _casters.RemoveAll(k => now > k.Activation.AddSeconds(1d));
    }
}

// BCA1 resolves first as a 15y lateral shove. C163 chooses SourceRight or SourceLeft separately
// for every target from that target's side of the helper's facing axis.
sealed class KnockAside(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeRect Shape = new(40f, 30f);
    internal const float Distance = 15f; // exposed for the connected knockback arrows
    private const float SafeHalfWidth = 19f;
    private const double HitDelay = 5.1d;

    private sealed class AsideSource(WPos asidePos, WPos circlePos, Angle facing, DateTime activation, ulong actorID)
    {
        public readonly WPos AsidePos = asidePos;
        public readonly WPos CirclePos = circlePos;
        public readonly Angle Facing = facing;
        public readonly DateTime Activation = activation;
        public readonly ulong ActorID = actorID;

        public Kind KindFor(WPos point) => (point - AsidePos).Dot(Facing.ToDirection().OrthoR()) >= 0f ? Kind.DirRight : Kind.DirLeft;
    }

    private readonly List<AsideSource> _sources = [];
    private readonly List<(WPos AsidePos, Angle Facing, DateTime Activation, ulong ActorID)> _pendingAside = [];
    private readonly List<Knockback> _displayed = [with(4)];

    public bool AddCombinedAIHint(Knockback circle, AIHints hints)
    {
        foreach (var source in _sources)
            if (source.Activation < circle.Activation && source.CirclePos.AlmostEqual(circle.Origin, 0.5f))
            {
                // Second landing must stay inside the 20y square (19.5 margin).
                hints.AddForbiddenZone(new SDAsideThenAwayFromOriginInAABBSquare(Arena.Center, source.AsidePos,
                    source.Facing.ToDirection(), Distance, circle.Origin, circle.Distance, 19.5f), source.Activation);
                return true;
            }
        return false;
    }

    // The aside paired with the given second knockback (same BCA0 caster position), used to draw
    // the connected knockback arrows.
    public (WPos AsidePos, Angle Facing)? AsideFor(Knockback circle)
    {
        foreach (var source in _sources)
            if (source.Activation < circle.Activation && source.CirclePos.AlmostEqual(circle.Origin, 0.5f))
                return (source.AsidePos, source.Facing);
        return null;
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        foreach (var source in _sources)
            _displayed.Add(new(source.AsidePos, Distance, source.Activation, Shape, source.Facing, source.KindFor(actor.Position), actorID: source.ActorID));
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var source in _sources)
            hints.AddForbiddenZone(new SDAsideKnockbackInAABBSquare(Arena.Center, source.AsidePos, source.Facing.ToDirection(), Distance, SafeHalfWidth), source.Activation);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        switch (spell.Action.ID & 0xFFFF)
        {
            case (uint)AID.KnockAsideTelegraph:
                _pendingAside.RemoveAll(p => p.ActorID == caster.InstanceID);
                _pendingAside.Add((caster.Position, spell.Rotation, Module.CastFinishAt(spell).AddSeconds(HitDelay), caster.InstanceID));
                break;
            case (uint)AID.CircularKnockbackTelegraph:
                // The circle helper arrives a couple of seconds after the aside telegraph; pair it
                // with the pending aside so AI can evaluate both landings from one candidate point.
                for (var i = _pendingAside.Count - 1; i >= 0; --i)
                {
                    var p = _pendingAside[i];
                    _sources.RemoveAll(s => s.ActorID == p.ActorID);
                    _sources.Add(new(p.AsidePos, spell.LocXZ, p.Facing, p.Activation, p.ActorID));
                    _pendingAside.RemoveAt(i);
                }
                break;
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if ((spell.Action.ID & 0xFFFF) == (uint)AID.RavenousGodsAsideHit)
        {
            _sources.Clear();
            _pendingAside.Clear();
            ++NumCasts;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        _sources.RemoveAll(s => s.ActorID == actor.InstanceID);
        _pendingAside.RemoveAll(p => p.ActorID == actor.InstanceID);
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _sources.RemoveAll(s => now > s.Activation.AddSeconds(1d));
        _pendingAside.RemoveAll(p => now > p.Activation.AddSeconds(1d));
    }
}
sealed class GemstoneRaidwides(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.RubyLight, (uint)AID.RavenousGods, (uint)AID.Howl]);
sealed class RubyReflectionHint(BossModule module) : Components.CastHint(module, (uint)AID.RubyLight, "Ruby reflection - watch the gemstone lines");

sealed class RebelliousFamiliarStates : StateMachineBuilder
{
    public RebelliousFamiliarStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ClawTailCombo>()
            .ActivateOnEnter<TopazRay>()
            .ActivateOnEnter<LethalBoundary>()
            .ActivateOnEnter<RubyReflection>()
            .ActivateOnEnter<CircularKnockback>()
            .ActivateOnEnter<KnockAside>()
            .ActivateOnEnter<GemstoneRaidwides>()
            .ActivateOnEnter<RubyReflectionHint>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(RebelliousFamiliarStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 56u,
    SortOrder = 5)]
// The arena is a 20y square: the floor is a 4x4 grid of 10y cells (North-Horn trigger XML R=20).
public sealed class RebelliousFamiliar(WorldState ws, Actor primary) : BossModule(ws, primary, new(238f, 352f), new ArenaBoundsSquare(20f));
