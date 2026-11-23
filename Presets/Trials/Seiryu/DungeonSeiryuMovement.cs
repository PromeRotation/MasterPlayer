using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<(string Id, Vector3 From, Vector3 To)> SeiryuKnockbacks()
    {
        if (!IsSeiryu || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        foreach (var action in new[] { 14349u, 14346u })
        {
            var from = player.Position;
            foreach (var cast in seiryuKnockbackCasts.Where(c => c.Rule.Action == action))
            {
                if (action == 14346 && WhorlImmune(player, cast.FinishAt)) continue;
                var delta = action == 14349 ? new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation)) : from - cast.Location;
                delta.Y = 0;
                if (delta.LengthSquared() == 0) continue;
                var to = from + Vector3.Normalize(delta) * (action == 14349 ? 25 : 10);
                yield return ($"{action}/{cast.Source}", from, to);
                from = to;
            }
        }
    }

    private IEnumerable<DungeonAmbientAoe> SeiryuMovementAoes()
    {
        if (Arena == null) yield break;
        if (!_casts.Values.Any(c => c.Rule.Action == 14344) && seiryuKnockbackCasts.FirstOrDefault(c => c.Rule.Action == 14349) is { } river)
        {
            var offset = new Vector3(MathF.Sin(river.Rotation), 0, MathF.Cos(river.Rotation)) * 25;
            yield return new("seiryu/river/join", "流水击退后留在场内", new SohrKhaiWallHintTemplate(Arena,
                [new CircleGeometry(Arena.Position - offset, 19)]), Arena.Position, Display: false, Activation: river.FinishAt);
        }
        if (seiryuKnockbackCasts.FirstOrDefault(c => c.Rule.Action == 14346) is { } force)
            yield return new("seiryu/force/join", "进入中心十码", new SohrKhaiWallHintTemplate(Arena,
                [new CircleGeometry(Arena.Position, 10)]), Arena.Position, Display: false, Activation: force.FinishAt);
        foreach (var (id, from, to) in SeiryuKnockbacks())
            foreach (var aoe in TrialKnockbackDrawing("seiryu/knockback/" + id, "青龙击退落点", from, to)) yield return aoe;
    }

    private IEnumerable<string> SeiryuHints()
    {
        if (!IsSeiryu || Active == null || Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (SeiryuKnockbacks().Any(k => !Arena.Contains(k.To))) yield return "击退落点将出界";
        if (seiryuBaits.Any(b => b.Action == 14333)) yield return "圆形死刑";
        foreach (var bait in seiryuBaits.Where(b => !b.Source.Dead))
        {
            var position = bait.Action == 14333 ? bait.Target.Position : bait.Source.Position;
            var shape = SeiryuBaitShape(bait.Action).Resolve(position, SeiryuAngle(bait), null);
            if (SeiryuOwn(bait) ? _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position))
                : shape.Contains(player.Position)) yield return SeiryuOwn(bait) ? "诱导远离队友" : "远离诱导范围";
        }
        foreach (var action in new[] { 15388u, 15474u })
        {
            var lines = seiryuLines.Where(l => l.Action == action && !l.Source.Dead).ToArray();
            if (lines.Length == 0) continue;
            var marked = lines.Any(SeiryuOwn);
            var otherInside = lines.Where(l => !SeiryuOwn(l)).Where(l =>
                SeiryuLineShape(action).Resolve(l.Source.Position, SeiryuAngle(l), null).Contains(player.Position)).ToArray();
            if (otherInside.Any(l => SeiryuForbidden(l, player.PartySlot))) yield return "不能参与此直线分摊";
            else if (marked && otherInside.Length > 0) yield return "直线分摊点名互避";
            else if (!marked && otherInside.Length == 0 && lines.Any(l => !SeiryuForbidden(l, player.PartySlot))) yield return "进入直线分摊";
        }
        if (seiryuSpreads.Any(s => s.Target.Id == PlayerId && !s.Destroyed)) yield return "登龙分散";
        if (seiryuTowers.Count > 0)
        {
            var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
            var own = seiryuTowers.Where(t => new CircleGeometry(t.Position, 3).Contains(player.Position)).ToArray();
            if (own.Any(t => party.Count(a => new CircleGeometry(t.Position, 3).Contains(a.Position)) > 1)) yield return "单人塔超员";
            else if (own.Length == 0 && seiryuTowers.Any(t => !party.Any(a => new CircleGeometry(t.Position, 3).Contains(a.Position))))
                yield return "进入未满单人塔";
        }
    }
}
