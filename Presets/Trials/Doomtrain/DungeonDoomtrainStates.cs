using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsDoomtrain => territory == DoomtrainCatalog.TERRITORY;
    private readonly HashSet<DoomtrainComponent> doomComponents = [];
    private int doomStage = -1;
    private DateTime doomStageAt;
    private int doomCar = 1;
    private int doomTopology = 1;
    private bool doomPhase2;
    private bool doomWiping;
    private int doomUnlimitedHits;
    private int doomRunawayHits;

    private bool DoomEnabled(DoomtrainComponent component) => doomComponents.Contains(component);

    private void DoomDirector(uint command)
    {
        if (!IsDoomtrain) return;
        if (command == 0x40000005)
        {
            doomWiping = true;
            ResetFight();
        }
        if (command == 0x40000011) doomWiping = false;
    }

    private void DoomTick()
    {
        if (!IsDoomtrain || Active == null) return;
        if (doomStage < 0) DoomEnter(0);
        // Phase completion is evaluated before the state's 70000-second timeout.
        if (!doomPhase2 && _actors.Values.FirstOrDefault(a => a.Oid == 0x4A34)?.Dead == true)
        {
            doomPhase2 = true;
            DoomClearComponents();
            DoomEnter(19);
        }
        if (doomPhase2 && (_actors.GetValueOrDefault(PrimaryId) is not { Dead: false } boss || boss.Hp == 0))
        {
            ResetFight();
            return;
        }
        for (var guard = 0; guard < 24; ++guard)
        {
            var primary = _actors.GetValueOrDefault(PrimaryId);
            var castPresent = _casts.ContainsKey(PrimaryId) || primary?.Cast != null;
            var done = doomStage switch
            {
                0 or 4 or 8 or 12 or 16 => castPresent,
                1 or 5 or 9 => doomUnlimitedHits > 0 || _now >= doomStageAt.AddSeconds(6.9),
                13 or 17 => doomRunawayHits > 0 || _now >= doomStageAt.AddSeconds(6.9),
                2 or 6 or 10 or 14 or 22 => primary?.Targetable == false,
                3 or 7 or 11 or 19 or 21 => true,
                15 => _now >= doomStageAt.AddSeconds(70000),
                20 => doomRunawayHits > 0 || _now >= doomStageAt.AddSeconds(16.2),
                // These states have no successor; their condition never exits them.
                _ => false,
            };
            if (!done) break;
            if (doomStage is 1 or 5 or 9) doomComponents.Remove(DoomtrainComponent.Unlimited);
            if (doomStage is 13 or 17 or 20) doomComponents.Remove(DoomtrainComponent.Runaway);
            DoomEnter(doomStage + 1);
        }
        doomTankBaits.RemoveAll(b => b.Target.Actor.Dead);
        if (DoomEnabled(DoomtrainComponent.Arcane) && _actors.Values.Where(a => a.Oid == 0x4A36).All(a => a.Dead))
            doomArcane.Clear();
    }

    private void DoomEnter(int stage)
    {
        doomStage = stage;
        doomStageAt = _now;
        switch (stage)
        {
            case 0:
                DoomActivate(DoomtrainComponent.TankBait, DoomtrainComponent.Levin, DoomtrainComponent.Lightning,
                    DoomtrainComponent.Windpipe, DoomtrainComponent.Blastpipe, DoomtrainComponent.Unlimited);
                break;
            case 3:
            case 7:
                DoomSetCar(doomCar + 1);
                DoomActivate(DoomtrainComponent.Short, DoomtrainComponent.Medium, DoomtrainComponent.Long, DoomtrainComponent.Levin);
                DoomActivate(stage == 3 ? DoomtrainComponent.Windpipe : DoomtrainComponent.HeadOn);
                break;
            case 4:
            case 8:
                DoomActivate(DoomtrainComponent.Unlimited);
                break;
            case 11:
                DoomSetCar(doomCar + 1);
                DoomActivate(DoomtrainComponent.HeadOn);
                break;
            case 12:
            case 16:
            case 19:
                DoomActivate(DoomtrainComponent.Runaway);
                break;
            case 15:
                Arena = DoomtrainArenas.Intermission(Arena?.Position.Y ?? 0);
                DoomActivate(DoomtrainComponent.Surge, DoomtrainComponent.Ray, DoomtrainComponent.Shockwave,
                    DoomtrainComponent.RunawayRaidwide, DoomtrainComponent.Runaway);
                break;
            case 21:
                DoomSetCar(4);
                DoomActivate(DoomtrainComponent.ArenaChange, DoomtrainComponent.Hail, DoomtrainComponent.Arcane,
                    DoomtrainComponent.Stack, DoomtrainComponent.Derail);
                break;
            case 23:
                DoomActivate(DoomtrainComponent.HeadOn, DoomtrainComponent.Long, DoomtrainComponent.Medium,
                    DoomtrainComponent.Short, DoomtrainComponent.Upper, DoomtrainComponent.Levin,
                    DoomtrainComponent.Lightning, DoomtrainComponent.Electray3, DoomtrainComponent.Windpipe,
                    DoomtrainComponent.Blastpipe, DoomtrainComponent.TankBait);
                break;
        }
    }

    private void DoomActivate(params DoomtrainComponent[] components)
    {
        foreach (var component in components)
        {
            doomComponents.Add(component);
            // Re-activation replaces the source component, then replays the
            // current actors/casts/statuses. It is intentionally not idempotent.
            switch (component)
            {
                case DoomtrainComponent.Unlimited: doomUnlimitedHits = 0; break;
                case DoomtrainComponent.Runaway: doomRunawayHits = 0; break;
                case DoomtrainComponent.Levin:
                    doomLevin.Clear();
                    foreach (var actor in _actors.Values) DoomAddLevin(actor);
                    break;
                case DoomtrainComponent.Arcane:
                    doomArcane.Clear();
                    foreach (var actor in _actors.Values.Where(a => a.Oid == 0x4A36))
                        doomArcane.Add(DoomActor(actor));
                    break;
                case DoomtrainComponent.HeadOn: doomHeadOn.Clear(); doomHeadKeys.Clear(); break;
                case DoomtrainComponent.Upper: doomUpper.Clear(); doomUpperKeys.Clear(); break;
                case DoomtrainComponent.Derail: doomDerail.Clear(); doomDerailKeys.Clear(); break;
                case DoomtrainComponent.TankBait: doomTankBaits.Clear(); break;
                case DoomtrainComponent.Ray: doomRays.Clear(); break;
                case DoomtrainComponent.Windpipe: doomWindpipe = null; break;
                case DoomtrainComponent.Stack: doomStacks.Clear(); doomStackKeys.Clear(); break;
            }
            foreach (var cast in _casts.Values.ToArray()) DoomRecordCast(cast, component);
        }
    }

    private void DoomSetCar(int car)
    {
        doomCar = car;
        var y = Arena?.Position.Y ?? 0;
        if (car is 3 or 5) doomTopology = car;
        if (car == 2) Arena = DoomtrainArenas.Car2(y);
        else Arena = new RectangleGeometry(new(100, y, 50 + car * 50), 0, 20, car == 4 ? 29.2f : 30);
    }

    private IGeometryShape DoomDeck(bool air)
    {
        var y = Arena?.Position.Y ?? 0;
        if (doomTopology == 5) return air ? DoomtrainArenas.Car5Air(y) : DoomtrainArenas.Car5Ground(y);
        // Normalize the retained Car3 topology to the current Car4 origin:
        // this removes AOEShapeCustom's call-history-dependent origin cache.
        if (doomTopology == 3 && doomCar == 4)
            return air ? DoomtrainArenas.Car4Air(y) : DoomtrainArenas.Car4Ground(y);
        if (doomTopology == 3) return air ? DoomtrainArenas.Car3Air(y) : DoomtrainArenas.Car3Ground(y);
        return new RectangleGeometry(Arena?.Position ?? new Vector3(100, y, 100), 0, 20, 30);
    }

    private void DoomClearComponents()
    {
        doomComponents.Clear(); doomLevin.Clear(); doomHeadOn.Clear(); doomHeadKeys.Clear();
        doomUpper.Clear(); doomUpperKeys.Clear(); doomDerail.Clear(); doomDerailKeys.Clear();
        doomTankBaits.Clear(); doomRays.Clear(); doomArcane.Clear(); doomWindpipe = null;
        doomStacks.Clear(); doomStackKeys.Clear();
        doomUnlimitedHits = doomRunawayHits = 0;
    }

    private void ResetDoom()
    {
        DoomClearComponents();
        doomActors.Clear(); doomSeen.Clear(); doomRawStop.Clear(); doomPendingStop.Clear();
        doomStage = -1; doomCar = doomTopology = 1; doomPhase2 = false; doomKey = 0;
    }
}
