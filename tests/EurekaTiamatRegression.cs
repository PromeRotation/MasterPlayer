using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class EurekaTiamatRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-300, 0, -300);
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(1101);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x3D9A, Center, InCombat: true, Hp: 100),
            new(2, 0x233C, Center),
            new(100, 0, Center + Vector3.UnitZ * 6, PartySlot: 0, Player: true),
            new(101, 0, Center + Vector3.UnitX * 15, PartySlot: 1, Player: true),
        ];
        internal DateTime Now = Start;
        internal ulong Viewer = 100;
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == Viewer)?.Position, Now, playerId: Viewer);
        internal void Cast(Vector3? location = null)
        {
            Now = Now.AddMilliseconds(10);
            Set(2, a => a with { Cast = new(31847, location ?? a.Position, 0, Now, Now.AddSeconds(5.3), 100, TotalTime: 5) });
            Sync();
        }
        internal void Icon(ulong actor = 100, ulong maskTarget = 100) => E.Icon(maskTarget, 197, Now.AddSeconds(-10), actor);
        internal void Effect(uint action = 31848, ulong target = 0, Vector3? position = null) =>
            E.Effect(2, action, 7, Now, target, position);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("Eureka30 five activated source components square OID and ordinary actual geometry", Catalog);
        run("Eureka30 icon actor identity nearest duplicate targets and source first hit no movement", Icons);
        run("Eureka30 Whei Morn actual effect nearest location current clock five hits repeats no Finish expiry", Chase);
        run("Eureka30 original target references death after first hit removal and same ID reconstruction", References);
        run("Eureka30 first raw authority no duplicate chaser pure packet and cancellation retain history", Raw);
        run("Eureka30 moving heads source filter finite motion and observed body windows", Heads);
        run("Eureka30 own and teammate navigation finite hit windows line preview and owner withdrawal", Navigation);
        run("Eureka30 complete native packet primary target precedence legacy exclusion no target bootstrap", Adapter);
        run("Eureka30 fixed bait destination belongs to marked actor across movement and perspective changes", Bait);
        run("Eureka30 recovery follows actual fifth hit target expires and resets with encounter", Recovery);
        run("Eureka30 head tracking grid windows correction activation removal and rolling horizon", HeadTracking);
        run("Eureka30 ordinary hit windows survive natural raw end and clear cancellation expiry and unload", OrdinaryWindows);
        EurekaTiamatRecordedRegression.Run(run);
    }
    private static void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); }
    private static void Catalog()
    {
        var f = new Fixture(); f.Sync();
        Check(f.E.Active?.BossOid == 0x3D9A && EurekaOrthosCatalog.Owns(1101, 0x3D9A)
            && !EurekaOrthosCatalog.Owns(1101, 0x3D9B), "Ownership expanded to head/Zone.");
        Check(EurekaOrthosCatalog.Create(1101).Single().Casts.Single(c => c.Action == 31846).Shape == RayRectangleTemplate.Symmetric(16, 40)
            && EurekaOrthosCatalog.Create(1101).Single().Casts.Single(c => c.Action == 31850).Shape == new CircleTemplate(6),
            "Source real spell dimensions differ.");
        foreach (var action in new uint[] { 31841, 31842, 31843, 31845, 31849 })
            f.E.StartCast(2, new(action, Center, 0, Start, Start.AddSeconds(3)), Start);
        Check(f.E.Casts.Count == 0, "Unactivated visual became independent AOE.");
    }
    private static void Icons()
    {
        var f = new Fixture(); f.Sync();
        f.E.Icon(100, 197, f.Now, 0);
        f.E.Icon(100, 197, f.Now, 0xE0000000);
        f.Cast();
        Check(f.E.EurekaChasers.Count == 0, "Invalid icon source invented a target from the mask.");
        f.Set(2, a => a with { Cast = null }); f.Sync();
        f.Icon(101, 100); f.Cast(Center + Vector3.UnitX * 15);
        Check(f.E.EurekaChasers.Single().Target.Actor.Id == 101 && f.E.EurekaChasers[0].Remaining == 5,
            "Source icon actor replaced by target mask ID.");
        var c = f.E.EurekaChasers[0];
        Check(c.Cast.Source == 2 && c.Cast.Rule.Action == 31847 && c.Cast.Observed
            && c.MoveDistance == 0 && c.Predicted() == DungeonEncounter.QuantizedDungeonPosition(Center + Vector3.UnitX * 15),
            "First cast moved instead of locking initial location.");
        var nearest = new Fixture(); nearest.Sync(); nearest.Icon(100); nearest.Icon(101); nearest.Icon(100);
        nearest.Cast(Center + Vector3.UnitX * 14);
        Check(nearest.E.EurekaChasers[0].Target.Actor.Id == 101, "Icon targets not selected by nearest current location.");
        nearest.Set(2, a => a with { Cast = null }); nearest.Sync(); nearest.Cast(Center);
        nearest.Set(2, a => a with { Cast = null }); nearest.Sync(); nearest.Cast(Center);
        Check(nearest.E.EurekaChasers.Count == 3 && nearest.E.EurekaChasers.Skip(1).All(a => a.Target.Actor.Id == 100),
            "Duplicate icon reference collapsed.");
    }
    private static void Chase()
    {
        var f = new Fixture(); f.Sync(); f.Icon(); f.Cast();
        var first = f.E.EurekaChasers[0];
        f.Set(2, a => a with { Cast = null }); f.Sync();
        Check(f.E.EurekaChasers.Count == 1, "Cancellation cleared chasing history.");
        f.Now = Start.AddSeconds(100); f.Sync();
        Check(f.E.EurekaChasers.Count == 1, "Chaser expired without effect.");
        f.Effect(31847, position: Center);
        Check(first.Remaining == 4 && first.Previous == Center && first.Activation == f.Now.AddSeconds(2)
            && first.Predicted() == DungeonEncounter.QuantizedDungeonPosition(f.Actors.Single(a => a.Id == 100).Position),
            "Effect failed exact six step or current clock.");
        f.Set(100, a => a with { Position = Center + Vector3.UnitZ * 15 }); f.Sync();
        Check(first.Predicted() == DungeonEncounter.QuantizedDungeonPosition(Center + Vector3.UnitZ * 6),
            "Chaser failed six-unit cap.");
        for (var i = 0; i < 4; ++i) f.Effect(position: Center);
        Check(f.E.EurekaChasers.Count == 0, "Repeated same sequence native effects were deduplicated or failed five-hit removal.");
        f.Cast();
        Check(f.E.EurekaChasers.Count == 0, "Completed wave retained old icon targets.");
        var multi = new Fixture(); multi.Sync(); multi.Icon(100); multi.Cast(Center);
        multi.Set(2, a => a with { Cast = null }); multi.Sync(); multi.Icon(101); multi.Cast(Center + Vector3.UnitX * 15);
        multi.Effect(position: Center + Vector3.UnitX * 14);
        Check(multi.E.EurekaChasers[0].Remaining == 5 && multi.E.EurekaChasers[1].Remaining == 4,
            "Effect consumed FIFO instead of nearest predicted chaser.");
    }
    private static void References()
    {
        var f = new Fixture(); f.Sync(); f.Icon(); f.Cast();
        f.Set(100, a => a with { Dead = true }); f.Sync();
        Check(f.E.EurekaChasers.Count == 1, "Pre-first-hit dead target removed too early.");
        f.Effect(position: Center); f.Sync();
        Check(f.E.EurekaChasers.Count == 0, "Post-first-hit dead target remained.");
        var removed = new Fixture(); removed.Sync(); removed.Icon(); removed.Cast();
        var old = removed.E.EurekaChasers[0].Target;
        removed.Actors.RemoveAll(a => a.Id == 100); removed.Sync();
        removed.Actors.Add(new(100, 0, Center - Vector3.UnitX * 15, PartySlot: 0, Player: true)); removed.Sync();
        Check(ReferenceEquals(removed.E.EurekaChasers[0].Target, old) && old.Removed,
            "Chaser rebound old target to new same-ID object.");
        removed.Effect(position: Center); removed.Sync();
        Check(removed.E.EurekaChasers.Count == 0, "Destroyed original reference failed post-hit cleanup.");
    }
    private static void Raw()
    {
        var f = new Fixture(); f.Sync(); f.Icon();
        var input = new DungeonCastObservation(31847, Center, 0, Start, Start.AddSeconds(5.3), TotalTime: 5);
        f.E.StartCast(2, input with { Location = Center + Vector3.UnitX }, Start);
        Check(f.E.EurekaChasers.Count == 0, "Packet invented source cast activation.");
        f.Set(2, a => a with { Cast = input }); f.Sync(); f.Sync();
        Check(f.E.EurekaChasers.Count == 1, "First raw lost/duplicated chaser.");
        f.E.Finish(2, 31847, Start.AddSeconds(1));
        Check(f.E.EurekaChasers.Count == 1, "Real first Finish removed source chasing history.");
    }
    private static void Heads()
    {
        var f = new Fixture();
        var p = Center + new Vector3(.137f, 0, .231f);
        f.Actors.Add(new(20, 0x3D9B, p, Dead: true, Targetable: false, Rotation: .7f, EventState: 7, AnimationState1: 1));
        f.Actors.Add(new(21, 0x3D9B, p, AnimationState1: 0)); f.Sync();
        var head = f.E.AmbientAoes().Where(a => a.Id.StartsWith("eureka/head/")).ToArray();
        Check(head.Length == 32 && head.Count(a => a.Display) == 1 && head.All(a => !a.Obstacle)
            && head.Single(a => a.Display).Position == p && head.Single(a => a.Display).Shape == new DungeonCapsuleTemplate(2, 6),
            "Head source dead/State/targetable filters or unquantized geometry changed.");
        var body = head.Single(a => a.Id.EndsWith("/body"));
        Check(body.Shape == new CircleTemplate(2) && body.Position == p
            && body.Activation == f.Now && body.ExpiresAt == f.Now.AddSeconds(.2) && body.HasMotionEnvelope,
            "Observed head body lost its finite occupancy window.");
        var future = head.Where(a => a.Id.Contains("/future/")).ToArray();
        Check(future.All(a => a.Activation != null && a.ExpiresAt > a.Activation && a.HasMotionEnvelope
                && a.Shape == new DungeonCapsuleTemplate(2.5f, 1.5f))
            && future[0].Activation == Start.AddSeconds(.2) && future[0].ExpiresAt == Start.AddSeconds(2.4),
            "Moving-head finite windows or trajectory uncertainty differ.");
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        output.Apply(f.E, f.Now, false);
        var snapshot = safe.Manager.BuildSnapshot(f.Now);
        Check(snapshot.DangerAreas.Count == 31 && snapshot.HardObstacles.Count == 0
            && snapshot.DangerAreas.All(a => a.DangerWindows.Single() is { HasMotionEnvelope: true, HasExplicitTiming: true, DangerUntil: not null }),
            "Production output dropped motion metadata or finite expiry.");
        var window = snapshot.DangerAreas[0].DangerWindows.Single();
        Check(window.Buffered(5, 5) == window && !window.IsActiveAt(window.DangerUntil!.Value.AddTicks(1)),
            "Motion window was buffered a second time or remained active after expiry.");
        Check(f.E.Hints(p).Contains("GTFO from voidzone!"), "Source moving-head warning missing.");
        f.Actors.RemoveAll(a => a.Id == 20); f.Sync();
        Check(!f.E.AmbientAoes().Any(a => a.Id.StartsWith("eureka/head/")), "Removed head remained in live enemy list.");
        output.Apply(f.E, f.Now, false);
        Check(safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0
            && safe.Manager.BuildSnapshot(f.Now).HardObstacles.Count == 0, "Removed head retained production areas.");
    }
    private static void Navigation()
    {
        var f = new Fixture(); f.Sync(); f.Icon(); f.Cast();
        var own = f.E.AmbientAoes().Single(a => a.Id.EndsWith("/navigation"));
        Check(own.Shape == new CircleTemplate(6) && own.Position == f.E.EurekaChasers[0].Previous
            && own.RequireOutwardExit && own.ExpiresAt == own.Activation!.Value.AddSeconds(.8),
            "First own hit was not locked to initial circle and finite timing.");
        f.Viewer = 101; f.Sync();
        Check(f.E.AmbientAoes().Single(a => a.Id.EndsWith("/navigation")).Shape == new CircleTemplate(7),
            "Other chaser avoidance radius changed.");
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
        var draw = new ScriptDrawApi(); var output = new DungeonOutput(safe, draw, safe.Manager);
        output.Apply(f.E, f.Now, false);
        Check(draw.Previews.Values.Any(p => p.Id.Contains("eureka/whei/")) && draw.Lines.Count == 1
            && safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 1, "Display line or production danger missing.");
        f.Viewer = 100; f.Sync(); output.Apply(f.E, f.Now, false);
        var snapshot = safe.Manager.BuildSnapshot(f.Now);
        var result = new SafeZoneSolver().Solve(snapshot, new(Center, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
        Check(result.IsSuccess && !result.IsLeastDangerFallback && result.ExecutablePath.Count + result.ContinuationPath.Count > 0,
            "Actual solver could not escape own chase.");
        output.Clear();
        Check(draw.Previews.Count == 0 && draw.Lines.Count == 0 && safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0
            && safe.Manager.BuildSnapshot(f.Now).GoalAreas.Count == 0,
            "Owner withdrawal retained chase output.");
    }
    private static void Bait()
    {
        var f = new Fixture(); f.Sync(); f.Icon(); f.Cast();
        var chase = f.E.EurekaChasers.Single();
        var destination = chase.Destination!.Value;
        var observer = new Fixture { Viewer = 101 }; observer.Sync(); observer.Icon(); observer.Cast();
        Check(observer.E.EurekaChasers.Single().Destination == destination,
            "Initial observer perspective changed the real target's endpoint.");
        Check(f.E.Arena!.Contains(destination, 0) && Vector3.Distance(destination, chase.Previous) >= 14
            && !f.E.AmbientAoes().Any(a => a.Id.EndsWith("/bait")), "Bait moved too early or selected an invalid endpoint.");
        f.Now = chase.Cast.FinishAt.AddSeconds(-2.49); f.Sync();
        Check(f.E.AmbientAoes().Single(a => a.Id.EndsWith("/bait")).Position == destination, "Marked actor lost fixed bait goal.");
        f.Effect(position: Center);
        f.Set(100, a => a with { Position = Center + Vector3.UnitX * 6, Rotation = 1.7f }); f.Sync();
        var predicted = chase.Predicted();
        var own = f.E.AmbientAoes().Single(a => a.Id.EndsWith("/navigation"));
        Check(own.Shape == new CircleTemplate(12.5f) && own.Position == Center && own.RequireOutwardExit
            && f.E.AmbientAoes().Single(a => a.Id.EndsWith("/bait")).Position == destination,
            "Actual hit, target motion or facing changed the fixed endpoint or outward escape.");
        f.Viewer = 101; f.Sync();
        Check(chase.Predicted() == predicted && chase.Destination == destination
            && !f.E.AmbientAoes().Any(a => a.Safe && a.Id.Contains("eureka/")),
            "Observer received bait/recovery goal or changed the real target prediction.");
        f.Viewer = 100; f.Sync();
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
        var draw = new ScriptDrawApi(); var output = new DungeonOutput(safe, draw, safe.Manager);
        output.Apply(f.E, f.Now, false);
        var goal = safe.Manager.BuildSnapshot(f.Now).GoalAreas.Single().GoalWindows.Single();
        Check(goal.GoalUntil == chase.Activation.AddSeconds((chase.Remaining - 1) * 2 + .8)
            && !goal.OverrideDangerMargin, "Bait whole-sequence timing or safety margin lost.");
        f.Viewer = 101; f.Sync(); output.Apply(f.E, f.Now, false);
        Check(safe.Manager.BuildSnapshot(f.Now).GoalAreas.Count == 0 && draw.Lines.Count == 1,
            "Perspective switch retained former player's personal goal.");
        f.Viewer = 100; f.Set(100, a => a with { Dead = true }); f.Sync(); output.Apply(f.E, f.Now, false);
        Check(safe.Manager.BuildSnapshot(f.Now).GoalAreas.Count == 0, "Dead target retained bait movement.");
    }
    private static void Recovery()
    {
        var f = new Fixture(); f.Sync(); f.Icon(); f.Cast();
        for (var i = 0; i < 4; ++i) f.Effect(position: Center);
        Check(!f.E.AmbientAoes().Any(a => a.Id == "eureka/recovery"), "Recovery began before the fifth real hit.");
        f.Effect(position: Center);
        var recovery = f.E.AmbientAoes().Single(a => a.Id == "eureka/recovery");
        Check(recovery is { Safe: true, Display: false, Shape: CircleTemplate { Radius: 8 }, OverrideDangerMargin: false }
            && recovery.Position == Center && recovery.ExpiresAt == f.Now.AddSeconds(10), "Recovery geometry, owner or lifetime differ.");
        f.Viewer = 101; f.Sync();
        Check(!f.E.AmbientAoes().Any(a => a.Id == "eureka/recovery"), "Non-target inherited another player's recovery.");
        f.Viewer = 100; f.Sync();
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        output.Apply(f.E, f.Now, false);
        Check(safe.Manager.BuildSnapshot(f.Now).GoalAreas.Count == 1, "Recovery goal not published.");
        output.Clear(); output.Apply(f.E, f.Now, false);
        Check(safe.Manager.BuildSnapshot(f.Now).GoalAreas.Count == 1, "Output withdrawal erased global recovery history.");
        f.Now = recovery.ExpiresAt!.Value.AddTicks(1); f.Sync(); output.Apply(f.E, f.Now, false);
        Check(safe.Manager.BuildSnapshot(f.Now).GoalAreas.Count == 0, "Recovery survived expiry.");
        f.Set(2, a => a with { Cast = null }); f.Sync(); f.Icon(); f.Cast();
        for (var i = 0; i < 5; ++i) f.Effect(position: Center);
        f.Set(2, a => a with { Cast = null }); f.Sync(); f.Icon(); f.Cast();
        Check(!f.E.AmbientAoes().Any(a => a.Id == "eureka/recovery"), "New chase retained previous recovery.");
        f.E.EurekaDirector(0x40000005); output.Apply(f.E, f.Now, false);
        Check(f.E.EurekaChasers.Count == 0 && f.E.Active == null
            && safe.Manager.BuildSnapshot(f.Now).GoalAreas.Count == 0,
            "Encounter unload retained personal navigation.");
    }
    private static void HeadTracking()
    {
        var f = new Fixture(); f.Actors.Add(new(20, 0x3D9B, Center, AnimationState1: 1)); f.Sync();
        var initial = f.E.AmbientAoes().Where(a => a.Id.Contains("/future/")).ToArray();
        f.Sync();
        Check(f.E.AmbientAoes().Where(a => a.Id.Contains("/future/")).SequenceEqual(initial), "Unchanged raw restarted head clock.");
        f.Now = Start.AddSeconds(2); f.Set(20, a => a with { Position = Center + Vector3.UnitZ * 3 }); f.Sync();
        Check(f.E.AmbientAoes().Where(a => a.Id.Contains("/future/")).Any(a => a.Position == Center),
            "A still-active trailing slice was lost.");
        f.Now = Start.AddSeconds(22); f.Set(20, a => a with { Position = Center + Vector3.UnitZ * 63 }); f.Sync();
        Check(f.E.AmbientAoes().Where(a => a.Id.Contains("/future/")).Any(a => a.Activation > f.Now.AddSeconds(10)),
            "Long-lived head lost rolling prediction after fifteen seconds.");
        f.Set(1, a => a with { Cast = new(31849, Center, 0, f.Now, f.Now.AddSeconds(3)) }); f.Sync();
        var grid = f.E.AmbientAoes().Where(a => a.Id.Contains("/future/")).ToArray();
        Check(grid.Length >= 100 && grid.All(a => a.Shape == new DungeonCapsuleTemplate(2.5f, .45f))
            && grid.All(a => Math.Abs((a.ExpiresAt!.Value - a.Activation!.Value).TotalSeconds - .55) < .00001)
            && f.E.Casts.Count == 0, "Grid windows not refined or visual became an AOE.");
        f.Now = f.Now.AddMilliseconds(100); f.Set(20, a => a with { Position = Center, Rotation = MathF.PI / 2 }); f.Sync();
        Check(f.E.AmbientAoes().Where(a => a.Id.Contains("/future/")).First() is { Position: var p, Rotation: var r }
            && p == Center && r == MathF.PI / 2, "Observed motion correction not applied.");
        f.Set(20, a => a with { AnimationState1 = 0 }); f.Sync();
        Check(!f.E.AmbientAoes().Any(a => a.Id.StartsWith("eureka/head/")), "Inactive model retained trajectory.");
        f.Set(20, a => a with { AnimationState1 = 1 }); f.Sync();
        Check(f.E.AmbientAoes().First(a => a.Id.Contains("/future/")).Activation == f.Now.AddSeconds(.85),
            "Reactivated head inherited old start time.");
        f.E.EurekaDirector(0x40000005);
        Check(!f.E.AmbientAoes().Any(a => a.Id.StartsWith("eureka/head/")), "Wipe retained head prediction.");
    }
    private static void OrdinaryWindows()
    {
        foreach (var action in new uint[] { 31844, 31846, 31850 })
        {
            foreach (var end in new[] { 1d, 3d })
            {
                var f = new Fixture(); f.Sync();
                f.Set(2, a => a with { Cast = new(action, Center, 0, Start, Start.AddSeconds(3.3), TotalTime: 3) });
                f.Sync();
                using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = f.Now };
                var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
                output.Apply(f.E, f.Now, false);
                var original = safe.Manager.BuildSnapshot(f.Now).DangerAreas.Single().DangerWindows.Single();
                Check(original.HasExplicitTiming && original.DangerUntil == original.DangerFrom.AddSeconds(.6),
                    "Ordinary EO30 footprint has an unbounded navigation window.");
                f.Now = Start.AddSeconds(end); f.Set(2, a => a with { Cast = null }); f.Sync();
                output.Apply(f.E, f.Now, false);
                Check(f.E.Casts.Count == 0, "Output retention altered native event history.");
                var retained = safe.Manager.BuildSnapshot(f.Now).DangerAreas;
                Check((end == 3) == (retained.Count == 1), "Early cancellation or natural raw end was misclassified.");
                if (end == 3)
                {
                    Check(retained.Single().DangerWindows.Single() == original, "Retained cast changed its clock.");
                    output.Clear();
                    output.Apply(f.E, f.Now, false);
                    var recreated = safe.Manager.BuildSnapshot(f.Now).DangerAreas.Single().DangerWindows.Single();
                    Check(recreated.HasExplicitTiming && recreated.DangerFrom == original.DangerFrom
                        && recreated.DangerUntil == original.DangerUntil,
                        "Recreating output lost the mechanic's naturally completed hit.");
                    f.Now = original.DangerUntil!.Value.AddTicks(1); f.Sync(); output.Apply(f.E, f.Now, false);
                    Check(safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0, "Retained footprint survived its expiry.");
                }
                output.Clear();
                Check(safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0, "Owner clear retained ordinary footprints.");
            }
            foreach (var change in new[] { "cancel", "dead", "removed", "replacement", "reset" })
            {
                var f = new Fixture(); f.Sync();
                f.Set(2, a => a with { Cast = new(action, Center, 0, Start, Start.AddSeconds(3.3), TotalTime: 3) });
                f.Sync();
                f.Now = Start.AddSeconds(3);
                if (change == "cancel")
                    f.E.Finish(2, action, f.Now, completed: false);
                else
                {
                    f.Set(2, a => a with { Cast = null });
                    f.Sync();
                    switch (change)
                    {
                        case "dead":
                            f.Set(2, a => a with { Dead = true });
                            f.Sync();
                            break;
                        case "removed":
                            f.Actors.RemoveAll(a => a.Id == 2);
                            f.Sync();
                            break;
                        case "replacement":
                            f.Set(2, a => a with { Cast = new(31847, Center + Vector3.UnitX * 10, 0,
                                f.Now, f.Now.AddSeconds(5.3), TotalTime: 5) });
                            f.Sync();
                            break;
                        case "reset":
                            f.E.EurekaDirector(0x40000005);
                            break;
                    }
                }
                Check(!f.E.AmbientAoes().Any(a => a.Id.StartsWith("eureka/hit/")),
                    $"{change} retained EO30's ordinary hit in the mechanic.");
            }
        }
    }
    private static void Adapter()
    {
        var f = new Fixture(); f.Sync(); f.Icon(); f.Cast();
        f.E.Effect(999, 31848, 7, f.Now, 100, Center);
        Check(f.E.EurekaChasers[0].Remaining == 5, "Missing effect source consumed a chase.");
        var adapter = new DungeonEventAdapter(f.E, f.Sync, _ => 0, _ => { });
        adapter.Handle(new NativeActionEffectEvent { SourceId = 2, ActionId = 31847, ActionType = 2,
            MainTargetId = 101, Targets = [] }, Start.AddSeconds(1));
        Check(f.E.EurekaChasers[0].Remaining == 5, "Known non-spell packet consumed a spell mechanism.");
        adapter.Handle(new NativeActionEffectEvent { SourceId = 2, ActionId = 31847, MainTargetId = 101,
            TargetPosition = Center - Vector3.UnitZ * 10, GlobalSequence = 7, Targets = [] }, Start.AddSeconds(1));
        Check(f.E.EurekaChasers[0].Previous == f.Actors.Single(a => a.Id == 101).Position
            && f.E.EurekaChasers[0].Activation == Start.AddSeconds(3), "Native main target world position or consumption clock lost.");
        adapter.Handle(new PromeRotation.LogSystem.LogSystemActionEffectEvent { SourceId = 2, ActionId = 31847,
            TargetId = 101, GlobalSequence = 7 }, Start.AddSeconds(1));
        Check(f.E.EurekaChasers[0].Remaining == 4, "Legacy target expansion duplicated native effect.");
        adapter.Handle(new NativeActionEffectEvent { SourceId = 2, ActionId = 31848, MainTargetId = 999,
            GlobalSequence = 0, Targets = [] }, Start.AddSeconds(2));
        Check(f.E.EurekaChasers[0].Remaining == 4 && f.E.Hints(Center).Any(t => t.Contains("判定待确认")),
            "Old recording without target position fabricated a zero-coordinate hit.");
        f.E.Effect(100, 31848, 7, f.Now, 100, Center);
        Check(f.E.EurekaChasers[0].Remaining == 4, "Player source bypassed source spell classification.");
        foreach (var nativeType in new ushort[] { 0x202, 0x203, 0x209 })
        {
            f.Set(2, a => a with { NativeType = nativeType });
            f.Sync(); f.Effect(position: Center);
            Check(f.E.EurekaChasers[0].Remaining == 4, "Companion source consumed boss mechanics.");
        }
        var playerCast = new Fixture(); playerCast.Sync(); playerCast.Icon();
        playerCast.Set(100, a => a with { Cast = new(31847, Center, 0, Start, Start.AddSeconds(5.3), TotalTime: 5) });
        playerCast.Sync();
        Check(playerCast.E.EurekaChasers.Count == 0, "Player raw spell created a boss chase.");
    }
}
