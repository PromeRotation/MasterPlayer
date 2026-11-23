using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(ulong Target, DateTime Activation)> twinningImpacts = [];
    private readonly List<(ulong Target, DateTime Activation)> twinningPounces = [];
    private readonly HashSet<(ulong Target, uint Icon, DateTime Time)> twinningIcons = [];
    private readonly Dictionary<ulong, DateTime> twinningBirths = [];
    private readonly HashSet<ulong> twinningSeen = [];
    private readonly Dictionary<ulong, DungeonAmbientAoe> twinningShocks = [];
    private readonly Dictionary<ulong, (DungeonAmbientAoe Short, DungeonAmbientAoe Long)> twinningLasers = [];

    private void TwinningIcon(ulong target, uint icon, DateTime time)
    {
        if (Active?.BossOid != 0x27D1 || !twinningIcons.Add((target, icon, time))) return;
        if (icon is >= 50 and <= 53) twinningImpacts.Add((target, time.AddSeconds(7.2)));
        if (icon == 90) twinningPounces.Add((target, time.AddSeconds(4.6)));
    }

    private void TwinningCreated(ulong source, DateTime time)
    {
        if (territory != TwinningCatalog.TERRITORY) return;
        if (!twinningBirths.TryGetValue(source, out var previous) || time < previous) twinningBirths[source] = time;
        if (twinningShocks.TryGetValue(source, out var shock))
            twinningShocks[source] = shock with { Activation = twinningBirths[source].AddSeconds(7.7) };
        if (twinningLasers.TryGetValue(source, out var laser))
            twinningLasers[source] = (laser.Short with { Activation = twinningBirths[source].AddSeconds(6.5) },
                laser.Long with { Activation = twinningBirths[source].AddSeconds(14.3) });
    }

    private void SynchronizeTwinning()
    {
        if (territory != TwinningCatalog.TERRITORY) return;
        foreach (var actor in _actors.Values.Where(a => !a.Dead
            && (Active?.BossOid == 0x2803 && a.Oid == 0x2804 || Active?.BossOid == 0x2805 && a.Oid == 0x1EAC39)))
        {
            if (!twinningSeen.Add(actor.Id)) continue;
            var birth = twinningBirths.GetValueOrDefault(actor.Id, _now);
            var p = QuantizedDungeonPosition(actor.Position);
            if (actor.Oid == 0x2804)
                twinningShocks[actor.Id] = new($"twinning/shock/{actor.Id}", "雷球延迟爆炸", new CircleTemplate(6),
                    p, Activation: birth.AddSeconds(7.7));
            else
                twinningLasers[actor.Id] = (new($"twinning/laser/{actor.Id}/short", "时间停止短光束", RayRectangleTemplate.Symmetric(8, 10),
                    p, actor.Rotation, Activation: birth.AddSeconds(6.5)),
                    new($"twinning/laser/{actor.Id}/long", "时间恢复长光束", RayRectangleTemplate.Symmetric(8, 40),
                        p, actor.Rotation, Activation: birth.AddSeconds(14.3)));
        }
        twinningImpacts.RemoveAll(b => !_actors.TryGetValue(b.Target, out var a) || a.Dead);
        twinningPounces.RemoveAll(b => !_actors.TryGetValue(b.Target, out var a) || a.Dead);
    }

    private void TwinningEffect(ulong source, uint action, Vector3? location)
    {
        if (territory != TwinningCatalog.TERRITORY) return;
        if (action is 15713 or 15718 or 15719 or 15720 && twinningImpacts.Count > 0) twinningImpacts.RemoveAt(0);
        if (action is 15711 or 15721 or 15722 or 16842 && twinningPounces.Count > 0) twinningPounces.RemoveAt(0);
        if (action == 15857) twinningShocks.Clear();
        if (action is not (15863 or 15859) || (_actors.GetValueOrDefault(source)?.Position ?? location) is not { } origin) return;
        foreach (var id in twinningLasers.Where(l => Vector2.DistanceSquared(
            new(l.Value.Short.Position.X, l.Value.Short.Position.Z), new(origin.X, origin.Z)) <= 1).Select(l => l.Key).ToArray())
            twinningLasers.Remove(id);
    }

    private void TwinningRemoved(ulong source)
    {
        twinningImpacts.RemoveAll(b => b.Target == source); twinningPounces.RemoveAll(b => b.Target == source);
        // Laser and shock VFX objects can disappear before the actual helper
        // effects. Keep their locked forecasts until those effects resolve.
    }

    private void ResetTwinning()
    {
        twinningImpacts.Clear(); twinningPounces.Clear(); twinningIcons.Clear(); twinningBirths.Clear();
        twinningSeen.Clear(); twinningShocks.Clear(); twinningLasers.Clear();
    }
}
