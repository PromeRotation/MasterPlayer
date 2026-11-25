using ECommons.DalamudServices;
using MasterPlayer.Mechanics.DeepDungeon;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

public abstract class PilgrimsFloorScript : DeepDungeonFloorScript
{
    internal override DeepDungeonFloorEncounter CreateEncounter() => new PilgrimsFloorEncounter();
    // Upstream has no PT wall/trap coordinate tables. Null would incorrectly
    // select the Palace defaults, so both tables must be explicitly empty.
    internal override PalaceFloorHazards CreateFloor() => new(4, 100,
        new Dictionary<(int, byte), PalaceFloorRoom[]>(), new Dictionary<ushort, System.Numerics.Vector2[]>());
    internal override DungeonDefinition[] CreateBosses(uint territory) => [];
    internal override uint[] SpecialActions => PilgrimsFloorEncounter.SpecialActions;
    internal override byte DungeonId => 4;
    protected override string? InputDiagnostic(IEnumerable<ReplayActorState> actors)
        => actors.Any(a => a.Kind == 2 && a.SubKind is 5 or 11 && a.IsEnemy == true
            && a.Cast is { Rotation: null } c && c.ActionId is 44690 or 44691 or 42214 or 42216 or 44759)
            ? "读条缺少锁定朝向，未发布定向首击范围" : null;

    protected override bool BossCast(ReplayActorState actor, uint action)
        => PtBossCatalog.Owns(actor.Oid, ReplayWorld?.Territory ?? Svc.ClientState.TerritoryType);

    private protected override PalaceCast? ResolveCast(ReplayActorState actor)
    {
        var cast = base.ResolveCast(actor);
        if (cast == null || actor.Kind != 2 || actor.SubKind is not (5 or 11) || actor.IsEnemy != true) return cast;
        if (cast.Action is not (44690 or 44691 or 42214 or 42216 or 42516 or 42578 or 42748 or 43132 or 44759))
            return cast;
        if (!Dodge.AoeShapeDatabase.TryGetShape(cast.Action, actor.HitboxRadius, out var descriptor) || descriptor == null)
            return cast;
        // Exact first-hit consumers require the recorded cast facing. The
        // current Actor heading cannot reconstruct a missing locked direction.
        if (descriptor.UseCastRotation && cast.Rotation == null)
        {
            return cast with { Shape = null, Charge = false };
        }
        return cast with { Shape = Dodge.AoeShapeDatabase.CreateTemplate(descriptor), Charge = false };
    }
}
