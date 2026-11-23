using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Source = BossMod.Heavensward.Dungeon.D09SaintMociannesArboretum.D090MalboroTerra.D090MalboroTerra;
using Fight = HalataliRegression.Fight;

internal static class MalboroRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (shape, field) in new[]
        {
            (MalboroArenas.First(0), "Arena1"), (MalboroArenas.Open(0), "Arena1B"), (MalboroArenas.Second(0), "Arena2"),
        })
            run($"Malboro {field} exact source radar and world geometry", () =>
            {
                var source = (BossMod.ArenaBoundsCustom)typeof(Source).GetField(field, BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
                var world = HostDrawingGeometry.Convert(shape);
                var bounds = shape.GetBounds();
                for (var x = bounds.MinX + .137f; x < bounds.MaxX; x += .57f)
                    for (var z = bounds.MinZ + .271f; z < bounds.MaxZ; z += .57f)
                    {
                        var p = new Vector3(x, 0, z);
                        Check(shape.Contains(p) == world.Contains(p), "World shape differs.");
                        var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                        if (shape.Contains(p) == expected) continue;
                        var edge = shape.GetContours().Min(c => Enumerable.Range(0, c.Count).Min(i =>
                            GeometryHelper.DistanceSquaredPointToSegment2D(new(x, z),
                                new(c[i].X, c[i].Z), new(c[(i + 1) % c.Count].X, c[(i + 1) % c.Count].Z))));
                        Check(edge < .000001f, $"Source geometry differs at {p}.");
                    }
            });
        foreach (var oid in ArboretumCatalog.MALBORO)
            run($"Malboro {oid:X} starts without targetability and survives total group death and removal", () =>
            {
                var engine = new DungeonEncounter(511);
                var a = new DungeonActor(1, oid, new(65, 0, 105), Targetable: false, InCombat: true);
                engine.Synchronize([a], a.Position, DateTime.UtcNow);
                Check(engine.Active?.BossOid == 0x141F && engine.Arena != null, "Group did not start.");
                engine.Synchronize([a with { Dead = true, InCombat = false }], a.Position, DateTime.UtcNow);
                Check(engine.Active?.BossOid == 0x141F, "Death prematurely ended stage.");
                engine.RemoveActor(1);
                engine.Synchronize([], a.Position, DateTime.UtcNow);
                Check(engine.Active?.BossOid == 0x141F, "Removal prematurely ended stage.");
                engine.LegacyMapEffect(1, 0, [1]);
                Check(engine.Active == null && engine.Arena == null, "Legacy completion did not end stage.");
                engine.Synchronize([a], a.Position, DateTime.UtcNow);
                Check(engine.Active == null, "Completed stage restarted from stale combat.");
            });
        run("Malboro completion survives personal death reset and stale combat after revival", () =>
        {
            using var f = new Fight(0x141F, 511);
            f.Engine.Synchronize(f.Actors.Values, null, f.Now, playerId: 100);
            Check(f.Engine.Active == null, "Personal death retained output.");
            f.Events.Handle(new LegacyMapEffectEvent { Sequence = 1, Data = [1] }, f.Now);
            f.Scan();
            Check(f.Engine.Active == null && f.Snapshot.ArenaBoundary == null, "Completion during death was lost.");
        });
        run("Malboro wall state uses strict Z16 crossing and reversible arenas", () =>
        {
            using var f = new Fight(0x141F, 511);
            f.Actors[10] = new(10, 0x1E9DF8, Vector3.Zero);
            void Scan(float z)
            {
                f.Engine.Synchronize(f.Actors.Values, new(114, 0, z), f.Now, playerId: 100);
                f.Render();
            }
            bool Arena(string name) => f.Engine.Arena!.GetBounds().Equals((name switch
            {
                "first" => MalboroArenas.First(0), "open" => MalboroArenas.Open(0), _ => MalboroArenas.Second(0),
            }).GetBounds());
            Scan(15); Check(Arena("first"), "Unopened wall allowed transition.");
            f.Engine.Animation(10, 0x00040004); Check(Arena("first"), "Wrong animation opened wall.");
            Scan(17); f.Engine.Animation(10, 0x00040008);
            Check(Arena("open"), "Wall did not open.");
            Scan(16); Check(Arena("open"), "Equality crossed forward.");
            Scan(15); Check(Arena("second"), "Forward crossing failed.");
            Scan(16); Check(Arena("second"), "Equality crossed backward.");
            Scan(17); Check(Arena("open"), "Reverse crossing failed.");
        });
        run("Malboro legacy input guards empty wrong sequence parameter payload and normal MapEffect", () =>
        {
            using var f = new Fight(0x141F, 511);
            foreach (var ev in new LegacyMapEffectEvent[]
            {
                new() { Sequence = 1, Data = [] }, new() { Sequence = 2, Data = [1] },
                new() { Sequence = 1, Param = 1, Data = [1] }, new() { Sequence = 1, Data = [0, 1] },
            }) f.Events.Handle(ev, f.Now);
            f.Engine.MapEffect(1, 1, f.Now);
            Check(f.Engine.Active != null, "Unrelated event ended stage.");
            f.Events.Handle(new LegacyMapEffectEvent { Sequence = 1, Data = [1, 2] }, f.Now);
            f.Render();
            Check(f.Engine.Active == null && f.Snapshot.ArenaBoundary == null, "Legacy route did not clear arena.");
        });
        foreach (var (action, radius) in new[] { (4532u, 6f), (5355u, 6f) })
            run($"Malboro {action} frozen geometry packet correction timing owner death and explicit finish", () =>
            {
                using var f = new Fight(0x141F, 511);
                f.Actors[10] = new(10, 0xFFFF, Vector3.Zero);
                f.Scan();
                f.Cast(10, action, 4, MathF.PI / 2);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Action ID was incorrectly filtered by source OID.");
                var original = f.Engine.OutputCasts.Single().Value;
                Check(original.UntilObservedEnd && original.FinishAt == f.Start.AddSeconds(4.3), "Cast clock differs.");
                var area = f.Snapshot.DangerAreas.Single();
                Check(area.DangerWindows.Single().DangerUntil == null, "Cast expires without observed end.");
                var p = Vector3.UnitX * (radius - .01f);
                Check(f.Draw.Previews.Single().Value.Geometry.Contains(p)
                    && HostDrawingGeometry.Convert(f.Draw.Previews.Single().Value.Geometry).Contains(p), "Display extent differs.");
                Check(area.Geometry.Contains(Vector3.UnitX * (radius + .4f)), "Navigation margin missing.");
                var correction = f.Actors[10].Cast! with { Location = Vector3.UnitZ * 10, Rotation = 0 };
                f.Engine.StartCast(10, correction, f.Now);
                f.Actors[10] = f.Actors[10] with { Position = Vector3.UnitX * 100, Rotation = 3 };
                f.Scan();
                Check(f.Engine.OutputCasts.Count() == 1 && f.Engine.OutputCasts.Single().Value.Location == correction.Location
                    && f.Engine.OutputCasts.Single().Value.Rotation == 0, "Snapshot replaced locked packet correction.");
                f.Actors[10] = f.Actors[10] with { Dead = true };
                f.Now = f.Start.AddSeconds(30); f.Scan();
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Death or elapsed clock removed AOE.");
                f.Engine.Finish(10, action); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Explicit finish leaked.");
            });
        run("Malboro actual solver escapes breath inside original complex arena", () =>
        {
            using var f = new Fight(0x141F, 511);
            var p = new Vector3(64, 0, 106);
            var cast = new DungeonCastObservation(4532, p, 0, f.Now, f.Now.AddSeconds(4.3));
            f.Engine.StartCast(1, cast, f.Now); f.Render();
            var snapshot = f.Snapshot;
            var result = new SafeZoneSolver().Solve(snapshot, new(p, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            Check(result.Status == SolutionStatus.Success && !result.IsLeastDangerFallback, "No safe path.");
            var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
            Check(path.Count > 1 && path.All(n => f.Engine.Arena!.Contains(n.Position))
                && !snapshot.DangerAreas.Single().Geometry.Contains(path[^1].Position), "Unsafe breath route.");
        });
        run("Malboro observed cast clearing replacement disappearance and timestamp jitter match source finish events", () =>
        {
            using var f = new Fight(0x141F, 511);
            f.Cast(1, 4532, 4);
            var original = f.Actors[1].Cast!;
            f.Engine.Finish(1, 4532);
            f.Actors[1] = f.Actors[1] with { Cast = original with
            {
                StartedAt = original.StartedAt.AddMilliseconds(20), FinishAt = original.FinishAt.AddMilliseconds(20),
            }};
            f.Scan();
            Check(!f.Engine.OutputCasts.Any(), "A jittered ended snapshot resurrected AOE.");
            var next = original with { StartedAt = original.StartedAt.AddSeconds(6), FinishAt = original.FinishAt.AddSeconds(6) };
            f.Now = f.Start.AddSeconds(6);
            f.Actors[1] = f.Actors[1] with { Cast = next };
            f.Scan(); Check(f.Engine.OutputCasts.Count() == 1, "A new genuine cast was suppressed.");
            f.Actors[1] = f.Actors[1] with { Cast = next with { Action = 5355 } };
            f.Scan();
            Check(f.Engine.OutputCasts.Single().Value.Rule.Action == 5355, "Replacement retained old AOE.");
            f.Now = f.Now.AddSeconds(.3);
            f.Actors[1] = f.Actors[1] with { Dead = true, Cast = null };
            f.Scan(); Check(!f.Engine.OutputCasts.Any(), "Observed cast clear on death retained AOE.");
            f.Actors[10] = new(10, 0x1420, Vector3.Zero);
            f.Cast(10, 5355, 4);
            f.Actors.Remove(10);
            f.Scan(); Check(!f.Engine.OutputCasts.Any() && f.Engine.Active != null, "Destroyed caster leaked AOE or ended stage.");
            f.Actors[11] = new(11, 0x1420, Vector3.Zero);
            f.Cast(11, 5355, 4);
            f.Events.Handle(new PromeRotation.LogSystem.LogSystemObjectRemoveEvent { SourceId = 11 }, f.Now);
            Check(!f.Engine.OutputCasts.Any(), "Explicit object removal retained AOE.");
        });
        run("Malboro legacy event JSON and replay router preserve payload timestamp and owner lifetime", () =>
        {
            var start = DateTime.UtcNow;
            using var memory = new MemoryStream();
            using (var writer = new ReplayRecordingWriter(memory))
            {
                writer.WriteHeader(new(1, 511, start, "test"));
                writer.WriteEvent(1, new LegacyMapEffectEvent { Sequence = 1, Param = 0, Data = [1, 2, 3], Timestamp = start });
            }
            using var input = new MemoryStream(memory.ToArray());
            var recording = ReplayRecordingReader.Load(input);
            Check(recording.SkippedEvents == 0 && recording.Events.Single().Event is LegacyMapEffectEvent e
                && e.Sequence == 1 && e.Param == 0 && e.Data is [1, 2, 3] && e.Timestamp == start, "Legacy JSON roundtrip differs.");
            using var router = new ReplayScriptEventRouter();
            var seen = 0;
            var sub = router.Subscribe(new object(), "LogEvent", (Action<LegacyMapEffectEvent>)(_ => seen++), typeof(LegacyMapEffectEvent));
            router.Raise(recording.Events.Single().Event);
            sub.Dispose(); router.Raise(recording.Events.Single().Event);
            Check(seen == 1, "Replay type matching or disposal failed.");
            var owner = new object();
            using var bad = LegacyMapEffectEvents.Subscribe(owner, _ => throw new InvalidOperationException("expected"));
            using var good = LegacyMapEffectEvents.Subscribe(owner, _ => seen++);
            var errors = 0;
            LegacyMapEffectEvents.Publish((LegacyMapEffectEvent)recording.Events.Single().Event, _ => errors++);
            Check(errors == 1 && seen == 2, "Live handlers were not isolated.");
            LegacyMapEffectEvents.UnsubscribeAll(owner);
            LegacyMapEffectEvents.Publish((LegacyMapEffectEvent)recording.Events.Single().Event, _ => errors++);
            Check(errors == 1 && seen == 2, "Owner cleanup retained subscription.");
        });
        run("Malboro legacy native capture copies buffers rejects oversize and discards both replay boundaries", () =>
        {
            using var capture = new LegacyMapEffectCapture(installHook: false);
            var observed = new List<LegacyMapEffectEvent>();
            using var subscription = LegacyMapEffectEvents.Subscribe(new object(), observed.Add);
            byte[] buffer = [1, 2, 3];
            capture.Capture(1, 0, buffer);
            buffer[0] = 9;
            capture.Drain(false);
            Check(observed.Count == 1 && observed[0].Data is [1, 2, 3], "Native buffer was retained instead of copied.");
            capture.Capture(1, 0, buffer);
            capture.SetReplayMode(true);
            capture.Capture(1, 0, buffer);
            capture.SetReplayMode(false);
            capture.Drain(false);
            Check(observed.Count == 1, "A pre-replay or in-replay packet escaped after exit.");
            capture.Capture(1, 0, buffer);
            capture.Clear();
            capture.Drain(false);
            Check(observed.Count == 1, "A packet survived territory clear.");
            capture.Capture(1, 0, []);
            capture.Drain(false);
            Check(observed.Count == 2 && observed[1].Data.Length == 0, "Empty payload was lost.");
            var rejected = false;
            try { capture.Capture(1, 0, new byte[4097]); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Oversize payload was accepted.");
            capture.Capture(1, 0, buffer);
            capture.Dispose();
            capture.Drain(false);
            Check(observed.Count == 2, "Disposed capture retained packets.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
