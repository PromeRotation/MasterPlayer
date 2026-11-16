// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Low-one ARRs 1758/1759: after 46874, four 46900 lanes advance 3y
// per ~2.6s, along the bishop's 46873 facing and its quarter turns.
// Waiting for each 1.5s helper cast leaves too little time to escape overlap.
// Actual cast packets supersede predictions; generic AOEs still handle them.
sealed class CrucibleBlackfire(BossModule module) : BossComponent(module)
{
    private readonly List<(WPos Position, DateTime Activation)> _predicted = [];
    private Angle _direction;
    private DateTime _directionAt;
    private DateTime _centerActivation;
    private WPos _origin;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 46873)
        {
            _direction = spell.Rotation;
            _directionAt = WorldState.CurrentTime;
            _centerActivation = default;
            _predicted.Clear();
        }
        else if (spell.Action.ID == 46874 && _directionAt != default
                 && (WorldState.CurrentTime - _directionAt).TotalSeconds < 2)
        {
            _origin = spell.LocXZ;
            _centerActivation = Module.CastFinishAt(spell);
            Predict();
        }
        else if (spell.Action.ID == 46900)
        {
            _predicted.RemoveAll(p => p.Position.AlmostEqual(spell.LocXZ, .5f)
                && Math.Abs((p.Activation - Module.CastFinishAt(spell)).TotalSeconds) < 1);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 46874 && _centerActivation != default)
        {
            _centerActivation = WorldState.CurrentTime;
            Predict();
        }
        else if (spell.Action.ID == 46900)
            _predicted.RemoveAll(p => p.Position.AlmostEqual(caster.Position, .5f)
                && Math.Abs((p.Activation - WorldState.CurrentTime).TotalSeconds) < 1);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 46873 or 46874 && spell.Interrupted)
            _predicted.Clear();
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var now = WorldState.CurrentTime;
        _predicted.RemoveAll(p => p.Activation.AddSeconds(.5) < now);
        foreach (var p in _predicted)
            hints.AddForbiddenZone(new SDCircle(p.Position, 5f), p.Activation.AddSeconds(-.2), p.Activation.AddSeconds(.5));
    }

    private void Predict()
    {
        _predicted.Clear();
        for (var step = 1; step <= 7; step++)
            for (var lane = 0; lane < 4; lane++)
                _predicted.Add((_origin + (_direction + (90f * lane).Degrees()).ToDirection() * (3f * step),
                    _centerActivation.AddSeconds(2.6 * step)));
    }
}

// 46917 leaves two 5y pools; the smaller 46916 explosions do not leave voidzones.
// ARR 2026-09-10 16:49:04: EObj 0x1EC025 spawns at the two large impacts,
// then receives EventState 7 after ~23s. Track the objects, not a guessed duration.
sealed class CrucibleLava(BossModule module) : Components.Voidzone(module, 5f, GetLava)
{
    private static IEnumerable<Actor> GetLava(BossModule module) => module.Enemies(0x1EC025)
        .Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed);
}

// Fire Spirit's Ward leaves EObj 0x1E9927 at the arena center. ARR 17:30:59
// records Burning (3065) inside this field; the object persists until EventState 7.
// Its b0740 SGB/AVFX has a 6y core. Keep it blocked even after the summoning cast.
sealed class CrucibleFireSpiritWard(BossModule module) : Components.Voidzone(module, 6f, GetWard)
{
    private static IEnumerable<Actor> GetWard(BossModule module) => module.Enemies(0x1E9927)
        .Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed);
}

// Spreading Fire (46922): client radius 10y, but the actual cast is only 0.7s.
// Chasing model from six balls (run1792/1793/1796/1799 ACT/ARR): the ball stands ~5s after spawn,
// then closes on the player at ~3y/s and starts 46922 once within ~1.6–2.8y; balls that were not
// caught cast at ~14.4–15.0s after spawn. Holding the full 10y from spawn only produced radial
// escapes into the wall (run1796/1799 ended pinned there), so the danger is the catch radius while
// the ball is young and the full circle near the fuse or during the cast, plus a rim-kiting goal.
sealed class CrucibleFireball(BossModule module) : Components.GenericAOEs(module, 46922u, "离开火球的延烧范围！")
{
    internal static readonly AOEShapeCircle DetonationShape = new(10f);
    internal static readonly AOEShapeCircle PlanningShape = new(11f);
    internal static readonly AOEShapeCircle CatchShape = new(4f);
    internal const double KiteStartAge = 3.5, SpreadAge = 11, FuseWarnAge = 13.5;
    internal const float KiteRimRadius = 13f, KiteStep = 7f, KiteClearance = 7f;
    internal const int KitePriority = 200; // above CrucibleOgreLanding's 100: a live ball outranks the landing spot
    private readonly HashSet<ulong> _resolved = [];
    private readonly Dictionary<ulong, DateTime> _spawned = [];
    private readonly Dictionary<ulong, KiteRoute> _routes = [];
    private readonly Dictionary<ulong, (WPos Position, DateTime Finish)> _detonations = [];
    internal Action<string>? RouteDiagnostic;
    private (WPos Origin, Angle Rotation, DateTime Activation)? _jumpSlam;
    private bool HasLiveBall => Module.Enemies(19343u).Any(a => !a.IsDeadOrDestroyed && !_resolved.Contains(a.InstanceID));

