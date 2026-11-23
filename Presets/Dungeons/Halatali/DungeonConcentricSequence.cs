namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DungeonConcentricSequence
{
    private readonly DungeonPendingCast first;
    public DungeonPendingCast Current { get; private set; }
    public ulong Source => first.Source;
    public bool Finished { get; private set; }

    public DungeonConcentricSequence(DungeonPendingCast first)
    {
        this.first = first;
        Current = first;
    }

    public bool Matches(DungeonPendingCast cast)
        => cast.Rule.Action == Current.Rule.Action
            && System.Numerics.Vector2.DistanceSquared(new(cast.Location.X, cast.Location.Z),
                new(first.Location.X, first.Location.Z)) <= 1;

    public void Advance(DungeonDefinition definition, DateTime completedAt)
    {
        var action = Current.Rule.Action switch
        {
            40599 => 40600u, 40600 => 40601u, 29586 => 29591u,
            29286 => 29287u, 29287 => 29288u, _ => 0u,
        };
        if (action == 0)
        {
            Finished = true;
            return;
        }
        // This is the upstream ConcentricAOEs prediction, not a measured effect timestamp.
        Current = new(first.Source, definition.Casts.Single(c => c.Action == action),
            first.Location, 0, completedAt, completedAt.AddSeconds(action is 29287 or 29288 ? 2 : 3));
    }
}
