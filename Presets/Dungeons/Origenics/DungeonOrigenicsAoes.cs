using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> OrigenicsAoes()
    {
        if (territory != OrigenicsCatalog.TERRITORY || Arena == null) yield break;
        if (origenicsShrink != null) yield return origenicsShrink;
        foreach (var cast in origenicsSimple)
        {
            var position = cast.Location;
            if (cast.Rule.Action == 36460 && _actors.TryGetValue(cast.Source, out var caster) && caster.Cast is { } current)
                position = _actors.GetValueOrDefault(current.TargetId)?.Position ?? current.Location;
            yield return new($"origenics/simple/{cast.Source}/{cast.StartedAt.Ticks}", cast.Rule.Name, cast.Rule.Shape!,
                position, cast.Rotation, Activation: cast.FinishAt);
        }
        foreach (var poison in origenicsPoison) yield return poison;
        foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1E9E3C && a.EventState != 7))
            yield return new($"origenics/zone/{zone.Id}", "地面毒池", new CircleTemplate(2), QuantizedDungeonPosition(zone.Position));
        if (origenicsRiot.Count > 0)
        {
            origenicsRiot[0] = origenicsRiot[0] with { Navigate = true,
                Color = origenicsRiot.Count > 1 ? new Vector4(1, 1, 0, 1) : origenicsRiot[0].Color };
            if (origenicsRiot.Count > 1 && origenicsRiot[0].Shape == origenicsRiot[1].Shape)
                origenicsRiot[1] = origenicsRiot[1] with { Navigate = false };
            foreach (var aoe in origenicsRiot.Take(2)) yield return aoe;
            if (origenicsRiot.Count > 1)
                yield return new("origenics/riot/close", "连续横扫贴近中心", new SohrKhaiWallHintTemplate(Arena,
                    [new CircleGeometry(origenicsRiot[0].Position, 3)]), Arena.Position, Display: false, Activation: origenicsRiot[0].Activation);
        }
        for (var i = 0; i < origenicsRush.Count; ++i)
        {
            if (i == 0 && origenicsRush.Count > 1) origenicsRush[i] = origenicsRush[i] with { Color = new Vector4(1, 1, 0, 1) };
            yield return origenicsRush[i];
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 37921 or 38320 or 36438
            && _actors.TryGetValue(c.TargetId, out var a) && !a.Dead))
            foreach (var aoe in ShisuiBait($"origenics/spread/{cast.Source}", cast.TargetId, 5, cast.FinishAt))
                yield return aoe with { Shape = aoe.Display ? aoe.Shape : new CircleTemplate(6), OutlineOnly = aoe.Display,
                    Position = QuantizedDungeonPosition(aoe.Position) };
        foreach (var aoe in OrigenicsLineAoes()) yield return aoe;
        foreach (var aoe in OrigenicsKnockbackAoes()) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> OrigenicsLineAoes()
    {
        if (Arena == null) yield break;
        var lines = origenicsLines.Where(l => !l.Source.Dead).ToArray();
        var marked = lines.Any(l => l.Target.Id == PlayerId);
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var join = new List<IGeometryShape>();
        foreach (var line in lines)
        {
            var delta = line.Target.Position - line.Source.Position;
            var angle = MathF.Atan2(delta.X, delta.Z);
            yield return new($"origenics/line/{line.Serial}/display", "直线分摊", RayRectangleTemplate.Symmetric(8, 50),
                line.Source.Position, angle, Navigate: false, Safe: line.Target.Id == PlayerId || !marked);
            if (line.Target.Id != PlayerId)
            {
                if (!marked) join.Add(RayRectangleTemplate.Symmetric(8, 50).Resolve(line.Source.Position, angle, null));
                else
                    yield return new($"origenics/line/{line.Serial}/avoid", "避免重叠直线分摊", RayRectangleTemplate.Symmetric(16, 50),
                        line.Source.Position, angle, Display: false, Activation: line.At);
            }
            else
            {
                var candidates = party.Where(a => !lines.Any(l => l.Target.Id == a.Id)).Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (candidates.Length > 0)
                    yield return new($"origenics/line/{line.Serial}/gather", "直线分摊点名靠拢队友", new SohrKhaiWallHintTemplate(Arena, candidates),
                        Arena.Position, Display: false, Activation: line.At);
            }
        }
        if (join.Count > 0)
            yield return new("origenics/line/join", "进入直线分摊", new DungeonExclusiveJoinTemplate(Arena, join.ToArray()),
                Arena.Position, Display: false, Activation: lines[0].At);
    }

    private IEnumerable<DungeonAmbientAoe> OrigenicsKnockbackAoes()
    {
        if (Arena == null) yield break;
        foreach (var hint in origenicsWallHints) yield return hint;
        if (origenicsKnockbacks.Count > 0)
        {
            var surge = origenicsKnockbacks[0].Action == 36367;
            var shelters = new List<IGeometryShape>();
            if (surge)
            {
                var walls = OrigenicsWalls();
                // Upstream passes an unnormalised direction of length four to SDInvertedRect.
                for (var i = 0; i < walls.Length; i += 2)
                    shelters.Add(RayRectangleTemplate.Symmetric(10, 2.5f).Resolve(
                        new(Arena.Position.X, Arena.Position.Y, walls[i].Y - 5), walls[i].X == -187.5f ? -MathF.PI / 2 : MathF.PI / 2, null));
            }
            else
                foreach (var kb in origenicsKnockbacks.Where(k => k.Action == 36433))
                    shelters.Add(RayRectangleTemplate.Symmetric(14, 19).Resolve(kb.Origin, kb.Rotation, null));
            // Missing full-map state has no defined upstream Surge AI; retain previews without indexing nonexistent walls.
            // The empty NS intersection for Ambrose is intentionally a full forbidden zone.
            if (!surge || shelters.Count > 0)
                yield return new("origenics/kb/nav", "击退前安全站位", new SohrKhaiWallHintTemplate(Arena, shelters.ToArray()),
                    Arena.Position, Display: false, Activation: origenicsKnockbacks[0].At);
        }
        var player = _actors.GetValueOrDefault(PlayerId);
        var active = origenicsKnockbacks.Count > 0 || OrigenicsPendingKnockback(PlayerId);
        if (Active?.BossOid == 0x417D)
        {
            var match = -1;
            if (active)
                match = origenicsKnockbacks.FindIndex(k => origenicsCharge != null
                    ? MathF.Abs(MathF.Atan2(MathF.Sin(origenicsCharge.Rotation - k.Rotation - MathF.PI),
                        MathF.Cos(origenicsCharge.Rotation - k.Rotation - MathF.PI))) <= MathF.PI / 180
                    : k.Action == 36433);
            if (match >= 0)
            {
                var kb = origenicsKnockbacks[match];
                yield return new("origenics/charge/safe", "击退安全位置", RayRectangleTemplate.Symmetric(14, 19),
                    kb.Origin, kb.Rotation, Navigate: false, Safe: true, Activation: kb.At);
            }
            if (!active && origenicsCharge != null) yield return origenicsCharge;
            if (origenicsKnockbacks.Count > 0 && origenicsCharge != null)
                yield return origenicsCharge with { Id = "origenics/charge/nav", Rotation = origenicsCharge.Rotation + MathF.PI, Display = false };
        }
        if (player == null) yield break;
        var start = player.Position;
        var count = 0;
        foreach (var kb in origenicsKnockbacks)
        {
            if (KnockbackImmune(player, kb.At)) continue;
            GeometryTemplate shape = kb.Action == 36367 ? new FanTemplate(60, MathF.PI)
                : RayRectangleTemplate.Symmetric(kb.Action == 36433 ? 15 : 20, kb.Action == 36433 ? 20 : 15);
            if (!shape.Resolve(kb.Origin, kb.Rotation, null).Contains(start, 0)) continue;
            var landing = OrigenicsLanding(start, kb.Rotation, kb.Action == 36367 ? 30 : 20, kb.Walls, player.HitboxRadius);
            foreach (var aoe in DungeonMovementPreview($"origenics/kb/{count}", "击退落点", start, landing))
            {
                var unsafeLanding = !Arena.Contains(landing, 0) || kb.Action != 36367 && origenicsCharge != null
                    && origenicsCharge.Shape.Resolve(origenicsCharge.Position, origenicsCharge.Rotation, null).Contains(landing, 0);
                yield return aoe with { Safe = !unsafeLanding };
            }
            start = landing;
            if (++count == 1 && kb.Action != 36367) break;
        }
    }

    private Vector3 OrigenicsLanding(Vector3 start, float rotation, float distance, Vector2[] walls, float hitbox)
    {
        if (Arena == null || walls.Length == 0) return start + new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation)) * distance;
        var direction = new Vector2(MathF.Sin(rotation), MathF.Cos(rotation));
        var origin = new Vector2(start.X, start.Z);
        float Intersection(Vector2 a, Vector2 b)
        {
            var segment = b - a;
            var det = direction.X * segment.Y - direction.Y * segment.X;
            if (MathF.Abs(det) < 1e-6f) return float.MaxValue;
            var delta = a - origin;
            var t = (delta.X * segment.Y - delta.Y * segment.X) / det;
            var u = (delta.X * direction.Y - delta.Y * direction.X) / det;
            return t >= 0 && u >= 0 && u <= 1 ? t : float.MaxValue;
        }
        var closest = float.MaxValue;
        for (var i = 0; i < walls.Length; i += 2)
        {
            var t = Intersection(walls[i], walls[i + 1]);
            if (t <= distance) closest = MathF.Min(closest, t);
        }
        if (closest < float.MaxValue)
        {
            var radius = hitbox < .499f ? .5f : hitbox;
            distance = MathF.Min(distance, closest - Math.Clamp(radius - .499f, .5f - .499f, radius - .499f));
        }
        else
        {
            var boundary = float.MaxValue;
            foreach (var contour in Arena.GetContours())
                for (var i = 0; i < contour.Count; ++i)
                    boundary = MathF.Min(boundary, Intersection(new(contour[i].X, contour[i].Z),
                        new(contour[(i + 1) % contour.Count].X, contour[(i + 1) % contour.Count].Z)));
            distance = MathF.Min(distance, boundary + (.5f - .499f));
        }
        return start + new Vector3(direction.X, 0, direction.Y) * distance;
    }

    private IEnumerable<DungeonDamagePrediction> OrigenicsDamage()
    {
        if (territory != OrigenicsCatalog.TERRITORY) yield break;
        foreach (var buster in origenicsBusters)
        {
            var a = _actors.GetValueOrDefault(buster.Target.Id) ?? buster.Target;
            if (a.PartySlot >= 0) yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, buster.At, "Tankbuster");
        }
        var lines = origenicsLines.Where(l => !l.Source.Dead).ToArray();
        if (lines.Length > 0)
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, lines.Min(l => l.At), "Shared");
    }

    private IEnumerable<string> OrigenicsHints()
    {
        if (territory != OrigenicsCatalog.TERRITORY) yield break;
        if (origenicsLines.Any(l => !l.Source.Dead)) yield return "直线分摊";
        if (origenicsWallHints.Count > 0 || origenicsKnockbacks.Any(k => k.Action != 36367))
            yield return "在安全位置等待击退";
    }
}
