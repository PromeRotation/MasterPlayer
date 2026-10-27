// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// EObj 2015493..2015501 use b4231..b4239: the first model contains 1..9
// dice pips respectively. Read each object's position, since the grid is shuffled.
sealed class CrucibleSphinxNumbers(BossModule module) : Components.GenericAOEs(module)
{
    // Cells are 12y apart; stay inside the 12y square with a 0.5y edge margin.
    private static readonly AOEShapeRect Tile = new(5.5f, 5.5f, 5.5f);
    private uint _spokenRule;
    private DateTime _spokenExpires;
    private WPos[] _safePositions = [];
    private SafeCells? _constraint;

    private (uint rule, DateTime expires) Rule(Actor actor)
    {
        foreach (var status in actor.Statuses)
            if (status.ID is >= 5148 and <= 5151 && status.ExpireAt > WorldState.CurrentTime)
                return (status.ID, status.ExpireAt);
        return _spokenExpires > WorldState.CurrentTime ? (_spokenRule, _spokenExpires) : default;
    }

    private static bool Matches(uint rule, uint number) => rule switch
    {
        5148 => number % 2 == 0,
        5149 => number % 2 == 1,
        5150 => number is 2 or 3 or 5 or 7,
        5151 => number % 3 == 0,
        _ => false
    };

    private IEnumerable<Actor> Tiles() => WorldState.Actors.Where(a => a.OID is >= 2015493 and <= 2015501 && !a.IsDeadOrDestroyed && a.EventState != 7);
    private IEnumerable<Actor> SafeTiles(uint rule) => Tiles().Where(a => Matches(rule, a.OID - 2015492));

    public override void OnEventDirectorUpdate(uint updateID, uint param1, uint param2, uint param3, uint param4)
    {
        if (updateID != 0x80000027 || param3 != 14665 || param4 != Module.PrimaryActor.InstanceID)
            return;
        // Battle talk 67 entries 28..31 (InstanceContentTextData 45128..45131).
        if (param1 is >= 28 and <= 31)
        {
            _spokenRule = param1 switch { 28 => 5148u, 29 => 5149u, 30 => 5151u, _ => 5150u };
            _spokenExpires = WorldState.FutureTime(12);
        }
        else if (param1 is 1 or 2 or 7 or 8 or 9)
        {
            _spokenRule = 0;
            _spokenExpires = default;
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var (rule, expires) = Rule(actor);
        return rule == 0 || Module.PrimaryActor.IsDeadOrDestroyed ? [] : SafeTiles(rule)
            .Select(a => new AOEInstance(Tile, a.Position, activation: expires, color: Colors.SafeFromAOE, risky: false)).ToArray();
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var (rule, expires) = Rule(actor);
        if (rule == 0 || Module.PrimaryActor.IsDeadOrDestroyed)
            return;
        var tiles = SafeTiles(rule).Select(a => a.Position).ToArray();
        if (tiles.Length == 0)
            return;
        // Outside every correct tile is forbidden at the deadline, while all
        // correct tiles remain available for avoiding other casts and attacking.
        if (_constraint == null || !_safePositions.SequenceEqual(tiles))
        {
            _safePositions = tiles;
            _constraint = new(tiles, Module.Center);
        }
        hints.AddForbiddenConstraint(_constraint, expires.AddSeconds(-1));
    }

    private sealed class SafeCells(WPos[] positions, WPos center) : ShapeDistance
    {
        private FrozenConstraintGeometry? _geometry;
        public override float Distance(in WPos p)
        {
            var clearance = float.MinValue;
            foreach (var cell in positions)
                clearance = Math.Max(clearance, 5.5f - Math.Max(Math.Abs(p.X - cell.X), Math.Abs(p.Z - cell.Z)));
            return clearance;
        }
        public override MasterPlayer.Spatial.Geometry.IGeometryShape Geometry(float y)
        {
            if (_geometry != null && _geometry.Position.Y == y) return _geometry;
            return _geometry = FrozenConstraintGeometry.Create(p => Distance(p), center, y, radius: 21);
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        var (rule, _) = Rule(actor);
        if (rule == 0 || Module.PrimaryActor.IsDeadOrDestroyed)
            return;
        var name = rule switch { 5148 => "偶数（2、4、6、8）", 5149 => "奇数（1、3、5、7、9）", 5150 => "质数（2、3、5、7）", _ => "3的倍数（3、6、9）" };
        hints.Add($"数字谜题：进入{name}的绿色安全格！", !SafeTiles(rule).Any(t => Tile.Check(actor.Position, t.Position, default)));
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        var (rule, _) = Rule(pc);
        if (rule == 0 || Module.PrimaryActor.IsDeadOrDestroyed)
            return;
        foreach (var tile in Tiles())
        {
            var number = tile.OID - 2015492;
            Arena.TextWorld(tile.Position, number.ToString(), Matches(rule, number) ? Colors.Safe : Colors.Object);
        }
    }
}
