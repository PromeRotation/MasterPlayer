using MasterPlayer.Mechanics.DeepDungeon;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.HeavenOnHigh;

internal sealed class HeavenOnHighFloorEncounter : DeepDungeonFloorEncounter
{
    internal static uint[] SpecialActions => [6351, 12174, 12322, 12357, 12261, 12416, 12442, 12415, 12432];
    internal static uint[] Territories => [770, 771, 772, 782, 773, 783, 774, 784, 775, 785];
    internal static bool OwnsTerritory(uint territory) => Territories.Contains(territory);

    protected override void AddZone(PalaceTrashCaster source, DateTime now)
    {
        var radius = source.Action switch
        {
            6351 => 100, 12174 => 25, 12322 or 12357 => 35,
            12261 or 12416 => 40, 12442 => 30, _ => 0,
        };
        if (radius != 0) gazes.Add(new(source, source.Action, new CircleTemplate(radius)));
        if (source.Action is 12415 or 12432)
            donuts.Add((source, source.Action == 12415 ? 9.5f : 8, source.Action == 12415 ? 41 : 30));
        if (source.Action is 12313 or 12258 or 12227) interrupts.Add(source);
        if (source.Action == 12350) stuns.Add(source);
        if (source.Action is 12174 or 12415 or 12432) hintDisabled.Add(source);
    }
}