    private sealed record KitePath(WPos Position, KitePath? Previous);
    private sealed class KiteRoute(DateTime at, WPos[] points)
    {
        public readonly DateTime At = at;
        public readonly WPos[] Points = points;
        public int Next;
    }
    private readonly record struct KiteNode(WPos Player, WPos Ball, double CastAt, WPos Goal, WDir Direction, float Cost, KitePath? Path, bool OutsideCatch);

    // These are planning estimates from the ARR chase samples, not a replacement for actual casts.
    // Only the preference is predicted; the shared movement solver still validates every dispatch.
    private WPos? PredictKite(Actor actor, Actor fireball, double age, AIHints.ForbiddenZone[] zones)
    {
        if (!double.IsFinite(age)) return null;
        var now = WorldState.CurrentTime;
        var casting = _detonations.TryGetValue(fireball.InstanceID, out var detonation);
        if (_routes.TryGetValue(fireball.InstanceID, out var cached)
            && ContinueRoute(actor, fireball, age, zones, cached) is { } continuation)
            return continuation;
        const double step = .5;
        // The selected goal is not applied instantly: reserve the bounded
        // worker/dispatch startup interval while the chasing hazard keeps moving.
        const double reactionSeconds = .4;
        const float playerSpeed = 6, ballSpeed = 3;
        var castAt = casting ? (detonation.Finish - now).TotalSeconds - .7 : double.PositiveInfinity;
        var beam = new List<KiteNode> { new(actor.Position, fireball.Position, castAt, actor.Position, default, 0, null, (actor.Position - fireball.Position).Length() >= 4.5f) };
        var horizon = Math.Clamp(15.7 - age + .5, 4, 16);
        var layers = (int)Math.Ceiling(horizon / step);
        const int goalLayers = 3;
        const float bossWeight = .02f, deadlineWeight = .5f;
        for (var layer = 1; layer <= layers; ++layer)
        {
            var elapsed = layer * step;
            var next = new List<KiteNode>();
            foreach (var node in beam)
                for (var direction = 0; direction < 16; ++direction)
                {
                    var heading = direction < 0 ? default : (direction * 22.5f).Degrees().ToDirection();
                    var p = node.Player;
                    var b = node.Ball;
                    var cast = node.CastAt;
                    var valid = true;
                    var outsideCatch = node.OutsideCatch;
                    for (var sample = 1; sample <= 5; ++sample)
                    {
                        var time = elapsed - step + sample * .1;
                        var previousGap = (p - b).Length();
                        if (time > reactionSeconds) p += heading * (playerSpeed * .1f);
                        if (!Module.Arena.InBounds(p)) { valid = false; break; }
                        foreach (var zone in zones)
                        {
                            var activation = (zone.Activation - now).TotalSeconds;
                            if (zone.Activation == DateTime.MaxValue || activation > time + .6
                                || zone.ExpiresAt is { } expires && expires < now.AddSeconds(time)) continue;
                            if (zone.Shape.Distance(p) <= .8f) { valid = false; break; }
                        }
                        if (!valid) break;
                        if (double.IsPositiveInfinity(cast))
                        {
                            var gap = (p - b).Length();
                            if (gap <= 3 || age + time >= 15) cast = time;
                            else if (age + time >= 5) b += (p - b) / gap * (ballSpeed * .1f);
                        }
                        // Stay outside the caught radius until the guaranteed explosion has resolved.
                        var gapAfterStep = (p - b).Length();
                        if (time <= cast + .9 && !ValidCatchEscape(previousGap, gapAfterStep, ref outsideCatch)
                            || time >= cast + .5 && time <= cast + .9 && gapAfterStep < 11.5f)
                        { valid = false; break; }
                    }
                    if (!valid) continue;
                    var goal = layer <= goalLayers ? p : node.Goal;
                    var turn = node.Direction.LengthSq() > .1f ? 1 - heading.Dot(node.Direction) : 0;
                    var gapAfter = (p - b).Length();
                    // Prefer a continuing route with separation; do not oscillate between distant endpoints.
                    var cost = node.Cost + .12f * turn + (direction < 0 ? .15f : .03f)
                        + MathF.Max(0, 12 - gapAfter) * .04f
                        + MathF.Max(0, (p - Module.PrimaryActor.Position).Length() - 6) * bossWeight;
                    foreach (var zone in zones)
                    {
                        var remaining = (zone.Activation - now).TotalSeconds - elapsed;
                        if (zone.Activation != DateTime.MaxValue && remaining > 0)
                            cost += MathF.Max(0, 1 - zone.Shape.Distance(p)) * deadlineWeight / (float)Math.Max(.5, remaining);
                    }
                    next.Add(new(p, b, cast, goal, heading, cost, new(p, node.Path), outsideCatch));
                }
            if (next.Count == 0) return null;
            // Keep different positions and chase states, rather than filling the beam with one dead-end.
            beam = next.OrderBy(n => n.Cost - MathF.Min(14, (n.Player - n.Ball).Length()) * .1f)
                .DistinctBy(n => ((int)MathF.Round(n.Player.X / 2), (int)MathF.Round(n.Player.Z / 2),
                    (int)MathF.Round(n.Ball.X / 3), (int)MathF.Round(n.Ball.Z / 3)))
                .Take(48).ToList();
        }
        foreach (var best in beam.OrderBy(n => n.Cost - MathF.Min(16, (n.Player - n.Ball).Length()) * .1f))
        {
            var points = new List<WPos>();
            for (var node = best.Path; node != null; node = node.Previous) points.Add(node.Position);
            points.Reverse();
            var route = new KiteRoute(now, points.ToArray());
            if (ContinueRoute(actor, fireball, age, zones, route) is not { } goal) continue;
            _routes[fireball.InstanceID] = route;
            RouteDiagnostic?.Invoke($"new age={age:F2} points={string.Join(';', route.Points.Select(p => $"{p.X:F1},{p.Z:F1}"))}");
            return goal;
        }
        return null;
    }

