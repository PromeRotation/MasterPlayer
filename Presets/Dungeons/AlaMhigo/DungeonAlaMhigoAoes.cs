using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> AlaMhigoAoes()
    {
        if (territory != AlaMhigoCatalog.TERRITORY || Arena == null) yield break;
        if (Active?.BossOid == 0x1BA4)
        {
            foreach (var laser in alaLasers.Values) yield return laser;
            foreach (var target in _actors.Values.Where(a => a.Oid == 0x1BA5 && a.EventState == 0))
                yield return new($"ala/target/{target.Id}", "目标搜索", new CircleTemplate(5), target.Position);
            foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1EA66D && a.EventState != 7))
                yield return new($"ala/fire/{zone.Id}", "锁定火池", new CircleTemplate(5), zone.Position);
            foreach (var pool in alaFirePredictions) yield return pool;
        }
        if (Active?.BossOid == 0x1BA6)
        {
            foreach (var (source, body) in alaBodies.Where(p => p.Value.Target == PlayerId))
            {
                yield return new($"ala/body/{source}/display", "返回身体", new CircleTemplate(2), body.Position, Navigate: false, Safe: true, OutlineOnly: true);
                yield return new($"ala/body/{source}/enter", "进入自己的身体", new SohrKhaiWallHintTemplate(Arena,
                    [new CircleGeometry(body.Position, 2)]), Arena.Position, Display: false);
                if (_actors.TryGetValue(PlayerId, out var player))
                    foreach (var line in AlaMhigoLine($"ala/body/{source}/line", player.Position, body.Position, true)) yield return line;
            }
        }
        if (Active?.BossOid != 0x1BAA) yield break;
        if (!alaShrunk)
            yield return new("ala/shrink", "场地即将缩至20码", new DonutTemplate(20, 35), Arena.Position, Activation: alaShrinkAt);
        if (alaTroikaImminent && _actors.GetValueOrDefault(PrimaryId) is { Targetable: true, Dead: false } boss
            && _actors.TryGetValue(boss.TargetId, out var tank))
        {
            var angle = MathF.Atan2(tank.Position.X - boss.Position.X, tank.Position.Z - boss.Position.Z);
            yield return new("ala/troika/display", "不动三段首击", new FanTemplate(9.96f, MathF.PI * 2 / 3),
                QuantizedDungeonPosition(boss.Position), angle, Navigate: false, OutlineOnly: true);
            if (PlayerId != tank.Id)
                yield return new("ala/troika/avoid", "避开不动三段首击", new FanTemplate(9.96f, MathF.PI * 2 / 3),
                    QuantizedDungeonPosition(boss.Position), angle, Display: false);
            else
                foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"ala/troika/party/{other.Id}", "首击不要覆盖队友", new FanTemplate(100, MathF.PI * 2 / 3),
                        QuantizedDungeonPosition(boss.Position), MathF.Atan2(other.Position.X - boss.Position.X, other.Position.Z - boss.Position.Z), Display: false);
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 8293 or 9606))
        {
            yield return new($"ala/swell/{cast.Source}/center", "击退前进入中央5码", new SohrKhaiWallHintTemplate(Arena,
                [new CircleGeometry(Arena.Position, 5)]), Arena.Position, Display: false, Activation: cast.FinishAt);
            if (!_actors.TryGetValue(PlayerId, out var player)
                || player.StatusExpirations?.Any(s => s.Key is 160 or 1209 && s.Value > cast.FinishAt) == true) continue;
            var delta = player.Position - cast.Location;
            delta.Y = 0;
            if (delta.LengthSquared() <= .0001f) continue;
            var landing = player.Position + Vector3.Normalize(delta) * 15;
            var safe = Arena.Contains(landing);
            yield return new($"ala/swell/{cast.Source}/landing", "击退落点", new CircleTemplate(.4f), landing, Navigate: false, Safe: safe, OutlineOnly: true);
            foreach (var line in AlaMhigoLine($"ala/swell/{cast.Source}/line", player.Position, landing, safe)) yield return line;
        }
        foreach (var (key, activation) in alaSwordBaits)
        {
            if (!_actors.TryGetValue(key.Source, out var source) || !_actors.TryGetValue(key.Target, out var target)) continue;
            foreach (var aoe in DomaLineBait($"ala/sword/{key.Source}/{key.Target}", source, target, 3, 40.5f))
                yield return aoe with { Activation = activation };
        }
        var suppressSpark = _casts.Values.Any(c => c.Rule.Action == 8293);
        foreach (var (id, bait) in alaSparkBaits)
        {
            if (!_actors.TryGetValue(id, out var source) || !_actors.TryGetValue(bait.Target, out var target)) continue;
            var angle = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            yield return new($"ala/spark/{id}/display", "无明闪诱导", new FanTemplate(40.96f, MathF.PI / 2),
                source.Position, angle, Navigate: false, OutlineOnly: true, Activation: bait.Activation);
            foreach (var line in AlaMhigoLine($"ala/spark/{id}/line", source.Position, target.Position, false)) yield return line;
            if (suppressSpark) continue;
            if (PlayerId != target.Id)
                yield return new($"ala/spark/{id}/avoid", "避开无明闪", new FanTemplate(40.96f, MathF.PI / 2),
                    source.Position, angle, Display: false, Activation: bait.Activation);
            else
            {
                foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"ala/spark/{id}/party/{other.Id}", "无明闪不要覆盖队友", new FanTemplate(100, MathF.PI / 2),
                        source.Position, MathF.Atan2(other.Position.X - source.Position.X, other.Position.Z - source.Position.Z),
                        Display: false, Activation: bait.Activation);
                var outward = new FanTemplate(10, MathF.PI / 2).Resolve(source.Position,
                    MathF.Atan2(source.Position.X - Arena.Position.X, source.Position.Z - Arena.Position.Z), null);
                yield return new($"ala/spark/{id}/outward", "将无明闪引向场外", new SohrKhaiWallHintTemplate(Arena, [outward]),
                    Arena.Position, Display: false, Activation: _now.AddSeconds(8));
            }
        }
    }

    private static IEnumerable<DungeonAmbientAoe> AlaMhigoLine(string id, Vector3 from, Vector3 to, bool safe)
    {
        var delta = to - from;
        var length = new Vector2(delta.X, delta.Z).Length();
        if (length <= .001f) yield break;
        yield return new(id, "机制连线", RayRectangleTemplate.Symmetric(.1f, length), from,
            MathF.Atan2(delta.X, delta.Z), Navigate: false, Safe: safe);
    }
}
