// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// 5555 freezes the facing reference. 49485/86 turns along or against that
// reference; 5341 later expels a blade in the corresponding player-relative direction.
sealed class CrucibleLaudaShockwave(BossModule module) : Components.GenericKnockback(module, 49489)
{
    private sealed class Mark(Angle facing, DateTime boundUntil)
    {
        public Angle Facing = facing;
        public DateTime BoundUntil = boundUntil, Activation;
        public bool? Forward;
        public int Side;
        public WPos? Staging, WalkingLaunch;
        public WPos? ObservedWalkingPosition, RequestedWalkingToward;
        public DateTime ObservedWalkingAt;
        public bool ObservedWalking;
        public DateTime StagingUntil;
        public int StagingSide;
        public Components.GenericAOEs.AOEInstance[] ObservedBeforeJump=[];
    }
    private readonly Dictionary<ulong, Mark> _marks = [];
    private readonly Knockback[] _source = new Knockback[1];
    // run2313 / run2317 第二遍：5341 上身后标记的 Forward 仍为空（提示一直停在「记录Boss出现的前后方向」），
    // 人没去长轴，被 40y 击退进场外火地板。把止步时的朝向和 49485/86 的转向单独记下来，
    // 标记缺方向时（标记被重建，或动作先于标记到达）从这里补。
    private readonly Dictionary<ulong, (Angle Facing, DateTime At)> _boundFacing = [];
    private (Angle Rotation, DateTime At)? _turn;

    private void FillForward(ulong id, Mark mark)
    {
        if (mark.Forward != null || _turn is not { } turn || (WorldState.CurrentTime - turn.At).TotalSeconds > 20)
            return;
        var facing = _boundFacing.TryGetValue(id, out var bound) && (WorldState.CurrentTime - bound.At).TotalSeconds <= 25 ? bound.Facing : mark.Facing;
        mark.Forward = turn.Rotation.ToDirection().Dot(facing.ToDirection()) > 0;
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        base.OnStatusGain(actor, ref status);
        if (status.ID == 5555)
        {
            if (!_boundFacing.TryGetValue(actor.InstanceID, out var known) || (WorldState.CurrentTime - known.At).TotalSeconds > 10)
            {
                _boundFacing[actor.InstanceID] = (actor.Rotation, WorldState.CurrentTime);
                _turn = null; // 新一轮止步，上一轮的转向作废
            }
            if (!_marks.TryGetValue(actor.InstanceID, out var mark))
                _marks[actor.InstanceID] = new(actor.Rotation, status.ExpireAt);
            else
            {
                // Native scans and queued status packets can arrive in either order.
                if (mark.BoundUntil == default && mark.Forward == null) mark.Facing = actor.Rotation;
                mark.BoundUntil = status.ExpireAt;
            }
        }
        else if (status.ID == 5341)
        {
            if (!_marks.TryGetValue(actor.InstanceID, out var mark))
                _marks[actor.InstanceID] = mark = new(actor.Rotation, default);
            mark.Activation = status.ExpireAt;
        }
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        base.OnStatusLose(actor, ref status);
        if (_marks.TryGetValue(actor.InstanceID, out var mark))
        {
            if (status.ID == 5555)
                mark.BoundUntil = default;
            else if (status.ID == 5341)
                mark.Activation = WorldState.FutureTime(0.25); // Removal packet precedes 49489 by ~80ms.
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 49485 or 49486)
        {
            _turn = (spell.Rotation, WorldState.CurrentTime);
            foreach (var (id, mark) in _marks)
                if (WorldState.Actors.Find(id) is { } target && target.Position.InCircle(caster.Position, 1))
                    mark.Forward = spell.Rotation.ToDirection().Dot(mark.Facing.ToDirection()) > 0;
            foreach (var (id, mark) in _marks)
                FillForward(id, mark);
        }
        else if (spell.Action.ID == WatchedAction)
        {
            foreach (var target in spell.Targets)
                _marks.Remove(target.ID);
            ++NumCasts;
        }
    }

