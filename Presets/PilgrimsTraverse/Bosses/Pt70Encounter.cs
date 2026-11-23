using System.Numerics;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class Pt70Encounter() : PtBossEncounter(70, 0x484C)
{
    public override uint[] Actions { get; } = [43411, 43412, 43413, 43414, 43415, 43406, 43407, 43408, 43409, 43410,
        43417, 43418, 43419, 43420, 43421, 43422, 43423, 43424, 43425, 43426, 43427, 43428, 43429, 43430, 43431, 43437, 43432, 43434, 43436, 43435, 43433];
    private DateTime haloHit;
    private readonly HashSet<ulong> spheres = [];
    private int swipeIndex;
    private Cast? groan;

    protected override void OnCast(Cast cast)
    {
        switch (cast.Action)
        {
            case 43411 or 43406: haloHit = cast.Hit.AddSeconds(8.4); break;
            case 43418 or 43423: haloHit = cast.Hit.AddSeconds(6.7); break;
            case >= 43407 and <= 43410 or >= 43412 and <= 43415:
                Hit(cast, RayRectangleTemplate.Symmetric(10, 5), "Zealous Glower");
                break;
            case >= 43419 and <= 43422 or >= 43424 and <= 43427 when cast.Rotation != null:
                Hit(cast, new PtDonutSectorTemplate(5, 10, Rad(90)), "Ardorous Eye");
                break;
            case 43417:
                areas.Remove($"halo/{cast.Source}");
                Hit(cast, new DonutTemplate(3, 20), "Brutal Halo");
                break;
            case 43429: Hit(cast, new CircleTemplate(8), "Twothousand Mina Swing"); break;
            case 43431: groan = cast; break;
            case 43437 when cast.Rotation != null:
                var first = areas.Values.Where(a => a.Key.StartsWith("swipe/", StringComparison.Ordinal)).MinBy(a => a.From);
                var count = areas.Values.Count(a => a.Key.StartsWith("swipe/", StringComparison.Ordinal));
                var hit = first == null ? cast.Hit.AddSeconds(8.2) : first.From.AddSeconds(.2 + count * 2);
                AddHit($"swipe/{swipeIndex++}", "Octuple Swipe 记忆八连", new FanTemplate(40, Rad(90)), cast.Position, cast.Rotation.Value, hit, cast.Source);
                break;
        }
    }

    protected override void SynchronizeActors(DateTime now)
    {
        if (haloHit == default) return;
        var newSpheres = actors.Values.Where(a => a.Oid == 0x484D && !a.Dead && !spheres.Contains(a.Id)).OrderBy(a => a.Id).ToArray();
        foreach (var sphere in newSpheres)
        {
            spheres.Add(sphere.Id);
            var count = areas.Values.Count(a => a.Name == "Brutal Halo");
            AddHit($"halo/{sphere.Id}", "Brutal Halo", new DonutTemplate(3, 20), sphere.Position, 0, haloHit.AddSeconds(count * 2.5), sphere.Id, 43417);
        }
    }

    protected override void OnEffect(LogSystemActionEffectEvent effect, DateTime now)
    {
        if (effect.ActionId == 43417) areas.Remove($"halo/{effect.SourceId}");
        if (effect.ActionId is 43434 or 43436 or 43435 or 43433) ResolveFirst("swipe/");
        if (effect.ActionId == 43431) groan = null;
    }

    protected override void AddPersonalAreas(List<Area> result, DateTime now, ulong player)
    {
        PreviewNextWave(result, a => a.Name == "Brutal Halo");
        PreviewNextWave(result, a => a.Key.StartsWith("swipe/", StringComparison.Ordinal), 2);
        if (groan is not { } cast || now > cast.Hit.AddSeconds(.3) || KnockbackImmune(player, cast.Hit)) return;
        var halos = result.Where(a => a.Name == "Brutal Halo" && Math.Abs((a.From.AddSeconds(.2) - cast.Hit).TotalSeconds) < 1)
            .Select(a => a.Geometry).ToArray();
        result.Add(new("groan/landing", "Disorienting Groan 击退", new PtKnockbackTemplate(Boundary, cast.Position, 7, LandingDangers: halos),
            Center, 0, cast.Hit.AddSeconds(-.2), cast.Hit.AddSeconds(.15), NavigationOnly: true));
        if (!result.Any(a => a.Name == "Brutal Halo"))
            result.Add(new("groan/goal", "击退靠近中心", new CircleTemplate(2.5f), cast.Position, 0, now, cast.Hit, Safe: true));
    }
    public override void Cancel(ulong source) { base.Cancel(source); if (groan?.Source == source) groan = null; }
    public override IEnumerable<PtKnockbackTemplate> KnockbackSources(DateTime now, ulong player)
    {
        if (groan is { } cast && now <= cast.Hit.AddSeconds(.3) && !KnockbackImmune(player, cast.Hit))
            yield return new(Boundary, cast.Position, 7);
    }
    protected override void ResetMechanics() { haloHit = default; spheres.Clear(); swipeIndex = 0; groan = null; }
}
