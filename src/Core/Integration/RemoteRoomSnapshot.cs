namespace MasterPlayer.Integration;

public sealed record RemoteRoomSnapshot
{
    public string RoomCode { get; init; } = "";
    public string RoomName { get; init; } = "";
    public bool IsConnected { get; init; }
    public bool IsHost { get; init; }
    public bool IsVirtual { get; init; }
    public string Transport { get; init; } = "Remote";
    public int OnlineMemberCount { get; init; }
    public int TotalMemberCount { get; init; }
    public RemoteRoomMember[] Members { get; init; } = [];
}