    public override void Update()
    {
        foreach (var (id, mark) in _marks.ToArray())
            if (WorldState.Actors.Find(id) is not { IsDeadOrDestroyed: false }
                || (mark.Activation != default ? mark.Activation < WorldState.CurrentTime.AddSeconds(-1) : mark.BoundUntil < WorldState.CurrentTime.AddSeconds(-20)))
                _marks.Remove(id);
        foreach (var (id, mark) in _marks)
            FillForward(id, mark);
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        if (!_marks.TryGetValue(actor.InstanceID, out var mark) || mark.Activation == default || mark.Activation == DateTime.MaxValue || mark.Forward is not { } forward)
            return [];
        _source[0] = new(actor.Position, 40, mark.Activation, direction: actor.Rotation + (forward ? default : 180f.Degrees()), kind: Kind.DirForward, ignoreImmunes: true);
        return _source;
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !CrucibleLaudaFloor.Contains(pos);

    protected override bool LandingUnsafe(int slot, Actor actor, WPos landing, DateTime activation)
        => base.LandingUnsafe(slot, actor, landing, activation)
            || Module.FindComponent<XBMB45AOE>() is { } aoes && aoes.ActiveAOEs(slot, actor).ToArray()
                .Any(a => ResolvesAfterJump(actor, a.Activation) && a.Check(landing));

    // Navigation cannot represent a future teleport across an AOE. Keep the
    // later pattern on the overlay and assess it at the landing instead.
    public bool ResolvesAfterJump(Actor actor, DateTime activation) => _marks.TryGetValue(actor.InstanceID, out var mark)
        && mark.Activation != default && mark.Activation != DateTime.MaxValue && mark.Forward != null
        && activation > mark.Activation.AddSeconds(0.5) && activation <= mark.Activation.AddSeconds(4);


    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_marks.TryGetValue(actor.InstanceID, out var mark))
        {
            hints.Add(mark.Forward switch
            {
                true => "止步飞刀：沿长轴站位，面向击退落点！",
                false => "止步飞刀：沿长轴站位，背对击退落点！",
                _ => "止步飞刀：记录Boss出现的前后方向，准备长轴击退！"
            }, false);
            base.AddHints(slot, actor, hints);
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!_marks.TryGetValue(actor.InstanceID, out var mark))
            return;
        if(mark.ObservedWalkingAt!=WorldState.CurrentTime)
        {
            var elapsed=(WorldState.CurrentTime-mark.ObservedWalkingAt).TotalSeconds;
            var delta=actor.Position-(mark.ObservedWalkingPosition??actor.Position);
            mark.ObservedWalking=mark.RequestedWalkingToward is {} toward && mark.ObservedWalkingPosition is {} previous
                && elapsed>0 && elapsed<=.3 && delta.LengthSq()>.01f && delta.Length()<=6*elapsed+.02
                && delta.Dot((toward-previous).Normalized())>delta.Length()*.5f;
            mark.ObservedWalkingPosition=actor.Position;mark.ObservedWalkingAt=WorldState.CurrentTime;
            mark.RequestedWalkingToward=null;
        }
        var inputDelay=mark.ObservedWalking?0:.1;
        if (mark.BoundUntil > WorldState.CurrentTime)
        {
            hints.ForcedMarchImminent = true;
            return;
        }
        if (mark.Activation == default || mark.Activation == DateTime.MaxValue || mark.Forward is not { } forward)
            return;

