using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long mesoSerial;
    private bool mesoSmall;
    private DungeonAmbientAoe? mesoShrink;
    private readonly HashSet<(ulong Source, DateTime Start)> mesoStarts = [];
    private readonly List<DungeonPendingCast> mesoSimple = [];
    private readonly List<DungeonPendingCast> mesoKnockbacks = [];
    private readonly List<DungeonAmbientAoe> mesoSpheres = [];
    private readonly List<DungeonAmbientAoe> mesoBombs = [];
    private readonly List<DungeonAmbientAoe> mesoKeraunos = [];
    private readonly List<DungeonActor> mesoTerrors = [];
    private DungeonAmbientAoe? mesoTurmoil;
    private bool mesoImpressionActive;
    private bool mesoImpressionUpdated;
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, long Serial)> mesoLines = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, DateTime Start)> mesoBusters = [];
    private int mesoBusterHits;
    private readonly Dictionary<ulong, DungeonActor> mesoLastActors = [];

    private void MesoCast(DungeonPendingCast c)
    {
        if (territory != MesoCatalog.TERRITORY || Arena == null) return;
        var fresh = mesoStarts.Add((c.Source, c.StartedAt));
        var action = c.Rule.Action;
        if (c.Rule.Shape != null)
        {
            var i = mesoSimple.FindIndex(a => a.Source == c.Source && a.StartedAt == c.StartedAt);
            if (i >= 0) mesoSimple[i] = c;
            else if (fresh) mesoSimple.Add(c);
            if (action == 43810 && mesoSimple.Count(a => a.Rule.Action == action) >= 99)
            {
                var indices = Enumerable.Range(0, mesoSimple.Count).Where(n => mesoSimple[n].Rule.Action == action).ToArray();
                var ordered = indices.Select(n => mesoSimple[n]).OrderBy(a => a.FinishAt).ToArray();
                for (var n = 0; n < indices.Length; ++n) mesoSimple[indices[n]] = ordered[n];
            }
        }
        if (action is 43807 or 43818)
        {
            var i = mesoKnockbacks.FindIndex(a => a.Source == c.Source && a.StartedAt == c.StartedAt);
            if (i >= 0) mesoKnockbacks[i] = c;
            else if (fresh) mesoKnockbacks.Add(c);
            if (action == 43818) mesoImpressionActive = true;
        }
        if (action == 43798 && !mesoSmall)
            mesoShrink = new("meso/shrink", "场地收缩", new SohrKhaiWallHintTemplate(
                new RectangleGeometry(Arena.Position, 0, 44, 44), [new RectangleGeometry(Arena.Position, 0, 40, 40)]),
                Arena.Position, Activation: c.FinishAt.AddSeconds(.8));
        if (action == 43806)
            for (var i = 0; i < Math.Min(4, mesoSpheres.Count); ++i)
                mesoSpheres[i] = mesoSpheres[i] with { Navigate = true, Activation = c.FinishAt };
        if (action == 43813)
        {
            var id = $"meso/keraunos/{c.Source}/{c.StartedAt.Ticks}";
            var a = new DungeonAmbientAoe(id, "Keraunography", RayRectangleTemplate.Symmetric(20, 60),
                c.Location, c.Rotation, Activation: c.FinishAt.AddSeconds(3));
            var i = mesoKeraunos.FindIndex(v => v.Id == id);
            if (i >= 0) mesoKeraunos[i] = a;
            else if (fresh) mesoKeraunos.Add(a);
        }
        if (action == 43589)
        {
            var target = c.TargetId == c.Source ? _actors.GetValueOrDefault(c.Source)?.TargetId ?? 0 : c.TargetId;
            if (_actors.TryGetValue(c.Source, out var source) && _actors.TryGetValue(target, out var actor))
            {
                var i = mesoBusters.FindIndex(b => b.Source.Id == c.Source && b.Start == c.StartedAt);
                var b = (source, actor, c.FinishAt.AddSeconds(.4), c.StartedAt);
                if (i >= 0) mesoBusters[i] = b;
                else if (fresh) mesoBusters.Add(b);
            }
        }
    }

    private void MesoFinish(DungeonPendingCast c)
    {
        if (territory != MesoCatalog.TERRITORY) return;
        // The grouped AOEs consume the first entry for this caster, regardless of action.
        var i = mesoSimple.FindIndex(a => a.Source == c.Source);
        if (i >= 0) mesoSimple.RemoveAt(i);
        i = mesoKnockbacks.FindIndex(a => a.Source == c.Source);
        if (i >= 0) mesoKnockbacks.RemoveAt(i);
        if (c.Rule.Action == 43806) mesoSpheres.Clear();
        if (c.Rule.Action == 43811) mesoBombs.Clear();
        if (c.Rule.Action == 45176) mesoKeraunos.Clear();
    }

    private void MesoMap(uint index, uint state)
    {
        if (territory != MesoCatalog.TERRITORY || Arena == null) return;
        if (Active?.BossOid == 0x488F && state == 0x00020001)
        {
            if (index == 4)
            {
                mesoSmall = true; mesoShrink = null;
                Arena = new RectangleGeometry(Arena.Position, 0, 40, 40);
            }
            else if (index is >= 0x0B and <= 0x12)
            {
                var corner = (index - 0x0B) % 4;
                var p = new Vector3(corner % 2 == 0 ? 260 : 280, Arena.Position.Y, corner < 2 ? 2 : 22);
                mesoSpheres.Add(new($"meso/sphere/{++mesoSerial}", "SterileSphere", new CircleTemplate(index < 0x0F ? 15 : 8),
                    QuantizedDungeonPosition(p), Navigate: false));
            }
        }
        if (!IsMesoHeadsmen || index is not (0x19 or 0x1A)) return;
        var pos = new Vector3(index == 0x19 ? 42.5f : 77.5f, Arena.Position.Y, -258);
        if (state == 0x00020001)
            mesoHellmakers.Add(new($"meso/hellmaker/{++mesoSerial}", "CellBlock", new RectangleTemplate(16, 16), pos));
        else if (state == 0x00200004)
        {
            var i = mesoHellmakers.FindIndex(a => a.Position == pos);
            if (i >= 0) mesoHellmakers.RemoveAt(i);
        }
    }

    private void MesoTimeline(ulong source, uint timeline)
    {
        if (territory != MesoCatalog.TERRITORY || Active?.BossOid != 0x48BE
            || _actors.GetValueOrDefault(source) is not { Oid: 0x48C0 } a) return;
        if (timeline == 0x11D3) mesoTerrors.Add(a);
        else
        {
            var i = mesoTerrors.FindIndex(t => t.Id == source);
            if (i >= 0) mesoTerrors.RemoveAt(i);
        }
    }

    private void MesoYell(ulong source, uint yell, DateTime now)
    {
        if (territory != MesoCatalog.TERRITORY || mesoTerrors.Count <= 6 || yell is < 18705 or > 18708
            || !_actors.TryGetValue(source, out var a)) return;
        var big = mesoTerrors.Any(t => t.Id != a.Id && MesoNear(t.Position, a.Position, 5));
        var rotation = MathF.Round(a.Rotation * 180 / MathF.PI) * MathF.PI / 180;
        var p = a.Position + (big ? 3.5f : 0) * new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation));
        mesoBombs.Add(new($"meso/bomb/{++mesoSerial}", "Bombardment", new CircleTemplate(big ? 14 : 3),
            QuantizedDungeonPosition(p), Activation: mesoBombs.Count == 0 ? now.AddSeconds(9.8) : mesoBombs[0].Activation));
    }

    private void MesoIcon(ulong target, uint icon, DateTime now, ulong source)
    {
        if (territory != MesoCatalog.TERRITORY || Active?.BossOid != 0x48BE || icon != 525) return;
        if (_actors.TryGetValue(source is 0 or 0xE0000000 ? target : source, out var a)
            && _actors.TryGetValue(target, out var b)) mesoLines.Add((a, b, now.AddSeconds(5.2), ++mesoSerial));
    }

    private void MesoEffect(ulong source, uint action, DateTime now, bool hasTargets, Vector3? position, float? rotation)
    {
        if (territory != MesoCatalog.TERRITORY) return;
        if (action == 43590 && ++mesoBusterHits == 3) { mesoBusters.Clear(); mesoBusterHits = 0; }
        if (action == 43822 && mesoLines.Count > 0) mesoLines.RemoveAt(0);
        if (action is 43814 or 43815 && _actors.TryGetValue(source, out var a))
            mesoTurmoil = new("meso/turmoil", "Turmoil", RayRectangleTemplate.Symmetric(20, 40),
                QuantizedDungeonPosition((position ?? a.Position) + Vector3.UnitX * (action == 43814 ? -10 : 10)), Activation: now.AddSeconds(4.4));
        if (action == 43816) mesoTurmoil = null;
        if (action == 43594) MesoAdvanceSword(source, now, hasTargets, position, rotation);
    }

    private void MesoObserveActors()
    {
        if (territory != MesoCatalog.TERRITORY) return;
        foreach (var a in _actors.Values) mesoLastActors[a.Id] = a;
        foreach (var id in mesoLastActors.Keys.Where(id => !_actors.ContainsKey(id) && !mesoDestroyed.Contains(id)).ToArray())
            MesoRemoved(id);
        DungeonActor Latest(DungeonActor a) => _actors.GetValueOrDefault(a.Id) ?? a;
        for (var i = 0; i < mesoTerrors.Count; ++i) mesoTerrors[i] = Latest(mesoTerrors[i]);
        for (var i = 0; i < mesoLines.Count; ++i)
        {
            var b = mesoLines[i]; b.Source = Latest(b.Source); b.Target = Latest(b.Target); mesoLines[i] = b;
        }
        for (var i = 0; i < mesoBusters.Count; ++i)
        {
            var b = mesoBusters[i]; b.Source = Latest(b.Source); b.Target = Latest(b.Target); mesoBusters[i] = b;
        }
        MesoObservePrisons();
        foreach (var a in _actors.Values)
        {
            if (a.Oid == 0x4894) MesoObserveSword(a);
            if (MesoCatalog.Headsman(a.Oid) && a.Dead && mesoDeadBosses.Add(a.Id)) MesoSwordDeath(a);
        }
    }

    private void MesoTick()
    {
        if (territory != MesoCatalog.TERRITORY) return;
        if (mesoBusters.Count > 0 && (mesoBusters[0].Source.Dead || mesoBusters[0].Target.Dead)) mesoBusters.Clear();
        if (mesoImpressionActive)
        {
            if (!mesoImpressionUpdated && mesoBombs.Count == 8)
            {
                for (var i = 0; i < mesoBombs.Count; ++i) mesoBombs[i] = mesoBombs[i] with { Navigate = false };
                mesoImpressionUpdated = true;
            }
            else if (mesoImpressionUpdated && mesoKnockbacks.Count == 0)
            {
                for (var i = 0; i < mesoBombs.Count; ++i) mesoBombs[i] = mesoBombs[i] with { Navigate = true };
                mesoImpressionUpdated = false; mesoImpressionActive = false;
            }
        }
        MesoUpdateArena();
        MesoBuildSwordCache();
    }

    private static bool MesoNear(Vector3 a, Vector3 b, float tolerance)
        => MathF.Abs(a.X - b.X) <= tolerance && MathF.Abs(a.Z - b.Z) <= tolerance;

    private void ResetMeso()
    {
        mesoSerial = 0; mesoSmall = false; mesoShrink = null; mesoStarts.Clear(); mesoSimple.Clear();
        mesoKnockbacks.Clear(); mesoSpheres.Clear(); mesoBombs.Clear(); mesoKeraunos.Clear(); mesoTerrors.Clear();
        mesoTurmoil = null; mesoImpressionActive = false; mesoImpressionUpdated = false;
        mesoLines.Clear(); mesoBusters.Clear(); mesoBusterHits = 0;
        mesoAssigned.Clear(); mesoPrisons.Clear(); mesoStatusObserved.Clear(); mesoPendingStatuses.Clear();
        mesoTethers.Clear(); mesoPendingTethers.Clear(); mesoBossIds.Clear(); mesoDestroyed.Clear();
        mesoHellmakers.Clear(); mesoPrisonArena = null; mesoPrisonDirty = true;
        mesoDefaultArena = null;
        mesoBirths.Clear(); mesoSwordsSeen.Clear(); mesoSwords.Clear(); mesoSwordCache.Clear(); mesoSwordDirty = false;
        mesoDeadBosses.Clear();
        mesoLastActors.Clear(); mesoInferredStatuses.Clear();
    }
}
