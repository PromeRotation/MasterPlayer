using System.Collections.Concurrent;
using Dalamud.Hooking;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace MasterPlayer.Scripting;

internal sealed unsafe class LegacyMapEffectCapture : IDisposable
{
    private const int MAX_DATA_LENGTH = 4096;
    private readonly object hookLock = new();
    private readonly ConcurrentQueue<(int Epoch, LegacyMapEffectEvent Event)> pending = new();
    private Hook<EventFramework.Delegates.SetDirectorData>? hook;
    private string? pendingError;
    private int epoch;
    private volatile bool disposed;
    private bool replayMode;

    internal LegacyMapEffectCapture(bool installHook = true)
    {
        // 离线验证复用同一缓冲和会话隔离逻辑，不安装游戏 hook。
        if (!installHook) return;
        try
        {
            hook = Svc.Hook.HookFromAddress<EventFramework.Delegates.SetDirectorData>(
                EventFramework.Addresses.SetDirectorData.Value, Detour);
            hook.Enable();
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[PromeRadar] Director 旧式地图事件采集安装失败；植物园首段无法识别完成事件。");
        }
    }

    private void Detour(EventFramework* framework, EventId eventId, byte sequence, byte param, byte* data, ulong length)
    {
        lock (hookLock)
        {
            // 与卸载串行；已经进入 detour 的调用在卸载后仍须执行还原的原函数。
            hook!.OriginalDisposeSafe(framework, eventId, sequence, param, data, length);
            if (disposed || replayMode) return;
            try
            {
                if (length > MAX_DATA_LENGTH || data == null && length != 0)
                    throw new InvalidDataException($"Director UnionData 长度或指针无效：{length}。");
                Capture(sequence, param, new ReadOnlySpan<byte>(data, (int)length));
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref pendingError, ex.Message);
            }
        }
    }

    internal void Capture(byte sequence, byte param, ReadOnlySpan<byte> data)
    {
        lock (hookLock)
        {
            if (disposed || replayMode) return;
            if (data.Length > MAX_DATA_LENGTH) throw new InvalidDataException("Director UnionData 过长。");
            pending.Enqueue((epoch, new LegacyMapEffectEvent
            {
                Sequence = sequence, Param = param, Data = data.ToArray(), Timestamp = DateTime.UtcNow,
            }));
        }
    }

    internal void SetReplayMode(bool active)
    {
        lock (hookLock)
        {
            Clear();
            replayMode = active;
        }
    }

    internal void Drain(bool replayActive)
    {
        ReportError();
        while (pending.TryDequeue(out var entry))
            if (!replayActive && entry.Epoch == Volatile.Read(ref epoch))
                LegacyMapEffectEvents.Publish(entry.Event,
                    ex => Svc.Log.Error(ex, "[PromeRadar] Director 旧式地图事件订阅回调失败"));
    }

    internal void Clear()
    {
        lock (hookLock)
        {
            Interlocked.Increment(ref epoch);
            while (pending.TryDequeue(out _)) { }
        }
    }

    private void ReportError()
    {
        if (Interlocked.Exchange(ref pendingError, null) is { } error)
            Svc.Log.Error($"[PromeRadar] Director 旧式地图事件采集失败：{error}");
    }

    public void Dispose()
    {
        lock (hookLock)
        {
            if (disposed) return;
            disposed = true;
            try { hook?.Dispose(); }
            finally { Clear(); ReportError(); }
        }
    }
}
