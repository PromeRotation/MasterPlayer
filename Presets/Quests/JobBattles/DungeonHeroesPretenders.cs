using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsHeroesPretenders => territory == JobQuestCatalog.HEROES_AND_PRETENDERS;
    private readonly List<(long Id, ulong Source, Vector3 Next, Vector3 Advance, DateTime Activation, int Remaining)> heroesFlurries = [];
    private readonly Dictionary<ulong, long> heroesFlurryCasts = [];
    private readonly List<DungeonAmbientAoe> heroesFlurryAoes = [];
    private readonly List<(DungeonActor Source, DungeonActor Target, DungeonPendingCast Cast, bool TargetRemoved)> heroesArrows = [];
    private long heroesFlurryGeneration;
    internal IReadOnlyList<(long Id, ulong Source, Vector3 Next, Vector3 Advance, DateTime Activation, int Remaining)> HeroesFlurries => heroesFlurries;
    private bool heroesPretendersWiping;
    private readonly HashSet<ulong> heroesPretendersCompleted = [];
    private readonly Dictionary<ulong, uint> heroesPretendersActorOids = [];

    private void HeroesPretendersInput()
    {
        if (!IsHeroesPretenders) return;
        foreach (var id in heroesPretendersActorOids.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
        {
            heroesPretendersActorOids.Remove(id);
            heroesPretendersCompleted.Remove(id);
        }
        foreach (var actor in _actors.Values.ToArray())
        {
            if (heroesPretendersActorOids.TryGetValue(actor.Id, out var oid))
                _actors[actor.Id] = actor with { Oid = oid };
            else
                heroesPretendersActorOids[actor.Id] = actor.Oid;
            if (actor.Id == PrimaryId && Active != null && (actor.Dead || actor.Hp == 0)
                || actor.Oid == 0x428A && actor.Targetable && actor.InCombat && (actor.Dead || actor.Hp == 0))
                heroesPretendersCompleted.Add(actor.Id);
        }
    }

    private void HeroesPretendersDirector(uint update)
    {
        if (!IsHeroesPretenders || update is not (0x40000005 or 0x40000011)) return;
        heroesPretendersWiping = update == 0x40000005;
        if (heroesPretendersWiping)
        {
            ResetFight();
            heroesPretendersCompleted.Clear();
        }
    }

    private void HeroesPretendersCast(DungeonPendingCast cast)
    {
        if (!IsHeroesPretenders) return;
        if (cast.Rule.Action == 37467)
        {
            var index = heroesFlurryCasts.TryGetValue(cast.Source, out var id)
                ? heroesFlurries.FindIndex(l => l.Id == id && l.Remaining == 8) : -1;
            if (heroesFlurryCasts.ContainsKey(cast.Source) && index < 0) return;
            var line = (index < 0 ? ++heroesFlurryGeneration : heroesFlurries[index].Id, cast.Source,
                cast.SourcePosition ?? _actors[cast.Source].Position,
                new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation)) * 5, cast.FinishAt, 8);
            if (index < 0) heroesFlurries.Add(line);
            else heroesFlurries[index] = line;
            heroesFlurryCasts[cast.Source] = line.Item1;
            RebuildHeroesFlurries();
        }
        if (cast.Rule.Action != 37459 || !_actors.TryGetValue(cast.Source, out var source)) return;
        var arrow = heroesArrows.FindIndex(b => b.Source.Id == cast.Source);
        if (arrow >= 0)
        {
            var previous = heroesArrows[arrow];
            heroesArrows[arrow] = (previous.Source, previous.Target, cast, previous.TargetRemoved);
            return;
        }
        if (_actors.TryGetValue(cast.TargetId, out var target))
            heroesArrows.Add((source, target, cast, false));
    }

    private void HeroesPretendersFinish(DungeonPendingCast cast)
    {
        if (!IsHeroesPretenders) return;
        if (cast.Rule.Action == 37467) heroesFlurryCasts.Remove(cast.Source);
        if (cast.Rule.Action != 37459) return;
        var index = heroesArrows.FindIndex(b => b.Source.Id == cast.Source);
        if (index >= 0) heroesArrows.RemoveAt(index);
    }

    private void HeroesPretendersObserve()
    {
        if (!IsHeroesPretenders || Active == null) return;
        for (var i = 0; i < heroesArrows.Count; ++i)
        {
            var arrow = heroesArrows[i];
            var source = _actors.GetValueOrDefault(arrow.Source.Id);
            var target = !arrow.TargetRemoved ? _actors.GetValueOrDefault(arrow.Target.Id) : null;
            heroesArrows[i] = (source ?? arrow.Source, target ?? arrow.Target, arrow.Cast,
                arrow.TargetRemoved || target == null);
        }
    }

    private void HeroesPretendersEffect(ulong source, uint action, DateTime time)
    {
        if (!IsHeroesPretenders || action is not (37467 or 37468) || !_actors.TryGetValue(source, out var caster)) return;
        var index = heroesFlurries.FindIndex(l => MathF.Abs(l.Next.X - caster.Position.X) <= 1
            && MathF.Abs(l.Next.Z - caster.Position.Z) <= 1);
        if (index < 0) return;
        var line = heroesFlurries[index];
        if (line.Remaining == 1) heroesFlurries.RemoveAt(index);
        else heroesFlurries[index] = line with
        {
            Next = caster.Position + line.Advance,
            Activation = time.AddSeconds(1.1d),
            Remaining = line.Remaining - 1,
        };
        RebuildHeroesFlurries();
    }

    private void RebuildHeroesFlurries()
    {
        heroesFlurryAoes.Clear();
        foreach (var line in heroesFlurries)
        {
            var start = line.Activation > _now ? line.Activation : _now;
            for (var i = 1; i < Math.Min(line.Remaining, 3); ++i)
                heroesFlurryAoes.Add(new($"heroes-pretenders/flurry/{line.Id}/{i}", "Foreseen Flurry", new CircleTemplate(4),
                    QuantizedDungeonPosition(line.Next + line.Advance * i), Activation: start.AddSeconds(i * 1.1d)));
        }
        foreach (var line in heroesFlurries)
            heroesFlurryAoes.Add(new($"heroes-pretenders/flurry/{line.Id}/0", "Foreseen Flurry", new CircleTemplate(4),
                QuantizedDungeonPosition(line.Next), Activation: line.Activation, Color: new(1, 0, 0, 1)));
    }

    private IEnumerable<DungeonAmbientAoe> HeroesPretendersAoes()
    {
        if (!IsHeroesPretenders || Active == null) yield break;
        foreach (var aoe in heroesFlurryAoes) yield return aoe;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var (arrow, index) in heroesArrows.Where(b => !b.Source.Dead).Select((b, i) => (b, i)))
        {
            var self = !arrow.TargetRemoved && arrow.Target.Id == PlayerId;
            yield return new($"heroes-pretenders/arrow/{index}/display", "Unbound Arrow", new CircleTemplate(5),
                arrow.Target.Position, Navigate: false, OutlineOnly: arrow.Target.Id == PlayerId);
            if (!self)
                yield return new($"heroes-pretenders/arrow/{index}/avoid", "Unbound Arrow", new CircleTemplate(5),
                    arrow.Target.Position, Display: false, Activation: arrow.Cast.FinishAt);
            else if (arrow.Source.Id != arrow.Target.Id)
                foreach (var member in party.Where(a => a.Id != PlayerId))
                    yield return new($"heroes-pretenders/arrow/{index}/party/{member.Id}", "Bait away from raid", new CircleTemplate(5),
                        member.Position, Display: false, Activation: arrow.Cast.FinishAt);
        }
    }

    private IEnumerable<DungeonAmbientAoe> HeroesPretendersPartyAoes()
    {
        if (Arena == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var spreads = jobQuestSpreads.Where(s => !s.Target.Dead).ToArray();
        foreach (var (mark, index) in spreads.Select((s, i) => (s, i)))
        {
            var position = QuantizedDungeonPosition(mark.Target.Position);
            yield return new($"quest/spread/{index}/display", "Acid Rain", new CircleTemplate(8), position,
                Navigate: false, OutlineOnly: true);
            if (party.Length < 2) continue;
            if (mark.Target.Id != PlayerId || mark.Removed)
                yield return new($"quest/spread/{index}/avoid", "Acid Rain", new CircleTemplate(9), position,
                    Display: false, Activation: mark.Cast.FinishAt);
            else
                foreach (var member in party.Where(a => !spreads.Any(s => !s.Removed && s.Target.Id == a.Id)))
                    yield return new($"quest/spread/{index}/separate/{member.Id}", "Acid Rain", new CircleTemplate(9),
                        QuantizedDungeonPosition(member.Position), Display: false, Activation: mark.Cast.FinishAt);
        }
        // GoldDust and AcidRain are independent source components. Their target sets do not cross-filter.
        var stacks = jobQuestStacks.Where(s => !s.Target.Dead).ToArray();
        var marked = stacks.Any(s => !s.Removed && s.Target.Id == PlayerId);
        var storedMark = jobQuestStacks.Any(s => !s.Removed && s.Target.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var (mark, index) in stacks.Select((s, i) => (s, i)))
        {
            var position = QuantizedDungeonPosition(mark.Target.Position);
            var circle = new CircleGeometry(position, 8);
            var count = party.Count(a => circle.Contains(a.Position, 0));
            var inside = _actors.GetValueOrDefault(PlayerId) is { } viewer && circle.Contains(viewer.Position, 0);
            var canJoin = inside ? count <= 2 : count < 2;
            var self = !mark.Removed && mark.Target.Id == PlayerId;
            yield return new($"quest/stack/{index}/display", "Gold Dust / 恰好2人分摊", new CircleTemplate(8), position,
                Navigate: false, OutlineOnly: true, Safe: self || !storedMark && canJoin);
            if (party.Length < 2) continue;
            if (self)
            {
                var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(s => !s.Removed && s.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 4)).ToArray();
                if (buddies.Length > 0)
                    yield return new($"quest/stack/{index}/buddy", "寻找双人分摊同伴", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: mark.Cast.FinishAt);
            }
            else if (canJoin)
                joins.Add(circle);
            else
                yield return new($"quest/stack/{index}/avoid", "避免分摊超员", new CircleTemplate(marked ? 16 : 8), position,
                    Display: false, Activation: mark.Cast.FinishAt);
        }
        if (joins.Count > 0)
            yield return new("quest/stack/join", "进入一个双人分摊", new DungeonExclusiveJoinTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].Cast.FinishAt);
    }

    private IEnumerable<DungeonDamagePrediction> HeroesPretendersDamage()
    {
        if (!IsHeroesPretenders || Active == null) yield break;
        foreach (var arrow in heroesArrows.Where(b => !b.Source.Dead).DistinctBy(b => b.Target.Id))
            if (_actors.GetValueOrDefault(arrow.Target.Id) is { PartySlot: >= 0 } member)
                yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, heroesArrows[0].Cast.FinishAt, "Tankbuster");
    }

    private IEnumerable<string> HeroesPretendersHints(Vector3? player)
    {
        if (!IsHeroesPretenders || Active == null || player is not { } p) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var spreads = jobQuestSpreads.Where(s => !s.Target.Dead).ToArray();
        if (spreads.Any(s => !s.Removed && s.Target.Id == PlayerId)) yield return "Spread!";
        if (spreads.Any(s => (s.Removed || s.Target.Id != PlayerId)
            && NativeEnemyDistanceSquared(p, QuantizedDungeonPosition(s.Target.Position)) <= 64))
            yield return "GTFO from spreads!";
        var stacks = jobQuestStacks.Where(s => !s.Target.Dead).ToArray();
        var own = stacks.FirstOrDefault(s => !s.Removed && s.Target.Id == PlayerId);
        if (own.Cast != null)
        {
            var count = 1 + party.Count(a => a.Id != PlayerId
                && NativeEnemyDistanceSquared(a.Position, QuantizedDungeonPosition(own.Target.Position)) <= 64);
            yield return $"Stack! ({count}/2)";
        }
        else if (stacks.Any(s => NativeEnemyDistanceSquared(p, QuantizedDungeonPosition(s.Target.Position)) <= 64
            || 1 + party.Count(a => (a.Id != s.Target.Id || s.Removed)
                && NativeEnemyDistanceSquared(a.Position, QuantizedDungeonPosition(s.Target.Position)) <= 64) < 2))
            yield return "Stack!";
        if (heroesArrows.Count > 0) yield return "Tankbuster cleave";
        if (heroesArrows.Any(b => !b.Source.Dead && b.Target.Id == PlayerId
            && _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId
                && NativeEnemyDistanceSquared(a.Position, b.Target.Position) <= 25)))
            yield return "Bait away from raid!";
        if (heroesArrows.Any(b => !b.Source.Dead && b.Target.Id != PlayerId
            && NativeEnemyDistanceSquared(p, b.Target.Position) <= 25))
            yield return "GTFO from baited aoe!";
    }

    private void ResetHeroesPretenders()
    {
        heroesFlurries.Clear();
        heroesFlurryCasts.Clear();
        heroesFlurryAoes.Clear();
        heroesArrows.Clear();
    }
}
