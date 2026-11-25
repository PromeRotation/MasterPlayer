using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsHeavenswardIntro => HeavenswardIntroCatalog.IsTerritory(territory);
    private readonly List<ulong> regulaDetonating = [];

    private string? RegulaDetonationHint
    {
        get
        {
            if (territory != HeavenswardIntroCatalog.REGULA) return null;
            if (regulaDetonating.Count == 0) return null;
            var source = regulaDetonating[0];
            var cast = _casts.GetValueOrDefault(source);
            var observed = _actors.GetValueOrDefault(source)?.Cast;
            var finish = observed?.Action == 6032 ? observed.FinishAt : cast?.FinishAt ?? _now;
            return $"Kill turret before detonation! {(finish - _now).TotalSeconds:f1}s left";
        }
    }

    private int HeavenswardIntroPriority(DungeonActor actor)
    {
        if (IsGodmother && actor.Oid == 0x18E8) return 5;
        if (IsGodmother && GodmotherForbiddenTargets.Contains(actor.Id)) return -4;
        if (IsPalacePlant && actor.Oid == PalaceHornetOid) return 2;
        if (IsPalacePlant && actor.Oid == Definitions[0].BossOid) return 1;
        if (IsDivineIntervention) return actor.Oid == 0x102C ? 1 : 0;
        if (territory == HeavenswardIntroCatalog.TISIPHONE)
        {
            if (actor.Oid == 0x181C) return TisiphoneRanged ? 5 : 1;
            if (!TisiphoneRanged && actor.Oid == 0x18ED) return 5;
        }
        if (territory == HeavenswardIntroCatalog.ALDYNN && actor.Oid == 0x1552) return 5;
        if (territory == HeavenswardIntroCatalog.IXTAB && actor.Oid == 0x1764
            || territory == HeavenswardIntroCatalog.AH_PUCH && actor.Oid == 0x1906) return 1;
        if (territory == HeavenswardIntroCatalog.REGULA && actor.Cast?.Action == 6032) return 5;
        return actor.AggroPlayer || actor.InCombat
            && _actors.GetValueOrDefault(actor.TargetId) is { } target && (target.PartySlot >= 0 || target.Buddy)
            ? 0 : -3;
    }

    private IEnumerable<DungeonAmbientAoe> HeavenswardIntroEnemies()
    {
        if (!IsHeavenswardIntro || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values)
        {
            var visible = actor.Id == PrimaryId
                || territory == HeavenswardIntroCatalog.REGULA && actor.Oid is 0x161F or 0x1620
                || territory == HeavenswardIntroCatalog.IXTAB && actor.Oid == 0x1764
                || territory == HeavenswardIntroCatalog.AH_PUCH && actor.Oid == 0x1906;
            visible |= territory == HeavenswardIntroCatalog.TISIPHONE && actor.Oid is 0x18ED or 0x18EE;
            visible |= IsGodmother && actor.Oid == 0x18E8;
            visible |= IsPalacePlant && actor.Oid == PalaceHornetOid;
            visible |= IsNybeth && actor.Oid is 0x180A or 0x1809 or 0x180B;
            visible |= IsDivineIntervention && actor.Oid is 0x1010 or 0x1011 or 0x102C;
            var untargetableSword = territory == HeavenswardIntroCatalog.REGULA_VITH && actor.Oid == 0xF5D;
            visible |= untargetableSword || territory == HeavenswardIntroCatalog.ALDYNN && actor.Oid == 0x1552;
            if (!visible || actor.Dead || !actor.Targetable && !untargetableSword) continue;
            var color = new Vector4(1, 0, 0, 1);
            if (untargetableSword) color = new(1, 1, 0, 1);
            if (IsDivineIntervention && actor.Oid == 0x102C) color = new(1, 128f / 255, 0, 1);
            yield return HeavenswardIntroEnemyMarker(actor, Arena, $"hw-intro/enemy/{actor.Id}", color);
        }
    }

    private static DungeonAmbientAoe HeavenswardIntroEnemyMarker(DungeonActor actor, IGeometryShape arena, string id, Vector4 color)
    {
            var marker = new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]);
            var outside = !arena.Contains(actor.Position);
            var position = actor.Position;
            var offset = position - arena.Position;
            // Match ArenaBoundsCustom's center exception and closest-boundary marker.
            if (outside && arena is CircleGeometry circle)
            {
                var direction = Vector2.Normalize(new(offset.X, offset.Z)) * circle.Radius;
                position = new(circle.Position.X + direction.X, position.Y, circle.Position.Z + direction.Y);
            }
            else if (outside && (MathF.Abs(offset.X) > 1 || MathF.Abs(offset.Z) > 1) && MathF.Abs(offset.X) >= .1f)
            {
                var point = new Vector2(position.X, position.Z);
                var closest = arena.GetContours().SelectMany(c => Enumerable.Range(0, c.Count).Select(i =>
                {
                    var a = new Vector2(c[i].X, c[i].Z);
                    var b = new Vector2(c[(i + 1) % c.Count].X, c[(i + 1) % c.Count].Z);
                    var edge = b - a;
                    var t = edge.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0, 1) : 0;
                    return a + edge * t;
                })).MinBy(p => Vector2.DistanceSquared(p, point));
                position = new(closest.X, position.Y, closest.Y);
            }
            return new(id, "实体与朝向", marker,
                position, actor.Rotation, Navigate: false, OutlineOnly: outside, Unclipped: true,
                Color: color);
    }

    private IEnumerable<DungeonActor> PalacePoxSources => Active != null
        && territory is HeavenswardIntroCatalog.IXTAB or HeavenswardIntroCatalog.AH_PUCH
        ? _actors.Values.Where(a => a.Oid == 0x1E8EA9
            && (territory == HeavenswardIntroCatalog.IXTAB ? a.AnimationState1 == 1 : a.EventState != 7))
        : [];

    private IEnumerable<DungeonAmbientAoe> PalacePoxAreas()
    {
        foreach (var source in PalacePoxSources)
        {
            // BMR plans from quantized positions while drawing the actual object position.
            yield return new($"palace/pox/{source.Id}/nav", "Ancient Eruption", new CircleTemplate(4),
                QuantizedDungeonPosition(source.Position), Display: false);
            yield return new($"palace/pox/{source.Id}/display", "Ancient Eruption", new CircleTemplate(4),
                source.Position, Navigate: false);
        }
    }
}
