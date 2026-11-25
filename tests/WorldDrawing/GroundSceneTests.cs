using System.Diagnostics;
using System.Numerics;
using MasterPlayer.Drawing;
using MasterPlayer.Spatial.Terrain;

internal static class GroundSceneTests
{
    private sealed class Source : ITerrainSceneSource
    {
        internal TerrainSceneSnapshot Scene = GroundDrawingTests.Scene(GroundDrawingTests.Grid([-4, 0, 4], [-4, 0, 4], (_, _) => 0));
        internal bool Fail;
        internal int Captures;
        public bool TryCapture(Vector3 origin, float radius, out TerrainSceneSnapshot? snapshot)
        {
            ++Captures;
            if (Fail) throw new InvalidDataException("fixture capture error");
            snapshot = Scene;
            return true;
        }
    }

    internal static async Task RunAsync()
    {
        var source = new Source();
        using var controller = new GroundSceneController(source);
        var now = DateTime.UtcNow;
        async Task Publish(byte floor)
        {
            var clock = Stopwatch.StartNew();
            while (controller.Current == null && clock.Elapsed < TimeSpan.FromSeconds(3))
            {
                controller.Update(Vector3.Zero, 1, (0, floor, 0), now);
                await Task.Delay(5);
            }
            Require(controller.Current != null, "后台地表未发布");
        }
        await Publish(1);
        var original = controller.Current;
        now = now.AddSeconds(1);
        controller.Update(Vector3.Zero, 1, (0, 1, 0), now);
        Require(ReferenceEquals(original, controller.Current), "相同地形重复建模");
        controller.Update(new Vector3(5, 0, 0), 1, (0, 1, 0), now);
        Require(ReferenceEquals(original, controller.Current), "移动窗口刷新使仍然有效的地面范围消失");
        controller.Clear();
        await Publish(1);
        original = controller.Current;

        source.Scene = GroundDrawingTests.Scene(GroundDrawingTests.Grid([-4, 0, 4], [-4, 0, 4], (_, _) => 1));
        now = now.AddSeconds(1);
        controller.Update(Vector3.Zero, 1, (0, 1, 0), now);
        Require(controller.Current == null, "地形变化未立即失效旧面");
        await Publish(1);
        Require(!ReferenceEquals(original, controller.Current), "地形变化后仍使用旧面");
        controller.Update(Vector3.Zero, 1, (0, 2, 0), now);
        Require(controller.Current == null, "同区域换层未清理地表");
        controller.Clear();
        await Task.Delay(20);
        controller.Update(null, 1, default, now);
        Require(controller.Current == null, "已撤销的后台结果重新发布");

        source.Fail = true;
        controller.Update(Vector3.Zero, 1, (0, 3, 0), now);
        Require(controller.Current == null && controller.Status.Contains("fixture capture error"), "采集失败被静默吞掉");
        var captures = source.Captures;
        controller.Update(null, 1, default, now);
        Require(source.Captures == captures, "无贴地需求时仍访问原生场景");
        Console.WriteLine("PASS ground lifecycle: reuse, scene invalidation, layout reset, cancelled publication and visible failures");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
