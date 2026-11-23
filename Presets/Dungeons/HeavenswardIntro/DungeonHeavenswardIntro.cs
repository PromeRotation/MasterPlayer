using System.Numerics;
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
        if (territory == HeavenswardIntroCatalog.REGULA && actor.Cast?.Action == 6032) return 5;
        return actor.AggroPlayer || actor.InCombat
            && _actors.GetValueOrDefault(actor.TargetId) is { } target && (target.PartySlot >= 0 || target.Buddy)
            ? 0 : -3;
    }

    private IEnumerable<DungeonAmbientAoe> HeavenswardIntroEnemies()
    {
        if (!IsHeavenswardIntro || Active == null || Arena == null) yield break;
        var marker = new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]);
        foreach (var actor in _actors.Values)
        {
            var visible = actor.Id == PrimaryId
                || territory == HeavenswardIntroCatalog.REGULA && actor.Oid is 0x161F or 0x1620;
            if (!visible || actor.Dead || !actor.Targetable) continue;
            var outside = !Arena.Contains(actor.Position);
            var position = actor.Position;
            var offset = position - Arena.Position;
            // Match ArenaBoundsCustom's center exception and closest-boundary marker.
            if (outside && (MathF.Abs(offset.X) > 1 || MathF.Abs(offset.Z) > 1) && MathF.Abs(offset.X) >= .1f)
            {
                var point = new Vector2(position.X, position.Z);
                var closest = Arena.GetContours().SelectMany(c => Enumerable.Range(0, c.Count).Select(i =>
                {
                    var a = new Vector2(c[i].X, c[i].Z);
                    var b = new Vector2(c[(i + 1) % c.Count].X, c[(i + 1) % c.Count].Z);
                    var edge = b - a;
                    var t = edge.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0, 1) : 0;
                    return a + edge * t;
                })).MinBy(p => Vector2.DistanceSquared(p, point));
                position = new(closest.X, position.Y, closest.Y);
            }
            yield return new($"hw-intro/enemy/{actor.Id}", "实体与朝向", marker,
                position, actor.Rotation, Navigate: false, OutlineOnly: outside, Unclipped: true,
                Color: new Vector4(1, 0, 0, 1));
        }
    }
}
