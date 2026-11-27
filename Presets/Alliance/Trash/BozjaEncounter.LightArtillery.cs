namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsLightArtillery => Definition.Key == "A20LightArtilleryUnit";
    internal int LightSelfDestructCount { get; private set; }
    internal int LightHomingCount { get; private set; }
    private List<BozjaActor>? lightFire;

    private void ClearLightArtillery()
    {
        LightSelfDestructCount = 0;
        LightHomingCount = 0;
        lightFire = null;
    }

    private void LightEffect(uint action)
    {
        if (!IsLightArtillery) return;
        if (action == 21013) ++LightSelfDestructCount;
        if (action == 21008) ++LightHomingCount;
    }

    private void LightCreated(BozjaActor actor)
    {
        if (IsLightArtillery && actor.Actor.Oid == 0x1E8D9B) lightFire?.Add(actor);
    }

    private IEnumerable<BozjaActor> LightFire
        // BossModule.Enemies caches its first query, then appends Created.
        // Re-enumerating Dictionary after a removal can reuse an earlier slot.
        => (lightFire ??= Actors.Values.Where(a => a.Actor.Oid == 0x1E8D9B).ToList())
            .Where(a => a.Actor.EventState != 7);

    private IEnumerable<BozjaAoe> LightAoes(BozjaRule rule)
    {
        if (!IsLightArtillery || rule.Component != "FireVoidzone") yield break;
        foreach (var fire in LightFire)
            yield return new(new(-fire.BirthOrder, rule, fire, fire.Actor.Position, fire.Actor.Rotation, default), false, true);
    }

    private IEnumerable<BozjaZone> LightNavigation(BozjaRule rule)
    {
        if (!IsLightArtillery || rule.Component != "FireVoidzone") yield break;
        foreach (var fire in LightFire)
            yield return new($"light/fire/{fire.BirthOrder}", rule.Component, default, rule.Shape,
                fire.Actor.Position, fire.Actor.Rotation, Obstacle: true);
    }
}
