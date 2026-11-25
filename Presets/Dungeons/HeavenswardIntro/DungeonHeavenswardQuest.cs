using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool questTerminusActive;
    private readonly HashSet<ulong> questTerminusIds = [];

    private void ObserveHeavenswardQuest()
    {
        if (territory != HeavenswardIntroCatalog.REGULA_VITH) return;
        var ids = _actors.Values.Where(a => a.Oid == 0xF5D).Select(a => a.Id).ToArray();
        // Actor death leaves the other swords active; object destruction closes the whole group.
        if (questTerminusIds.Except(ids).Any()) questTerminusActive = false;
        questTerminusIds.Clear();
        questTerminusIds.UnionWith(ids);
    }

    private IEnumerable<DungeonActor> HeavenswardQuestVoidzones => Active == null ? []
        : territory switch
        {
            HeavenswardIntroCatalog.ALDYNN => _actors.Values.Where(a => a.Oid == 0x1E9FA8 && a.EventState != 7),
            // This source has no EventState or death filter.
            HeavenswardIntroCatalog.REGULA_VITH => _actors.Values.Where(a => a.Oid == 0x1E88F5),
            _ => [],
        };

    private IEnumerable<DungeonAmbientAoe> HeavenswardQuestAoes()
    {
        foreach (var source in HeavenswardQuestVoidzones)
            yield return new($"hw-quest/obstacle/{source.Id}", "GTFO from voidzone!",
                new CircleTemplate(territory == HeavenswardIntroCatalog.ALDYNN ? 4 : 8), source.Position, Obstacle: true);
        if (territory != HeavenswardIntroCatalog.REGULA_VITH || Active == null || !questTerminusActive) yield break;
        foreach (var sword in _actors.Values.Where(a => a.Oid == 0xF5D && !a.Dead))
            yield return new($"hw-quest/terminus/{sword.Id}", "Terminus Est", RayRectangleTemplate.Symmetric(4, 40),
                QuantizedDungeonPosition(sword.Position), sword.Rotation);
    }
}
