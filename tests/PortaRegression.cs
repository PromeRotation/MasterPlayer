using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class PortaRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (boss, center) in new[] { (0x38FBu, new Vector3(-772, 0, -600)), (0x3900u, new Vector3(-704, 0, 480)) })
            run($"Porta {boss:X} exact phase arena ownership and trivial lifetime", () =>
            {
                using var f = new Fight(boss, 1048);
                Check(ArrDungeonCatalog.Owns(boss, 1048) && !ArrDungeonCatalog.Owns(boss, 0)
                    && ArrDungeonCatalog.IsScript("masterplayer.trial.porta-decumana"), "Ownership mismatch.");
                var arena = f.Engine.Arena!;
                Check(arena.Position == center && arena.Contains(center + Vector3.UnitX * 19.49f)
                    && !arena.Contains(center + Vector3.UnitX * 19.51f), "Fixed arena differs.");
                f.Actors[1] = f.Actors[1] with { InCombat = false }; f.Scan();
                Check(f.Engine.Active?.BossOid == boss, "Temporary combat flag ended phase.");
                f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
                Check(f.Engine.Active == null && f.Snapshot.ArenaBoundary == null, "Dead primary retained phase.");
            });
        foreach (var (boss, action, radius, width) in new[]
        {
            (0x38FBu, 28999u, 25f, 0f), (0x38FBu, 29000u, 40f, 6f), (0x38FBu, 28981u, 40f, 6f),
            (0x38FBu, 29001u, 6f, 0f), (0x38FBu, 28997u, 23f, 0f), (0x38FBu, 28983u, 8f, 0f),
            (0x3900u, 29008u, 40f, 6f), (0x3900u, 29009u, 40f, 6f), (0x3900u, 29010u, 40f, 6f),
            (0x3900u, 29019u, 40f, 4f), (0x3900u, 29020u, 40f, 12f),
        })
            run($"Porta {action} locked shape world conversion margin NPC clock and observed end", () =>
            {
                using var f = new Fight(boss, 1048);
                f.Actors[10] = new(10, 0xFFFF, Vector3.Zero); f.Scan(); f.Cast(10, action, 4, MathF.PI / 2);
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                var p = Vector3.UnitX * (radius - .01f);
                Check(geometry.Contains(p) && HostDrawingGeometry.Convert(geometry).Contains(p)
                    && !geometry.Contains(Vector3.UnitX * (radius + .01f)), "Shape or world extent differs.");
                if (width > 0) Check(geometry.Contains(new(1, 0, width / 2 - .01f))
                    && !geometry.Contains(new(1, 0, width / 2 + .01f)), "Rectangle width differs.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Now.AddSeconds(4.3)
                    && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerUntil == null, "Clock/lifetime differs.");
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.UnitX * (radius + .4f)), "Margin missing.");
                f.Now = f.Start.AddSeconds(20);
                f.Actors[10] = f.Actors[10] with { Dead = true, Position = Vector3.UnitZ * 100 };
                f.Scan(); Check(f.Draw.Previews.Count == 1, "Predicted timeout/death ended observed cast.");
                f.Actors[10] = f.Actors[10] with { Cast = null };
                f.Scan(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Actual finish leaked.");
            });
        run("Porta phase one donut and parallel puddles remain independent", () =>
        {
            using var f = new Fight(0x38FB, 1048);
            for (ulong i = 10; i < 20; ++i) f.Actors[i] = new(i, 0x233C, Vector3.Zero);
            f.Scan();
            for (ulong i = 10; i < 20; ++i) f.Cast(i, 29001, 3);
            f.Cast(1, 28980, 3);
            Check(f.Draw.Previews.Count == 11 && f.Snapshot.DangerAreas.Count == 11, "Parallel puddles collapsed.");
            var donut = (DonutGeometry)f.Draw.Previews["cast/1"].Geometry;
            Check(donut.InnerRadius == 12.5f && donut.OuterRadius == 25, "Donut differs.");
            f.Engine.Finish(10, 29001); f.Render();
            Check(f.Draw.Previews.Count == 10, "One finish consumed other puddles.");
        });
        foreach (var status in new[] { 325u, 3012u })
            run($"Porta shield {status} bans only primary and clears through event removal", () =>
            {
                using var f = new Fight(0x38FB, 1048);
                f.Actors[1] = f.Actors[1] with { Statuses = [status] };
                f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false);
                f.Actors[10] = new(10, 0x38FE, Vector3.Zero, Statuses: [status], InCombat: true, TargetId: 100); f.Scan();
                Check(f.Engine.ForbiddenTargets.SequenceEqual([1UL]) && !f.Engine.RequireAllStop, "Shield affected unrelated target or globally stopped.");
                Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 10, "Shield request prevented switching to an attackable gaol.");
                f.Actors[10] = f.Actors[10] with { Targetable = false };
                f.Actors[11] = new(11, 0x233C, Vector3.Zero, Targetable: false, InCombat: true, TargetId: 100);
                f.Actors[12] = new(12, 0x38FC, Vector3.Zero, Hostile: false, InCombat: true, TargetId: 100);
                f.Actors[13] = new(13, 0x1234, Vector3.Zero); f.Scan();
                Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 0, "Shield fallback selected a helper, ally or passive enemy.");
                f.Actors[13] = f.Actors[13] with { InCombat = true, TargetId = 100 }; f.Scan();
                Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 13, "Shield fallback ignored an enemy attacking the party.");
                f.Actors[13] = f.Actors[13] with { TargetId = 999 }; f.Scan();
                Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 0, "Shield fallback selected an enemy attacking an unknown target.");
                f.Events.Handle(new PromeRotation.LogSystem.LogSystemStatusRemoveEvent { TargetId = 1, StatusId = status, Timestamp = f.Now }, f.Now);
                f.Scan(); Check(f.Engine.ForbiddenTargets.Length == 0 && f.Engine.PriorityTarget(1, Vector3.Zero) == 1,
                    "Removed shield resurrected from stale snapshot or failed to restore primary.");
            });
        run("Porta phase one center requirement ignores immunity but displacement honors activation immunity", () =>
        {
            using var f = new Fight(0x38FB, 1048);
            var center = f.Engine.Arena!.Position;
            f.Actors[100] = new(100, 0, center + Vector3.UnitX * 10, PartySlot: 0, Hostile: false,
                StatusExpirations: new Dictionary<uint, DateTime> { [1209] = f.Now.AddSeconds(10) });
            var cast = new DungeonCastObservation(29003, center, 0, f.Now, f.Now.AddSeconds(4.3));
            f.Actors[10] = new(10, 0x38FF, center, Cast: cast);
            f.Scan();
            Check(Group(f, "vulcan").Single().Shape.Resolve(center, 0, null).Contains(center + Vector3.UnitX * 6)
                && !Group(f, "vulcan").Single().Shape.Resolve(center, 0, null).Contains(center), "Center R5 requirement missing.");
            Check(Group(f, "knockback").Length == 0 && !f.Engine.WantsPortaKnockbackImmunity, "Immunity projection or unrequested action differs.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [1209] = f.Now.AddSeconds(4) } };
            f.Scan();
            Check(Group(f, "knockback").Length == 2, "Expired immunity still suppressed displacement.");
            var landing = Group(f, "knockback").Single(a => a.Id.EndsWith("/landing")).Position;
            Check(f.Engine.Arena.Contains(landing) && Vector3.Distance(landing, center) > 19.49f, "Stop-at-wall landing differs.");
            f.Engine.Finish(10, 29003); f.Render();
            Check(Group(f, "vulcan").Length == 0 && Group(f, "knockback").Length == 0, "Knockback finish retained output.");
        });
        run("Porta explosion stays visible while cannon alone controls navigation then restores", () =>
        {
            using var f = new Fight(0x3900, 1048);
            f.Actors[10] = new(10, 0x233C, Vector3.Zero);
            f.Actors[11] = new(11, 0x3901, Vector3.Zero); f.Scan();
            f.Cast(10, 29021, 7); f.Cast(11, 29019, 4);
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 1
                && !Group(f, "explosion").Single().Navigate, "Explosion visual/navigation gates conflated.");
            Check(f.Engine.Hints(Vector3.Zero).Contains("离开飞空艇爆炸"), "Suppressed navigation hid warning.");
            f.Engine.Finish(11, 29019); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1 && Group(f, "explosion").Single().Navigate, "Cannon finish did not restore explosion.");
            f.Engine.Finish(10, 29021); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Explosion finish leaked.");
        });
        run("Porta moving orb angle gate axis aligned squares true union and display circle", () =>
        {
            using var f = new Fight(0x3900, 1048);
            var center = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x3902, center, Rotation: 0);
            f.Actors[11] = new(11, 0x3902, center + Vector3.UnitX * 5, Rotation: MathF.PI / 2); f.Scan();
            Check(Group(f, "orb").Count(a => a.Safe && a.Display) == 1 && f.Engine.WantsPortaSprint, "Orb angle gate failed.");
            var goal = Group(f, "orb").Single(a => a.Navigate);
            var nav = goal.Shape.Resolve(goal.Position, 0, null);
            Check(nav.Contains(center + Vector3.UnitX * 5.5f) && !nav.Contains(center)
                && !nav.Contains(center + new Vector3(6.1f, 0, 0)), "Orb target square differs.");
            Check(f.Snapshot.GoalAreas.Count == 1 && f.Snapshot.DangerAreas.Count == 0, "Soft attraction became hard danger.");
            f.Actors[10] = f.Actors[10] with { Rotation = MathF.PI }; f.Scan();
            var goals = Group(f, "orb").Where(a => a.Navigate).Select(a => a.Shape.Resolve(a.Position, 0, null)).ToArray();
            Check(goals.Any(g => g.Contains(center - Vector3.UnitZ * .5f)) && goals.Any(g => g.Contains(center + Vector3.UnitX * 5.5f)),
                "Orb union lost a target.");
            f.Actors[10] = f.Actors[10] with { Dead = true };
            f.Actors.Remove(11); f.Scan();
            Check(!f.Engine.WantsPortaSprint && !Group(f, "orb").Any(), "Dead/removed orbs retained control.");
        });
        run("Porta spread follows target uses six display seven navigation excludes dead and clears", () =>
        {
            using var f = Party();
            CastTarget(f, 10, 29012, 100);
            Check(Group(f, "spread").Single(a => a.Display).Shape is MasterPlayer.Spatial.SafeZone.CircleTemplate { Radius: 6 }
                && Group(f, "spread").Where(a => a.Navigate).All(a => a.Shape is MasterPlayer.Spatial.SafeZone.CircleTemplate { Radius: 7 }),
                "Spread display/navigation threshold differs.");
            f.Actors[100] = f.Actors[100] with { Position = Vector3.UnitX * 10 }; f.Scan();
            Check(Group(f, "spread").Single(a => a.Display).Position.X == 10, "Spread did not follow target.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(!Group(f, "spread").Any(), "Dead spread target retained output.");
            f.Engine.Finish(10, 29012); f.Render(); Check(!Group(f, "spread").Any(), "Spread finish leaked.");
        });
        foreach (var (boss, territory, orbOid, prefix) in new[]
        {
            (0x3900u, 1048u, 0x3902u, "porta"), (0x415Fu, 1193u, 0x4162u, "worqor"),
        })
            foreach (var mode in new[] { "single", "multiple", "nearest-danger" })
                run($"Porta related {prefix} {mode} actual default solver reaches shifted square and respects real danger", () =>
                {
                    using var f = new Fight(boss, territory);
                    var center = f.Engine.Arena!.Position;
                    f.Actors[1] = f.Actors[1] with { Position = center };
                    f.Actors[100] = new(100, 0, center, PartySlot: 0, Hostile: false);
                    f.Actors[10] = new(10, orbOid, center + new Vector3(5.137f, 0, .271f), Rotation: MathF.PI / 2);
                    if (mode != "single") f.Actors[11] = new(11, orbOid, center + new Vector3(.137f, 0, 9.271f), Rotation: -MathF.PI / 2);
                    f.Engine.Synchronize(f.Actors.Values, center, f.Now, playerId: 100); f.Render();
                    if (mode == "nearest-danger")
                        f.Safe.AddPersistentDanger(new MasterPlayer.Spatial.SafeZone.CircleTemplate(2),
                            new MasterPlayer.Spatial.SafeZone.StaticTransform(f.Actors[10].Position, 0));
                    var snapshot = f.Snapshot;
                    var solution = new SafeZoneSolver().Solve(snapshot, new(center, 0, 6, false, false, 0, true, snapshot.Version),
                        new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
                    Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback, "Default solver failed small orb goals.");
                    var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
                    var goals = f.Engine.AmbientAoes().Where(a => a.Id.StartsWith(prefix + "/orb/join"))
                        .Select(a => a.Shape.Resolve(a.Position, 0, null)).ToArray();
                    Check(path.Count > 1 && goals.Any(g => g.Contains(path[^1].Position)), "Solver did not actively enter an orb square.");
                    Check(path.All(p => f.Engine.Arena.Contains(p.Position)), "Orb route left arena.");
                    if (mode == "nearest-danger")
                        Check(Vector3.Distance(path[^1].Position, f.Actors[10].Position) > 2.5f, "Orb goal overrode real danger.");
                });
        run("Porta four person stack target joins buddy others join circle overcapacity changes color", () =>
        {
            using var f = Party();
            CastTarget(f, 10, 29014, 100);
            Check(Group(f, "stack").Single(a => a.Display).Safe && Group(f, "stack").Any(a => a.Id.EndsWith("/buddy")), "Marked player lost buddy navigation.");
            f.PlayerId = 101; f.Scan();
            Check(Group(f, "stack").Single(a => a.Display).Safe && Group(f, "stack").Any(a => a.Id.EndsWith("/join")), "Unmarked player did not join.");
            f.Actors[104] = new(104, 0, Vector3.Zero, PartySlot: 4, Hostile: false); f.Scan();
            Check(!Group(f, "stack").Single(a => a.Display).Safe && Group(f, "stack").Any(a => a.Id.EndsWith("/avoid")), "Overcapacity stack still invited player.");
            f.Engine.Finish(10, 29014); f.Render(); Check(!Group(f, "stack").Any(), "Stack finish leaked.");
        });
        run("Porta raidwide tankbuster predictions and Ultima countdown immunity request cleanup", () =>
        {
            using var f = Party();
            CastTarget(f, 10, 29023, 100); CastTarget(f, 11, 29022, 1);
            Check(f.Engine.PredictedDamage.Count(d => d.DamageType == "Tankbuster") == 1
                && f.Engine.PredictedDamage.Count(d => d.DamageType == "Raidwide") == 4, "Damage predictions differ.");
            CastTarget(f, 12, 29015, 1);
            Check(f.Engine.WantsPortaKnockbackImmunity && f.Engine.Hints(Vector3.Zero).Contains("准备吸收以太球"), "AethericBoom request missing.");
            f.Engine.Finish(12, 29015); f.Render(); Check(!f.Engine.WantsPortaKnockbackImmunity, "Immunity request leaked.");
            CastTarget(f, 12, 29024, 1, 71);
            Check(f.Engine.PortaCountdown!.Contains("71.3"), "Enrage countdown differs.");
            f.Engine.ResetFight(); f.Render();
            Check(f.Engine.PortaCountdown == null && f.Engine.ForbiddenTargets.Length == 0 && f.Draw.Previews.Count == 0, "Reset leaked phase state.");
        });
    }

    private static Fight Party()
    {
        var f = new Fight(0x3900, 1048);
        for (var i = 0; i < 4; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, Vector3.Zero, PartySlot: i, Hostile: false);
        f.Scan();
        return f;
    }
    private static void CastTarget(Fight f, ulong source, uint action, ulong target, double duration = 5)
    {
        f.Actors[source] = new(source, 0xFFFF, Vector3.Zero);
        f.Scan();
        var cast = new DungeonCastObservation(action, Vector3.Zero, 0, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast };
        f.Engine.StartCast(source, cast, f.Now);
        f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("porta/" + prefix)).ToArray();
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
