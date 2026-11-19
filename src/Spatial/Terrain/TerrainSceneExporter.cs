using System.Collections.Concurrent;

namespace MasterPlayer.Spatial.Terrain;

internal sealed class TerrainSceneExporter
{
    private readonly ConcurrentQueue<(string? Path, Exception? Error)> completed = new();
    private bool running;
    private string status = "尚未导出";
    internal bool IsRunning { get { Poll(); return running; } }
    internal string Status { get { Poll(); return status; } }

    internal bool Start(string directory, TerrainSceneArchive.Data data)
    {
        Poll();
        if (running) return false;
        running = true;
        status = "正在后台导出地形场景…";
        _ = Task.Run(() =>
        {
            string? temporary = null;
            try
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, $"terrain_{data.Capture.CapturedAtUtc:yyyyMMdd_HHmmss_fff}_t{data.Territory}_{Guid.NewGuid():N}.terrain.zip");
                temporary = path + ".tmp";
                TerrainSceneArchive.Write(temporary, data);
                File.Move(temporary, path);
                completed.Enqueue((path, null));
            }
            catch (Exception ex)
            {
                if (temporary != null && File.Exists(temporary))
                {
                    try { File.Delete(temporary); }
                    catch (Exception cleanup) { ex = new AggregateException(ex, cleanup); }
                }
                completed.Enqueue((null, ex));
            }
        });
        return true;
    }

    private void Poll()
    {
        while (completed.TryDequeue(out var result))
        {
            running = false;
            status = result.Error == null ? $"已导出：{result.Path}" : $"地形导出失败：{result.Error}";
        }
    }
}
