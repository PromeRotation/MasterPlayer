using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> MesoAoes()
    {
        if (territory != MesoCatalog.TERRITORY || Arena == null) yield break;
        if (mesoShrink != null) yield return mesoShrink;
        foreach (var a in mesoSpheres.Concat(mesoBombs).Concat(mesoKeraunos).Concat(mesoSwordCache)) yield return a;
        if (mesoTurmoil != null) yield return mesoTurmoil;
        if (IsMesoHeadsmen && !mesoAssigned.ContainsKey(PlayerId))
            foreach (var a in mesoHellmakers) yield return a;
        var grouped = mesoSimple.Where(c => c.Rule.Action is 43587 or 43596 or 43592 or 43595).ToArray();
        var rays = mesoSimple.Where(c => c.Rule.Action == 43810).ToArray();
        foreach (var c in mesoSimple)
        {
            var group = c.Rule.Action == 43810 ? rays : c.Rule.Action is 43587 or 43596 or 43592 or 43595 ? grouped : [];
            if (group.Length > 0)
            {
                var visible = group.TakeWhile(a => a.FinishAt < group[0].FinishAt.AddSeconds(c.Rule.Action == 43810 ? 1 : 2));
                if (!visible.Contains(c)) continue;
            }
            yield return new($"meso/simple/{c.Source}/{c.StartedAt.Ticks}", c.Rule.Name, c.Rule.Shape!, c.Location, c.Rotation,
                Navigate: c.Rule.Action != 43818, Activation: c.FinishAt);
        }
        foreach (var sword in _actors.Values.Where(a => IsMesoHeadsmen && a.Oid == 0x4894 && !a.Dead && !mesoDestroyed.Contains(a.Id)))
            yield return new($"meso/obstacle/{sword.Id}", "SwordOfJustice", RayRectangleTemplate.Symmetric(4, 4),
                sword.Position, sword.Rotation, Display: false, Obstacle: true);
        if (_actors.TryGetValue(PlayerId, out var player))
        {
            var kb = mesoKnockbacks.FirstOrDefault();
            if (kb != null)
            {
                var impression = kb.Rule.Action == 43818;
                if (impression)
                    yield return new("meso/knockback/center", "击退前离开中心", new CircleTemplate(10),
                        QuantizedDungeonPosition(Arena.Position), Display: false, Activation: kb.FinishAt);
                if (!KnockbackImmune(player, kb.FinishAt))
                {
                    var circles = impression ? mesoBombs.Select(a =>
                        (IGeometryShape)new CircleGeometry(a.Position, ((CircleTemplate)a.Shape).Radius + 1)).ToArray() : [];
                    var landing = new VanaspatiKnockbackTemplate(new RectangleGeometry(Arena.Position, 0, 38, 38),
                        kb.Location, circles, impression ? 11 : 24);
                    yield return new("meso/knockback/avoid", "避免击退危险落点", landing, Arena.Position, Display: false, Activation: kb.FinishAt);
                    var dest = landing.Landing(player.Position);
                    var displayed = impression ? mesoBombs : mesoSpheres;
                    var unsafeLanding = !Arena.Contains(dest, 0) || displayed.Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(dest, 0));
                    foreach (var a in DungeonMovementPreview("meso/knockback", "击退落点", player.Position, dest))
                        yield return a with { Color = unsafeLanding ? new Vector4(1, .1f, .1f, 1) : new Vector4(.2f, 1, .3f, 1) };
                }
            }
            if (player.PartyRole is "Melee" or "melee")
                foreach (var a in _actors.Values.Where(a => !a.Dead && a.Targetable && MesoHellmaker(a)))
                    yield return new($"meso/hellmaker/goal/{a.Id}", "近战攻击Hellmaker", new CircleTemplate(3), a.Position,
                        Display: false, Safe: true, GoalPriority: 99);
        }
        foreach (var a in MesoLineAoes()) yield return a;
    }

    private IEnumerable<DungeonAmbientAoe> MesoLineAoes()
    {
        if (Arena == null) yield break;
        var lines = mesoLines.Where(l => !l.Source.Dead).ToArray();
        var marked = lines.Any(l => l.Target.Id == PlayerId);
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var joins = new List<IGeometryShape>();
        foreach (var l in lines)
        {
            var delta = l.Target.Position - l.Source.Position;
            var rotation = MathF.Atan2(delta.X, delta.Z);
            var shape = RayRectangleTemplate.Symmetric(6, 60);
            var key = $"meso/line/{l.Serial}";
            yield return new(key + "/display", "MemoryOfTheStorm", shape, l.Source.Position, rotation, Navigate: false,
                Safe: l.Target.Id == PlayerId || !marked, Activation: l.At);
            if (l.Target.Id == PlayerId)
            {
                var buddies = party.Where(a => !lines.Any(b => b.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (buddies.Length > 0)
                    yield return new(key + "/gather", "直线分摊目标靠拢队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: l.At);
            }
            else if (!marked) joins.Add(shape.Resolve(l.Source.Position, rotation, null));
            else
                yield return new(key + "/avoid", "避免重叠直线分摊", RayRectangleTemplate.Symmetric(12, 60),
                    l.Source.Position, rotation, Display: false, Activation: l.At);
        }
        if (joins.Count > 0)
            yield return new("meso/line/join", "进入一个直线分摊", new DungeonExclusiveJoinTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: lines[0].At);
    }

    private IEnumerable<DungeonDamagePrediction> MesoDamage()
    {
        if (territory != MesoCatalog.TERRITORY) yield break;
        foreach (var b in mesoBusters.Where(b => b.Target.PartySlot >= 0))
            yield return new(b.Target.Id, b.Target.PartySlot, b.Target.PartyRole, b.Target.Name, b.At, "Tankbuster");
        var lines = mesoLines.Where(l => !l.Source.Dead).ToArray();
        if (lines.Length > 0)
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, lines[0].At, "Shared");
    }

    private IEnumerable<string> MesoHints()
    {
        if (territory != MesoCatalog.TERRITORY) yield break;
        if (mesoAssigned.TryGetValue(PlayerId, out var id)) yield return $"攻击分配目标：{_actors.GetValueOrDefault(id)?.Name ?? id.ToString()}";
        if (mesoBusters.Count > 0) yield return "三连死刑";
        if (mesoLines.Count > 0) yield return "直线分摊";
        if (_actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && (HasStatus(a, 2104) || HasStatus(a, 4594))))
            yield return "驱散中毒 / 死亡宣告";
    }
}
