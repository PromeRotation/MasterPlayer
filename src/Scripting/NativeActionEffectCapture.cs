using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Hooking;
using ECommons.DalamudServices;
using NativeActionEffectHandler = FFXIVClientStructs.FFXIV.Client.Game.Character.ActionEffectHandler;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using NativeGameObjectId = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObjectId;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterPlayer.Scripting;

internal sealed unsafe class NativeActionEffectCapture : IDisposable
{
    private const int MAX_TARGETS = 32;
    private readonly object hookLock = new();
    private readonly ConcurrentQueue<(int Epoch, NativeActionEffectEvent Event)> pending = new();
    private Hook<NativeActionEffectHandler.Delegates.Receive>? hook;
    private string? pendingError;
    private int epoch;
    private bool disposed;
    private bool replayMode;

    internal NativeActionEffectCapture(bool installHook = true)
    {
        if (!installHook) return;
        try
        {
            hook = Svc.Hook.HookFromAddress<NativeActionEffectHandler.Delegates.Receive>(
                NativeActionEffectHandler.Addresses.Receive.Value, Detour);
            hook.Enable();
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[PromeRadar] 原生效果包采集安装失败；依赖主目标或完整目标集合的脚本无法结算。");
        }
    }

    private void Detour(uint casterId, NativeCharacter* casterObj, Vector3* targetPos,
        NativeActionEffectHandler.Header* header, NativeActionEffectHandler.TargetEffects* effects, NativeGameObjectId* targets)
    {
        lock (hookLock)
        {
            if (!disposed && !replayMode)
            {
                try
                {
                    if (header == null || header->NumTargets > MAX_TARGETS
                        || header->NumTargets != 0 && (targets == null || effects == null))
                        throw new InvalidDataException("效果包头或目标数组无效。");
                    var ids = new ulong[header->NumTargets];
                    var rawEffects = new ulong[ids.Length][];
                    var raw = (ulong*)effects;
                    for (var i = 0; i < ids.Length; ++i)
                    {
                        ids[i] = targets[i];
                        rawEffects[i] = new ulong[8];
                        for (var j = 0; j < 8; ++j) rawEffects[i][j] = raw[i * 8 + j];
                    }
                    Capture(casterId, header->ActionId, header->AnimationTargetId, header->GlobalSequence, ids,
                        targetPosition: targetPos == null ? null : *targetPos, rawEffects: rawEffects, actionType: header->ActionType,
                        sourceOid: casterObj == null ? null : ((NativeGameObject*)casterObj)->BaseId,
                        sourceObjectIndex: casterObj == null ? null : ((NativeGameObject*)casterObj)->ObjectIndex);
                }
                catch (Exception ex) { Interlocked.Exchange(ref pendingError, ex.Message); }
            }
            hook!.OriginalDisposeSafe(casterId, casterObj, targetPos, header, effects, targets);
        }
    }

    internal void Capture(ulong source, uint action, ulong mainTarget, uint sequence, ReadOnlySpan<ulong> targets, DateTime? at = null,
        Vector3? targetPosition = null, ulong[][]? rawEffects = null, byte? actionType = null,
        uint? sourceOid = null, int? sourceObjectIndex = null)
    {
        lock (hookLock)
        {
            if (disposed || replayMode) return;
            if (targets.Length > MAX_TARGETS) throw new InvalidDataException("效果包目标过多。");
            if (rawEffects != null && (rawEffects.Length != targets.Length || rawEffects.Any(slots => slots is not { Length: 8 })))
                throw new InvalidDataException("原始效果槽必须与目标数组同序且每目标恰八槽。");
            pending.Enqueue((epoch, new()
            {
                SourceId = source, ActionId = action, MainTargetId = mainTarget, GlobalSequence = sequence,
                Targets = targets.ToArray(), Timestamp = (at ?? DateTime.UtcNow).ToUniversalTime(),
                TargetPosition = targetPosition, RawEffects = rawEffects?.Select(slots => slots.ToArray()).ToArray(),
                ActionType = actionType, SourceOid = sourceOid, SourceObjectIndex = sourceObjectIndex,
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

    internal void Drain(bool replayActive, DateTime? through = null)
    {
        ReportError();
        through = through?.ToUniversalTime();
        while (pending.TryPeek(out var next) && (through == null || next.Event.Timestamp <= through)
            && pending.TryDequeue(out var entry))
            if (!replayActive && entry.Epoch == Volatile.Read(ref epoch))
                NativeActionEffectEvents.Publish(entry.Event,
                    ex => Svc.Log.Error(ex, "[PromeRadar] 原生效果包订阅回调失败"));
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
            Svc.Log.Error($"[PromeRadar] 原生效果包采集失败：{error}");
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