    // Keep the selected polyline, not just its distant endpoint. Re-time it from
    // the observed player/ball every frame; new mechanics can invalidate it.
    private WPos? ContinueRoute(Actor actor, Actor fireball, double age, AIHints.ForbiddenZone[] zones, KiteRoute route)
    {
        while (route.Next < route.Points.Length && (route.Points[route.Next] - actor.Position).LengthSq() < 1.44f)
            ++route.Next;
        if (route.Next == route.Points.Length) { RouteDiagnostic?.Invoke($"end age={age:F2}"); return null; }
        for (var candidate = Math.Min(route.Next + 3, route.Points.Length - 1); candidate >= route.Next; --candidate)
        {
            if (candidate > route.Next && (route.Points[candidate] - actor.Position).LengthSq() > 36) continue;
            if (!ValidateRemainingRoute(actor, fireball, age, zones, route, candidate)) continue;
            route.Next = candidate;
            return route.Points[candidate];
        }
        return null;
    }

    private bool ValidateRemainingRoute(Actor actor, Actor fireball, double age, AIHints.ForbiddenZone[] zones, KiteRoute route, int next)
    {
        var now = WorldState.CurrentTime;
        var p = actor.Position;
        var b = fireball.Position;
        var casting = _detonations.TryGetValue(fireball.InstanceID, out var detonation);
        var cast = casting ? (detonation.Finish - now).TotalSeconds - .7 : double.PositiveInfinity;
        var outsideCatch = (p - b).Length() >= 4.5f;
        var startup = Math.Max(0, .4 - (now - route.At).TotalSeconds);
        var horizon = Math.Clamp(16.2 - age, 4, 16);
        for (var t = .1; t <= horizon; t += .1)
        {
            var previousGap = (p - b).Length();
            var movement = t > startup ? .6f : 0;
            while (movement > .001f && next < route.Points.Length)
            {
                var leg = route.Points[next] - p;
                var distance = leg.Length();
                if (distance <= movement)
                {
                    p = route.Points[next++];
                    movement -= distance;
                }
                else { p += leg / distance * movement; movement = 0; }
            }
            if (!Module.Arena.InBounds(p)) { RouteDiagnostic?.Invoke($"wall age={age:F2} ahead={t:F2}"); return false; }
            foreach (var zone in zones)
                if (zone.Activation != DateTime.MaxValue && zone.Activation <= now.AddSeconds(t + .6)
                    && !(zone.ExpiresAt is { } expires && expires < now.AddSeconds(t))
                    && zone.Shape.Distance(p) <= .8f) { RouteDiagnostic?.Invoke($"zone age={age:F2} ahead={t:F2} p={p}"); return false; }
            var gap = (p - b).Length();
            if (double.IsPositiveInfinity(cast))
            {
                if (gap <= 3 || age + t >= 15) cast = t;
                else if (age + t >= 5) b += (p - b) / gap * .3f;
            }
            gap = (p - b).Length();
            if (t <= cast + .9 && !ValidCatchEscape(previousGap, gap, ref outsideCatch)
                || t >= cast + .5 && t <= cast + .9 && gap < 11.5f)
            { RouteDiagnostic?.Invoke($"ball age={age:F2} ahead={t:F2} gap={gap:F2} next={route.Next}"); return false; }
        }
        return true;
    }

