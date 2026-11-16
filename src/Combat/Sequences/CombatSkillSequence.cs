using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using MasterPlayer.Settings;
using PromeRotation.Data;
using PromeRotation.Helpers;
using PromeRotation.Hosting;
using PromeRotation.LogSystem;
using PromeRotation.Updaters;
using HostActionType = PromeRotation.Data.ActionType;
using NativeActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace MasterPlayer.Combat.Sequences;

internal sealed unsafe class CombatSkillSequence : IDisposable
{
    private readonly SkillSequenceRunner runner = new();
    private readonly IDisposable effects;
    private bool wasInCombat, pendingStart, pendingCast, pausedAcr, disposed;
    private uint runningJob, pendingId, pendingAdjusted, lastSequence;
    private uint[]? chain;
    private int chainIndex, attempts, previousStep = -1;
    private DateTime attemptedAt, blockedSince;
    private string error = "";
    public string Status => string.IsNullOrEmpty(error) ? runner.Status : error;
    public bool Running => runner.Running;

    public CombatSkillSequence() => effects = PromeEventBus.OnActionEffect(this, OnEffect);

    public void Stop()
    {
        runner.Stop();
        pendingStart = false;
        ClearAction();
        ReleaseAcr();
    }

    public void Update()
    {
        try { UpdateCore(); }
        catch (Exception ex)
        {
            Stop();
            error = $"技能序列已停止：{ex.Message}";
            Svc.Log.Error(ex, "[PromeRadar] 技能序列执行失败");
        }
    }

    private void UpdateCore()
    {
        if (disposed) return;
        if (PlayerPerspective.IsReplay || !MasterPlayerSettings.Instance.EnableSkillSequences
            || Svc.Objects.LocalPlayer is not { IsDead: false } player)
        {
            Stop();
            wasInCombat = false;
            return;
        }
        var inCombat = Svc.Condition[ConditionFlag.InCombat];
        if (inCombat && !wasInCombat) pendingStart = true;
        wasInCombat = inCombat;
        if (!inCombat)
        {
            Stop();
            return;
        }
        var job = player.ClassJob.RowId;
        if (pausedAcr && PromeSettings.Instance.EnableAcr != AcrState.Hold)
        {
            Stop();
            return;
        }
        var profile = MasterPlayerSettings.Instance.SkillSequences.FirstOrDefault(p => p.JobId == job && p.IsActive);
        if (runner.Running && (job != runningJob || profile?.EnableAutoRun != true)) Stop();
        if (pendingStart && Svc.Condition[ConditionFlag.NormalConditions])
        {
            pendingStart = false;
            if (profile is { EnableAutoRun: true, Entries.Count: > 0 })
            {
                runner.Start(profile, DateTime.UtcNow);
                runningJob = job;
                error = "";
                if (PromeSettings.Instance.EnableAcr == AcrState.On)
                {
                    PromeSettings.Instance.EnableAcr = AcrState.Hold;
                    pausedAcr = true;
                }
            }
        }
        if (!runner.Running) return;
        var now = DateTime.UtcNow;
        if (player.IsCasting)
        {
            if (pendingId != 0) { pendingCast = true; attemptedAt = now; }
            return;
        }
        if (PromeRotation.Core.GameData.IsPlayerOccupied()) return;
        if (pendingId != 0)
        {
            if (now < attemptedAt.AddSeconds(1)) return;
            if (pendingCast)
            {
                Skip(now, "读条结束后未收到效果确认，已跳过");
                return;
            }
            pendingId = pendingAdjusted = 0;
        }
        var statuses = player.StatusList.Select(s => s.StatusId).ToHashSet();
        var entry = runner.Tick(now, statuses);
        var command = runner.TakeCompletionCommand();
        if (!string.IsNullOrWhiteSpace(command)) new PromeRotation.Timeline.Actions.ExecuteCommandAction(command).Execute();
        if (entry == null)
        {
            if (!runner.Running) ReleaseAcr();
            return;
        }
        if (previousStep != runner.StepIndex)
        {
            ClearAction();
            previousStep = runner.StepIndex;
        }
        if (chain == null)
            chain = job == 30 && !statuses.Contains(496) ? NinjaSequence.Expand(entry.SkillId) : [entry.SkillId];
        var requested = chain[chainIndex];
        var adjusted = ActionHelper.GetAdjustedActionId(requested);
        if (job == 30 && chain.Length > 1 && !NinjaSequence.IsMudra(requested))
        {
            adjusted = ActionHelper.GetAdjustedActionId(2260);
            if (adjusted != requested)
            {
                WaitOrSkip(now, "忍术结印结果不匹配");
                return;
            }
        }
        var mudra = NinjaSequence.IsMudra(requested);
        var type = mudra ? HostActionType.Always : ActionHelper.ResolveActionType(adjusted);
        if (ActionHelper.GetAnimationLock() > 0) return;
        var gcd = ActionHelper.GetGcdRemain();
        if (!mudra && (type == HostActionType.Gcd && gcd > 0.5f || type == HostActionType.OffGcd && gcd is > 0 and <= 0.6f)) return;
        if (!ActionHelper.IsActionAvailableByLevelAndQuest(adjusted))
        {
            Skip(now, "技能未解锁，已跳过");
            return;
        }
        var targetSelf = entry.TargetSelf || mudra
            || ActionHelper.TryResolveActionTargetType(adjusted, out var targetType) && targetType == ActionTargetType.Self;
        var target = targetSelf ? player : ResolveTarget(player.Position);
        var manager = ActionManager.Instance();
        if (manager == null || target == null) { WaitOrSkip(now, "无有效目标，已跳过"); return; }
        var status = manager->GetActionStatus(NativeActionType.Action, adjusted, target.GameObjectId, checkRecastActive: type != HostActionType.Gcd);
        if (type == HostActionType.Gcd && ActionHelper.GetActionCooldown(adjusted) > Math.Max(gcd, 0.5f) + 0.1f
            && ActionHelper.GetActionCharges(adjusted) < 1)
        {
            WaitOrSkip(now, "技能独立冷却未就绪，已跳过");
            return;
        }
        if (status != 0)
        {
            WaitOrSkip(now, $"技能不可用（{status}），已跳过");
            return;
        }
        blockedSince = default;
        if (++attempts > 3)
        {
            Skip(now, "未收到技能效果确认，已跳过");
            return;
        }
        pendingId = requested;
        pendingAdjusted = adjusted;
        attemptedAt = now;
        // Synchronous PR dispatch: no global queue entries remain when this module stops.
        ActionUpdaterRouter.UseAction(new PAction(adjusted, type, ActionTargetType.Self) { NetworkTid = target.EntityId });
    }

