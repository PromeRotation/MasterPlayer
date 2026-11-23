using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> WorqorAoes()
    {
        if (territory != WorqorCatalog.TERRITORY || Arena == null) yield break;
        if (worqorShrink != null) yield return worqorShrink;
        if (worqorFreezeCount > 0)
            foreach (var frozen in worqorFrozen.Take(worqorTutorial < 2 ? 2 : 4)) yield return frozen.Aoe;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var player = _actors.GetValueOrDefault(PlayerId);
        foreach (var gaze in _casts.Values.Where(c => c.Rule.Action == 36297))
        {
            var delta = player == null ? Vector2.Zero : new Vector2(gaze.Location.X - player.Position.X, gaze.Location.Z - player.Position.Z);
            var direction = player == null ? Vector2.Zero : new Vector2(MathF.Sin(player.Rotation), MathF.Cos(player.Rotation));
            var danger = delta.LengthSquared() > 0 && Vector2.Dot(direction, Vector2.Normalize(delta)) >= .707107f;
            var color = danger ? new Vector4(1, .2f, .2f, 1) : new Vector4(.2f, .8f, 1, 1);
            var lens = new List<Vector2>();
            for (var i = 0; i <= 16; ++i)
            {
                var x = -.7f + 1.4f * i / 16;
                lens.Add(new(x, .35f * (1 - x * x / .49f)));
            }
            for (var i = 15; i > 0; --i)
            {
                var x = -.7f + 1.4f * i / 16;
                lens.Add(new(x, -.35f * (1 - x * x / .49f)));
            }
            yield return new($"worqor/gaze/{gaze.Source}/eye", "背对视线", new LocalPolygonTemplate(lens),
                gaze.Location, Navigate: false, Safe: !danger, Color: color);
            yield return new($"worqor/gaze/{gaze.Source}/pupil", "视线眼睛", new CircleTemplate(.18f),
                gaze.Location, Navigate: false, Color: new Vector4(0, 0, 0, 1));
            if (player != null)
                yield return new($"worqor/gaze/{gaze.Source}/facing", "当前角色面向", new Mechanics.PilgrimsTraverse.PtDonutSectorTemplate(.98f, 1.02f, MathF.PI / 2),
                    player.Position, player.Rotation, Navigate: false, Color: new Vector4(1, .2f, .2f, 1));
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 36281 or 36295 or 36298 or 36306
            && _actors.TryGetValue(c.TargetId, out var a) && !a.Dead))
        {
            var radius = cast.Rule.Action == 36281 ? 5 : 6;
            foreach (var a in ShisuiBait($"worqor/spread/{cast.Source}", cast.TargetId, radius, cast.FinishAt))
                yield return a with { Shape = a.Display ? a.Shape : new CircleTemplate(radius + 1), OutlineOnly = a.Display,
                    Position = QuantizedDungeonPosition(a.Position) };
        }
        if (worqorWind is { } wind)
        {
            yield return wind;
            var geometry = wind.Shape.Resolve(wind.Position, wind.Rotation, null);
            if (worqorWindInverted)
                yield return new("worqor/wind/join", "进入晶体线", new SohrKhaiWallHintTemplate(Arena, [geometry]),
                    Arena.Position, Display: false, Activation: wind.Activation);
            else if (worqorWindIndex >= 0x20 && player != null)
            {
                var tank = player.PartyRole == "Tank" || player.ClassJob is 1 or 3 or 19 or 21 or 32 or 37;
                if (tank)
                    yield return new("worqor/wind/tank", "坦克保持中心十二码内", new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(Arena.Position, 12)]),
                        Arena.Position, Display: false, Activation: wind.Activation);
                else
                {
                    var back = worqorWindIndex == 0x20 ? 1 : 10;
                    var angle = worqorWindIndex == 0x20 ? -40 * MathF.PI / 180 : 0;
                    var center = Arena.Position + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * ((20 - back) * .5f);
                    yield return new("worqor/wind/nontank", "风土晶体站位", new RectangleTemplate(40, 20 + back),
                        center, angle, Display: false, Activation: wind.Activation);
                }
            }
            if (worqorStacks.Count > 0)
            {
                var candidates = party.Where(a => a.Id != PlayerId
                    && (worqorWindIndex >= 0x20 ? geometry.Contains(a.Position, 0) : wind.Shape is DonutTemplate && !geometry.Contains(a.Position, 0)))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 1.66f)).ToArray();
                if (candidates.Length > 0)
                    yield return new("worqor/donut/join", "月环分摊靠拢安全队友", new SohrKhaiWallHintTemplate(Arena, candidates),
                        Arena.Position, Display: false, Activation: worqorStacks[0].At);
            }
        }
        // The icon owns the donut drawing even while the environmental AOE is absent.
        foreach (var stack in worqorStacks)
            yield return new($"worqor/donut/{stack.Serial}/display", "风月环分摊", new DonutTemplate(5, 10),
                QuantizedDungeonPosition(stack.Target.Position), Navigate: false, Activation: stack.At);
        var towers = _casts.Values.Where(c => c.Rule.Action == 36153).ToArray();
        var towerJoin = new List<IGeometryShape>();
        foreach (var tower in towers)
        {
            var shape = new CircleGeometry(tower.Location, 6);
            var inside = player != null && shape.Contains(player.Position, 0);
            var count = party.Count(a => shape.Contains(a.Position, 0));
            var safe = count < 4 || inside && count <= 4;
            yield return new($"worqor/tower/{tower.Source}/display", "四人塔", new CircleTemplate(6),
                QuantizedDungeonPosition(tower.Location), Navigate: false, OutlineOnly: inside || count < 4, Safe: safe);
            if (count < 4 || inside && count == 4) towerJoin.Add(shape);
        }
        if (towerJoin.Count > 0)
            yield return new("worqor/tower/join", "进入四人塔", new SohrKhaiWallHintTemplate(Arena, towerJoin.ToArray()),
                Arena.Position, Display: false, Activation: towers[0].FinishAt);
        else
            foreach (var tower in towers)
                yield return new($"worqor/tower/{tower.Source}/avoid", "避开满员塔", new CircleTemplate(6),
                    tower.Location, Display: false, Activation: towers[0].FinishAt);
        var fireMax = worqorFire.Count;
        if (worqorFirstFire)
        {
            fireMax = Math.Min(fireMax, 12);
            if (fireMax >= 4) fireMax -= fireMax % 4;
        }
        for (var i = 0; i < fireMax; ++i)
        {
            var fire = worqorFire[i];
            if (worqorFirstFire)
            {
                var risky = fire.Activation < worqorFire[0].Activation!.Value.AddSeconds(1);
                if (risky && worqorFire.Count > 4) fire = fire with { Color = new Vector4(1, 1, 0, 1) };
                worqorFire[i] = fire with { Navigate = risky };
                yield return worqorFire[i];
            }
            else
            {
                yield return fire;
                yield return new(fire.Id + "/join", "站安全区等击退", new SohrKhaiWallHintTemplate(Arena,
                    [fire.Shape.Resolve(fire.Position, fire.Rotation, null)]), Arena.Position, Display: false, Activation: fire.Activation);
            }
        }
        var winds = _actors.Values.Where(a => a.Oid == 0x4160 && a.EventState != 7).ToArray();
        foreach (var whirlwind in winds)
        {
            yield return new($"worqor/whirlwind/{whirlwind.Id}/display", "移动旋风", new DungeonCapsuleTemplate(5, 7),
                whirlwind.Position, whirlwind.Rotation, Navigate: false);
            yield return new($"worqor/whirlwind/{whirlwind.Id}/obstacle", "旋风障碍", new CircleTemplate(5),
                whirlwind.Position, Display: false, Obstacle: true);
            foreach (var (length, seconds) in new (float, double)[] { (3.5f, 1.1), (7, 3), (14, 10), (21, double.MaxValue) })
                yield return new($"worqor/whirlwind/{whirlwind.Id}/{length}", "旋风前方预测", new DungeonCapsuleTemplate(5, length),
                    whirlwind.Position, whirlwind.Rotation, Display: false, Activation: seconds == double.MaxValue ? DateTime.MaxValue : _now.AddSeconds(seconds));
        }
        var orbs = _actors.Values.Where(a => a.Oid == 0x4162 && !a.Dead).ToArray();
        foreach (var orb in orbs)
            yield return new($"worqor/orb/{orb.Id}/display", "收集灵气球", new CircleTemplate(1), orb.Position,
                Navigate: false, OutlineOnly: true, Safe: true);
        foreach (var orb in orbs)
        {
            var center = orb.Position + new Vector3(MathF.Sin(orb.Rotation), 0, MathF.Cos(orb.Rotation)) * .5f;
            yield return new($"worqor/orb/join/{orb.Id}", "收集灵气球", new RectangleTemplate(1, 1),
                center, Display: false, Safe: true);
        }
        foreach (var action in new uint[] { 36307, 36310, 39074 })
        {
            var cast = worqorKnockbacks.FirstOrDefault(c => c.Rule.Action == action);
            if (cast == null) continue;
            GeometryTemplate? constraint = null;
            if (action == 36310)
                constraint = new VanaspatiKnockbackTemplate(new RectangleGeometry(Arena.Position, 0, 38, 38), cast.Location, [], 15);
            else if (action == 36307 && worqorFire.Count == 0)
                constraint = new SohrKhaiWallHintTemplate(Arena, [RayRectangleTemplate.Symmetric(40, 15).Resolve(cast.Location, cast.Rotation, null)]);
            else if (action == 39074 && winds.Length > 0)
            {
                IGeometryShape[] safe;
                if ((cast.FinishAt - _now).TotalSeconds > 3) safe = [new CircleGeometry(cast.Location, 8)];
                else
                {
                    float[] angles = worqorWEWE ? [15, -165, 105, -75] : [-15, 165, -105, 75];
                    safe = angles.Select(a => new FanTemplate(5, MathF.PI / 6).Resolve(cast.Location, a * MathF.PI / 180, null)).ToArray();
                }
                constraint = new SohrKhaiWallHintTemplate(Arena, safe);
            }
            if (constraint != null)
                yield return new($"worqor/knockback/{action}/nav", "击退前安全站位", constraint, Arena.Position,
                    Display: false, Activation: cast.FinishAt);
        }
        if (player != null)
        {
            var start = player.Position;
            foreach (var cast in worqorKnockbacks.OrderBy(c => c.FinishAt))
            {
                if (KnockbackImmune(player, cast.FinishAt)) continue;
                var landing = WorqorLanding(cast, start);
                foreach (var a in DungeonMovementPreview($"worqor/knockback/{cast.Source}", "击退落点", start, landing)) yield return a;
                start = landing;
            }
        }
        foreach (var a in WorqorLineAoes(party)) yield return a;
        foreach (var debris in _actors.Values.Where(a => a.Oid == 0x415E && !a.Dead))
            yield return new($"worqor/debris/{debris.Id}", "可攻击晶体", new CircleTemplate(debris.HitboxRadius),
                debris.Position, Navigate: false, OutlineOnly: true, Color: new Vector4(0, 1, 1, 1));
    }

    private static Vector3 WorqorLanding(DungeonPendingCast cast, Vector3 point)
    {
        if (cast.Rule.Action == 36307) return point + new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation)) * 25;
        var delta = point - cast.Location; delta.Y = 0;
        return delta.LengthSquared() > .000001f ? point + Vector3.Normalize(delta) * 15 : point;
    }

    private IEnumerable<DungeonAmbientAoe> WorqorLineAoes(DungeonActor[] party)
    {
        if (Arena == null) yield break;
        var lines = worqorLines.Where(l => !l.Source.Dead).ToArray();
        var marked = lines.Any(l => l.Target.Id == PlayerId);
        var join = new List<IGeometryShape>();
        foreach (var line in lines)
        {
            var delta = line.Target.Position - line.Source.Position;
            var angle = MathF.Atan2(delta.X, delta.Z);
            yield return new($"worqor/line/{line.Serial}/display", "直线分摊", RayRectangleTemplate.Symmetric(8, 50),
                line.Source.Position, angle, Navigate: false, Safe: line.Target.Id == PlayerId || !marked);
            if (line.Target.Id != PlayerId)
            {
                if (!marked) join.Add(RayRectangleTemplate.Symmetric(8, 50).Resolve(line.Source.Position, angle, null));
                else
                    yield return new($"worqor/line/{line.Serial}/avoid", "避免重叠直线分摊", RayRectangleTemplate.Symmetric(16, 50),
                        line.Source.Position, angle, Display: false, Activation: line.At);
            }
            else
            {
                var candidates = party.Where(a => !lines.Any(l => l.Target.Id == a.Id)).Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (candidates.Length > 0)
                    yield return new($"worqor/line/{line.Serial}/gather", "直线分摊点名靠拢队友", new SohrKhaiWallHintTemplate(Arena, candidates),
                        Arena.Position, Display: false, Activation: line.At);
            }
        }
        if (join.Count > 0)
            yield return new("worqor/line/join", "进入直线分摊", new DungeonExclusiveJoinTemplate(Arena, join.ToArray()),
                Arena.Position, Display: false, Activation: lines[0].At);
    }

    private IEnumerable<DungeonDamagePrediction> WorqorDamage()
    {
        if (territory != WorqorCatalog.TERRITORY) yield break;
        var lines = worqorLines.Where(l => !l.Source.Dead).ToArray();
        if (lines.Length > 0)
            foreach (var p in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(p.Id, p.PartySlot, p.PartyRole, p.Name, lines.Min(l => l.At), "Shared");
        var towers = _casts.Values.Where(c => c.Rule.Action == 36153).ToArray();
        if (towers.Length > 0)
            foreach (var p in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
                && towers.Any(t => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(t.Location.X, t.Location.Z)) <= 36)))
                yield return new(p.Id, p.PartySlot, p.PartyRole, p.Name, towers[0].FinishAt, "Raidwide");
    }

    private IEnumerable<string> WorqorHints()
    {
        if (territory != WorqorCatalog.TERRITORY) yield break;
        if (WantsWorqorSprint) yield return "收集灵气球";
        if (worqorWindInverted && worqorWind != null) yield return "进入晶体线";
        if (worqorStacks.Count > 0) yield return "风月环分摊：靠拢安全队友";
        if (!worqorFirstFire && worqorFire.Count > 0) yield return "站安全区等击退";
        if (worqorLines.Any(l => !l.Source.Dead)) yield return "直线分摊";
        if (Arena == null || !_actors.TryGetValue(PlayerId, out var player)) yield break;
        var start = player.Position;
        foreach (var cast in worqorKnockbacks.OrderBy(c => c.FinishAt))
        {
            if (KnockbackImmune(player, cast.FinishAt)) continue;
            start = WorqorLanding(cast, start);
            if (!Arena.Contains(start, 0) || cast.Rule.Action == 39074 && _actors.Values.Any(a => a.Oid == 0x4160 && a.EventState != 7
                && new DungeonCapsuleTemplate(5, 7).Resolve(a.Position, a.Rotation, null).Contains(start, 0)))
                yield return "击退落点危险";
        }
    }
}
