using ECommons.DalamudServices;
using Dalamud.Bindings.ImGui;

namespace MasterPlayer.Windows;

public sealed partial class MainWindow
{
    private static void DrawTerrainDiagnostics()
    {
        var runtime = MasterPlayerPlugin.Instance?.SafeZone;
        if (runtime == null) return;
        Md.BeginCard("terrain_diagnostics", Md.CardKind.Outlined);
        Md.SectionLabel("地形场景诊断", "导出最近一次完成建模的场景，供离线重建；不会重新采样或改变移动状态。");
        var capture = runtime.TerrainCapture;
        if (capture != null)
            Md.ValueRow("建模快照", $"{capture.CapturedAtUtc:HH:mm:ss.fff} UTC · 半径 {capture.Radius:F0} 码 · {capture.Scene.Instances.Length} 个实例");
        ImGui.BeginDisabled(capture == null || runtime.TerrainExporting);
        if (ImGui.Button("导出地形场景"))
            runtime.ExportTerrainScene(Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "MasterPlayer", "TerrainCaptures"));
        ImGui.EndDisabled();
        Md.MutedText(runtime.TerrainExportStatus);
        if (runtime.TerrainCaptureDiagnostics is { } sampled)
        {
            Md.ValueRow("最近采集尝试", $"{(sampled.Complete ? "完整" : "等待补齐")} · {sampled.TotalMilliseconds:F2} ms · 锁内 {sampled.LockMilliseconds:F2} ms");
            Md.ValueRow("原生复制 / 内容校验", $"{sampled.CopyMilliseconds:F2} / {sampled.HashMilliseconds:F2} ms");
            Md.ValueRow("采集计数", $"遍历 {sampled.VisitedColliders} · 纳入 {sampled.Instances} · 缓存命中 {sampled.CacheHits}");
            Md.ValueRow("模型复制", $"{sampled.CopiedModels} 个 / {sampled.CopiedBytes / 1024.0:F1} KiB · 校验 {sampled.HashedModels} 个 · 缓存 {sampled.CachedBytes / 1048576.0:F1} MiB");
        }
        if (capture is { Result.Diagnostics: { } built })
        {
            if (capture.CaptureDiagnostics is { } captured)
                Md.ValueRow("建模输入采集累计", $"{captured.Attempts} 次尝试 · {captured.TotalMilliseconds:F2} ms · 锁内 {captured.LockMilliseconds:F2} ms · 复制 {captured.CopiedBytes / 1024.0:F1} KiB");
            Md.ValueRow("最近后台构建", $"{built.TotalMilliseconds:F2} ms · 新解码 {built.DecodedModels} 个模型");
            Md.ValueRow("解码 / 顶点变换 / 面分类", $"{built.DecodeMilliseconds:F2} / {built.TransformMilliseconds:F2} / {built.ClassifyMilliseconds:F2} ms");
            Md.ValueRow("光栅化 / 过滤 / 紧凑高度场", $"{built.RasterizeMilliseconds:F2} / {built.FilterMilliseconds:F2} / {built.CompactMilliseconds:F2} ms");
            Md.ValueRow("禁行扣除 / 腐蚀", $"{built.ForbiddenMilliseconds:F2} / {built.ErodeMilliseconds:F2} ms");
            Md.ValueRow("可达区域 / 边界生成", $"{built.ReachabilityMilliseconds:F2} / {built.BoundaryMilliseconds:F2} ms");
            Md.ValueRow("几何输入", $"{built.InputVertices} 个实例顶点 · {built.InputTriangles} 个输入面 · {built.RasterizedTriangles} 个栅格面（禁行 {built.ForbiddenTriangles}）");
            Md.ValueRow("高度场", $"{built.CompactSpans} 个跨度 · 可达 {built.ReachableSpans} · 通行 {built.WalkableCells} 格");
        }
        Md.MutedText("最近采集与建模输入累计分别统计。后台阶段不含排队、主线程采集及实时路线复核。");
        Md.EndCard();
    }
}
