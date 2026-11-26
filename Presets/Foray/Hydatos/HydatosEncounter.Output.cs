using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hydatos;

internal sealed partial class HydatosEncounter
{
    internal int? TargetPriority(ReplayActorState actor)
    {
        if (Definition.Name == "ProvenanceWatcher") return null;
        if (actor.Oid == Definition.PrimaryOid
            || Definition.Name is "Phuabo" or "Tristitia" && actor.Oid is 0x2740 or 0x2741) return 1;
        if (Definition.Name == "Ceto" && actor.Oid == 0x2767
            || Definition.Name == "Molech" && actor.Oid == 0x275E) return -1;
        return actor.InCombat ? 0 : -3;
    }

    internal IGeometryShape NavigationBoundary(float y)
        => Definition.Name == "ProvenanceWatcher"
            ? new RectangleGeometry(NavigationCenter with { Y = y }, 0, 60, 60)
            : new CircleGeometry(ArenaCenter with { Y = y }, 30);

    internal HydatosActor[] Members(bool includeDead = false)
        => Party.Where(p => p.Slot is >= 0 and < 64).Select(p => Actors.GetValueOrDefault(p.EntityId))
            .OfType<HydatosActor>().Where(a => includeDead || !a.Actor.Dead).ToArray();

    internal IEnumerable<HydatosNavigation> Navigation(ReplayActorState player)
    {
        if (!Active || player.Dead) yield break;
        var viewer = Actors.GetValueOrDefault(player.Id);
        if (viewer == null || !Party.Any(p => p.EntityId == player.Id)) yield break;
        foreach (var aoe in VisibleAoes().Where(a => a.Risky && a.Shape != null && a.Rule.Kind is
            HydatosRuleKind.Aoe or HydatosRuleKind.Charge or HydatosRuleKind.IceAndLevin
            or HydatosRuleKind.Charybdis or HydatosRuleKind.HotTailSecond or HydatosRuleKind.DualCast))
            yield return Zone($"aoe/{aoe.Id}", aoe, aoe.Shape!, aoe.Origin, aoe.Rotation);
        var members = Members();
        foreach (var bait in Aoes.Where(a => a.Rule.Kind == HydatosRuleKind.Bait && !a.Source.Actor.Dead && a.Target != null))
        {
            if (!ReferenceEquals(bait.Target, viewer))
                yield return Zone($"bait/{bait.Id}", bait, bait.Shape!, bait.Target!.Actor.Position);
            else if (!ReferenceEquals(bait.Source, viewer))
                foreach (var buddy in members.Where(a => !ReferenceEquals(a, viewer)))
                    yield return Zone($"bait/{bait.Id}/avoid/{buddy.Actor.Id}", bait, bait.Shape!, buddy.Actor.Position);
        }
        var spreads = Aoes.Where(a => a.Rule.Kind == HydatosRuleKind.IonShower && a.Target?.Actor.Dead == false).ToArray();
        // The source's binary 0/100 goal for the marked player is not accepted
        // by the existing MP goal converter. Preserve it as a diagnostic.
        if (members.Length >= 2 && !spreads.Any(a => ReferenceEquals(a.Target, viewer)))
            foreach (var spread in spreads)
                yield return Zone($"spread/{spread.Id}", spread, new CircleTemplate(21), Quantized(spread.Target!.Actor.Position));
        foreach (var group in Aoes.Where(a => a.Rule.Kind is HydatosRuleKind.IconStack or HydatosRuleKind.AkhMorn
            && a.Target?.Actor.Dead == false).GroupBy(a => a.Rule.Component))
        {
            if (members.Length < 2) continue;
            var stacks = group.ToArray();
            var own = stacks.Any(a => ReferenceEquals(a.Target, viewer));
            foreach (var self in stacks.Where(a => ReferenceEquals(a.Target, viewer)))
            {
                var buddies = members.Where(a => !ReferenceEquals(a, viewer)
                    && !stacks.Any(s => ReferenceEquals(s.Target, a))).Select(a => a.Actor.Position).ToArray();
                if (buddies.Length > 0)
                    yield return Zone($"stack/{self.Id}/buddy", self,
                        new DungeonStackBuddyTemplate(NavigationBoundary(player.Position.Y), buddies, 3), default);
            }
            var join = new List<IGeometryShape>();
            foreach (var stack in stacks.Where(a => !ReferenceEquals(a.Target, viewer)))
            {
                var center = Quantized(stack.Target!.Actor.Position);
                var max = stack.Rule.Kind == HydatosRuleKind.IconStack ? 24 : int.MaxValue;
                var count = members.Count(a => Inside(a.Actor.Position, center, 6));
                if (count < max || Inside(player.Position, center, 6) && count <= max)
                    join.Add(new CircleGeometry(center, 6));
                else
                    yield return Zone($"stack/{stack.Id}/avoid", stack, new CircleTemplate(own ? 12 : 6), center);
            }
            if (join.Count > 0)
            {
                var boundary = NavigationBoundary(player.Position.Y);
                GeometryTemplate shape = join.Count == 1
                    ? new SohrKhaiWallHintTemplate(boundary, join.ToArray())
                    : new DungeonExclusiveJoinTemplate(boundary, join.ToArray());
                yield return Zone($"stack/{group.Key}/join", stacks[0], shape, default);
            }
        }
    }

