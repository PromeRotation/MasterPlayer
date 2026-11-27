using MasterPlayer.Mechanics.Dungeons;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsArachne => Definition.Key == "A21ArachneEve";
    internal bool[] ArachneArenaStatus { get; } = new bool[24];
    internal bool ArachneWebArena { get; private set; }
    internal float ArachneRadius => ArachneWebArena ? ArachneArenas.WebRadius : ArachneArenas.GroundRadius;

    private void ClearArachne()
    {
        Array.Clear(ArachneArenaStatus);
        ArachneWebArena = false;
    }

    internal void ObserveArachneDrawing(BozjaActor viewer)
    {
        if (!IsArachne || !Active) return;
        var slot = ViewerSlot(viewer);
        if (slot is < 0 or >= 24) return;
        var point = viewer.Actor.Position;
        var x = point.X - 16f;
        var z = point.Z + 55f;
        if (x * x + z * z > 900f) return;
        var previous = ArachneArenaStatus[slot];
        var next = previous;
        if (!previous && point.Y > 10f) next = true;
        else if (previous && point.Y < 10f) next = false;
        if (previous == next) return;
        ArachneArenaStatus[slot] = next;
        ArachneWebArena = next;
        Arena = next ? ArachneCatalog.Web : ArachneCatalog.Ground;
        Center = Arena.Position;
    }
}
