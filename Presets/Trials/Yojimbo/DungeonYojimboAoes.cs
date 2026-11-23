using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> YojimboAoes()
    {
        if (!IsYojimbo || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values.Where(a => a.Oid is 0x25FD or 0x25FF && a.Targetable && !a.Dead))
            yield return new($"yojimbo/enemy/{actor.Id}", "保镖及分身", new CircleTemplate(actor.HitboxRadius),
                actor.Position, Navigate: false, OutlineOnly: true);
        foreach (var aoe in yojimboBirthAoes)
            yield return new($"yojimbo/birth/{aoe.Key}/{aoe.Source}", "生成后爆炸", new CircleTemplate(8),
                aoe.Position, Activation: aoe.At);
        for (var i = 0; i < yojimboDonuts.Count; ++i)
        {
            var (cast, at) = yojimboDonuts[i];
            yield return new($"yojimbo/kasha/{i}", "花车月环", new DonutTemplate(2.9f, 10),
                cast.Location, cast.Rotation, Activation: at);
        }
        foreach (var line in yojimboDragons.Where(l => l.Left > 0))
            for (var i = line.Left - 1; i >= 0; --i)
            {
                var at = i == 0 ? line.At : (line.At > _now ? line.At : _now).AddSeconds(.6 * i);
                yield return new($"yojimbo/dragon/{line.Key}/{line.Source}/{i}", "龙头连续爆炸", RayRectangleTemplate.Symmetric(6, 3),
                    QuantizedDungeonPosition(line.Next + line.Step * i), line.Rotation, Activation: at,
                    Color: i > 0 ? new Vector4(1, .5f, 0, 1) : null);
            }
        foreach (var (source, cast) in yojimboCharges)
            yield return new($"yojimbo/charge/{source}", "正宗冲锋", cast.Rule.Shape!, cast.Location, cast.Rotation,
                Activation: cast.FinishAt);
        for (var i = 0; i < yojimboStorms.Count; ++i)
        {
            var aoe = yojimboStorms[i];
            var direction = new Vector3(MathF.Sin(aoe.Rotation), 0, MathF.Cos(aoe.Rotation));
            yield return new($"yojimbo/storm/{i}", aoe.Center ? "中央劈海斩" : "两侧劈海斩",
                RayRectangleTemplate.Symmetric(aoe.Center ? 4 : 20, 41), aoe.Position - direction,
                aoe.Rotation, Activation: aoe.At,
                Color: !aoe.Center && _now < yojimboStormAt ? new Vector4(1, .5f, 0, 1) : null);
        }
        foreach (var chain in yojimboChains)
            {
                var actor = chain.Target;
                yield return new($"yojimbo/chain/{chain.Key}/outline", "救援铁锁", new CircleTemplate(1.5f),
                    actor.Position, Navigate: false, OutlineOnly: true);
                if (!actor.Dead && actor.Targetable && !chain.Destroyed)
                    yield return new($"yojimbo/chain/{chain.Key}/actor", "铁锁", new CircleTemplate(actor.HitboxRadius),
                        actor.Position, Navigate: false, OutlineOnly: true);
            }
        if (yojimboEnchain.Contains(PlayerId) && _actors.GetValueOrDefault(PrimaryId) is { } boss)
            yield return new("yojimbo/enchain/avoid", "远离二十码准备束缚", new CircleTemplate(20),
                boss.Position, Display: false, Activation: DateTime.MaxValue);
        foreach (var aoe in YojimboCleaveAoes()) yield return aoe;
        foreach (var aoe in YojimboPartyAoes()) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> YojimboCleaveAoes()
    {
        foreach (var boss in _actors.Values.Where(a => a.Oid == 0x25FD && !a.Dead && a.Targetable))
        {
            if (!_actors.TryGetValue(boss.TargetId, out var target)) continue;
            var angle = MathF.Atan2(target.Position.X - boss.Position.X, target.Position.Z - boss.Position.Z);
            var position = QuantizedDungeonPosition(boss.Position);
            var shape = new FanTemplate(9.8f, MathF.PI / 2);
            yield return new($"yojimbo/cleave/{boss.Id}/display", "苦涩结局顺劈", shape, position, angle,
                Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new($"yojimbo/cleave/{boss.Id}/avoid", "避开顺劈", shape, position, angle, Display: false);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new($"yojimbo/cleave/{boss.Id}/buddy/{buddy.Id}", "顺劈远离队友",
                        new FanTemplate(100, MathF.PI / 2), position,
                        MathF.Atan2(buddy.Position.X - boss.Position.X, buddy.Position.Z - boss.Position.Z), Display: false);
        }
    }

    private IEnumerable<DungeonAmbientAoe> YojimboPartyAoes()
    {
        if (Arena == null) yield break;
        foreach (var mark in yojimboGekko)
        {
            if (mark.Destroyed) continue;
            var target = mark.Target;
            var id = target.Id;
            var shape = new CircleTemplate(5);
            var inside = _actors.GetValueOrDefault(PlayerId) is { } player && shape.Resolve(target.Position, 0, null).Contains(player.Position);
            var overlaps = yojimboGekko.Any(other => other.Target.Id != PlayerId && !other.Destroyed
                && !other.Target.Dead && shape.Resolve(target.Position, 0, null).Contains(other.Target.Position));
            yield return new($"yojimbo/gekko/{id}/display", "月光分散", shape, target.Position, Navigate: false);
            if (inside && overlaps && _now >= yojimboYukikazeEnd)
                yield return new($"yojimbo/gekko/{id}/avoid", "月光目标彼此分离", shape, target.Position,
                    Display: false, Activation: _now.AddSeconds(4.165));
        }
        var jumps = yojimboJumps.Where(j => !j.Target.Dead).ToArray();
        foreach (var jump in jumps)
        {
            var target = jump.Target;
            var position = QuantizedDungeonPosition(target.Position);
            yield return new($"yojimbo/jump/{jump.Key}/display", "巨型跳跃分散", new CircleTemplate(25), position,
                Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId || jump.Destroyed)
                yield return new($"yojimbo/jump/{jump.Key}/avoid", "避开巨型跳跃", new CircleTemplate(26), position,
                    Display: false, Activation: jump.At);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && !jumps.Any(j => j.Target.Id == a.Id && !j.Destroyed)))
                    yield return new($"yojimbo/jump/{jump.Key}/buddy/{buddy.Id}", "跳跃远离队友", new CircleTemplate(26),
                        QuantizedDungeonPosition(buddy.Position), Display: false, Activation: jump.At);
        }
        if (yojimboTiny.Any(m => m.Target.Id == PlayerId && !m.Destroyed))
        {
            var others = yojimboTiny.Where(m => m.Target.Id != PlayerId || m.Destroyed).Select(m => m.Target).ToArray();
            foreach (var target in others)
                yield return new($"yojimbo/tiny/{target.Id}/display", "缩小标记集合", new CircleTemplate(1), target.Position,
                    Navigate: false, Safe: true, OutlineOnly: true);
            if (others.Length > 0)
                yield return new("yojimbo/tiny/join", "与其他缩小标记集合", new SohrKhaiWallHintTemplate(Arena,
                    others.Select(a => (IGeometryShape)new CircleGeometry(a.Position, 1)).ToArray()),
                    Arena.Position, Display: false, Activation: yojimboTinyAt);
        }
    }

    private IEnumerable<DungeonDamagePrediction> YojimboDamage()
    {
        if (!IsYojimbo) yield break;
        var active = yojimboJumps.Where(j => !j.Target.Dead).ToArray();
        if (active.Length == 0) yield break;
        foreach (var actor in active.Select(j => j.Target).DistinctBy(a => a.Id))
            if (actor.PartySlot >= 0)
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, active.Min(j => j.At), "Raidwide");
    }

    private IEnumerable<string> YojimboHints()
    {
        if (!IsYojimbo || Active == null) yield break;
        if (yojimboGekko.Any(m => m.Target.Id == PlayerId && !m.Destroyed)) yield return "月光分散";
        if (_now < yojimboDragonHintEnd) yield return "龙头离场后全屏伤害";
        if (yojimboEnchain.Count > 0) yield return yojimboEnchain.Contains(PlayerId) ? "即将被束缚" : "准备解救被束缚队友";
        if (yojimboBound.Count > 0 && !yojimboBound.Contains(PlayerId)) yield return "击破铁锁解救队友";
        if (_casts.Values.Any(c => c.Rule.Action == 14404)) yield return "全队血量降至1";
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (yojimboTiny.Any(m => m.Target.Id == PlayerId && !m.Destroyed) && !yojimboTiny.Any(m => (m.Target.Id != PlayerId || m.Destroyed)
            && Vector2.Distance(new(m.Target.Position.X, m.Target.Position.Z),
                new(player.Position.X, player.Position.Z)) <= 1)) yield return "与其他缩小标记集合";
        if (YojimboCleaveAoes().Any(a => a.Id.EndsWith("/avoid") && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player.Position)))
            yield return "避开苦涩结局顺劈";
        foreach (var jump in yojimboJumps.Where(j => !j.Target.Dead))
        {
            var target = jump.Target;
            if (target.Id == PlayerId && !jump.Destroyed ? _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId
                    && Vector2.Distance(new(a.Position.X, a.Position.Z), new(player.Position.X, player.Position.Z)) <= 25)
                : Vector2.Distance(new(target.Position.X, target.Position.Z), new(player.Position.X, player.Position.Z)) <= 25)
                yield return "巨型跳跃分散";
        }
    }
}
