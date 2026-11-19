using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class Pt80Encounter() : PtBossEncounter(80, 0x485D)
{
    public override uint[] Actions { get; } = [43465, 43467, 43468, 43472, 43473, 43476, 43477, 43795, 43478, 44060, 43469, 43471, 43475];
    private readonly HashSet<ulong> knownBalls = [];
    private Cast? waul;
    protected override void OnCast(Cast cast)
    {
        switch (cast.Action)
        {
            case 43468: Hit(cast, new DonutTemplate(8, 40), "Roaring Ring"); break;
            case 43472: Hit(cast, new CircleTemplate(12), "Perilous Lair"); break;
            case 43473 when cast.Rotation != null: waul = cast; break;
            case 43795:
                var origin = cast.SourcePosition ?? actors.GetValueOrDefault(cast.Source)?.Position;
                if (origin == null) break;
                var delta = cast.Position - origin.Value;
                delta.Y = 0;
                if (delta.LengthSquared() < .001f) break;
                var predicted = areas.Values.Where(a => a.Key.StartsWith("lightning/charge/", StringComparison.Ordinal)).MinBy(a => Vector3.DistanceSquared(a.Position, origin.Value));
                AddHit(predicted?.Key ?? CastKey(cast.Source, cast.Action), "Stalking Static", RayRectangleTemplate.Symmetric(4, delta.Length()), origin.Value,
                    MathF.Atan2(delta.X, delta.Z), cast.Hit, cast.Source, cast.Action);
                break;
            case 44060:
                areas.Remove("lightning/end");
                Hit(cast, new CircleTemplate(30), "Static Shock");
                break;
        }
    }
    protected override void SynchronizeActors(DateTime now)
    {
        var balls = actors.Values.Where(a => a.Oid == 0x485E && !a.Dead).OrderByDescending(a => a.Id).ToArray();
        if (balls.Length != 9 || balls.All(b => knownBalls.Contains(b.Id))) return;
        RemoveGroup("lightning/");
        for (var i = 0; i < 9; ++i) knownBalls.Add(balls[i].Id);
        for (var i = 0; i < 8; ++i)
        {
            var delta = balls[i + 1].Position - balls[i].Position;
            delta.Y = 0;
            AddHit($"lightning/charge/{i}", "Stalking Static", RayRectangleTemplate.Symmetric(4, delta.Length()), balls[i].Position,
                MathF.Atan2(delta.X, delta.Z), now.AddSeconds(7.5 + .4 * i));
        }
        AddHit("lightning/end", "Static Shock", new CircleTemplate(30), balls[8].Position, 0, now.AddSeconds(11.7));
    }
    protected override void OnEffect(LogSystemActionEffectEvent effect, DateTime now)
    {
        if (effect.ActionId == 43795) ResolveFirst("lightning/charge/");
        if (effect.ActionId == 44060) RemoveGroup("lightning/");
        if (effect.ActionId == 43473) waul = null;
    }
    protected override void AddPersonalAreas(List<Area> result, DateTime now, ulong player)
    {
        PreviewNextWave(result, a => a.Key.StartsWith("lightning/", StringComparison.Ordinal) || a.Action is 43795 or 44060);
        if (waul is not { Rotation: { } rotation } cast || now > cast.Hit.AddSeconds(.3)) return;
        var doomed = Status(player, 4518, cast.Hit) != null;
        // A doomed player must take the same original half-arena cone.
        var fan = new FanTemplate(40, MathF.PI);
        if (doomed)
        {
            result.Add(new("waul/required", "Shadow of Death 必须吃扇形", new PtShapeTemplate(new PtCompositeGeometry([Boundary], [fan.Resolve(cast.Position, rotation, null)])),
                Center, 0, cast.Hit.AddSeconds(-.2), cast.Hit.AddSeconds(.15), NavigationOnly: true));
            result.Add(new("waul/preview", "Shadow of Death 必须吃扇形", fan, cast.Position, rotation, now, cast.Hit.AddSeconds(.15), Safe: true, PreviewOnly: true));
        }
        else result.Add(new("waul", "Profane Waul", fan, cast.Position, rotation, cast.Hit.AddSeconds(-.2), cast.Hit.AddSeconds(.15)));
    }
    public override bool HoldMovement(DateTime now, ulong player) => Status(player, 4519, now)?.Param >= 6;
    public override IEnumerable<string> Hints(DateTime now, ulong player)
    {
        if (Status(player, 4519, now) is { } status) yield return $"减少移动：{status.Param}/8 层；6 层起停止自动移动";
        if (waul != null && Status(player, 4518, now) != null) yield return "Shadow of Death：进入半场扇形解除死亡状态";
    }
    public override void Cancel(ulong source) { base.Cancel(source); if (waul?.Source == source) waul = null; }
    protected override void ResetMechanics() { knownBalls.Clear(); waul = null; }
}
