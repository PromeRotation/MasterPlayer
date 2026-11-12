namespace MasterPlayer.Integration;

public sealed record RemoteRoomMember
{
    public string CidHash { get; init; } = "";
    public string Name { get; init; } = "";
    public string Role { get; init; } = "";
    public string? JobRole { get; init; }
    public bool IsConnected { get; init; }
    public bool IsMe { get; init; }
    public bool IsNavigating { get; init; }
    public bool IsMounted { get; init; }
    public bool IsInCombat { get; init; }
    public float? WorldX { get; init; }
    public float? WorldY { get; init; }
    public float? WorldZ { get; init; }
}
