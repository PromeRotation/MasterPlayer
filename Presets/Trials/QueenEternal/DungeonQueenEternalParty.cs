using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> QueenPartyAoes()
    {
        if (Arena == null || queenPrimary is not { Actor.Dead: false } source) yield break;
        var own = queenActors.GetValueOrDefault(PlayerId);
        var party = _actors.Values.Where(a => a.PartySlot is >= 0 and < 8 && !a.Dead).ToArray();
        foreach (var flare in queenFlare)
        {
            var target = flare.Target.Actor;
            var id = $"queen/flare/{flare.Key}";
            var marked = flare.Target == own;
            yield return new(id + "/display", "绝对权威核爆", new CircleTemplate(12), target.Position,
                Navigate: false, OutlineOnly: marked);
            if (!marked)
                yield return new(id + "/avoid", "避开核爆", new CircleTemplate(12), target.Position, Display: false, Activation: flare.At);
            else if (flare.Target != source)
                foreach (var buddy in party.Where(a => a.Id != PlayerId))
                    yield return new(id + $"/buddy/{buddy.Id}", "核爆远离队友", new CircleTemplate(12), buddy.Position,
                        Display: false, Activation: flare.At);
        }
        foreach (var line in queenLines)
        {
            var caster = source.Actor;
            var target = line.Target.Actor;
            var angle = MathF.Atan2(target.Position.X - caster.Position.X, target.Position.Z - caster.Position.Z);
            var id = $"queen/line/{line.Key}";
            var marked = line.Target == own;
            var shape = RayRectangleTemplate.Symmetric(5, 100);
            yield return new(id + "/display", "分而治之直线诱导", shape, caster.Position, angle, Navigate: false, OutlineOnly: marked);
            if (!marked)
                yield return new(id + "/avoid", "避开队友直线", shape, caster.Position, angle, Display: false, Activation: line.At);
            else
                foreach (var buddy in party.Where(a => a.Id != PlayerId))
                {
                    var delta = new Vector2(buddy.Position.X - caster.Position.X, buddy.Position.Z - caster.Position.Z);
                    var half = MathF.Asin(Math.Clamp(2.5f / delta.Length(), -1, 1));
                    if (!float.IsFinite(half)) continue;
                    yield return new(id + $"/buddy/{buddy.Id}", "诱导线远离队友", new FanTemplate(100, half * 2), caster.Position,
                        MathF.Atan2(delta.X, delta.Y), Display: false, Activation: line.At);
                }
        }
        if (queenDorito is not { Target.Actor.Dead: false } stack) yield break;
        var position = QuantizedDungeonPosition(stack.Target.Actor.Position);
        var occupants = party.Count(a => new CircleGeometry(position, 3).Contains(a.Position));
        yield return new("queen/dorito/display", "绝对权威八人分摊", new CircleTemplate(3), position,
            Navigate: false, OutlineOnly: true, Safe: stack.Target == own || occupants < 8
                || _actors.GetValueOrDefault(PlayerId) is { } player && new CircleGeometry(position, 3).Contains(player.Position));
        if (party.Length < 2) yield break;
        if (stack.Target == own)
        {
            var buddies = party.Where(a => a.Id != PlayerId).Select(a => (IGeometryShape)new CircleGeometry(a.Position, 1.5f)).ToArray();
            if (buddies.Length > 0)
                yield return new("queen/dorito/buddy", "分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                    Arena.Position, Display: false, Activation: stack.At);
        }
        else if (occupants < 8 || occupants == 8 && _actors.GetValueOrDefault(PlayerId) is { } participant
            && new CircleGeometry(position, 3).Contains(participant.Position))
            yield return new("queen/dorito/join", "进入安全队友分摊", new SohrKhaiWallHintTemplate(Arena,
                [new CircleGeometry(position, 3)]), Arena.Position, Display: false, Activation: stack.At);
        else
            yield return new("queen/dorito/avoid", "避开已满的分摊", new CircleTemplate(3), position,
                Display: false, Activation: stack.At);
    }

    private IEnumerable<DungeonDamagePrediction> QueenDamage()
    {
        if (!IsQueenEternal || Active == null || queenPrimary is not { Actor.Dead: false } || queenFlare.Count == 0) yield break;
        foreach (var flare in queenFlare.DistinctBy(f => f.Target))
            if (flare.Target.Actor is { PartySlot: >= 0 } target)
                yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, queenFlare[0].At, "Raidwide");
    }
}
