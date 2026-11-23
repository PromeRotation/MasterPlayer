using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsThornmarch => territory == ThornmarchCatalog.TERRITORY;
    private readonly List<DungeonPendingCast> thornRounds = [];
    private readonly List<(DungeonPendingCast Cast, DungeonActor Target)> thornParty = [];
    private DungeonPendingCast? thornTankbuster;
    private DungeonActor? thornTankTarget;
    private readonly List<(Vector3 Position, int Stage, DateTime At, int Key, ulong Source, DateTime Start)> thornStones = [];
    private readonly HashSet<(ulong Source, uint Action, DateTime Start)> thornStarts = [];
    private uint thornActiveTowers;
    private uint thornSoakedTowers;
    private DateTime thornTowerAt;
    private double thornTowerDelay = 8;
    private int thornComets;
    private int thornStoneKey;
    internal string CurrentAssignment { get; set; } = "Unassigned";

    private int ThornTowerAssignment() => CurrentAssignment switch
    {
        "H1" => 0, "H2" => 1, "R1" => 2, "R2" => 3, _ => -1,
    };

    private static bool ThornTank(DungeonActor actor)
        => actor.PartyRole == "Tank" || actor.ClassJob is 1 or 3 or 19 or 21 or 32 or 37;

    private void ThornCast(DungeonPendingCast cast)
    {
        if (!IsThornmarch) return;
        var fresh = thornStarts.Add((cast.Source, cast.Rule.Action, cast.StartedAt));
        if (cast.Rule.Action is 29196 or 29197)
        {
            var index = thornRounds.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt);
            if (index < 0) thornRounds.Add(cast);
            else thornRounds[index] = cast;
        }
        if (cast.Rule.Action is 29192 or 29204 && _actors.TryGetValue(cast.TargetId, out var target))
        {
            var index = thornParty.FindIndex(p => p.Cast.Source == cast.Source && p.Cast.StartedAt == cast.StartedAt);
            if (index < 0) thornParty.Add((cast, target));
            else thornParty[index] = (cast, target);
        }
        if (cast.Rule.Action == 29206)
        {
            thornTankbuster = cast;
            thornTankTarget = _actors.GetValueOrDefault(cast.TargetId);
        }
        if (cast.Rule.Action == 29201)
        {
            var index = thornStones.FindIndex(s => s.Source == cast.Source && s.Start == cast.StartedAt && s.Stage == 0);
            if (index >= 0)
            {
                var stone = thornStones[index];
                thornStones[index] = (cast.Location, stone.Stage, cast.FinishAt, stone.Key, stone.Source, stone.Start);
            }
            else if (fresh) thornStones.Add((cast.Location, 0, cast.FinishAt, ++thornStoneKey, cast.Source, cast.StartedAt));
        }
        if (!fresh) return;
        if (cast.Rule.Action == 29190) { thornTowerDelay = 16; thornComets = 3; }
        if (cast.Rule.Action == 29199) --thornComets;
    }

    private void ThornObserve()
    {
        if (!IsThornmarch) return;
        for (var i = 0; i < thornParty.Count; ++i)
        {
            var p = thornParty[i];
            if (_actors.TryGetValue(p.Target.Id, out var target)) thornParty[i] = (p.Cast, target);
        }
        if (thornTankTarget != null && _actors.TryGetValue(thornTankTarget.Id, out var current)) thornTankTarget = current;
    }

    private void ThornFinish(DungeonPendingCast cast, DateTime at)
    {
        if (!IsThornmarch) return;
        if (cast.Rule.Action is 29196 or 29197)
        {
            var index = thornRounds.FindIndex(c => c.Source == cast.Source);
            if (index >= 0) thornRounds.RemoveAt(index);
        }
        if (cast.Rule.Action is 29192 or 29204)
        {
            var index = thornParty.FindIndex(p => p.Cast.Rule.Action == cast.Rule.Action && p.Target.Id == cast.TargetId);
            if (index >= 0) thornParty.RemoveAt(index);
        }
        if (cast.Source == thornTankbuster?.Source) { thornTankbuster = null; thornTankTarget = null; }
        var stage = cast.Rule.Action switch { 29201 => 0, 29202 => 1, 29619 => 2, _ => -1 };
        if (stage < 0) return;
        var stoneIndex = thornStones.FindIndex(s => s.Stage == stage
            && MathF.Abs(s.Position.X - cast.Location.X) <= 1 && MathF.Abs(s.Position.Z - cast.Location.Z) <= 1);
        if (stoneIndex < 0) return;
        var stone = thornStones[stoneIndex];
        if (stage == 2) thornStones.RemoveAt(stoneIndex);
        else thornStones[stoneIndex] = (stone.Position, stage + 1, at.AddSeconds(2), stone.Key, stone.Source, stone.Start);
    }

    private void ThornMap(uint index, uint state, DateTime at)
    {
        if (!IsThornmarch || Active == null || index is < 8 or >= 16) return;
        var bit = 1u << (int)(index - 8);
        switch (state)
        {
            case 0x00020001: thornActiveTowers |= bit; thornTowerAt = at.AddSeconds(thornTowerDelay); break;
            case 0x00200010: thornSoakedTowers |= bit; break;
            case 0x00400001: thornSoakedTowers &= ~bit; break;
            case 0x00080004: thornActiveTowers &= ~bit; thornSoakedTowers &= ~bit; break;
        }
    }

    private Vector3 ThornTowerPosition(int index) => Arena!.Position
        + new Vector3(MathF.Sin(MathF.PI / 4 * index), 0, MathF.Cos(MathF.PI / 4 * index)) * 10;

    private IEnumerable<DungeonAmbientAoe> ThornAoes()
    {
        if (!IsThornmarch || Active == null || Arena == null) yield break;
        foreach (var enemy in _actors.Values.Where(a => ThornmarchCatalog.IsEnemy(a.Oid)))
            yield return new($"thorn/enemy/{enemy.Id}", "莫古力", new CircleTemplate(enemy.HitboxRadius), enemy.Position,
                Navigate: false, OutlineOnly: true);
        var rounds = thornRounds.Where(c => _actors.ContainsKey(c.Source)).ToArray();
        foreach (var cast in rounds.Take(2))
            yield return new($"thorn/round/{cast.Source}", "莫古力转圈", new CircleTemplate(20),
                _actors[cast.Source].Position, Activation: cast.FinishAt);
        if (rounds.Length > 2)
        {
            var first = _actors[rounds[0].Source].Position;
            yield return new("thorn/round/near", "为第三次转圈保留站位", new SohrKhaiWallHintTemplate(Arena,
                [new CircleGeometry(first, 23)]), Arena.Position, Display: false, Activation: rounds[1].FinishAt);
            yield return new("thorn/round/third", "远离第三次转圈中心", new CircleTemplate(10),
                _actors[rounds[2].Source].Position, Display: false, Activation: rounds[1].FinishAt);
        }
        foreach (var stone in thornStones)
            yield return new($"thorn/stone/{stone.Key}", "莫古石连续环形", stone.Stage switch
            {
                0 => new CircleTemplate(10), 1 => new DonutTemplate(10, 20), _ => new DonutTemplate(20, 30),
            }, stone.Position, Activation: stone.At);
        foreach (var aoe in ThornTowerAoes()) yield return aoe;
        foreach (var aoe in ThornPartyAoes()) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> ThornTowerAoes()
    {
        if (Arena == null || thornActiveTowers == 0) yield break;
        for (var i = 0; i < 8; ++i)
        {
            if ((thornActiveTowers & (1u << i)) == 0) continue;
            yield return new($"thorn/tower/{i}/display", "陨石塔", new CircleTemplate(5), ThornTowerPosition(i),
                Navigate: false, OutlineOnly: true, Safe: (thornSoakedTowers & (1u << i)) != 0);
            if (thornComets > 0)
                yield return new($"thorn/tower/{i}/comet", "彗星期间远离塔", new CircleTemplate(6), ThornTowerPosition(i), Display: false);
        }
        if (thornComets > 0) yield break;
        var assignment = ThornTowerAssignment();
        if (assignment >= 0)
        {
            if ((thornActiveTowers & (1u << assignment)) == 0) assignment += 4;
            yield return new("thorn/tower/join", "进入分工陨石塔", new SohrKhaiWallHintTemplate(Arena,
                [new CircleGeometry(ThornTowerPosition(assignment), 5)]), Arena.Position, Display: false, Activation: thornTowerAt);
        }
        else
            for (var i = 0; i < 8; ++i)
                if ((thornActiveTowers & (1u << i)) != 0)
                    yield return new($"thorn/tower/{i}/avoid", "将陨石塔留给分工队友", new CircleTemplate(5),
                        ThornTowerPosition(i), Display: false, Activation: thornTowerAt);
    }

    private IEnumerable<DungeonAmbientAoe> ThornPartyAoes()
    {
        if (Arena == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var spreads = thornParty.Where(p => p.Cast.Rule.Action == 29192 && !p.Target.Dead).ToArray();
        var stacks = thornParty.Where(p => p.Cast.Rule.Action == 29204 && !p.Target.Dead).ToArray();
        foreach (var (cast, target) in spreads)
        {
            yield return new($"thorn/spread/{cast.Source}/display", "六码分散", new CircleTemplate(6), target.Position,
                Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new($"thorn/spread/{cast.Source}/avoid", "七码分散间距", new CircleTemplate(7), target.Position,
                    Display: false, Activation: cast.FinishAt);
            else
                foreach (var buddy in party.Where(a => a.Id != PlayerId && !spreads.Any(s => s.Target.Id == a.Id)))
                    yield return new($"thorn/spread/{cast.Source}/{buddy.Id}", "分散远离队友", new CircleTemplate(7),
                        buddy.Position, Display: false, Activation: cast.FinishAt);
        }
        var marked = stacks.Any(s => s.Target.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var (cast, target) in stacks)
        {
            var circle = new CircleGeometry(target.Position, 6);
            var inside = party.Count(p => circle.Contains(p.Position));
            var playerInside = _actors.GetValueOrDefault(PlayerId) is { } player && circle.Contains(player.Position);
            var safe = target.Id == PlayerId || !marked && (playerInside ? inside <= 8 : inside < 8);
            yield return new($"thorn/stack/{cast.Source}/display", "八人分摊", new CircleTemplate(6), target.Position,
                Navigate: false, OutlineOnly: true, Safe: safe);
            if (target.Id == PlayerId)
            {
                var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(s => s.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                if (buddies.Length > 0)
                    yield return new($"thorn/stack/{cast.Source}/buddy", "分摊点名靠拢队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: cast.FinishAt);
            }
            else if (safe) joins.Add(circle);
            else
                yield return new($"thorn/stack/{cast.Source}/avoid", "避免重叠或超员分摊", new CircleTemplate(marked ? 12 : 6),
                    target.Position, Display: false, Activation: cast.FinishAt);
        }
        if (joins.Count > 0)
            yield return new("thorn/stack/join", "进入八人分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].Cast.FinishAt);
        if (thornTankbuster is not { } buster || thornTankTarget is not { } tankTarget) yield break;
        var tank = _actors.GetValueOrDefault(PlayerId) is { } current && ThornTank(current);
        yield return new("thorn/tank/display", "双坦分摊死刑", new CircleTemplate(6), tankTarget.Position,
            Navigate: false, OutlineOnly: tank, Safe: tank && tankTarget.Id != PlayerId);
        if (tankTarget.Id == PlayerId)
        {
            foreach (var member in party.Where(a => a.Id != PlayerId && !ThornTank(a)))
                yield return new($"thorn/tank/avoid/{member.Id}", "坦克远离非坦队友", new CircleTemplate(6),
                    member.Position, Display: false, Activation: buster.FinishAt);
        }
        else if (tank)
            yield return new("thorn/tank/join", "双坦分摊死刑", new SohrKhaiWallHintTemplate(Arena,
                [new CircleGeometry(tankTarget.Position, 6)]), Arena.Position, Display: false, Activation: buster.FinishAt);
        else
            yield return new("thorn/tank/avoid", "远离坦克分摊", new CircleTemplate(6), tankTarget.Position,
                Display: false, Activation: buster.FinishAt);
    }

    private IEnumerable<DungeonDamagePrediction> ThornDamage()
    {
        if (!IsThornmarch) yield break;
        if (thornTankbuster != null)
            foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && ThornTank(a)))
                yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, thornTankbuster.FinishAt, "Tankbuster");
    }

    private IEnumerable<string> ThornHints(Vector3? player)
    {
        if (!IsThornmarch || Active == null) yield break;
        if (thornActiveTowers != 0)
        {
            if (thornComets > 0) yield return $"彗星剩余 {thornComets} 次：远离陨石塔";
            else if (ThornTowerAssignment() >= 0) yield return "按分工进入陨石塔";
            else yield return "避开陨石塔";
        }
        if (thornTankTarget is { } tankTarget)
        {
            if (tankTarget.Id == PlayerId) yield return "与另一坦克分摊或使用无敌";
            else if (_actors.GetValueOrDefault(PlayerId) is { } current && ThornTank(current)) yield return "与坦克分摊";
            else yield return "远离坦克分摊";
        }
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var (cast, target) in thornParty.Where(p => !p.Target.Dead))
        {
            var circle = new CircleGeometry(target.Position, 6);
            if (cast.Rule.Action == 29204 && player is { } p
                && (target.Id == PlayerId ? party.Count(a => circle.Contains(a.Position)) != 8 : !circle.Contains(p)))
                yield return "八人分摊";
            if (cast.Rule.Action == 29192 && (target.Id == PlayerId
                ? party.Any(a => a.Id != PlayerId && circle.Contains(a.Position))
                : player is { } position && circle.Contains(position)))
                yield return "分散远离队友";
        }
    }

    private void ResetThorn()
    {
        thornRounds.Clear(); thornParty.Clear(); thornStones.Clear(); thornStarts.Clear();
        thornTankbuster = null; thornTankTarget = null;
        thornActiveTowers = 0; thornSoakedTowers = 0; thornTowerAt = default;
        thornTowerDelay = 8; thornComets = 0; thornStoneKey = 0;
    }
}
