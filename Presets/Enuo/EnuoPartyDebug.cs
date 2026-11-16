using System.Numerics;
using MasterPlayer.Replay;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Enuo;

internal sealed record EnuoDebugTarget(int Role, ulong Actor, Vector3? Position, EnuoGoal? Goal, string State);
internal sealed record EnuoDebugMarker(Vector3 Position, string Label, int FirstRole);
internal sealed record EnuoDebugFrame(float ArenaRadius, EnuoDebugTarget[] Targets)
{
    // Shared stacks have one marker with every assigned role; don't offset the true destination.
    public EnuoDebugMarker[] Markers => Targets.Where(t => t.Goal != null).GroupBy(t => t.Goal!.Position)
        .Select(g => new EnuoDebugMarker(g.Key, string.Join("/", g.Select(t => EnuoStrategy.Roles[t.Role])), g.First().Role)).ToArray();
}

// Keep every role's personal history current, both for ARR perspective switches and debug display.
internal sealed class EnuoPartyDebug
{
    private readonly EnuoEncounter[] _encounters = Enumerable.Range(0, 8).Select(_ => new EnuoEncounter()).ToArray();
    private readonly EnuoEventAdapter[] _events;
    private ulong[] _party = [];

    public EnuoPartyDebug(Func<LogSystemCastStartEvent, float?> rotation)
        => _events = _encounters.Select(e => new EnuoEventAdapter(e, rotation)).ToArray();

    public void Synchronize(IEnumerable<ReplayActorState> actors, IEnumerable<ReplayPartyMember> party, DateTime now)
    {
        var snapshot = actors.ToArray(); var members = party.ToArray();
        var resolved = EnuoStrategy.ResolveParty(members);
        if (_party.Length == 8 && !resolved.SequenceEqual(_party)) Reset();
        _party = resolved;
        for (var i = 0; i < 8; i++) _encounters[i].Synchronize(snapshot, members, _party.Length == 8 ? _party[i] : 0, now);
    }

    public void Handle(LogSystemEvent ev, DateTime now)
    {
        foreach (var adapter in _events) adapter.Handle(ev, now);
    }

    public EnuoEncounter? ForPlayer(ulong id)
    {
        var slot = id == 0 ? -1 : Array.IndexOf(_party, id);
        return slot < 0 ? null : _encounters[slot];
    }

    public EnuoDebugFrame Snapshot(DateTime now)
    {
        var targets = new EnuoDebugTarget[8]; var radius = 20f;
        for (var i = 0; i < 8; i++)
        {
            var encounter = _encounters[i]; var plan = encounter.Plan(now); radius = plan.ArenaRadius;
            var id = _party.Length == 8 ? _party[i] : 0;
            var actor = encounter.Actors.GetValueOrDefault(id);
            var goal = plan.Goal is { } g && g.Until >= now ? g : null;
            var state = id == 0 ? "分工未确认" : actor == null ? "对象未加载" : actor.Dead ? "已死亡"
                : plan.HoldMovement ? "热病：停步" : goal?.Name ?? "暂无机制目标";
            targets[i] = new(i, id, actor?.Position, goal, state);
        }
        return new(radius, targets);
    }

    public void Reset()
    {
        _party = [];
        foreach (var encounter in _encounters) encounter.Reset();
    }
}
