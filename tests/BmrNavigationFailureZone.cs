using BossMod;

internal sealed class BmrNavigationFailureZone(WorldState world) : ZoneModule(world)
{
    public override void CalculateAIHints(int playerSlot, Actor player, AIHints hints)
        => throw new InvalidOperationException("injected navigation failure");
}
