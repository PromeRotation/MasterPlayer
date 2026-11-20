using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using MasterPlayer.Spatial.Terrain;

internal static class SceneTerrainTests
{
    private sealed class Source(TerrainSceneSnapshot snapshot) : ITerrainSceneSource
    {
        internal TerrainSceneSnapshot Snapshot = snapshot;
        internal bool Available = true;
        internal int Calls;
        internal Func<Vector3, float, TerrainSceneSnapshot>? Capture;
        internal Vector3 LastOrigin;
        internal readonly int OwnerThread = Environment.CurrentManagedThreadId;
        public bool TryCapture(Vector3 origin, float radius, out TerrainSceneSnapshot? result)
        {
            Check(Environment.CurrentManagedThreadId == OwnerThread, "Scene extraction escaped the main thread.");
            ++Calls; LastOrigin = origin;
            result = Available ? Capture?.Invoke(origin, radius) ?? Snapshot : null;
            return Available;
        }
    }

    private sealed class LiveProbe(AnalyticTerrainRaycaster rays) : ITerrainProbe
    {
        private readonly GameTerrainProbe probe = new(rays);
        public bool IsAvailable => probe.IsAvailable;
        public void BeginScanBatch() => throw new InvalidOperationException("Scene modeling called the native grid scanner.");
        public void EndScanBatch() => throw new InvalidOperationException("Scene modeling called the native grid scanner.");
        public TerrainCellKind Sample(Vector3 reference, out Vector3 floor) => throw new InvalidOperationException("Scene modeling sampled a native grid cell.");
        public bool TryGetGround(Vector3 reference, out Vector3 floor) => probe.TryGetGround(reference, out floor);
        public bool CanTraverse(Vector3 from, Vector3 to) => probe.CanTraverse(from, to);
        public bool CanTraverse(Vector3 from, Vector3 to, out TerrainObstruction? obstruction) => probe.CanTraverse(from, to, out obstruction);
    }

    internal static TerrainSceneSnapshot.Instance Box(ulong id, Vector3 scale, Vector3 translation)
        => new(id, TerrainPrimitives.Box, Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(translation), 0x4000, ulong.MaxValue);
    internal static TerrainSceneSnapshot Floor(params TerrainSceneSnapshot.Instance[] other)
        => new([Box(1, new(30, 1, 30), new(0, -1, 0)), .. other]);
    private static LocalTerrainBoundary Build(TerrainSceneSnapshot scene, Vector3 origin = default, float radius = 8)
        => SceneTerrainBuilder.Build(scene, origin, radius, default).Boundary;
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Pump(Action update, Func<bool> done)
    {
        var clock = Stopwatch.StartNew();
        do { update(); if (done()) return; Thread.Sleep(1); } while (clock.Elapsed.TotalSeconds < 5);
        throw new InvalidOperationException("Background scene construction did not complete.");
    }

