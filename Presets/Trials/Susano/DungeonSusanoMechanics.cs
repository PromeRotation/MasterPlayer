using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsSusano => territory == SusanoCatalog.TERRITORY;
    private readonly List<(DungeonActor Target, DateTime At, int Key)> susanoStacks = [];
    private DungeonActor? susanoTankTarget;
    private DateTime susanoTankAt;
    private int susanoStackKey;

    private void SusanoIcon(ulong actorId, uint icon, DateTime at)
    {
        if (!IsSusano || Active == null || !_actors.TryGetValue(actorId, out var actor)) return;
        if (icon == 62) susanoStacks.Add((actor, at.AddSeconds(8), ++susanoStackKey));
        if (icon == 230) { susanoTankTarget = actor; susanoTankAt = at.AddSeconds(5.1); }
    }

    private void SusanoObserve()
    {
        if (!IsSusano) return;
        for (var i = 0; i < susanoStacks.Count; ++i)
        {
            var stack = susanoStacks[i];
            if (_actors.TryGetValue(stack.Target.Id, out var actor)) susanoStacks[i] = (actor, stack.At, stack.Key);
        }
        if (susanoTankTarget != null && _actors.TryGetValue(susanoTankTarget.Id, out var target)) susanoTankTarget = target;
    }

    private void SusanoEffect(uint action, ulong target)
    {
        if (!IsSusano) return;
        if (action == 8227) susanoTankTarget = null;
        if (action == 8224 && susanoStacks.Count > 0)
        {
            var index = susanoStacks.FindIndex(s => s.Target.Id == target);
            susanoStacks.RemoveAt(index >= 0 ? index : 0);
        }
    }

    private IEnumerable<DungeonAmbientAoe> SusanoAoes()
    {
        if (!IsSusano || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values.Where(a => (a.Oid is 0x1AF8 or 0x1BA1 or 0x1C20 or 0x1C84 or 0x1EA479 or 0x1F53 or 0x1B9B)
            && (a.Oid == 0x1F53 || a.Targetable && !a.Dead)))
            yield return new($"susano/enemy/{actor.Id}", "须佐之男及辅助体", new CircleTemplate(actor.HitboxRadius),
                actor.Position, Navigate: false, OutlineOnly: true,
                Color: actor.Oid == 0x1F53 ? new Vector4(1, 128f / 255, 0, 1) : null);
        if (_actors.GetValueOrDefault(PrimaryId) is { Targetable: true } primary)
            yield return new("susano/primary", "须佐之男", new CircleTemplate(primary.HitboxRadius),
                Arena.Position - Vector3.UnitZ * 20, Navigate: false, OutlineOnly: true);
        foreach (var orb in _actors.Values.Where(a => a.Oid == 0x1B9B && !a.Dead))
            yield return new($"susano/orb/{orb.Id}", "副坦处理雷球", new CircleTemplate(1), orb.Position, Navigate: false);
        foreach (var aoe in SusanoStackAoes()) yield return aoe;
        if (susanoTankTarget is { } target && _actors.GetValueOrDefault(PrimaryId) is { } boss)
        {
            var rotation = MathF.Atan2(target.Position.X - boss.Position.X, target.Position.Z - boss.Position.Z);
            var shape = RayRectangleTemplate.Symmetric(4, 40);
            var tank = _actors.GetValueOrDefault(PlayerId) is { } player && ThornTank(player);
            yield return new("susano/tank/display", "双坦直线分摊", shape, boss.Position, rotation,
                Navigate: false, OutlineOnly: tank, Safe: tank && target.Id != PlayerId);
            if (target.Id != PlayerId)
                yield return new("susano/tank/position", tank ? "进入坦克直线分摊" : "远离坦克直线分摊",
                    tank ? new SohrKhaiWallHintTemplate(Arena, [shape.Resolve(boss.Position, rotation, null)]) : shape,
                    tank ? Arena.Position : boss.Position, tank ? 0 : rotation, Display: false, Activation: susanoTankAt);
        }
        foreach (var (source, from, to) in SusanoKnockbacks())
            foreach (var aoe in TrialKnockbackDrawing("susano/knockback/" + source, "八咫镜击退落点", from, to))
                yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> SusanoStackAoes()
    {
        if (Arena == null) yield break;
        var stacks = susanoStacks.Where(s => !s.Target.Dead).ToArray();
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var marked = stacks.Any(s => s.Target.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var stack in stacks)
        {
            var position = QuantizedDungeonPosition(stack.Target.Position);
            yield return new($"susano/stack/{stack.Key}/display", "至少两人分摊", new CircleTemplate(6), position,
                Navigate: false, OutlineOnly: true, Safe: stack.Target.Id == PlayerId || !marked);
            if (party.Length < 2) continue;
            if (stack.Target.Id == PlayerId)
            {
                var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(s => s.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                if (buddies.Length > 0)
                    yield return new($"susano/stack/{stack.Key}/buddy", "分摊点名靠拢队友",
                        new SohrKhaiWallHintTemplate(Arena, buddies), Arena.Position, Display: false, Activation: stack.At);
            }
            else
            {
                // The source's unlimited-capacity branch accepts another marked player's circle too.
                joins.Add(new CircleGeometry(position, 6));
            }
        }
        if (joins.Count > 0)
            yield return new("susano/stack/join", "进入图标分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].At);
    }

    private IEnumerable<(ulong Source, Vector3 From, Vector3 To)> SusanoKnockbacks()
    {
        if (!IsSusano || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var position = player.Position;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 8223))
        {
            if (WhorlImmune(player, cast.FinishAt)) continue;
            var delta = position - cast.Location;
            delta.Y = 0;
            if (delta.LengthSquared() == 0) continue;
            var next = position + Vector3.Normalize(delta) * 20;
            yield return (cast.Source, position, next);
            position = next;
        }
    }

    private IEnumerable<DungeonDamagePrediction> SusanoDamage()
    {
        if (!IsSusano) yield break;
        if (susanoTankTarget != null)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && ThornTank(a)))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, susanoTankAt, "Tankbuster");
    }

    private IEnumerable<string> SusanoHints()
    {
        if (!IsSusano || Active == null) yield break;
        if (_actors.Values.Any(a => a.Oid == 0x1B9B && !a.Dead)) yield return "副坦处理雷球";
        if (Arena != null && SusanoKnockbacks().Any(k => !Arena.Contains(k.To))) yield return "击退落点将出界";
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        foreach (var stack in susanoStacks.Where(s => !s.Target.Dead))
        {
            var circle = new CircleGeometry(QuantizedDungeonPosition(stack.Target.Position), 6);
            var inside = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && circle.Contains(a.Position));
            var overlapping = stack.Target.Id == PlayerId
                && susanoStacks.Any(s => s.Key != stack.Key && !s.Target.Dead && circle.Contains(s.Target.Position));
            if (overlapping || stack.Target.Id == PlayerId && inside < 2
                || stack.Target.Id != PlayerId && !circle.Contains(player.Position) && inside < 2)
                yield return "至少两人分摊";
        }
        if (susanoTankTarget is { } target)
        {
            if (target.Id == PlayerId) yield return "与另一坦克分摊或使用无敌";
            else if (ThornTank(player)) yield return "与坦克分摊";
            else yield return "远离坦克直线分摊";
        }
    }

    private void ResetSusano()
    {
        susanoStacks.Clear(); susanoTankTarget = null; susanoTankAt = default; susanoStackKey = 0;
    }
}
