using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using MasterPlayer.Spatial.Terrain;

internal static class SceneTerrainBenchmark
{
    internal static void Run()
    {
        var epoch = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
        foreach (var name in new[] { "room", "corridor-turn" })
        {
            var scene = name == "room" ? SceneTerrainTests.Floor(
                SceneTerrainTests.Box(2, new(.001f, 1.5f, 8), new(-8, 1.5f, 0)),
                SceneTerrainTests.Box(3, new(.001f, 1.5f, 8), new(8, 1.5f, 0)),
                SceneTerrainTests.Box(4, new(8, 1.5f, .001f), new(0, 1.5f, -8)),
                SceneTerrainTests.Box(5, new(8, 1.5f, .001f), new(0, 1.5f, 8)))
                : SceneTerrainTests.Floor(
                    SceneTerrainTests.Box(2, new(.001f, 1.5f, 17.25f), new(-1.5f, 1.5f, -12.75f)),
                    SceneTerrainTests.Box(3, new(.001f, 1.5f, 15.75f), new(1.5f, 1.5f, -14.25f)),
                    SceneTerrainTests.Box(4, new(14.25f, 1.5f, .001f), new(15.75f, 1.5f, 1.5f)),
                    SceneTerrainTests.Box(5, new(15.75f, 1.5f, .001f), new(14.25f, 1.5f, 4.5f)));
            var cold = SceneTerrainBuilder.Build(scene, Vector3.Zero, 20, default);
            var warm = Enumerable.Range(0, 5).Select(_ => SceneTerrainBuilder.Build(scene, Vector3.Zero, 20, default).Milliseconds).Order().ToArray();
            var rays = TerrainScanBenchmark.Scene(name, true);
            rays.WorkMilliseconds = .002;
            var scan = new LocalTerrainScanner(new GameTerrainProbe(rays));
            var frames = 0;
            var clock = Stopwatch.StartNew();
            do { scan.Update(Vector3.Zero, 20, epoch.AddMilliseconds(frames++ * 16)); } while (!scan.Complete);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Scene = name, OldGridMilliseconds = clock.Elapsed.TotalMilliseconds, OldGridFrames = frames, OldGridRays = rays.Calls,
                ColdManagedBuildMilliseconds = cold.Milliseconds, WarmManagedMedianMilliseconds = warm[2], StaticBuildRays = 0,
                Triangles = cold.Triangles, WalkableCells = cold.Boundary.WalkableCount,
                ColdStages = cold.Diagnostics,
                Note = "Analytic geometry; simulated 2us native-ray cost. Native snapshot acquisition and live route guard are excluded.",
            }));
        }
    }
}
