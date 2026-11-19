using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using MasterPlayer.Spatial.Terrain;

internal static class TerrainSceneArchiveTests
{
    internal static void Run(Action<string, Action> run, DateTime epoch)
    {
        run("export capture follows the published generation and clears with the terrain session", () =>
        {
            var source = new Source(SceneTerrainTests.Floor()) { Failures = 2 };
            var model = new SceneTerrainModel(source);
            TerrainSceneCapture? near = null;
            Check(SpinWait.SpinUntil(() =>
            {
                model.Update(Vector3.Zero, 12, epoch);
                if (model.LatestCapture is { Radius: 8 } captured) near = captured;
                return model.Ready;
            }, TimeSpan.FromSeconds(10)), "Scene did not finish its near/full publication.");
            var capture = model.LatestCapture!;
            Check(capture.Radius == 12 && ReferenceEquals(capture.Result.Boundary, model.Current)
                && ReferenceEquals(capture.Scene, source.Scene) && capture.CaptureDiagnostics?.Attempts == 1,
                "Export input was not paired with the published full map.");
            Check(near?.CaptureDiagnostics is { Attempts: 3, CopiedBytes: 256, CopyMilliseconds: 2, TotalMilliseconds: 5 },
                "The completed near capture lost model-copy costs from its earlier partial attempts.");
            model.Invalidate(new(20, 0, 0), 12);
            Check(model.LatestCapture == null, "Invalidation retained an export from another planning window.");
            model.Clear();
            Check(model.LatestCapture == null && model.Current == null, "Clearing terrain retained export evidence as live state.");
        });
        run("scene archive rebuilds detached PCB, shared primitives, materials, heights and live corrections", () =>
        {
            InDirectory(directory =>
            {
                var asset = new TerrainMeshAsset("memory/procedural-fixture", SceneTerrainTests.PcbNode());
                var scene = SceneTerrainTests.Floor(
                    new(2, asset, Matrix4x4.CreateTranslation(3, 0, 1), 0xFEDCBA9876543210, 0xFFFFFFFFFFFFFFFF),
                    new(3, asset, Matrix4x4.CreateScale(-1, 1, 1) * Matrix4x4.CreateTranslation(-3, 0, 1), 0, 0),
                    SceneTerrainTests.Box(4, new(.1f, 2, 3), new(4, 2, 0)));
                var result = SceneTerrainBuilder.Build(scene, Vector3.Zero, 8, default);
                var obstruction = new TerrainObstruction(new(2, 0, 0), TerrainCellKind.Wall, Vector3.Zero, new(2, 0, 0));
                var capture = new TerrainSceneCapture(scene, Vector3.Zero, 8, epoch,
                    new() { Complete = true, CopiedModels = 1, CopiedBytes = 256 }, result);
                var data = new TerrainSceneArchive.Data(capture, 1281, 21, 0,
                    result.Boundary.WithObstructions([obstruction]), [obstruction]);
                var path = Path.Combine(directory, "fixture.terrain.zip");
                TerrainSceneArchive.Write(path, data);
                var imported = TerrainSceneArchive.Read(path);
                Check(imported.Capture.CapturedAtUtc == epoch && imported.Territory == 1281 && imported.Floor == 21,
                    "Capture identity or clock changed.");
                var instances = imported.Capture.Scene.Instances;
                Check(ReferenceEquals(instances[1].Asset, instances[2].Asset), "Shared geometry was duplicated.");
                Check(instances[1].Material == 0xFEDCBA9876543210 && instances[1].MaterialMask == ulong.MaxValue
                    && instances[2].World == scene.Instances[2].World, "64-bit material or world transform was lost.");
                Check(TerrainSceneArchive.SameBoundary(result.Boundary, imported.Capture.Result.Boundary)
                    && TerrainSceneArchive.SameBoundary(data.PublishedBoundary, imported.PublishedBoundary), "Stored grids changed.");
                Check(Rebuild(path), "Imported scene did not reproduce its original base and corrected maps.");
                Check(imported.Capture.Result.Diagnostics == result.Diagnostics && imported.Capture.CaptureDiagnostics == capture.CaptureDiagnostics,
                    "Stage measurements were lost.");
                try { TerrainSceneArchive.Write(path, data); throw new Exception("Existing evidence was overwritten."); }
                catch (IOException) { }
                using var archive = ZipFile.OpenRead(path);
                Check(archive.Entries.Count == 4, "Archive has unexpected entries or duplicate model files.");
            });
        });
        run("scene archive rejects unsupported versions and truncated geometry", () =>
        {
            InDirectory(directory =>
            {
                var result = SceneTerrainBuilder.Build(SceneTerrainTests.Floor(), Vector3.Zero, 8, default);
                var capture = new TerrainSceneCapture(SceneTerrainTests.Floor(), Vector3.Zero, 8, epoch, null, result);
                var data = new TerrainSceneArchive.Data(capture, 1, 0, 0, result.Boundary, []);
                var path = Path.Combine(directory, "invalid.terrain.zip");
                TerrainSceneArchive.Write(path, data);
                using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
                {
                    var entry = archive.GetEntry("manifest.json")!;
                    JsonNode metadata;
                    using (var stream = entry.Open()) metadata = JsonNode.Parse(stream)!;
                    entry.Delete(); metadata["FormatVersion"] = 999;
                    using var output = archive.CreateEntry("manifest.json").Open();
                    JsonSerializer.Serialize(output, metadata);
                }
                ExpectInvalid(() => TerrainSceneArchive.Read(path));
                var truncated = Path.Combine(directory, "truncated.terrain.zip");
                TerrainSceneArchive.Write(truncated, data);
                using (var archive = ZipFile.Open(truncated, ZipArchiveMode.Update))
                {
                    archive.GetEntry("geometry.bin")!.Delete();
                    using var output = archive.CreateEntry("geometry.bin").Open();
                    output.WriteByte(1);
                }
                ExpectInvalid(() => TerrainSceneArchive.Read(truncated));
            });
        });
        run("background scene export publishes complete archives and reports filesystem failures", () =>
        {
            InDirectory(directory =>
            {
                var scene = SceneTerrainTests.Floor();
                var result = SceneTerrainBuilder.Build(scene, Vector3.Zero, 8, default);
                var data = new TerrainSceneArchive.Data(new(scene, Vector3.Zero, 8, epoch, null, result), 1, 0, 0, result.Boundary, []);
                var exporter = new TerrainSceneExporter();
                Check(exporter.Start(directory, data), "Export did not start.");
                Check(SpinWait.SpinUntil(() => !exporter.IsRunning, TimeSpan.FromSeconds(10)), "Export did not complete.");
                var path = Directory.GetFiles(directory, "*.terrain.zip").Single();
                Check(exporter.Status.Contains(path) && Rebuild(path), "Export completion did not identify a usable archive.");
                Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "Completed export retained a temporary file.");
                Check(exporter.Start(path, data), "Failure case did not start.");
                Check(SpinWait.SpinUntil(() => !exporter.IsRunning, TimeSpan.FromSeconds(10))
                    && exporter.Status.Contains("导出失败"), "Filesystem failure was hidden or escaped to navigation.");
                Check(TerrainSceneArchive.SameBoundary(result.Boundary, SceneTerrainBuilder.Build(scene, Vector3.Zero, 8, default).Boundary),
                    "Export mutated the live scene.");
            });
        });
        run("build diagnostics count geometry and account for stages without changing navigation", () =>
        {
            var scene = SceneTerrainTests.Floor(SceneTerrainTests.Box(2, new(.1f, 2, 3), new(3, 2, 0)));
            var result = SceneTerrainBuilder.Build(scene, Vector3.Zero, 8, default);
            var d = result.Diagnostics;
            double[] stages = [d.DecodeMilliseconds, d.TransformMilliseconds, d.ClassifyMilliseconds, d.RasterizeMilliseconds,
                d.FilterMilliseconds, d.CompactMilliseconds, d.ForbiddenMilliseconds, d.ErodeMilliseconds,
                d.ReachabilityMilliseconds, d.BoundaryMilliseconds];
            Check(stages.All(v => double.IsFinite(v) && v >= 0) && d.TotalMilliseconds >= stages.Sum(), "Stage timing overlaps or is invalid.");
            Check(d.Instances == scene.Instances.Length && d.InputVertices == scene.Instances.Sum(i => i.Asset.Mesh.Value.Vertices.Length)
                && d.InputTriangles == scene.Instances.Sum(i => i.Asset.Mesh.Value.Triangles.Length)
                && d.RasterizedTriangles == result.Triangles && d.WalkableCells == result.Boundary.WalkableCount
                && d.CompactSpans >= d.ReachableSpans && d.ReachableSpans > 0, "Geometry diagnostics disagree with the result.");
        });
    }

    private sealed class Source(TerrainSceneSnapshot scene) : ITerrainSceneSource
    {
        internal int Failures { get; set; }
        internal TerrainSceneSnapshot Scene => scene;
        public TerrainCaptureDiagnostics? Diagnostics { get; private set; }
        public bool TryCapture(Vector3 origin, float radius, out TerrainSceneSnapshot? snapshot)
        {
            if (Failures > 0)
            {
                --Failures;
                Diagnostics = new() { CopiedBytes = 128, CopyMilliseconds = 1, TotalMilliseconds = 2 };
                snapshot = null;
                return false;
            }
            Diagnostics = new() { Complete = true, Instances = scene.Instances.Length, TotalMilliseconds = 1 };
            snapshot = scene;
            return true;
        }
    }

    internal static bool Rebuild(string path)
    {
        var data = TerrainSceneArchive.Read(path);
        var capture = data.Capture;
        var result = SceneTerrainBuilder.Build(capture.Scene, capture.Origin, capture.Radius, default);
        var baseMatches = TerrainSceneArchive.SameBoundary(capture.Result.Boundary, result.Boundary);
        var corrected = result.Boundary.WithObstructions(data.Obstructions);
        var publishedMatches = TerrainSceneArchive.SameBoundary(data.PublishedBoundary, corrected);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Scene = Path.GetFileName(path), data.Territory, data.Floor, data.Layout,
            capture.CapturedAtUtc, capture.Radius, BaseMatches = baseMatches, PublishedMatches = publishedMatches,
            OriginalCapture = capture.CaptureDiagnostics, OriginalBuild = capture.Result.Diagnostics, Rebuilt = result.Diagnostics,
        }));
        return baseMatches && publishedMatches;
    }

    private static void InDirectory(Action<string> action)
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), ".tmp", "terrain-archive-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static void ExpectInvalid(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        catch (EndOfStreamException) { return; }
        throw new Exception("Corrupt terrain archive was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
