using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class Pt40Encounter() : PtBossEncounter(40, 0x460B)
{
    public override uint[] Actions { get; } = [42124, 42126, 42123, 42125, 42129, 45121, 45169, 45122, 45170, 42128, 42131, 44047, 43289, 43290];
    private readonly Dictionary<ulong, Cast> knockbacks = [];
    protected override int TargetPriority(ReplayActorState actor)
        => actor.Oid == 0x460C ? 1 : base.TargetPriority(actor);
    public override IReadOnlyList<ulong> ForbiddenDotTargets
        => Active ? actors.Values.Where(a => a.Oid == 0x460C && !a.Dead && a.Targetable).Select(a => a.Id).ToArray() : [];
    protected override void OnCast(Cast cast)
    {
        switch (cast.Action)
        {
            case 42129: Hit(cast, new CircleTemplate(20), "Shining Shot"); break;
            case 45169: Hit(cast, new CircleTemplate(13), "Near Tide"); break;
            case 45170: Hit(cast, new DonutTemplate(8, 60), "Far Tide"); break;
            case 42131 or 44047: Hit(cast, new CircleTemplate(5), "Chaser"); break;
            case 42128: knockbacks[cast.Source] = cast; break;
        }
    }
    protected override void AddPersonalAreas(List<Area> result, DateTime now, ulong player)
    {
        var sequence = knockbacks.Values.Where(c => c.Hit.AddSeconds(.3) >= now && !KnockbackImmune(player, c.Hit)).OrderBy(c => c.Hit).ToArray();
        if (sequence.Length == 0) return;
        var first = sequence[0];
        var landingDangers = result.Where(a => !a.Safe && !a.Obstacle && a.From <= first.Hit.AddSeconds(.4) && a.Until >= first.Hit).Select(a => a.Geometry).ToArray();
        var constraint = new PtKnockbackTemplate(Boundary, first.Position, 21, LandingDangers: landingDangers);
        if (sequence.Length > 1 && !KnockbackImmune(player, sequence[1].Hit))
        {
            var direction = sequence[1].Position - first.Position;
            constraint = constraint with { LandingAllowed = new FanGeometry(first.Position, MathF.Atan2(direction.X, direction.Z), 100, Rad(30)) };
        }
        result.Add(new("saltwater/landing", "连续 Saltwater Shot 击退落点", constraint, Center, 0,
            first.Hit.AddSeconds(-.2), first.Hit.AddSeconds(.15), NavigationOnly: true));
    }
    protected override void OnEffect(LogSystemActionEffectEvent effect, DateTime now)
    { if (effect.ActionId == 42128) knockbacks.Remove(effect.SourceId); }
    public override IEnumerable<PtKnockbackTemplate> KnockbackSources(DateTime now, ulong player)
        => knockbacks.Values.Where(c => c.Hit.AddSeconds(.3) >= now && !KnockbackImmune(player, c.Hit))
            .OrderBy(c => c.Hit).Select(c => new PtKnockbackTemplate(Boundary, c.Position, 21));
    public override void Cancel(ulong source) { base.Cancel(source); knockbacks.Remove(source); }
    protected override void ResetMechanics() => knockbacks.Clear();
    public override IEnumerable<string> Hints(DateTime now, ulong player)
        => actors.Values.Any(a => a.Oid == 0x460C && !a.Dead && a.Cast?.ActionId == 43289) ? ["优先击杀小怪，阻止自爆"] : [];
}
