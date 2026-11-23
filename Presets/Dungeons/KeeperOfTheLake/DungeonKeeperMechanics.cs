using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DungeonPendingCast? flamethrower;
    private int flameHits;
    private DungeonPendingCast? akhMorn;
    private int akhMornHits;
    private DungeonPendingCast? keeperDisgust;
    private readonly Dictionary<ulong, DungeonPendingCast> mirageRays = [];
    private readonly List<(Vector3 Position, DateTime Activation)> garleanFires = [];

    private void KeeperCast(DungeonPendingCast cast)
    {
        if (cast.Rule.Action == 3389) { flamethrower = cast; flameHits = 0; }
        if (cast.Rule.Action == 29283) { akhMorn = cast; akhMornHits = 0; }
        if (cast.Rule.Action == 29891) keeperDisgust = cast with { FinishAt = cast.FinishAt.AddSeconds(.5) };
    }

    private void KeeperFinish(DungeonPendingCast cast, bool completed)
    {
        if (completed) return;
        if (cast.Rule.Action == 3389) { flamethrower = null; flameHits = 0; }
        if (cast.Rule.Action == 29283) { akhMorn = null; akhMornHits = 0; }
        if (cast.Rule.Action == 29891) keeperDisgust = null;
    }

    private void KeeperEffect(uint action, DateTime now, ulong target, Vector3? location)
    {
        if (Active?.BossOid == 0xE68)
        {
            if (action is 3389 or 3390 && flamethrower != null && ++flameHits == 6)
            { flamethrower = null; flameHits = 0; }
            if (action == 3411)
            {
                var position = _actors.GetValueOrDefault(target)?.Position ?? location;
                if (position is { } p && float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z))
                    garleanFires.Add((p, now.AddSeconds(.2)));
                else warning?.Invoke("GarleanFire: 缺少目标及效果位置，跳过预测。");
            }
        }
        if (Active?.BossOid == 0x392A)
        {
            if (action == 29290) mirageRays.Clear();
            if (action == 29892) keeperDisgust = null;
            if (action is 29283 or 29284 && akhMorn != null && ++akhMornHits == 4)
            { akhMorn = null; akhMornHits = 0; }
        }
    }

    private void KeeperTimeline(ulong source, uint timeline, DateTime now)
    {
        if (Active?.BossOid != 0x392A || timeline != 0x11D3
            || !_actors.TryGetValue(source, out var actor) || actor.Oid is not (0x392C or 0x392B)) return;
        var rule = new DungeonCastRule(29290, actor.Oid, "MirageAdmonishment", RayRectangleTemplate.Symmetric(12, 40));
        mirageRays.TryAdd(source, new(source, rule, actor.Position, actor.Rotation, now, now.AddSeconds(12)));
    }

    private void SynchronizeKeeper()
    {
        foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1E98CA && !a.Dead && a.EventState != 7))
        {
            var index = garleanFires.FindIndex(f => Vector2.DistanceSquared(new(f.Position.X, f.Position.Z), new(zone.Position.X, zone.Position.Z)) <= 4);
            if (index >= 0) garleanFires.RemoveAt(index);
        }
        foreach (var id in mirageRays.Keys.Where(id => !_actors.TryGetValue(id, out var actor) || actor.Dead).ToArray())
            mirageRays.Remove(id);
        if (akhMorn != null && (!_actors.TryGetValue(akhMorn.TargetId, out var target) || target.Dead)) akhMorn = null;
    }

    private IEnumerable<DungeonAmbientAoe> KeeperAoes()
    {
        if (Active?.BossOid != 0xE68) yield break;
        if (flamethrower != null)
            yield return new("flamethrower", $"喷火：已判定 {flameHits}/6", flamethrower.Rule.Shape!, flamethrower.Location,
                flamethrower.Rotation, Activation: flamethrower.FinishAt);
        for (var i = 0; i < garleanFires.Count; ++i)
            yield return new($"garlean/predict/{i}", "燃烧地面预告", new CircleTemplate(8), garleanFires[i].Position,
                Activation: garleanFires[i].Activation);
        foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1E98CA && !a.Dead && a.EventState != 7))
            yield return new($"garlean/zone/{zone.Id}", "燃烧地面", new CircleTemplate(8), zone.Position);
    }
}
