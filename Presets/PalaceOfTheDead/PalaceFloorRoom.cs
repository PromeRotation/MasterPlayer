using System.Numerics;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed record PalaceFloorRoom(Vector2 Center, PalaceFloorWall North, PalaceFloorWall South,
    PalaceFloorWall West, PalaceFloorWall East);