    private static HydatosNavigation Zone(string key, HydatosAoe source, GeometryTemplate shape,
        Vector3 origin, float rotation = 0)
        => new(key, source.Rule.Component, shape, origin, rotation, source.Activation, source.StartedAt);

    internal IEnumerable<(HydatosAoe Aoe, Vector3 From, Vector3 To)> Pulls(ReplayActorState player)
    {
        var slot = Party.FirstOrDefault(p => p.EntityId == player.Id)?.Slot ?? -1;
        if (slot < 0) yield break;
        // Each component has its own movement chain. These two components never
        // coexist in the same source encounter.
        var from = player.Position;
        foreach (var pull in Aoes.Where(a => a.Rule.Kind is HydatosRuleKind.Pull or HydatosRuleKind.Petrattraction))
        {
            if (ImmuneAt(slot, pull.Activation) || !Inside(from, pull.Origin, pull.Rule.Range)) continue;
            var delta = Xz(pull.Origin - from);
            if (delta == Vector2.Zero) continue;
            var distance = MathF.Min(pull.Rule.Range, delta.Length() - pull.MinDistance);
            var direction = Vector2.Normalize(delta);
            var to = from + new Vector3(direction.X, 0, direction.Y) * distance;
            yield return (pull, from, to);
            from = to;
        }
    }

    internal HydatosAoe[] Gazes(ReplayActorState player)
        => Aoes.Where(a => a.Rule.Kind == HydatosRuleKind.Gaze && Inside(player.Position, a.Origin, a.Rule.Range)).ToArray();

    internal float? Facing(ReplayActorState player)
        => DungeonFacingDirections.Solve(Gazes(player).Where(a => a.Activation <= Now.AddSeconds(.75))
            .Select(a => (MathF.Atan2(a.Origin.X - player.Position.X, a.Origin.Z - player.Position.Z), MathF.PI / 4)).ToArray(),
            player.Rotation);

    internal IEnumerable<DungeonDamagePrediction> Damage()
    {
        if (!Active) yield break;
        foreach (var cast in Aoes.Where(a => a.Rule.Kind is HydatosRuleKind.Raidwide or HydatosRuleKind.Touchdown))
            foreach (var member in Party.Where(p => p.Slot is >= 0 and < 64))
                if (Actors.GetValueOrDefault(member.EntityId)?.Actor is { Dead: false } actor)
                    yield return new(actor.Id, member.Slot, member.Role, actor.Name,
                        cast.Rule.Kind == HydatosRuleKind.Raidwide ? Activation(cast.Source) : cast.Activation, "Raidwide");
        foreach (var cast in Aoes.Where(a => a.Rule.Kind == HydatosRuleKind.SingleTarget))
            if (cast.Source.Cast is { } spell)
            {
                var target = spell.TargetId == cast.Source.Actor.Id ? cast.Source.Actor.TargetId : spell.TargetId;
                if (Party.FirstOrDefault(p => p.EntityId == target) is { } member)
                    yield return new(target, member.Slot, member.Role, Actors.GetValueOrDefault(target)?.Actor.Name ?? "",
                        Activation(cast.Source), "Tankbuster");
            }
        var baits = Aoes.Where(a => a.Rule.Kind == HydatosRuleKind.Bait).ToArray();
        foreach (var id in baits.Where(a => !a.Source.Actor.Dead).Select(a => a.Target!.Actor.Id).Distinct())
            if (Party.FirstOrDefault(p => p.EntityId == id) is { } member)
                yield return new(id, member.Slot, member.Role, Actors.GetValueOrDefault(id)?.Actor.Name ?? "",
                    baits[0].Activation, "Raidwide");
        // Source GenericStackSpread with an all-zero ForbiddenPlayers mask
        // deliberately emits no Shared prediction.
    }