    internal static byte[] PcbNode()
    {
        var data = new byte[48 + 36 + 18 + 24];
        void Float(int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset), value);
        Float(28, 2); Float(32, 2); Float(36, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(40), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(42), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(44), 3);
        Float(60, 1); Float(80, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(84 + 6), ushort.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(84 + 12 + 4), ushort.MaxValue);
        data[102] = 0; data[103] = 2; data[104] = 1;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(106), 0x4000);
        data[114] = 3; data[115] = 5; data[116] = 4;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(118), 0x4000);
        return data;
    }

    internal static void Run(Action<string, Action> run, DateTime epoch)
    {
        run("scene PCB decoding preserves raw/compressed vertices, materials and rejects corrupt nodes", () =>
        {
            var data = PcbNode(); var mesh = TerrainMesh.Decode(data);
            Check(mesh.Vertices.Length == 6 && mesh.Triangles.Length == 2 && mesh.Vertices[4] == new Vector3(2, 0, 0)
                && mesh.Vertices[5] == new Vector3(0, 0, 2), "Compressed or raw PCB vertices decoded incorrectly.");
            var bad = data.ToArray(); bad[102] = 250;
            try { TerrainMesh.Decode(bad); throw new Exception("An invalid index was accepted."); } catch (InvalidDataException) { }
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8), data.Length + 100);
            try { TerrainMesh.Decode(data); throw new Exception("An invalid child was accepted."); } catch (InvalidDataException) { }
            var tree = PcbNode().Concat(PcbNode()).ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(tree.AsSpan(8), data.Length);
            Check(TerrainMesh.Decode(tree).Triangles.Length == 4, "A relative child node was lost.");
            BinaryPrimitives.WriteInt32LittleEndian(tree.AsSpan(data.Length + 12), -data.Length);
            try { TerrainMesh.Decode(tree); throw new Exception("A node cycle was accepted."); } catch (InvalidDataException) { }
        });
        run("scene layer, activation and material replacement preserve native collider semantics", () =>
        {
            Check(TerrainSceneSnapshot.IsActive(1, 1) && !TerrainSceneSnapshot.IsActive(0, 1) && !TerrainSceneSnapshot.IsActive(1, 2), "Inactive/foreign-layer geometry leaked into the scene.");
            Check(TerrainSceneSnapshot.ResolveMaterial(0, 0x4000, 0) == 0 && TerrainSceneSnapshot.ResolveMaterial(0x4000, 0, 0) == 0x4000, "An unmasked object value replaced triangle material.");
            Check(TerrainSceneSnapshot.ResolveMaterial(0x4000, 0, 0x4000) == 0 && TerrainSceneSnapshot.ResolveMaterial(0, 0x4000, 0x4000) == 0x4000, "A masked replacement lost the native material semantics.");
        });
        run("native scene snapshot detaches PCB data, ignores inactive deep-dungeon rooms and reuses assets", NativeSceneFixture.Verify);
        run("native active deep-dungeon doors and overworld barriers are modeled before any live collision feedback", NativeSceneFixture.VerifyNavigation);
        run("native procedural geometry updates invalidate cached models even at identical addresses and triangle counts", NativeSceneFixture.VerifyMutable);
        run("non-raycast wall materials remain obstacles in the initial scene model", () =>
        {
            foreach (var material in new ulong[] { 0x1000, 0x2000, 0x1400, 0x202411 })
            {
                var wall = Box(2, new(.01f, 2, 30), new(1.8f, 2, 0)) with { Material = material };
                var map = Build(Floor(wall));
                Check(map.Contains(Vector3.Zero) && !map.Contains(new(4, 0, 0)), $"Navigation discarded wall material {material:X}.");
            }
        });
        run("overworld invisible boundary and hole flags cannot merge back into a coplanar walkable floor", () =>
        {
            foreach (var material in new ulong[] { 0x202411, 0x2004000 })
            {
                var asset = new TerrainMeshAsset($"edge/{material}", new TerrainMesh(
                    [new(1, 0, -30), new(1, 0, 30), new(2, 0, 30), new(2, 0, -30)],
                    [new(0, 1, 2, material), new(0, 2, 3, material)]));
                var map = Build(Floor(new TerrainSceneSnapshot.Instance(2, asset, Matrix4x4.Identity, 0, 0)));
                Check(map.Contains(Vector3.Zero) && !map.Contains(new(1.5f, 0, 0)) && !map.Contains(new(4, 0, 0)),
                    $"A coplanar forbidden boundary {material:X} became walkable after span merging.");
            }
            var ordinary = new TerrainMeshAsset("ordinary-material", new TerrainMesh(
                [new(-30, 0, -30), new(-30, 0, 30), new(30, 0, 30), new(30, 0, -30)], [new(0, 1, 2, 0x4010), new(0, 2, 3, 0x4010)]));
            Check(Build(new([new(1, ordinary, Matrix4x4.Identity, 0, 0)])).Contains(Vector3.Zero), "A regular floor with bit 0x10 was misclassified as a zone barrier.");
        });
        run("bulk scene geometry excludes thin walls and unreachable floor without native grid rays", () =>
        {
            var map = Build(Floor(Box(2, new(.01f, 2, 30), new(1.8f, 2, 0))));
            Check(map.Contains(Vector3.Zero) && map.Contains(new(0, 0, 4)) && !map.Contains(new(4, 0, 0)), "The bulk flood connected floor through a thin wall.");
            Check(!ArenaBoundaryTraversal.ContainsSegment(map, Vector3.Zero, new(4, 0, 0)), "Boundary traversal crossed a wall.");
        });
        run("bulk headroom, cliff margins, gentle slopes and overlapping floors remain conservative", () =>
        {
            Check(Build(Floor(Box(2, new(10, .1f, 10), new(0, 1.2f, 0)))).WalkableCount == 0, "A low ceiling was walkable.");
            var cliff = Build(new([Box(1, new(10, 1, 10), new(-10, -1, 0))]), new(-2, 0, 0));
            Check(cliff.Contains(new(-2, 0, 0)) && !cliff.Contains(new(.1f, 0, 0)), "The mesh extended beyond a cliff.");
            var upper = Build(Floor(Box(2, new(20, .2f, 20), new(0, 2.8f, 0))));
            Check(upper.TryGetFloorPosition(Vector3.Zero, out var floor) && floor.Y < .1f, "An overlapping upper floor replaced the current support layer.");
            var ramp = new TerrainMeshAsset("ramp", new TerrainMesh([new(-10, -2, -10), new(-10, -2, 10), new(10, 2, 10), new(10, 2, -10)], [new(0, 1, 2, 0x4000), new(0, 2, 3, 0x4000)]));
            var slope = Build(new([new(1, ramp, Matrix4x4.Identity, 0, 0)]));
            Check(slope.TryGetFloorPosition(new(4, .8f, 0), out var support) && MathF.Abs(support.Y - .8f) < .15f, "A smooth ramp lost its ground height.");
        });
        run("bulk narrow corridors preserve the centre and radar edges preserve internal holes", () =>
        {
            var corridor = Build(Floor(Box(2, new(.1f, 2, 30), new(-1, 2, 0)), Box(3, new(.1f, 2, 30), new(1, 2, 0))));
            Check(corridor.Contains(Vector3.Zero) && corridor.Contains(new(0, 0, 4)) && !corridor.Contains(new(1, 0, 0)), "A narrow corridor was removed or its wall opened.");
            var hole = Build(Floor(Box(2, new(1, 2, 1), new(3, 2, 0))));
            Check(!hole.Contains(new(3, 0, 0)) && hole.GetContours().Any(e => MathF.Abs(e[0].X - 3) < 2 && MathF.Abs(e[0].Z) < 2), "The internal obstacle contour vanished.");
            var blocked = hole.BlockedCells().Count();
            Check(hole.BlockedRuns.Count < blocked && hole.GetContours().All(e => TerrainMesh.Finite(e[0])), "Radar geometry did not batch rows or preserve valid edges.");
        });
        run("real temporal solver and smoothing consume the bulk scene boundary", () =>
        {
            var map = Build(Floor(Box(2, new(.02f, 2, 2), new(1.8f, 2, 0))));
            using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
            manager.SetFallbackBoundary(map);
            manager.AddGoalArea(new CircleTemplate(.6f), new StaticTransform(new(4.5f, 0, 0), 0), [GoalWindow.Persistent(epoch, 1)]);
            var snapshot = manager.BuildSnapshot(epoch);
            var solution = new SafeZoneSolver().Solve(snapshot, new(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, ArenaSafetyMargin = 0, EnablePathSmoothing = true }, epoch);
            Check(solution.IsSuccess && solution.ExecutablePath.Any(p => MathF.Abs(p.Position.Z) > 2.3f), $"The real solver did not route around scene geometry: {solution.Status}");
            var previous = Vector3.Zero;
            foreach (var point in solution.ExecutablePath)
            {
                Check(ArenaBoundaryTraversal.ContainsSegment(map, previous, point.Position), "Smoothing cut through the scene boundary.");
                previous = point.Position;
            }
        });
        run("scene models publish nearby then full terrain, reuse unchanged snapshots and notice doors", () =>
        {
            var source = new Source(Floor()); var model = new SceneTerrainModel(source);
            var nearbyPublished = false;
            Pump(() =>
            {
                model.Update(Vector3.Zero, 20, epoch);
                nearbyPublished |= model.Current?.Contains(Vector3.Zero) == true && !model.Current.Contains(new(0, 0, 15));
            }, () => model.Ready);
            Check(nearbyPublished, "Nearby terrain was withheld until the outer model finished.");
            Check(model.Current!.Contains(new(0, 0, 15)), "The full local radius was never built.");
            var first = model.Current; var calls = source.Calls;
            model.Update(Vector3.Zero, 20, epoch.AddMilliseconds(100));
            Check(source.Calls == calls, "An idle frame re-extracted the scene.");
            model.Update(Vector3.Zero, 20, epoch.AddSeconds(1));
            Check(ReferenceEquals(first, model.Current), "Unchanged geometry invalidated the solver snapshot.");
            source.Snapshot = Floor(Box(2, new(.02f, 2, 30), new(1.8f, 2, 0)));
            Pump(() => model.Update(Vector3.Zero, 20, epoch.AddSeconds(2)), () => model.Ready);
            Check(!model.Current!.Contains(new(4, 0, 0)) && first!.Contains(new(4, 0, 0)), "A closed door was ignored or mutated the earlier snapshot.");
            source.Snapshot = Floor();
            Pump(() => model.Update(Vector3.Zero, 20, epoch.AddSeconds(3)), () => model.Ready);
            Check(model.Current!.Contains(new(4, 0, 0)), "An opened door did not restore its passage.");
            model.Clear();
        });
        run("moving inside a terrain window does not invalidate it when distant colliders enter the capture radius", () =>
        {
            var source = new Source(Floor());
            var closed = false;
            source.Capture = (origin, radius) =>
            {
                var instances = new List<TerrainSceneSnapshot.Instance>();
                if (origin.X + radius + 2 >= 23) instances.Add(Box(2, new(.25f, 2, 2), new(23, 2, 0)));
                if (closed) instances.Add(Box(3, new(.02f, 2, 30), new(1.8f, 2, 0)));
                return Floor(instances.ToArray());
            };
            var model = new SceneTerrainModel(source);
            try
            {
                Pump(() => model.Update(Vector3.Zero, 20, epoch), () => model.Ready);
                var first = model.Current;
                for (var step = 1; step <= 6; ++step)
                {
                    var player = new Vector3(step % 2 == 0 ? -2 : 2, 0, 0);
                    model.Update(player, 20, epoch.AddSeconds(step));
                    Check(ReferenceEquals(first, model.Current) && model.Current!.Contains(player),
                        "Walking changed the capture subset and replaced valid terrain with an unknown map.");
                }
                var next = new Vector3(4.5f, 0, 0);
                Pump(() => model.Update(next, 20, epoch.AddSeconds(7)), () => model.Ready);
                Check(!ReferenceEquals(first, model.Current) && source.LastOrigin == next,
                    "Crossing the window threshold did not build around the new anchor.");
                closed = true;
                model.Update(next, 20, epoch.AddSeconds(8));
                Check(model.Current is { WalkableCount: > 0 } && !model.Current.Contains(new(1.8f, 0, 0))
                    && model.Current.Contains(new(-4, 0, 0)) && model.LatestCapture == null,
                    "A real door change was not masked locally while preserving unaffected terrain.");
                Pump(() => model.Update(next, 20, epoch.AddSeconds(8)), () => model.Ready);
                Check(!model.Current!.Contains(new(0, 0, 0)), "A closed door was ignored after stabilizing capture origins.");
            }
            finally { model.Clear(); }
        });
        run("escape retains the published terrain and solver version until a full replacement is released at safety", () =>
        {
            var source = new Source(Floor());
            var model = new SceneTerrainModel(source);
            using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
            try
            {
                Pump(() => model.Update(Vector3.Zero, 20, epoch), () => model.Ready);
                var first = model.Current;
                var capture = model.LatestCapture;
                manager.SetFallbackBoundary(first);
                var version = manager.Version;
                var next = new Vector3(4.5f, 0, 0);
                model.Update(next, 20, epoch.AddSeconds(1), deferPublication: true);
                Pump(() =>
                {
                    model.Update(next, 20, epoch.AddSeconds(1), deferPublication: true);
                    manager.SetFallbackBoundary(model.Current);
                    Check(ReferenceEquals(first, model.Current) && manager.Version == version,
                        "A background window refresh changed planning terrain during the escape.");
                }, () => model.Ready);
                Check(ReferenceEquals(capture, model.LatestCapture) && ReferenceEquals(first, model.Display),
                    "Export or radar switched ahead of the active planning terrain.");
                model.Update(next, 20, epoch.AddSeconds(2), deferPublication: false);
                Check(!ReferenceEquals(first, model.Current) && model.LatestCapture?.Origin == next
                    && model.LatestCapture.Radius == 20 && ReferenceEquals(model.Current, model.Display)
                    && ReferenceEquals(model.Current, model.LatestCapture.Result.Boundary),
                    "Safety did not atomically publish the full prepared window.");
            }
            finally { model.Clear(); }
        });
        run("an in-flight near-to-full terrain upgrade also waits for escape completion", () =>
        {
            var model = new SceneTerrainModel(new Source(Floor()));
            try
            {
                Pump(() => model.Update(Vector3.Zero, 20, epoch), () => model.LatestCapture?.Radius == 8);
                var near = model.Current;
                Pump(() =>
                {
                    model.Update(Vector3.UnitX, 20, epoch, deferPublication: true);
                    Check(ReferenceEquals(near, model.Current), "An already-running full build interrupted a near-map escape.");
                }, () => model.Ready);
                Check(model.LatestCapture?.Radius == 8, "Export advanced ahead of the held near map.");
                model.Update(Vector3.UnitX, 20, epoch.AddSeconds(1));
                Check(model.LatestCapture?.Radius == 20 && !ReferenceEquals(near, model.Current),
                    "Completing the escape failed to publish the full terrain upgrade.");
            }
            finally { model.Clear(); }
        });
        run("a deferred terrain window still audits real geometry changes in the retained window", () =>
        {
            var source = new Source(Floor());
            var closed = false;
            source.Capture = (origin, radius) => closed && origin.X - radius - 2 <= -19
                ? Floor(Box(2, new(.02f, 2, 30), new(-19, 2, 0))) : Floor();
            var model = new SceneTerrainModel(source);
            try
            {
                Pump(() => model.Update(Vector3.Zero, 20, epoch), () => model.Ready);
                var first = model.Current;
                var next = new Vector3(12, 0, 0);
                model.Update(next, 20, epoch.AddSeconds(1), deferPublication: true);
                Pump(() => model.Update(next, 20, epoch.AddSeconds(1), deferPublication: true), () => model.Ready);
                closed = true;
                model.Update(next, 20, epoch.AddSeconds(2), deferPublication: true);
                Check(model.Current is { WalkableCount: > 0 } && !ReferenceEquals(first, model.Current)
                    && !model.Current.Contains(new(-19, 0, 0)) && model.Current.Contains(next) && model.LatestCapture == null,
                    "Waiting for escape completion hid a collision change outside the candidate window.");
            }
            finally { model.Clear(); }
        });
        run("a prepared terrain window is revalidated before publication and never restores a new obstacle", () =>
        {
            var source = new Source(Floor());
            var closed = false;
            source.Capture = (origin, radius) => closed && origin.X + radius + 2 >= 23
                ? Floor(Box(2, new(.2f, 2, 30), new(23, 2, 0))) : Floor();
            var model = new SceneTerrainModel(source);
            try
            {
                Pump(() => model.Update(Vector3.Zero, 20, epoch), () => model.Ready);
                var next = new Vector3(4.5f, 0, 0);
                model.Update(next, 20, epoch.AddSeconds(1), deferPublication: true);
                Pump(() => model.Update(next, 20, epoch.AddSeconds(1), deferPublication: true), () => model.Ready);
                closed = true;
                model.Update(next, 20, epoch.AddSeconds(2));
                Check(model.LatestCapture == null && !model.Current!.Contains(new(23, 0, 0))
                    && model.Current.Contains(next), "A stale prepared map was published after a new obstacle appeared.");
                Pump(() => model.Update(next, 20, epoch.AddSeconds(2)), () => model.Ready);
                Check(!model.Current!.Contains(new(23, 0, 0)), "Rebuilding restored the stale candidate passage.");
            }
            finally { model.Clear(); }
        });
        foreach (var change in new[] { "outside", "layer", "floor" })
            run($"deferred terrain cannot survive {change} invalidation", () =>
            {
                var source = new Source(Floor());
                var model = new SceneTerrainModel(source);
                try
                {
                    Pump(() => model.Update(Vector3.Zero, 20, epoch), () => model.Ready);
                    var next = new Vector3(4.5f, 0, 0);
                    model.Update(next, 20, epoch.AddSeconds(1), deferPublication: true);
                    Pump(() => model.Update(next, 20, epoch.AddSeconds(1), deferPublication: true), () => model.Ready);
                    source.Available = false;
                    if (change == "floor") model.Clear();
                    var position = change == "outside" ? new Vector3(25, 0, 0) : new Vector3(4.5f, 2, 0);
                    model.Update(position, 20, epoch.AddSeconds(2), deferPublication: true);
                    Check(model.Current is { WalkableCount: 0 } && model.LatestCapture == null,
                        "A deferred result bypassed terrain invalidation.");
                    if (change != "outside") Check(model.Display == null, "A different layer/floor retained its previous display.");
                }
                finally { model.Clear(); }
            });
        run("live collision corrections bypass deferred publication and survive the later map swap", () =>
        {
            var source = new Source(Floor());
            var rays = new AnalyticTerrainRaycaster();
            using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
            var controller = new LocalTerrainController(manager, new LiveProbe(rays), source);
            try
            {
                Pump(() => controller.Update(Vector3.Zero, 1, true, 20, epoch), () => controller.StatusText.Contains("已就绪"));
                var first = controller.Current;
                var next = new Vector3(4.5f, 0, 0);
                Pump(() => controller.Update(next, 1, true, 20, epoch.AddSeconds(1), deferPublication: true),
                    () => controller.StatusText.Contains("安全点"));
                Check(ReferenceEquals(first, controller.Current), "The controller published a background replacement during escape.");
                rays.Walls.Add((5.5f, -30, 30, 4));
                Check(!controller.ValidateStep(next, new(6.5f, 0, 0)), "Deferred publication disabled the forward collision check.");
                controller.RejectRoute(next, 20, epoch.AddSeconds(1));
                Check(!controller.Current!.Contains(new(5.5f, 0, 0)), "A live obstruction waited for map publication.");
                controller.Update(next, 1, true, 20, epoch.AddSeconds(2));
                Check(!controller.Current!.Contains(new(5.5f, 0, 0)) && controller.LatestCapture?.Origin == next,
                    "Swapping the prepared map restored a known collision passage.");
            }
            finally { controller.Clear(); }
        });
        run("radar retains complete terrain and fixed scale while planning invalidates and live doors still block", () =>
        {
            var source = new Source(Floor());
            var rays = new AnalyticTerrainRaycaster();
            using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
            var controller = new LocalTerrainController(manager, new LiveProbe(rays), source);
            var sawNear = false;
            Pump(() =>
            {
                controller.Update(Vector3.Zero, 1, true, 20, epoch);
                Check(controller.ViewRadius == 20, "Initial near/full publication changed radar scale.");
                sawNear |= controller.Display?.Contains(Vector3.Zero) == true && !controller.Display.Contains(new(0, 0, 15));
            }, () => controller.StatusText.Contains("已就绪"));
            var oldDisplay = controller.Display!;
            Check(sawNear && oldDisplay.Contains(new(0, 0, 15)), "Initial terrain did not publish near then full display.");
            source.Snapshot = Floor(Box(2, new(.02f, 2, 30), new(1.8f, 2, 0)));
            rays.Walls.Add((1.8f, -30, 30, 4));
            controller.Update(Vector3.Zero, 1, true, 20, epoch.AddSeconds(1));
            Check(ReferenceEquals(oldDisplay, controller.Display) && controller.Current is { WalkableCount: > 0 }
                && controller.Current.Contains(new(-4, 0, 0))
                && !manager.BuildSnapshot(epoch).ArenaBoundary!.Contains(new(4, 0, 0)),
                "Retaining radar terrain also retained a stale planning passage.");
            Check(!controller.ValidateStep(Vector3.Zero, new(2, 0, 0)), "The retained display bypassed a live closed door.");
            controller.RejectRoute(Vector3.Zero, 20, epoch.AddSeconds(1));
            var retainedUnaffectedTerrain = controller.Current!.Contains(new(-4, 0, 0));
            Pump(() =>
            {
                controller.Update(Vector3.Zero, 1, true, 20, epoch.AddSeconds(1));
                Check(controller.ViewRadius == 20, "Replacement terrain changed radar scale.");
                if (!controller.StatusText.Contains("已就绪"))
                {
                    Check(ReferenceEquals(oldDisplay, controller.Display), "Radar cleared or shrank before the full replacement was ready.");
                    retainedUnaffectedTerrain &= controller.Current?.Contains(new(-4, 0, 0)) == true;
                }
            }, () => controller.StatusText.Contains("已就绪"));
            Check(retainedUnaffectedTerrain && ReferenceEquals(controller.Display, controller.Current)
                && !controller.Display!.Contains(new(4, 0, 0)) && oldDisplay.Contains(new(4, 0, 0)),
                "The full closed-door map failed to atomically replace the immutable old display.");
            source.Available = false;
            controller.Update(Vector3.Zero, 1, true, 20, epoch.AddSeconds(2), scene: ((nint)1, 32, 0));
            Check(controller.Display == null && controller.Current is { WalkableCount: 0 }, "A new deep-dungeon floor displayed the old floor.");
            controller.Clear();
        });
        run("failed terrain rebuild retains only radar history and hard-blocks movement until recovery", () =>
        {
            var source = new Source(Floor());
            using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
            var controller = new LocalTerrainController(manager, new LiveProbe(new AnalyticTerrainRaycaster()), source);
            Pump(() => controller.Update(Vector3.Zero, 1, true, 20, epoch), () => controller.StatusText.Contains("已就绪"));
            var oldDisplay = controller.Display;
            source.Snapshot = new([new(1, new TerrainMeshAsset("invalid-view-rebuild", [0]), Matrix4x4.Identity, 0x4000, ulong.MaxValue)]);
            Pump(() => controller.Update(Vector3.Zero, 1, true, 20, epoch.AddSeconds(1)), () => controller.LastError != null);
            Check(ReferenceEquals(oldDisplay, controller.Display) && controller.Current is { WalkableCount: 0 }
                && !controller.ValidateStep(Vector3.Zero, Vector3.UnitX)
                && !manager.BuildSnapshot(epoch).ArenaBoundary!.Contains(Vector3.Zero),
                "A failed replacement either erased the display or allowed stale movement.");
            source.Snapshot = Floor();
            Pump(() => controller.Update(Vector3.Zero, 1, true, 20, epoch.AddSeconds(4)), () => controller.StatusText.Contains("已就绪"));
            Check(controller.LastError == null && ReferenceEquals(controller.Display, controller.Current), "Recovery did not publish the new complete display.");
            manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 10));
            controller.Update(Vector3.Zero, 1, true, 20, epoch.AddSeconds(5));
            Check(controller.Display == null && controller.Current == null, "A formal arena retained the fallback display.");
            controller.Clear();
        });
        run("support-layer changes discard the old radar floor even while a replacement is pending", () =>
        {
            var source = new Source(Floor()); var model = new SceneTerrainModel(source);
            Pump(() => model.Update(Vector3.Zero, 20, epoch), () => model.Ready);
            source.Available = false;
            model.Update(new(0, 2, 0), 20, epoch.AddSeconds(1));
            Check(model.Display == null && model.Current is { WalkableCount: 0 }, "A different support layer kept the previous radar floor.");
            model.Clear();
        });
        run("obsolete scene tasks cannot publish into a new floor and unavailable data stays blocked", () =>
        {
            var source = new Source(Floor()); var model = new SceneTerrainModel(source);
            model.Update(Vector3.Zero, 20, epoch); model.Clear();
            source.Available = false;
            model.Update(Vector3.Zero, 20, epoch);
            Check(model.Current is { WalkableCount: 0 }, "An unavailable scene fell back to unrestricted floor.");
            source.Available = true; source.Snapshot = Floor(Box(2, new(.01f, 2, 30), new(1.8f, 2, 0)));
            Pump(() => model.Update(Vector3.Zero, 20, epoch.AddSeconds(1)), () => model.Ready);
            Check(!model.Current!.Contains(new(4, 0, 0)), "An old floor's task replaced the active scene.");
            model.Clear();
        });
        run("scene controller keeps live door/cliff checks and never invokes native grid sampling", () =>
        {
            var source = new Source(Floor()); var rays = new AnalyticTerrainRaycaster();
            using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
            var controller = new LocalTerrainController(manager, new LiveProbe(rays), source);
            Pump(() => controller.Update(Vector3.Zero, 1, true, 8, epoch, scene: ((nint)1234, 31, 0)),
                () => controller.Current is { WalkableCount: > 0 } && controller.StatusText.Contains("已就绪"));
            Check(controller.ValidateStep(Vector3.Zero, new(2, 0, 0)), "A safe native route was rejected.");
            rays.Walls.Add((1, -10, 10, 3));
            Check(!controller.ValidateStep(Vector3.Zero, new(2, 0, 0)), "A live door was ignored by the scene model.");
            rays.Walls.Clear(); rays.CliffAtX = 1;
            Check(!controller.ValidateStep(Vector3.Zero, new(2, 0, 0)), "Live missing support was ignored by the scene model.");
            rays.CliffAtX = null;
            var first = controller.Current;
            controller.Update(new(0, 2, 0), 1, true, 8, epoch, airborne: true, scene: ((nint)1234, 31, 0));
            Check(ReferenceEquals(first, controller.Current), "Jumping cleared the model.");
            controller.Update(Vector3.Zero, 1, true, 8, epoch, scene: ((nint)1234, 32, 1));
            Check(!ReferenceEquals(first, controller.Current), "The next floor reused the previous scene model.");
            controller.Clear();
        });
        run("scene background failures stop movement, expose the cause and retry after backoff", () =>
        {
            var invalid = new TerrainMeshAsset("invalid", [0]);
            var source = new Source(new([new(1, invalid, Matrix4x4.Identity, 0x4000, ulong.MaxValue)]));
            using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
            var controller = new LocalTerrainController(manager, new LiveProbe(new AnalyticTerrainRaycaster()), source);
            Pump(() => controller.Update(Vector3.Zero, 1, true, 8, epoch), () => controller.LastError != null);
            Check(controller.Current is { WalkableCount: 0 } && controller.LastError!.Contains("越界")
                && !controller.ValidateStep(Vector3.Zero, Vector3.UnitX), "A background failure lost its cause or allowed movement.");
            source.Snapshot = Floor(); var calls = source.Calls;
            controller.Update(Vector3.Zero, 1, true, 8, epoch.AddSeconds(1));
            Check(source.Calls == calls && controller.LastError != null, "The failure backoff was bypassed.");
            Pump(() => controller.Update(Vector3.Zero, 1, true, 8, epoch.AddSeconds(3)),
                () => controller.Current is { WalkableCount: > 0 } && controller.LastError == null);
            calls = source.Calls;
            manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 10));
            controller.Update(Vector3.Zero, 1, true, 8, epoch.AddSeconds(4));
            Check(controller.Current == null && source.Calls == calls, "A registered arena still extracted live scene geometry.");
            controller.Clear();
        });
        run("live collision patches a wall missing from the scene model and the real solver routes around it", () =>
        {
            var source = new Source(Floor());
            var rays = new AnalyticTerrainRaycaster();
            using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
            var controller = new LocalTerrainController(manager, new LiveProbe(rays), source);
            Pump(() => controller.Update(Vector3.Zero, 1, true, 8, epoch),
                () => controller.Current is { WalkableCount: > 0 } && controller.StatusText.Contains("已就绪"));
            var original = controller.Current!;
            rays.Walls.Add((1.5f, -.1f, .1f, 3));
            Check(!controller.ValidateStep(Vector3.Zero, new(2, 0, 0)), "A missing scene wall passed the live guard.");
            var version = manager.Version;
            controller.RejectRoute(Vector3.Zero, 8, epoch);
            var corrected = controller.Current!;
            Check(!corrected.Contains(new(1.5f, 0, 0)) && corrected.Contains(Vector3.Zero) && corrected.Contains(new(-2, 0, 0))
                && original.Contains(new(1.5f, 0, 0)) && manager.Version > version, "Collision feedback did not publish a local immutable correction.");
            Check(!ArenaBoundaryTraversal.ContainsSegment(corrected, Vector3.Zero, new(3, 0, 0))
                && corrected.GetContours().Any(e => MathF.Abs(e[0].X - 1.5f) < .6f && MathF.Abs(e[0].Z) < .6f)
                && corrected.BlockedCells().Any(c => c.Kind == TerrainCellKind.Wall && MathF.Abs(c.Center.X - 1.5f) < .6f),
                "The corrected wall was absent from traversal or radar geometry.");
            manager.AddGoalArea(new CircleTemplate(.4f), new StaticTransform(new(3, 0, 0), 0), [GoalWindow.Persistent(epoch, 1)]);
            var snapshot = manager.BuildSnapshot(epoch);
            var solution = new SafeZoneSolver().Solve(snapshot, new(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, ArenaSafetyMargin = 0 }, epoch);
            Check(solution.IsSuccess, $"The solver could not use corrected terrain: {solution.Status}");
            var previous = Vector3.Zero;
            foreach (var point in solution.ExecutablePath)
            {
                Check(controller.ValidateStep(previous, point.Position), "The corrected route still crossed the independently queried wall.");
                previous = point.Position;
            }
            controller.Update(Vector3.Zero, 1, true, 8, epoch.AddMilliseconds(100));
            Check(ReferenceEquals(corrected, controller.Current), "An unchanged scene discarded the collision correction.");
            version = manager.Version;
            controller.Update(Vector3.Zero, 1, true, 8, epoch.AddMilliseconds(600));
            Check(ReferenceEquals(corrected, controller.Current) && manager.Version == version, "Rechecking the same wall invalidated an unchanged corrected snapshot.");
            rays.Walls.Clear();
            controller.Update(Vector3.Zero, 1, true, 8, epoch.AddMilliseconds(1200));
            Check(controller.Current!.Contains(new(1.5f, 0, 0)) && !corrected.Contains(new(1.5f, 0, 0)), "Opening a door failed to remove its correction or mutated an earlier map.");
            controller.Clear();
        });
        run("live collision corrections survive model window rebuilds and clear on deep-dungeon floor changes", () =>
        {
            var source = new Source(Floor()); var rays = new AnalyticTerrainRaycaster();
            using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
            var controller = new LocalTerrainController(manager, new LiveProbe(rays), source);
            Pump(() => controller.Update(Vector3.Zero, 1, true, 8, epoch, scene: ((nint)1, 31, 0)),
                () => controller.StatusText.Contains("已就绪"));
            rays.Walls.Add((1.5f, -1, 1, 3));
            Check(!controller.ValidateStep(Vector3.Zero, new(2, 0, 0)), "The fixture wall was not detected.");
            controller.RejectRoute(Vector3.Zero, 8, epoch);
            Pump(() => controller.Update(new(-4.5f, 0, 0), 1, true, 8, epoch.AddSeconds(1), scene: ((nint)1, 31, 0)),
                () => controller.StatusText.Contains("已就绪"));
            Check(!controller.Current!.Contains(new(1.5f, 0, 0)), "A model rebuild restored a known collision wall.");
            rays.Walls.Clear();
            Pump(() => controller.Update(Vector3.Zero, 1, true, 8, epoch.AddSeconds(2), scene: ((nint)1, 32, 0)),
                () => controller.StatusText.Contains("已就绪"));
            Check(controller.Current!.Contains(new(1.5f, 0, 0)), "A new floor inherited the previous floor's wall.");
            controller.Clear();
        });
        run("live cliff corrections preserve the drop type and only recheck one learned obstruction per frame", () =>
        {
            var rays = new AnalyticTerrainRaycaster { CliffAtX = 1 };
            var probe = new GameTerrainProbe(rays);
            Check(!probe.CanTraverse(Vector3.Zero, new(2, 0, 0), out var failure) && failure is { Kind: TerrainCellKind.Drop }, "A lost support point was not reported.");
            var corrections = new TerrainCollisionCorrections();
            corrections.Record(failure!.Value, epoch);
            var map = Build(Floor());
            var corrected = corrections.Apply(map)!;
            Check(corrected.BlockedCells().Any(c => c.Kind == TerrainCellKind.Drop) && !corrected.Contains(failure.Value.Position), "Cliff feedback lost its radar type or exclusion.");
            corrections.Record(failure.Value with { Position = failure.Value.Position + Vector3.UnitZ, From = Vector3.UnitZ, To = new(2, 0, 1) }, epoch);
            rays.CliffAtX = null;
            corrections.Refresh(probe, Vector3.Zero, 8, epoch.AddSeconds(1));
            Check(corrections.Count == 1, "A frame rechecked every collision correction.");
            corrections.Refresh(probe, Vector3.Zero, 8, epoch.AddSeconds(1));
            Check(corrections.Count == 0 && ReferenceEquals(corrections.Apply(map), map), "Recovered support did not restore the original model.");
        });
        run("live route checks retain front clearance and reject low ceilings before character contact", () =>
        {
            var rays = new AnalyticTerrainRaycaster(); var probe = new GameTerrainProbe(rays);
            rays.Walls.Add((1, -10, 10, 3));
            Check(!probe.CanTraverse(Vector3.Zero, new(.8f, 0, 0), out var wall) && wall is { Kind: TerrainCellKind.Wall }
                && MathF.Abs(wall.Value.Position.X - 1) < .01f, "The character radius overlapped a wall beyond the route endpoint.");
            rays.Walls.Clear(); rays.Ceiling = 1.5f;
            Check(!probe.CanTraverse(Vector3.Zero, Vector3.UnitX, out var ceiling) && ceiling is { Kind: TerrainCellKind.Wall }, "A level path passed under a low ceiling.");
        });
        run("collision corrections remain on their ground layer and failed native queries never invent walls", () =>
        {
            var map = new LocalTerrainBoundary(Vector3.Zero, 3, Enumerable.Repeat(TerrainCellKind.Walkable, 9).ToArray(), Enumerable.Repeat(3f, 9).ToArray(), .25f);
            var corrections = new TerrainCollisionCorrections();
            corrections.Record(new(Vector3.Zero, TerrainCellKind.Wall, Vector3.Zero, Vector3.UnitX), epoch);
            Check(ReferenceEquals(corrections.Apply(map), map), "A lower-level collision blocked an upper floor.");
            var source = new Source(Floor()); var rays = new AnalyticTerrainRaycaster();
            using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
            var controller = new LocalTerrainController(manager, new LiveProbe(rays), source);
            Pump(() => controller.Update(Vector3.Zero, 1, true, 8, epoch), () => controller.StatusText.Contains("已就绪"));
            rays.ThrowOnCast = true;
            Check(!controller.ValidateStep(Vector3.Zero, Vector3.UnitX), "A native query exception allowed movement.");
            controller.RejectRoute(Vector3.Zero, 8, epoch);
            Check(controller.Current is { WalkableCount: 0 } && controller.LastError != null && !controller.Current.BlockedCells().Any(), "An exception was treated as evidence for a physical wall.");
            controller.Clear();
        });
        run("returning to an existing floor window reuses tiles and clearing the session rebuilds them", () =>
        {
            var model = new SceneTerrainModel(new Source(Floor()));
            try
            {
                Pump(() => model.Update(Vector3.Zero, 20, epoch), () => model.Ready);
                Pump(() => model.Update(new(4.5f, 0, 0), 20, epoch.AddSeconds(1)), () => model.Ready);
                Pump(() => model.Update(Vector3.Zero, 20, epoch.AddSeconds(2)), () => model.Ready);
                Check(model.LatestCapture?.Result.Diagnostics is { BuiltTiles: 0, CachedTiles: > 0 },
                    "The production model rebuilt a previously visited window.");
                model.Clear();
                Pump(() => model.Update(Vector3.Zero, 20, epoch.AddSeconds(3)), () => model.Ready);
                Check(model.LatestCapture?.Result.Diagnostics is { BuiltTiles: > 0, CachedTiles: 9 },
                    "The new terrain session reused an old floor instead of only its fresh near tiles.");
            }
            finally { model.Clear(); }
        });
        run("changing support layers clears tile memory before publishing the new floor height", () =>
        {
            var source = new Source(Floor(Box(2, new(25, .2f, 25), new(0, 3.8f, 0))));
            var model = new SceneTerrainModel(source);
            try
            {
                Pump(() => model.Update(Vector3.Zero, 20, epoch), () => model.Ready);
                Pump(() => model.Update(new(0, 4, 0), 20, epoch.AddSeconds(1)), () => model.Ready);
                Check(model.LatestCapture?.Result.Diagnostics is { BuiltTiles: > 0, CachedTiles: 9 }
                    && model.Current!.TryGetFloorPosition(Vector3.Zero, out var upper) && MathF.Abs(upper.Y - 4) < .1f,
                    "Changing support layers restored cached lower-floor spans.");
            }
            finally { model.Clear(); }
        });
        foreach (var change in new[] { "territory", "director", "floor", "layout" })
            run($"production controller clears tile memory on {change} changes at identical coordinates", () =>
            {
                using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
                var controller = new LocalTerrainController(manager, new LiveProbe(new AnalyticTerrainRaycaster()), new Source(Floor()));
                try
                {
                    Pump(() => controller.Update(Vector3.Zero, 1, true, 20, epoch, scene: ((nint)1, 31, 0)),
                        () => controller.StatusText.Contains("已就绪"));
                    var territory = change == "territory" ? 2u : 1u;
                    var identity = (Director: (nint)(change == "director" ? 2 : 1),
                        Floor: (byte)(change == "floor" ? 32 : 31), Layout: (byte)(change == "layout" ? 1 : 0));
                    Pump(() => controller.Update(Vector3.Zero, territory, true, 20, epoch.AddSeconds(1), scene: identity),
                        () => controller.StatusText.Contains("已就绪"));
                    Check(controller.LatestCapture?.Result.Diagnostics is { BuiltTiles: > 0, CachedTiles: 9 },
                        "A new territory/floor/layout inherited cached navigation tiles.");
                }
                finally { controller.Clear(); }
            });
        run("learned live collisions survive leaving a cached room and are rechecked only upon returning", () =>
        {
            var corrections = new TerrainCollisionCorrections();
            var rays = new AnalyticTerrainRaycaster();
            var probe = new GameTerrainProbe(rays);
            var map = Build(Floor());
            corrections.Record(new(new(1, 0, 0), TerrainCellKind.Wall, Vector3.Zero, new(2, 0, 0)), epoch);
            var calls = rays.Calls;
            corrections.Refresh(probe, new(40, 0, 0), 20, epoch.AddSeconds(1));
            Check(corrections.Count == 1 && rays.Calls == calls && !corrections.Apply(map)!.Contains(new(1, 0, 0)),
                "Leaving the room discarded a known collision or queried unloaded distant terrain.");
            rays.Walls.Add((1, -10, 10, 3));
            corrections.Refresh(probe, Vector3.Zero, 20, epoch.AddSeconds(2));
            Check(corrections.Count == 1 && !corrections.Apply(map)!.Contains(new(1, 0, 0)), "Returning resurrected a known blocked passage.");
            rays.Walls.Clear();
            corrections.Refresh(probe, Vector3.Zero, 20, epoch.AddSeconds(3));
            Check(corrections.Count == 0 && ReferenceEquals(corrections.Apply(map), map), "Confirmed recovery failed to restore the cached room.");
        });
    }
}
