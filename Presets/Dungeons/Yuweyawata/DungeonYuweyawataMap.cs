using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsYuweTrash => territory == YuweyawataCatalog.TERRITORY && Active != null && YuweyawataCatalog.IsTrash(Active.BossOid);
    private static bool YuweMember(DungeonDefinition definition, DungeonActor actor)
        => actor.Oid == definition.BossOid || definition.PrimaryAliases?.Contains(actor.Oid) == true;

    private DungeonDefinition? YuweDefinition(DungeonActor actor)
        => Definitions.Where(d => YuweMember(d, actor) && (!YuweyawataCatalog.BoundedPull(d.BossOid)
            || AetherfontInBounds(d.Arena(actor.Position), actor.Position, AetherfontRadius(d.Arena(actor.Position)))))
            .OrderBy(d => Vector3.DistanceSquared(d.Arena(actor.Position).Position, actor.Position)).FirstOrDefault();

    private void YuweSelectPrimary()
    {
        if (!IsYuweTrash || Active == null || Arena == null) return;
        var radius = AetherfontRadius(Arena);
        // CheckPull and phase completion deliberately use opposite bounds policies.
        var alive = _actors.Values.Where(a => !a.Dead && YuweMember(Active, a)
            && (YuweyawataCatalog.BoundedPull(Active.BossOid) || AetherfontInBounds(Arena, a.Position, radius))).ToArray();
        if (alive.Length == 0) { ResetFight(); return; }
        if (!alive.Any(a => a.Id == PrimaryId)) PrimaryId = alive[0].Id;
    }

    private void YuweMap(uint index, uint state, DateTime now)
    {
        if (territory != YuweyawataCatalog.TERRITORY || Arena == null) return;
        var y = Arena.Position.Y;
        if (Active?.BossOid == 0x4641 && state is 0x00020001 or 0x00200010)
        {
            if (state == 0x00200010)
                index = index switch { 0x0D => 0x10, 0x0E => 0x0F, 0x0F => 0x0E, 0x10 => 0x0D, _ => index };
            var p = index switch
            {
                0x0D => new Vector3(81.132f, y, 268.868f), 0x0E => new(81.132f, y, 285.132f),
                0x0F => new(64.868f, y, 268.868f), 0x10 => new(64.868f, y, 285.132f), _ => new(0, y, 0),
            };
            yuweCell = new("yuwe/cell", "电胞爆炸", new CircleTemplate(26), QuantizedDungeonPosition(p), Activation: now.AddSeconds(8));
        }
        if (Active?.BossOid == 0x464A && index == 7)
        {
            yuwePrepare = false;
            var arena = state switch
            {
                0x00020001 => YuweyawataArenas.KanilokkaDefault(y), 0x00200010 => YuweyawataArenas.KanilokkaTiny(y),
                0x00800040 => YuweyawataArenas.KanilokkaPathsA(y), 0x02000100 => YuweyawataArenas.KanilokkaPathsB(y),
                0x00080004 => YuweyawataArenas.KanilokkaStarting(y), _ => null,
            };
            if (arena != null) { Arena = arena; yuweShrink = null; }
        }
        if (Active?.BossOid == 0x464C)
        {
            IGeometryShape? arena = null;
            if (index == 0x19 && state == 0x00020001) arena = YuweyawataArenas.LunipyatiDefault(y);
            if (index == 0x11 && state == 0x00800040) arena = YuweyawataArenas.LunipyatiDonut(y);
            if (arena != null) { Arena = arena; yuweShrink = null; }
        }
    }
}
