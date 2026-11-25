using MasterPlayer.Mechanics.DeepDungeon;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Scripting;

namespace MasterPlayer.Mechanics.EurekaOrthos;

public abstract class EurekaFloorScript : DeepDungeonFloorScript
{
    [UserSetting("使用位图遮挡处理视线攻击")]
    public bool AutoLOS { get => Settings.GetBool("AutoLOS", false); set => Settings.Set("AutoLOS", value); }
    internal override DeepDungeonFloorEncounter CreateEncounter() => new EurekaFloorEncounter();
    internal override PalaceFloorHazards CreateFloor() => new(3, 100, EurekaFloorData.Rooms, EurekaFloorData.Traps);
    internal override DungeonDefinition[] CreateBosses(uint territory) => EurekaOrthosCatalog.Create(territory);
    internal override uint[] SpecialActions => EurekaFloorEncounter.SpecialActions;
    internal override byte DungeonId => 3;
}
