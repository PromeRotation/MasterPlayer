using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using PromeRotation.Hosting;
using PromeRotation.LogSystem;
using PromeRotation.Spatial.SafeZone;
using NativeActionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace MasterPlayer.Dodge;

public sealed class DodgeController : IDisposable
{
    private static readonly TimeSpan PruneInterval = TimeSpan.FromSeconds(0.5);

    private readonly List<SafeZoneAreaHandle> _registeredHandles = new();
    private readonly IDisposable _startCastSubscription;
    private DateTime _nextPruneUtc = DateTime.MinValue;
    private bool _disposed;

    public DodgeController()
    {
        _startCastSubscription = PromeEventBus.OnStartCast(this, HandleStartCast);
    }

    // PromeEventBus 在主线程派发（底层是 Plugin.Instance.LogSystem.Events），回调里直接注册危险区。
    private void HandleStartCast(LogSystemCastStartEvent e)
    {
        var settings = MasterPlayerSettings.Instance;
        if (_disposed || !(settings.EnableAutoDodge || settings.EnableRadar || settings.EnableRadarOverlay))
        {
            return;
        }

        ProcessCast(e);
    }

    public void Update()
    {
        if (_disposed)
        {
            return;
        }

        PruneHandles();
    }

    private void ProcessCast(LogSystemCastStartEvent e)
    {
        // 运行中脚本声明接管的技能：通用躲避让位，避免与脚本重复注册同一危险区
        if (MasterPlayerPlugin.Instance?.Scripts?.IsGenericDodgeSuppressed(e) == true)
        {
            return;
        }

        if (!IsHostileCaster(e, out var casterHitboxRadius))
        {
            return;
        }

        if (!AoeShapeDatabase.TryGetShape(e.ActionId, casterHitboxRadius, out var descriptor) || descriptor is null)
        {
            return;
        }

        var template = AoeShapeDatabase.CreateTemplate(descriptor, e);
        if (template is null)
        {
            return;
        }

        template = DangerMarginInflator.Inflate(template, MasterPlayerSettings.Instance.DodgeDangerMargin);

        if (!AoeShapeDatabase.TryCreateBinding(e, descriptor, out var binding))
        {
            Svc.Log.Warning($"[MasterPlayer] 无法读取当前施法朝向，跳过 AOE 预测 (ActionId={e.ActionId}, SourceId={e.SourceId:X})");
            return;
        }
        var delaySeconds = MathF.Max(0f, e.CastTime + descriptor.ExtraDelaySeconds);

        try
        {
            var handle = PromeRotation.Plugin.Instance.SafeZoneRebornRegistry.AddDelayedHit(template, binding, delaySeconds);
            if (handle.IsValid)
            {
                _registeredHandles.Add(handle);
                var action = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(e.ActionId);
                var actionName = action?.Name.ToString();
                var label = string.IsNullOrWhiteSpace(actionName) ? $"读条技能 {e.ActionId}" : $"{actionName}（{e.ActionId}）";
                MasterPlayerPlugin.Instance?.AoeVisibility?.Track(handle, $"action/{e.ActionId}", label);
                if (MasterPlayerSettings.Instance.DebugMode)
                {
                    Svc.Log.Info($"[MasterPlayer] 注册躲避危险区 ActionId={e.ActionId} Kind={descriptor.Kind} Delay={delaySeconds:0.0}s");
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"[MasterPlayer] 注册躲避危险区失败 (ActionId={e.ActionId})");
        }
    }

    // 敌我过滤（对齐 BMR AIHintsBuilder.OnCastStarted / WorldStateGameSync 的 IsAlly 判定）：
    // 只响应敌对 BattleNpc 的读条；玩家、宠物、坐骑、事件 NPC 一律不算。
    // 友方战斗 NPC（剧情/Trust 等同盟单位）类型也是 BattleNpc，用游戏原生 ActionManager.ClassifyTarget 甄别（!= Enemy 即盟友）。
    // 对象表无法解析的施法者（多为场外辅助体）放行，避免丢预告。
    private static unsafe bool IsHostileCaster(LogSystemCastStartEvent e, out float casterHitboxRadius)
    {
        casterHitboxRadius = 0f;
        var sourceId = (uint)e.SourceId;
        if (sourceId == 0)
        {
            return false;
        }

        if (Svc.Objects.SearchByEntityId(sourceId) is not { } caster)
        {
            return true;
        }

        casterHitboxRadius = caster.HitboxRadius;
        if (caster.ObjectKind != ObjectKind.BattleNpc || caster.Address == nint.Zero)
        {
            return false;
        }

        return NativeActionManager.ClassifyTarget((NativeCharacter*)caster.Address)
            == NativeActionManager.TargetCategory.Enemy;
    }

    private void PruneHandles()
    {
        if (_registeredHandles.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now < _nextPruneUtc)
        {
            return;
        }
        _nextPruneUtc = now + PruneInterval;

        try
        {
            var manager = PromeRotation.Plugin.Instance.SafeZoneRebornManager;
            for (var i = _registeredHandles.Count - 1; i >= 0; i--)
            {
                if (!manager.ContainsArea(_registeredHandles[i]))
                {
                    MasterPlayerPlugin.Instance?.AoeVisibility?.Remove(_registeredHandles[i]);
                    _registeredHandles.RemoveAt(i);
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MasterPlayer] 清理失效躲避危险区句柄失败");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        try
        {
            _startCastSubscription.Dispose();
        }
        catch (Exception ex)
        {
            SafeLogError(ex, "[MasterPlayer] 退订 OnStartCast 失败");
        }

        try
        {
            var registry = PromeRotation.Plugin.Instance.SafeZoneRebornRegistry;
            foreach (var handle in _registeredHandles)
            {
                try
                {
                    registry.Remove(handle);
                    MasterPlayerPlugin.Instance?.AoeVisibility?.Remove(handle);
                }
                catch (Exception ex)
                {
                    SafeLogError(ex, "[MasterPlayer] 移除躲避危险区失败");
                }
            }
        }
        catch (Exception ex)
        {
            SafeLogError(ex, "[MasterPlayer] 获取 SafeZoneRegistry 失败，残留危险区由本体生命周期事件清理");
        }

        _registeredHandles.Clear();
    }

    private static void SafeLogError(Exception ex, string message)
    {
        try
        {
            Svc.Log.Error(ex, message);
        }
        catch
        {
        }
    }
}
