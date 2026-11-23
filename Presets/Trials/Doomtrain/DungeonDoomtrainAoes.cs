using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> DoomAoes()
    {
        if (!IsDoomtrain || Active == null || Arena == null) yield break;
        foreach (var aoe in DoomGeometryAoes()) yield return aoe;
        foreach (var cast in _casts.Values)
        {
            var component = cast.Rule.Action switch
            {
                45633 => DoomtrainComponent.Short, 45632 => DoomtrainComponent.Medium,
                45629 => DoomtrainComponent.Long, 45631 => DoomtrainComponent.Electray3,
                45627 => DoomtrainComponent.Blastpipe, 45659 => DoomtrainComponent.Hail,
                45643 => DoomtrainComponent.Surge, _ => (DoomtrainComponent?)null,
            };
            if (component == null || !DoomEnabled(component.Value)) continue;
            var remaining = (cast.FinishAt - _now).TotalSeconds;
            var risky = cast.Rule.Action switch
            {
                45631 => remaining <= 5, 45627 => remaining <= 3,
                45659 => remaining <= 3.2, 45643 => remaining <= 6, _ => true,
            };
            yield return new($"doom/cast/{cast.Source}", cast.Rule.Name, cast.Rule.Shape!, cast.Location, cast.Rotation,
                Navigate: risky, Activation: cast.FinishAt);
        }
        foreach (var levin in doomLevin)
        {
            // The source uses length zero to cull elevated signals away from
            // platforms; MP geometries reject degenerate rectangles.
            if (levin.Length <= 0) continue;
            yield return new($"doom/levin/{levin.Key}", "等离子光束预判", RayRectangleTemplate.Symmetric(5, levin.Length),
                levin.Actor.Actor.Position + new Vector3(0, 0, levin.Offset), Activation: levin.At);
        }
        foreach (var aoe in doomHeadOn.Concat(doomUpper).Concat(doomDerail)) yield return aoe;
        foreach (var aoe in DoomPartyAoes()) yield return aoe;
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (DoomEnabled(DoomtrainComponent.Lightning))
        {
            var from = player.Position;
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 45618))
            {
                if (WhorlImmune(player, cast.FinishAt)) continue;
                var displacement = 16 * new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation));
                yield return new($"doom/lightning/{cast.Source}/ai", "雷光快车击退出界",
                    new DoomtrainDisplacementTemplate(Arena, displacement), Arena.Position, Display: false, Activation: cast.FinishAt);
                foreach (var aoe in TrialKnockbackDrawing($"doom/lightning/{cast.Source}", "雷光快车击退轨迹", from, from + displacement))
                    yield return aoe;
                from += displacement;
            }
        }
        if (doomWindpipe != null && !WhorlImmune(player, _now))
            yield return new("doom/windpipe/ai", "躲在箱体后抵抗吸入",
                new DoomtrainDisplacementTemplate(Arena, new(0, 0, -20), doomCar == 2 ? 2 : 5),
                Arena.Position, Display: false, Activation: _now);
    }

    private IEnumerable<DungeonAmbientAoe> DoomPartyAoes()
    {
        if (Arena == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot is >= 0 and < 8 && !a.Dead).ToArray();
        var primaryAlive = doomActors.GetValueOrDefault(PrimaryId)?.Actor.Dead == false;
        if (primaryAlive)
        {
            foreach (var bait in doomTankBaits)
                foreach (var aoe in DoomBaitAoes($"doom/tank/{bait.Key}", "雷爆死刑分散",
                    doomActors[PrimaryId].Actor, bait.Target.Actor, new CircleTemplate(5), true, bait.At, party)) yield return aoe;
            foreach (var marker in doomArcane)
                foreach (var aoe in DoomBaitAoes($"doom/arcane/{marker.Actor.Id}", "秘能显现诱导",
                    doomActors[PrimaryId].Actor, marker.Actor, new CircleTemplate(16), true, default, party)) yield return aoe;
        }
        foreach (var ray in doomRays.Where(b => !b.Source.Actor.Dead))
            foreach (var aoe in DoomBaitAoes($"doom/ray/{ray.Key}", "以太射线诱导", ray.Source.Actor,
                ray.Target.Actor, new FanTemplate(50, 35 * MathF.PI / 180), false, ray.At, party)) yield return aoe;
        var stacks = doomStacks.Where(c => doomActors.GetValueOrDefault(c.TargetId)?.Actor.Dead == false).ToArray();
        var marked = stacks.Any(c => c.TargetId == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var stack in stacks)
        {
            var target = doomActors[stack.TargetId].Actor;
            yield return new($"doom/stack/{stack.Source}/display", "脱轨攻城：八人分摊", new CircleTemplate(5),
                QuantizedDungeonPosition(target.Position), Navigate: false, OutlineOnly: true, Safe: target.Id == PlayerId || !marked);
            if (party.Length < 2) continue;
            if (target.Id != PlayerId)
                joins.Add(new CircleGeometry(QuantizedDungeonPosition(target.Position), 5));
            else
            {
                var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(c => c.TargetId == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2.5f)).ToArray();
                if (buddies.Length > 0)
                    yield return new($"doom/stack/{stack.Source}/buddy", "分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: stack.FinishAt);
            }
        }
        if (joins.Count > 0)
            yield return new("doom/stack/join", "进入八人分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].FinishAt);
    }

    private IEnumerable<DungeonAmbientAoe> DoomBaitAoes(string id, string name, DungeonActor source, DungeonActor target,
        GeometryTemplate shape, bool centerAtTarget, DateTime at, DungeonActor[] party)
    {
        var own = target.Id == PlayerId;
        var origin = centerAtTarget ? target.Position : source.Position;
        var angle = target.Id == source.Id ? source.Rotation
            : MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
        yield return new(id + "/display", name, shape, origin, angle, Navigate: false, OutlineOnly: own);
        if (!own) yield return new(id + "/avoid", name, shape, origin, angle, Display: false, Activation: at == default ? null : at);
        else if (target.Id != source.Id)
            foreach (var buddy in party.Where(a => a.Id != PlayerId))
            {
                var direction = MathF.Atan2(buddy.Position.X - source.Position.X, buddy.Position.Z - source.Position.Z);
                yield return new(id + $"/buddy/{buddy.Id}", name, centerAtTarget ? shape : new FanTemplate(100, 35 * MathF.PI / 180),
                    centerAtTarget ? buddy.Position : source.Position, direction, Display: false, Activation: at == default ? null : at);
            }
    }

    private IEnumerable<DungeonDamagePrediction> DoomDamage()
    {
        if (!IsDoomtrain || Active == null) yield break;
        foreach (var cast in _casts.Values)
        {
            var component = cast.Rule.Action switch
            {
                45624 => DoomtrainComponent.Unlimited, 45638 => DoomtrainComponent.Runaway,
                45645 => DoomtrainComponent.RunawayRaidwide, 45647 => DoomtrainComponent.Shockwave,
                _ => (DoomtrainComponent?)null,
            };
            if (component == null || !DoomEnabled(component.Value)) continue;
            foreach (var actor in _actors.Values.Where(a => a.PartySlot is >= 0 and < 8 && !a.Dead))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, cast.FinishAt, "Raidwide");
        }
        if (doomActors.GetValueOrDefault(PrimaryId)?.Actor.Dead == false)
            foreach (var actor in doomTankBaits.Select(b => b.Target.Actor).DistinctBy(a => a.Id))
            {
                if (actor.PartySlot >= 0)
                    yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, doomTankBaits[0].At, "Tankbuster");
            }
        foreach (var actor in doomRays.Where(b => !b.Source.Actor.Dead).Select(b => b.Target.Actor).DistinctBy(a => a.Id))
        {
            if (actor.PartySlot >= 0)
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, doomRays[0].At, "Tankbuster");
        }
        // GenericStackSpread skips its Shared prediction when every slot is
        // eligible. This source stack has no forbidden players.
    }

    private IEnumerable<string> DoomHints()
    {
        if (!IsDoomtrain || Active == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (DoomAoes().Any(a => a.Navigate && !a.Obstacle && !a.Id.StartsWith("doom/stack/")
            && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player.Position)))
            yield return "离开机制危险区";
        foreach (var stack in doomStacks.Where(c => doomActors.GetValueOrDefault(c.TargetId)?.Actor.Dead == false))
        {
            var target = doomActors[stack.TargetId].Actor;
            var count = _actors.Values.Count(a => a.PartySlot is >= 0 and < 8 && !a.Dead
                && new CircleGeometry(QuantizedDungeonPosition(target.Position), 5).Contains(a.Position));
            var otherMarked = doomStacks.Any(c => c.TargetId != stack.TargetId && doomActors.GetValueOrDefault(c.TargetId) is { Actor.Dead: false } other
                && new CircleGeometry(QuantizedDungeonPosition(target.Position), 5).Contains(other.Actor.Position));
            if (otherMarked) yield return "八人分摊：避免重叠其他点名";
            else yield return count < 8 ? "八人分摊：人数不足" : "八人分摊：保持集合";
        }
        foreach (var bait in DoomPartyAoes().Where(a => a.Id.EndsWith("/display")))
        {
            var own = bait.OutlineOnly && !bait.Safe;
            var shape = bait.Shape.Resolve(bait.Position, bait.Rotation, null);
            if (own && _actors.Values.Any(a => a.PartySlot is >= 0 and < 8 && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position)))
                yield return "将点名诱导到队友外";
        }
    }
}
