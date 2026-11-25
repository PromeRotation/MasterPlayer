using ECommons.DalamudServices;
using Dalamud.Game.ClientState.Objects.Types;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Mechanics.Dungeons;

namespace MasterPlayer.Mechanics.Quests;

public abstract class QuestTargetScript(uint territory) : MasterPlayerScript
{
    private readonly Dictionary<int, ulong> npcParty = [];
    internal Func<DateTime, IEnumerable<ReplayActorState>> CaptureLiveActors { get; set; } = now =>
    {
        var enmity = ReplayWorldCapture.CapturePlayerEnmity();
        return Svc.Objects.Where(o => o.Address != 0 && o is IBattleChara)
            .Select(o => ReplayWorldCapture.CaptureActor(o, now, playerEnmity: enmity)).ToArray();
    };
    internal Func<ReplayPartyMember[]> CaptureLiveParty { get; set; } = ReplayWorldCapture.CaptureParty;
    internal Func<byte?, uint> CaptureLiveAllowedFate { get; set; } = ReplayWorldCapture.CaptureAllowedFate;
    public ulong PlannedTargetId { get; private set; }

    public override IEnumerable<string> GetMechanisms() => ["优先目标"];

    protected override void OnStart() => UpdateTarget();
    protected override void OnUpdate(float deltaTime) => UpdateTarget();

    protected override void OnStop()
    {
        PlannedTargetId = 0;
        npcParty.Clear();
        Hints.SetTarget(0);
    }

    private void UpdateTarget()
    {
        if (CurrentPlayer is not { Dead: false } player || !IsMechanismEnabled("优先目标"))
        {
            PlannedTargetId = 0;
            Hints.SetTarget(0);
            return;
        }
        var now = CurrentTime;
        var actors = (ReplayWorld is { } world ? world.Actors.Values : CaptureLiveActors(now)).ToArray();
        IReadOnlySet<ulong>? party = null;
        if (QuestTargetPriority.PartialOverride(territory))
            party = PartyIds(actors, ReplayWorld?.Party ?? CaptureLiveParty(), player.Id);
        var allowedFate = ReplayWorld?.AllowedFateId;
        if (ReplayWorld == null && actors.Any(a => a.FateId is > 0))
            allowedFate = CaptureLiveAllowedFate(player.Level);
        var currentTarget = ReplayWorld?.TargetId ?? player.TargetId;
        PlannedTargetId = QuestTargetPriority.Select(territory, actors, currentTarget, player.Position, party, allowedFate);
        var clearTarget = actors.Any(a => a.Id == currentTarget && QuestTargetPriority.ExplicitlyPointless(territory, a))
            ? currentTarget : 0;
        Hints.SetTarget(PlannedTargetId, clearTarget);
    }

    private HashSet<ulong> PartyIds(ReplayActorState[] actors, IReadOnlyList<ReplayPartyMember> party, ulong player)
    {
        var roster = party.Where(p => p.Slot is >= 0 and < 64 && p.EntityId is not (0 or 0xE0000000))
            .GroupBy(p => p.Slot).ToDictionary(g => g.Key, g => g.First().EntityId);
        var friends = actors.Where(a => a.Kind == 2 && a.SubKind == 5 && a.IsEnemy == false
            && a.Targetable && !DungeonEncounter.IgnoredFriendlyNpc(a.Oid)).Select(a => a.Id).ToHashSet();
        foreach (var (slot, id) in npcParty.ToArray())
            if (!friends.Contains(id) || roster.ContainsKey(slot) || roster.ContainsValue(id)) npcParty.Remove(slot);
        foreach (var actor in actors.Where(a => friends.Contains(a.Id) && !roster.ContainsValue(a.Id) && !npcParty.ContainsValue(a.Id)))
        {
            var slot = Enumerable.Range(24, 40).FirstOrDefault(s => !roster.ContainsKey(s) && !npcParty.ContainsKey(s), -1);
            if (slot < 0) break;
            npcParty[slot] = actor.Id;
        }
        return roster.Values.Concat(npcParty.Values).Append(player).ToHashSet();
    }
}
