using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(ulong Id, ulong RawSource, ulong RawTarget, DungeonActor Source, DungeonActor Target,
        bool SourceRemoved, bool TargetRemoved)> guildivainBaits = [];
    private readonly Dictionary<ulong, ulong> guildivainTethers = [];
    private readonly Dictionary<ulong, ulong> guildivainObservedTethers = [];
    private readonly Dictionary<ulong, byte> guildivainStates = [];
    private DateTime guildivainActivation;
    private ulong guildivainBaitId;
    private bool guildivainInitialized;

    private void GuildivainTether(ulong source, ulong target, uint tether, bool present, bool observed = false)
    {
        if (!IsGuildivain || tether != 17) return;
        if (!present)
        {
            if (guildivainTethers.GetValueOrDefault(source) == target) guildivainTethers.Remove(source);
            var oldFrom = _actors.GetValueOrDefault(source);
            if (oldFrom == null)
            {
                // A removal frame may precede ObjectRemove; outgoing Untether still uses the old actor.
                var old = guildivainBaits.FirstOrDefault(b => b.RawSource == source);
                oldFrom = old.Source?.Id == source ? old.Source : old.Target;
            }
            if (oldFrom == null || !_actors.TryGetValue(target, out var oldTo)) return;
            var (oldEnemy, oldPlayer) = oldFrom.Player || oldFrom.Buddy ? (oldTo, oldFrom) : (oldFrom, oldTo);
            if (!oldPlayer.Player && !oldPlayer.Buddy || oldEnemy.Player || oldEnemy.Buddy) return;
            var index = guildivainBaits.FindIndex(b => b.Source.Id == oldEnemy.Id && b.Target.Id == oldPlayer.Id);
            if (index >= 0) guildivainBaits.RemoveAt(index);
            return;
        }
        if (guildivainTethers.TryGetValue(source, out var previous))
        {
            if (previous == target && observed) return;
            GuildivainTether(source, previous, tether, false, observed);
        }
        guildivainTethers[source] = target;
        if (!_actors.TryGetValue(source, out var from) || !_actors.TryGetValue(target, out var to)) return;
        var (enemy, player) = from.Player || from.Buddy ? (to, from) : (from, to);
        if (!player.Player && !player.Buddy || enemy.Player || enemy.Buddy) return;
        if (guildivainActivation == default) guildivainActivation = _now;
        guildivainBaits.Add((++guildivainBaitId, source, target, enemy, player, false, false));
    }

    private void GuildivainObserve()
    {
        if (!IsGuildivain) return;
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1EA1A1))
        {
            if (guildivainInitialized) GuildivainState(actor.Id, actor.EventState);
            else guildivainStates[actor.Id] = actor.EventState;
        }
        guildivainInitialized = true;
        var tethers = _actors.Values.Where(a => a.Tethers?.GetValueOrDefault(17u) is > 0)
            .ToDictionary(a => a.Id, a => a.Tethers![17u]);
        foreach (var previous in guildivainObservedTethers.ToArray())
            if (tethers.GetValueOrDefault(previous.Key) != previous.Value)
                GuildivainTether(previous.Key, previous.Value, 17, false, observed: true);
        foreach (var pair in tethers)
            if (guildivainObservedTethers.GetValueOrDefault(pair.Key) != pair.Value)
                GuildivainTether(pair.Key, pair.Value, 17, true, observed: true);
        guildivainObservedTethers.Clear();
        foreach (var pair in tethers) guildivainObservedTethers[pair.Key] = pair.Value;
        for (var i = 0; i < guildivainBaits.Count; ++i)
        {
            var bait = guildivainBaits[i];
            var source = !bait.SourceRemoved ? _actors.GetValueOrDefault(bait.Source.Id) : null;
            var target = !bait.TargetRemoved ? _actors.GetValueOrDefault(bait.Target.Id) : null;
            guildivainBaits[i] = (bait.Id, bait.RawSource, bait.RawTarget, source ?? bait.Source, target ?? bait.Target,
                source == null, target == null);
        }
    }

    private void GuildivainRemoved(ulong id)
    {
        if (!IsGuildivain) return;
        // The source actor still exists here: upstream dispatches its outgoing Untethered before Removed.
        if (guildivainTethers.TryGetValue(id, out var target))
            GuildivainTether(id, target, 17, false);
        guildivainObservedTethers.Remove(id);
        guildivainStates.Remove(id);
        for (var i = 0; i < guildivainBaits.Count; ++i)
        {
            var bait = guildivainBaits[i];
            guildivainBaits[i] = (bait.Id, bait.RawSource, bait.RawTarget, bait.Source, bait.Target,
                bait.SourceRemoved || bait.Source.Id == id, bait.TargetRemoved || bait.Target.Id == id);
        }
    }

    private void GuildivainState(ulong source, byte state)
    {
        if (!IsGuildivain || _actors.GetValueOrDefault(source)?.Oid != 0x1EA1A1) return;
        if (guildivainStates.TryGetValue(source, out var previous) && previous == state) return;
        guildivainStates[source] = state;
        if (state == 7 && Arena != null) Arena = JobQuestCatalog.LifeArena(Arena.Position.Y, 19.5f);
    }

    private void GuildivainEffect(uint action, ulong target)
    {
        if (!IsGuildivain) return;
        if (action is 26852 or 26853)
            guildivainBaits.RemoveAll(b => b.Target.Id == target);
        if (action == 26854 && Arena != null)
            Arena = JobQuestCatalog.LifeArena(Arena.Position.Y, 10);
    }

    private IEnumerable<DungeonAmbientAoe> GuildivainAoes()
    {
        if (!IsGuildivain) yield break;
        foreach (var bait in guildivainBaits.Where(b => !b.Source.Dead))
        {
            var direction = bait.Target.Position - bait.Source.Position;
            var angle = MathF.Atan2(direction.X, direction.Z);
            var own = !bait.TargetRemoved && bait.Target.Id == PlayerId;
            yield return new($"guildivain/bait/{bait.Id}/display", "Aetherstream", RayRectangleTemplate.Symmetric(4, 50),
                bait.Source.Position, angle, Navigate: !own, OutlineOnly: own, Activation: guildivainActivation);
            var length = MathF.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
            if (length > .001f)
                yield return new($"guildivain/bait/{bait.Id}/line", "Aetherstream Tether", new RectangleTemplate(.08f, length),
                    (bait.Source.Position + bait.Target.Position) * .5f, angle, Navigate: false, Color: Vector4.One);
            if (!own) continue;
            foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
            {
                var delta = member.Position - bait.Source.Position;
                var distance = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
                // The source's asin(half-width/distance) has no finite cone when the buddy overlaps the source.
                if (distance < 2) continue;
                var halfAngle = MathF.Asin(2 / distance);
                yield return new($"guildivain/bait/{bait.Id}/away/{member.Id}", "诱导直线远离队伍", new FanTemplate(100, halfAngle * 2),
                    bait.Source.Position, MathF.Atan2(delta.X, delta.Z), Display: false, Activation: guildivainActivation);
            }
        }
    }

    private IEnumerable<DungeonDamagePrediction> GuildivainDamage()
    {
        if (!IsGuildivain || guildivainBaits.Count == 0) yield break;
        foreach (var bait in guildivainBaits.Where(b => !b.Source.Dead).DistinctBy(b => b.Target.Id))
            if (_actors.GetValueOrDefault(bait.Target.Id) is { PartySlot: >= 0 } target)
                yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, guildivainActivation, "Tankbuster");
    }

    private IEnumerable<string> GuildivainHints(Vector3? player)
    {
        if (!IsGuildivain || player is not { } position) yield break;
        foreach (var bait in guildivainBaits.Where(b => !b.Source.Dead))
        {
            var delta = bait.Target.Position - bait.Source.Position;
            var shape = RayRectangleTemplate.Symmetric(4, 50).Resolve(bait.Source.Position, MathF.Atan2(delta.X, delta.Z), null);
            if (!bait.TargetRemoved && bait.Target.Id == PlayerId)
            {
                if (_actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position)))
                    yield return "Bait away from raid!";
            }
            else if (shape.Contains(position))
                yield return "GTFO from baited aoe!";
        }
    }
}
