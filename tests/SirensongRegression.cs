using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;
using Source = BossMod.Stormblood.Dungeon.D01SirensongSea;

internal static class SirensongRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (type, factory) in new (Type, Func<float, IGeometryShape>)[]
        {
            (typeof(Source.D011Lugat.D011Lugat), SirensongArenas.Lugat),
            (typeof(Source.D012TheGovernor.DO12TheGovernor), SirensongArenas.Governor),
            (typeof(Source.D013Lorelei.D013Lorelei), SirensongArenas.Lorelei),
        })
            run($"Sirensong exact {type.Name} arena matches source in both views", () =>
            {
                var tuple = type.GetMethod("BuildArena", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
                var source = (BossMod.ArenaBoundsCustom)tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
                CompareArena(source, factory(0));
            });
        run("Sirensong exact small Lorelei arena matches resolved twenty-four edges", () =>
            CompareArena(new BossMod.ArenaBoundsCustom([new BossMod.Polygon(new(-44.49977f, 465.00049f), 15.95688f, 24)]), SirensongArenas.LoreleiSmall(0)));
        run("Sirensong territory owns all three bosses without matching old territory", () =>
        {
            foreach (var oid in new uint[] { 0x1AFB, 0x1AFC, 0x1AFE })
                Check(ArrDungeonCatalog.Owns(oid, 1142) && !ArrDungeonCatalog.Owns(oid, 626), "Ownership differs.");
        });
        foreach (var (boss, caster, action, radius) in new[]
        {
            (0x1AFBu, 0x1AFBu, 8022u, 24.5f), (0x1AFBu, 0x1AFBu, 8026u, 7f),
            (0x1AFBu, 0x18D6u, 8027u, 8f), (0x1AFEu, 0x1AFEu, 8040u, 8f),
        })
            run($"Sirensong cast {action} lock clock both views single margin and cancellation", () =>
            {
                using var f = Prepare(boss);
                var source = boss == caster ? 1UL : 10UL;
                if (source != 1) { f.Actors[source] = new(source, caster, Center(boss)); Scan(f); }
                Cast(f, source, action, 4, MathF.PI / 2);
                var shape = f.Draw.Previews.Single().Value.Geometry;
                var origin = f.Engine.Casts[source].Location;
                Check(shape.Contains(origin + Vector3.UnitX * (radius - .01f))
                    && HostDrawingGeometry.Convert(shape).Contains(origin + Vector3.UnitX * (radius - .01f))
                    && !shape.Contains(origin + Vector3.UnitX * (radius + .01f)), "Geometry extent differs.");
                var area = f.Snapshot.DangerAreas.Single();
                Check(area.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.3)
                    && area.Geometry.Contains(origin + Vector3.UnitX * (radius + .4f)), "Clock or margin differs.");
                f.Engine.Finish(source); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled cast leaked.");
            });
        run("Sirensong Lugat stack original icon time and effect fallback", () =>
        {
            using var f = Prepare(0x1AFB);
            f.Now = f.Start.AddSeconds(.2); f.Engine.Tick(f.Now);
            f.Engine.Icon(100, 62, f.Start); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 1 && f.Draw.Previews.Single().Value.Safe, "Hydroball stack missing.");
            f.Engine.Effect(1, 8023, 1, f.Now, 1); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0, "Stack self-target fallback failed.");
        });
        run("Sirensong Governor waits six actual cone events and ignores duplicate packets", () =>
        {
            using var f = Prepare(0x1AFC);
            AddShadows(f);
            for (uint i = 0; i < 5; ++i) Shadow(f, i);
            f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Partial ShadowFlow visible.");
            Shadow(f, 4); f.Render(); Check(f.Draw.Previews.Count == 0, "Duplicate counted toward six.");
            Shadow(f, 5); f.Render();
            Check(f.Draw.Previews.Count == 13 && f.Snapshot.DangerAreas.Count == 13, "Six cones plus center and six adds missing.");
            var cone = f.Engine.AmbientAoes().Single(a => a.Id == "siren/shadow/cone/0");
            Check(cone.Activation == f.Start.AddSeconds(8) && MathF.Abs(cone.Rotation - .2f) < .0001f,
                "Packet clock or effect rotation ignored.");
            var circle = f.Engine.AmbientAoes().Single(a => a.Id == "siren/shadow/add/10");
            var locked = circle.Position;
            f.Actors[10] = f.Actors[10] with { Position = Center(0x1AFC) + Vector3.UnitX * 9 }; Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Id == circle.Id).Position == locked, "Shadow add circle followed instead of locking.");
            f.Now = f.Start.AddSeconds(8); f.Engine.Tick(f.Now); f.Render(); Check(f.Draw.Previews.Count == 13, "Strict first-activation expiry became inclusive.");
            f.Now = f.Start.AddSeconds(8.001); f.Engine.Tick(f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Shadow wave did not expire.");
            Shadow(f, 10); f.Render(); Check(f.Draw.Previews.Count == 0, "Next partial wave inherited old count.");
        });
        run("Sirensong Governor pull dynamically follows boss and respects immunity expiry", () =>
        {
            using var f = Prepare(0x1AFC);
            f.Actors[100] = f.Actors[100] with { Position = Center(0x1AFC) + Vector3.UnitX * 10 }; Scan(f);
            f.Engine.Icon(100, 22, f.Start); f.Render();
            Check(f.Engine.WantsSirenKnockbackImmunity && f.Draw.Previews.Count == 2, "Pull or immunity request missing.");
            var landing = f.Engine.AmbientAoes().Single(a => a.Id == "siren/pull/landing").Position;
            Check(Vector3.Distance(landing, f.Actors[1].Position) < .001f, "Pull overshot source.");
            f.Actors[1] = f.Actors[1] with { Position = Center(0x1AFC) + Vector3.UnitZ * 2 }; Scan(f);
            Check(Vector3.Distance(f.Engine.AmbientAoes().Single(a => a.Id == "siren/pull/landing").Position, f.Actors[1].Position) < .001f,
                "Boss interpolation did not update pull.");
            f.Actors[100] = f.Actors[100] with { Statuses = [1209], StatusExpirations = new Dictionary<uint, DateTime> { [1209] = f.Start.AddSeconds(3) } }; Scan(f);
            Check(f.Engine.WantsSirenKnockbackImmunity, "Exactly expiring immunity incorrectly accepted.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [1209] = f.Start.AddSeconds(4) } }; Scan(f);
            Check(!f.Engine.WantsSirenKnockbackImmunity && f.Draw.Previews.Count == 0, "Covering immunity retained pull.");
        });
        run("Sirensong Governor tether sixteen-yard deadline remains even with pull immunity", () =>
        {
            using var f = Prepare(0x1AFC);
            f.Actors[100] = f.Actors[100] with { Position = Center(0x1AFC) + Vector3.UnitX * 10 }; Scan(f);
            f.Engine.Icon(100, 22, f.Start);
            f.Engine.Tether(100, 61, true, f.Start, 1); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 2
                && f.Snapshot.DangerAreas.Any(a => a.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.3)), "Tether activation differs.");
            f.Actors[100] = f.Actors[100] with { Statuses = [1209], StatusExpirations = new Dictionary<uint, DateTime> { [1209] = f.Start.AddSeconds(10) } }; Scan(f);
            Check(f.Snapshot.DangerAreas.Count == 1 && f.Draw.Previews.Count == 2, "Tether incorrectly disabled by immunity.");
            f.Engine.Tether(100, 61, false, f.Now, 1); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Untether retained pull/stretch.");
        });
        run("Sirensong Governor restores snapshot tether then clears missing snapshot", () =>
        {
            using var f = Prepare(0x1AFC);
            f.Actors[1] = f.Actors[1] with { Tethers = new Dictionary<uint, ulong> { [61] = 100 } }; Scan(f);
            Check(f.Snapshot.DangerAreas.Count == 1, "Snapshot tether missing.");
            f.Actors[1] = f.Actors[1] with { Tethers = null }; f.Now = f.Start.AddSeconds(1); Scan(f);
            Check(f.Snapshot.DangerAreas.Count == 0, "Missing snapshot tether leaked.");
        });
        run("Sirensong Lorelei fourth unshrunk tears predicts plus point-seven ring and state shrinks", () =>
        {
            using var f = Prepare(0x1AFE);
            for (var i = 0; i < 4; ++i)
            {
                f.Now = f.Start.AddSeconds(i * 10); Cast(f, 1, 8041, 3);
                var ring = f.Engine.AmbientAoes().FirstOrDefault(a => a.Id == "siren/ring");
                Check((i < 3 && ring == null) || i == 3 && ring?.Activation is { } at
                    && Math.Abs((at - f.Now.AddSeconds(3.7)).TotalMilliseconds) < .001, "Tears count or clock differs.");
                f.Engine.Finish(1, completed: true); f.Actors[1] = f.Actors[1] with { Cast = null };
            }
            f.Engine.EventState(500, 2); f.Render();
            Check(f.Draw.Previews.Count == 0 && !f.Engine.Arena!.Contains(Center(0x1AFE) + Vector3.UnitX * 17), "Small arena or ring cleanup failed.");
            f.Engine.EventState(500, 1); f.Render();
            Check(f.Engine.Arena!.Contains(Center(0x1AFE) + Vector3.UnitX * 17), "Arena reset failed.");
        });
        run("Sirensong Lorelei tears packet correction does not increment cast counter", () =>
        {
            using var f = Prepare(0x1AFE);
            Cast(f, 1, 8041, 3);
            f.Engine.StartCast(1, f.Actors[1].Cast! with { Location = Center(0x1AFE) + Vector3.UnitX }, f.Now);
            f.Engine.Finish(1, completed: true); f.Now = f.Start.AddSeconds(10); Cast(f, 1, 8041, 3);
            f.Engine.Finish(1, completed: true); f.Now = f.Start.AddSeconds(20); Cast(f, 1, 8041, 3); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "siren/ring"), "Correction was counted as a separate tears cast.");
        });
        run("Sirensong Lorelei mine EventState seven and small arena snapshot precede march", () =>
        {
            using var f = Prepare(0x1AFE);
            f.Actors[10] = new(10, 0x1EA300, Center(0x1AFE) + Vector3.UnitX * 8);
            f.Actors[20] = new(20, 0x1EA2FF, Center(0x1AFE), EventState: 2);
            Scan(f);
            Check(f.Snapshot.DangerAreas.Count == 1 && !f.Engine.Arena!.Contains(Center(0x1AFE) + Vector3.UnitX * 17), "Mine or restored arena missing.");
            f.Engine.EventState(10, 7); f.Render(); Check(f.Snapshot.DangerAreas.Count == 0, "Disabled mine still navigated.");
        });
        foreach (var action in new uint[] { 8037, 8038 })
            run($"Sirensong Lorelei {action} marches 19.8 yards using PR facing and original deadline", () =>
            {
                using var f = Prepare(0x1AFE);
                f.Actors[100] = f.Actors[100] with { Position = Center(0x1AFE) + Vector3.UnitZ * 4, Rotation = action == 8037 ? 0 : MathF.PI };
                f.Actors[10] = new(10, 0x1EA300, Center(0x1AFE) + new Vector3(10, 0, 0)); Scan(f); Cast(f, 1, action, 5);
                var motion = f.Engine.AmbientAoes().Single(a => a.Id == "siren/march/avoid");
                Check(motion.Activation is { } at && Math.Abs((at - f.Start.AddSeconds(6)).TotalMilliseconds) < .001,
                    "Forced march activation must be finish plus one second, not packet delay twice.");
                var landing = f.Engine.AmbientAoes().Single(a => a.Id == "siren/march/landing").Position;
                Check(MathF.Abs(Vector3.Distance(landing, f.Actors[100].Position) - 19.8f) < .001f, "Forced speed/duration differs.");
                Check(f.Engine.GazeFacing(f.Actors[100].Position, f.Actors[100].Rotation) == null, "Facing was applied too early.");
                f.Now = f.Start.AddSeconds(5.3); f.Engine.Tick(f.Now);
                var face = f.Engine.GazeFacing(f.Actors[100].Position, f.Actors[100].Rotation);
                Check(face != null, "Safe forced march facing missing.");
                var offset = action == 8038 ? MathF.PI : 0;
                var endpoint = f.Actors[100].Position + new Vector3(MathF.Sin(face!.Value + offset), 0, MathF.Cos(face.Value + offset)) * 19.8f;
                Check(f.Engine.Arena!.Contains(endpoint, 0) && !new CircleGeometry(f.Actors[10].Position, 7).Contains(endpoint),
                    "PR-facing destination unsafe.");
                f.Engine.Finish(1, completed: true); f.Render();
                Check(f.Engine.AmbientAoes().Any(a => a.Id == "siren/march/avoid"), "Natural finish cleared pending march before forced status.");
            });
        run("Sirensong Lorelei forced status consumes pending rotation holds movement and shrinks remaining path", () =>
        {
            using var f = Prepare(0x1AFE);
            f.Actors[100] = f.Actors[100] with { Position = Center(0x1AFE) + Vector3.UnitZ * 4, Rotation = MathF.PI }; Scan(f);
            Cast(f, 1, 8038, 5);
            f.Now = f.Start.AddSeconds(6);
            f.Engine.StatusChanged(100, 5174, true, f.Now, expiresAt: f.Start.AddSeconds(9)); f.Engine.Tick(f.Now); f.Render();
            Check(f.Engine.RequireSirenMovementHold && f.Engine.GazeFacing(f.Actors[100].Position, 0) == null, "Active march competed with MP facing/movement.");
            var landing = f.Engine.AmbientAoes().Single(a => a.Id == "siren/march/landing").Position;
            Check(landing.Z < Center(0x1AFE).Z, "Retreat offset was applied twice after forced actor rotation.");
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "siren/march/staging"), "Active march retained staging movement.");
            f.Now = f.Start.AddSeconds(8); f.Engine.Tick(f.Now); f.Render();
            landing = f.Engine.AmbientAoes().Single(a => a.Id == "siren/march/landing").Position;
            Check(MathF.Abs(Vector3.Distance(landing, f.Actors[100].Position) - 6.6f) < .001f, "Active march remaining duration ignored.");
            f.Engine.StatusChanged(100, 5174, false, f.Now); f.Render();
            Check(!f.Engine.RequireSirenMovementHold && !f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("siren/march")), "Status loss resurrected pending march.");
        });
        run("Sirensong Lorelei forced NPC status restores without start packet and releases on viewer death", () =>
        {
            using var f = Prepare(0x1AFE);
            f.Actors[100] = f.Actors[100] with { Statuses = [3629],
                StatusExpirations = new Dictionary<uint, DateTime> { [3629] = f.Now.AddSeconds(3) } }; Scan(f);
            Check(f.Engine.RequireSirenMovementHold && f.Draw.Previews.Count == 2, "Forced snapshot recovery missing.");
            f.Actors[101] = new(101, 0, Center(0x1AFE), PartySlot: 1, Hostile: false);
            f.PlayerId = 101; Scan(f); Check(!f.Engine.RequireSirenMovementHold && f.Draw.Previews.Count == 0, "Old viewer forced control leaked.");
            f.PlayerId = 100; f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f);
            Check(!f.Engine.RequireSirenMovementHold, "Dead viewer movement remained held.");
        });
        run("Sirensong Lorelei march cancellation and reset clear all previews constraints and controls", () =>
        {
            using var f = Prepare(0x1AFE); Cast(f, 1, 8037, 5);
            f.Engine.Finish(1); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled march leaked.");
            f.Now = f.Start.AddSeconds(20); Cast(f, 1, 8037, 5);
            f.Engine.ResetFight(); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0 && !f.Engine.RequireSirenMovementHold, "Reset leaked.");
        });
        run("Sirensong real solver enters strong three-to-five-yard march preparation ring", () =>
        {
            using var f = Prepare(0x1AFE);
            f.Actors[100] = f.Actors[100] with { Position = Center(0x1AFE), Rotation = 0 }; Scan(f); Cast(f, 1, 8037, 5);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(Center(0x1AFE), 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            var distance = path.Count > 0 ? Vector3.Distance(path[^1].Position, Center(0x1AFE)) : 0;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && distance is >= 3 and <= 5,
                $"March staging solver failed: {solution.Status}, endpoint radius {distance}.");
        });
        run("Sirensong utility immunity uses actual combat role and rejects crafting jobs", () =>
        {
            Check(SirensongCatalog.KnockbackAction(24) == 7559 && SirensongCatalog.KnockbackAction(42) == 7559
                && SirensongCatalog.KnockbackAction(19) == 7548 && SirensongCatalog.KnockbackAction(41) == 7548
                && SirensongCatalog.KnockbackAction(8) == 0, "Immunity action job filtering missing.");
        });
        FollowRegression.RunSirensongControls(run);
    }

    private static void CompareArena(BossMod.ArenaBoundsCustom source, IGeometryShape native)
    {
        var host = HostDrawingGeometry.Convert(native);
        var bounds = native.GetBounds();
        for (var x = bounds.MinX + .137f; x < bounds.MaxX; x += .47f)
            for (var z = bounds.MinZ + .273f; z < bounds.MaxZ; z += .47f)
            {
                var p = new Vector3(x, 0, z);
                var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                Check(native.Contains(p) == expected && host.Contains(p) == expected, $"Arena differs at {p}.");
            }
    }
    private static void AddShadows(Fight f)
    {
        for (ulong i = 10; i < 16; ++i) f.Actors[i] = new(i, 0x1AFD, Center(0x1AFC) + Vector3.UnitX * (i - 9));
        Scan(f);
    }
    private static void Shadow(Fight f, uint index) => f.Engine.Effect(10 + index % 6, 8031, index + 1, f.Now,
        occurredAt: f.Start.AddSeconds(index < 6 ? index * .01 : 9), sourceRotation: .2f + index);
    private static Vector3 Center(uint boss) => boss switch { 0x1AFB => new(-2.7f, 0, -217), 0x1AFC => new(-8, 0, 79), _ => new(-44.5f, 0, 465) };
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1142); f.Engine.ResetFight();
        f.Actors[1] = new(1, boss, Center(boss), InCombat: true);
        f.Actors[100] = new(100, 0, Center(boss), PartySlot: 0, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors.GetValueOrDefault(f.PlayerId)?.Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double duration, float rotation = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3));
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
