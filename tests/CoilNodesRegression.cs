using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class CoilNodesRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Coil nodes identity owns exactly ADS and seven nodes with source bounds", () =>
        {
            using var f = Scene(0x7D9);
            Check(ArrDungeonCatalog.IsScript("masterplayer.raid.coil1") && ArrDungeonCatalog.IsScript("masterplayer.raid.coil2")
                && ArrDungeonCatalog.Owns(0x887, 241) && ArrDungeonCatalog.Owns(0x7D7, 241), "T01 ownership missing.");
            foreach (var oid in Enumerable.Range(0x7D9, 7).Select(i => (uint)i))
            {
                using var n = Scene(oid);
                Check(n.Engine.Active?.BossOid == oid && ArrDungeonCatalog.Owns(oid, 242)
                    && n.Engine.Arena is RectangleGeometry, "Node identity.");
            }
            Check(f.Engine.Arena!.Position == new Vector3(0, 0, 77) && f.Engine.Arena.Contains(new(17.9f, 0, 89.9f))
                && !f.Engine.Arena.Contains(new(18.1f, 0, 77)), "ADS half extents.");
        });
        run("Coil nodes temporary node pull suppressed while ADS in combat", () =>
        {
            using var f = Scene(0x7DA);
            f.Engine.ResetFight(); f.Actors[2] = new(2, 0x7D9, new(0, 0, 77), InCombat: true);
            f.Scan(); Check(f.Engine.Active?.BossOid == 0x7D9 && f.Engine.PrimaryId == 2, "Temporary quarantine started.");
            f.Engine.ResetFight(); f.Actors[2] = f.Actors[2] with { InCombat = false };
            f.Scan(); Check(f.Engine.Active?.BossOid == 0x7DA, "Idle ADS blocked node.");
        });
        run("Coil nodes capability isolation does not enable other node mechanics", () =>
        {
            foreach (var oid in Enumerable.Range(0x7DA, 6).Select(i => (uint)i))
            {
                using var f = Scene(oid);
                foreach (var action in new uint[] { 1220, 1218, 675, 1343 })
                {
                    var expected = f.Engine.Active!.Casts.Any(c => c.Action == action);
                    Start(f, 10, action);
                    Check(f.Engine.Casts.ContainsKey(10) == expected, "Node capability bleed.");
                    End(f, 10, action); f.Now = f.Now.AddSeconds(1);
                }
            }
        });
        run("Coil nodes Ballast three radii and 270 degree arcs match source on sampled grid", () =>
        {
            using var f = Scene(0x7D9);
            foreach (var (action, inner, outer) in new[] { (1343u, 0f, 5.5f), (1222u, 5.5f, 10.5f), (1223u, 10.5f, 15.5f) })
            {
                Start(f, 10, action, rotation: .7f);
                var c = f.Engine.Casts[10]; var shape = c.Rule.Shape!.Resolve(c.Location, c.Rotation, null);
                BossMod.AOEShape source = inner == 0 ? new BossMod.AOEShapeCone(outer, new(MathF.PI * .75f))
                    : new BossMod.AOEShapeDonutSector(inner, outer, new(MathF.PI * .75f));
                for (var x = -16f; x <= 16; x += .37f)
                    for (var z = -16f; z <= 16; z += .41f)
                        Check(shape.Contains(c.Location + new Vector3(x, 0, z), .00001f)
                            == source.Check(new(c.Location.X + x, c.Location.Z + z), new(c.Location.X, c.Location.Z), new(.7f)), "Ballast grid mismatch.");
                End(f, 10, action); f.Now = f.Now.AddSeconds(1);
            }
        });
        run("Coil nodes continuous Ballast finish consumes only matching helper and effect does not finish", () =>
        {
            using var f = Scene(0x7D9);
            Start(f, 10, 1343); Start(f, 11, 1222); Start(f, 12, 1223);
            Check(f.Draw.Previews.Count >= 3 && f.Snapshot.DangerAreas.Count == 3, "Dual view and navigation.");
            f.Engine.Effect(10, 1343, 1, f.Now); f.Render();
            Check(f.Engine.Casts.Count == 3, "Effect prematurely consumed cast.");
            End(f, 11, 1222); Check(f.Engine.Casts.Count == 2 && f.Snapshot.DangerAreas.Count == 2, "Finish consumed other rings.");
            f.Now = f.Now.AddSeconds(20); f.Engine.Tick(f.Now); f.Render();
            Check(f.Engine.Casts.Count == 2, "No observed cast end was replaced by nominal timer.");
        });
        run("Coil nodes exact laser cannon dirty cannon and five simultaneous fire streams", () =>
        {
            using var f = Scene(0x887, 241);
            Start(f, 10, 1450); Check(f.Engine.Casts[10].Rule.Shape is RayRectangleTemplate { Length: 32.3f, MinLocalX: -3, MaxLocalX: 3 }, "T01 laser.");
            End(f, 10, 1450); f.Now = f.Now.AddSeconds(1);
            Start(f, 10, 1378); Check(f.Engine.Casts[10].Rule.Shape is CircleTemplate { Radius: 5.15f }, "Dirty cannon.");
            using var p = Scene(0x7DF);
            for (ulong i = 10; i < 15; ++i) Start(p, i, 675, rotation: (i - 10) * .4f);
            Check(p.Engine.OutputCasts.Count() == 5 && p.Snapshot.DangerAreas.Count == 5
                && p.Engine.Casts.Values.All(c => c.Rule.Shape is RayRectangleTemplate { Length: 35.5f }), "Firestream helpers.");
        });
        run("Coil nodes gravity predicts target dynamically then event fixed location plus one second", () =>
        {
            using var f = Scene(0x7DB);
            Start(f, 10, 1220, target: 101);
            f.Actors[101] = f.Actors[101] with { Position = new(-40, 0, 90) }; f.Scan();
            Check(Group(f, "gravity/cast").Single().Position == f.Actors[101].Position, "Cast target stopped following.");
            f.Engine.Effect(10, 1220, 1, f.Now, 101, new(999, 0, 999)); f.Render();
            var p = Group(f, "gravity/event").Single();
            Check(p.Position == f.Actors[101].Position && p.Activation == f.Now.AddSeconds(1), "Event target/activation.");
            End(f, 10, 1220); f.Now = f.Now.AddSeconds(100); f.Engine.Tick(f.Now); f.Render();
            Check(Group(f, "gravity/event").Length == 1, "Predicted field expired before spawn.");
        });
        run("Coil nodes gravity spawn consumes one prediction per source per update and retains EventState7", () =>
        {
            using var f = Scene(0x7DB);
            f.Engine.Effect(10, 1220, 1, f.Now, 101);
            f.Engine.Effect(10, 1220, 2, f.Now, 101);
            f.Actors[20] = new(20, 0x1E8728, f.Actors[101].Position, EventState: 7);
            f.Scan(); Check(Group(f, "gravity/event").Length == 1 && Group(f, "gravity/live").Length == 1, "Source one-per-update/EventState.");
            f.Scan(); Check(!Group(f, "gravity/event").Any(), "Second prediction not matched.");
            f.Actors.Remove(20); f.Scan(); Check(!Group(f, "gravity").Any(), "Removed source remained visible.");
        });
        run("Coil nodes cleave follows target and hides on any cast then restores after finish", () =>
        {
            using var f = Scene(0x7D9);
            f.Actors[1] = f.Actors[1] with { TargetId = 101 }; f.Scan();
            Check(Group(f, "cleave/outline").Single() is { Navigate: true, OutlineOnly: true, Shape: FanTemplate { Radius: 8.3f } }, "ADS cleave.");
            Start(f, 1, 1217); Check(!Group(f, "cleave").Any(), "Cleave active while casting.");
            End(f, 1, 1217); Check(Group(f, "cleave").Any(), "Cleave failed to resume.");
            f.Actors[101] = f.Actors[101] with { Position = f.Actors[1].Position + new Vector3(5, 0, 0) }; f.Scan();
            Check(Group(f, "cleave/outline").Single().Rotation == MathF.PI / 2, "Cleave used actor facing.");
        });
        run("Coil nodes targeted cleave excludes own navigation and adds all buddy bait constraints", () =>
        {
            using var f = Scene(0x7DA);
            f.Actors[1] = f.Actors[1] with { TargetId = 100 }; f.Scan();
            Check(Group(f, "cleave/outline").Single() is { Navigate: false, Shape: FanTemplate { Radius: 7.15f } }
                && Group(f, "cleave/bait").Length == 7 && Group(f, "cleave/bait").All(a => !a.Display), "Target-specific cleave.");
        });
        run("Coil nodes Allagan rot cast predicts finish plus15 and packet correction keeps deadline", () =>
        {
            using var f = Scene(0x7DA); Assign(f);
            var c = Start(f, 1, 1218, target: 100);
            f.Engine.StartCast(1, c with { Location = new(3, 0, 4) }, f.Now); f.Render();
            Check(f.Engine.Hints(null).Any(h => h.Contains("20.3")) && !Group(f, "rot/personal").Any(), "Rot cast prediction/current holder.");
            f.Now = f.Now.AddSeconds(14); f.Engine.Tick(f.Now);
            f.PlayerId = 106; f.Scan();
            Check(Group(f, "rot/personal").Single() is { Shape: SohrKhaiWallHintTemplate } p
                && p.Activation == c.FinishAt.AddSeconds(15), "Predicted receiver deadline.");
        });
        run("Coil nodes rot exact seven second threshold excludes immune at equal deadline", () =>
        {
            using var f = Scene(0x7DA); Assign(f);
            Status(f, 100, 333, 15);
            f.PlayerId = 106; f.Now = f.Start.AddSeconds(7.999); f.Scan();
            Check(!Receiving(f), "Receiver started early.");
            f.Now = f.Start.AddSeconds(8); f.Engine.Tick(f.Now); f.Render();
            Check(Receiving(f), "Exact seven seconds.");
            Status(f, 106, 334, 15); f.Render();
            Check(!Receiving(f), "Immunity equality must reject.");
            f.PlayerId = 104; f.Scan(); Check(Receiving(f), "M1 next priority.");
        });
        run("Coil nodes rot dead candidate skip complete assignment requirement and holder immobility", () =>
        {
            using var f = Scene(0x7DA); Assign(f); Status(f, 100, 333, 6);
            f.Actors[106] = f.Actors[106] with { Dead = true }; f.PlayerId = 104; f.Scan();
            Check(Receiving(f), "Dead R1 accepted.");
            f.Engine.CurrentPartyAssignments = []; f.Render();
            Check(!Receiving(f), "Unset assignment guessed receiver.");
            f.Engine.CurrentPartyAssignments = ["MT", "OT", "H1", "H2", "M1", "M2", "R1", "R1"]; f.Render();
            Check(!Receiving(f), "Duplicate roles accepted.");
            f.PlayerId = 100; f.Scan(); Check(!Group(f, "rot/personal").Any(), "Holder should wait for receiver.");
        });
        run("Coil nodes rot status packet overrides stale frame and loss intentionally retains last holder", () =>
        {
            using var f = Scene(0x7DA); Assign(f);
            Status(f, 100, 333, 6); Status(f, 106, 333, 15);
            f.Scan(); Check(Group(f, "rot/holder").Single().Position == f.Actors[106].Position, "Stale raw replaced packet holder.");
            f.Engine.StatusChanged(106, 333, false, f.Now); f.Render();
            Check(Group(f, "rot/holder").Single().Position == f.Actors[106].Position, "Loss reset holder unlike source.");
            f.Actors[106] = f.Actors[106] with { Statuses = [] }; f.Scan();
            Check(Group(f, "rot/holder").Single().Position == f.Actors[106].Position, "Raw loss reset retained holder.");
        });
        run("Coil nodes raw status gain backfill and packet confirmation do not revive earlier holder", () =>
        {
            using var f = Scene(0x7DA); Assign(f);
            f.Actors[100] = f.Actors[100] with { Statuses = [333], StatusExpirations = new Dictionary<uint, DateTime> { [333] = f.Start.AddSeconds(6) } };
            f.Scan(); f.PlayerId = 106; f.Scan();
            Check(Receiving(f), "Raw backfill missing.");
            Status(f, 100, 333, 6); Status(f, 106, 333, 15); f.Scan();
            Check(!Group(f, "rot/personal").Any(), "Old confirmed status took holder back.");
        });
        run("Coil nodes interrupt actions high voltage only and OT add priority", () =>
        {
            using var f = Scene(0x887, 241); Assign(f);
            f.Actors[20] = new(20, 0x888, f.Actors[1].Position); f.Scan();
            Start(f, 1, 1447);
            Check(f.Engine.CoilInterruptTarget(19)?.Id == 1 && f.Engine.CoilInterruptTarget(25) == null, "Job interrupt gating.");
            Check(f.Engine.PriorityTarget(0, Vector3.Zero) == 1, "Non-OT abandoned boss.");
            f.PlayerId = 101; f.Scan(); Check(f.Engine.PriorityTarget(0, Vector3.Zero) == 20, "OT add priority.");
            End(f, 1, 1447); Check(f.Engine.CoilInterruptTarget(19) == null, "Interrupt persisted.");
        });
        run("Coil nodes missing status duration accepts authoritative raw expiry without permanent packet lock", () =>
        {
            using var f = Scene(0x7DA); Assign(f);
            f.Engine.StatusChanged(100, 333, true, f.Now);
            f.Actors[100] = f.Actors[100] with { Statuses = [333], StatusExpirations = new Dictionary<uint, DateTime> { [333] = f.Start.AddSeconds(15) } };
            f.PlayerId = 106; f.Scan(); Check(!Receiving(f), "Missing duration was never corrected.");
            f.Engine.StatusChanged(106, 334, true, f.Now, expiresAt: f.Start.AddSeconds(45));
            f.Actors[106] = f.Actors[106] with { Statuses = [334], StatusExpirations = new Dictionary<uint, DateTime> { [334] = f.Start.AddSeconds(9) } };
            f.Now = f.Start.AddSeconds(8); f.Scan();
            Check(Receiving(f), "Divergent raw expiry permanently blocked after convergence window.");
        });
        run("Coil nodes raw source and param changes reemit status gain and same ID recreation backfills", () =>
        {
            using var f = Scene(0x7DA);
            var a = f.Actors[100] with { Statuses = [333], StatusExpirations = new Dictionary<uint, DateTime> { [333] = f.Start.AddSeconds(15) } };
            f.Actors[100] = a; f.Scan(); Status(f, 106, 333, 20);
            f.Actors[100] = a with { StatusSources = new Dictionary<uint, ulong> { [333] = 1 } }; f.Scan();
            Check(Group(f, "rot/holder").Single().Position == a.Position, "Source-only gain missed.");
            Status(f, 106, 333, 20);
            f.Actors[100] = f.Actors[100] with { StatusParams = new Dictionary<uint, ushort> { [333] = 1 } }; f.Scan();
            Check(Group(f, "rot/holder").Single().Position == a.Position, "Param-only gain missed.");
            Status(f, 106, 333, 20); var restored = f.Actors[100];
            f.Engine.RemoveActor(100); f.Actors.Remove(100); f.Scan();
            f.Actors[100] = restored; f.Scan();
            Check(Group(f, "rot/holder").Single().Position == a.Position, "Recreated actor did not replay gain.");
        });
        run("Coil nodes tank swap four vuln stacks only when off tank clean", () =>
        {
            using var f = Scene(0x7D9); Assign(f);
            f.Actors[1] = f.Actors[1] with { TargetId = 100 };
            f.Actors[100] = f.Actors[100] with { Statuses = [202], StatusParams = new Dictionary<uint, ushort> { [202] = 4 } };
            f.PlayerId = 101; f.Scan(); Check(f.Engine.Hints(null).Any(h => h.Contains("换坦")), "Swap missing.");
            f.Actors[101] = f.Actors[101] with { Statuses = [202] }; f.Scan();
            Check(!f.Engine.Hints(null).Any(h => h.Contains("换坦")), "Dirty off tank asked to provoke.");
        });
        run("Coil nodes live death preserves fight and director wipe clears owner until recommence", () =>
        {
            using var f = Scene(0x7DA); Status(f, 100, 333, 6); Start(f, 10, 1217);
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(f.Engine.Active != null && f.Engine.PreserveFightAcrossPlayerDeath, "Personal death reset fight.");
            f.Engine.DirectorUpdate(0x40000005); f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Wipe resurrected live node.");
            f.Actors[10] = f.Actors[10] with { Cast = null };
            f.Engine.DirectorUpdate(0x40000011); f.Scan();
            Check(f.Engine.Active != null && !Group(f, "rot").Any(), "Rot leaked across wipe.");
        });
        run("Coil nodes solver escapes ballast ring into available safe region", () =>
        {
            using var f = Scene(0x7D9);
            f.Actors[10] = f.Actors[10] with { Position = new(0, 0, 77) };
            Start(f, 10, 1223, rotation: 0);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot,
                new(new(0, 0, 89), 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback
                && solution.ExecutablePath.Count + solution.ContinuationPath.Count > 1, "Ballast escape failed.");
        });
    }

    private static Fight Scene(uint oid, uint territory = 242)
    {
        var f = new Fight(oid, territory);
        var c = f.Engine.Arena!.Position;
        f.Actors[1] = f.Actors[1] with { Position = c, Hp = 100 };
        for (var i = 0; i < 8; ++i)
            f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * (i + 1), PartySlot: i, Hostile: false);
        for (ulong i = 10; i < 16; ++i) f.Actors[i] = new(i, 0x8E2, c);
        f.Scan(); return f;
    }
    private static void Assign(Fight f) => f.Engine.CurrentPartyAssignments = ["MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2"];
    private static void Status(Fight f, ulong actor, uint status, double expiry)
        => f.Engine.StatusChanged(actor, status, true, f.Now, expiresAt: f.Start.AddSeconds(expiry));
    private static DungeonCastObservation Start(Fight f, ulong source, uint action, float rotation = 0, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render(); return cast;
    }
    private static void End(Fight f, ulong source, uint action)
    { f.Engine.Finish(source, action); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render(); }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("coil/" + prefix)).ToArray();
    private static bool Receiving(Fight f) => Group(f, "rot/personal").Single().Shape is SohrKhaiWallHintTemplate;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
