using System.Numerics;
using MasterPlayer.Mechanics.Crucible;

internal static class CrucibleGeometryRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("capsule analytic geometry matches distance semantics and skips the constraint grid", () =>
        {
            var capsule = new SDCapsule(new WPos(100, 200), new WDir(0.6f, 0.8f), 3f, 2.3f);
            var geometry = capsule.Geometry(5f);
            Check(geometry != null, "capsule must produce analytic geometry instead of the constraint grid");

            var rng = new Random(42);
            var compared = 0;
            for (var i = 0; i < 4000; ++i)
            {
                var x = 96f + (float)rng.NextDouble() * 10f;
                var z = 196f + (float)rng.NextDouble() * 10f;
                var distance = capsule.Distance(new WPos(x, z));
                if (MathF.Abs(distance) < 0.06f)
                    continue; // 弦逼近误差带（<0.05m）不判定
                ++compared;
                Check((distance <= 0f) == geometry.Contains(new Vector3(x, 5f, z)),
                    $"capsule mismatch at ({x:F2},{z:F2}): distance={distance:F3}");
            }
            Check(compared > 3000, "sampling did not cover enough clear-cut points");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 10; ++i)
                _ = FrozenConstraintGeometry.Create(p => capsule.Distance(p), new WPos(100, 200), 5f);
            var frozenMs = sw.Elapsed.TotalMilliseconds / 10;
            sw.Restart();
            for (var i = 0; i < 10; ++i)
                _ = capsule.Geometry(5f);
            var analyticMs = sw.Elapsed.TotalMilliseconds / 10;
            Console.WriteLine($"INFO capsule geometry cost: analytic={analyticMs:F3}ms vs frozen grid={frozenMs:F2}ms per creation");
            Check(analyticMs < frozenMs / 20f,
                $"analytic capsule ({analyticMs:F3}ms) must be far cheaper than the constraint grid ({frozenMs:F2}ms)");
        });

        run("frozen-path shapes expose stable content keys for geometry caching", () =>
        {
            var capsule = new SDCapsule(new WPos(1, 2), new WDir(0, 1), 3f, 2f);
            Check(capsule.ContentKey() == null, "high-frequency moving capsules must not occupy the cache");

            var i1 = new SDIntersection(new SDCircle(new WPos(0, 0), 5f), new SDInvertedRect(new WPos(0, 0), new WDir(0, 1), 4f, 1f, 2f));
            var i2 = new SDIntersection(new SDCircle(new WPos(0, 0), 5f), new SDInvertedRect(new WPos(0, 0), new WDir(0, 1), 4f, 1f, 2f));
            Check(i1.ContentKey() != null && i1.ContentKey() == i2.ContentKey(), "identical intersections must share a content key");
            var i3 = new SDIntersection(new SDCircle(new WPos(1, 0), 5f), new SDInvertedRect(new WPos(0, 0), new WDir(0, 1), 4f, 1f, 2f));
            Check(i1.ContentKey() != i3.ContentKey(), "moved intersection kept the stale key");
            Check(new SDIntersection(capsule).ContentKey() == null, "uncacheable member must poison the intersection key");

            var k1 = new SDKnockbackInAABBRectAwayFromOrigin(new WPos(0, 0), new WPos(0, -5), 10f, 3f, 4f);
            var k2 = new SDKnockbackInAABBRectAwayFromOrigin(new WPos(0, 0), new WPos(0, -5), 10f, 3f, 4f);
            Check(k1.ContentKey() == k2.ContentKey(), "identical knockback fields must share a content key");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
