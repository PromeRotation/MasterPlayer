using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long underSerial;
    private readonly Dictionary<ulong, DateTime> underBirths = [];
    private readonly HashSet<ulong> underSpheresSeen = [];
    private readonly List<DungeonAmbientAoe> underSpheres = [];
    private readonly HashSet<(ulong Source, DateTime Start)> underStarts = [];
    private readonly List<DungeonPendingCast> underSimple = [];
    private readonly List<(DungeonActor Target, DungeonPendingCast Cast)> underSpreads = [];
    private readonly List<(DungeonActor Target, DungeonPendingCast Cast)> underBusters = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, long Serial)> underBaits = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, long Serial)> underLines = [];

    private void UnderCreated(ulong source, DateTime now)
    {
        if (territory != UnderkeepCatalog.TERRITORY) return;
        if (!underBirths.TryAdd(source, now)) return;
        if (_actors.TryGetValue(source, out var actor)) UnderObserveSphere(actor);
        var i = underSpheres.FindIndex(a => a.Id == $"under/sphere/{source}");
        if (i >= 0) underSpheres[i] = underSpheres[i] with { Activation = now.AddSeconds(7.9) };
        // A scan can see several objects before their Create callbacks. Restore
        // callback order, without resurrecting predictions already consumed.
        var order = underBirths.Keys.Select((id, n) => (id, n)).ToDictionary(p => p.id, p => p.n);
        var ordered = underSpheres.OrderBy(a => order.GetValueOrDefault(ulong.Parse(a.Id.AsSpan("under/sphere/".Length)), int.MaxValue)).ToArray();
        underSpheres.Clear(); underSpheres.AddRange(ordered);
    }

    private void UnderObserveSphere(DungeonActor a)
    {
        if (Active?.BossOid != 0x4791 || a.Oid != 0x4792 || !underSpheresSeen.Add(a.Id)) return;
        underSpheres.Add(new($"under/sphere/{a.Id}", "SphereShatter", new CircleTemplate(6),
            QuantizedDungeonPosition(a.Position), Activation: underBirths.GetValueOrDefault(a.Id, _now).AddSeconds(7.9)));
    }

    private void UnderObserveActors()
    {
        if (territory != UnderkeepCatalog.TERRITORY || Active == null) return;
        foreach (var id in underBirths.Keys)
            if (_actors.TryGetValue(id, out var actor)) UnderObserveSphere(actor);
        foreach (var actor in _actors.Values) UnderObserveSphere(actor);
        DungeonActor Latest(DungeonActor a) => _actors.GetValueOrDefault(a.Id) ?? a;
        for (var i = 0; i < underSpreads.Count; ++i)
        {
            var s = underSpreads[i]; s.Target = Latest(s.Target); underSpreads[i] = s;
        }
        for (var i = 0; i < underBusters.Count; ++i)
        {
            var s = underBusters[i]; s.Target = Latest(s.Target); underBusters[i] = s;
        }
        for (var i = 0; i < underBaits.Count; ++i)
        {
            var s = underBaits[i]; s.Source = Latest(s.Source); s.Target = Latest(s.Target); underBaits[i] = s;
        }
        for (var i = 0; i < underLines.Count; ++i)
        {
            var s = underLines[i]; s.Source = Latest(s.Source); s.Target = Latest(s.Target); underLines[i] = s;
        }
        UnderObserveSector();
        UnderObserveTethers();
    }

    private void UnderCast(DungeonPendingCast cast)
    {
        if (territory != UnderkeepCatalog.TERRITORY) return;
        var fresh = underStarts.Add((cast.Source, cast.StartedAt));
        if (cast.Rule.PartyAoe != null || cast.Rule.Action == 42548)
        {
            var list = cast.Rule.Action == 42548 ? underBusters : underSpreads;
            var i = list.FindIndex(s => s.Cast.Source == cast.Source && s.Cast.StartedAt == cast.StartedAt);
            var target = cast.TargetId == cast.Source && cast.Rule.Action == 42548
                ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId;
            if (_actors.TryGetValue(target, out var actor))
            {
                if (i >= 0) list[i] = (actor, cast);
                else if (fresh) list.Add((actor, cast));
            }
            else if (i >= 0) list.RemoveAt(i);
        }
        else if (cast.Rule.Shape != null)
        {
            var i = underSimple.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (i >= 0) underSimple[i] = cast;
            else if (fresh) underSimple.Add(cast);
        }
    }

    private void UnderFinish(DungeonPendingCast cast)
    {
        if (territory != UnderkeepCatalog.TERRITORY) return;
        var i = underSimple.FindIndex(c => c.Source == cast.Source);
        if (i >= 0) underSimple.RemoveAt(i);
        i = underSpreads.FindIndex(s => s.Target.Id == cast.TargetId && s.Cast.Rule.Action == cast.Rule.Action);
        if (i >= 0) underSpreads.RemoveAt(i);
        if (cast.Rule.Action is 42545 or 43135 && underSpheres.Count > 0) underSpheres.RemoveAt(0);
        if (cast.Rule.Action is 42566 or 42567)
        {
            underSector = null; underCloneCount = 0; underEdges.Clear(); underFirstClone = 0;
            // The source deliberately retains active/direction until later events replace them.
        }
        if (cast.Rule.Action == 42737)
        {
            i = underCrosses.FindIndex(a => UnderNear(a.Position, cast.Location));
            if (i < 0) return;
            underCrosses.RemoveAt(i);
            if (underCrosses.Count == 0)
            {
                underTeleported = false; underCenter = default; underStartingPositions.Clear(); ++underCrossBatches;
            }
        }
    }

    private void UnderEffect(ulong source, uint action, ulong target)
    {
        if (territory != UnderkeepCatalog.TERRITORY) return;
        if (action == 42548)
        {
            var i = underBusters.FindIndex(s => s.Target.Id == target);
            if (i >= 0) underBusters.RemoveAt(i);
        }
        if (action is 42575 or 43261 && underBaits.Count > 0) underBaits.RemoveAt(0);
        if (action == 42540 && underLines.Count > 0) underLines.RemoveAt(0);
        if (Active?.BossOid == 0x47AD && underFirstClone == 0 && action is 43163 or 43164)
        {
            underFirstClone = source; underSectorActive = true;
        }
    }

    private void UnderIcon(ulong target, uint icon, DateTime now, ulong actorId)
    {
        if (territory != UnderkeepCatalog.TERRITORY) return;
        var actor = _actors.GetValueOrDefault(actorId is 0 or 0xE0000000 ? target : actorId);
        if (icon == 525 && Active?.BossOid == 0x478E && actor != null && _actors.TryGetValue(target, out var lineTarget))
            underLines.Add((actor, lineTarget, now.AddSeconds(5.3), ++underSerial));
        if (!(Active?.BossOid == 0x47AD && icon == 591 || Active?.BossOid == 0x478E && icon == 586)) return;
        var baitTarget = _actors.GetValueOrDefault(target) ?? actor;
        if (baitTarget != null && _actors.TryGetValue(PrimaryId, out var primary))
            underBaits.Add((primary, baitTarget, now.AddSeconds(icon == 591 ? 5.1 : 7.4), ++underSerial));
    }

    private void UnderTick(DateTime now)
    {
        if (territory != UnderkeepCatalog.TERRITORY) return;
        underBaits.RemoveAll(s => s.Target.Dead);
        if (!underSectorActive || underFirstClone == 0 || underSector != null || underEdges.Count >= underCloneCount) return;
        var edge = underEdges.FirstOrDefault(e => e.Source.Id == underFirstClone);
        if (edge.Target == null) return;
        var target = _actors.GetValueOrDefault(edge.Target.Id) ?? edge.Target;
        underSector = new("under/sector", "SectorBisector", new FanTemplate(45, MathF.PI),
            QuantizedDungeonPosition(target.Position), target.Rotation + (underDirection ? -1 : 1) * MathF.PI / 2,
            Activation: now.AddSeconds(underCloneCount == 6 ? 4.2 : 5.9));
        underSectorActive = false;
    }

    private void ResetUnder()
    {
        underSerial = 0; underBirths.Clear(); underSpheresSeen.Clear(); underSpheres.Clear();
        underStarts.Clear(); underSimple.Clear(); underSpreads.Clear(); underBusters.Clear(); underBaits.Clear(); underLines.Clear();
        underCloneCount = 0; underDirection = false; underSectorActive = false; underFirstClone = 0; underSector = null;
        underEdges.Clear(); underModels.Clear(); underLiveTethers.Clear(); underPendingTethers.Clear();
        underInferredTeleports.Clear();
        underCrosses.Clear(); underTeleported = false; underCenter = default; underStartingPositions.Clear(); underCrossBatches = 0;
    }
}
