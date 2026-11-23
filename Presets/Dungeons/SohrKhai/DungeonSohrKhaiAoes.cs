using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> SohrBait(string id, string name, DungeonActor source, ulong targetId,
        GeometryTemplate shape, DateTime? activation = null, float? halfWidth = null)
    {
        if (!_actors.TryGetValue(targetId, out var target) || target.Dead) yield break;
        var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
        yield return new(id + "/display", name, shape, source.Position, rotation, Navigate: false, OutlineOnly: true);
        if (targetId != PlayerId)
            yield return new(id + "/avoid", name, shape, source.Position, rotation, Display: false, Activation: activation);
        else
            foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
            {
                var angle = shape is FanTemplate fan ? fan.AngleRadians
                    : 2 * MathF.Asin(Math.Clamp((halfWidth ?? 3) / Math.Max(.001f,
                        Vector2.Distance(new(source.Position.X, source.Position.Z), new(buddy.Position.X, buddy.Position.Z))), 0, 1));
                yield return new(id + $"/party/{buddy.Id}", name + " / 避开队友", new FanTemplate(100, angle), source.Position,
                    MathF.Atan2(buddy.Position.X - source.Position.X, buddy.Position.Z - source.Position.Z), Display: false, Activation: activation);
            }
    }

    private IEnumerable<DungeonAmbientAoe> SohrAoes()
    {
        if (territory != SohrKhaiCatalog.TERRITORY || Active == null) yield break;
        if (Active.BossOid == 0x15FB)
        {
            if (sohrCross != null)
                yield return new("sohr/cross", "PomBom", sohrCross.Rule.Shape!, sohrCross.Location, Activation: sohrCross.FinishAt);
            if (sohrCleave && _actors.TryGetValue(PrimaryId, out var boss) && boss.Targetable)
                foreach (var aoe in SohrBait("sohr/cleave", "ThousandKuponzeCharge", boss, boss.TargetId, new FanTemplate(9.8f, MathF.PI * 2 / 3))) yield return aoe;
            if (SohrRelevantMoogles.Any())
                foreach (var (id, position) in sohrPraise)
                    yield return new($"sohr/praise/{id}", "将莫古力推出复活圈", new CircleTemplate(4.9f), position, Navigate: false, OutlineOnly: true, Safe: true);
        }
        if (Active.BossOid == 0x1604)
            foreach (var skipper in _actors.Values.Where(a => a.Oid == 0x1609 && !a.Dead && a.Targetable))
                foreach (var aoe in SohrBait($"sohr/venom/{skipper.Id}", "Venom", skipper, skipper.TargetId,
                    new FanTemplate(10.9f, MathF.PI * 2 / 3))) yield return aoe;
        if (Active.BossOid == 0x155C)
        {
            if (sohrCloud != null)
                yield return new("sohr/cloud", "LightningBolt", sohrCloud.Rule.Shape!, sohrCloud.Location, Activation: sohrCloud.FinishAt);
            foreach (var (id, activation) in sohrCloudBaits)
            {
                if (!_actors.TryGetValue(id, out var target)) continue;
                yield return new($"sohr/cloudbait/{id}", "CloudCall", new CircleTemplate(8), target.Position, Navigate: false, OutlineOnly: true);
                if (id != PlayerId)
                    yield return new($"sohr/cloudbait/{id}/avoid", "CloudCall", new CircleTemplate(8), target.Position, Display: false, Activation: activation);
                else
                {
                    yield return new("sohr/cloudbait/center", "将云诱导至边墙", new RectangleTemplate(10, 38),
                        new(400, target.Position.Y, 104.166f), Display: false);
                    foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                        yield return new($"sohr/cloudbait/party/{buddy.Id}", "云诱导避开队友", new CircleTemplate(8), buddy.Position,
                            Display: false, Activation: activation);
                }
            }
            var walls = _actors.Values.Where(a => a.Oid == 0x155E && !a.Dead).ToArray();
            if (sohrBright != null && _actors.TryGetValue(sohrBright.Source, out var source))
            {
                foreach (var aoe in SohrBait("sohr/bright", "BurningBright", source, sohrBright.TargetId,
                    RayRectangleTemplate.Symmetric(6, 28.5f), sohrBright.FinishAt, 3)) yield return aoe;
                if (sohrBright.TargetId == PlayerId && walls.Length is > 0 and <= 4)
                    foreach (var wall in walls)
                    {
                        var distance = Vector2.Distance(new(source.Position.X, source.Position.Z), new(wall.Position.X, wall.Position.Z));
                        yield return new($"sohr/bright/wall/{wall.Id}", "直线避开墙", new FanTemplate(100,
                            2 * MathF.Asin(Math.Clamp(8 / Math.Max(.001f, distance), 0, 1))), source.Position,
                            MathF.Atan2(wall.Position.X - source.Position.X, wall.Position.Z - source.Position.Z), Display: false, Activation: sohrBright.FinishAt);
                    }
            }
            if (sohrCloudBaits.ContainsKey(PlayerId) || sohrBright?.TargetId == PlayerId)
                foreach (var wall in walls)
                    yield return new($"sohr/wall/{wall.Id}", "避免破坏墙体", new CircleTemplate(wall.HitboxRadius), wall.Position, Navigate: false, OutlineOnly: true);
            foreach (var cast in sohrKnockbacks.Values)
            {
                if (Arena == null) continue;
                // All active sources disappear together when any Gallop cast finishes.
                yield return new($"sohr/knockback/{cast.Source}", "击退落点不可通行",
                    new SohrKhaiKnockbackTemplate(Arena, cast.Location, cast.Rotation, sohrRemovedWalls), cast.Location,
                    Display: false, Activation: cast.FinishAt);
                if (_actors.TryGetValue(PlayerId, out var player)
                    && RayRectangleTemplate.Symmetric(40, 4.5f).Resolve(cast.Location, cast.Rotation, null).Contains(player.Position))
                    foreach (var aoe in DungeonMovementPreview($"sohr/knockback/{cast.Source}", "向前击退30码",
                        player.Position, SohrKhaiPoqhirajArena.Landing(player.Position, cast.Rotation, sohrRemovedWalls))) yield return aoe;
            }
            for (var i = 0; i < sohrKnockbackGoals.Count; ++i)
            {
                var goal = sohrKnockbackGoals[i];
                yield return new($"sohr/knockback/goal/{i}", "利用完整墙段挡击退", new RectangleTemplate(10, 4.5f),
                    goal.Position, goal.Rotation, Navigate: false, Safe: true);
            }
            if (sohrKnockbackGoals.Count > 0 && Arena != null)
                yield return new("sohr/knockback/shelter", "击退前进入完整墙段后", new SohrKhaiWallHintTemplate(Arena,
                    sohrKnockbackGoals.Select(g => (IGeometryShape)new RectangleGeometry(g.Position, g.Rotation, 10, 4.5f)).ToArray()),
                    Arena.Position, Display: false, Activation: sohrKnockbackGoals[0].Activation);
        }
        if (sohrTouchdown != null)
            yield return new("sohr/touchdown", "Touchdown", sohrTouchdown.Rule.Shape!, sohrTouchdown.Location, Activation: sohrTouchdown.FinishAt);
        if (Active.BossOid != 0x3D17) yield break;
        if (sohrStack != null && _actors.TryGetValue(sohrStack.TargetId, out var stackTarget))
        {
            yield return new("sohr/stack/display", "死亡轮回四人分摊", new CircleTemplate(6), stackTarget.Position, Navigate: false, OutlineOnly: true, Safe: true);
            if (PlayerId != stackTarget.Id)
                yield return new("sohr/stack/goal", "死亡轮回四人分摊", new CircleTemplate(6), stackTarget.Position, Display: false, Safe: true);
        }
        foreach (var (id, activation) in sohrSpreads)
        {
            if (!_actors.TryGetValue(id, out var target)) continue;
            yield return new($"sohr/spread/{id}", "HolyBreath", new CircleTemplate(6), target.Position, Navigate: false, OutlineOnly: true);
            if (id != PlayerId)
                yield return new($"sohr/spread/{id}/avoid", "HolyBreath", new CircleTemplate(6), target.Position, Display: false, Activation: activation);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"sohr/spread/party/{buddy.Id}", "分散避开队友", new CircleTemplate(6), buddy.Position, Display: false, Activation: activation);
        }
        foreach (var (id, line) in sohrOrbs)
            for (var i = 0; i < Math.Min(3, line.Remaining); ++i)
                yield return new($"sohr/orb/{id}/{i}", "HolyOrb", new CircleTemplate(6), QuantizedDungeonPosition(line.Next + i * line.Step),
                    Activation: (line.Activation > _now || i == 0 ? line.Activation : _now).AddSeconds(i));
        if (_actors.TryGetValue(PlayerId, out var skating) && HasStatus(skating, 911))
        {
            yield return new("sohr/ice/constraint", "薄冰滑行可达站位", new SohrKhaiThinIceTemplate(skating.Position), skating.Position, Display: false);
            yield return new("sohr/ice/range", "薄冰滑行11码", new CircleTemplate(11), skating.Position, Navigate: false, OutlineOnly: true);
            if (Arena != null && (!LegacyMovement || CameraAzimuth != null))
            {
                var rotation = LegacyMovement ? CameraAzimuth!.Value + MathF.PI : skating.Rotation;
                var landing = DungeonWallLanding(Arena, skating.Position, new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation)) * 11);
                foreach (var aoe in DungeonMovementPreview("sohr/ice", "薄冰滑行预测", skating.Position, landing)) yield return aoe;
            }
        }
    }
}
