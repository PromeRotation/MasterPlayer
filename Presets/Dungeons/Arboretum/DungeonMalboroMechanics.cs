using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool malboroOpen;
    private bool malboroInSecond;
    private bool malboroComplete;
    private bool malboroStarted;
    private readonly List<DungeonPendingCast> malboroCasts = [];
    private readonly List<DungeonPendingCast> malboroFinished = [];
    private bool IsMalboro => territory == ArboretumCatalog.TERRITORY && Active?.BossOid == 0x141F;
    internal bool PreserveStageAcrossBattleEnd => IsCaduceus && CaduceusAlive || IsCoilGauntlet && Active != null || IsMalboro || IsThornmarch && _actors.ContainsKey(PrimaryId)
        || IsLabyrinthBone && _actors.ContainsKey(PrimaryId)
        || IsNativeTrial && _actors.GetValueOrDefault(PrimaryId) is { Dead: false } || InnocencePhaseAlive || HadesPhaseAlive;

    public void LegacyMapEffect(byte sequence, byte param, byte[]? data)
    {
        // 死亡会撤销个人输出，但不能漏掉队友推进产生的阶段完成事件。
        if (!malboroStarted || sequence != 1 || param != 0 || data is not [1, ..]) return;
        malboroComplete = true;
        if (IsMalboro) ResetFight();
    }

    private void MalboroStart(ulong source, DungeonCastObservation cast, bool fromPacket)
    {
        if (!_actors.TryGetValue(source, out var actor) || actor.Dead
            || malboroFinished.Any(c => MalboroSameCast(c, source, cast))) return;
        var rule = Active!.Casts.FirstOrDefault(r => r.Action == cast.Action);
        if (rule == null || !float.IsFinite(cast.Location.X) || !float.IsFinite(cast.Location.Y) || !float.IsFinite(cast.Location.Z)) return;
        if (rule.Shape is FanTemplate && (cast.Rotation is not { } r || !float.IsFinite(r)))
        {
            warning?.Invoke($"{rule.Name}: 缺少锁定施法朝向，跳过预测。");
            return;
        }
        var index = malboroCasts.FindIndex(c => MalboroSameCast(c, source, cast));
        var pendingCast = new DungeonPendingCast(source, rule, cast.Location, cast.Rotation ?? 0,
            cast.StartedAt, cast.FinishAt, cast.TargetId) { UntilObservedEnd = true };
        if (index < 0)
        {
            foreach (var previous in malboroCasts.Where(c => c.Source == source).ToArray())
                MalboroFinish(source, previous.Rule.Action);
            malboroCasts.Add(pendingCast);
        }
        else if (fromPacket)
            malboroCasts[index] = pendingCast with
            {
                StartedAt = malboroCasts[index].StartedAt, FinishAt = malboroCasts[index].FinishAt,
            };
    }

    private void MalboroFinish(ulong source, uint action)
    {
        var index = malboroCasts.FindIndex(c => c.Source == source && (action == 0 || c.Rule.Action == action));
        if (index < 0) return;
        var cast = malboroCasts[index];
        malboroFinished.Add(cast);
        malboroCasts.RemoveAt(index);
    }

    private void MalboroObserve(DungeonActor actor)
    {
        if (actor.Cast is { } cast)
        {
            foreach (var previous in malboroCasts.Where(c => c.Source == actor.Id && !MalboroSameCast(c, actor.Id, cast)).ToArray())
                MalboroFinish(actor.Id, previous.Rule.Action);
            MalboroStart(actor.Id, cast, false);
        }
        else if (malboroCasts.Any(c => c.Source == actor.Id && _now >= c.StartedAt.AddSeconds(.2)))
            MalboroFinish(actor.Id, 0);
    }

    private static bool MalboroSameCast(DungeonPendingCast pending, ulong source, DungeonCastObservation cast)
        => pending.Source == source && pending.Rule.Action == cast.Action
            && Math.Abs((pending.StartedAt - cast.StartedAt).TotalSeconds) < .25
            && Math.Abs((pending.FinishAt - cast.FinishAt).TotalSeconds) < .75;
}
