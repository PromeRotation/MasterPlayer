using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsMainScenarioQuest => MainScenarioQuestCatalog.IsTerritory(territory);
    private bool IsFadedThordan => territory == MainScenarioQuestCatalog.FADED_MEMORIES && Active?.BossOid == 0x2F1D;
    private bool FadedZenosPull => _actors.Values.Any(a => a.Oid is 0x2F2B or 0x2F2A or 0x2F2C && a.InCombat);
    private bool IsFeast => territory == MainScenarioQuestCatalog.FEAST_OF_LIES;
    private bool FeastTwelve => IsFeast && _casts.Values.Count(c => c.Rule.Action == 16286) == 12;
    private readonly List<ulong> mainScenarioCastOrder = [];
    private void MainScenarioCastStarted(DungeonPendingCast cast)
    {
        if (IsMainScenarioQuest && cast.Rule.Action is 24054 or 24046 or 18035 or 18036 or 16290 or 16292 or 30021 or 34888 or 38200
            && !mainScenarioCastOrder.Contains(cast.Source))
            mainScenarioCastOrder.Add(cast.Source);
    }

    private bool MainScenarioCastVisible(DungeonPendingCast cast)
    {
        if (!IsMainScenarioQuest || cast.Rule.Action is not (24054 or 24046 or 18035 or 16290 or 30021 or 34888 or 38200)) return true;
        var max = cast.Rule.Action switch { 24054 => 3, 24046 => 2, 16290 or 30021 => 5, 38200 => 6, _ => 8 };
        return mainScenarioCastOrder.Where(id => _casts.GetValueOrDefault(id)?.Rule.Action == cast.Rule.Action)
            .Take(max).Contains(cast.Source);
    }

    private IEnumerable<DungeonAmbientAoe> MainScenarioKnockbacks()
    {
        if ((territory != MainScenarioQuestCatalog.ORACLE_OF_LIGHT && !IsFeast) || Active == null
            || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var from = player.Position;
        foreach (var id in mainScenarioCastOrder)
        {
            if (_casts.GetValueOrDefault(id) is not { } cast || cast.Rule.Action is not (18036 or 16292)
                || WhorlImmune(player, cast.FinishAt)) continue;
            var direction = new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation));
            if (IsFeast)
            {
                direction = from - cast.Location;
                direction.Y = 0;
                if (direction.LengthSquared() == 0) continue;
                direction = Vector3.Normalize(direction);
            }
            var distance = MathF.Min(20, QuestWindRay(from, direction) - MathF.Max(.001f, player.HitboxRadius - .499f));
            var to = from + direction * distance;
            var prefix = IsFeast ? $"feast/kb/{id}" : $"oracle/kb/{id}";
            foreach (var aoe in QuestKnockbackProjection(prefix, player, from, to)) yield return aoe;
            from = to;
        }
    }

    private ulong MainScenarioPriorityTarget(ulong current, Vector3 player)
    {
        var candidates = _actors.Values.Where(a => Active != null && a.IsEnemy == true && a.Targetable && !a.Dead
            && a.ObjectIndex is { } index && index < 200 && (index & 1) == 0).ToArray();
        var electTarget = FeastTwelve ? candidates.Where(a => a.Oid == 0x295B)
            .MinBy(a => MathF.Sqrt(NativeEnemyDistanceSquared(a.Position, player)) - a.HitboxRadius)?.Id ?? 0 : 0;
        int Priority(DungeonActor actor)
        {
            int priority;
            if ((IsSlickshell || IsMaxQuest) && actor.FateId is > 0 && actor.FateId != AllowedFateId
                || MathF.Abs(actor.Position.Y - player.Y) > 12)
                priority = -2;
            else if (actor.NameId != 541 && actor.Hp is { } hp
                && (long)hp + (actor.IncomingEffects?.Sum(e => (long)e.HpDelta) ?? 0) <= 1)
                priority = -1;
            else if (actor.AggroPlayer || actor.InCombat && _actors.GetValueOrDefault(actor.TargetId)?.PartySlot >= 0)
                priority = 0;
            else
                priority = -3;
            if (IsFadedThordan && priority != -1)
                return HasStatus(actor, 671) ? -2 : 1;
            if (territory == MainScenarioQuestCatalog.DEATH_UNTO_DAWN && actor.Oid == 0x3205 && priority != -1)
                return 5;
            if (IsFeast && priority != -1)
            {
                if (FeastTwelve) priority = actor.Id == electTarget ? 1 : 0;
                if (actor.Oid == 0x29E8) priority = 1;
            }
            if (IsSleep && actor.Oid == 0x2D3C) return 0;
            if (IsSlickshell && priority != -1)
                return _actors.GetValueOrDefault(actor.TargetId)?.Oid == 0x2138 ? 1 : 0;
            if (IsHopeOnTheWaves && priority != -1)
                return HopeLimitBreakCenter != null && actor.Oid == 0x2114 || actor.Cast?.Action == 10928 ? 5 : 0;
            if (IsCarnivale && Active?.BossOid is 0x2708 or 0x270B && priority != -1)
                return actor.Oid == Active.BossOid ? 1 : 0;
            if (IsCarnivale && Active?.BossOid == 0x25CD)
                priority = actor.Oid == 0x25CD ? 1 : 0;
            if (IsCarnivale && Active?.BossOid == 0x26FF)
                priority = actor.Oid is 0x26FF or 0x25CE ? 1 : 0;
            if (IsCarnivale && Active?.BossOid == 0x242D)
                priority = actor.Oid == 0x242D ? 0 : 1;
            if (IsCarnivale && Active?.BossOid == 0x25D5 && priority != -1)
                return actor.Oid == 0x25D6 ? 1 : 0;
            if (IsCarnivale && Active?.BossOid == 0x272C)
                return actor.Oid switch { 0x272E => 2, 0x272D => 1, _ => 0 };
            if (IsCarnivale && Active?.BossOid == 0x2CD2 && priority != -1)
                return actor.Oid == 0x2CD2 ? 0 : 1;
            if (IsCarnivale && Active?.BossOid == 0x2CF8 && priority != -1)
                return actor.Oid switch { 0x2CEC => 1, 0x2CF8 => 0, _ => -1 };
            if (IsCarnivale && Active?.BossOid == 0x267F && priority != -1)
                return actor.Oid == 0x2682 ? 1 : 0;
            if (IsCarnivale && Active?.BossOid == 0x2736)
                return actor.Oid is 0x2737 or 0x2738 ? 1 : 0;
            if (IsCarnivale && Active?.BossOid == 0x2739)
                return actor.Oid == 0x273A ? 1 : 0;
            if (IsCarnivale && Active?.BossOid is 0x2724 or 0x2725 && priority != -1)
                return actor.Oid == Active.BossOid ? 1 : 0;
            if (IsCarnivale && Active?.BossOid == 0x2721 && priority != -1)
                return actor.Oid is 0x2722 or 0x2723 ? 1 : 0;
            if (IsCarnivale && Active?.BossOid == 0x26F4 && priority != -1)
                return actor.Oid == 0x26F3 ? 1 : 0;
            if (IsCarnivale && Active?.BossOid == 0x26F9 && priority != -1)
                return actor.Oid switch { 0x26FA => 2, 0x26FB => 1, _ => 0 };
            if (IsCarnivale && Active?.BossOid == 0x271B && priority != -1)
                return actor.Oid == 0x271C ? 1 : 0;
            return priority;
        }
        return candidates.Where(a => Priority(a) >= 0)
            .OrderByDescending(Priority).ThenByDescending(a => a.Id == current)
            .ThenBy(a => NativeEnemyDistanceSquared(a.Position, player)).Select(a => a.Id).FirstOrDefault();
    }

    private IEnumerable<DungeonAmbientAoe> FeastElectrocutions()
    {
        if (!IsFeast || Active == null) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 16286))
            yield return new($"feast/electrocution/{cast.Source}", "Electrocution", cast.Rule.Shape!, cast.Location,
                Navigate: !FeastTwelve, Activation: cast.FinishAt);
    }

    private IEnumerable<string> MainScenarioHints(Vector3? player)
    {
        if (IsFeast && player is { } p && FeastElectrocutions().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(p)))
            yield return "GTFO from aoe!";
    }

    private IEnumerable<DungeonDamagePrediction> MainScenarioDamage()
    {
        if (territory != MainScenarioQuestCatalog.MEASURE_OF_HIS_REACH || Active == null) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 8398))
        foreach (var actor in _actors.Values.Where(a => !a.Dead && (a.PartySlot is >= 0 and < 8 or >= 24)))
            yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, cast.FinishAt, "Raidwide");
    }
}
