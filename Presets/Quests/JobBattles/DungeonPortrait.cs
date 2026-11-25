using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private sealed record PortraitRing(ulong Id, ulong Source, DateTime Started, Vector3 Origin, int Order, DateTime Activation);
    private sealed class PortraitWater(ulong source, DateTime started, int side, Vector3 next, Vector3 advance, float rotation, DateTime activation)
    {
        internal readonly ulong Source = source;
        internal readonly DateTime Started = started;
        internal readonly int Side = side;
        internal Vector3 Next = next;
        internal Vector3 Advance = advance;
        internal float Rotation = rotation;
        internal DateTime Activation = activation;
        internal int Left = 5;
    }

    private bool IsPortrait => territory == JobQuestCatalog.SOMEWHERE_ONLY_SHE_KNOWS && Active?.BossOid == 0x4298;
    private readonly HashSet<ulong> portraitFlowers = [];
    private readonly HashSet<ulong> portraitFlowerEvents = [];
    private readonly Dictionary<ulong, DungeonPendingCast> portraitStarted = [];
    private readonly List<ulong> portraitIvyOrder = [];
    private readonly List<PortraitRing> portraitRings = [];
    private readonly List<PortraitWater> portraitWater = [];
    private readonly List<DungeonAmbientAoe> portraitWaterOutput = [];
    private DungeonAmbientAoe? portraitFlower;
    private ulong portraitFlowerSource;
    private ulong portraitRingId;

    private void PortraitObserve()
    {
        if (!IsPortrait) return;
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x4299))
            if (portraitFlowers.Add(actor.Id))
            {
                portraitFlowerSource = actor.Id;
                PortraitFlower(actor.Position, actor.Rotation);
            }
    }

    private void PortraitCreated(ulong source, uint oid, Vector3? position, float rotation)
    {
        if (!IsPortrait || oid != 0x4299 || position is not { } p) return;
        if (!portraitFlowerEvents.Add(source)) return;
        if (portraitFlowers.Add(source))
        {
            portraitFlowerSource = source;
            PortraitFlower(p, rotation);
        }
        else if (portraitFlowerSource == source && portraitFlower is { } observed)
        {
            PortraitFlower(p, rotation);
            if (portraitFlower != null) portraitFlower = portraitFlower with { Activation = observed.Activation };
        }
    }

    private void PortraitFlower(Vector3 position, float rotation)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z) || !float.IsFinite(rotation)) return;
        portraitFlower = new("portrait/flower", "Bloody Caress", new FanTemplate(60, MathF.PI), position, rotation,
            Activation: _now.AddSeconds(9.8));
    }

    private void PortraitCastStarted(DungeonPendingCast cast)
    {
        if (!IsPortrait) return;
        if (portraitStarted.TryGetValue(cast.Source, out var previous) && previous.Rule.Action == cast.Rule.Action
            && Math.Abs((previous.StartedAt - cast.StartedAt).TotalSeconds) < .25
            && Math.Abs((previous.FinishAt - cast.FinishAt).TotalSeconds) < .75)
        {
            CorrectPortraitCast(cast);
            portraitStarted[cast.Source] = cast;
            return;
        }
        portraitStarted[cast.Source] = cast;
        if (cast.Rule.Action == 37529 && !portraitIvyOrder.Contains(cast.Source))
            portraitIvyOrder.Add(cast.Source);
        if (cast.Rule.Action == 37531)
            portraitRings.Add(new(++portraitRingId, cast.Source, cast.StartedAt, cast.Location, 0, cast.FinishAt));
        if (cast.Rule.Action != 37535 || cast.SourcePosition is not { } position) return;
        var right = new Vector3(-MathF.Cos(cast.Rotation), 0, MathF.Sin(cast.Rotation));
        portraitWater.Add(new(cast.Source, cast.StartedAt, 1, position + right * 7.5f, right * 5, cast.Rotation, cast.FinishAt));
        portraitWater.Add(new(cast.Source, cast.StartedAt, -1, position - right * 7.5f, -right * 5, cast.Rotation + MathF.PI, cast.FinishAt));
        RebuildPortraitWater();
    }

    private void CorrectPortraitCast(DungeonPendingCast cast)
    {
        if (cast.Rule.Action == 37531)
        {
            var index = portraitRings.FindIndex(r => r.Source == cast.Source && Math.Abs((r.Started - cast.StartedAt).TotalSeconds) < .25);
            if (index >= 0) portraitRings[index] = portraitRings[index] with { Origin = cast.Location };
        }
        if (cast.Rule.Action != 37535 || cast.SourcePosition is not { } position) return;
        var changed = false;
        var right = new Vector3(-MathF.Cos(cast.Rotation), 0, MathF.Sin(cast.Rotation));
        foreach (var line in portraitWater.Where(l => l.Source == cast.Source && l.Left == 5
            && Math.Abs((l.Started - cast.StartedAt).TotalSeconds) < .25))
        {
            var next = position + right * (7.5f * line.Side);
            var rotation = cast.Rotation + (line.Side == 1 ? 0 : MathF.PI);
            if (line.Next == next && line.Rotation == rotation) continue;
            line.Next = next;
            line.Advance = right * (5 * line.Side);
            line.Rotation = rotation;
            changed = true;
        }
        if (changed) RebuildPortraitWater();
    }

    private void PortraitFinish(DungeonPendingCast cast)
    {
        if (IsPortrait) portraitStarted.Remove(cast.Source);
        if (IsPortrait && cast.Rule.Action == 37529) portraitIvyOrder.Remove(cast.Source);
        if (!IsPortrait || cast.Rule.Action is < 37531 or > 37533) return;
        var order = (int)cast.Rule.Action - 37531;
        var index = portraitRings.FindIndex(r => r.Order == order
            && MathF.Abs(r.Origin.X - cast.Location.X) <= 1 && MathF.Abs(r.Origin.Z - cast.Location.Z) <= 1);
        if (index < 0) return;
        var current = portraitRings[index];
        if (order == 2) portraitRings.RemoveAt(index);
        else portraitRings[index] = current with { Order = order + 1, Activation = _now.AddSeconds(2) };
    }

    private void PortraitEffect(ulong source, uint action, Vector3? sourcePosition)
    {
        if (!IsPortrait) return;
        if (action == 37527) portraitFlower = null;
        if (action != 37536) return;
        var position = sourcePosition ?? _actors.GetValueOrDefault(source)?.Position;
        if (position is not { } p) return;
        for (var i = 0; i < portraitWater.Count; ++i)
        {
            var line = portraitWater[i];
            if (MathF.Abs(line.Next.X - p.X) > 3 || MathF.Abs(line.Next.Z - p.Z) > 3) continue;
            var right = new Vector3(-MathF.Cos(line.Rotation), 0, MathF.Sin(line.Rotation));
            line.Next = p + right * 2.5f + line.Advance;
            line.Activation = _now.AddSeconds(2);
            if (--line.Left == 0) portraitWater.RemoveAt(i);
            RebuildPortraitWater();
            break;
        }
    }

    private void RebuildPortraitWater()
    {
        portraitWaterOutput.Clear();
        // The source caches predictions on line changes; passing time alone must not slide their timestamps.
        for (var i = 0; i < portraitWater.Count; ++i)
        {
            var line = portraitWater[i];
            if (line.Left > 1)
                portraitWaterOutput.Add(new($"portrait/water/{i}/next", "Flood in Blue", new RectangleTemplate(5, 50),
                    QuantizedDungeonPosition(line.Next + line.Advance), line.Rotation,
                    Activation: (line.Activation > _now ? line.Activation : _now).AddSeconds(2)));
        }
        for (var i = 0; i < portraitWater.Count; ++i)
        {
            var line = portraitWater[i];
            portraitWaterOutput.Add(new($"portrait/water/{i}/current", "Flood in Blue", new RectangleTemplate(5, 50),
                QuantizedDungeonPosition(line.Next), line.Rotation, Activation: line.Activation, Color: new Vector4(1, 1, 0, 1)));
        }
    }

    private IEnumerable<DungeonAmbientAoe> PortraitAoes()
    {
        if (!IsPortrait) yield break;
        if (portraitFlower != null) yield return portraitFlower;
        foreach (var ring in portraitRings)
        {
            GeometryTemplate shape = ring.Order switch
            {
                0 => new CircleTemplate(10),
                1 => new DonutTemplate(10, 20),
                _ => new DonutTemplate(20, 30),
            };
            yield return new($"portrait/ring/{ring.Id}", "Earthquake", shape, ring.Origin, Activation: ring.Activation);
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 37535))
            yield return new($"portrait/water/first/{cast.Source}", "Flood in Blue", cast.Rule.Shape!, cast.Location, cast.Rotation,
                Activation: cast.FinishAt, Color: new Vector4(1, 1, 0, 1));
        foreach (var water in portraitWaterOutput) yield return water;
    }

    private void ResetPortrait()
    {
        portraitFlowers.Clear();
        portraitFlowerEvents.Clear();
        portraitStarted.Clear();
        portraitIvyOrder.Clear();
        portraitRings.Clear();
        portraitWater.Clear();
        portraitWaterOutput.Clear();
        portraitFlower = null;
        portraitFlowerSource = 0;
        portraitRingId = 0;
    }

    private IEnumerable<string> PortraitHints(Vector3 player)
    {
        if (!IsPortrait) yield break;
        var hits = PortraitAoes().Where(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player)).ToArray();
        if (hits.Any(a => !a.Id.StartsWith("portrait/water/") || a.Id.StartsWith("portrait/water/first/")))
            yield return "GTFO from aoe!";
        if (hits.Any(a => a.Id.StartsWith("portrait/water/") && !a.Id.StartsWith("portrait/water/first/")))
            yield return "GTFO from exaflare!";
    }
}
