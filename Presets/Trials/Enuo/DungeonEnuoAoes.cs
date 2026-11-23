using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> EnuoAoes()
    {
        if (!IsEnuo || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values.Where(a => a.Oid is 0x4DB9 or 0x4DBE or 0x4DBC or 0x4DBD && a.Targetable && !a.Dead))
            yield return new($"enuo/enemy/{actor.Id}", actor.Name, new CircleTemplate(actor.HitboxRadius), actor.Position,
                Navigate: false, OutlineOnly: true);
        foreach (var cast in enuoCones.Take(7))
            yield return new($"enuo/cone/{cast.Source}", "虚空凝视扇形", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
        foreach (var chase in enuoChasers)
        {
            var position = EnuoChasePosition(chase);
            var id = $"enuo/chase/{chase.Key}";
            yield return new(id + "/display", "无尽追逐", new CircleTemplate(6), position, Navigate: false);
            var own = chase.Target == enuoActors.GetValueOrDefault(PlayerId);
            var player = _actors.GetValueOrDefault(PlayerId);
            var center = own && player != null ? position - new Vector3(MathF.Sin(player.Rotation), 0, MathF.Cos(player.Rotation)) * 6 : position;
            yield return new(id + "/avoid", "持续避开追踪圈", new CircleTemplate(own ? 8.9f : 7), center,
                Display: false, Activation: chase.Activation);
            var delta = chase.Target.Actor.Position - chase.Previous;
            var length = new Vector2(delta.X, delta.Z).Length();
            if (length > 0)
                yield return new(id + "/line", "追踪方向", RayRectangleTemplate.Symmetric(.1f, length),
                    chase.Previous, MathF.Atan2(delta.X, delta.Z), Navigate: false, OutlineOnly: true);
        }
        if (enuoLos.FirstOrDefault() is { Safe: not null } los)
        {
            yield return new($"enuo/los/{los.Key}/display", "躲在零式身后的安全影区", new DungeonLosTemplate(los.Safe),
                los.Safe.Position, Navigate: false, Safe: true);
            yield return new($"enuo/los/{los.Key}/avoid", "进入遮挡影区", new SohrKhaiWallHintTemplate(Arena, [los.Safe]),
                Arena.Position, Display: false, Activation: los.At);
        }
        foreach (var aoe in EnuoFreezeAoes()) yield return aoe;
        foreach (var aoe in EnuoPartyAoes()) yield return aoe;
        foreach (var aoe in EnuoLineAoes()) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> EnuoFreezeAoes()
    {
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var bait in enuoFreeze.Where(b => !b.Source.Actor.Dead))
        {
            var own = bait.Target == enuoActors.GetValueOrDefault(PlayerId);
            var id = $"enuo/freeze/{bait.Key}";
            yield return new(id + "/display", "深度冻结诱导", new CircleTemplate(8), bait.Target.Actor.Position,
                Navigate: false, OutlineOnly: own);
            if (!own)
                yield return new(id + "/avoid", "避开深度冻结", new CircleTemplate(8), bait.Target.Actor.Position,
                    Display: false, Activation: bait.At);
            else
                foreach (var buddy in party.Where(a => a.Id != PlayerId))
                    yield return new(id + $"/buddy/{buddy.Id}", "深度冻结远离队友", new CircleTemplate(8), buddy.Position,
                        Display: false, Activation: bait.At);
        }
    }

    private IEnumerable<DungeonDamagePrediction> EnuoDamage()
    {
        if (!IsEnuo || Active == null) yield break;
        if (enuoFreeze.Count > 0)
            foreach (var target in enuoFreeze.Where(b => !b.Source.Actor.Dead).Select(b => b.Target.Actor).DistinctBy(a => a.Id)
                .Where(a => a.PartySlot >= 0))
                yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, enuoFreeze[0].At, "Raidwide");
        var lines = enuoLines.Where(b => !b.Source.Actor.Dead).ToArray();
        if (lines.Length > 0)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, lines.Min(b => b.At), "Shared");
    }

    private IEnumerable<string> EnuoHints()
    {
        if (!IsEnuo || Active == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (enuoLos.FirstOrDefault() is { Safe: not null } los && !los.Safe.Contains(player.Position)
            && enuoLosOrigin is { } origin && Vector2.DistanceSquared(new(player.Position.X, player.Position.Z), new(origin.X, origin.Z)) < 1600)
            yield return "躲到零式身后";
        if (enuoChasers.Any(c => c.Target == enuoActors.GetValueOrDefault(PlayerId))) yield return "持续移动躲开追踪圈";
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var bait in enuoFreeze.Where(b => !b.Source.Actor.Dead))
            if (bait.Target == enuoActors.GetValueOrDefault(PlayerId)
                ? party.Any(a => a.Id != PlayerId && Inside(a.Position, bait.Target.Actor.Position, 8))
                : Inside(player.Position, bait.Target.Actor.Position, 8)) yield return "深度冻结远离队友";
        foreach (var spread in enuoSpreads.Where(s => !s.Target.Actor.Dead))
            if (spread.Target == enuoActors.GetValueOrDefault(PlayerId)
                ? party.Any(a => a.Id != PlayerId && Inside(a.Position, QuantizedDungeonPosition(spread.Target.Actor.Position), 5))
                : Inside(player.Position, QuantizedDungeonPosition(spread.Target.Actor.Position), 5)) yield return "熔毁分散";
        var stacks = enuoStacks.Where(s => !s.Target.Actor.Dead).ToArray();
        var ownStack = stacks.FirstOrDefault(s => s.Target == enuoActors.GetValueOrDefault(PlayerId));
        if (ownStack.Target != null)
        {
            if (party.Count(a => Inside(a.Position, QuantizedDungeonPosition(ownStack.Target.Actor.Position), 7)) < 4) yield return "分摊至少四人";
            if (stacks.Any(s => s.Target != ownStack.Target
                && Inside(s.Target.Actor.Position, QuantizedDungeonPosition(ownStack.Target.Actor.Position), 7))) yield return "分摊点名重叠";
        }
        else if (stacks.Length > 0 && !stacks.Any(s => Inside(player.Position, QuantizedDungeonPosition(s.Target.Actor.Position), 7))
            && stacks.Any(s => 1 + party.Count(a => enuoActors.GetValueOrDefault(a.Id) != s.Target
                && Inside(a.Position, QuantizedDungeonPosition(s.Target.Actor.Position), 7)) < 4))
            yield return "进入分摊";
        var lines = enuoLines.Where(l => !l.Source.Actor.Dead).ToArray();
        var ownLine = lines.Any(l => l.Target == enuoActors.GetValueOrDefault(PlayerId));
        bool InLine((EnuoActorReference Source, EnuoActorReference Target, DateTime At, int Key) line)
        {
            var source = line.Source.Actor; var target = line.Target.Actor;
            var angle = source.Id == target.Id ? source.Rotation
                : MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            return RayRectangleTemplate.Symmetric(8, 60).Resolve(source.Position, angle, null).Contains(player.Position);
        }
        if (!ownLine && lines.Length > 0 && !lines.Any(InLine)) yield return "进入直线分摊";
        if (ownLine && lines.Any(l => l.Target != enuoActors.GetValueOrDefault(PlayerId) && InLine(l))) yield return "离开其他直线分摊";
        // DimensionZero's minStackSize=8 is metadata only in GenericBaitStack; do not invent an eight-player count hint.
        static bool Inside(Vector3 a, Vector3 b, float r) => Vector2.DistanceSquared(new(a.X, a.Z), new(b.X, b.Z)) <= r * r;
    }
}
