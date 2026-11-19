using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using MasterPlayer.Spatial.Terrain;

internal static class TerrainScanBenchmark
{
    internal static AnalyticTerrainRaycaster Scene(string name, bool omitNormals)
    {
        var rays = new AnalyticTerrainRaycaster { OmitNormals = omitNormals };
        if (name == "room")
        {
            rays.Walls.Add((-8, -8, 8, 3)); rays.Walls.Add((8, -8, 8, 3));
            rays.CrossWalls.Add((-8, -8, 8, 3)); rays.CrossWalls.Add((8, -8, 8, 3));
        }
        else if (name == "corridor-turn")
        {
            rays.Walls.Add((-1.5f, -30, 4.5f, 3)); rays.Walls.Add((1.5f, -30, 1.5f, 3));
            rays.CrossWalls.Add((1.5f, 1.5f, 30, 3)); rays.CrossWalls.Add((4.5f, -1.5f, 30, 3));
        }
        return rays;
    }

    internal static void Run(bool nativeBudget = false)
    {
        var epoch = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
        foreach (var name in new[] { "open", "room", "corridor-turn" })
            foreach (var omitNormals in new[] { false, true })
            {
                var rays = Scene(name, omitNormals);
                if (nativeBudget) rays.WorkMilliseconds = .002;
                var scan = new LocalTerrainScanner(new GameTerrainProbe(rays));
                var frames = 0; var nearFrame = 0;
                var clock = Stopwatch.StartNew();
                do
                {
                    // Unrestricted mode isolates work caps; budgeted mode models a 2us native query.
                    scan.Update(Vector3.Zero, 20, epoch.AddMilliseconds(frames * 16), budgetMilliseconds: nativeBudget ? 1 : 1000);
                    ++frames;
                    if (nearFrame == 0 && scan.Current?.Contains(new(0, 0, 3)) == true) nearFrame = frames;
                    if (frames > 10000) throw new InvalidOperationException("Benchmark failed to complete.");
                } while (!scan.Complete);
                var initialCalls = rays.Calls;
                var initialSamples = scan.SampledCount;
                var initialMilliseconds = clock.Elapsed.TotalMilliseconds;
                for (var i = 0; i < 600; ++i)
                    scan.Update(Vector3.Zero, 20, epoch.AddSeconds(10).AddMilliseconds(i * 16), budgetMilliseconds: nativeBudget ? 1 : 1000);
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    Scene = name, OmitNormals = omitNormals, Budgeted = nativeBudget, Frames = frames, NearFrame = nearFrame,
                    Samples = initialSamples, Rays = initialCalls, Walkable = scan.Current!.WalkableCount,
                    InitialMilliseconds = Math.Round(initialMilliseconds, 2), IdleRays = rays.Calls - initialCalls,
                }));
            }
    }
}