    internal IEnumerable<string> Hints(ReplayActorState player)
    {
        if (!Active) yield break;
        if (InputDiagnostic != null) yield return InputDiagnostic;
        if (Aoes.Any(a => a.Rule.Kind is HydatosRuleKind.Raidwide or HydatosRuleKind.Touchdown)) yield return "全队伤害";
        foreach (var dispel in DispelTargets)
            yield return $"可驱散：{dispel.Rule.Component} → {dispel.Actor.Actor.Name} ({dispel.Actor.Actor.Id:X})";
        if (Aoes.Any(a => a.Rule.Kind == HydatosRuleKind.SingleTarget)) yield return "单体重击";
        var forward = new Vector2(MathF.Sin(player.Rotation), MathF.Cos(player.Rotation));
        if (Gazes(player).Any(a => Xz(a.Origin - player.Position) is var delta && delta != Vector2.Zero
            && Vector2.Dot(Vector2.Normalize(delta), forward) >= .707107f)) yield return "背对视线";
        var members = Members();
        foreach (var bait in Aoes.Where(a => a.Rule.Kind == HydatosRuleKind.Bait && !a.Source.Actor.Dead && a.Target != null))
        {
            var target = bait.Target!;
            if (target.Actor.Id == player.Id && members.Any(a => a.Actor.Id != player.Id
                && Inside(a.Actor.Position, target.Actor.Position, 3))) yield return "将诱导范围远离队友";
            else if (target.Actor.Id != player.Id && Inside(player.Position, target.Actor.Position, 3)) yield return "离开他人的诱导范围";
        }
        var spreads = Aoes.Where(a => a.Rule.Kind == HydatosRuleKind.IonShower && a.Target?.Actor.Dead == false).ToArray();
        if (spreads.Any(a => a.Target!.Actor.Id == player.Id))
            yield return members.Any(a => a.Actor.Id != player.Id && Inside(a.Actor.Position, Quantized(player.Position), 20))
                ? "分散，远离队友" : "已分散";
        if (spreads.Any(a => a.Target!.Actor.Id != player.Id
            && Inside(player.Position, Quantized(a.Target.Actor.Position), 20))) yield return "离开他人的分散范围";
        if (Aoes.Any(a => a.Rule.Kind == HydatosRuleKind.IonShower && a.Target?.Actor.Id == player.Id && !a.Target.Actor.Dead))
            yield return "Ion Shower：远离首领（源二值目标仅诊断）";
        if (Navigation(player).Any(a => a.Key.StartsWith("aoe/") && DungeonSourceGeometry.Contains(a.Shape, a.Origin, a.Rotation, player.Position)))
            yield return "离开危险范围";
        if (Pulls(player).Any(p => !Inside(p.To, ArenaCenter, 30))) yield return "拉拽落点将离开显示场地";
        foreach (var group in Aoes.Where(a => a.Rule.Kind is HydatosRuleKind.IconStack or HydatosRuleKind.AkhMorn
            && a.Target?.Actor.Dead == false).GroupBy(a => a.Rule.Component))
        {
            var stacks = group.ToArray();
            var own = stacks.FirstOrDefault(a => a.Target!.Actor.Id == player.Id);
            if (own != null)
            {
                var count = 1 + members.Count(a => a.Actor.Id != player.Id && Inside(a.Actor.Position, Quantized(player.Position), 6));
                var min = own.Rule.Kind == HydatosRuleKind.IconStack ? 4 : 2;
                var max = own.Rule.Kind == HydatosRuleKind.IconStack ? 24 : int.MaxValue;
                var overlap = members.Any(a => a.Actor.Id != player.Id
                    && stacks.Any(s => ReferenceEquals(s.Target, a)) && Inside(a.Actor.Position, Quantized(player.Position), 6));
                yield return count < min || count > max || overlap ? $"集合分摊 ({count}/{min}–{max})" : "分摊人数已满足";
            }
            else
            {
                var participating = stacks.Count(a => Inside(player.Position, Quantized(a.Target!.Actor.Position), 6));
                var min = stacks[0].Rule.Kind == HydatosRuleKind.IconStack ? 4 : 2;
                var unsatisfied = stacks.Any(s => 1 + members.Count(a => !ReferenceEquals(a, s.Target)
                    && Inside(a.Actor.Position, Quantized(s.Target!.Actor.Position), 6)) < min);
                if (participating > 1 || participating == 0 && unsatisfied) yield return "集合分摊";
            }
        }
    }

    internal bool StackSafe(HydatosAoe stack, ReplayActorState? player)
    {
        if (player == null || stack.Target == null) return true;
        if (stack.Target.Actor.Id == player.Id) return true;
        var group = Aoes.Where(a => a.Rule.Component == stack.Rule.Component).ToArray();
        if (group.Any(a => a.Target?.Actor.Id == player.Id)) return false;
        var max = stack.Rule.Kind == HydatosRuleKind.IconStack ? 24 : int.MaxValue;
        var center = Quantized(stack.Target.Actor.Position);
        var inside = Inside(player.Position, center, 6);
        var count = Members().Count(a => Inside(a.Actor.Position, center, 6));
        return inside ? count <= max : count < max;
    }

    private static bool Inside(Vector3 point, Vector3 center, float radius)
        => Vector2.DistanceSquared(Xz(point), Xz(center)) <= radius * radius;
}
