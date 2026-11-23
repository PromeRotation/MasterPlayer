using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DateTime> mesoBirths = [];
    private readonly HashSet<ulong> mesoSwordsSeen = [];
    private readonly HashSet<ulong> mesoDeadBosses = [];
    private readonly List<MesoSwordLine> mesoSwords = [];
    private readonly List<DungeonAmbientAoe> mesoSwordCache = [];
    private bool mesoSwordDirty;

    private void MesoCreated(ulong source, DateTime now)
    {
        if (territory != MesoCatalog.TERRITORY || !mesoBirths.TryAdd(source, now)) return;
        if (_actors.TryGetValue(source, out var a)) MesoObserveSword(a);
        var i = mesoSwords.FindIndex(s => s.Source == source && s.Left == 51);
        if (i >= 0)
        {
            mesoSwords[i] = mesoSwords[i] with { At = now.AddSeconds(5.5) };
            mesoSwordDirty = true;
        }
    }

    private void MesoObserveSword(DungeonActor a)
    {
        if (!IsMesoHeadsmen || a.Oid != 0x4894 || !mesoSwordsSeen.Add(a.Id)) return;
        var direction = new Vector3(MathF.Sin(a.Rotation), 0, MathF.Cos(a.Rotation));
        mesoSwords.Add(new(a.Id, ++mesoSerial, a.Position, direction * .24118f, a.Rotation,
            mesoBirths.GetValueOrDefault(a.Id, _now).AddSeconds(5.5)));
        mesoSwordDirty = true;
    }

    private void MesoAdvanceSword(ulong source, DateTime now, bool hasTargets, Vector3? position, float? rotation)
    {
        if (!_actors.TryGetValue(source, out var actor)) return;
        actor = actor with { Position = position ?? actor.Position, Rotation = rotation ?? actor.Rotation };
        var i = mesoSwords.FindIndex(l => MesoNear(l.Next, actor.Position, 1));
        if (i < 0) return;
        var line = mesoSwords[i];
        var advance = line.Left == 50
            ? .3222f * new Vector3(MathF.Sin(actor.Rotation), 0, MathF.Cos(actor.Rotation)) : line.Advance;
        if (line.Left == 1 || hasTargets) mesoSwords.RemoveAt(i);
        else mesoSwords[i] = line with { Next = actor.Position + advance, Advance = advance, At = now.AddSeconds(.2), Left = line.Left - 1 };
        mesoSwordDirty = true;
    }

    private void MesoSwordDeath(DungeonActor a)
    {
        var middle = a.Position.X is > 50 and < 70;
        bool Angle(float a, float b) => MathF.Abs(MathF.Atan2(MathF.Sin(a - b), MathF.Cos(a - b))) <= MathF.PI / 180;
        mesoSwords.RemoveAll(l => MesoNear(l.Next, a.Position, 10)
            && (middle ? Angle(l.Rotation, MathF.PI / 2) || Angle(l.Rotation, -MathF.PI / 2)
                : Angle(l.Rotation, 0) || Angle(l.Rotation, MathF.PI)));
        mesoSwordDirty = true;
    }

    private void MesoRemoved(ulong source)
    {
        if (territory != MesoCatalog.TERRITORY) return;
        if (!mesoDestroyed.Add(source)) return;
        // ActorState destroys tethers and statuses before removing the actor.
        foreach (var (key, target) in mesoTethers.Where(p => p.Key.Actor == source).ToArray())
            MesoTether(source, target, key.Tether, false, true);
        foreach (var key in mesoPendingTethers.Keys.Where(k => k.Actor == source).ToArray()) mesoPendingTethers.Remove(key);
        foreach (var (key, status) in mesoStatusObserved.Where(p => p.Key.Actor == source && p.Value.Present).ToArray())
            MesoStatus(source, key.Status, false, status.Param, true);
        foreach (var key in mesoPendingStatuses.Keys.Where(k => k.Actor == source).ToArray()) mesoPendingStatuses.Remove(key);
        if (_casts.TryGetValue(source, out var c)) MesoFinish(c);
        var a = _actors.GetValueOrDefault(source) ?? mesoLastActors.GetValueOrDefault(source);
        if (a?.Oid == 0x4894)
        {
            var i = mesoSwords.FindIndex(l => MesoNear(l.Next, a.Position, 1));
            if (i >= 0) { mesoSwords.RemoveAt(i); mesoSwordDirty = true; }
        }
    }

    private void MesoBuildSwordCache()
    {
        if (!mesoSwordDirty) return;
        mesoSwordCache.Clear();
        foreach (var l in mesoSwords)
        {
            var pos = l.Next;
            var time = l.At > _now ? l.At : _now;
            for (var i = 1; i < Math.Min(12, l.Left); ++i)
            {
                pos += l.Advance; time = time.AddSeconds(.2);
                mesoSwordCache.Add(new($"meso/sword/{l.Serial}/{i}", "PealOfJudgment", RayRectangleTemplate.Symmetric(4, 2),
                    QuantizedDungeonPosition(pos), l.Rotation, Activation: time));
            }
        }
        foreach (var l in mesoSwords)
            mesoSwordCache.Add(new($"meso/sword/{l.Serial}/0", "PealOfJudgment", RayRectangleTemplate.Symmetric(4, 2),
                QuantizedDungeonPosition(l.Next), l.Rotation, Activation: l.At, Color: new Vector4(1, .1f, .1f, 1)));
        mesoSwordDirty = false;
    }
}