    // RequireOutwardExit permits an inherited overlap to escape, never a new
    // entry or a step deeper into the moving hazard. Once clear, keep the guard.
    internal static bool ValidCatchEscape(float previousGap, float gap, ref bool outside)
    {
        if (gap >= 4.5f) { outside = true; return true; }
        return !outside && gap >= previousGap - .0001f;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var center = Module.Center;
        AIHints.ForbiddenZone? jumpZone = null;
        if (_jumpSlam is { } jump && HasLiveBall && !Module.PrimaryActor.IsDeadOrDestroyed
            && WorldState.CurrentTime <= jump.Activation.AddSeconds(.6))
            jumpZone = new(CrucibleSpells.Table[46912].Shape.Distance(jump.Origin, jump.Rotation), jump.Activation, jump.Activation.AddSeconds(.6));
        else _jumpSlam = null;
        var actualZones = hints.ForbiddenZones.ToArray(); // authoritative cones/lava from earlier components
        var others = jumpZone is { } forecast ? actualZones.Append(forecast).ToArray() : actualZones;
        foreach (var fireball in Module.Enemies(19343u))
        {
            if (fireball.IsDeadOrDestroyed || _resolved.Contains(fireball.InstanceID)) continue;
            hints.GoalDirectedTemporalPlanning = true;
            var age = _spawned.TryGetValue(fireball.InstanceID, out var at) ? (WorldState.CurrentTime - at).TotalSeconds : double.PositiveInfinity;
            var casting = _detonations.TryGetValue(fireball.InstanceID, out var detonation);
            var armed = age >= FuseWarnAge || casting;
            var ball = fireball.Position;
            hints.ForbiddenZones.Add(new(CatchShape.Distance(ball, default), default, null) { RequireOutwardExit = true });
            if (casting)
                hints.ForbiddenZones.Add(new(PlanningShape.Distance(detonation.Position, default), detonation.Finish, detonation.Finish.AddSeconds(.6)) { RequireOutwardExit = true });
            var current = (actor.Position - ball).Length();
            // Kite once the ball is about to move (~5s after spawn) or is already close; a young ball is harmless.
            // An early escape route stays active after it clears the close
            // radius. ContinueRoute still revalidates it; the young/far gate
            // only suppresses starting an escape that was never needed.
            if (!(armed || age >= KiteStartAge || current <= CatchShape.Radius + 1.5f
                || _routes.ContainsKey(fireball.InstanceID)))
                continue;
            var predicted = PredictKite(actor, fireball, age, others);
            if (predicted == null && jumpZone != null)
            {
                // A tentative warning must not erase an executable escape and
                // hand movement back to attack follow. Authoritative hazards
                // stay present in this retry, including an actual 46912 cast.
                predicted = PredictKite(actor, fireball, age, actualZones);
            }
            else if (predicted != null && jumpZone is { } plannedForecast)
                hints.ForbiddenZones.Add(plannedForecast);
            if (predicted is { } kitePoint)
            {
                hints.GoalPoints.Add((kitePoint, .5f, KitePriority));
                continue;
            }
            others = actualZones;
            // Keep a kiting goal even when already far from the ball. Search both inner and outer rings;
            // the shared planner still validates movement against the other mechanics.
            var radial = actor.Position - center;
            var heading = radial.LengthSq() > 1 ? Angle.FromDirection(radial) : Angle.FromDirection(actor.Position - ball);
            WPos? best = null; var bestScore = float.MinValue;
            void Consider(WPos candidate)
            {
                var distance = (candidate - ball).Length();
                var leg = candidate - actor.Position;
                var length = leg.Length();
                if (length < 1 || distance < current + 1 || !Module.Arena.InBounds(candidate)) return;
                // Never plan a leg that passes the ball: closest approach along the straight leg must stay clear.
                var t = Math.Clamp((ball - actor.Position).Dot(leg) / (length * length), 0, 1);
                if ((actor.Position + leg * t - ball).Length() < Math.Min(KiteClearance, current) - .1f) return;
                // A kiting preference must never offer a landing point inside another mechanic.
                // A soft score allowed extra ball separation to win over the wall-slam cone.
                foreach (var zone in others)
                    if (zone.Activation != DateTime.MaxValue && zone.Shape.Distance(candidate) <= 1.5f) return;
                // Near the fuse maximise separation outright; earlier, trade a little distance for a shorter leg.
                var score = age >= SpreadAge ? distance : distance - .3f * length;
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            foreach (var radius in new[] { KiteRimRadius, 18f })
                for (var k = -7; k <= 7; ++k)
                    Consider(center + (heading + (25f * k).Degrees()).ToDirection() * radius);
            if (current > .1f)
            {
                var away = actor.Position + (actor.Position - ball) / current * KiteStep;
                var offset = away - center;
                if (offset.Length() > KiteRimRadius) away = center + offset / offset.Length() * KiteRimRadius;
                Consider(away);
            }
            if (best is { } point) hints.GoalPoints.Add((point, 1f, KitePriority));
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var aoes = new List<AOEInstance>();
        foreach (var fireball in Module.Enemies(19343u))
            if (!fireball.IsDeadOrDestroyed && !_resolved.Contains(fireball.InstanceID))
                aoes.Add(new(DetonationShape, fireball.Position, actorID: fireball.InstanceID));
        return CollectionsMarshal.AsSpan(aoes);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 46914u && caster.InstanceID == Module.PrimaryActor.InstanceID && HasLiveBall)
        {
            var offset = spell.TargetXZ - Module.Center;
            // The zero-target jump effect carries its landing coordinate. A
            // wall jump with a live ball precedes the inward 46912 cone; center
            // jumps belong to the ward sequence and never create this warning.
            if (offset.LengthSq() is >= 196 and <= 324)
                _jumpSlam = (spell.TargetXZ, Angle.FromDirection(-offset), WorldState.FutureTime(7.8));
            else _jumpSlam = null;
        }
        if (spell.Action.ID == WatchedAction)
        {
            ++NumCasts;
            _resolved.Add(caster.InstanceID);
            _routes.Remove(caster.InstanceID);
            _detonations.Remove(caster.InstanceID);
            if (!HasLiveBall) _jumpSlam = null;
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 46912 or 46910 or 46918) _jumpSlam = null;
        if (spell.Action.ID == WatchedAction)
            _detonations[caster.InstanceID] = (spell.LocXZ, spell.FinishAt);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 46912 or 46913 && spell.Interrupted) _jumpSlam = null;
        if (spell.Action.ID == WatchedAction && spell.Interrupted)
            _detonations.Remove(caster.InstanceID);
    }

