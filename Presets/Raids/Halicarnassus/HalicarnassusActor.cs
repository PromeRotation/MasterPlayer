namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class HalicarnassusActor(DungeonActor actor)
{
    internal DungeonActor Actor { get; set; } = actor;
    internal bool NativeNpc { get; } = !actor.Player && !actor.Buddy
        && actor.NativeType is not (0x104 or 0x202 or 0x203 or 0x209);
    internal DungeonCastObservation? Cast { get; set; }
    internal float Elapsed { get; set; }
    internal DateTime Clock { get; set; }
    internal bool Destroyed { get; set; }
}
