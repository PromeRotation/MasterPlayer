using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> QueenAoes()
    {
        if (!IsQueenEternal || Active == null || Arena == null) yield break;
        if (queenPrimary is { Actor: { Dead: false, Targetable: true } boss })
            yield return new("queen/enemy", boss.Name, new CircleTemplate(boss.HitboxRadius), boss.Position, Navigate: false, OutlineOnly: true);
        foreach (var aoe in queenBesiege) yield return aoe;
        if (queenTithe != null) yield return queenTithe;
        if (queenBesiege.Count == 0 && queenForce.Count > 0)
        {
            var two = queenForce.Count > 1 && queenForce[0].Rotation != queenForce[1].Rotation;
            foreach (var aoe in queenForce.Take(two ? queenForce.Count : 1))
                yield return aoe with { Color = two && aoe == queenForce[0] ? new Vector4(1, 0, 0, 1) : null };
        }
        var count = queenForce.Count;
        if (count > 0 && queenArenaMode != "split" || queenBesiege.Count == 0 && count == 2 && queenGravity && queenArenaMode == "split")
            yield return new("queen/force/center", "正统之力中线准备", new SohrKhaiWallHintTemplate(Arena,
                [new RectangleGeometry(Arena.Position, 0, 6, 40)]), Arena.Position, Display: false, Activation: queenForce[0].Activation);
        else if (count != 2 && queenBesiege.Count == 0 && queenGravity && queenArenaMode == "split")
            yield return new("queen/force/islands", "重力结束前留在两侧平台", new SohrKhaiWallHintTemplate(Arena,
                [new RectangleGeometry(new(108, Arena.Position.Y, 94), 0, 8, 16),
                    new RectangleGeometry(new(92, Arena.Position.Y, 94), 0, 8, 16)]), Arena.Position,
                Display: false, Activation: queenForce.FirstOrDefault()?.Activation);
        foreach (var target in queenWaltz)
            yield return new($"queen/waltz/{target.Key}", "王权之舞提前预告", new CircleTemplate(MathF.Sqrt(212) * .5f),
                target.Actor.Actor.Position, Activation: target.At);
        if (queenRuthless is { } ruthless)
        {
            var caster = ruthless.Actor.Actor;
            yield return new("queen/ruthless/display", "无情王权", RayRectangleTemplate.Symmetric(12, 100),
                caster.Position, caster.Rotation, Navigate: false);
            var own = queenActors.GetValueOrDefault(PlayerId);
            if (queenTethered.Count > 1 && own != null && queenTethered.Contains(own))
            {
                var other = queenTethered.FirstOrDefault(t => t != own);
                if (other != null)
                    yield return new("queen/ruthless/tether", "连线保持分开", RayRectangleTemplate.Symmetric(24, 100),
                        new(caster.Position.X, caster.Position.Y, other.Actor.Position.Z), caster.Rotation * MathF.PI / 180,
                        Display: false);
            }
            else
                yield return new("queen/ruthless/avoid", "避开无情王权", RayRectangleTemplate.Symmetric(12, 100),
                    caster.Position, caster.Rotation, Display: false, Activation: ruthless.At);
        }
        if (queenBanishment.Count > 0)
        {
            var deadline = queenBanishment[0].FinishAt.AddSeconds(1);
            foreach (var cast in queenBanishment.TakeWhile(c => c.FinishAt < deadline))
                yield return new($"queen/banishment/{cast.Source}", "王权放逐当前波次", cast.Rule.Shape!,
                    cast.Location, cast.Rotation, Activation: cast.FinishAt);
        }
        foreach (var aoe in QueenPartyAoes()) yield return aoe;
        foreach (var aoe in QueenGazeDrawing()) yield return aoe;
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        foreach (var action in new[] { 36610u, 36612u })
        {
            var casts = queenKnockbacks.Where(c => c.Rule.Action == action).ToArray();
            if (casts.FirstOrDefault() is { } first && !WhorlImmune(player, first.FinishAt))
            {
                var polygon = action == 36610 ? QueenEternalArenas.XInsetHalf(Arena.Position.Y) : QueenEternalArenas.XInsetOne(Arena.Position.Y);
                yield return new($"queen/knockback/{action}/avoid", "击退前站位",
                    new QueenEternalKnockbackTemplate(polygon, Arena.Position, first.Location, action == 36612 ? first.Rotation : null),
                    Arena.Position, Display: false, Activation: first.FinishAt);
            }
            var from = player.Position;
            foreach (var cast in casts)
            {
                if (WhorlImmune(player, cast.FinishAt)) continue;
                var template = new QueenEternalKnockbackTemplate(Arena, Arena.Position, cast.Location, action == 36612 ? cast.Rotation : null);
                var to = template.Landing(from);
                var distance = Vector3.Distance(from, to);
                var wallDistance = QueenEternalKnockbackGeometry.FirstBoundaryDistance(Arena, from, to);
                if (wallDistance < distance && distance > 0) to = from + Vector3.Normalize(to - from) * wallDistance;
                foreach (var aoe in TrialKnockbackDrawing($"queen/knockback/{cast.Source}", "击退实际轨迹", from, to)) yield return aoe;
                from = to;
            }
        }
    }

    private IEnumerable<string> QueenHints()
    {
        if (!IsQueenEternal || Active == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (QueenRequireStop) yield return "权能之握：停手、停读条、停止自动攻击并保持不动";
        else if (queenStatuses.TryGetValue((PlayerId, 4130), out var holdAt) && holdAt <= _now.AddSeconds(3))
            yield return "权能之握即将判定：准备停止一切动作";
        if (queenFlare.Any(b => b.Target.Actor.Id == PlayerId)) yield return "核爆远离队友";
        if (queenLines.Count > 0) yield return "诱导直线远离队友";
        if (queenDorito is { Target.Actor.Dead: false } stack)
        {
            var position = QuantizedDungeonPosition(stack.Target.Actor.Position);
            var circle = new CircleGeometry(position, 3);
            var occupants = _actors.Values.Count(a => a.PartySlot is >= 0 and < 8 && !a.Dead && circle.Contains(a.Position));
            if (stack.Target.Actor.Id == PlayerId && occupants != 8) yield return $"八人分摊人数不足：{occupants}/8";
            else if (!circle.Contains(player.Position) && occupants < 8) yield return "靠近安全队友分摊";
        }
    }
}
