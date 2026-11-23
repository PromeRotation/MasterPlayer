using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Firemane = BossMod.RealmReborn.Dungeon.D04Halatali.D041Firemane.D041Firemane;
using Guivre = BossMod.RealmReborn.Dungeon.D04Halatali.D042ThunderclapGuivre.D042ThunderclapGuivre;
using Tangata = BossMod.RealmReborn.Dungeon.D04Halatali.D043Tangata.D043Tangata;

internal static class HalataliRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Halatali original arena contours and Shock subtraction match BMR in both views", () =>
        {
            foreach (var (geometry, original) in new (IGeometryShape, BossMod.ArenaBoundsCustom)[]
            {
                (HalataliArenas.Firemane(7), Firemane.arena),
                (HalataliArenas.Thunderclap(7), Guivre.arena),
                (HalataliArenas.Tangata(7), Tangata.arena),
            })
            {
                var host = HostDrawingGeometry.Convert(geometry);
                var bounds = geometry.GetBounds();
                Check(Math.Abs(geometry.Position.X - original.Center.X) < .001f
                    && Math.Abs(geometry.Position.Z - original.Center.Z) < .001f, "Arena/radar origin shifted.");
                for (var x = bounds.MinX + .137f; x < bounds.MaxX; x += .7f)
                    for (var z = bounds.MinZ + .231f; z < bounds.MaxZ; z += .7f)
                    {
                        var expected = original.Contains(new(x - original.Center.X, z - original.Center.Z));
                        Check(geometry.Contains(new(x, 7, z), 0) == expected, $"Arena parity at {x}, {z}.");
                        Check(host.Contains(new(x, 7, z), 0) == expected, "World drawing conversion changed arena.");
                    }
            }
            var shock = HalataliArenas.Shock(7);
            var world = HostDrawingGeometry.Convert(shock);
            for (var x = -205f + .137f; x < -153; x += .7f)
                for (var z = -167f + .231f; z < -99; z += .7f)
                {
                    var expected = Guivre.Shock.Check(new(x, z), Guivre.arena.Center, default);
                    Check(shock.Contains(new(x, 7, z), 0) == expected, $"Water subtraction parity at {x}, {z}.");
                    Check(world.Contains(new(x, 7, z), 0) == expected, "World view filled a safe shore.");
                }
            Check(shock.GetContours().Count == 1, "Safe shore subtraction must form notches, not filled hole contours.");
            Check(!shock.Contains(new(-183, 7, -108)) && !shock.Contains(new(-174, 7, -160))
                && shock.Contains(new(-180, 7, -135)), "Safe shores/water were reversed.");
        });
        run("Halatali Fireflow sorts reversed helpers and promotes the second wave after each finish", () =>
        {
            using var f = new Fight(0x4643);
            for (var i = 0; i < 8; ++i)
            {
                var id = (ulong)(10 + i);
                f.Actors[id] = new(id, 0x233C, f.Center);
                f.Scan();
                f.Cast(id, i < 4 ? 40589u : 40588u, i < 4 ? 9 : 6, i * MathF.PI / 4);
            }
            Check(f.Engine.Casts.Count == 8 && f.Draw.Previews.Count == 4 && f.Snapshot.DangerAreas.Count == 4, "Expected first four active cones only.");
            Check(f.Engine.OutputCasts.All(c => c.Value.Rule.Action == 40588), "Late helper arrival order replaced activation order.");
            var output = f.Engine.OutputCasts.First();
            var shape = f.Draw.Previews[output.Key].Geometry;
            var cast = output.Value;
            var direction = new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation));
            Check(shape.Contains(cast.Location + direction * 59), "Cone range/locked rotation lost.");
            Check(f.Snapshot.DangerAreas.All(d => d.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(6.3)), "First group deadline shifted.");
            f.Complete(14, 6);
            Check(f.Engine.OutputCasts.Count(c => c.Value.Rule.Action == 40589) == 1, "Next cone was not promoted.");
            foreach (var id in new ulong[] { 15, 16, 17 }) f.Complete(id, 6);
            Check(f.Draw.Previews.Count == 4 && f.Engine.OutputCasts.All(c => c.Value.Rule.Action == 40589), "Second group disappeared.");
            Check(f.Snapshot.DangerAreas.All(d => d.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(9.3)), "Second group inherited first deadline.");
            foreach (var id in new ulong[] { 10, 11, 12, 13 }) f.Complete(id, 9);
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Finished helper leaked.");
        });
        run("Halatali Tangata concentric sequence advances across different helpers without predicting all rings at once", () =>
        {
            using var f = new Fight(0x4645);
            f.Actors[10] = new(10, 0x233C, f.Center);
            f.Actors[11] = new(11, 0x233C, f.Center);
            f.Scan();
            f.Cast(11, 40601, 11);
            f.Cast(10, 40600, 8);
            Check(f.Draw.Previews.Count == 0, "Helpers started a concentric sequence before its boss.");
            f.Cast(1, 40599, 5);
            Check(f.Draw.Previews.Count == 1 && f.Draw.Previews.Single().Value.Geometry is CircleGeometry, "First ring should be the only current display.");
            f.Complete(1, 5);
            Check(f.Engine.OutputCasts.Single().Value.Rule.Action == 40600, "Boss finish did not advance to helper ring.");
            Check(f.Engine.OutputCasts.Single().Value.FinishAt == f.Start.AddSeconds(8), "Next activation must use the 3-second BMR prediction.");
            Check(f.Draw.Previews.Single().Value.Geometry is DonutGeometry { InnerRadius: 10, OuterRadius: 20 }, "Second ring geometry changed.");
            var before = f.Engine.OutputCasts.Single().Value;
            f.Engine.Effect(1, 40599, 42, f.Now);
            f.Engine.Effect(1, 40599, 42, f.Now);
            Check(ReferenceEquals(before, f.Engine.OutputCasts.Single().Value), "Repeated effects advanced the sequence.");
            f.Complete(10, 8);
            Check(f.Engine.OutputCasts.Single().Value.Rule.Action == 40601
                && f.Engine.OutputCasts.Single().Value.FinishAt == f.Start.AddSeconds(11), "Second helper finish lost the final ring.");
            Check(f.Draw.Previews.Single().Value.Geometry is DonutGeometry { InnerRadius: 20, OuterRadius: 30 }, "Third ring geometry changed.");
            f.Complete(11, 11);
            Check(!f.Engine.OutputCasts.Any() && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Final ring leaked.");
        });
        run("Halatali Tangata cancellation, missing effects and death release sequence and boundary", () =>
        {
            using var f = new Fight(0x4645);
            f.Cast(1, 40599, 5);
            f.Now = f.Start.AddSeconds(1);
            f.Engine.Finish(1, 40599, f.Now);
            f.Scan();
            Check(!f.Engine.OutputCasts.Any() && f.Draw.Previews.Count == 0, "Cancelled first cast left future rings.");
            f.Now = f.Start.AddSeconds(2);
            f.Cast(1, 40599, 5);
            f.Complete(1, 7);
            Check(f.Engine.OutputCasts.Single().Value.Rule.Action == 40600, "Missing effect packet blocked scan-based completion.");
            f.Actors[1] = f.Actors[1] with { Dead = true };
            f.Scan();
            Check(f.Engine.Active == null && f.Snapshot.ArenaBoundary == null && f.Draw.Previews.Count == 0, "Death retained a sequence.");
        });
        run("Halatali Tangata sequence matches helper XZ without depending on packet height", () =>
        {
            using var f = new Fight(0x4645);
            f.Actors[10] = new(10, 0x233C, f.Center);
            f.Scan();
            f.Cast(10, 40600, 8);
            var elevated = f.Actors[10].Cast! with { Location = f.Center + Vector3.UnitY * 20 };
            f.Actors[10] = f.Actors[10] with { Cast = elevated };
            f.Engine.StartCast(10, elevated, f.Now);
            f.Cast(1, 40599, 5);
            f.Complete(1, 5);
            f.Complete(10, 8);
            Check(f.Engine.OutputCasts.Single().Value.Rule.Action == 40601, "A different helper Y blocked planar sequence matching.");
        });
        run("Halatali Tangata current helper disappearance cancels its predicted sequence", () =>
        {
            using var f = new Fight(0x4645);
            f.Actors[10] = new(10, 0x233C, f.Center);
            f.Scan();
            f.Cast(10, 40600, 8);
            f.Cast(1, 40599, 5);
            f.Complete(1, 5);
            f.Actors.Remove(10);
            f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "A vanished current helper left a predicted ring.");
        });
        run("Halatali BurningWard handles multiple enemies, immediate status removal and stale scans", () =>
        {
            using var f = new Fight(0x4645);
            f.Actors[10] = new(10, 0x4646, f.Center, Statuses: [4175]);
            f.Actors[11] = new(11, 0x4647, f.Center);
            f.Actors[12] = new(12, 0, f.Center, Statuses: [4175], Hostile: false);
            f.Scan();
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 1, StatusId = 4175, Timestamp = f.Now }, f.Now);
            Check(f.Engine.ForbiddenTargets.Order().SequenceEqual(new ulong[] { 1, 10 }), "Shield restriction must target all shielded enemies and preserve other targets.");
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 10, StatusId = 4175, Timestamp = f.Now }, f.Now);
            f.Scan();
            Check(f.Engine.ForbiddenTargets.SequenceEqual([1UL]), "Stale actor snapshot resurrected removed shield.");
            f.Actors[10] = f.Actors[10] with { Statuses = [] };
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 1, StatusId = 4175, Timestamp = f.Now }, f.Now);
            f.Now = f.Now.AddSeconds(.6);
            f.Scan();
            Check(f.Engine.ForbiddenTargets.Length == 0, "Shield ending left ACR restrictions.");
            f.Actors[10] = f.Actors[10] with { Statuses = [4175] };
            f.Scan();
            f.Events.Handle(new LogSystemObjectRemoveEvent { SourceId = 10, Timestamp = f.Now }, f.Now);
            Check(f.Engine.ForbiddenTargets.Length == 0, "Removed shielded actor remained forbidden until next scan.");
        });
        run("Halatali Shock actual temporal solver reaches a shore within arena", () =>
        {
            using var f = new Fight(0x4644);
            f.Actors[10] = new(10, 0x233C, f.Center);
            f.Scan();
            f.Cast(10, 41113, 10);
            var snapshot = f.Snapshot;
            var player = new Vector3(-180, 0, -135);
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback, "No safe shore route.");
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(path.Count >= 2, "Planner did not move out of water.");
            Check(path.All(p => f.Engine.Arena!.Contains(p.Position)), "Route escaped through arena wall.");
            Check(!HalataliArenas.Shock(0).Contains(path[^1].Position), "Route ended in water.");
        });
        foreach (var (boss, action, radius) in new[] { (0x4643u, 40592u, 5f), (0x4644u, 40595u, 6f), (0x4645u, 41123u, 3f) })
            run($"Halatali AID {action} location circle, display size and independent navigation margin", () =>
            {
                using var f = new Fight(boss);
                f.Cast(1, action, 4);
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                Check(geometry is CircleGeometry circle && circle.Radius == radius, "Preview size changed.");
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Center + Vector3.UnitX * (radius + .4f)), "Navigation margin lost.");
                Check(f.Safe.NavigationOnly.Count == 1, "Navigation must not duplicate world/radar preview.");
            });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal sealed class Fight : IDisposable
    {
        public readonly DateTime Start = DateTime.UtcNow;
        public DateTime Now;
        public readonly Vector3 Center;
        public readonly Dictionary<ulong, DungeonActor> Actors = [];
        public readonly DungeonEncounter Engine;
        public readonly ScriptSafeZoneApi Safe = new() { DangerMarginForTest = .5f };
        public readonly ScriptDrawApi Draw = new();
        public readonly DungeonOutput Output;
        public readonly DungeonEventAdapter Events;
        public ulong PlayerId = 100;
        public Fight(uint boss, uint territory = 1245)
        {
            Now = Start;
            Engine = new(territory);
            Center = boss switch
            {
                0x4643 => new(30, 0, 130), 0x4644 => new(-180, 0, -135), 0x4645 => new(-258, 0, 17),
                0x1A7 => new(114, 0, -14), 0x3DE => new(-7, 0, -86), 0x1AB => new(-112, 0, -34), 0x38C5 => new(-26, 0, -236),
                0x103 => new(215, 0, -145), _ => Vector3.Zero,
            };
            Actors[1] = new(1, boss, Center, InCombat: true);
            Output = new(Safe, Draw, Safe.Manager);
            Events = new(Engine, Scan, _ => 0, message => throw new Exception(message));
            Scan();
        }
        public SafeZoneSnapshot Snapshot => Safe.Manager.BuildSnapshot(Now);
        public void Scan() { Engine.Synchronize(Actors.Values, Center, Now, playerId: PlayerId); Render(); }
        public void Render()
        {
            Safe.UtcNowForTest = Now;
            Output.Apply(Engine, Now, false);
        }
        public void Cast(ulong source, uint action, double duration, float rotation = 0)
        {
            var cast = new DungeonCastObservation(action, Center, rotation, Now, Now.AddSeconds(duration + .3));
            Actors[source] = Actors[source] with { Cast = cast };
            Engine.StartCast(source, cast, Now);
            Render();
        }
        public void Complete(ulong source, double seconds)
        {
            Now = Start.AddSeconds(seconds);
            Actors[source] = Actors[source] with { Cast = null };
            Scan();
        }
        public void Dispose() { Output.Clear(); Safe.Dispose(); }
    }
}
