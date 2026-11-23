using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class VanaspatiRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Vanaspati exact three polygon arenas and territory ownership", () =>
        {
            foreach (var (boss, name, field, small) in new (uint, string, string, bool)[]
            {
                (0x33E8, "D031Snatcher.D031Snatcher", "arena", false),
                (0x33E9, "D032Wrecker.D032Wrecker", "StartingArena", false),
                (0x33E9, "D032Wrecker.D032Wrecker", "DefaultArena", true),
            })
            {
                using var f = Prepare(boss); if (small) f.Engine.MapEffect(6, 0x00020001);
                var type = typeof(BossMod.WorldState).Assembly.GetType("BossMod.Endwalker.Dungeon.D03Vanaspati." + name)!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
                var arena = f.Engine.Arena!; var world = HostDrawingGeometry.Convert(arena);
                for (var x = -31.137f; x < 31; x += .31f)
                    for (var z = -31.231f; z < 31; z += .31f)
                    {
                        var p = arena.Position + new Vector3(x, 0, z);
                        Check(arena.Contains(p, 0) == source.Contains(new(x, z)) && world.Contains(p, 0) == arena.Contains(p, 0), "Arena changed.");
                    }
            }
            Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.vanaspati") && new uint[] { 0x33E8, 0x33E9, 0x33EB }
                .All(o => ArrDungeonCatalog.Owns(o, 970) && !ArrDungeonCatalog.Owns(o, 969)), "Ownership leaked.");
        });
        foreach (var (boss, action, shape) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x33E8, 25138, new BossMod.AOEShapeCircle(13)),
            (0x33E8, 25140, new BossMod.AOEShapeCone(40, new(MathF.PI / 2))),
            (0x33E8, 25139, new BossMod.AOEShapeCone(40, new(MathF.PI / 2))),
            (0x33EB, 25167, new BossMod.AOEShapeCircle(6)),
        })
            run($"Vanaspati {action} exact shape world radar cancellation", () =>
            {
                using var f = Prepare(boss);
                var id = action is 25140 or 25139 ? 1UL : 10UL;
                if (id != 1) f.Actors[id] = new(id, 0x233C, f.Actors[1].Position);
                Scan(f); Cast(f, id, action);
                var cast = f.Engine.Casts[id]; Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, shape);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Shape not published.");
                f.Engine.Finish(id); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancel leaked.");
            });
        foreach (var status in new uint[] { 1422, 2936, 3694, 3909 })
            run($"Vanaspati misdirection {status} status event perspective and loss", () =>
            {
                using var f = Prepare(0x33E8); f.Engine.StatusChanged(100, status, true, f.Now); f.Render();
                Check(f.Engine.RequireVanaspatiMisdirection, "Misdirection start missing.");
                f.PlayerId = 101; Scan(f); Check(!f.Engine.RequireVanaspatiMisdirection, "Misdirection leaked across perspective.");
                f.PlayerId = 100; Scan(f); Check(f.Engine.RequireVanaspatiMisdirection, "Personal status lost across perspective.");
                f.Engine.StatusChanged(100, status, false, f.Now); Check(!f.Engine.RequireVanaspatiMisdirection, "Misdirection loss missing.");
            });
        run("Vanaspati misdirection arrow fifteen degree window wrap finite and arrival", () =>
        {
            Check(VanaspatiMisdirectionPolicy.ShouldMove(Vector3.Zero, Vector3.UnitX * 10, .2f, MathF.PI / 2), "Aligned arrow did not move.");
            Check(!VanaspatiMisdirectionPolicy.ShouldMove(Vector3.Zero, Vector3.UnitX * 10, .2f, MathF.PI / 2 + .27f), "Wrong arrow moved.");
            Check(VanaspatiMisdirectionPolicy.ShouldMove(Vector3.Zero, -Vector3.UnitZ * 10, .2f, -MathF.PI), "Wrap at pi failed.");
            Check(!VanaspatiMisdirectionPolicy.ShouldMove(Vector3.Zero, Vector3.UnitX * .1f, .2f, MathF.PI / 2)
                && !VanaspatiMisdirectionPolicy.ShouldMove(Vector3.Zero, Vector3.UnitX, .2f, float.NaN), "Arrival/nonfinite moved.");
        });
        run("Vanaspati bubble fire forced union actual solver consumed bubble cannot revive", () =>
        {
            using var f = Prepare(0x33E9); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x3731, c + Vector3.UnitX * 8);
            f.Actors[11] = new(11, 0x3731, c - Vector3.UnitX * 8); Scan(f);
            Check(f.Snapshot.DangerAreas.Count == 2, "Normal bubbles not dangerous.");
            Cast(f, 1, 25147); var aoes = f.Engine.AmbientAoes().ToArray();
            Check(aoes.Count(a => a.Safe && a.Display && !a.Navigate) == 2
                && aoes.Single(a => a.Id == "vanaspati/fire/enter").Activation == f.Engine.Casts[1].FinishAt, "Fire bubble activation/display differs.");
            Solve(f, p => Math.Min(Vector3.Distance(p, f.Actors[10].Position), Vector3.Distance(p, f.Actors[11].Position)) <= 2.51f);
            Cast(f, 10, 27847); Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "vanaspati/bubble/10"), "Consumed bubble revived from stale object.");
            f.Engine.RemoveActor(11); f.Actors.Remove(11); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("vanaspati/bubble/") || a.Id == "vanaspati/fire/enter"), "Destroyed bubble retained.");
        });
        run("Vanaspati water thirteen yard projected landing tangents immunity and shrink clock", () =>
        {
            using var f = Prepare(0x33E9); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x3731, c + Vector3.UnitX * 15); Scan(f); Cast(f, 1, 25148);
            var aoes = f.Engine.AmbientAoes().ToArray();
            Check(aoes.Any(a => a.Id == "vanaspati/water/center") && aoes.Any(a => a.Id == "vanaspati/water/cone/10"), "Water source constraints missing.");
            var projected = (VanaspatiKnockbackTemplate)aoes.Single(a => a.Id == "vanaspati/water/avoid-landing").Shape;
            Check(Vector3.Distance(projected.Landing(c + Vector3.UnitX), c + Vector3.UnitX * 14) < .001f, "Water distance differs.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(20) } }; Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("vanaspati/water/")), "Water immunity failed.");
            Cast(f, 1, 25153);
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "vanaspati/ring").Activation == f.Engine.Casts[1].FinishAt.AddSeconds(.7), "Wrecker ring delay differs.");
            f.Engine.MapEffect(6, 0x00020001); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "vanaspati/ring") && !f.Engine.Arena!.Contains(c + Vector3.UnitX * 21, 0), "Wrecker shrink failed.");
        });
        run("Vanaspati dead bubble hidden from drawing retained in fire union until destruction", () =>
        {
            using var f = Prepare(0x33E9); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x3731, c + Vector3.UnitX * 8); Scan(f); Cast(f, 1, 25147);
            f.Actors[10] = f.Actors[10] with { Dead = true }; Scan(f);
            var enter = f.Engine.AmbientAoes().Single(a => a.Id == "vanaspati/fire/enter");
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "vanaspati/bubble/10")
                && !enter.Shape.Resolve(enter.Position, 0, null).Contains(f.Actors[10].Position, 0), "Dead unconsumed bubble lost from fire union.");
            f.Engine.RemoveActor(10); f.Actors.Remove(10); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "vanaspati/fire/enter"), "Destroyed dead bubble retained.");
        });
        run("Vanaspati Svarbhanu square ring eight point nine delay double views map shrink", () =>
        {
            using var f = Prepare(0x33EB); Cast(f, 1, 25170); var cast = f.Engine.Casts[1];
            f.Engine.Finish(1, completed: true); f.Render();
            var ring = f.Engine.AmbientAoes().Single(); var shape = ring.Shape.Resolve(ring.Position, 0, null); var world = HostDrawingGeometry.Convert(shape);
            Check(ring.Activation == cast.FinishAt.AddSeconds(8.9), "Svarbhanu ring delay differs.");
            Check(shape.Contains(ring.Position + Vector3.UnitX * 22, 0) && world.Contains(ring.Position + Vector3.UnitX * 22, 0)
                && !shape.Contains(ring.Position, 0) && !world.Contains(ring.Position, 0), "Square ring hole was filled.");
            f.Engine.MapEffect(7, 0x00020001); f.Render();
            Check(!f.Engine.AmbientAoes().Any() && !f.Engine.Arena!.Contains(ring.Position + Vector3.UnitX * 21, 0), "Svarbhanu shrink failed.");
        });
        foreach (var (pattern, blue) in new (uint, int[])[] { (0x00400020, [1, 2]), (0x00020001, [2, 3]), (0x00100008, [0, 3]), (0x01000080, [0, 1]) })
            foreach (var isBlue in new[] { true, false })
                run($"Vanaspati pattern {pattern:X} blue {isBlue} exact event clock and stripes", () =>
                {
                    using var f = Prepare(0x33EB); f.Engine.MapEffect(8, pattern);
                    f.Engine.Effect(1, isBlue ? 25165u : 25164u, 1, f.Now, occurredAt: f.Now.AddSeconds(-2)); f.Render();
                    var aoes = f.Engine.AmbientAoes().ToArray(); var expected = isBlue ? blue : Enumerable.Range(0, 4).Except(blue).ToArray();
                    Check(aoes.Length == 2 && aoes.All(a => Math.Abs((a.Activation!.Value - f.Start.AddSeconds(5.7)).TotalMilliseconds) < .001)
                        && aoes.All(a => expected.Any(lane => Math.Abs(a.Position.Z - new BossMod.WPos(280, -142 - lane * 10).Quantized().Z) < .0001f)), "Wrong lanes or original clock.");
                    foreach (var a in aoes) Compare(a.Shape, a.Position, a.Rotation, new BossMod.AOEShapeRect(40, 5));
                    f.Engine.Effect(1, 25162, 2, f.Now); f.Render();
                    Check(!f.Engine.AmbientAoes().Any(), "Stripe resolve did not clear.");
                    f.Engine.Effect(1, 25165, 3, f.Now); Check(!f.Engine.AmbientAoes().Any(), "Resolved pattern reused.");
                });
        foreach (var index in new uint[] { 0x0A, 0x0B })
            run($"Vanaspati cosmic map {index} nine real FIFO hits first three and duplicate", () =>
            {
                using var f = Prepare(0x33EB); f.Engine.MapEffect(index, 0x00020001, f.Start.AddSeconds(-1)); f.Render();
                for (var i = 0; i < 9; ++i)
                {
                    var aoes = f.Engine.AmbientAoes().ToArray();
                    Check(aoes.Length == Math.Min(3, 9 - i), "Cosmic visible/active count differs.");
                    var delay = i < 3 ? 4.3 : i < 6 ? 9.5 : 14.6;
                    Check(Math.Abs((aoes[0].Activation!.Value - f.Start.AddSeconds(delay - 1)).TotalMilliseconds) < .001, "Wave clock changed.");
                    Compare(aoes[0].Shape, aoes[0].Position, aoes[0].Rotation, new BossMod.AOEShapeRect(50, 5));
                    f.Engine.MapEffect(index, 0x00020001);
                    f.Engine.Effect(10, 25374, (uint)i, f.Now);
                    f.Engine.Effect(10, 25374, (uint)i, f.Now); f.Render();
                }
                Check(!f.Engine.AmbientAoes().Any() && f.Draw.Previews.Count == 0, "Cosmic final hit leaked.");
            });
        run("Vanaspati cosmic knockback hides direct navigation preserves drawing immunity and exact projected SDF", () =>
        {
            using var f = Prepare(0x33EB); var c = f.Engine.Arena!.Position;
            f.Engine.MapEffect(8, 0x00400020); f.Engine.Effect(1, 25165, 1, f.Now);
            f.Actors[10] = new(10, 0x233C, c); Scan(f); Cast(f, 10, 25169);
            var stripes = f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("vanaspati/stripe/")).ToArray();
            Check(stripes.Length == 2 && stripes.All(a => a.Display && !a.Navigate), "Stripes must stay drawn but release direct navigation.");
            var template = (VanaspatiKnockbackTemplate)f.Engine.AmbientAoes().Single(a => a.Id == "vanaspati/cosmic/landing").Shape;
            var geometry = template.Resolve(c, 0, null);
            var sdf = new BossMod.SDKnockbackInAABBSquareAwayFromOriginPlusAOERects(new(c.X, c.Z), new(c.X, c.Z), 13, 19,
                stripes.Select(s => (new BossMod.WPos(s.Position.X, s.Position.Z), new BossMod.Angle(s.Rotation).ToDirection())).ToArray(), 40, 5, 2);
            for (var x = -18.137f; x < 19; x += .41f)
                for (var z = -18.231f; z < 19; z += .41f)
                {
                    var p = c + new Vector3(x, 0, z);
                    Check(geometry.Contains(p, 0) == sdf.Contains(new(p.X, p.Z)), "Projected SDF differs.");
                }
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(20) } }; Scan(f);
            Check(f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("vanaspati/stripe/")).All(a => a.Navigate)
                && !f.Engine.AmbientAoes().Any(a => a.Id == "vanaspati/cosmic/landing"), "Immunity failed to restore direct navigation.");
        });
        foreach (var own in new[] { false, true })
            run($"Vanaspati stack own {own} real solver", () =>
            {
                using var f = Prepare(0x33E9); f.Actors[10] = new(10, 0x233C, f.Actors[1].Position); Scan(f);
                Cast(f, 10, 27851, own ? 100UL : 101UL);
                var hardJoin = f.Engine.AmbientAoes().Single(a => a.Id == "vanaspati/stack/join");
                Check(hardJoin.Activation == f.Engine.Casts[10].FinishAt && hardJoin.Navigate && !hardJoin.Safe
                    && hardJoin.Shape.Resolve(hardJoin.Position, 0, null).Contains(f.Actors[100].Position, 0), "Stack lost deadline or mandatory constraint.");
                Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) <= (own ? 3.01f : 6.01f));
                f.Engine.ResetFight(); f.Render(); Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Reset leaked.");
            });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 970); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, c, InCombat: true);
        f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitX * 10, PartySlot: 1, Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, .7f, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Compare(GeometryTemplate template, Vector3 origin, float rotation, BossMod.AOEShape source)
    {
        var shape = template.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -52.137f; x < 52; x += .71f)
            for (var z = -52.231f; z < 52; z += .71f)
            {
                var p = origin + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(origin.X, origin.Z), new(rotation));
                Check(shape.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Source shape differs.");
            }
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot; var player = f.Actors[f.PlayerId].Position;
        var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && accept(path.Count > 0 ? path[^1].Position : player), "Real solver failed.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
