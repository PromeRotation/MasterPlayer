namespace MasterPlayer.Replay;

/// <summary>Client floor map captured with the object frame; absent in older recordings.</summary>
public sealed record ReplayDeepDungeonState
{
    public byte DungeonId { get; init; }
    public byte Floor { get; init; }
    public byte Tileset { get; init; }
    public byte PassageProgress { get; init; }
    public byte WeaponLevel { get; init; }
    public byte ArmorLevel { get; init; }
    public bool? TrapsHidden { get; init; }
    public ReplayDeepDungeonItem[]? Items { get; init; }
    public ushort[] Rooms { get; init; } = [];
    public Dictionary<ulong, int> PartyRooms { get; init; } = [];
}
