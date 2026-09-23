using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Hooking;
using ECommons.DalamudServices;
using MasterPlayer.BmrRuntime;
using NativeActionEffectHandler = FFXIVClientStructs.FFXIV.Client.Game.Character.ActionEffectHandler;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using NativeGameObjectId = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObjectId;

namespace MasterPlayer.BmrBridge;

public sealed unsafe class BmrActionEffectCapture : IDisposable
{
    private const int MaxTargets = 32;

    private readonly ConcurrentQueue<BmrEventActionEffect> _pending = new();
    private Hook<NativeActionEffectHandler.Delegates.Receive>? _hook;
    private bool _disposed;

    public bool IsActive => !_disposed && _hook is { IsEnabled: true };
    public string? LastError { get; private set; }

    public BmrActionEffectCapture()
    {
        try
        {
            _hook = Svc.Hook.HookFromAddress<NativeActionEffectHandler.Delegates.Receive>(
                NativeActionEffectHandler.Addresses.Receive.Value, Detour);
            _hook.Enable();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Svc.Log.Warning($"[MasterPlayer] BMR 效果包捕获 hook 安装失败，依赖效果数组的组件降级运行：{ex.Message}");
        }
    }

    private void Detour(uint casterId, NativeCharacter* casterObj, Vector3* targetPos,
        NativeActionEffectHandler.Header* header, NativeActionEffectHandler.TargetEffects* effects, NativeGameObjectId* targets)
    {
        try
        {
            var count = Math.Min((int)header->NumTargets, MaxTargets);
            var targetDtos = new BmrActionEffectTarget[count];
            var raw = (ulong*)effects;
            for (var i = 0; i < count; ++i)
            {
                var rawEffects = new ulong[8];
                for (var j = 0; j < rawEffects.Length; ++j)
                    rawEffects[j] = raw[i * 8 + j];
                targetDtos[i] = new(targets[i], rawEffects);
            }
            _pending.Enqueue(new(casterId, header->ActionType, header->ActionId, header->AnimationTargetId,
                header->AnimationLock, *targetPos, header->GlobalSequence, header->SourceSequence, header->RotationInt, targetDtos));
        }
        catch
        {
        }
        _hook!.Original(casterId, casterObj, targetPos, header, effects, targets);
    }

    public void Drain(BmrWorldFeed? feed)
    {
        while (_pending.TryDequeue(out var effectEvent))
            feed?.Enqueue(effectEvent);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            _hook?.Dispose();
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[MasterPlayer] BMR 效果包捕获 hook 卸载失败：{ex.Message}");
        }
        _hook = null;
        Drain(null);
    }
}
