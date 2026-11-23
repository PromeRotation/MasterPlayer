using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> ZoraalAoes()
    {
        if (!IsZoraalJa || Active == null || Arena == null) yield break;
        if (_actors.GetValueOrDefault(PrimaryId) is { Dead: false, Targetable: true } boss)
            yield return new("zoraal/enemy", boss.Name, new CircleTemplate(boss.HitboxRadius), boss.Position, Navigate: false, OutlineOnly: true);
        for (var i = 0; i < zoraalSwords.Count; ++i)
        {
            var cast = zoraalSwords[i];
            yield return new($"zoraal/sword/{cast.Source}/{cast.StartedAt.Ticks}", "连续双刃", cast.Rule.Shape!,
                cast.Location, cast.Rotation, Navigate: i == 0, Activation: cast.FinishAt,
                Color: zoraalSwordDanger.Contains((cast.Source, cast.StartedAt)) ? new Vector4(1, 0, 0, 1) : null);
        }
        foreach (var charge in zoraalCharges.Values) yield return charge;
        foreach (var chasm in zoraalChasm) yield return chasm;
        foreach (var track in zoraalTracks.Take(4)) yield return track;
        if (zoraalTransition != null) yield return zoraalTransition;
        if (zoraalChasm.Count == 0 && zoraalHalfFull.FirstOrDefault() is { } half)
            yield return new($"zoraal/half/{half.Source}", "半面斩", half.Rule.Shape!, half.Location, half.Rotation, Activation: half.FinishAt);
        foreach (var aoe in ZoraalPartyAoes()) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> ZoraalPartyAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var party = _actors.Values.Where(a => a.PartySlot is >= 0 and < 8 && !a.Dead).ToArray();
        foreach (var spread in zoraalSpreads)
        {
            var id = $"zoraal/spread/{spread.Key}";
            var target = spread.Target.Actor;
            yield return new(id + "/display", "火炎分散", new CircleTemplate(6), QuantizedDungeonPosition(target.Position),
                Navigate: false, OutlineOnly: true);
            if (party.Length < 2) continue;
            if (target.Id != PlayerId)
                yield return new(id + "/avoid", "远离分散目标", new CircleTemplate(7), QuantizedDungeonPosition(target.Position),
                    Display: false, Activation: spread.At);
            else
                foreach (var buddy in party.Where(a => !zoraalSpreads.Any(s => s.Target.Actor.Id == a.Id)))
                    yield return new(id + $"/buddy/{buddy.Id}", "分散远离队友", new CircleTemplate(7), QuantizedDungeonPosition(buddy.Position),
                        Display: false, Activation: spread.At);
        }
        var stacks = zoraalStacks.Where(s => !s.Source.Actor.Dead).ToArray();
        var marked = stacks.Any(s => s.Target.Actor.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var stack in stacks)
        {
            var id = $"zoraal/stack/{stack.Key}";
            var source = stack.Source.Actor;
            var target = stack.Target.Actor;
            var angle = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            var shape = RayRectangleTemplate.Symmetric(8, 100);
            var own = target.Id == PlayerId;
            yield return new(id + "/display", "四连直线分摊", shape, source.Position, angle,
                Navigate: false, Safe: own || !marked);
            if (own)
            {
                var buddies = party.Where(a => a.Id != PlayerId && !zoraalStacks.Any(s => s.Target.Actor.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (buddies.Length > 0)
                    yield return new(id + "/buddy", "分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies), Arena.Position,
                        Display: false, Activation: stack.At);
            }
            else if (!marked) joins.Add(shape.Resolve(source.Position, angle, null));
            else
                yield return new(id + "/avoid", "避免重叠其他分摊", RayRectangleTemplate.Symmetric(16, 100),
                    source.Position, angle, Display: false, Activation: stack.At);
        }
        if (joins.Count > 0)
            yield return new("zoraal/stack/join", "进入四连分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()), Arena.Position,
                Display: false, Activation: stacks[0].At);
    }

    private IEnumerable<DungeonDamagePrediction> ZoraalDamage()
    {
        if (!IsZoraalJa || Active == null) yield break;
        // The source registers SoulOverflow twice; retain its prediction multiplicity.
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 37707))
            foreach (var actor in _actors.Values.Where(a => a.PartySlot is >= 0 and < 8 && !a.Dead))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, cast.FinishAt, "Raidwide");
        foreach (var spread in zoraalSpreads)
        {
            var actor = spread.Target.Actor;
            if (actor.PartySlot >= 0)
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, zoraalSpreads[0].At, "Raidwide");
        }
        var stacks = zoraalStacks.Where(s => !s.Source.Actor.Dead).ToArray();
        if (stacks.Length > 0)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot is >= 0 and < 8 && !a.Dead))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, stacks.Min(s => s.At), "Shared");
    }

    private IEnumerable<string> ZoraalHints()
    {
        if (!IsZoraalJa || Active == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (ZoraalAoes().Any(a => a.Navigate && !a.Id.StartsWith("zoraal/spread/") && !a.Id.StartsWith("zoraal/stack/")
            && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player.Position)))
            yield return "离开机制危险区";
        var ownSpread = zoraalSpreads.Any(s => s.Target.Actor.Id == PlayerId);
        if (ownSpread && _actors.Values.Any(a => a.PartySlot is >= 0 and < 8 && !a.Dead
            && a.Id != PlayerId
            && Vector2.DistanceSquared(new(player.Position.X, player.Position.Z), new(a.Position.X, a.Position.Z)) < 36))
            yield return "分散远离队友";
        if (!ownSpread && zoraalSpreads.Any(s => Vector2.DistanceSquared(new(player.Position.X, player.Position.Z),
            new(s.Target.Actor.Position.X, s.Target.Actor.Position.Z)) < 36))
            yield return "离开分散范围";
        var stacks = zoraalStacks.Where(s => !s.Source.Actor.Dead).ToArray();
        if (stacks.Length == 0) yield break;
        var marked = stacks.Any(s => s.Target.Actor.Id == PlayerId);
        var inOther = stacks.Where(s => s.Target.Actor.Id != PlayerId).Any(s =>
        {
            var from = s.Source.Actor.Position;
            var to = s.Target.Actor.Position;
            return RayRectangleTemplate.Symmetric(8, 100).Resolve(from, MathF.Atan2(to.X - from.X, to.Z - from.Z), null).Contains(player.Position);
        });
        if (!marked && !inOther) yield return "进入四连直线分摊";
        else yield return "参与四连直线分摊";
        if (marked && inOther) yield return "避免重叠其他分摊";
    }
}
