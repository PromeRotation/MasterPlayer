using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Source = BossMod.Stormblood.Dungeon.D05CastrumAbania;

internal static class CastrumAbaniaParityRegression
{
    private static readonly DateTime Start = new(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc);
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (type, oid) in new (Type, uint)[]
        {
            (typeof(Source.D051MagnaRoader.D051MagnaRoader), 0x1AA9),
            (typeof(Source.D052SubjectNumberXXIV.D052SubjectNumberXXIV), 0x3F3B),
            (typeof(Source.D053Inferno.D053Inferno), 0x1AAE),
        })
            run($"Castrum Abania exact {type.Name} source arena in both views", () =>
            {
                var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var native = CastrumAbaniaArena.For(oid, 0); var host = HostDrawingGeometry.Convert(native); var bounds = native.GetBounds();
                for (var x = bounds.MinX - 1.123f; x < bounds.MaxX + 1; x += .37f)
                    for (var z = bounds.MinZ - 1.241f; z < bounds.MaxZ + 1; z += .37f)
                    {
                        var p = new Vector3(x, 0, z); var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                        Check(native.Contains(p) == expected && host.Contains(p) == expected, $"Arena differs at {p}.");
                    }
            });
        run("Castrum Abania Haywire persists past natural finish and advances four real effects", () =>
        {
            var center = Center(0x1AA9); var e = Prepare(0x1AA9, out var actors);
            for (ulong i = 10; i < 14; ++i)
            {
                actors.Add(Actor(i, 0x18D6, center + Vector3.UnitX * (i - 10)));
                e.Synchronize(actors, Start);
                e.StartCast(i, 0x18D6, 7960, actors[^1].Position, 0, Start.AddSeconds((double)i - 10), Start.AddSeconds(6.8 + i - 10), 0);
            }
            e.Synchronize(actors, Start.AddSeconds(12));
            Check(e.Collect(Start.AddSeconds(12), 100).Count(a => a.Action == 7960) == 4, "Predicted finish discarded Haywire.");
            var first = e.Collect(Start, 100).First(a => a.Action == 7960);
            Check(Math.Abs((first.From - Start.AddSeconds(6.6)).TotalMilliseconds) < 1 && first.Window.DangerUntil == null, "Haywire source -0.2 timing/window differs.");
            for (uint i = 1; i <= 4; ++i)
            {
                var effect = new LogSystemActionEffectEvent { ActionId = 8184, SourceId = 20, GlobalSequence = i };
                e.Apply(effect, Start.AddSeconds(12)); e.Apply(effect, Start.AddSeconds(12));
                Check(e.Collect(Start.AddSeconds(12), 100).Count(a => a.Action == 7960) == 4 - i, "Haywire duplicate/advance differs.");
            }
            Check(!e.HaywireActive, "Final Haywire kept cannon phase active.");
        });
        run("Castrum Abania Haywire fetters clears all and stale helper cannot rearm", () =>
        {
            var e = Prepare(0x1AA9, out var actors); var center = Center(0x1AA9);
            var helper = Actor(10, 0x18D6, center) with { Cast = new(7960, 0, center, 0, Start, Start.AddSeconds(6.8), false, false) };
            actors.Add(helper); e.Synchronize(actors, Start);
            e.Status(new LogSystemStatusAddEvent { TargetId = 1, StatusId = 1399 });
            e.Synchronize(actors, Start.AddSeconds(.1));
            Check(!e.HaywireActive && e.Collect(Start, 100).Count == 0, "Fetters did not clear full queue.");
        });
        run("Castrum Abania cannon choices ban mounted combat and restore add priority", () =>
        {
            var e = Prepare(0x1AA9, out var actors); var center = Center(0x1AA9);
            actors.Add(Actor(10, 0x18D6, center)); actors.Add(Actor(11, 0x1AAA, center)); actors.Add(Actor(12, 0x1AAC, center + Vector3.UnitX));
            e.Synchronize(actors, Start); e.StartCast(10, 0x18D6, 7960, center, 0, Start, Start.AddSeconds(6.8), 0);
            Check(e.CannonTarget(center)?.Id == 12 && e.PriorityTarget(1, center) == 12 && e.ForbiddenTargets.Length == 0, "Available cannon not selected.");
            var areas = e.Collect(Start, 100);
            Check(areas.Any(a => a.Key == "cannon/12/display" && a.PreviewOnly && a.OutlineOnly
                && a.Geometry.Contains(center + Vector3.UnitX * 3.9f)), "R3 cannon outline missing.");
            actors[^1] = actors[^1] with { Targetable = false }; e.Synchronize(actors, Start.AddSeconds(1));
            Check(e.PriorityTarget(1, center) == 0 && e.ForbiddenTargets.Contains(1UL) && e.ForbiddenTargets.Contains(11UL), "Mounted phase failed to prevent target attacks.");
            e.Status(new LogSystemStatusAddEvent { TargetId = 1, StatusId = 1399 });
            Check(e.ForbiddenTargets.Length == 0 && e.PriorityTarget(1, center) == 11, "Post-cannon add priority did not resume.");
        });
        run("Castrum Abania cannon interaction range rate and execution gates", () =>
        {
            var e = Prepare(0x1AA9, out var actors); var center = Center(0x1AA9);
            actors.Add(Actor(10, 0x18D6, center)); actors.Add(Actor(11, 0x1AAC, center));
            e.Synchronize(actors, Start); e.StartCast(10, 0x18D6, 7960, center, 0, Start, Start.AddSeconds(6.8), 0);
            var control = new CastrumCannonControl(); var interactions = 0; var fires = 0; var mounted = false;
            bool Interact(ulong target) { Check(target == 11, "Wrong cannon."); ++interactions; mounted = true; return true; }
            bool Fire(Vector3 target) { Check(target == center, "Wrong ground target."); if (!mounted) return false; ++fires; return true; }
            var player = actors[1];
            control.Update(e, player, false, Start, Interact, Fire);
            Check(interactions == 0 && fires == 0 && control.PulseAttempts == 0, "Disabled/replay invoked native callbacks.");
            control.Update(e, player with { Position = center + Vector3.UnitX * 4 }, true, Start, Interact, Fire);
            Check(interactions == 0, "Out-of-range cannon interacted.");
            control.Update(e, player with { Cast = new(1, 0, center, 0, Start, Start.AddSeconds(5), false, false) }, true, Start, Interact, Fire);
            Check(interactions == 0, "Cannon interrupted a live cast.");
            control.Update(e, player, true, Start, Interact, Fire);
            control.Update(e, player, true, Start.AddSeconds(.1), Interact, Fire);
            control.Update(e, player, true, Start.AddSeconds(.6), Interact, Fire);
            Check(interactions == 1 && fires == 1, "Cannon rate/enter/fire order differs.");
            e.Reset(); control.Update(e, player, true, Start.AddSeconds(2), Interact, Fire);
            Check(control.InteractionAttempts == 0 && control.PulseAttempts == 0, "Finished phase kept cannon request.");
        });
        run("Castrum Abania ten grids and three cones expose subsequent helper after resolution", () =>
        {
            var e = Prepare(0x3F3B, out var actors); var center = Center(0x3F3B);
            foreach (var (action, count) in new[] { (33460u, 11), (33463u, 4) })
                for (var i = 0; i < count; ++i)
                {
                    var source = (ulong)(action * 100 + i); actors.Add(Actor(source, 0x233C, center));
                    e.Synchronize(actors, Start);
                    e.StartCast(source, 0x233C, action, center, 0, Start, Start.AddSeconds(4.3), 0);
                }
            Check(e.Collect(Start, 100).Count(a => a.Action == 33460) == 10 && e.Collect(Start, 100).Count(a => a.Action == 33463) == 3, "Display caps differ.");
            e.Apply(new LogSystemActionEffectEvent { SourceId = 3346000, ActionId = 33460, GlobalSequence = 1 }, Start.AddSeconds(4));
            Check(e.Collect(Start, 100).Count(a => a.Action == 33460) == 10, "Next grid failed to advance.");
        });
        foreach (var stack in new[] { false, true })
            run($"Castrum Abania real solver enters {(stack ? "four-player stack" : "empty tower")} and both views retain original radius", () =>
            {
                var e = Prepare(0x3F3B, out var actors); var center = Center(0x3F3B);
                actors.Add(Actor(10, 0x233C, center + Vector3.UnitX * 10));
                actors.Add(Player(101, stack ? center - Vector3.UnitX * 10 : center + Vector3.UnitX * 10));
                actors.Add(Actor(11, 0x233C, center - Vector3.UnitX * 10)); e.Synchronize(actors, Start);
                e.StartCast(10, 0x233C, stack ? 33462u : 33464u, center, null, Start, Start.AddSeconds(8.3), 101, actors[2].Position);
                if (!stack) e.StartCast(11, 0x233C, 33464, center, null, Start, Start.AddSeconds(8.3), 0, actors[^1].Position);
                var areas = e.Collect(Start, 100);
                foreach (var a in areas.Where(a => a.PreviewOnly))
                    Check(HostDrawingGeometry.Convert(a.Geometry).Contains(a.Position + Vector3.UnitX * 4.9f)
                        && !a.Geometry.Contains(a.Position + Vector3.UnitX * 5.1f), "Stack/tower display radius differs.");
                using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
                manager.SetArenaBoundary(e.Boundary!);
                foreach (var a in areas.Where(a => !a.PreviewOnly && !a.Safe))
                    manager.AddDangerArea(a.Shape, new StaticTransform(a.Position, a.Rotation), [a.Window]);
                var snapshot = manager.BuildSnapshot(Start);
                var solution = new SafeZoneSolver().Solve(snapshot, new(center, 0, 6, false, false, 0, true, snapshot.Version),
                    new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, Start);
                var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
                Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 1
                    && Vector3.Distance(path[^1].Position, center - Vector3.UnitX * 10) <= 5, "Strong stack/tower demand failed.");
            });
        run("Castrum Abania bait clock persists until actual effect and ignores unrelated spectators", () =>
        {
            var e = Prepare(0x3F3B, out var actors); var center = Center(0x3F3B);
            actors.Add(Actor(10, 0x233C, center)); actors[1] = Player(100, center + Vector3.UnitZ * 8);
            actors.Add(Player(101, center + Vector3.UnitX * 8)); actors.Add(Player(999, center + Vector3.UnitZ * 8));
            e.Synchronize(actors, Start, new HashSet<ulong> { 100, 101 });
            e.Apply(new LogSystemActionEffectEvent { SourceId = 10, TargetId = 100, ActionId = 33467, GlobalSequence = 1 }, Start);
            var bait = e.Collect(Start.AddSeconds(8), 100).Single(a => a.Key.StartsWith("bait-aim"));
            Check(bait.From == Start.AddSeconds(5) && !bait.Geometry.Contains(actors[1].Position)
                && bait.Geometry.Contains(actors[3].Position), "Bait timing/party direction differs.");
            e.Apply(new LogSystemActionEffectEvent { SourceId = 10, ActionId = 33466, GlobalSequence = 2 }, Start.AddSeconds(8));
            Check(e.Collect(Start, 100).Count == 0, "Bait effect did not clear.");
        });
        run("Castrum Abania long claw tether survives fifteen seconds and stale cancellation", () =>
        {
            var e = Prepare(0x1AAE, out var actors); var center = Center(0x1AAE);
            actors.Add(Actor(10, 0x1AB0, center) with { Tethers = [new(1, 100)] });
            e.Synchronize(actors, Start);
            e.Synchronize(actors, Start.AddSeconds(20));
            Check(e.Collect(Start.AddSeconds(20), 100).Any(a => a.Key == "claw/10"), "Long tether timed out.");
            e.TetherCancel(new LogSystemTetherCancelEvent { SourceId = 10, Timestamp = Start.AddSeconds(20) });
            e.Synchronize(actors, Start.AddSeconds(20.1));
            Check(e.ClawTethers.Count == 0, "Stale tether resurrected cancellation.");
            e.Synchronize(actors, Start.AddSeconds(20.8));
            Check(e.ClawTethers.Count == 0, "Stale tether resurrected after short override expired.");
            actors[^1] = actors[^1] with { Tethers = [] }; e.Synchronize(actors, Start.AddSeconds(21));
            Check(e.ClawTethers.Count == 0, "Confirmed untether retained.");
        });
        run("Castrum Abania spread icon uses original clock and waits for multi-target effect", () =>
        {
            var e = Prepare(0x1AAE, out var actors); var center = Center(0x1AAE);
            actors.Add(Player(101, center + Vector3.UnitX * 12)); e.Synchronize(actors, Start);
            e.Icon(new LogSystemTargetIconEvent { TargetId = 100, MarkerId = 74 }, Start);
            e.Icon(new LogSystemTargetIconEvent { TargetId = 101, MarkerId = 74 }, Start);
            Check(e.Collect(Start.AddSeconds(7), 100).Count(a => a.Action == 7978 && !a.NavigationOnly) == 2, "Spread disappeared before actual hit.");
            foreach (var target in new ulong[] { 100, 101 })
                e.Apply(new LogSystemActionEffectEvent { SourceId = 10, TargetId = target, ActionId = 7978, GlobalSequence = 1 }, Start.AddSeconds(7));
            Check(e.Collect(Start, 100).Count == 0, "Batch spread effect retained second target.");
        });
    }

    private static CastrumAbaniaEncounter Prepare(uint oid, out List<ReplayActorState> actors)
    {
        var center = Center(oid); actors = [Actor(1, oid, center) with { InCombat = true }, Player(100, center)];
        var e = new CastrumAbaniaEncounter(); e.Synchronize(actors, Start); return e;
    }
    private static ReplayActorState Actor(ulong id, uint oid, Vector3 position)
        => new() { Id = id, Oid = oid, Kind = 2, Position = position, Targetable = true, Hostile = true, Hp = 1000, MaxHp = 1000 };
    private static ReplayActorState Player(ulong id, Vector3 position) => Actor(id, 0, position) with { Kind = 1, Hostile = false };
    private static Vector3 Center(uint oid) => oid switch { 0x1AA9 => new(-212.99f, 0, 186.99f), 0x3F3B => new(10.5f, 0, 186.5f), _ => new(282.5f, 0, -27.25f) };
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
