using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsGudanna => territory == HeavenswardIntroCatalog.GUDANNA;
    internal int GudannaCastIndex => palaceGuideLoaded?.GudannaCastIndex ?? 0;
    internal bool GudannaRotationDisabled => palaceGuideLoaded?.GudannaRotationDisabled ?? false;
    private string GudannaGuide => $"{palaceGuideLoaded?.Primary.Actor.Name} will cast Trounce (Cone AOE) from the East and West wall. \nMake sure to stay near him to dodge the AOE. \n{palaceGuideLoaded?.Primary.Actor.Name} will also cast Ecliptic Meteor at 10% HP, plan accordingly!";
    internal bool UsesGudannaNativeEffect(uint action) => IsGudanna && action is 7095 or 7096 or 7098 or 7099;

    private void GudannaEffect(uint action)
    {
        if (!IsGudanna || palaceGuideLoaded is not { Active: true } module || module.GudannaRotationDisabled) return;
        if (action == 7099) module.GudannaRotationDisabled = true;
        else if (action is 7095 or 7096 or 7098)
        {
            ++module.GudannaCastIndex;
            if (module.GudannaCastIndex == 8) module.GudannaCastIndex = 0;
        }
    }

    internal PalaceGudannaPositioning? GudannaPositioning
    {
        get
        {
            if (!IsGudanna || Active == null || GudannaRotationDisabled || Arena == null
                || !palaceGuideActors.TryGetValue(PrimaryId, out var primary)) return null;
            var next = GudannaCastIndex switch
            {
                0 or 2 or 5 => PalaceGudannaNextAction.Thunderbolt,
                1 or 4 or 6 => PalaceGudannaNextAction.Charybdis,
                3 => PalaceGudannaNextAction.TrounceEast,
                _ => PalaceGudannaNextAction.TrounceWest,
            };
            return new(primary, GudannaCastIndex, next, Arena.Position);
        }
    }

    private IEnumerable<DungeonAmbientAoe> GudannaAoes()
    {
        if (!IsGudanna || !palaceGuideEnabled) yield break;
        if (Active == null && palaceGuideLoaded is { Started: false } loaded)
        {
            var primary = loaded.Primary.Actor;
            var boundary = Definitions[0].Arena(primary.Position);
            yield return new("gudanna/precombat/arena", "场地", new LocalPolygonTemplate(
                boundary.GetContours()[0].Select(p => MasterPlayer.Spatial.Geometry.GeometryHelper.WorldToLocal(p, boundary.Position, 0))),
                boundary.Position, Navigate: false, OutlineOnly: true);
            if (!primary.Dead && primary.Targetable)
                yield return HeavenswardIntroEnemyMarker(primary, boundary, "gudanna/precombat/enemy", new(1, 0, 0, 1));
        }
        if (Active == null) yield break;
        // Source Maelstrom consumes every Tornado until destruction, even dead
        // or EventState=7. This is a persistent obstacle, not its 7167 cast.
        foreach (var tornado in _actors.Values.Where(a => a.Oid == 0x18F0))
            yield return new($"gudanna/tornado/{tornado.Id}", "Maelstrom", new CircleTemplate(10),
                tornado.Position, Obstacle: true, OverrideDangerMargin: false);
        // Source rotation preferences weigh .5, below the existing bridge's
        // 100 threshold. Keep GudannaPositioning intact without new navigation.
    }

    private IEnumerable<string> GudannaHints(Vector3? player)
    {
        if (!IsGudanna || Active == null) yield break;
        if (GudannaPositioning is { } positioning)
            yield return positioning.Index switch
            {
                0 => "Thunderbolt -> Charybdis -> Thunderbolt -> Trounce from East Wall",
                1 => "Charybdis -> Thunderbolt -> Trounce from East Wall",
                2 => "Thunderbolt -> Trounce from East Wall",
                3 => "Trounce from East Wall",
                4 => "Charybdis -> Thunderbolt -> Charybdis -> Trounce from West Wall.",
                5 => "Thunderbolt -> Charybdis -> Trounce from West Wall.",
                6 => "Charybdis -> Trounce from West Wall!",
                _ => "Trounce from West Wall!",
            };
        if (player is { } position && _actors.Values.Any(a => a.Oid == 0x18F0
            && NativeEnemyDistanceSquared(position, a.Position) <= 100))
            yield return "GTFO from voidzone!";
    }

}