        // Starting 18y from center leaves the 40y landing on the opposite tip.
        // Consider overlapping AOEs at the landing, including the following cleave.
        float Score(int side)
        {
            var start = Module.Center + new WDir(0, side * 18);
            var end = Module.Center - new WDir(0, side * 22);
            float penalty = 0;
            foreach (var zone in hints.ForbiddenZones)
                if (zone.activation >= mark.Activation.AddSeconds(-0.5) && zone.activation <= mark.Activation.AddSeconds(4)
                    && zone.shapeDistance.Distance(end) <= 0)
                    penalty += 10000;
            if (Module.FindComponent<XBMB45AOE>() is { } aoes)
                foreach (var aoe in aoes.ActiveAOEs(slot, actor))
                    if (ResolvesAfterJump(actor, aoe.Activation) && aoe.Shape.Check(end, aoe.Origin, aoe.Rotation))
                        penalty += 10000;
            return penalty + (actor.Position - start).LengthSq() + (mark.Side == side ? -100 : 0);
        }
        mark.Side = Score(1) < Score(-1) ? 1 : -1;
        var goal = Module.Center + new WDir(0, mark.Side * 18);
        var landing = Module.Center - new WDir(0, mark.Side * 22);
        hints.GoalPoints.Add((goal, 0.2f, 100));
        // The pre-jump R8 blasts can cover the eventual launch pocket. Stage
        // beside their observed union, then enter only after their full window.
        var visibleBeforeJump=Module.FindComponent<XBMB45AOE>()?.ActiveAOEs(slot,actor).ToArray()
            .Where(a=>a.Activation.AddSeconds(.7)>WorldState.CurrentTime && a.Activation<mark.Activation.AddSeconds(-.5)).ToArray() ?? [];
        var coveringGoal=visibleBeforeJump.Where(a=>(a.ShapeDistance??a.Shape.Distance(a.Origin,a.Rotation)).Distance(goal)<1).ToArray();
        mark.ObservedBeforeJump=mark.ObservedBeforeJump.Concat(visibleBeforeJump)
            .Where(a=>a.Activation.AddSeconds(.7)>WorldState.CurrentTime)
            .GroupBy(a=>(a.ActorID,a.Activation,a.Origin,a.Rotation)).Select(g=>g.First()).ToArray();
        if(mark.StagingSide!=mark.Side){mark.Staging=null;mark.StagingUntil=default;mark.StagingSide=mark.Side;mark.WalkingLaunch=null;}
        List<WPos>? stagingRoute=null;
        if(coveringGoal.Length>0 && Module.FindComponent<CrucibleLaudaFireKnife>() is {} stageWalking)
        {
            var clearAt=coveringGoal.Max(a=>a.Activation).AddSeconds(.7);
            WPos? staging=null;
            var candidates=new List<(WPos Point,float Cost)>();
            for(var x=504f;x<=536;x+=.5f)
                for(var z=-446f;z<=-394;z+=.5f)
                {
                    var p=new WPos(x,z);var toGoal=(p-goal).Length();
                    if(!CrucibleLaudaFloor.Contains(p)||!CrucibleLaudaFloor.Contains(p+new WDir(.5f,0))||!CrucibleLaudaFloor.Contains(p-new WDir(.5f,0))
                        ||!CrucibleLaudaFloor.Contains(p+new WDir(0,.5f))||!CrucibleLaudaFloor.Contains(p-new WDir(0,.5f))
                        ||clearAt.AddSeconds(.1+Math.Max(0,toGoal-1.5f)/6)>mark.Activation.AddSeconds(-.5)
                        ||visibleBeforeJump.Any(a=>(a.ShapeDistance??a.Shape.Distance(a.Origin,a.Rotation)).Distance(p)<1))continue;
                    candidates.Add((p,(p-actor.Position).Length()+toGoal));
                }
            foreach(var candidate in candidates.OrderBy(c=>c.Cost))
            {
                // No floor-safe route can be shorter than the direct distance.
                // Reject impossible deadlines before the full timing search.
                if(WorldState.FutureTime(.1+inputDelay+(candidate.Point-actor.Position).Length()/6)>=clearAt)continue;
                var route=(candidate.Point-actor.Position).Length()<.1f ? new List<WPos>()
                    : stageWalking.TryValidatedEscape(actor.Position,candidate.Point,mark.ObservedBeforeJump,hints,inputDelay);
                if(route==null)continue;
                var length=0f;var from=actor.Position;
                foreach(var point in route){length+=(point-from).Length();from=point;}
                if(WorldState.FutureTime(.1+inputDelay+length/6)>=clearAt)continue;
                staging=candidate.Point;stagingRoute=route;break;
            }
            if(staging is {} next)
            {
                mark.Staging=next;mark.StagingUntil=clearAt;
                // The last safe pocket approach may have been selected before
                // these blasts forced a different departure point. Select it
                // again from the actual staging position when the blasts clear.
                mark.WalkingLaunch=null;
            }
        }
        if(mark.Staging is {} wait && WorldState.CurrentTime<mark.StagingUntil)
        {
            hints.GoalPoints.Add((wait,.25f,180));
            if(stagingRoute is {Count:>0})
            {
                hints.DirectRoute=stagingRoute;
                hints.DirectRouteDeadline=mark.StagingUntil;
                mark.RequestedWalkingToward=stagingRoute[0];
            }
        }
        // This standing pocket must survive grid dilation. Do not add damage
        // inflation or advance the deadline: the preceding R8 circles still
        // cover it. PR adds its own preparation time before the actual impact.
        hints.ForbiddenZones.Add(new(new SDInvertedCircle(goal, 2), mark.Activation, null, ExactGeometry: true));
        if ((mark.Staging==null || WorldState.CurrentTime>=mark.StagingUntil)
            && Module.FindComponent<CrucibleLaudaFireKnife>() is {} walking)
        {
            // Preserve full observed blast tails while validating an ordinary walk.
            // The launch has a deadline; an expensive worker result must not be
            // the only way to leave a now-safe staging point in time.
            // The original R2 pocket accepts points around its center. Stop at
            // a nearer interior point and aim the 40y landing from the actual
            // position, instead of spending the last input window on its center.
            mark.WalkingLaunch ??= goal+(actor.Position-goal).Normalized()*Math.Min(1.25f,(actor.Position-goal).Length());
            var route=walking.TryValidatedEscape(actor.Position,mark.WalkingLaunch.Value,mark.ObservedBeforeJump,hints,inputDelay);
            if(route!=null)
            {
                var length=0f;var from=actor.Position;
                foreach(var point in route){length+=(point-from).Length();from=point;}
                if(WorldState.FutureTime(.1+inputDelay+length/6)<mark.Activation.AddSeconds(-.5))
                {
                    hints.DirectRoute=route;
                    hints.DirectRouteDeadline=mark.Activation.AddSeconds(-.5);
                    mark.RequestedWalkingToward=route[0];
                }
            }
        }
        hints.MaxCastTime = 0;
        var direction = (landing - actor.Position).Normalized();
        var actualLanding = actor.Position + direction * 40;
        if (actor.Position.InCircle(goal, 2.0f) && !LandingUnsafe(slot, actor, actualLanding, mark.Activation))
        {
            // Aim from the actual position. A 12-degree facing error moves a
            // 40y landing almost 9y sideways and misses the narrow end platform.
            hints.DesiredFacing = Angle.FromDirection(forward ? direction : -direction);
            hints.DesiredFacingExpire = mark.Activation.AddSeconds(0.5);
            hints.ForcedMarchImminent = true;
            hints.ForceCancelCast = true;
        }
    }
}

// Icon669 snapshots a knife (49468), followed by the proximity cast 49469.
// Its R80 sheet radius describes damage falloff, not a forbidden arena-sized circle.
sealed class CrucibleLaudaFireKnife(BossModule module) : BossComponent(module)
{
    private ulong _target;
    private ulong _explosionCaster;
    private WPos? _drop, _dropDestination, _explosion, _escapeFallbackGoal, _lastEscapeObserved, _circleEscapeGoal, _circleStaging;
    private DateTime _lastEscapeObservedAt;
    private DateTime _dropUntil, _explodeAt, _circleAt;
    // Native Icon669 circle starts ~5.4s later, with its position already snapshotted.
    // Arrive before that observed window while also preparing the later sword bait.
    public WPos? LockedPlacement => _explosion;

