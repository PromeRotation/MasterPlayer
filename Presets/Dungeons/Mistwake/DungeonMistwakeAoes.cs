using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IGeometryShape[] MistLosShelters(Vector3 origin)
    {
        var result = new List<IGeometryShape>();
        foreach (var rock in mistRocks)
        {
            var d = rock.Position - origin;
            var distance = new Vector2(d.X, d.Z).Length();
            var half = rock.HitboxRadius < distance ? MathF.Asin(rock.HitboxRadius / distance) : MathF.PI / 2;
            var rotation = MathF.Atan2(d.X, d.Z);
            Vector2[] Arc(float radius, float start, float end)
            {
                var count = Math.Clamp(((int)MathF.Ceiling(MathF.Abs(end - start)
                    / (2 * MathF.Acos(1 - Math.Min(.05f / radius, 1)))) + 1) & ~1, 4, 512);
                return Enumerable.Range(0, count + 1).Select(i =>
                {
                    var angle = start + i * ((end - start) / count);
                    return -radius * new Vector2(MathF.Sin(angle), MathF.Cos(angle));
                }).ToArray();
            }
            result.Add(new LocalPolygonGeometry(origin, 0,
                Arc(30, rotation - half, rotation + half).Concat(Arc(distance + .2f, rotation + half, rotation - half))));
        }
        return result.ToArray();
    }

    private IEnumerable<DungeonAmbientAoe> MistAoes()
    {
        if (territory != MistwakeCatalog.TERRITORY || Arena == null) yield break;
        if (mistShrink != null) yield return mistShrink;
        if (mistStorm != null) yield return mistStorm;
        foreach (var a in mistCharges.Select(v => v.Aoe).Concat(mistBursts)) yield return a;
        foreach (var c in mistSimple)
        {
            if (c.Rule.Action == 45304 && mistSimple.Any(v => v.Rule.Action == 45302)) continue;
            yield return new($"mist/simple/{c.Source}/{c.StartedAt.Ticks}", c.Rule.Name, c.Rule.Shape!, c.Location, c.Rotation, Activation: c.FinishAt);
        }
        if (mistLos.Count > 0)
        {
            var los = mistLos[0];
            for (var i = 0; i < los.Shelters.Length; ++i)
                yield return new($"mist/los/display/{i}", "躲在岩石阴影内", new DungeonShapeTemplate(los.Shelters[i]),
                    los.Cast.Location, Navigate: false, Safe: true, Activation: los.Cast.FinishAt);
            // No blockers means no safezone in the source component.
            if (los.Shelters.Length > 0)
                yield return new("mist/los/avoid", "躲在岩石后", new SohrKhaiWallHintTemplate(Arena, los.Shelters),
                    Arena.Position, Display: false, Activation: los.Cast.FinishAt);
        }
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var spreads = mistParty.Where(v => v.Cast.Rule.PartyAoe == "分散" && !v.Target.Dead).ToArray();
        foreach (var (target, c) in spreads)
        {
            var p = QuantizedDungeonPosition(target.Position);
            var key = $"mist/spread/{c.Source}/{c.StartedAt.Ticks}";
            yield return new(key + "/display", c.Rule.Name, new CircleTemplate(5), p, Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new(key + "/avoid", "避开分散", new CircleTemplate(6), p, Display: false, Activation: c.FinishAt);
            else
            {
                foreach (var other in party.Where(a => a.Id != PlayerId && !spreads.Any(v => v.Target.Id == a.Id)))
                    yield return new(key + $"/separate/{other.Id}", "分散远离队友", new CircleTemplate(6),
                        QuantizedDungeonPosition(other.Position), Display: false, Activation: c.FinishAt);
                if (c.Rule.Action == 43333)
                    foreach (var rock in mistRocks.Where(r => !mistSimple.Any(v => v.Rule.Action == 43332
                        && v.Rule.Shape!.Resolve(v.Location, v.Rotation, null).Contains(r.Position, 0))))
                        foreach (var a in MistProtectRock(key, rock, 5, spreads[0].Cast.FinishAt)) yield return a;
            }
        }
        foreach (var (target, c) in mistParty.Where(v => v.Cast.Rule.PartyAoe == "分摊" && !v.Target.Dead))
        {
            var key = $"mist/stack/{c.Source}/{c.StartedAt.Ticks}";
            yield return new(key + "/display", "三连分摊", new CircleTemplate(6), QuantizedDungeonPosition(target.Position),
                Navigate: false, Safe: true, Activation: c.FinishAt);
            if (target.Id != PlayerId)
                yield return new(key + "/join", "进入三连分摊", new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(QuantizedDungeonPosition(target.Position), 6)]),
                    Arena.Position, Display: false, Activation: c.FinishAt);
            else
            {
                var buddies = party.Where(a => a.Id != PlayerId && !mistParty.Any(v => v.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(QuantizedDungeonPosition(a.Position), 3)).ToArray();
                if (buddies.Length > 0)
                    yield return new(key + "/gather", "分摊靠拢队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: c.FinishAt);
            }
        }
        foreach (var b in mistBusters.Where(v => !v.Source.Dead))
        {
            var key = $"mist/buster/{b.Cast.Source}/{b.Cast.StartedAt.Ticks}";
            var mine = b.Target.Id == PlayerId;
            yield return new(key + "/display", "ThunderIII", new CircleTemplate(4), b.Target.Position,
                Navigate: false, OutlineOnly: mine, Activation: b.Cast.FinishAt);
            if (!mine)
                yield return new(key + "/avoid", "避开死刑", new CircleTemplate(4), b.Target.Position, Display: false, Activation: b.Cast.FinishAt);
            else
            {
                foreach (var other in party.Where(a => a.Id != PlayerId))
                    yield return new(key + $"/away/{other.Id}", "死刑远离队友", new CircleTemplate(4), other.Position,
                        Display: false, Activation: b.Cast.FinishAt);
                foreach (var rock in mistRocks)
                    foreach (var a in MistProtectRock(key, rock, 4, mistBusters[0].Cast.FinishAt)) yield return a;
            }
        }
        foreach (var a in MistLineAoes(party)) yield return a;
        if (mistKbs.Count > 0 && _actors.TryGetValue(PlayerId, out var player))
        {
            if (!KnockbackImmune(player, mistKbs[0].FinishAt))
            {
                var kb = new MistwakeKnockbackTemplate(Arena.Position, mistAlongZ);
                yield return new("mist/kb/avoid", "击退落点保持在十九码内", kb, Arena.Position, Display: false, Activation: mistKbs[0].FinishAt);
            }
            var dest = player.Position;
            foreach (var c in mistKbs)
            {
                if (KnockbackImmune(player, c.FinishAt)) continue;
                foreach (var angle in new[] { c.Rotation + MathF.PI / 2, c.Rotation - MathF.PI / 2 })
                    if (RayRectangleTemplate.Symmetric(60, 30).Resolve(Arena.Position, angle, null).Contains(dest, 0))
                        dest += 12 * new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle));
            }
            if (dest != player.Position)
                foreach (var a in DungeonMovementPreview("mist/kb", "击退落点", player.Position, dest))
                    yield return a with { Color = Arena.Contains(dest, 0) ? new Vector4(.2f, 1, .3f, 1) : new Vector4(1, .1f, .1f, 1) };
        }
    }

    private static IEnumerable<DungeonAmbientAoe> MistProtectRock(string key, DungeonActor rock, float radius, DateTime at)
    {
        yield return new(key + $"/rock/{rock.Id}/avoid", "保护岩石", new CircleTemplate(rock.HitboxRadius + radius),
            rock.Position, Display: false, Activation: at);
        yield return new(key + $"/rock/{rock.Id}/outline", "岩石边缘", new CircleTemplate(rock.HitboxRadius),
            rock.Position, Navigate: false, OutlineOnly: true);
    }

    private IEnumerable<DungeonAmbientAoe> MistLineAoes(DungeonActor[] party)
    {
        if (Arena == null) yield break;
        var lines = mistLines.Where(v => !v.Source.Dead).ToArray();
        var marked = lines.Any(v => v.Target.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var l in lines)
        {
            var delta = l.Target.Position - l.Source.Position;
            var rotation = MathF.Atan2(delta.X, delta.Z);
            var shape = RayRectangleTemplate.Symmetric(5, 50);
            var key = $"mist/line/{l.Serial}";
            yield return new(key + "/display", "RayOfLightning", shape, l.Source.Position, rotation,
                Navigate: false, Safe: l.Target.Id == PlayerId || !marked, Activation: l.At);
            if (l.Target.Id == PlayerId)
            {
                var buddies = party.Where(a => !lines.Any(v => v.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (buddies.Length > 0)
                    yield return new(key + "/gather", "线分摊靠拢队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: l.At);
                var first = lines[0];
                foreach (var rock in mistRocks)
                {
                    var d = rock.Position - first.Source.Position;
                    var half = MathF.Asin((2.5f + rock.HitboxRadius) / new Vector2(d.X, d.Z).Length());
                    if (float.IsFinite(half))
                        yield return new(key + $"/rock/{rock.Id}/avoid", "线分摊避开岩石", new FanTemplate(100, 2 * half),
                            first.Source.Position, MathF.Atan2(d.X, d.Z), Display: false, Activation: first.At);
                    yield return new(key + $"/rock/{rock.Id}/outline", "岩石边缘", new CircleTemplate(rock.HitboxRadius),
                        rock.Position, Navigate: false, OutlineOnly: true);
                }
            }
            else if (!marked) joins.Add(shape.Resolve(l.Source.Position, rotation, null));
            else
                yield return new(key + "/avoid", "避免重叠线分摊", RayRectangleTemplate.Symmetric(10, 50),
                    l.Source.Position, rotation, Display: false, Activation: l.At);
        }
        if (joins.Count > 0)
            yield return new("mist/line/join", "进入一个线分摊", new DungeonExclusiveJoinTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: lines[0].At);
    }

    private IEnumerable<DungeonDamagePrediction> MistDamage()
    {
        if (territory != MistwakeCatalog.TERRITORY) yield break;
        foreach (var b in mistBusters.Where(v => !v.Source.Dead && v.Target.PartySlot >= 0))
            yield return new(b.Target.Id, b.Target.PartySlot, b.Target.PartyRole, b.Target.Name, b.Cast.FinishAt, "Tankbuster");
        var shared = mistLines.Where(v => !v.Source.Dead).Select(v => v.At).ToArray();
        foreach (var at in shared)
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, at, "Shared");
    }

    private IEnumerable<string> MistHints()
    {
        if (territory != MistwakeCatalog.TERRITORY) yield break;
        if (mistLos.Count > 0) yield return "躲在岩石后";
        if (mistBusters.Any(v => v.Target.Id == PlayerId) || mistLines.Any(v => v.Target.Id == PlayerId)) yield return "诱导避开岩石";
        if (mistParty.Any(v => v.Target.Id == PlayerId && v.Cast.Rule.Action == 43333)) yield return "分散避开安全岩石";
        if (mistParty.Any(v => v.Cast.Rule.Action == 45353)) yield return "三连分摊";
    }
}
