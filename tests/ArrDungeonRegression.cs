using System.Numerics;
using MasterPlayer.Dodge;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

internal static class ArrDungeonRegression
{
    public static void Run(Action<string, Action> run)
    {
        (uint Territory, uint Boss, uint Caster, uint Action, float Radius, float Angle, float Inner)[] aoes = [
            (1036, 0x19B, 0x19B, 351, 6.15f, 0, 0), (1036, 0x1A1, 0x1A1, 556, 8, 90, 0),
            (1037, 0x237, 0x237, 912, 7, 90, 0), (1037, 0x4C, 0x4C, 987, 9.95f, 0, 0),
            (1038, 0x387C, 0x233C, 28544, 12, 0, 0), (1038, 0x387C, 0x387C, 28546, 30, 60, 0),
            (1038, 0x3870, 0x3870, 28462, 4, 0, 0), (1038, 0x3870, 0x3871, 28465, 8, 0, 0),
            (1038, 0x38C9, 0x38C9, 28762, 40, 0, 4), (1038, 0x38C9, 0x38C9, 28760, 10, 0, 0),
            (1038, 0x38C9, 0x233C, 28761, 8, 0, 0), (1038, 0x38C9, 0x38C9, 28763, 40, 60, 0)];
        foreach (var (territory, boss, caster, action, radius, angle, inner) in aoes)
            run($"ARR dungeons AID {action}: BMR dimensions, locked location and absolute timing", () =>
            {
                using var f = new Fight(territory, boss);
                var source = caster == boss ? 1UL : f.Spawn(caster);
                var location = f.Player + new Vector3(2, 0, 1);
                f.Cast(source, action, location, MathF.PI / 2, 5);
                var draw = f.Draw.Previews.Single().Value.Geometry;
                Check(Vector3.Distance(draw.Position, location) < 0.001f, "AOE must use packet location, not caster origin.");
                Check(draw.Contains(location + new Vector3(radius - 0.01f, 0, 0)), "Point just inside the BMR radius was lost.");
                Check(!draw.Contains(location + new Vector3(radius + 0.01f, 0, 0)), "Display radius was inflated or hitbox counted twice.");
                if (angle != 0)
                {
                    var half = angle * MathF.PI / 360;
                    Check(draw.Contains(location + Direction(MathF.PI / 2 + half - 0.01f) * 3), "Cone must include its full BMR half-angle.");
                    Check(!draw.Contains(location + Direction(MathF.PI / 2 + half + 0.01f) * 3), "Cone half-angle was doubled twice.");
                    Check(!draw.Contains(location - Vector3.UnitX * 3), "Cone used actor facing instead of cast rotation.");
                }
                if (inner != 0)
                {
                    Check(!draw.Contains(location + Vector3.UnitX * (inner - 0.01f)), "Donut hole is missing.");
                    Check(draw.Contains(location + Vector3.UnitX * (inner + 0.01f)), "Donut inner radius changed.");
                }
                var danger = f.Snapshot.DangerAreas.Single();
                Check(danger.DangerWindows.Single().DangerFrom == f.Now.AddSeconds(5.3), "NPC padding must be exactly 0.3 seconds.");
                Check(danger.Geometry.Contains(location + Vector3.UnitX * (radius + 0.45f)), "Configured navigation margin is missing.");
                Check(f.Safe.NavigationOnly.Contains(danger.Handle), "Navigation copy must not draw over true geometry.");
                Check(AoeShapeDatabase.TryGetShape(action, 20, out var fallback), "Missing generic fallback.");
                var fallbackShape = AoeShapeDatabase.CreateTemplate(fallback!)!.Resolve(location, MathF.PI / 2, null);
                Check(!fallbackShape.Contains(location + Vector3.UnitX * (radius + 0.01f)), "Fallback adds caster hitbox twice.");
                Check(fallback!.ExtraDelaySeconds == 0.3f && !fallback.FollowCaster, "Fallback lost BMR snapshot semantics.");
                Check(ArrDungeonCatalog.ClaimedActions(f.Engine.Definitions).Contains(action), "Generic dodge would duplicate this cast.");
                f.Actors[source] = f.Actors[source] with { Position = location + new Vector3(15, 0, 15) };
                f.Scan();
                Check(f.Draw.Previews.Single().Value.Geometry.Position == location, "Moving caster moved the locked AOE.");
            });

        foreach (var (boss, action, hint) in new[] { (0x387Cu, 28545u, "死刑"), (0x387Cu, 28547u, "单体伤害"), (0x3870u, 28461u, "死刑"), (0x38C9u, 28764u, "死刑") })
            run($"ARR dungeons AID {action}: single target hint never becomes spatial danger", () =>
            {
                using var f = new Fight(1038, boss);
                f.Cast(1, action, f.Player, null);
                Check(f.Engine.Hints(f.Player).Contains(hint), "BMR cast hint was dropped.");
                Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Tankbuster was fabricated as an AOE.");
                f.Engine.Finish(1, action); f.Render();
                Check(!f.Engine.Hints(f.Player).Contains(hint), "Cancelled single target hint leaked.");
            });

        run("ARR dungeons visual Divide and jump actions do not predict invented damage", () =>
        {
            using var f = new Fight(1038, 0x3870);
            f.Cast(1, 28463, f.Player);
            Check(f.Engine.Casts.Count == 0 && f.Draw.Previews.Count == 0, "Divide visual was mistaken for Burst.");
            using var k = new Fight(1038, 0x387C);
            k.Cast(1, 28543, k.Player);
            Check(k.Engine.Casts.Count == 0, "Jump visual was mistaken for helper AOE.");
        });
        run("ARR dungeons concurrent helpers and duplicate packet correction", () =>
        {
            using var f = new Fight(1038, 0x387C);
            var a = f.Spawn(0x233C); var b = f.Spawn(0x233C);
            f.Cast(a, 28544, f.Player);
            f.Cast(b, 28544, f.Player + Vector3.UnitX * 5);
            var deadline = f.Engine.Casts[a].FinishAt;
            f.Engine.StartCast(a, new(28544, f.Player, 1, f.Now.AddSeconds(0.2), deadline.AddSeconds(0.2)), f.Now);
            f.Render();
            Check(f.Snapshot.DangerAreas.Count == 2 && f.Engine.Casts[a].FinishAt == deadline, "Duplicate packet duplicated or delayed a helper.");
            f.Engine.Finish(a, 28544); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 1 && f.Engine.Casts.ContainsKey(b), "Cancelling one helper removed another.");
        });
        run("ARR dungeons missing cone rotation fails visibly and never substitutes facing", () =>
        {
            using var f = new Fight(1037, 0x237);
            f.Cast(1, 912, f.Player, null);
            f.Scan();
            Check(f.Errors.Count == 1 && f.Engine.Casts.Count == 0 && f.Draw.Previews.Count == 0, "Unknown rotation produced a false cone or repeated errors.");
            f.Cast(1, 912, f.Player, 0);
            Check(f.Draw.Previews.Count == 1, "A later valid packet did not restore the cast.");
        });
        run("ARR dungeons packet corrects scanned cone without shifting deadline", () =>
        {
            using var f = new Fight(1038, 0x38C9);
            f.Cast(1, 28763, f.Player, 0);
            var end = f.Engine.Casts[1].FinishAt;
            f.Engine.StartCast(1, new(28763, f.Player, MathF.PI, f.Now, end.AddSeconds(0.2)), f.Now);
            f.Render();
            Check(f.Engine.Casts[1].FinishAt == end && f.Draw.Previews.Single().Value.Geometry.Contains(f.Player - Vector3.UnitZ * 10), "Cast rotation correction failed.");
        });
        run("ARR dungeons cancellation is action scoped and stale scan cannot resurrect it", () =>
        {
            using var f = new Fight(1038, 0x3870);
            f.Cast(1, 28462, f.Player);
            f.Engine.Finish(1, 28461); f.Render();
            Check(f.Engine.Casts.Count == 1, "Unrelated interrupt removed current cast.");
            f.Engine.Finish(1, 28462); f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Stale native cast revived cancellation.");
            f.Actors[1] = f.Actors[1] with { Cast = null }; f.Scan();
            f.Engine.StartCast(1, new(28462, f.Player, 0, f.Now, f.Now.AddSeconds(4.3)), f.Now);
            Check(f.Engine.Casts.Count == 0, "Late duplicate revived cancelled cast after native state cleared.");
            f.Now = f.Now.AddSeconds(5); f.Cast(1, 28462, f.Player);
            Check(f.Draw.Previews.Count == 1, "Cancellation latch blocked a later distinct cast.");
        });
        run("ARR dungeons effects deduplicate targets and normal finish removes once", () =>
        {
            using var f = new Fight(1036, 0x19B);
            f.Cast(1, 351, f.Player);
            for (var i = 0; i < 8; ++i) f.Engine.Effect(1, 351, 123, f.Now);
            Check(f.Engine.Casts.Count == 1 && f.Engine.Casts[1].EffectSeen, "Effects duplicated or prematurely removed SimpleAOEs.");
            f.Now = f.Now.AddSeconds(4.3); f.Actors[1] = f.Actors[1] with { Cast = null }; f.Scan();
            Check(f.Engine.Casts.Count == 0 && f.Draw.Previews.Count == 0, "Finished cast leaked.");
        });
        foreach (var delay in new[] { 0.05, 0.5 })
            run($"ARR dungeons same-action recast {delay:F2}s after cancellation is distinct", () =>
            {
                using var f = new Fight(1038, 0x3870);
                f.Cast(1, 28462, f.Player);
                f.Now = f.Now.AddSeconds(0.1); f.Engine.Tick(f.Now);
                f.Engine.Finish(1, 28462); f.Scan();
                Check(f.Engine.Casts.Count == 0, "Native scan resurrected the cancelled cast.");
                f.Now = f.Now.AddSeconds(delay); f.Cast(1, 28462, f.Player + Vector3.UnitX);
                Check(f.Engine.Casts.Count == 1 && f.Draw.Previews.Single().Value.Geometry.Position == f.Player + Vector3.UnitX,
                    "Cancellation suppressed a distinct quick recast.");
            });
        run("ARR dungeons PR cast timestamp compensates dispatch delay and converts local time", () =>
        {
            using var f = new Fight(1038, 0x38C9);
            var started = f.Now;
            f.Now = started.AddSeconds(1.25); f.PacketRotation = MathF.PI / 2;
            var location = f.Player + Vector3.UnitX * 2;
            f.Event(new LogSystemCastStartEvent { SourceId = 1, ActionId = 28763, CastTime = 4, Timestamp = started.ToLocalTime(), EffectPosition = location });
            var cast = f.Engine.Casts[1];
            Check(cast.StartedAt == started && Math.Abs((cast.FinishAt - started).TotalSeconds - 4.3) < 0.001,
                "PR event dispatch delay or local timezone shifted the deadline.");
            Check(f.Draw.Previews.Single().Value.Geometry.Contains(location + Vector3.UnitX * 10), "Adapter lost injected locked cast rotation.");
            f.Now = started.AddSeconds(4.1);
            for (var i = 0; i < 8; ++i)
                f.Event(new LogSystemActionEffectEvent { SourceId = 1, ActionId = 28763, GlobalSequence = 42, TargetIndex = i, Timestamp = started.AddSeconds(4) });
            Check(f.Engine.Casts.Count == 1 && f.Engine.Casts[1].EffectSeen, "Per-target PR effects duplicated or prematurely removed the cast.");
        });
        foreach (var interrupt in new[] { false, true })
            run($"ARR dungeons delayed PR {(interrupt ? "interrupt" : "cancel")} uses event time and protects the next cast", () =>
            {
                using var f = new Fight(1038, 0x3870);
                var started = f.Now;
                var packet = new LogSystemCastStartEvent { SourceId = 1, ActionId = 28462, CastTime = 4, Timestamp = started, EffectPosition = f.Player };
                f.Event(packet);
                var stale = new DungeonCastObservation(28462, f.Player, 0, started, started.AddSeconds(4.3));
                f.Now = started.AddSeconds(1);
                LogSystemEvent cancelled = interrupt
                    ? new LogSystemInterruptCastEvent { SourceId = 1, ActionId = 28462, Timestamp = started.AddSeconds(0.1) }
                    : new LogSystemCancelCastEvent { SourceId = 1, ActionId = 28462, Timestamp = started.AddSeconds(0.1) };
                f.Event(cancelled);
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "PR cancellation left owned output.");
                f.Event(packet);
                Check(f.Engine.Casts.Count == 0, "Duplicate start revived the cancelled cast.");
                f.Event(packet with { Timestamp = started.AddSeconds(0.15), EffectPosition = f.Player + Vector3.UnitX });
                Check(f.Engine.Casts.Count == 1, "Dispatch-time cancellation blocked the next real cast.");
                f.Event(packet);
                Check(f.Draw.Previews.Single().Value.Geometry.Position == f.Player + Vector3.UnitX, "Late old packet overwrote the next cast location.");
                f.Event(cancelled);
                Check(f.Engine.Casts.Count == 1, "Late cancellation removed a newer same-action cast.");
                f.Actors[1] = f.Actors[1] with { Cast = stale }; f.Scan();
                Check(f.Engine.Casts[1].StartedAt == started.AddSeconds(0.15)
                    && f.Draw.Previews.Single().Value.Geometry.Position == f.Player + Vector3.UnitX, "Stale native scan replaced a newer packet.");
            });
        foreach (var cleanup in new[] { "normal scan", "effect scan", "timeout" })
            run($"ARR dungeons {cleanup} does not reject a delayed next same-action packet", () =>
            {
                using var f = new Fight(1038, 0x3870);
                var started = f.Now;
                f.Cast(1, 28462, f.Player);
                if (cleanup != "timeout")
                {
                    f.Now = started.AddSeconds(cleanup == "effect scan" ? 4.1 : 4.5);
                    if (cleanup == "effect scan") f.Engine.Effect(1, 28462, 42, f.Now);
                    f.Actors[1] = f.Actors[1] with { Cast = null }; f.Scan();
                    Check(f.Engine.Casts.Count == 0, "Completed cast remained registered.");
                }
                var nextStarted = started.AddSeconds(cleanup == "timeout" ? 6.4 : cleanup == "effect scan" ? 4.05 : 4.4);
                f.Now = nextStarted.AddSeconds(0.2);
                f.Event(new LogSystemCastStartEvent { SourceId = 1, ActionId = 28462, CastTime = 4, Timestamp = nextStarted, EffectPosition = f.Player + Vector3.UnitX });
                Check(f.Engine.Casts.Count == 1 && f.Engine.Casts[1].StartedAt == nextStarted && f.Draw.Previews.Count == 1,
                    "Cleanup processing time suppressed the next real same-action cast.");
            });
        run("ARR dungeons stale scan cannot overwrite a shorter same-action recast", () =>
        {
            using var f = new Fight(1038, 0x3870);
            var started = f.Now;
            f.Cast(1, 28462, f.Player);
            f.Now = started.AddSeconds(0.1);
            f.Event(new LogSystemCancelCastEvent { SourceId = 1, ActionId = 28462, Timestamp = f.Now });
            f.Now = started.AddSeconds(0.15);
            f.Event(new LogSystemCastStartEvent { SourceId = 1, ActionId = 28462, CastTime = 1, Timestamp = f.Now, EffectPosition = f.Player + Vector3.UnitX });
            f.Scan();
            Check(f.Engine.Casts[1].StartedAt == started.AddSeconds(0.15) && f.Engine.Casts[1].FinishAt < started.AddSeconds(2)
                && f.Draw.Previews.Single().Value.Geometry.Position == f.Player + Vector3.UnitX,
                "Cancelled native cast with a different duration overwrote the new packet.");
        });
        run("ARR dungeons PR adapter rescans new helpers and removal clears only the source", () =>
        {
            using var f = new Fight(1038, 0x387C);
            f.Cast(1, 28546, f.Player);
            f.Actors[10] = new(10, 0x233C, f.Player);
            f.Event(new LogSystemCastStartEvent { SourceId = 10, ActionId = 28544, CastTime = 5, Timestamp = f.Now, EffectPosition = f.Player });
            Check(f.Engine.Casts.Count == 2 && f.Draw.Previews.Count == 2, "New helper packet was lost before the next scheduled scan.");
            f.Event(new LogSystemObjectRemoveEvent { SourceId = 10, Timestamp = f.Now });
            Check(f.Engine.Casts.Count == 1 && f.Engine.Casts.ContainsKey(1) && f.Draw.Previews.Count == 1, "Helper removal affected another caster.");
            f.Event(new LogSystemObjectRemoveEvent { SourceId = 1, Timestamp = f.Now });
            Check(f.Engine.Active == null && f.Snapshot.ArenaBoundary == null && f.Draw.Previews.Count == 0, "Primary removal leaked encounter state.");
        });
        run("ARR dungeons replacing a cast rejects stale scans even when actions change quickly", () =>
        {
            using var f = new Fight(1038, 0x3870);
            f.Cast(1, 28462, f.Player);
            var previous = f.Actors[1].Cast;
            f.Now = f.Now.AddSeconds(0.1);
            f.Event(new LogSystemCastStartEvent { SourceId = 1, ActionId = 28461, CastTime = 4, Timestamp = f.Now, EffectPosition = f.Player });
            Check(f.Engine.Casts[1].Rule.Action == 28461 && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0,
                "Tankbuster replacement left the previous spatial danger.");
            f.Actors[1] = f.Actors[1] with { Cast = previous }; f.Scan();
            Check(f.Engine.Casts[1].Rule.Action == 28461 && f.Draw.Previews.Count == 0, "Stale native action replaced the newer packet.");
        });
        run("ARR dungeons PR EAnim uses high word and discovers newly visible ripples", () =>
        {
            using var f = new Fight(1036, 0x1A1);
            f.Actors[10] = new(10, 0x1E8615, f.Player);
            f.Event(new LogSystemEObjAnimationEvent { SourceId = 10, Param1 = 4, Param2 = 0, Timestamp = f.Now });
            Check(f.Draw.Previews.Count == 1 && f.Engine.ActiveRipples.Single().Id == 10, "EAnim high word or object rescan lost the ripple.");
            f.Event(new LogSystemEObjAnimationEvent { SourceId = 10, Param1 = 0, Param2 = 4, Timestamp = f.Now });
            Check(f.Draw.Previews.Count == 0, "EAnim low word was mistaken for the spawn bit.");
        });
        run("ARR dungeons PR invalid durations fail visibly without registering danger", () =>
        {
            using var f = new Fight(1038, 0x3870);
            foreach (var duration in new[] { float.NaN, float.PositiveInfinity, -1f, 3601f })
                f.Event(new LogSystemCastStartEvent { SourceId = 1, ActionId = 28462, CastTime = duration, Timestamp = f.Now, EffectPosition = f.Player });
            Check(f.Errors.Count == 4 && f.Engine.Casts.Count == 0 && f.Draw.Previews.Count == 0, "Invalid PR duration created a hazard or silently failed.");
        });
        run("ARR dungeons timeout and stale late packet cannot recreate expired preview", () =>
        {
            using var f = new Fight(1036, 0x19B);
            f.Cast(1, 351, f.Player);
            var observation = f.Actors[1].Cast!;
            f.Now = f.Now.AddSeconds(7); f.Engine.Tick(f.Now); f.Render();
            f.Engine.StartCast(1, observation, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Expired packet recreated a hazard.");
        });
        foreach (var mode in new[] { "dead", "destroyed", "out of combat", "disabled", "player absent" })
            run($"ARR dungeons lifecycle: {mode} clears owned state", () =>
            {
                using var f = new Fight(1036, 0x19B);
                f.Cast(1, 351, f.Player);
                if (mode == "dead") f.Actors[1] = f.Actors[1] with { Dead = true };
                if (mode == "destroyed") f.Actors.Remove(1);
                if (mode == "out of combat") f.Actors[1] = f.Actors[1] with { InCombat = false };
                f.Engine.Synchronize(f.Actors.Values, mode == "player absent" ? null : f.Player, f.Now, _ => mode != "disabled");
                f.Render();
                Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0 && f.Snapshot.ArenaBoundary == null, "Lifecycle left an owned resource.");
            });
        run("ARR dungeons inactive or untargetable boss cannot lock dungeon traversal", () =>
        {
            using var f = new Fight(1036, 0x19B);
            f.Engine.ResetFight();
            f.Actors[1] = f.Actors[1] with { InCombat = false }; f.Scan();
            Check(f.Snapshot.ArenaBoundary == null, "Out-of-combat boss activated boundary.");
            f.Actors[1] = f.Actors[1] with { InCombat = true, Targetable = false }; f.Scan();
            Check(f.Snapshot.ArenaBoundary == null, "Untargetable boss activated encounter.");
        });
        run("ARR dungeons stable updates preserve handles and foreign boundary ownership", () =>
        {
            using var f = new Fight(1038, 0x3870);
            f.Cast(1, 28462, f.Player);
            var handle = f.Snapshot.DangerAreas.Single().Handle;
            var version = f.Safe.Manager.Version;
            for (var i = 0; i < 10; ++i) { f.Scan(); f.Render(); }
            Check(f.Snapshot.DangerAreas.Single().Handle == handle && f.Safe.Manager.Version == version, "Stable frames churn solver versions.");
            var foreign = new CircleGeometry(Vector3.Zero, 20);
            f.Safe.Manager.SetArenaBoundary(foreign); f.Output.Clear(); f.Output.Clear();
            Check(ReferenceEquals(f.Snapshot.ArenaBoundary, foreign), "Cleanup removed another owner's boundary.");
        });
        run("ARR dungeons water ripples use animation bit, exact outline and no navigation", () =>
        {
            using var f = new Fight(1036, 0x1A1);
            var ripple = f.Spawn(0x1E8615);
            f.Engine.Animation(ripple, 0x00040004); f.Render();
            var preview = f.Draw.Previews.Single().Value;
            var shape = (RectangleGeometry)preview.Geometry;
            Check(preview.OutlineOnly && shape.Width == 4 && shape.Length == 6 && shape.Position == f.Actors[ripple].Position, "Ripple rectangle differs from BMR.");
            Check(f.Engine.Hints(f.Player).Contains("小怪即将出现") && f.Snapshot.DangerAreas.Count == 0 && f.Snapshot.GoalAreas.Count == 0, "Ripple became navigation or lost hint.");
            f.Output.Apply(f.Engine, f.Now, false);
            Check(f.Draw.Previews.Count == 0, "Ripple toggle failed.");
            f.Engine.Animation(ripple, 0x00080008); f.Render();
            Check(f.Draw.Previews.Count == 0, "Spawned ripple did not clear.");
            f.Engine.Animation(ripple, 0x00040000); f.Actors.Remove(ripple); f.Scan();
            Check(!f.Engine.ActiveRipples.Any(), "Removed ripple leaked.");
        });
        foreach (var (oid, color, label) in new[] { (0x1E8554u, "Blue", "蓝"), (0x1E8A8Cu, "Red", "红"), (0x1E8A8Du, "Green", "绿") })
            run($"ARR dungeons Sastasha zone: {color} memo and resolved switch", () =>
            {
                var e = new DungeonEncounter(1036);
                e.Synchronize([], Vector3.Zero, DateTime.UtcNow);
                Check(e.Hints(null).Single() == "正确开关：未知", "Unknown hint missing.");
                e.Synchronize([new(1, oid, Vector3.Zero, Targetable: false)], Vector3.Zero, DateTime.UtcNow);
                Check(e.CoralColor == "Unknown", "Untargetable memo revealed wrong switch.");
                e.Synchronize([new(1, oid, Vector3.Zero)], Vector3.Zero, DateTime.UtcNow);
                Check(e.CoralColor == color && e.Hints(null).Single() == "正确开关：" + label, "Memo color incorrect.");
                e.ResetFight();
                Check(e.CoralColor == color, "Boss reset discarded zone knowledge.");
                e.Synchronize([new(2, 0x1E8558, Vector3.Zero)], Vector3.Zero, DateTime.UtcNow);
                Check(e.CoralColor == "Resolved" && !e.Hints(null).Any(), "Resolved switch kept prompting.");
            });
        run("ARR dungeons living untargetable imp still produces BMR hint", () =>
        {
            using var f = new Fight(1037, 0x4C);
            var imp = f.Spawn(0x7D);
            f.Actors[imp] = f.Actors[imp] with { Targetable = false }; f.Scan();
            Check(f.Engine.Hints(f.Player).Contains("先击杀小恶魔"), "Hint incorrectly requires targetability.");
            f.Actors[imp] = f.Actors[imp] with { Dead = true }; f.Scan();
            Check(!f.Engine.Hints(f.Player).Contains("先击杀小恶魔"), "Dead imp kept hint alive.");
        });
        run("ARR dungeons TamTara selects three arenas with strict X thresholds", () =>
        {
            var definition = ArrDungeonCatalog.Create(1037).Single(d => d.BossOid == 0x237);
            foreach (var x in new[] { 0f, -20f, -100f, -10f, -15f, -25f })
            {
                var arena = definition.Arena(new(x, 0, 0));
                var bounds = arena.GetBounds();
                Check(x == 0 ? bounds.MinX > -20 : x == -20 ? bounds.MinX > -40 && bounds.MaxZ > 40 : bounds.MinX < -120, "Wrong arena at X=" + x);
            }
            using var f = new Fight(1037, 0x237);
            var first = f.Engine.Arena;
            f.Actors[1] = f.Actors[1] with { Position = new(-20, 0, 20) }; f.Scan();
            Check(ReferenceEquals(first, f.Engine.Arena), "Moving boss changed its instance's arena.");
            f.Actors.Remove(1); f.Scan();
            f.Actors[99] = new(99, 0x237, new(-20, 0, 20), InCombat: true); f.Scan();
            Check(!ReferenceEquals(first, f.Engine.Arena) && f.Engine.Arena!.GetBounds().MaxZ > 40, "Next instance reused the previous arena.");
        });
        run("ARR dungeons exported Copperbell arenas preserve subtraction notches", () =>
        {
            var k = ArrDungeonArenas.Kottos(0);
            Check(k.Contains(new(43, 0, -90)) && !k.Contains(new(43, 0, -104.2f)), "Kottos entrance cutout lost.");
            Check(!k.Contains(new(39.5f, 0, -75.5f)) && !k.Contains(new(47.5f, 0, -75.5f)), "Kottos pillar notches filled.");
            var ire = ArrDungeonArenas.Ichorous(0);
            Check(ire.Contains(new(27, 0, 114)), "Ire center lost.");
            Check(!ire.Contains(new(36.4f, 0, 97)) && !ire.Contains(new(7.7f, 0, 114)), "Ire rotated cutouts filled.");
            Check(k.GetContours().Count == 1 && ire.GetContours().Count == 1, "Unexpected disconnected arena.");
        });
        foreach (var create in new Func<float, IGeometryShape>[] { ArrDungeonArenas.Chopper, ArrDungeonArenas.Denn,
            ArrDungeonArenas.VoidFirst, ArrDungeonArenas.VoidSecond, ArrDungeonArenas.VoidThird, ArrDungeonArenas.Kottos, ArrDungeonArenas.Ichorous })
            run($"ARR dungeons {create.Method.Name} radar origin preserves BMR bounding-box center", () =>
            {
                var arena = create(13);
                var center = arena.GetBounds().Center;
                Check(Vector2.Distance(new(arena.Position.X, arena.Position.Z), center) < 0.001f, "Radar uses polygon centroid instead of BMR bounds center.");
                Check(arena.Position.Y == 13 && arena.GetContours().SelectMany(p => p).All(p => p.Y == 13), "Arena height changed during coordinate conversion.");
            });
        run("ARR dungeons wrong caster OID and unknown territory remain inactive", () =>
        {
            using var f = new Fight(1038, 0x387C);
            f.Cast(1, 28544, f.Player);
            Check(f.Engine.Casts.Count == 0, "Helper-only cast accepted on boss.");
            var e = new DungeonEncounter(1342);
            e.Synchronize([new(1, 0x387C, Vector3.Zero, InCombat: true)], Vector3.Zero, DateTime.UtcNow);
            Check(e.Active == null && e.Definitions.Length == 0, "Dungeon runtime entered Crucible.");
        });
    }

    private static Vector3 Direction(float angle) => new(MathF.Sin(angle), 0, MathF.Cos(angle));
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class Fight : IDisposable
    {
        public DateTime Now = DateTime.UtcNow;
        public readonly Dictionary<ulong, DungeonActor> Actors = [];
        public readonly List<string> Errors = [];
        public readonly DungeonEncounter Engine;
        public readonly ScriptSafeZoneApi Safe = new() { DangerMarginForTest = 0.5f };
        public readonly ScriptDrawApi Draw = new();
        public readonly DungeonOutput Output;
        public readonly DungeonEventAdapter Events;
        public float? PacketRotation;
        public Vector3 Player;
        private ulong _next = 10;
        public Fight(uint territory, uint boss)
        {
            Player = boss switch { 0x19B => new(75, 0, -48), 0x1A1 => new(-320, 0, 320), 0x237 => new(0, 0, -16), 0x4C => new(-52.765f, 0, -12.789f), 0x387C => new(43, 0, -89.8f), 0x3870 => new(27, 0, 114), _ => new(-100, 0, 6) };
            Actors[1] = new(1, boss, Player, InCombat: true);
            Engine = new(territory, Errors.Add);
            Output = new(Safe, Draw, Safe.Manager);
            Events = new(Engine, Scan, _ => PacketRotation, Errors.Add);
            Scan();
        }
        public ulong Spawn(uint oid)
        {
            var id = _next++;
            Actors[id] = new(id, oid, Player, InCombat: true);
            Scan();
            return id;
        }
        public void Cast(ulong source, uint action, Vector3 location, float? rotation = 0, double duration = 4)
        {
            var cast = new DungeonCastObservation(action, location, rotation, Now, Now.AddSeconds(duration + 0.3));
            Actors[source] = Actors[source] with { Cast = cast };
            Engine.StartCast(source, cast, Now);
            Render();
        }
        public void Scan() { Engine.Synchronize(Actors.Values, Player, Now); Render(); }
        public void Event(LogSystemEvent ev) { Events.Handle(ev, Now); Render(); }
        public void Render() => Output.Apply(Engine, Now, true);
        public SafeZoneSnapshot Snapshot => Safe.Manager.BuildSnapshot(Now);
        public void Dispose() { Output.Clear(); Safe.Dispose(); }
    }
}
