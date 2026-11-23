using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> GulgAoes()
    {
        if (territory != MtGulgCatalog.TERRITORY || Arena == null) yield break;
        foreach (var aoe in gulgDonuts.Values.Concat(gulgExegesis)) yield return aoe;
        if (gulgPenance != null) yield return gulgPenance;
        if (Active?.BossOid == 0x27CC)
        {
            var available = new List<IGeometryShape>();
            var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
            var activation = DateTime.MaxValue;
            foreach (var tower in gulgTowers)
            {
                var shape = new CircleGeometry(tower.Position, 5);
                yield return new($"gulg/tower/{tower.Id}", "一人塔", new CircleTemplate(5), tower.Position,
                    Navigate: false, Safe: true, OutlineOnly: true, Activation: tower.Activation);
                if (party.Any(a => a.Id != PlayerId && shape.Contains(a.Position)))
                    yield return new($"gulg/tower/avoid/{tower.Id}", "避开已有队友的塔", new CircleTemplate(5),
                        tower.Position, Display: false, Activation: tower.Activation);
                else { available.Add(shape); if (tower.Activation < activation) activation = tower.Activation; }
            }
            if (available.Count > 0)
                yield return new("gulg/tower/enter", "进入未满一人塔", new SohrKhaiWallHintTemplate(Arena, available.ToArray()),
                    Arena.Position, Display: false, Activation: activation);
        }
        if (Active?.BossOid != 0x27CE) yield break;
        foreach (var id in gulgOrbs)
        {
            if (!_actors.TryGetValue(id, out var orb)) continue;
            var forward = new Vector2(MathF.Sin(orb.Rotation), MathF.Cos(orb.Rotation));
            var ring = _actors.Values.FirstOrDefault(a =>
            {
                if (a.Oid != 0x1EAB62 || a.Dead) return false;
                var delta = new Vector2(a.Position.X - orb.Position.X, a.Position.Z - orb.Position.Z);
                return Vector2.Dot(delta, forward) is >= 0 and <= 20 && MathF.Abs(delta.X * forward.Y - delta.Y * forward.X) <= 3;
            });
            GeometryTemplate shape = ring == null ? new CircleTemplate(3)
                : new DungeonCapsuleTemplate(3, Vector2.Distance(new(orb.Position.X, orb.Position.Z), new(ring.Position.X, ring.Position.Z)));
            yield return new($"gulg/orb/{id}", "光球及光环通道", shape, orb.Position, orb.Rotation);
        }
        if (gulgRingsmith is not { } start) yield break;
        var staggered = gulgRings.Count >= 2 && GulgStaggeredRings(gulgRings[0].Position, gulgRings[1].Position);
        // A pair is forecast only when both rings exist. Later pairs retain their
        // original slots even after earlier effects resolve.
        var queued = new List<DungeonAmbientAoe>();
        for (var i = 0; i < gulgRings.Count / 2 * 2 && i < 6; ++i)
        {
            var ring = gulgRings[i];
            if (gulgResolvedRings.Contains(ring.Id)) continue;
            var delay = i switch { 0 => 7.1, 1 => staggered ? 7.6 : 7.1, 2 => 8.1, 3 => staggered ? 8.6 : 8.1, _ => staggered ? 9.1 : 11.1 };
            queued.Add(new($"gulg/gold/{ring.Id}", "六段光线", RayRectangleTemplate.Symmetric(5, 40),
                QuantizedDungeonPosition(ring.Position), MathF.PI, Activation: start.AddSeconds(delay)));
        }
        for (var i = 0; i < Math.Min(4, queued.Count); ++i)
            yield return queued[i] with { Navigate = queued[i].Activation == queued[0].Activation };
    }

    private static bool GulgStaggeredRings(Vector3 first, Vector3 second)
    {
        (Vector2 A, Vector2 B)[] sets = [(new(-227.5f, 253), new(-232.5f, 251.5f)), (new(-252.5f, 253), new(-247.5f, 251.5f))];
        var a = new Vector2(first.X, first.Z); var b = new Vector2(second.X, second.Z);
        return sets.Any(s => a == s.A && b == s.B || a == s.B && b == s.A);
    }
}
