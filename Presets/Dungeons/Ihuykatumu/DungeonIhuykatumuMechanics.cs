using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private int ihuyInhales;
    private long ihuySerial;
    private DateTime ihuyTetherActivation;
    private DungeonAmbientAoe? ihuyInhale;
    private readonly List<DungeonAmbientAoe> ihuyBury = [];
    private readonly List<(Vector3 Position, int Stage, DateTime At, ulong Source, DateTime Start)> ihuyShores = [];
    private readonly List<DungeonAmbientAoe> ihuyWinds = [];
    private readonly List<ulong> ihuyWings = [];
    private readonly List<(Vector3 Position, DateTime At, long Serial)> ihuySparkPredictions = [];
    private readonly HashSet<ulong> ihuyCreated = [];
    private readonly HashSet<(ulong Source, ulong Target)> ihuyTetherPairs = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DateTime At, long Serial)> ihuyBaits = [];

    private bool IhuyCastVisible(DungeonPendingCast cast)
    {
        if (territory != IhuykatumuCatalog.TERRITORY) return true;
        if (IhuykatumuCatalog.IsBury(cast.Rule.Action)) return false;
        return cast.Rule.Action != 36351 || ihuyWings.Take(8).Contains(cast.Source);
    }

    private void IhuyCast(DungeonPendingCast cast)
    {
        if (territory != IhuykatumuCatalog.TERRITORY || Arena == null) return;
        if (cast.Rule.Action == 36351 && !ihuyWings.Contains(cast.Source)) ihuyWings.Add(cast.Source);
        if (cast.Rule.Action == 36494)
            ihuyInhale = new("ihuy/inhale", "吸气与浮出水面", new FanTemplate(100, MathF.PI / 3),
                QuantizedDungeonPosition(new(15, Arena.Position.Y, -95)), cast.Rotation, Activation: cast.FinishAt);
        if (IhuykatumuCatalog.IsBury(cast.Rule.Action))
        {
            var id = $"ihuy/bury/{cast.Source}/{cast.StartedAt.Ticks}";
            var index = ihuyBury.FindIndex(a => a.Id == id);
            var a = new DungeonAmbientAoe(id, cast.Rule.Name, cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
            if (index >= 0) ihuyBury[index] = a with { Color = ihuyBury[index].Color }; else ihuyBury.Add(a);
        }
        if (cast.Rule.Action == 36515)
        {
            var index = ihuyShores.FindIndex(s => s.Source == cast.Source && s.Start == cast.StartedAt);
            if (index < 0) ihuyShores.Add((cast.Location, 0, cast.FinishAt, cast.Source, cast.StartedAt));
            else if (ihuyShores[index].Stage == 0) ihuyShores[index] = ihuyShores[index] with { Position = cast.Location };
        }
    }

    private void IhuyFinish(DungeonPendingCast cast, DateTime now)
    {
        if (territory != IhuykatumuCatalog.TERRITORY) return;
        if (cast.Rule.Action == 36351) ihuyWings.Remove(cast.Source);
        if (IhuykatumuCatalog.IsBury(cast.Rule.Action) && ihuyBury.Count > 0) ihuyBury.RemoveAt(0);
        var order = cast.Rule.Action switch { 36515 => 0, 36516 => 1, 36517 => 2, _ => -1 };
        if (order < 0) return;
        var index = ihuyShores.FindIndex(s => s.Stage == order
            && MathF.Abs(s.Position.X - cast.Location.X) <= 1 && MathF.Abs(s.Position.Z - cast.Location.Z) <= 1);
        if (index < 0) return;
        var sequence = ihuyShores[index];
        if (order == 2) ihuyShores.RemoveAt(index);
        else ihuyShores[index] = sequence with { Stage = order + 1, At = now.AddSeconds(2) };
    }

    private void IhuyEffect(uint action, ulong target, Vector3? location, DateTime now)
    {
        if (territory != IhuykatumuCatalog.TERRITORY) return;
        if (action == 36496 && ++ihuyInhales == 6) { ihuyInhale = null; ihuyInhales = 0; }
        if (action == 36512) ihuyBaits.Clear();
        if (action == 36360 && ihuyWinds.Count > 0) ihuyWinds.RemoveAt(0);
        if (action == 36349 && (_actors.GetValueOrDefault(target)?.Position ?? location) is { } p)
            ihuySparkPredictions.Add((p, now.AddSeconds(.7), ++ihuySerial));
    }

    private void IhuyTether(ulong source, ulong target, uint id, bool present, DateTime now)
    {
        if (territory != IhuykatumuCatalog.TERRITORY || Active?.BossOid != 0x4190 || id != 17) return;
        if (!_actors.TryGetValue(source, out var a) || !_actors.TryGetValue(target, out var b)) return;
        if (a.PartySlot >= 0 || a.Buddy) (a, b) = (b, a);
        if (a.Oid != 0x4192 || b.PartySlot < 0 && !b.Buddy) return;
        // Untether is deliberately ignored: the actual snapshot follows about 0.6s later.
        if (!present) { ihuyTetherPairs.Remove((a.Id, b.Id)); return; }
        if (!ihuyTetherPairs.Add((a.Id, b.Id))) return;
        if (ihuyBaits.Any(bait => bait.Source.Id == a.Id && bait.Target.Id == b.Id)) return;
        // The source subclass clears baits without resetting the base activation clock.
        if (ihuyTetherActivation == default) ihuyTetherActivation = now.AddSeconds(8.6);
        ihuyBaits.Add((a, b, ihuyTetherActivation, ++ihuySerial));
    }

    private void IhuyObserveActors()
    {
        if (territory != IhuykatumuCatalog.TERRITORY) return;
        for (var i = 0; i < ihuyBaits.Count; ++i)
        {
            var bait = ihuyBaits[i];
            ihuyBaits[i] = (_actors.GetValueOrDefault(bait.Source.Id) ?? bait.Source,
                _actors.GetValueOrDefault(bait.Target.Id) ?? bait.Target, bait.At, bait.Serial);
        }
    }

    private void SynchronizeIhuy()
    {
        if (territory != IhuykatumuCatalog.TERRITORY || Arena == null) return;
        var pairs = _actors.Values.Where(a => a.Tethers?.ContainsKey(17u) == true)
            .Select(a => (a.Id, Target: a.Tethers![17u])).ToArray();
        foreach (var pair in pairs) IhuyTether(pair.Id, pair.Target, 17, true, _now);
        ihuyTetherPairs.RemoveWhere(p => !pairs.Any(a => a.Id == p.Source && a.Target == p.Target)
            && !pairs.Any(a => a.Id == p.Target && a.Target == p.Source));
        foreach (var actor in _actors.Values)
        {
            if (!ihuyCreated.Add(actor.Id) || actor.Oid != 0x416C || Active?.BossOid != 0x4165) continue;
            var sw = MathF.Abs(actor.Position.X + 121) <= 1 && MathF.Abs(actor.Position.Z - 279) <= 1;
            Vector2[] points = sw ? [new(-102.935f, 274.357f), new(-108.935f, 262.224f), new(-105.733f, 252.340f)]
                : [new(-111.688f, 253.942f), new(-102.276f, 264.313f), new(-108.922f, 276.528f)];
            double[] delays = [8.6, 16.7, 24.7];
            float[] angles = [89.999f, 44.998f, 134.999f, -.003f];
            for (var i = 0; i < 3; ++i)
                foreach (var degrees in angles)
                    ihuyWinds.Add(new($"ihuy/cutting/{++ihuySerial}", "连续切风", new RectangleTemplate(8, 72),
                        QuantizedDungeonPosition(new(points[i].X, actor.Position.Y, points[i].Y)), degrees * MathF.PI / 180,
                        Activation: _now.AddSeconds(delays[i])));
        }
        ihuyCreated.RemoveWhere(id => !_actors.ContainsKey(id));
        foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1EBA21 && a.EventState != 7))
        {
            var index = ihuySparkPredictions.FindIndex(p => Vector2.DistanceSquared(new(p.Position.X, p.Position.Z), new(zone.Position.X, zone.Position.Z)) <= 4);
            if (index >= 0) ihuySparkPredictions.RemoveAt(index);
        }
    }

    private void ResetIhuy()
    {
        ihuyInhales = 0; ihuySerial = 0; ihuyTetherActivation = default; ihuyInhale = null;
        ihuyBury.Clear(); ihuyShores.Clear(); ihuyWinds.Clear(); ihuyWings.Clear(); ihuySparkPredictions.Clear();
        ihuyCreated.Clear(); ihuyTetherPairs.Clear(); ihuyBaits.Clear();
    }
}
