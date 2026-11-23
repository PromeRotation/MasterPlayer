using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<DungeonAmbientAoe> heroesDash = [];
    private readonly Dictionary<ulong, ushort> heroesDashStates = [];
    private readonly HashSet<(ulong Source, ushort Param, DateTime Time)> heroesDashEvents = [];
    private readonly Dictionary<ulong, byte> heroesModels = [];
    private readonly List<DungeonAmbientAoe> heroesBursts = [];
    private readonly List<(ulong Target, DateTime Activation)> heroesBombBaits = [];
    private readonly Dictionary<ulong, DateTime> heroesSpreads = [];
    private readonly List<ulong> heroesSpreadOrder = [];
    private readonly Dictionary<ulong, (DungeonActor Actor, DateTime Activation)> heroesRubble = [];
    private readonly HashSet<ulong> heroesRubbleSeen = [];
    private readonly HashSet<Vector3> heroesCraters = [];
    private readonly Dictionary<ulong, DungeonPendingCast> heroesBusters = [];
    private DungeonAmbientAoe? heroesCollapse;
    private bool heroesShrunk;
    private long heroesSerial;

    private void SynchronizeHeroes()
    {
        if (territory != HeroesCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x2DEC && _actors.TryGetValue(PrimaryId, out var thief))
        {
            if (HasStatus(thief, 2193))
                HeroesDash(thief.Id, thief.StatusParams?.GetValueOrDefault(2193u) ?? 0, _now, false);
            else heroesDashStates.Remove(thief.Id);
        }
        if (Active?.BossOid == 0x2DF1)
        {
            foreach (var a in _actors.Values.Where(a => !a.Dead))
            {
                if (a.ModelState == 54 && heroesModels.GetValueOrDefault(a.Id) != 54)
                    heroesBursts.Add(new($"heroes/burst/{++heroesSerial}", "僵尸延迟爆炸", new CircleTemplate(8),
                        QuantizedDungeonPosition(a.Position), Activation: _now.AddSeconds(6)));
                heroesModels[a.Id] = a.ModelState;
            }
            heroesBombBaits.RemoveAll(b => !_actors.TryGetValue(b.Target, out var a) || a.Dead);
        }
        if (Active?.BossOid != 0x2EFD) return;
        foreach (var rubble in _actors.Values.Where(a => a.Oid == 0x2EFE && !a.Dead))
            if (heroesRubbleSeen.Add(rubble.Id)) heroesRubble[rubble.Id] = (rubble, _now.AddSeconds(6.1));
        foreach (var id in heroesSpreads.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) heroesSpreads.Remove(id);
        heroesSpreadOrder.RemoveAll(id => !heroesSpreads.ContainsKey(id));
        if (heroesSpreads.Count == 0) heroesRubble.Clear();
    }

    private void HeroesDash(ulong source, ushort param, DateTime time, bool actualEvent)
    {
        if (Active?.BossOid != 0x2DEC || source != PrimaryId) return;
        if (actualEvent ? !heroesDashEvents.Add((source, param, time)) : heroesDashStates.GetValueOrDefault(source) == param) return;
        heroesDashStates[source] = param;
        GeometryTemplate? shape = param switch
        {
            0xB0 => new CircleTemplate(15),
            0xB1 or 0xB2 => RayRectangleTemplate.Symmetric(14, 80),
            _ => null,
        };
        if (shape == null) return;
        var rotation = (param == 0xB2 ? 89.999f : -.003f) * MathF.PI / 180;
        foreach (var marker in _actors.Values.Where(a => a.Oid == 0x1EAED9))
        {
            var position = marker.Position;
            if (param is 0xB1 or 0xB2)
                position -= new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation)) * 40;
            heroesDash.Add(new($"heroes/dash/{++heroesSerial}", "瞬移后攻击", shape, position, rotation, Activation: time.AddSeconds(8.1)));
        }
    }

    private void HeroesStatus(ulong target, uint status, bool present, ushort param, DateTime time)
    {
        if (territory != HeroesCatalog.TERRITORY || status != 2193) return;
        if (present) HeroesDash(target, param, time, true);
        else heroesDashStates.Remove(target);
    }

    private void HeroesIcon(ulong target, uint icon, DateTime time)
    {
        if (territory != HeroesCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x2DF1 && icon == 23 && !heroesBombBaits.Contains((target, time.AddSeconds(5.1))))
            heroesBombBaits.Add((target, time.AddSeconds(5.1)));
        if (Active?.BossOid == 0x2EFD && icon == 229)
        {
            if (!heroesSpreads.ContainsKey(target)) heroesSpreadOrder.Add(target);
            heroesSpreads[target] = time.AddSeconds(6);
        }
    }

    private void HeroesAnimation(ulong source, uint state)
    {
        if (Active?.BossOid != 0x2EFD || state != 0x00010002 || !_actors.TryGetValue(source, out var a) || a.Oid != 0x1EA1A1) return;
        if (a.Position.X is 738 or 762 && a.Position.Z == 482) heroesCraters.Add(new(a.Position.X, Arena?.Position.Y ?? a.Position.Y, a.Position.Z));
    }

    private void HeroesCast(DungeonPendingCast cast)
    {
        if (territory != HeroesCatalog.TERRITORY) return;
        if (cast.Rule.Action == 20427)
            heroesBusters[cast.Source] = cast with { TargetId = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId };
        if (cast.Rule.Action == 21004 && !heroesShrunk)
        {
            var danger = HeroesArenas.BerserkerCollapse(cast.Location.Y);
            heroesCollapse = new("heroes/collapse", "场地角落塌落", new DungeonShapeTemplate(danger), danger.Position,
                Activation: cast.FinishAt.AddSeconds(1.1));
        }
    }

    private void HeroesFinish(DungeonPendingCast cast, bool completed)
    {
        if (territory != HeroesCatalog.TERRITORY) return;
        if (!completed)
        {
            heroesBusters.Remove(cast.Source);
            if (cast.Rule.Action == 21004) heroesCollapse = null;
        }
        if (HeroesCatalog.BURSTS.Contains(cast.Rule.Action)) heroesBursts.Clear();
    }

    private void HeroesMap(uint index, uint state)
    {
        if (territory != HeroesCatalog.TERRITORY || Active?.BossOid != 0x2EFD || index != 0x0B || state != 0x00020001) return;
        heroesShrunk = true; heroesCollapse = null;
        Arena = HeroesArenas.Berserker(Arena?.Position.Y ?? 0);
    }

    private void HeroesEffect(uint action, ulong target)
    {
        if (territory != HeroesCatalog.TERRITORY) return;
        if (action == 20427) heroesBusters.Clear();
        if (action is 20434 or 20433 or 20577 or 20578) heroesDash.Clear();
        if (action == 20323 && heroesBombBaits.Count > 0) heroesBombBaits.RemoveAt(0);
        if (action is 21000 or 21001)
        {
            if (_actors.TryGetValue(target, out var t))
                foreach (var id in heroesRubble.Where(r => Vector3.DistanceSquared(r.Value.Actor.Position, t.Position) <= 72.25f).Select(r => r.Key).ToArray()) heroesRubble.Remove(id);
            if (action == 21000) heroesSpreads.Remove(target);
            else if (!heroesSpreads.Remove(target) && heroesSpreadOrder.Count > 0) heroesSpreads.Remove(heroesSpreadOrder[0]);
            heroesSpreadOrder.RemoveAll(id => !heroesSpreads.ContainsKey(id));
            if (heroesSpreads.Count == 0) heroesRubble.Clear();
        }
    }

    private IEnumerable<DungeonDamagePrediction> HeroesDamage()
    {
        foreach (var cast in heroesBusters.Values)
            if (_actors.TryGetValue(cast.TargetId, out var a) && !a.Dead && a.PartySlot >= 0)
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, cast.FinishAt, "Tankbuster");
    }

    internal DungeonActor? HeroesCleanseTarget(uint job)
    {
        if (Active?.BossOid != 0x2DF1 || job is not (6 or 24 or 28 or 33 or 40 or 23) || !_actors.TryGetValue(PlayerId, out var player)) return null;
        return _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Targetable && HasStatus(a, 910)
            && Vector3.DistanceSquared(player.Position, a.Position) <= MathF.Pow(30 + a.HitboxRadius, 2))
            .OrderBy(a => a.Id != PlayerId).ThenBy(a => a.PartySlot).FirstOrDefault();
    }

    private void HeroesRemoved(ulong source)
    {
        if (_casts.TryGetValue(source, out var cast) && HeroesCatalog.BURSTS.Contains(cast.Rule.Action)) heroesBursts.Clear();
        heroesModels.Remove(source);
        heroesSpreadOrder.Remove(source);
        heroesBombBaits.RemoveAll(b => b.Target == source); heroesSpreads.Remove(source); heroesRubble.Remove(source); heroesBusters.Remove(source);
    }

    private void ResetHeroes()
    {
        heroesDash.Clear(); heroesDashStates.Clear(); heroesDashEvents.Clear(); heroesModels.Clear(); heroesBursts.Clear(); heroesBombBaits.Clear();
        heroesSpreads.Clear(); heroesSpreadOrder.Clear(); heroesRubble.Clear(); heroesRubbleSeen.Clear(); heroesCraters.Clear(); heroesBusters.Clear(); heroesCollapse = null; heroesShrunk = false; heroesSerial = 0;
    }
}
