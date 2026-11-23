using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> HolminsterAoes()
    {
        if (territory != HolminsterCatalog.TERRITORY || Arena == null) yield break;
        foreach (var aoe in holminsterDissonanceCharges.Values) yield return aoe;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 15828))
            yield return new($"holminster/water/cast/{cast.Source}", "圣水落点", new CircleTemplate(6),
                _actors.GetValueOrDefault(cast.TargetId)?.Position ?? cast.Location, Activation: cast.FinishAt);
        foreach (var pool in holminsterWater) yield return pool;
        foreach (var pool in _actors.Values.Where(a => (Active?.BossOid == 0x278B && a.Oid == 0x1EABF9
            || Active?.BossOid == 0x278C && a.Oid == 0x1EABFA) && a.EventState != 7))
            yield return new($"holminster/pool/{pool.Id}", "持续池", new CircleTemplate(pool.Oid == 0x1EABF9 ? 6 : 9),
                QuantizedDungeonPosition(pool.Position));
        if (_actors.TryGetValue(PrimaryId, out var boss))
            foreach (var (id, activation) in holminsterCharges)
                if (_actors.TryGetValue(id, out var target))
                    foreach (var aoe in DomaLineBait($"holminster/flagellation/{id}", boss, target, 2,
                        Vector2.Distance(new(boss.Position.X, boss.Position.Z), new(target.Position.X, target.Position.Z))))
                        yield return aoe with { Activation = activation };
        foreach (var (id, activation) in holminsterStacks)
            if (_actors.TryGetValue(id, out var target))
            {
                yield return new($"holminster/stack/{id}/display", "四人分摊", new CircleTemplate(6),
                    target.Position, Navigate: false, Safe: true, Activation: activation);
                if (id != PlayerId)
                    yield return new($"holminster/stack/{id}/enter", "进入四人分摊", new SohrKhaiWallHintTemplate(Arena,
                        [new CircleGeometry(target.Position, 6)]), Arena.Position, Display: false, Activation: activation);
            }
        foreach (var flare in BurnSpreads(holminsterFlares, "holminster/flare", 20)) yield return flare;
        if (holminsterFlares.TryGetValue(PlayerId, out var flareAt))
            yield return new("holminster/flare/edge", "远离场中诱导跳跃", new CircleTemplate(18.5f),
                new(134, Arena.Position.Y, -465), Display: false, Activation: flareAt);
        if (HolminsterChain is { } chain)
        {
            yield return new("holminster/chain/display", "救援锁链", new CircleTemplate(3.6f), chain.Position, Navigate: false, Safe: true, OutlineOnly: true);
            yield return new("holminster/chain/enter", "接近并击破锁链", new SohrKhaiWallHintTemplate(Arena,
                [new CircleGeometry(chain.Position, 3.6f)]), Arena.Position, Display: false);
        }
        if (holminsterAethersup != null)
            yield return holminsterAethersup with { Navigate = HolminsterChain == null };
        if (holminsterLineStack is { } stack && _actors.TryGetValue(stack.Source, out var source)
            && _actors.TryGetValue(stack.Target, out var marked))
        {
            var angle = MathF.Atan2(marked.Position.X - source.Position.X, marked.Position.Z - source.Position.Z);
            var line = RayRectangleTemplate.Symmetric(8, 50);
            yield return new("holminster/line/display", "直线分摊", line, source.Position, angle, Navigate: false, Safe: true, Activation: stack.Activation);
            if (stack.Target != PlayerId)
                yield return new("holminster/line/enter", "进入直线分摊", new SohrKhaiWallHintTemplate(Arena,
                    [line.Resolve(source.Position, angle, null)]), Arena.Position, Display: false, Activation: stack.Activation);
        }
        if (holminsterCat != null)
            for (var i = 0; i < Math.Min(2, holminsterCatRemaining); ++i)
                yield return holminsterCat with { Id = $"holminster/cat/{i}", Rotation = holminsterCat.Rotation - i * MathF.PI / 4,
                    Activation = holminsterCat.Activation!.Value.AddSeconds(i * 2), OutlineOnly = i != 0 };
        foreach (var line in holminsterExas)
            for (var i = 0; i < Math.Min(3, line.Remaining); ++i)
                yield return new($"holminster/exa/{line.Id}/{i}", "七次连续地火", new CircleTemplate(4),
                    QuantizedDungeonPosition(line.Next + i * line.Advance), OutlineOnly: i != 0, Activation: line.Activation.AddSeconds(i));
        foreach (var (_, aoe) in holminsterExaPredictions) yield return aoe;
    }
}
