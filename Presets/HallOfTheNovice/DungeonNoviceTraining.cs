using System.Numerics;
using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsNovice => NoviceCatalog.IsTerritory(territory);
    private readonly HashSet<ulong> noviceCompleted = [];
    private bool noviceEnabled;
    private readonly Dictionary<ulong, byte> noviceActorOrder = [];

    private void ObserveNovice(Func<string, bool>? enabled)
    {
        if (!IsNovice) return;
        noviceEnabled = enabled?.Invoke(Definitions[0].Name) != false;
        // The source feed removes missing actors in its persistent dictionary order before inserting new ones.
        foreach (var id in noviceActorOrder.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
            noviceActorOrder.Remove(id);
        foreach (var actor in _actors.Values)
            noviceActorOrder.TryAdd(actor.Id, 0);
        noviceCompleted.RemoveWhere(id => !_actors.TryGetValue(id, out var actor) || !NoviceCatalog.Owns(territory, actor.Oid));
        foreach (var actor in NoviceActors.Where(a => NoviceCatalog.Owns(territory, a.Oid)))
            if ((actor.Id == PrimaryId && Active != null
                || noviceEnabled && (actor.Targetable || IsTactical) && (IsTactical || territory == NoviceCatalog.TANK04 || actor.InCombat))
                && (actor.Dead || actor.Hp == 0))
                noviceCompleted.Add(actor.Id);
        ObserveNoviceTank04();
    }

    private IEnumerable<DungeonAmbientAoe> NovicePrecombatAoes()
    {
        if (!IsNovice || !noviceEnabled || Active != null) yield break;
        var primary = NoviceActors.FirstOrDefault(a => NoviceCatalog.Owns(territory, a.Oid) && !noviceCompleted.Contains(a.Id)
            && (territory != NoviceCatalog.TANK04 || a.Id == noviceTank04Primary));
        if (primary == null || primary.Dead) yield break;
        var arena = Definitions[0].Arena(primary.Position);
        yield return new("novice/precombat/arena", "场地", new CircleTemplate(20), arena.Position,
            Navigate: false, OutlineOnly: true);
        if (!primary.Targetable) yield break;
        var offset = primary.Position - arena.Position;
        offset.Y = 0;
        var outside = offset.LengthSquared() > 400;
        var position = outside ? arena.Position + Vector3.Normalize(offset) * 20 : primary.Position;
        yield return new("novice/precombat/primary", "敌人及朝向",
            new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]),
            position, primary.Rotation, Navigate: false, OutlineOnly: outside, Unclipped: true,
            Color: new Vector4(1, 0, 0, 1));
    }

    private IEnumerable<DungeonActor> NoviceActors => noviceActorOrder.Keys.Where(_actors.ContainsKey).Select(id => _actors[id]);

    private ulong NoviceDirectedTarget => NoviceActors.FirstOrDefault(a => a.Oid == 0x1574)?.TargetId ?? 0;

    private IEnumerable<ReplayActorState> NoviceTargetActors => _actors.Values.Select(a => new ReplayActorState
        {
            Id = a.Id, Oid = a.Oid, Kind = a.Player ? (byte)1 : (byte)2,
            IsEnemy = a.IsEnemy, Targetable = a.Targetable, Dead = a.Dead, Hp = a.Hp ?? uint.MaxValue,
            ObjectIndex = a.ObjectIndex, NameId = a.NameId, Position = a.Position,
            TargetId = a.TargetId, AggroPlayer = a.AggroPlayer, InCombat = a.InCombat,
            FateId = a.FateId, IncomingEffects = a.IncomingEffects?.ToArray() ?? [],
            Statuses = a.Statuses?.Select(s => new ReplayStatus(s, 0, 0, DateTime.MaxValue)).ToArray() ?? []
        });

    private ulong NovicePriorityTarget(ulong current, Vector3 position)
    {
        if (Active == null) return 0;
        var party = _actors.Values.Where(a => a.PartySlot >= 0).Select(a => a.Id).ToHashSet();
        var actors = NoviceTargetActors.ToArray();
        var directed = NoviceDirectedTarget;
        int? Assigned(ReplayActorState actor) => territory == NoviceCatalog.DPS03 && actor.Id == directed ? 1 : null;
        return QuestTargetPriority.Select(territory, actors, current, position, party, AllowedFateId, Assigned);
    }

    private IEnumerable<string> NoviceHints()
    {
        foreach (var hint in NoviceTank04Hints()) yield return hint;
        if (!IsNovice || territory != NoviceCatalog.DPS03 || Active == null) yield break;
        var directed = NoviceDirectedTarget;
        var current = _actors.GetValueOrDefault(PlayerId)?.TargetId ?? 0;
        // GenericInvincible only provides this text; its AI restriction is overridden by the source.
        if (directed != 0 && current != directed && _actors.ContainsKey(current))
            yield return "Attacking invincible target!";
    }
}
