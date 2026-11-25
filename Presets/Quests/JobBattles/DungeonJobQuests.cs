using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsNativeQuestBoss => IsHeavenswardIntro || JobQuestCatalog.IsTerritory(territory) || IsMainScenarioQuest || IsNovice || IsNativeFate;
    private bool IsJobQuestGroup => territory == JobQuestCatalog.SOMEWHERE_ONLY_SHE_KNOWS && Active?.BossOid is 0x4293 or 0x4296;
    private bool JobQuestGroupAlive => IsJobQuestGroup && _actors.Values.Any(a => !a.Dead && JobQuestCatalog.InGroup(Active!.BossOid, a.Oid));

    private IEnumerable<DungeonAmbientAoe> JobQuestEnemies()
    {
        if (IsMaxQuest) yield break;
        if ((!JobQuestCatalog.IsTerritory(territory) && !IsMainScenarioQuest && !IsNovice) || Active == null || Arena == null) yield break;
        var shape = new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]);
        foreach (var actor in _actors.Values)
        {
            var included = territory switch
            {
                MainScenarioQuestCatalog.WHERE_EVERYTHING_BEGINS => actor.Oid is 0x39B5 or 0x39B6 or 0x39B4,
                MainScenarioQuestCatalog.FROSTY_RECEPTION => actor.IsEnemy == true,
                MainScenarioQuestCatalog.HOPE_ON_THE_WAVES => actor.IsEnemy == true,
                MainScenarioQuestCatalog.WARMTH_OF_THE_FAMILY => AlphaEnemy(actor.Oid),
                MainScenarioQuestCatalog.ISEULTALON => actor.Id == PrimaryId || actor.Oid == 0x2650,
                JobQuestCatalog.BAR_THE_PASSAGE when IsBarPassageP1 => actor.Oid is 0x4664 or 0x4666 or 0x46B0 or 0x4662 or 0x46AC or 0x473A or 0x4661 or 0x4665 or 0x46AF or 0x4663 or 0x46AD,
                JobQuestCatalog.BAR_THE_PASSAGE when IsBarPassageP3 => actor.Id == PrimaryId || actor.Oid is 0x4670 or 0x4672,
                JobQuestCatalog.BAR_THE_PASSAGE => actor.Oid is 0x4669 or 0x4676 or 0x4667 or 0x4668 or 0x4675,
                JobQuestCatalog.HEROES_AND_PRETENDERS => actor.Oid is 0x428A or 0x428C or 0x4208 or 0x428E or 0x448B or 0x4207,
                MainScenarioQuestCatalog.TIME_BETWEEN => actor.Id == PrimaryId && !timePrimaryRemoved
                    || TimeBetweenPhase == 1 && actor.Oid is 0x1EBC or 0x1A37,
                MainScenarioQuestCatalog.FADED_MEMORIES when Active.BossOid == 0x2F1D => actor.Oid is 0x2F1D or 0x2F1A or 0x2F1C or 0x2F1B,
                MainScenarioQuestCatalog.FADED_MEMORIES when Active.BossOid == 0x2F28 => actor.Oid is 0x2F2B or 0x2F2A or 0x2F2C,
                MainScenarioQuestCatalog.FADED_MEMORIES when Active.BossOid == 0x2F1E => actor.Oid is 0x2F1E or 0x2F1F or 0x2F20,
                MainScenarioQuestCatalog.DEATH_UNTO_DAWN => actor.Id == PrimaryId || actor.Oid == 0x3205,
                MainScenarioQuestCatalog.FEAST_OF_LIES => actor.Id == PrimaryId || actor.Oid == 0x29E8,
                MainScenarioQuestCatalog.OTIS_OATHBROKEN => actor.Oid is 0x4342 or 0x4344 or 0x4345 or 0x4346 or 0x4343 or 0x4347,
                MainScenarioQuestCatalog.HIS_FORGOTTEN_HOME => actor.Oid is 0x213A or 0x213B or 0x213C or 0x213D,
                JobQuestCatalog.BLOOD_ON_THE_DECK => actor.IsEnemy == true,
                JobQuestCatalog.POWER_TO_PROTECT => actor.IsEnemy == true,
                JobQuestCatalog.ART_FOR_THE_LIVING => actor.Id == PrimaryId || actor.Oid is 0x1CB6 or 0x1CD1 or 0x1CD6 or 0x1CD8,
                JobQuestCatalog.FANGS_OF_THE_VIPER => actor.Oid is 0x42A2 or 0x42A1 or 0x42A3 or 0x42A0,
                JobQuestCatalog.FACE_OF_TRUE_EVIL => actor.Id == PrimaryId || actor.Oid == 0x1BF1,
                JobQuestCatalog.SAGES_FOCUS => actor.Oid is 0x3587 or 0x3586 or 0x3588,
                JobQuestCatalog.ORPHANS_AND_BROKEN_BLADE => actor.IsEnemy == true,
                JobQuestCatalog.KILLING_ART => actor.Id == PrimaryId || actor.Oid is 0x3666 or 0x3667,
                JobQuestCatalog.LIFE_EPHEMERAL => Active.BossOid == 0x35C5
                    ? actor.Oid is 0x35C5 or 0x35C4 : actor.Id == PrimaryId || actor.Oid == 0x3859,
                _ => actor.Id == PrimaryId || IsJobQuestGroup && JobQuestCatalog.InGroup(Active.BossOid, actor.Oid),
            };
            if (!included || actor.Dead || !actor.Targetable) continue;
            var outside = !Arena.Contains(actor.Position);
            var position = actor.Position;
            var offset = position - Arena.Position;
            // Custom source bounds keep outside actors on the center axis unprojected.
            var preserveAxis = (Arena is WorldPolygonGeometry or PolygonRegionGeometry || IsScarmiglioneQuest || IsFrostyQuest || IsColibriQuest)
                && (MathF.Abs(offset.X) < .1f || MathF.Abs(offset.X) <= 1 && MathF.Abs(offset.Z) <= 1);
            if (outside && IsNovice)
            {
                offset.Y = 0;
                position = (Arena.Position + Vector3.Normalize(offset) * 20) with { Y = actor.Position.Y };
            }
            else if (outside && !preserveAxis)
            {
                var point = new Vector2(position.X, position.Z);
                var closest = Arena.GetContours().SelectMany(c => Enumerable.Range(0, c.Count).Select(i =>
                {
                    var a = new Vector2(c[i].X, c[i].Z);
                    var b = new Vector2(c[(i + 1) % c.Count].X, c[(i + 1) % c.Count].Z);
                    var edge = b - a;
                    var t = edge.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0, 1) : 0;
                    return a + edge * t;
                })).MinBy(p => Vector2.DistanceSquared(p, point));
                position = new(closest.X, position.Y, closest.Y);
            }
            yield return new($"job-quest/enemy/{actor.Id}", "敌人及朝向", shape, position, actor.Rotation,
                Navigate: false, OutlineOnly: outside, Unclipped: true,
                Color: IsIseultalon && actor.Oid == 0x2650 ? new Vector4(1, 0, 1, 1) : new Vector4(1, 0, 0, 1));
        }
    }
}
