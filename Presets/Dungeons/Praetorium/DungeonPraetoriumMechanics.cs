using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool neroArenaChanged;
    private DateTime neroRingActivation;
    private DateTime gaiusEnrage;
    private readonly Dictionary<ulong, DungeonPendingCast> terminusEst = [];
    private readonly HashSet<ulong> finishedTerminus = [];

    internal double? GaiusEnrageSeconds => gaiusEnrage == default ? null : Math.Max(0, (gaiusEnrage - _now).TotalSeconds);

    private void SynchronizePraetorium()
    {
        if (Active?.BossOid == 0x3873)
        {
            if (neroArenaChanged)
            {
                if (neroRingActivation != default)
                {
                    Arena = PraetoriumArenas.Nero(_actors[PrimaryId].Position.Y);
                    neroRingActivation = default;
                }
            }
            else if (neroRingActivation == default)
                neroRingActivation = _now.AddSeconds(3.6);
        }
        if (Active?.BossOid != 0x3875) return;
        var boss = _actors[PrimaryId];
        if (boss.Targetable) gaiusEnrage = default;
        else if (gaiusEnrage == default) gaiusEnrage = _now.AddSeconds(92.4);
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x3878 && !a.Dead))
        {
            if (terminusEst.ContainsKey(actor.Id) || finishedTerminus.Contains(actor.Id) || !float.IsFinite(actor.Rotation)) continue;
            var rule = new DungeonCastRule(28487, 0x3878, "TerminusEst", RayRectangleTemplate.Symmetric(4, 40));
            terminusEst[actor.Id] = new(actor.Id, rule, actor.Position, actor.Rotation, _now, _now.AddSeconds(6));
        }
        foreach (var id in terminusEst.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) terminusEst.Remove(id);
        finishedTerminus.RemoveWhere(id => !_actors.ContainsKey(id));
    }

    internal void MapEffect(uint index, uint state, DateTime? timestamp = null)
    {
        ArrQuestMap(index, state);
        ThornMap(index, state, timestamp ?? _now);
        WolMap(index, state, timestamp ?? _now);
        AsuraMap(index, state);
        ZeleniaMap(index, state);
        EnuoMap(index, state);
        NecronMap(index, state);
        QueenMap(index, state);
        ZoraalMap(index, state);
        ValigMap(index, state, timestamp ?? _now);
        BabilMap(index, state);
        VanaspatiMap(index, state, timestamp ?? _now);
        KtisisMap(index, state, timestamp ?? _now);
        AitiascopeMap(index, state, timestamp ?? _now);
        DeadEndsMap(index, state);
        AlzadaalMap(index, state, timestamp ?? _now);
        TroiaMap(index, state);
        LapisMap(index, state, timestamp ?? _now);
        AetherfontMap(index, state, timestamp ?? _now);
        LunarMap(index, state, timestamp ?? _now);
        WorqorMap(index, state, timestamp ?? _now);
        SkydeepMap(index, state);
        VanguardMap(index, state, timestamp ?? _now);
        OrigenicsMap(index, state);
        AlexMap(index, state);
        TenderMap(index, state, timestamp ?? _now);
        StrayMap(index, state, timestamp ?? _now);
        YuweMap(index, state, timestamp ?? _now);
        MesoMap(index, state);
        MistMap(index, state);
        AeryMapEffect(index, state);
        CosmosMapEffect(index, state);
        AnamnesisMapEffect(index, state, timestamp ?? _now);
        HeroesMap(index, state);
        MatoyaMap(index, state, timestamp ?? _now);
        PaglthanMap(index, state, timestamp ?? _now);
        if (territory != PraetoriumCatalog.TERRITORY || index != 0 || state != 0x00020001) return;
        neroArenaChanged = true;
        if (Active?.BossOid == 0x3873 && _actors.TryGetValue(PrimaryId, out var boss))
        {
            Arena = PraetoriumArenas.Nero(boss.Position.Y);
            neroRingActivation = default;
        }
    }

    private void PraetoriumEffect(uint action)
    {
        if (Active?.BossOid != 0x3875 || action is not (28487 or 28488)) return;
        finishedTerminus.UnionWith(_actors.Values.Where(a => a.Oid == 0x3878).Select(a => a.Id));
        terminusEst.Clear();
    }

    private IEnumerable<DungeonAmbientAoe> PraetoriumAoes()
    {
        if (Active?.BossOid != 0x3873) yield break;
        if (!neroArenaChanged && neroRingActivation != default)
            yield return new("nero/ring", "场地缩小", new DonutTemplate(20, 35), new(-164, _actors[PrimaryId].Position.Y, 0),
                Activation: neroRingActivation);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 28476))
        {
            var center = new Vector3(-164, cast.Location.Y, 0);
            yield return new("nero/knockback-goal", "击退前站在场心8码内", new CircleTemplate(8), center, Safe: true);
            if (!_actors.TryGetValue(PlayerId, out var player) || KnockbackImmune(player, cast.FinishAt)) continue;
            var offset = new Vector2(player.Position.X - cast.Location.X, player.Position.Z - cast.Location.Z);
            if (offset.LengthSquared() <= .0001f) continue;
            var direction = Vector2.Normalize(offset);
            var landing = player.Position + new Vector3(direction.X, 0, direction.Y) * 12;
            var safe = Arena?.Contains(landing) == true;
            yield return new("nero/knockback-landing", "击退落点", new CircleTemplate(.5f), landing,
                Navigate: false, OutlineOnly: true, Safe: safe);
            yield return new("nero/knockback-path", "击退轨迹", new RectangleTemplate(.12f, 12),
                (player.Position + landing) * .5f, MathF.Atan2(direction.X, direction.Y), Navigate: false, Safe: safe);
        }
    }
}
