namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    // A completed source module is unloaded; changing the same object's state cannot load it again.
    // Keep the receipt through ResetFight and stale frames until absence or a real create establishes a new object.
    private readonly Dictionary<ulong, (uint Oid, bool Absent)> treasureCompletedRooms = [];
    private readonly Dictionary<ulong, (uint Oid, long Order, bool Pulled, bool Absent)> treasureRoomInstances = [];
    private long treasureRoomOrder;

    private void TreasureRoomsObserve()
    {
        if (territory != NativeTreasureCatalog.CENOTE_JA_JA_GURAL) return;
        foreach (var (id, receipt) in treasureCompletedRooms.ToArray())
        {
            if (!_actors.TryGetValue(id, out var actor))
                treasureCompletedRooms[id] = (receipt.Oid, true);
            else if (receipt.Absent || actor.Oid != receipt.Oid)
                treasureCompletedRooms.Remove(id);
        }
        foreach (var (id, instance) in treasureRoomInstances.ToArray())
            if (!_actors.TryGetValue(id, out var actor) || actor.Oid != instance.Oid)
                treasureRoomInstances[id] = instance with { Absent = true };
        foreach (var actor in _actors.Values.Where(IsTreasureRoomPrimary))
        {
            if (!treasureRoomInstances.TryGetValue(actor.Id, out var instance) || instance.Oid != actor.Oid || instance.Absent)
                instance = (actor.Oid, ++treasureRoomOrder, false, false);
            if (NativeCenoteRoomCatalog.Trash(actor.Oid).Any(oid => _actors.Values.Any(a => a.Oid == oid)))
                instance.Pulled = true;
            treasureRoomInstances[actor.Id] = instance;
            // Source updates every loaded module, including rooms that are not selected for display.
            if (instance.Pulled && actor.EventState == 7)
                treasureCompletedRooms[actor.Id] = (actor.Oid, false);
        }
        // A reused ID may already hold the next room's completed generation.
        if (IsTreasureRoom && !TreasureGroupAlive
            && (!_actors.TryGetValue(PrimaryId, out var current) || current.Oid == Active!.BossOid))
            treasureCompletedRooms[PrimaryId] = (Active!.BossOid, !_actors.ContainsKey(PrimaryId));
    }

    private long TreasureRoomOrder(DungeonActor actor) => treasureRoomInstances.GetValueOrDefault(actor.Id).Order;

    private void TreasureRoomCreated(ulong id, uint oid)
    {
        if (territory != NativeTreasureCatalog.CENOTE_JA_JA_GURAL || !NativeCenoteRoomCatalog.IsRoom(oid)) return;
        if (treasureCompletedRooms.Remove(id)) treasureRoomInstances.Remove(id);
    }

    private void TreasureRoomDestroyed(ulong id)
    {
        if (_actors.GetValueOrDefault(id) is { } actor && IsTreasureRoomPrimary(actor))
            treasureCompletedRooms[id] = (actor.Oid, false);
    }
}
