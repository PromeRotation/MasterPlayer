using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    // Hole closure is scene state, retained between pulls; replay seek creates a fresh encounter.
    private bool researchFirstHoleFilled;
    private bool researchSecondHoleFilled;
    private bool researchTrashFirstRoom;

    private bool IsResearchTrash => territory == ResearchFacilityCatalog.TERRITORY
        && Active != null && ResearchTrashCatalog.IsPrimary(Active.BossOid);

    private bool ResearchCanStart(DungeonActor actor, Vector3 player)
    {
        if (territory != ResearchFacilityCatalog.TERRITORY) return actor.InCombat;
        if (!ResearchTrashCatalog.IsPrimary(actor.Oid)) return actor.InCombat;
        return actor.Oid switch
        {
            0xFA5 or 0xFA3 => _actors.Values.Any(a => !a.Dead && a.InCombat && ResearchTrashCatalog.FIRST.Contains(a.Oid)),
            0x1350 => _actors.Values.Any(a => !a.Dead && a.InCombat && ResearchTrashCatalog.VANGUARD.Contains(a.Oid)),
            0xF4D => ResearchBiocultureArena(actor.Position.Y, actor.Position.Z < 250).Contains(player),
            _ => actor.InCombat,
        };
    }

    private IGeometryShape ResearchBiocultureArena(float y, bool first)
    {
        if (first)
            return researchFirstHoleFilled ? ResearchTrashArenas.BiocultureFirstFilled(y) : ResearchTrashArenas.BiocultureFirst(y);
        return researchSecondHoleFilled ? ResearchTrashArenas.BiocultureSecondFilled(y) : ResearchTrashArenas.BiocultureSecond(y);
    }

    private bool ResearchTrashMember(DungeonActor actor)
    {
        if (!IsResearchTrash || actor.Dead) return false;
        var oid = Active!.BossOid;
        return oid switch
        {
            0xFA5 or 0xFA3 => ResearchTrashCatalog.FIRST.Contains(actor.Oid),
            0x1350 => ResearchTrashCatalog.VANGUARD.Contains(actor.Oid),
            0xF54 => ResearchTrashCatalog.DREADNAUGHT.Contains(actor.Oid),
            0xF58 or 0xF56 when !researchTrashFirstRoom => ResearchTrashCatalog.SHABTI_CONJURER.Contains(actor.Oid) && actor.Position.Z <= 200,
            0xF58 => actor.Oid == 0xF58 && actor.Position.Z > 200,
            0xF56 => ResearchTrashCatalog.CONJURER.Contains(actor.Oid) && actor.Position.Z > 200,
            0xF4D => (researchTrashFirstRoom ? ResearchTrashCatalog.BIO_FIRST : ResearchTrashCatalog.BIO_SECOND).Contains(actor.Oid)
                && (actor.Position.Z < 250) == researchTrashFirstRoom,
            _ => false,
        };
    }

    private void ResearchTrashStarted(DungeonActor primary)
    {
        if (!IsResearchTrash) return;
        researchTrashFirstRoom = Active!.BossOid == 0xF4D ? primary.Position.Z < 250 : primary.Position.Z > 200;
        if (Active.BossOid == 0xF4D) Arena = ResearchBiocultureArena(primary.Position.Y, researchTrashFirstRoom);
    }

    private void ResearchTrashAnimation(ulong source, uint state)
    {
        if (territory != ResearchFacilityCatalog.TERRITORY || state != 0x00040008 || !_actors.TryGetValue(source, out var actor)) return;
        if (actor.Oid == 0x1E9934) researchFirstHoleFilled = true;
        else if (actor.Oid == 0x1E9935) researchSecondHoleFilled = true;
        else return;
        if (Active?.BossOid == 0xF4D && researchTrashFirstRoom == (actor.Oid == 0x1E9934))
            Arena = ResearchBiocultureArena(Arena!.Position.Y, researchTrashFirstRoom);
    }

    private IEnumerable<DungeonAmbientAoe> ResearchTrashAoes()
    {
        if (Active?.BossOid is not (0xFA5 or 0xFA3)) yield break;
        foreach (var source in _actors.Values.Where(a => !a.Dead && a.Targetable && a.Oid is 0xFA5 or 0xF5A or 0xF59))
        {
            if (!_actors.TryGetValue(source.TargetId, out var target)) continue;
            var infrared = source.Oid == 0xFA5;
            if (!infrared && _actors.TryGetValue(PlayerId, out var viewer)
                && (viewer.ClassJob is 1 or 2 or 3 or 4 or 19 or 20 or 21 or 22 or 29 or 30 or 32 or 34 or 37 or 39 or 41
                    || viewer.PartyRole is "Melee" or "Tank")) continue;
            var shape = new CircleTemplate(infrared ? 6 : 5.225f);
            var origin = infrared ? target.Position : source.Position;
            yield return new($"research/cleave/{source.Id}/display", infrared ? "被动红外制导" : "回旋", shape,
                origin, Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new($"research/cleave/{source.Id}/avoid", "避开溅射", shape, origin, Display: false);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"research/cleave/{source.Id}/party/{member.Id}", "溅射远离队友", shape, member.Position, Display: false);
        }
    }
}