    // Legacy minimum retreat distance, not a zero-damage boundary: run2364
    // still took proximity damage at 46y. Continue toward the farthest tip.
    public const float BlastRadius = 38;
    // 每帧最多做几次完整逃生校验（TryValidatedEscape），防止主线程卡顿。
    private const int MaxExitChecks = 24;
    private (WPos Center, float Range, bool Result)? _floorBeyond;
    // Standing floor (0.5y grid, same 1y inset as CrucibleLaudaFloor.Nearest) at least `range` from `center`.
    private bool FloorBeyond(WPos center, float range)
    {
        if (_floorBeyond is { } cached && cached.Center.AlmostEqual(center, .01f) && cached.Range == range) return cached.Result;
        var result = false;
        for (var x = 504f; x <= 536 && !result; x += .5f)
            for (var z = -446f; z <= -394; z += .5f)
            {
                var p = new WPos(x, z);
                if ((p - center).LengthSq() >= range * range && CrucibleLaudaFloor.Contains(p) && CrucibleLaudaFloor.Contains(p + new WDir(.5f, 0))
                    && CrucibleLaudaFloor.Contains(p - new WDir(.5f, 0)) && CrucibleLaudaFloor.Contains(p + new WDir(0, .5f))
                    && CrucibleLaudaFloor.Contains(p - new WDir(0, .5f)))
                { result = true; break; }
            }
        _floorBeyond = (center, range, result);
        return result;
    }
    public bool CircleEscapeActive => _explosion!=null && _circleAt!=default && _circleAt.AddSeconds(.5)>WorldState.CurrentTime;
    public DateTime PendingPlacementDeadline => _drop != null ? _dropUntil.AddSeconds(-3.5) : default;

    // EObj 2015453 marks each 49460 R12 prison centre ~4.5s before Icon669.
    private const uint PrisonMarker = 2015453;
    private const float PrisonRadius = 12, PrisonClearance = 1.5f;
    private static readonly WPos[] BlastRetreatTips = [new(518.5f, -396), new(521.5f, -396), new(518.5f, -444), new(521.5f, -444)];
    private string _prisonEscapeSignature = "";
    private HashSet<(int X,int Z)> _roomyPrisonCells = [];
    private readonly Dictionary<(int X,int Z),WPos> _prisonExitCells = [];

