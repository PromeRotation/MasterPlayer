using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> HadesAoes()
    {
        if (!IsHades || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values.Where(a => a.Hostile && !a.Dead && a.Targetable && !hadesWaitingRemoval.Contains(a.Id)))
            yield return new($"hades/enemy/{actor.Id}", "哈迪斯与敌人", new CircleTemplate(actor.HitboxRadius),
                actor.Position, Navigate: false, OutlineOnly: true);
        foreach (var aoe in hadesBrokenFaith) yield return aoe;
        if (HadesGaolAlive && HadesGaol is { } gaol)
            yield return new("hades/gaol/goal", "靠近以太牢狱", new CircleTemplate(6), gaol.Position + Vector3.UnitZ,
                Display: false, Safe: true, GoalPriority: 10);
        foreach (var bait in hadesBaits.Where(b => !b.Source.Dead))
        {
            var own = bait.Target.Id == PlayerId && !bait.TargetDestroyed;
            var cone = bait.Action == 16751;
            var radius = bait.Action switch { 16723 or 16755 => 6, 16745 => 8, _ => 5 };
            GeometryTemplate shape = cone ? new FanTemplate(100, MathF.PI / 3) : new CircleTemplate(radius);
            var angle = bait.Source.Id == bait.Target.Id ? bait.Source.Rotation
                : MathF.Atan2(bait.Target.Position.X - bait.Source.Position.X, bait.Target.Position.Z - bait.Source.Position.Z);
            var id = $"hades/bait/{bait.Action}/{bait.Key}";
            yield return new(id + "/display", "哈迪斯引导", shape, bait.Target.Position, angle,
                Navigate: false, OutlineOnly: own);
            if (!own)
                yield return new(id + "/avoid", "避开他人引导", shape, bait.Target.Position, angle,
                    Display: false, Activation: bait.At);
            else if (bait.Source.Id != bait.Target.Id)
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new(id + $"/buddy/{buddy.Id}", "引导远离队友", shape,
                        cone ? bait.Source.Position : buddy.Position,
                        cone ? MathF.Atan2(buddy.Position.X - bait.Source.Position.X, buddy.Position.Z - bait.Source.Position.Z) : 0,
                        Display: false, Activation: bait.At);
            if (bait.Action == 16755)
            {
                var length = Vector2.Distance(new(bait.Source.Position.X, bait.Source.Position.Z),
                    new(bait.Target.Position.X, bait.Target.Position.Z));
                yield return new(id + "/tether", "冥界冲击连线", RayRectangleTemplate.Symmetric(.06f, length),
                    bait.Source.Position, angle, Navigate: false, Unclipped: true, Color: new Vector4(1, 1, 1, 1));
            }
        }
        foreach (var aoe in HadesStackAoes()) yield return aoe;
        foreach (var aoe in HadesTowerAoes()) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> HadesTowerAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var joins = new List<IGeometryShape>();
        var avoids = new List<(int Key, Vector3 Position)>();
        var inCorrect = false;
        foreach (var tower in hadesTowers)
        {
            var shape = new CircleGeometry(tower.Position, 5);
            var inside = shape.Contains(player.Position);
            var count = party.Count(a => shape.Contains(a.Position));
            var correct = count is 1 or 2;
            yield return new($"hades/tower/{tower.Key}/display", "死亡宣告塔一至两人", new CircleTemplate(5), tower.Position,
                Navigate: false, Safe: count < 2 || inside && correct, OutlineOnly: count < 2 || inside);
            if (inside && correct) inCorrect = true;
            if (count < 2 || inside && correct) joins.Add(shape);
            else avoids.Add((tower.Key, tower.Position));
        }
        if (joins.Count > 0)
            yield return new("hades/tower/join", "进入死亡宣告塔", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()), Arena.Position, Display: false);
        else if (!inCorrect)
            foreach (var tower in avoids)
                yield return new($"hades/tower/{tower.Key}/avoid", "远离满员塔", new CircleTemplate(5), tower.Position, Display: false);
    }

    private IEnumerable<DungeonAmbientAoe> HadesStackAoes()
    {
        if (Arena == null) yield break;
        var stacks = hadesStacks.Where(s => !s.Target.Dead).ToArray();
        var marked = stacks.Any(s => s.Target.Id == PlayerId && !s.Destroyed);
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var joins = new List<IGeometryShape>();
        foreach (var stack in stacks)
        {
            var position = QuantizedDungeonPosition(stack.Target.Position);
            var shape = new CircleGeometry(position, 6);
            var own = stack.Target.Id == PlayerId && !stack.Destroyed;
            var inside = _actors.GetValueOrDefault(PlayerId) is { } player && shape.Contains(player.Position);
            var count = party.Count(a => shape.Contains(a.Position));
            var canJoin = inside ? count <= 8 : count < 8;
            var id = $"hades/stack/{stack.Key}";
            yield return new(id + "/display", "古代飞瀑分摊", new CircleTemplate(6), position,
                Navigate: false, OutlineOnly: true, Safe: own || !marked && canJoin);
            if (party.Length < 2) continue;
            if (own)
            {
                var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(s => s.Target.Id == a.Id && !s.Destroyed))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                if (buddies.Length > 0)
                    yield return new(id + "/buddy", "分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: stack.Cast.FinishAt);
            }
            else if (canJoin) joins.Add(shape);
            else
                yield return new(id + "/avoid", "避免分摊超员或点名重叠", new CircleTemplate(marked ? 12 : 6),
                    position, Display: false, Activation: stack.Cast.FinishAt);
        }
        if (joins.Count > 0)
            yield return new("hades/stack/join", "进入古代飞瀑分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].Cast.FinishAt);
    }
}
