using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed record PalaceTrashGaze(PalaceTrashCaster Source, uint Action, GeometryTemplate Shape,
    PalaceTrashCaster? Target = null, bool Generic = false);