    public override void OnActorCreated(Actor actor)
    {
        _resolved.Remove(actor.InstanceID);
        _routes.Remove(actor.InstanceID);
        _detonations.Remove(actor.InstanceID);
        if (actor.OID == 19343u) _spawned[actor.InstanceID] = WorldState.CurrentTime;
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor.InstanceID == Module.PrimaryActor.InstanceID) _jumpSlam = null;
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == Module.PrimaryActor.InstanceID) _jumpSlam = null;
        _resolved.Remove(actor.InstanceID);
        _routes.Remove(actor.InstanceID);
        _detonations.Remove(actor.InstanceID);
        _spawned.Remove(actor.InstanceID);
    }
}

// 火灵的守护期间食人魔不可选中，回来时固定在场心（run1763/1764 四次都在 120,-420）。
// 大妖火（DPS 检测）死后不再满场追小妖火：站到场心守护火场（R6）外的近战圈，
// 小妖火自己走到场心 3.5m 处，食人魔落地即可开打。大妖火活着时照常追它。
sealed class CrucibleOgreLanding(BossModule module) : BossComponent(module)
{
    internal const uint OgreOID = 19341u, LargeFireOID = 19924u, SmallFireOID = 19342u;
    internal const float LandingRadius = 7f;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (LandingPoint(actor) is { } point)
            hints.GoalPoints.Add((point, 0.5f, 100));
    }

    internal WPos? LandingPoint(Actor actor)
    {
        if (actor.IsDeadOrDestroyed || Module.Enemies(OgreOID).FirstOrDefault(a => !a.IsDeadOrDestroyed) is not { IsTargetable: false } ogre
            || Module.Enemies(LargeFireOID).Any(a => !a.IsDeadOrDestroyed && a.HPMP.CurHP > 0)
            // 小妖火走到场心就「大火焰」打当前坦（每只约 134，1769/1770 两次尝试 12 只全到、宠物白掉 1600）：
            // 还有小妖火活着就照常跟着打，它们自己往场心聚，全部结算后再站落点。
            || Module.Enemies(SmallFireOID).Any(a => !a.IsDeadOrDestroyed && a.HPMP.CurHP > 0))
            return null;
        var offset = actor.Position - Module.Center;
        if (offset.LengthSq() < 1)
            offset = new WDir(0, 1);
        return Module.Center + offset.Normalized() * LandingRadius;
    }
}

