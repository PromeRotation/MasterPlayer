using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DateTime> burnSpreads = [];
    private readonly Dictionary<ulong, DateTime> burnResidue = [];
    private readonly Dictionary<ulong, (bool Cone, DateTime Activation)> burnBaits = [];
    private readonly List<DungeonAmbientAoe> burnThrottles = [];
    private readonly List<(ulong Source, DungeonAmbientAoe Aoe)> burnCrosses = [];
    private readonly HashSet<(ulong Source, uint Action, DateTime Start)> burnSeenCasts = [];
    private readonly HashSet<ulong> burnSeenHelpers = [];
    private readonly Dictionary<ulong, DateTime> burnBirths = [];
    private DungeonAmbientAoe? burnCauterize;
    private IGeometryShape[] burnHailShadows = [];
    private ulong burnHailTarget;
    private DateTime burnColdActivation;
    private DateTime burnColdCurrentAt;
    private ushort burnColdParam;
    private bool burnColdActive;
    private bool burnColdReset;
    private bool burnColdResolved;

    private void BurnCast(DungeonPendingCast cast)
    {
        if (territory != BurnCatalog.TERRITORY || !burnSeenCasts.Add((cast.Source, cast.Rule.Action, cast.StartedAt))) return;
        if (cast.Rule.Action == 12692 && _actors.TryGetValue(PrimaryId, out var boss))
        {
            burnHailTarget = cast.TargetId;
            burnHailShadows = _actors.Values.Where(a => a.Oid == 0x241A && !a.Dead
                && Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(boss.Position.X, boss.Position.Z)) > .0001f).Select(a =>
            {
                var delta = a.Position - boss.Position; delta.Y = 0;
                var direction = Vector3.Normalize(delta);
                return RayRectangleTemplate.Symmetric(3.2f, 44.1f).Resolve(a.Position + direction * .1f,
                    MathF.Atan2(direction.X, direction.Z), null);
            }).ToArray();
        }
        if (cast.Rule.Action == 12613)
            foreach (var angle in new[] { MathF.PI / 2, MathF.PI / 4 })
                burnCrosses.Add((cast.Source, new($"burn/cross/{cast.Source}/{angle}", "雾羽后续十字",
                    new DungeonCrossTemplate(40, 2.5f), cast.Location, angle, Activation: cast.FinishAt.AddSeconds(3.6))));
        if (cast.Rule.Action == 12609)
        {
            burnColdActivation = cast.FinishAt;
            if (burnColdActive) burnColdCurrentAt = burnColdActivation;
        }
    }

    private void BurnFinish(DungeonPendingCast cast, bool completed)
    {
        if (territory != BurnCatalog.TERRITORY) return;
        if (cast.Rule.Action == 12692) { burnHailTarget = 0; burnHailShadows = []; }
        if (cast.Rule.Action == 11637) burnThrottles.Clear();
        if (cast.Rule.Action == 12614) burnCrosses.Clear();
        if (!completed && cast.Rule.Action == 12613) burnCrosses.RemoveAll(c => c.Source == cast.Source);
        if (cast.Rule.Action == 12617) burnCauterize = null;
    }

    private void BurnIcon(ulong target, uint icon, DateTime time)
    {
        if (Active?.BossOid == 0x2419 && icon == 96) burnSpreads.TryAdd(target, time.AddSeconds(5.8));
        if (Active?.BossOid == 0x23AA && icon == 2) burnResidue.TryAdd(target, time.AddSeconds(4.1));
        if (Active?.BossOid == 0x2431 && icon is 14 or 26) burnBaits.TryAdd(target, (icon == 26, time.AddSeconds(icon == 26 ? 4.1 : 6.1)));
    }

    private void BurnEffect(ulong source, uint action, ulong target, DateTime time)
    {
        if (territory != BurnCatalog.TERRITORY) return;
        if (action == 12697) burnSpreads.Remove(target);
        if (action == 11636 && burnResidue.Count > 0) burnResidue.Remove(burnResidue.Keys.First());
        if (action is 12620 or 12621)
            foreach (var id in burnBaits.Where(p => p.Value.Cone == (action == 12620)).Select(p => p.Key).ToArray()) burnBaits.Remove(id);
        if (action == 12610)
        {
            burnColdActive = false; burnColdReset = false; burnColdResolved = true;
        }
        if (action != 13526) return;
        foreach (var drone in _actors.Values.Where(a => a.Oid == 0x23AB && a.AnimationState1 != 1))
            burnThrottles.Add(new($"burn/throttle/{drone.Id}", "采掘机冲锋", RayRectangleTemplate.Symmetric(3, 50),
                QuantizedDungeonPosition(drone.Position), drone.Rotation, Activation: time.AddSeconds(8.2)));
        var side = burnThrottles.Count > 0 && burnThrottles[0].Position.X < 0 ? -1 : 1;
        burnThrottles.Add(new("burn/throttle/boss", "全速冲锋", RayRectangleTemplate.Symmetric(5, 50),
            QuantizedDungeonPosition(new(side * 18, Arena!.Position.Y, -71.5f)), -MathF.PI / 2 * side, Activation: time.AddSeconds(8.2)));
    }

    private void BurnStatus(ulong target, uint status, bool present, ushort param)
    {
        if (Active?.BossOid != 0x2431 || target != PrimaryId || status != 618) return;
        if (!present) return;
        burnColdParam = param; burnColdActive = true; burnColdResolved = false;
        burnColdCurrentAt = burnColdActivation;
    }

    private void BurnCreated(ulong source, DateTime time)
    {
        if (territory != BurnCatalog.TERRITORY) return;
        burnBirths.TryAdd(source, time);
        if (burnCauterize?.Id == $"burn/cauterize/{source}" && time.AddSeconds(7.3) < burnCauterize.Activation)
            burnCauterize = burnCauterize with { Activation = time.AddSeconds(7.3) };
    }

    private void SynchronizeBurn()
    {
        if (territory != BurnCatalog.TERRITORY) return;
        if (Active?.BossOid == 0x2431)
        {
            foreach (var actor in _actors.Values.Where(a => a.Oid == 0x2434 && !a.Dead && burnSeenHelpers.Add(a.Id)))
                burnCauterize = new($"burn/cauterize/{actor.Id}", "浓雾俯冲", RayRectangleTemplate.Symmetric(16, 40),
                    QuantizedDungeonPosition(actor.Position), actor.Rotation, Activation: burnBirths.GetValueOrDefault(actor.Id, _now).AddSeconds(7.3));
            if (_actors.TryGetValue(PrimaryId, out var boss))
            {
                if (boss.Statuses?.Contains(618u) != true) burnColdResolved = false;
                else if (!burnColdResolved && boss.StatusParams?.GetValueOrDefault(618u) is { } param
                    && (!burnColdActive || burnColdParam != param))
                    BurnStatus(boss.Id, 618, true, param);
            }
            if (burnColdActive && !burnColdReset && !_actors.Values.Any(a => a.Oid == 0x2432 && !a.Dead))
            {
                burnColdCurrentAt = _now.AddSeconds(5.6);
                burnColdReset = true;
            }
        }
        foreach (var id in burnSpreads.Keys.Concat(burnResidue.Keys).Concat(burnBaits.Keys).Distinct()
            .Where(id => !_actors.TryGetValue(id, out var actor) || actor.Dead).ToArray()) BurnRemoved(id);
    }

    private void BurnRemoved(ulong id)
    {
        burnSpreads.Remove(id); burnResidue.Remove(id); burnBaits.Remove(id);
        burnCrosses.RemoveAll(c => c.Source == id); burnBirths.Remove(id);
        if (burnHailTarget == id) { burnHailTarget = 0; burnHailShadows = []; }
        if (burnCauterize?.Id == $"burn/cauterize/{id}") burnCauterize = null;
    }

    private void ResetBurn()
    {
        burnSpreads.Clear(); burnResidue.Clear(); burnBaits.Clear(); burnThrottles.Clear(); burnCrosses.Clear();
        burnSeenCasts.Clear(); burnSeenHelpers.Clear(); burnBirths.Clear(); burnCauterize = null;
        burnHailShadows = []; burnHailTarget = 0; burnColdActivation = default; burnColdCurrentAt = default;
        burnColdParam = 0; burnColdActive = burnColdReset = burnColdResolved = false;
    }
}
