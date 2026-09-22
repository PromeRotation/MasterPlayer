using System.Numerics;

namespace MasterPlayer.Replay;

public sealed record ReplayStatus(uint Id, ushort Param, ulong SourceId, DateTime ExpiresAt)
{
    internal static DateTime Expiration(DateTime now, float remaining)
        => !float.IsFinite(remaining) || remaining == 0 ? DateTime.MaxValue
            : now.AddSeconds(Math.Min(MathF.Abs(remaining), 100000f));
}
public sealed record ReplayCast(uint ActionId, ulong TargetId, Vector3 Location, float? Rotation,
    DateTime StartedAt, DateTime FinishAt, bool Interruptible, bool EffectSeen);
public sealed record ReplayPartyMember(int Slot, string Role, ulong EntityId);
public sealed record ReplayTether(ushort Id, ulong TargetId, byte Progress = 0);
public sealed record ReplayCastData(float? PacketFacing);

public sealed record ReplayActorState
{
    public ulong Id { get; init; }
    public ulong GameObjectId { get; init; }
    public uint EntityId { get; init; }
    public uint Oid { get; init; }
    public uint NameId { get; init; }
    public string Name { get; init; } = "";
    public byte Kind { get; init; }
    public byte SubKind { get; init; }
    public ulong OwnerId { get; init; }
    public Vector3 Position { get; init; }
    public float Rotation { get; init; }
    public float HitboxRadius { get; init; }
    public bool Dead { get; init; }
    public bool Targetable { get; init; }
    public bool InCombat { get; init; }
    public bool Hostile { get; init; }
    public ulong TargetId { get; init; }
    public byte Level { get; init; }
    public uint ClassJob { get; init; }
    public uint Hp { get; init; }
    public uint MaxHp { get; init; }
    public uint Mp { get; init; }
    public uint MaxMp { get; init; }
    public byte Shield { get; init; }
    public byte EventState { get; init; }
    public byte ModelState { get; init; }
    public byte AnimationState1 { get; init; }
    public byte AnimationState2 { get; init; }
    public uint? EventAnimation { get; init; }
    public ReplayCast? Cast { get; init; }
    public ReplayStatus[] Statuses { get; init; } = [];
    public ReplayTether[] Tethers { get; init; } = [];

    internal bool SameAs(ReplayActorState other)
        => (this with { Statuses = other.Statuses, Tethers = other.Tethers }) == other
           && Statuses.SequenceEqual(other.Statuses) && Tethers.SequenceEqual(other.Tethers);
}

public sealed record ReplayWorldFrame
{
    public bool Full { get; init; }
    public ushort Territory { get; init; }
    public ulong LocalPlayerId { get; init; }
    public ulong TargetId { get; init; }
    public string LocalTimelineRole { get; init; } = "None";
    public ReplayPartyMember[] Party { get; init; } = [];
    public ReplayActorState[] Actors { get; init; } = [];
    public ulong[] Removed { get; init; } = [];

    internal static ReplayWorldFrame Delta(ReplayWorldFrame? previous, ReplayWorldFrame current)
    {
        if (previous == null || previous.Territory != current.Territory) return current with { Full = true };
        var old = previous.Actors.ToDictionary(a => a.Id);
        var ids = current.Actors.Select(a => a.Id).ToHashSet();
        return current with
        {
            Full = false,
            Actors = current.Actors.Where(a => !old.TryGetValue(a.Id, out var before) || !a.SameAs(before)).ToArray(),
            Removed = old.Keys.Where(id => !ids.Contains(id)).ToArray()
        };
    }
}

public sealed record ReplayWorldFrameEntry(double T, ReplayWorldFrame Frame, long Sequence = 0);

public sealed class ReplayWorldState
{
    private readonly Dictionary<ulong, ReplayActorState> _actors = [];
    public IReadOnlyDictionary<ulong, ReplayActorState> Actors => _actors;
    public IReadOnlyList<ReplayPartyMember> Party { get; private set; } = [];
    public DateTime CurrentTime { get; internal set; }
    public ushort Territory { get; private set; }
    public ulong LocalPlayerId { get; private set; }
    public ulong TargetId { get; private set; }
    public string LocalTimelineRole { get; private set; } = "None";
    public bool HasSnapshot { get; private set; }
    public long Version { get; private set; }
    public ReplayActorState? LocalPlayer => _actors.GetValueOrDefault(LocalPlayerId);

    internal void Apply(ReplayWorldFrame frame)
    {
        if (frame.Full) _actors.Clear();
        else if (!HasSnapshot) throw new InvalidDataException("对象变化之前缺少完整初始快照。");
        foreach (var id in frame.Removed) _actors.Remove(id);
        foreach (var actor in frame.Actors) _actors[actor.Id] = actor;
        Territory = frame.Territory;
        LocalPlayerId = frame.LocalPlayerId;
        TargetId = frame.TargetId;
        LocalTimelineRole = frame.LocalTimelineRole;
        Party = frame.Party;
        HasSnapshot = true;
        Version++;
    }
}