// 骑士前线护卫（格挡 680）只能从背后打。MP 是唯一移动者：PAW 的 vnavmesh 绕背见到 MP「下发中」就让路，
// 而 MP 平时跟怪也报下发中，run1766/1769 骑士正面打盾、首场 212s。骑士没盯玩家（宠物在扛）时目标点放到它背后近战圈。
sealed class CrucibleKnightRear(BossModule module) : BossComponent(module)
{
    internal const uint KnightOID = 19334u, ParryStatus = 680u;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (RearPoint(actor) is { } point)
            hints.GoalPoints.Add((point, 0.5f, 100));
    }

    internal WPos? RearPoint(Actor actor)
    {
        if (actor.IsDeadOrDestroyed || Module.Enemies(KnightOID).FirstOrDefault(a => !a.IsDeadOrDestroyed && a.IsTargetable) is not { } knight
            || knight.FindStatus(ParryStatus) == null || knight.TargetID == actor.InstanceID)
            return null;
        return Point(knight.Position, knight.Rotation, knight.HitboxRadius + actor.HitboxRadius);
    }

    // 生产近战停步距离 3-0.5 码再加双方碰撞半径；留 1 码余量站在圈内
    internal static WPos Point(WPos knight, Angle rotation, float radii) => knight - rotation.ToDirection() * (radii + 1.5f);
}

// 燃烧猛击跳场边：食人魔先跳到 A（46910，6s），同时在对面 B 读 49688（9s，扇形 R40 半角 63°），
// 49688/46911 判定后食人魔就站在 B（run1765 三次：西/东/南墙）。run1764/1765 人躲完离 B 15–28m，落地后再走过去。
// 读条一开始就把目标点放到 B 侧面扇形外（保持 63° 之外，并按实际碰撞圈贴在近战停步半径内），
// 危险由同一解算器避让：A 那次判定前过不去就等，判定后直接站到 B 旁边。
sealed class CrucibleOgreSlamLanding(BossModule module) : BossComponent(module)
{
    // 49688：对面墙 9 秒大扇形；46912：食人魔就站在本墙读的 5.7 秒同形扇形（run1786 两次被它打中，
    // 火球追人时 MP 往扇形里退）。两种都是站到施法点侧面最安全、也在近战圈内。
    internal const uint SlamAction = 49688u, WallSlamAction = 46912u;
    internal const float Side = 6.5f, Forward = 1f;
    private (WPos Origin, Angle Rotation, DateTime FinishAt)? _slam;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is SlamAction or WallSlamAction)
            _slam = (spell.LocXZ, spell.Rotation, spell.FinishAt);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (LandingPoint(actor, WorldState.CurrentTime) is { } point)
            hints.GoalPoints.Add((point, 0.5f, 100));
    }

    internal WPos? LandingPoint(Actor actor, DateTime now)
    {
        if (_slam is not { } slam || actor.IsDeadOrDestroyed)
            return null;
        if (now > slam.FinishAt.AddSeconds(1))
        {
            _slam = null;
            return null;
        }
        var ogre = Module.Enemies(CrucibleOgreLanding.OgreOID).FirstOrDefault(a => !a.IsDeadOrDestroyed);
        // Older anonymous fixtures do not carry collision radii; retain the
        // established side point there. Live actors always expose both radii.
        if (ogre is null || ogre.HitboxRadius <= 0 && actor.HitboxRadius <= 0)
            return Point(slam.Origin, slam.Rotation, actor.Position);
        return Point(slam.Origin, slam.Rotation, actor.Position,
            ogre?.HitboxRadius ?? 0, actor.HitboxRadius);
    }

    internal static WPos Point(WPos origin, Angle rotation, WPos player)
        => Point(origin, rotation, player, 0, 0, 6.5f);

    private static WPos Point(WPos origin, Angle rotation, WPos player, float targetRadius, float playerRadius)
        => Point(origin, rotation, player, targetRadius, playerRadius, MathF.Sqrt(MathF.Max(0,
            MathF.Pow(targetRadius + playerRadius + 2.5f, 2) - Forward * Forward)));

    private static WPos Point(WPos origin, Angle rotation, WPos player, float targetRadius, float playerRadius, float sideDistance)
    {
        var forward = rotation.ToDirection();
        var side = forward.OrthoL();
        if (side.Dot(player - origin) < 0)
            side = -side;
        return origin + forward * Forward + side * sideDistance;
    }
}