    // Candidate planning constraint only: preserve the entire R12 geometry. A
    // tiny safe pocket can place a knife yet have no 7m escape before its hit.
    private void UpdatePrisonEscapeCells(List<WPos> prisons)
    {
        var signature=string.Join(";",prisons.OrderBy(p=>p.X).ThenBy(p=>p.Z).Select(p=>$"{p.X:F3},{p.Z:F3}"));
        if(signature==_prisonEscapeSignature)return;
        _prisonEscapeSignature=signature;_roomyPrisonCells.Clear();_prisonExitCells.Clear();
        var remaining=new HashSet<(int X,int Z)>();
        for(var x=1008;x<=1072;x++)for(var z=-892;z<=-788;z++)
        {
            var p=new WPos(x*.5f,z*.5f);
            if(CrucibleLaudaFloor.Contains(p)&&prisons.All(c=>(p-c).Length()>=PrisonRadius+.55f))remaining.Add((x,z));
        }
        while(remaining.Count>0)
        {
            var first=remaining.First();remaining.Remove(first);
            var component=new List<(int X,int Z)> {first};var queue=new Queue<(int X,int Z)>();queue.Enqueue(first);
            while(queue.TryDequeue(out var p))
                foreach(var next in new[]{(p.X+1,p.Z),(p.X-1,p.Z),(p.X,p.Z+1),(p.X,p.Z-1)})
                    if(remaining.Remove(next)){component.Add(next);queue.Enqueue(next);}
            // A 14m diameter guarantees a 7m-away point from every placement in this
            // connected component, matching the knife escape distance. The old 16m
            // threshold dropped the only near component on the mirrored 06:37 layout
            // and forced a 27.5y drop that cannot be reached inside the 5.4s window.
            // run2384 23:51:31：大连通块逐对比较是 O(n²)，Icon669 那一帧卡了 2570ms（MP-PERF CrucibleLaudaFireKnife），
            // 人物站着吃了两道支配魔刃。外接框任一边 ≥28 格时必有两点相距 ≥28，直接判定；否则块不超过 28×28，逐对比较也很快。结果不变。
            var roomy=component.Max(c=>c.X)-component.Min(c=>c.X)>=28||component.Max(c=>c.Z)-component.Min(c=>c.Z)>=28;
            for(var i=0;i<component.Count&&!roomy;i++)for(var j=i+1;j<component.Count;j++)
                if((component[i].X-component[j].X)*(component[i].X-component[j].X)
                    +(component[i].Z-component[j].Z)*(component[i].Z-component[j].Z)>=28*28){roomy=true;break;}
            if(roomy)_roomyPrisonCells.UnionWith(component);
            var exits=component.Select(c=>new WPos(c.X*.5f,c.Z*.5f))
                .Where(p=>CrucibleLaudaFloor.Contains(p+new WDir(.5f,0))&&CrucibleLaudaFloor.Contains(p-new WDir(.5f,0))
                    &&CrucibleLaudaFloor.Contains(p+new WDir(0,.5f))&&CrucibleLaudaFloor.Contains(p-new WDir(0,.5f))).ToArray();
            if(exits.Length>0)
            {
                var exit=exits.OrderBy(p=>(p-Module.Center).LengthSq()).First();
                foreach(var cell in component)_prisonExitCells[cell]=exit;
            }
        }
    }

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != 669)
            return;
        _target = actor.InstanceID;
        _drop = ChooseDrop(actor.Position);
        _dropDestination = RefinePlacement(_drop.Value);
        _dropUntil = WorldState.FutureTime(8);
    }

    private WPos RefinePlacement(WPos drop)
    {
        var cell=((int)MathF.Round(drop.X*2),(int)MathF.Round(drop.Z*2));
        if(_roomyPrisonCells.Contains(cell) || !_prisonExitCells.TryGetValue(cell,out var exit))return drop;
        var prisons=WorldState.Actors.Where(a=>a.OID==PrisonMarker&&!a.IsDeadOrDestroyed).Select(a=>a.Position).ToArray();
        var best=drop;var separation=(drop-exit).LengthSq();
        for(var dx=-10;dx<=10;dx++)for(var dz=-10;dz<=10;dz++)
        {
            var p=drop+new WDir(dx*.05f,dz*.05f);
            var next=(p-exit).LengthSq();
            // The precise .2y destination remains wholly within the original
            // 1y placement goal, whose floor and prison checks stay unchanged.
            if(next<=separation || (p-drop).Length()>.8f || !CrucibleLaudaFloor.Contains(p)
                ||!CrucibleLaudaFloor.Contains(p+new WDir(.5f,0))||!CrucibleLaudaFloor.Contains(p-new WDir(.5f,0))
                ||!CrucibleLaudaFloor.Contains(p+new WDir(0,.5f))||!CrucibleLaudaFloor.Contains(p-new WDir(0,.5f))
                ||prisons.Any(c=>(p-c).Length()<PrisonRadius+PrisonClearance))continue;
            best=p;separation=next;
        }
        return best;
    }

    // 2026-10-29 06:37 ARR: prisons at (522.5,-410) (507.5,-412.5) (532.5,-427.5)
    // (517.5,-430) covered the fixed west tip (507,-422.5) by 2m; the knife holder
    // stood there with no reachable exit and took 49460 then the knife. Place the
    // knife on the floor point nearest the holder that clears every prison.
    private WPos ChooseDrop(WPos from)
    {
        var prisons = WorldState.Actors.Where(a => a.OID == PrisonMarker && !a.IsDeadOrDestroyed).Select(a => a.Position).ToList();
        if (prisons.Count == 0)
        {
            WPos west = new(507, -422.5f), east = new(533, -417.5f);
            return (from - west).LengthSq() < (from - east).LengthSq() ? west : east;
        }
        UpdatePrisonEscapeCells(prisons);
        WPos? best = null, clear = null, widest = null, blastClear = null, falloffDrop = null, pocketDrop = null;
        float bestDistance = float.MaxValue, clearDistance = float.MaxValue, widestClearance = float.MinValue, blastDistance = float.MaxValue;
        float falloffRange = 0, falloffDistance = float.MaxValue;
        float pocketExitDistance = 0, pocketDistance = float.MaxValue;
        for (var x = 504f; x <= 536f; x += .5f)
            for (var z = -446f; z <= -394f; z += .5f)
            {
                var p = new WPos(x, z);
                // Keep the whole 1y goal circle on the floor.
                if (!CrucibleLaudaFloor.Contains(p) || !CrucibleLaudaFloor.Contains(p + new WDir(1, 0)) || !CrucibleLaudaFloor.Contains(p - new WDir(1, 0))
                    || !CrucibleLaudaFloor.Contains(p + new WDir(0, 1)) || !CrucibleLaudaFloor.Contains(p - new WDir(0, 1)))
                    continue;
                var clearance = prisons.Min(c => (p - c).Length()) - PrisonRadius;
                if (clearance > widestClearance) { widestClearance = clearance; widest = p; }
                var distance = (p - from).Length();
                if (clearance < PrisonClearance) continue;
                // A small pocket cannot clear the knife until the prisons resolve.
                // Leave its inward exit free, so the holder can first walk across
                // the pocket, then continue out on the observed prison effect.
                if(distance<=5.4f*4.9f && _prisonExitCells.TryGetValue(((int)MathF.Round(x*2),(int)MathF.Round(z*2)),out var exit))
                {
                    var separation=(p-exit).LengthSq();
                    if(separation>pocketExitDistance+.01f || Math.Abs(separation-pocketExitDistance)<=.01f && distance<pocketDistance)
                    { pocketExitDistance=separation;pocketDistance=distance;pocketDrop=p; }
                }
                // run2359: the nearest prison-safe drop left only a 39.1y far
                // corner. The full damage/floor margins erased that pocket in
                // the production solver. Place a little farther along the same
                // clear floor, leaving a broad retreat without reducing margins.
                // Retain the original fallback when this cannot be reached in
                // the observed 5.4s placement window at ordinary walking speed.
                if (distance <= 5.4f * 4.9f && _roomyPrisonCells.Contains(((int)MathF.Round(x * 2), (int)MathF.Round(z * 2))))
                {
                    var range = BlastRetreatTips.Max(tip => (tip - p).Length());
                    if (distance < blastDistance && range >= BlastRadius + 3)
                    { blastDistance = distance; blastClear = p; }
                    // run2362: both axial tips are occupied by prisons. No
                    // placement can clear R38; retain all prison/floor margins
                    // and maximize falloff distance instead of the nearest drop.
                    if (range > falloffRange + .01f || Math.Abs(range - falloffRange) <= .01f && distance < falloffDistance)
                    { falloffRange = range; falloffDistance = distance; falloffDrop = p; }
                }
                // Roomy first; a plain clear cell is the fallback when the only
                // roomy component is across the arena and cannot be reached before
                // the 5.4s placement deadline (mirrored 06:37 layout).
                if (distance < clearDistance) { clearDistance = distance; clear = p; }
                if (_roomyPrisonCells.Contains(((int)MathF.Round(x*2),(int)MathF.Round(z*2))) && distance < bestDistance) { bestDistance = distance; best = p; }
            }
        return blastClear ?? falloffDrop ?? best ?? pocketDrop ?? clear ?? widest ?? from;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        if (spell.Action.ID == 49468)
        {
            _explosionCaster = caster.InstanceID;
            _drop = null;
            _explosion = spell.LocXZ;
            _explodeAt = Module.CastFinishAt(spell, 12);
            _circleAt = Module.CastFinishAt(spell);
            _escapeFallbackGoal = null;
            _circleEscapeGoal = _circleStaging = null;
        }
        else if (spell.Action.ID == 49469)
        {
            _explosionCaster = caster.InstanceID;
            _drop = null;
            _explosion = spell.LocXZ;
            _explodeAt = Module.CastFinishAt(spell);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 49468)
        {
            _explosionCaster = 0;
        } // The landed knife outlives the placement helper.
        if (spell.Action.ID == 49469)
            _explosion = null;
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 49468 or 49469 && caster.InstanceID == _explosionCaster && spell.Interrupted)
            _explosion = null;
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == _explosionCaster) _explosion = null;
        if (actor.InstanceID == _target) _drop = null;
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void Update()
    {
        if (_dropUntil < WorldState.CurrentTime)
            _drop = null;
        if (_explodeAt.AddSeconds(1) < WorldState.CurrentTime)
            _explosion = null;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Visible prison markers are current information. Stage before Icon669
        // into a pocket with an exit, while all active AOEs remain in the solver.
        if(_drop==null&&_explosion==null&&Module.FindComponent<CrucibleLaudaBaits>()?.Preparing!=true
            &&WorldState.Actors.Any(a=>a.OID==PrisonMarker&&!a.IsDeadOrDestroyed))
        {
            var staging=RefinePlacement(ChooseDrop(actor.Position));
            hints.GoalZones.Add(p=>MathF.Max(0,60-(p-staging).Length()*2));
            hints.GoalPoints.Add((staging,.2f,130));
        }
        if (_drop is { } drop && _target == actor.InstanceID && Module.FindComponent<CrucibleLaudaBaits>()?.Preparing != true)
        {
            hints.GoalZones.Add(p => p.InCircle(drop, 1) ? 50 : 0);
            var destination=_dropDestination??drop;
            hints.GoalPoints.Add((destination,.2f,140));
            hints.MaxCastTime = 0;
            var visible=Module.FindComponent<XBMB45AOE>()?.ActiveAOEs(slot,actor).ToArray() ?? [];
            var route=TryValidatedEscape(actor.Position,destination,visible,hints,.1);
            if(route!=null)
            {
                hints.DirectRoute=route;
                hints.DirectRouteDeadline=PendingPlacementDeadline;
            }
        }
        if (_explosion is { } explosion)
        {
            hints.GoalZones.Add(p => Math.Min(10, (p - explosion).Length() / 4));
            // Retain the existing minimum-distance navigation constraint only
            // when standing floor exists beyond it. It does not imply zero damage.
            if (_explodeAt > WorldState.CurrentTime && FloorBeyond(explosion, BlastRadius + 1))
                hints.AddForbiddenZone(new AOEShapeCircle(BlastRadius), explosion, default, _explodeAt);
            // Prefer the interior of the far pocket, rather than stopping on
            // the nearest edge of the circle with native arrival tolerance.
            // When every standing point is inside R38, distance still reduces
            // proximity damage. Keep that a goal, never a false full-floor danger.
            var retreat = BlastRetreatTips.OrderByDescending(p => (p - explosion).LengthSq()).First();
            if (_explodeAt > WorldState.CurrentTime
                && ((retreat - explosion).Length() >= BlastRadius + 2 || !FloorBeyond(explosion, BlastRadius + 1)))
            {
                hints.GoalPoints.Add((retreat, .5f, 140));
                hints.MaxCastTime = 0;
            }
            if (_circleAt != default && _circleAt.AddSeconds(.5) > WorldState.CurrentTime && Module.FindComponent<CrucibleLaudaBaits>()?.Preparing != true)
            {
                var visible=Module.FindComponent<XBMB45AOE>()?.ActiveAOEs(slot,actor).ToArray() ?? [];
                WPos? best=null;var nearest=float.MaxValue;
                for(var x=504f;x<=536;x+=.5f)
                    for(var z=-446f;z<=-394;z+=.5f)
                    {
                        var p=new WPos(x,z);var distance=(p-actor.Position).LengthSq();
                        if(distance>=nearest || (p-explosion).Length()<7 || !CrucibleLaudaFloor.Contains(p)
                            || !CrucibleLaudaFloor.Contains(p+new WDir(.5f,0)) || !CrucibleLaudaFloor.Contains(p-new WDir(.5f,0))
                            || !CrucibleLaudaFloor.Contains(p+new WDir(0,.5f)) || !CrucibleLaudaFloor.Contains(p-new WDir(0,.5f)))continue;
                        if(visible.Any(a=>a.Activation.AddSeconds(.7)>=_circleAt.AddSeconds(-.5) && a.Activation<=_circleAt.AddSeconds(.5) &&
                            (a.ShapeDistance ?? a.Shape.Distance(a.Origin,a.Rotation)).Distance(p)<1))continue;
                        best=p;nearest=distance;
                    }
                _circleEscapeGoal ??= best;
                if(_circleEscapeGoal is {} goal)
                {
                    hints.GoalPoints.Add((goal,.5f,150));
                    var observedMoving=_lastEscapeObserved is {} observed && _lastEscapeObservedAt < WorldState.CurrentTime
                        && _lastEscapeObservedAt.AddSeconds(.3) > WorldState.CurrentTime && (actor.Position-observed).LengthSq()>.01f;
                    if(_lastEscapeObservedAt < WorldState.CurrentTime){_lastEscapeObserved=actor.Position;_lastEscapeObservedAt=WorldState.CurrentTime;}
                    var inputDelay=observedMoving?0:.1;
                    var route=TryValidatedEscape(actor.Position,goal,visible,hints,inputDelay);
                    if(route!=null)_escapeFallbackGoal=goal;
                    else if(_escapeFallbackGoal is {} prior)
                        route=TryValidatedEscape(actor.Position,prior,visible,hints,inputDelay);
                    if(route!=null)
                    {
                        hints.DirectRoute=route;
                        hints.DirectRouteDeadline=_circleAt.AddSeconds(.5);
                    }
                    else
                    {
                        // An earlier prison may temporarily block the final exit.
                        // Walk toward it inside the current pocket while the knife
                        // is still pending; every offered segment retains the same
                        // full geometry, preparation, tail and native validation.
                        var exits=new List<(WPos Position,float Distance)>();
                        var clearance=MathF.Max(.5f,WorldState.NavigationMargin)+.05f;
                        var budget=(float)Math.Max(0,(_circleAt-WorldState.CurrentTime).TotalSeconds+.5-inputDelay)*WorldState.WalkingSpeed;
                        for(var x=504f;x<=536;x+=.5f)
                            for(var z=-446f;z<=-394;z+=.5f)
                            {
                                var p=new WPos(x,z);var next=(p-actor.Position).LengthSq();
                                if(next>budget*budget || (p-explosion).Length()<6+clearance+.1f || !CrucibleLaudaFloor.Contains(p)
                                    ||!CrucibleLaudaFloor.Contains(p+new WDir(.5f,0))||!CrucibleLaudaFloor.Contains(p-new WDir(.5f,0))
                                    ||!CrucibleLaudaFloor.Contains(p+new WDir(0,.5f))||!CrucibleLaudaFloor.Contains(p-new WDir(0,.5f)))continue;
                                if(visible.Any(a=>!a.Origin.AlmostEqual(explosion,.1f) && a.Activation.AddSeconds(.7)>=WorldState.CurrentTime
                                    && a.Activation<=_circleAt && (a.ShapeDistance??a.Shape.Distance(a.Origin,a.Rotation)).Distance(p)<clearance))continue;
                                exits.Add((p,next));
                            }
                        List<WPos>? walk=null;
                        // run2384 23:51:31：没有可达出口时这里对上千个出口逐个做完整校验，主线程一帧卡了 2570ms，
                        // 人物原地吃了两道支配魔刃。每帧只试最近的 MaxExitChecks 个；卡死比少试几个远出口危险得多。
                        foreach(var exit in exits.OrderBy(p=>p.Distance).Take(MaxExitChecks))
                            if(TryValidatedEscape(actor.Position,exit.Position,visible,hints,inputDelay) is {} reachable)
                            { walk=reachable;_escapeFallbackGoal=exit.Position;break; }
                        if(walk==null)
                        {
                            if(_circleStaging==null && _prisonExitCells.TryGetValue(((int)MathF.Round(explosion.X*2),(int)MathF.Round(explosion.Z*2)),out var pocketExit))
                            {
                                _circleStaging=pocketExit;
                                var advance=(pocketExit-Module.Center).LengthSq();
                                // Refine only this local staging cell. Every
                                // candidate keeps the original inward floor,
                                // prison clearance and complete time validation.
                                // 同一结果（最靠近场中、通过完整校验的那个格点），但先按离场中远近排序，第一个通过校验就停，
                                // 不再对 441 个格点全部做完整校验（run2384 卡顿）。
                                var refine=new List<(WPos P,float Next)>();
                                for(var dx=-10;dx<=10;dx++)for(var dz=-10;dz<=10;dz++)
                                {
                                    var p=pocketExit+new WDir(dx*.05f,dz*.05f);
                                    var next=(p-Module.Center).LengthSq();
                                    if(next>=advance || visible.Any(a=>!a.Origin.AlmostEqual(explosion,.1f)
                                        && a.Activation.AddSeconds(.7)>=WorldState.CurrentTime
                                        && (a.ShapeDistance??a.Shape.Distance(a.Origin,a.Rotation)).Distance(p)<clearance))continue;
                                    refine.Add((p,next));
                                }
                                foreach(var c in refine.OrderBy(c=>c.Next).Take(MaxExitChecks))
                                    if(TryValidatedEscape(actor.Position,c.P,visible,hints,inputDelay)!=null){_circleStaging=c.P;break;}
                            }
                            if(_circleStaging is {} pocket)walk=TryValidatedEscape(actor.Position,pocket,visible,hints,inputDelay);
                        }
                        if(walk!=null)
                        {
                            hints.DirectRoute=walk;
                            hints.DirectRouteDeadline=_circleAt.AddSeconds(.5);
                        }
                    }
                }
            }
        }
    }

    // Optional ordinary walking shortcut. Its own stricter check retains the
    // normal solver's .5s danger preparation; the runtime then validates again.
    public List<WPos>? TryValidatedEscape(WPos from,WPos goal,Components.GenericAOEs.AOEInstance[] visible,AIHints hints,double inputDelay)
    {
        WPos[] corners=[new(518.05f,-400.05f),new(521.95f,-400.05f),new(518.05f,-439.95f),new(521.95f,-439.95f),
            new(518.1f,-400.5f),new(521.9f,-400.5f),new(518.1f,-439.5f),new(521.9f,-439.5f),
            new(510.5f,-414.4f),new(510.5f,-410.6f),new(510.5f,-424.4f),new(510.5f,-420.6f),
            new(529.5f,-419.4f),new(529.5f,-415.6f),new(529.5f,-429.4f),new(529.5f,-425.6f)];
        var points=new[]{from,goal}.Concat(corners.Where(CrucibleLaudaFloor.Contains)).ToArray();
        var distance=Enumerable.Repeat(float.MaxValue,points.Length).ToArray();var previous=Enumerable.Repeat(-1,points.Length).ToArray();
        var visited=new bool[points.Length];distance[0]=0;
        bool FloorEdge(WPos a,WPos b)
        {
            var count=Math.Max(1,(int)MathF.Ceiling((b-a).Length()/.08f));
            for(var k=0;k<=count;k++)if(!CrucibleLaudaFloor.Contains(a+(b-a)*(k/(float)count)))return false;
            return true;
        }
        for(var step=0;step<points.Length;step++)
        {
            var index=-1;for(var i=0;i<points.Length;i++)if(!visited[i]&&(index<0||distance[i]<distance[index]))index=i;
            if(index<0||distance[index]==float.MaxValue)break;if(index==1)break;visited[index]=true;
            for(var i=0;i<points.Length;i++)
            {
                var next=distance[index]+(points[i]-points[index]).Length();
                if(!visited[i]&&next<distance[i]&&FloorEdge(points[index],points[i])){distance[i]=next;previous[i]=index;}
            }
        }
        if(distance[1]==float.MaxValue)return null;
        var indices=new List<int>();for(var i=1;i>0;i=previous[i]){if(i<0)return null;indices.Add(i);}indices.Reverse();
        var ordinaryClearance=MathF.Max(.5f,WorldState.NavigationMargin)+.05f;
        bool Safe(WPos p,double after)
        {
            var at=WorldState.CurrentTime.AddSeconds(after);
            if(!CrucibleLaudaFloor.Contains(p))return false;
            foreach(var z in hints.ForbiddenZones)
                if((z.Activation==default||z.Activation==DateTime.MaxValue||at>=z.Activation.AddSeconds(-.5))
                    &&(z.ExpiresAt==null||at<=z.ExpiresAt.Value.AddSeconds(.1))&&z.Shape.Distance(p)<(z.ExactGeometry?.05f:ordinaryClearance))return false;
            foreach(var a in visible)
                if(at>=a.Activation.AddSeconds(-.5)&&at<=a.Activation.AddSeconds(.7)
                    &&(a.ShapeDistance??a.Shape.Distance(a.Origin,a.Rotation)).Distance(p)<ordinaryClearance)return false;
            return true;
        }
        for(var t=0.0;t<=inputDelay+.0001;t+=.02)if(!Safe(from,t))return null;
        if(distance[1]<.1f)
            return Safe(goal,inputDelay+.1)?[goal]:null;
        // Native input may be acquired anywhere in the complete 0..100ms
        // window. Check every timing branch, including the final standing tail;
        // checking only the latest start can falsely cross after an AOE clears.
        var offsetCount=Math.Max(1,(int)Math.Ceiling(inputDelay/.025));
        for(var offsetIndex=0;offsetIndex<=offsetCount;offsetIndex++)
        {
            var elapsed=inputDelay*offsetIndex/offsetCount;var origin=from;
            foreach(var i in indices)
            {
                var end=points[i];var length=(end-origin).Length();var count=Math.Max(1,(int)MathF.Ceiling(length/.05f));
                for(var k=0;k<=count;k++)if(!Safe(origin+(end-origin)*(k/(float)count),elapsed+length/WorldState.WalkingSpeed*k/count))return null;
                elapsed+=length/WorldState.WalkingSpeed;origin=end;
            }
            for(var hold=0.0;hold<=.10001;hold+=.025)if(!Safe(origin,elapsed+hold))return null;
        }
        return indices.Select(i=>points[i]).ToList();
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_drop != null && _target == actor.InstanceID)
            hints.Add(Module.FindComponent<CrucibleLaudaBaits>()?.Preparing == true
                ? "飞刀与剑线重叠：保持上下中央凸台，剑线锁定后躲开！"
                : "飞刀点名：放到侧面安全凸台！", false);
        if (_explosion != null)
            hints.Add("魔刃核爆：远离飞刀，注意同时处理止步击退！", false);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (_drop is { } drop && _target == pc.InstanceID && Module.FindComponent<CrucibleLaudaBaits>()?.Preparing != true)
            new AOEShapeCircle(1).Outline(Arena, drop, default, Colors.Safe);
        if (_explosion is { } explosion)
            new AOEShapeCircle(6).Outline(Arena, explosion, default, Colors.Danger);
    }
}
