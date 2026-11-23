using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    // 环境开关可以先于开战到达；与首领读条状态分开保留至区域脚本停止。
    private bool northernWallRemoved;
    private bool wandilArenaShrunk;
    private readonly Dictionary<ulong, (uint State, DateTime Activation, Vector3 Origin, float Rotation)> snowWinds = [];
    private DungeonPendingCast? snowDrift;
    private DungeonPendingCast? tundra;
    private readonly Dictionary<ulong, DungeonPendingCast> frozenPillars = [];
    private Vector3? snowMoveAnchor;
    private ulong snowMovePlayer;

    internal bool RequireSnowMovement => snowDrift != null && _now >= snowDrift.FinishAt.AddSeconds(-.75)
        && _actors.TryGetValue(PlayerId, out var player) && !player.Dead;

    private IEnumerable<DungeonActor> IntactIcicles => _actors.Values.Where(a => a.Oid == 0x397A && !a.Dead && a.AnimationState1 != 1);

    private bool IsSnowcloakTrash(DungeonActor actor) => territory == SnowcloakCatalog.TERRITORY
        && SnowcloakCatalog.TRASH.Contains(actor.Oid) && SnowcloakArenas.Northern(actor.Position.Y).Contains(actor.Position);

    private void SnowcloakCast(DungeonPendingCast cast)
    {
        if (cast.Rule.Action == 3080) snowDrift = cast with { FinishAt = cast.FinishAt.AddSeconds(2) };
        if (cast.Rule.Action == 3082) tundra = cast with { FinishAt = cast.FinishAt.AddSeconds(2) };
        if (cast.Rule.Action == 29599)
        {
            var pillars = IntactIcicles.ToArray();
            losSnapshots[cast.Source] = (new(new(cast.Location, pillars, true, 80, false)),
                new(new(cast.Location, pillars, false, 80, false)));
        }
    }

    private void SnowcloakFinish(DungeonPendingCast cast, bool completed, DateTime at)
    {
        if (!completed && cast.Rule.Action == 3080) { snowDrift = null; snowMoveAnchor = null; }
        if (!completed && cast.Rule.Action == 3082) tundra = null;
        if (cast.Rule.Action == 29599 && completed && Active != null)
        {
            var rule = Active.Casts.Single(c => c.Action == 29601);
            foreach (var pillar in IntactIcicles)
                frozenPillars[pillar.Id] = new(pillar.Id, rule, pillar.Position, 0, at, at.AddSeconds(4.5));
        }
        if (cast.Rule.Action == 29601 && completed) frozenPillars.Clear();
    }

    private void SnowcloakAnimation(ulong source, uint state)
    {
        if (territory != SnowcloakCatalog.TERRITORY || !_actors.TryGetValue(source, out var actor)) return;
        if (actor.Oid == 0x1E94F1 && state == 0x00040008 && !northernWallRemoved)
        {
            northernWallRemoved = true;
            if (Active?.BossOid == 0xD1E) Arena = SnowcloakArenas.Northern(actor.Position.Y);
        }
        if (actor.Oid == 0x1E9661 && state == 0x00040008 && !wandilArenaShrunk)
        {
            wandilArenaShrunk = true; tundra = null;
            if (Active?.BossOid == 0xD07) Arena = SnowcloakArenas.Wandil(actor.Position.Y);
        }
        var origin = actor.Oid switch
        {
            0x1E96D4 => new Vector3(1.5f, actor.Position.Y, -140.75f),
            0x1E94F0 => new Vector3(-31.462f, actor.Position.Y, -167.332f),
            0x1E94F2 => new Vector3(-51.839f, actor.Position.Y, -132.241f),
            _ => (Vector3?)null,
        };
        if (origin == null || state is not (0x00040008 or 0x00010002)) return;
        if (snowWinds.TryGetValue(source, out var previous) && previous.State == state) return;
        // 上游关闭通知仅更新已经观察到的风场；不将首次关闭的风场伪造为将启动。
        if (state == 0x00010002 && !snowWinds.ContainsKey(source)) return;
        var degrees = actor.Oid switch { 0x1E96D4 => -135, 0x1E94F0 => -36, _ => -13 };
        snowWinds[source] = (state, state == 0x00010002 ? _now.AddSeconds(5) : _now, origin.Value, degrees * MathF.PI / 180);
    }

    private void SynchronizeSnowcloak()
    {
        foreach (var id in snowWinds.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) snowWinds.Remove(id);
        foreach (var id in frozenPillars.Keys.Where(id => !_actors.TryGetValue(id, out var actor) || actor.Dead).ToArray()) frozenPillars.Remove(id);
        if (!RequireSnowMovement) { snowMoveAnchor = null; return; }
        var player = _actors[PlayerId];
        if (snowMoveAnchor == null || snowMovePlayer != PlayerId || Vector3.DistanceSquared(player.Position, snowMoveAnchor.Value) > 1.21f)
        {
            snowMovePlayer = PlayerId;
            snowMoveAnchor = player.Position;
        }
    }

    private IEnumerable<DungeonAmbientAoe> SnowcloakAoes()
    {
        if (Active?.BossOid == 0xD1E)
            foreach (var (id, wind) in snowWinds)
                yield return new($"wind/{id}", "寒风区域", RayRectangleTemplate.Symmetric(34, 17), wind.Origin, wind.Rotation,
                    Activation: wind.Activation);
        if (Active?.BossOid == 0xD07 && tundra != null && !wandilArenaShrunk)
        {
            // 20边形内场与外圆的精确差集；每片无洞，雷达和世界绘制都保留内场。
            var center = new Vector3(56.41631f, Arena!.Position.Y, -88.51856f);
            var inner = SnowcloakArenas.Wandil(center.Y).GetContours()[0];
            for (var i = 0; i < inner.Count; ++i)
            {
                var a = inner[i]; var b = inner[(i + 1) % inner.Count];
                var angle = MathF.Atan2(a.X - center.X, a.Z - center.Z);
                var delta = MathF.IEEERemainder(MathF.Atan2(b.X - center.X, b.Z - center.Z) - angle, MathF.Tau);
                var points = new List<Vector2> { new(center.X - a.X, center.Z - a.Z) };
                for (var j = 0; j <= 8; ++j)
                {
                    var theta = angle + delta * j / 8;
                    points.Add(new(-20 * MathF.Sin(theta), -20 * MathF.Cos(theta)));
                }
                points.Add(new(center.X - b.X, center.Z - b.Z));
                yield return new($"tundra/{i}", "冻土边缘预告", new LocalPolygonTemplate(points), center, Activation: tundra.FinishAt);
            }
        }
        if (RequireSnowMovement && snowMoveAnchor is { } anchor)
            yield return new("snowdrift/move", "保持移动", new CircleTemplate(.8f), anchor, Display: false);
        if (Active?.BossOid == 0x3979)
        {
            foreach (var (source, snapshot) in losSnapshots)
            {
                if (!_casts.TryGetValue(source, out var cast) || cast.Rule.Action != 29599) continue;
                yield return new($"lunar/safe/{source}", "冰柱遮挡安全区", snapshot.Safe, cast.Location, Navigate: false, Safe: true);
                yield return new($"lunar/danger/{source}", "躲到冰柱后", snapshot.Danger, cast.Location, Display: false, Activation: cast.FinishAt);
            }
            foreach (var pillar in IntactIcicles)
                yield return new($"icicle/{pillar.Id}", "完整冰柱", new CircleTemplate(pillar.HitboxRadius), pillar.Position,
                    Navigate: false, OutlineOnly: true, Safe: true);
        }
    }
}