// Four simultaneous cast starts, resolving from the center out at 2s intervals.
// Only this sequence is risk-graded; concurrent web mechanics remain independently dangerous.
sealed class CrucibleGroundUpliftAOEs(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    internal static bool IsUplift(uint actionID) => actionID is >= 46902 and <= 46905;
    protected override double RiskyActivationWindow => 0.5d;
    protected override AOEConfig? ConfigFor(uint actionID) => IsUplift(actionID)
        ? new(CrucibleSpells.Table[actionID].Shape, LocationTargeted: true) : null;

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        _ = ActiveAOEs(slot, actor); // Prune expired/canceled casts before reading the complete queue.
        var opening = Pending.Length > 0 && Pending[0].ActionID == 46902;
        foreach (var entry in Pending)
            CrucibleScorpionAI.Avoid(hints, entry.AOE);

        // PR represents every wave with its own window, including the center reopening.
        if (opening && Pending.Length > 1)
        {
            // R9 leaves a staging annulus outside the buffered R6 opening hit.
            hints.AddForbiddenConstraint(new SDInvertedCircle(Pending[0].AOE.Origin, 9f), Pending[0].AOE.Activation.AddSeconds(-1d), Pending[0].AOE.Activation.AddSeconds(0.6));
            hints.GoalPoints.Add((Pending[0].AOE.Origin, 8f, 10));
        }
        else if (Pending.Length > 0)
            hints.GoalPoints.Add((Pending[0].AOE.Origin, 3f, 10));
    }
}

// The central web and eight outer circles resolve ~0.25s apart, not as one filled disk.
sealed class CrucibleWebAOEs(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    protected override int MaxDisplayed => int.MaxValue;
    protected override double RiskyActivationWindow => 0.15d;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == 46908
        ? new(CrucibleSpells.Table[actionID].Shape, LocationTargeted: true) : null;

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        _ = ActiveAOEs(slot, actor);
        var now = WorldState.CurrentTime;
        // Recognize the central cast geometrically, including module activation midway through
        // a sequence (existing casts are replayed in actor order, not cast-start order).
        var opening = Pending.Length > 0 && Pending[0].AOE.Origin.AlmostEqual(Module.Center, 1f);
        if (opening)
        {
            _centerActivation = Pending[0].AOE.Activation;
            _order ??= OuterOrder();
        }
        else if (Pending.Length == 0)
        {
            _centerActivation = default;
            _order = null;
            _reach = 0;
        }
        // The first two outer casts establish the 45-degree turn. Stage outside
        // the center on the last circle's ray, then enter after the center clears.
        // Every outer circle keeps its real danger window throughout preparation.
        var staged = _order is { } order && _centerActivation != default;
        // The wait ends as soon as the center's effect arrives (it then leaves Pending).
        var waiting = staged && opening && now < _centerActivation.AddSeconds(CenterEnd);
        // All central and outer casts retain their individual PR danger windows.
        foreach (var entry in Pending)
            hints.AddForbiddenZone(entry.AOE.Shape, entry.AOE.Origin, entry.AOE.Rotation, entry.AOE.Activation.AddSeconds(-0.1), entry.AOE.Activation.AddSeconds(0.6));
        // The preparation point can lie inside an add's earlier web line.
        // Let that line resolve before imposing the later web's staging constraint.
        if (staged)
        {
            // The wait spot is a ~1.5y pocket between the center circle and the first outer circle;
            // a goal alone lost to cells that were merely safe until the outer circles fired.
            var spot = Module.Center + _order!.Value.Wait.ToDirection() * WaitRadius;
            if (waiting && WorldState.Actors.Any(a => a.CastInfo is { EventHappened: false } cast
                && cast.Action.ID == WebLine && cast.FinishAt < _centerActivation
                && CrucibleSpells.Table[WebLine].Shape.Check(spot, a.Position, cast.Rotation)))
                return;
            if (waiting)
            {
                // 2026-10-30 afternoon ARRs: the adds' 46909 lines (3s before the web) kept the planner
                // from crossing to the spot; it fled the pending center circle to the far side and,
                // held 1.5s ahead, could not get back in time (NoTemporalPath, then the center hit).
                // Hold the spot as soon as no line remains, and meanwhile keep within reach of it.
                // The arrival deadline must allow reaching the far side after the
                // overlapping add line, rather than making its expiry the deadline.
                // Use the central web's own deadline; the host applies its
                // preparation margin to this constraint as well as the AOE.
                var hold = _centerActivation.AddSeconds(-0.6d);
                hints.AddForbiddenConstraint(new SDInvertedCircle(spot, WaitSlack), hold, _centerActivation);
                if (_reach == 0)
                    _reach = (actor.Position - spot).Length() + ReachSlack;
                if (now < hold)
                    hints.AddForbiddenConstraint(new SDInvertedCircle(spot, _reach), now, hold);
                hints.GoalPoints.Add(now < _centerActivation ? (spot, WaitSlack, 100) : (Module.Center, InnerRadius, 100));
            }
            else
            {
                // Outer circles already impose the actual escape deadlines. An
                // extra r<4.5 arrival constraint applied the solver's early
                // margin a second time and blocked the inward transit (run1762).
                hints.GoalPoints.Add((Module.Center, InnerRadius, 100));
            }
        }
        // Until two outer casts establish the turn, use only the real dangers.
        // A temporary R11 constraint can start an escape on the wrong side of
        // an add's line before the late-circle staging point becomes known.
    }

    private DateTime _centerActivation;
    // Distance from the spot when the sequence was first staged, plus slack.
    private float _reach;
    private (Angle Last, Angle Seventh, Angle Wait)? _order;
    // The center's danger window ends 0.6s after it fires (CrucibleScorpionAI.Avoid).
    private const double CenterEnd = 0.6;
    // The last circle's ray leaves enough distance from its earlier neighbours
    // for the inward transit. The center and outer danger margins remain intact.
    private const float WaitRadius = 10.1f, WaitSlack = 1.5f;
    private const uint WebLine = 46909;
    // A 3y reach slack still lets the player sidestep the earlier add lines.
    private const float ReachSlack = 3;
    // Outer circles (R9 at 15y) leave r < 6 open; keep clear of their margins.
    private const float InnerRadius = 3f;

    // Infer the last two outer directions once the first two casts are known.
    private (Angle Last, Angle Seventh, Angle Wait)? OuterOrder()
    {
        var outer = new List<(Angle Angle, DateTime Activation)>();
        foreach (var entry in Pending)
        {
            var offset = entry.AOE.Origin - Module.Center;
            if (offset.LengthSq() > 25)
                outer.Add((Angle.FromDirection(offset), entry.AOE.Activation));
        }
        if (outer.Count < 2)
            return null;
        outer.Sort((a, b) => a.Activation.CompareTo(b.Activation));
        var step = (outer[1].Angle - outer[0].Angle).Normalized().Deg;
        if (MathF.Abs(MathF.Abs(step) - 45) > 5)
            return null;
        var turn = MathF.Sign(step);
        // The last ray resolves latest; do not suppress either outer danger.
        return (outer[0].Angle - turn * 45f.Degrees(), outer[0].Angle - turn * 90f.Degrees(), outer[0].Angle - turn * 45f.Degrees());
    }
}

