using MasterPlayer.Mechanics.DeepDungeon;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Mechanics.PalaceOfTheDead;

namespace MasterPlayer.Mechanics.HeavenOnHigh;

public abstract class HeavenOnHighFloorScript : DeepDungeonFloorScript
{
    internal override DeepDungeonFloorEncounter CreateEncounter() => new HeavenOnHighFloorEncounter();
    internal override PalaceFloorHazards CreateFloor()
        => new(2, 100, HeavenOnHighFloorData.Rooms, HeavenOnHighFloorData.Traps);
    internal override DungeonDefinition[] CreateBosses(uint territory) => HeavenOnHighCatalog.Create(territory);
    internal override uint[] SpecialActions => HeavenOnHighFloorEncounter.SpecialActions;
    internal override byte DungeonId => 2;
}
