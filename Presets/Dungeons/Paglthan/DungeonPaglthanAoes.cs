using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> PaglthanAoes()
    {
        if (territory != PaglthanCatalog.TERRITORY || Arena == null) yield break;
        var ownLightning = paglthanLightning.ContainsKey(PlayerId);
        foreach (var (id, target) in paglthanLightning)
        {
            yield return new($"paglthan/lightning/{id}", "传雷点名", new CircleTemplate(10), target.Position,
                Navigate: id != PlayerId, OutlineOnly: id == PlayerId, Activation: paglthanLightningActivation);
            if (id == PlayerId)
                foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new($"paglthan/lightning/{id}/avoid/{other.Id}", "传雷避开队友", new CircleTemplate(10), other.Position,
                        Display: false, Activation: paglthanLightningActivation);
        }
        if (ownLightning)
        {
            var rods = _actors.Values.Where(a => a.Oid == 0x31B6 && !paglthanSeenLightning.GetValueOrDefault(a.Id)).ToArray();
            foreach (var rod in rods)
                yield return new($"paglthan/rod/{rod.Id}", "传雷给空闲避雷针", new CircleTemplate(4), rod.Position, Navigate: false, OutlineOnly: true, Safe: true);
            if (rods.Length > 0)
                yield return new("paglthan/rod/enter", "进入任一空闲避雷针4码内", new SohrKhaiWallHintTemplate(Arena,
                    rods.Select(a => (IGeometryShape)new CircleGeometry(a.Position, 4)).ToArray()), Arena.Position, Display: false, Activation: paglthanLightningActivation);
        }
        foreach (var aoe in paglthanShocks) yield return aoe;
        foreach (var aoe in paglthanBlasters) yield return aoe;
        foreach (var aoe in paglthanCannons) yield return aoe;
        foreach (var id in paglthanMissiles)
            if (_actors.TryGetValue(id, out var missile))
            {
                yield return new($"paglthan/missile/{id}/future", "导弹前方轨迹", new DungeonCapsuleTemplate(1, 10), missile.Position,
                    missile.Rotation, Activation: _now.AddSeconds(1.1));
                yield return new($"paglthan/missile/{id}/now", "导弹当前危险", new CircleTemplate(1), missile.Position, Display: false);
            }
        if (IsPaglthanFortress && paglthanPlatform && !paglthanCore)
        {
            var center = new Vector3(-175, Arena.Position.Y, 30);
            // The source's inverted zone is dated MaxValue. Publish an MP goal
            // so the transfer remains actionable while waiting for the core.
            yield return new("paglthan/platform", "核心平台传送圈", new CircleTemplate(2), center, Safe: true);
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 23703))
        {
            var center = new Vector3(-175, Arena.Position.Y, 43);
            var fan = new FanTemplate(20, MathF.PI / 5);
            yield return new($"paglthan/ballistic/{cast.Source}", "南北窄扇准备击退", new SohrKhaiWallHintTemplate(Arena,
                [fan.Resolve(center, 0, null), fan.Resolve(center, MathF.PI, null)]), Arena.Position, Display: false, Activation: cast.FinishAt);
            if (_actors.TryGetValue(PlayerId, out var player) && !KnockbackImmune(player, cast.FinishAt))
            {
                var offset = player.Position - cast.Location; offset.Y = 0;
                var displacement = offset.LengthSquared() > .000001f ? Vector3.Normalize(offset) * 10 : Vector3.Zero;
                foreach (var preview in DungeonMovementPreview($"paglthan/ballistic/{cast.Source}", "预计击退10码", player.Position, player.Position + displacement)) yield return preview;
            }
        }
        if (paglthanAkhMorn is { } stack && _actors.TryGetValue(stack.TargetId, out var targetStack))
        {
            yield return new("paglthan/akhmorn", "四次分摊", new CircleTemplate(4), targetStack.Position, Navigate: false, Safe: true);
            var regions = targetStack.Id == PlayerId
                ? _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId).Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray()
                : [new CircleGeometry(targetStack.Position, 4)];
            if (regions.Length > 0)
                yield return new("paglthan/akhmorn/enter", "保持四次分摊", new SohrKhaiWallHintTemplate(Arena, regions), Arena.Position, Display: false, Activation: stack.FinishAt);
        }
        foreach (var (id, bait) in paglthanBaits)
        {
            var own = id == PlayerId;
            yield return new($"paglthan/crossbait/{id}", "诱导后离开十字", new DungeonCrossTemplate(15, 3), bait.Target.Position,
                -.003f * MathF.PI / 180, Navigate: !own, OutlineOnly: own, Activation: bait.Activation);
            if (own)
                foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new($"paglthan/crossbait/{id}/avoid/{other.Id}", "十字不要覆盖队友", new DungeonCrossTemplate(15, 3),
                        other.Position, -.003f * MathF.PI / 180, Display: false, Activation: bait.Activation);
        }
        foreach (var aoe in paglthanCrosses) yield return aoe;
    }
}