    private static IBattleChara? ResolveTarget(Vector3 position)
    {
        if (Svc.Targets.Target is IBattleChara { IsDead: false, IsTargetable: true } current && IsEnemy(current)) return current;
        var nearest = Svc.Objects.OfType<IBattleChara>().Where(o => !o.IsDead && o.IsTargetable && IsEnemy(o))
            .OrderBy(o => Vector3.DistanceSquared(position, o.Position)).FirstOrDefault();
        if (nearest != null) Svc.Targets.Target = nearest;
        return nearest;
    }

    private static bool IsEnemy(IBattleChara target) => target.Address != 0
        && ActionManager.ClassifyTarget((Character*)target.Address) == ActionManager.TargetCategory.Enemy;

    private void OnEffect(LogSystemActionEffectEvent e)
    {
        if (disposed || pendingId == 0 || Svc.Objects.LocalPlayer is not { } player
            || e.SourceId != player.EntityId && e.SourceId != player.GameObjectId
            || e.ActionId != pendingId && e.ActionId != pendingAdjusted
            || e.GlobalSequence != 0 && e.GlobalSequence == lastSequence) return;
        lastSequence = e.GlobalSequence;
        pendingCast = false;
        attempts = 0;
        pendingId = pendingAdjusted = 0;
        if (chain != null && chainIndex + 1 < chain.Length)
            ++chainIndex;
        else
        {
            runner.Confirm();
            chain = null;
            chainIndex = 0;
        }
    }

    private void WaitOrSkip(DateTime now, string reason)
    {
        if (blockedSince == default) blockedSince = now;
        if (now >= blockedSince.AddMilliseconds(1200)) Skip(now, reason);
    }

    private void Skip(DateTime now, string reason)
    {
        Svc.Log.Info($"[PromeRadar 技能序列] {runner.Status}：{reason}");
        runner.Skip(now, reason);
        ClearAction();
    }

    private void ClearAction()
    {
        pendingId = pendingAdjusted = 0;
        pendingCast = false;
        chain = null;
        chainIndex = 0;
        attempts = 0;
        previousStep = -1;
        blockedSince = default;
    }

    private void ReleaseAcr()
    {
        if (!pausedAcr) return;
        pausedAcr = false;
        if (PromeSettings.Instance.EnableAcr == AcrState.Hold) PromeSettings.Instance.EnableAcr = AcrState.On;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Stop();
        effects.Dispose();
    }
}
