using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class Pt90Encounter() : PtBossEncounter(90, 0x48F5)
{
    public override uint[] Actions { get; } = [44250, 44251, 44259, 44262, 44257, 43796, 44258, 44267, 44268, 44266];
    private Cast? skinflayer;
    protected override void OnCast(Cast cast)
    {
        switch (cast.Action)
        {
            case 44250 or 44251: Hit(cast, new FanTemplate(30, Rad(270)), "Backhand"); break;
            case 44262: Hit(cast, new FanTemplate(35, Rad(90)), "Devil's Quarter"); break;
            case 44257 or 43796: Hit(cast, RayRectangleTemplate.Symmetric(10, 50), "Arcane Beacon"); break;
            case 44268: Hit(cast, new CircleTemplate(6), "Hot Iron"); break;
            case 44266 when cast.Rotation != null: skinflayer = cast; break;
        }
    }
    protected override void AddPersonalAreas(List<Area> result, DateTime now, ulong player)
    {
        if (skinflayer is not { Rotation: { } rotation } cast || now > cast.Hit.AddSeconds(.3) || KnockbackImmune(player, cast.Hit)) return;
        result.Add(new("skinflayer/landing", "Skinflayer 定向击退 30m", new PtKnockbackTemplate(Boundary, cast.Position, 30, Direction(rotation)),
            Center, 0, cast.Hit.AddSeconds(-.2), cast.Hit.AddSeconds(.15), NavigationOnly: true));
    }
    protected override void OnEffect(LogSystemActionEffectEvent effect, DateTime now)
    { if (effect.ActionId == 44266) skinflayer = null; }
    public override void Cancel(ulong source) { base.Cancel(source); if (skinflayer?.Source == source) skinflayer = null; }
    public override IEnumerable<PtKnockbackTemplate> KnockbackSources(DateTime now, ulong player)
    {
        if (skinflayer is { Rotation: { } rotation } cast && now <= cast.Hit.AddSeconds(.3) && !KnockbackImmune(player, cast.Hit))
            yield return new(Boundary, cast.Position, 30, Direction(rotation));
    }
    protected override void ResetMechanics() => skinflayer = null;
}