static class CrucibleScorpionAI
{
    // ARR hits can precede CastFinishAt by ~0.09s. Leave an additional 0.25s timing margin and
    // 0.35y spatial margin for movement/network sampling; these do not change the rendered shape.
    public static void Avoid(AIHints hints, Components.GenericAOEs.AOEInstance aoe)
    {
        var shape = aoe.Shape switch
        {
            AOEShapeCircle circle => new AOEShapeCircle(circle.Radius + 0.35f),
            AOEShapeDonut donut => new AOEShapeDonut(Math.Max(0, donut.InnerRadius - 0.35f), donut.OuterRadius + 0.35f),
            _ => aoe.Shape
        };
        hints.AddForbiddenZone(shape, aoe.Origin, aoe.Rotation, aoe.Activation == default ? default : aoe.Activation.AddSeconds(-0.25d), aoe.Activation == default ? null : aoe.Activation.AddSeconds(0.6));
    }
}

sealed class CrucibleVoidFlareHint(BossModule module) : Components.CastHints(module, [46893u, 46894u, 46895u], "核爆：尽量远离落点，距离越远伤害越低");

sealed class CrucibleVoidFlare(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == 46895
        ? new(new AOEShapeCircle(100f), LocationTargeted: true) : null;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        // 100y is the damage reach, not a lethal/safe-distance threshold. Do not forbid the whole arena.
        foreach (var pending in Pending)
            pending.AOE.Risky = false;
        return base.ActiveAOEs(slot, actor);
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc) { }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (ref readonly var aoe in ActiveAOEs(pcSlot, pc))
        {
            // Small rings identify the impact point, not a claimed safe radius.
            Arena.ZoneCircleOutline(aoe.Origin, 1f, Colors.Danger, 3f);
            Arena.ZoneCircleOutline(aoe.Origin, 2f, Colors.Danger, 2f);
            Arena.AddLine(pc.Position, aoe.Origin, Colors.Danger);
        }
    }
}
