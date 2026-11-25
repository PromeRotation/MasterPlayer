using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool tisiphoneEnabled;
    private bool TisiphoneRanged => _actors.GetValueOrDefault(PlayerId)?.ClassJob
        is 5 or 6 or 7 or 23 or 24 or 25 or 26 or 27 or 28 or 31 or 33 or 35 or 36 or 38 or 40 or 42;

    internal string? TisiphonePrecombatHint
    {
        get
        {
            if (territory != HeavenswardIntroCatalog.TISIPHONE || !tisiphoneEnabled || Active != null) return null;
            var boss = _actors.Values.FirstOrDefault(a => a.Oid == 0x181C && !a.Dead && a.Hp != 0);
            return boss == null ? null : $"{boss.Name} will spawn 4 zombies, you can either kite them or kill them. "
                + "The BloodRain raidwide will also kill them if they're still alive. "
                + "The boss will also life-steal however much HP is left of the Succubus, you're choice if you want to kill it or not.";
        }
    }

    private IEnumerable<DungeonAmbientAoe> TisiphoneZombieAreas()
    {
        if (territory != HeavenswardIntroCatalog.TISIPHONE || Active == null || !TisiphoneRanged) yield break;
        // Source enumerates all zombies until destruction, including dead/untargetable ones.
        foreach (var zombie in _actors.Values.Where(a => a.Oid == 0x18ED))
        {
            yield return new($"tisiphone/zombie/{zombie.Id}/near", "Zombie proximity", new CircleTemplate(3),
                zombie.Position, Display: false);
            yield return new($"tisiphone/zombie/{zombie.Id}/future", "Zombie future proximity", new CircleTemplate(8),
                zombie.Position, Display: false, Activation: _now.AddSeconds(5));
        }
    }
}
