using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(ulong Id, DungeonActor Target, DateTime Activation, bool Removed)> fallenStacks = [];
    private readonly List<(ulong Id, DungeonActor Target, DateTime Activation, bool Removed)> fallenSpreads = [];
    private ulong fallenMarkId;
    private bool IsFallenKuribu => territory == JobQuestCatalog.OUR_UNSUNG_HEROES;

    private void FallenIcon(ulong target, uint icon, DateTime at)
    {
        if (!IsFallenKuribu || Active == null || icon is not (71 or 62) || !_actors.TryGetValue(target, out var actor)) return;
        var marks = icon == 71 ? fallenSpreads : fallenStacks;
        marks.Add((++fallenMarkId, actor, at.AddSeconds(5.15f), false));
    }

    private void FallenObserve(ulong removed = 0)
    {
        if (!IsFallenKuribu) return;
        foreach (var marks in new[] { fallenStacks, fallenSpreads })
            for (var i = 0; i < marks.Count; ++i)
            {
                var mark = marks[i];
                if (!mark.Removed && mark.Target.Id != removed && _actors.TryGetValue(mark.Target.Id, out var actor))
                    marks[i] = (mark.Id, actor, mark.Activation, false);
                else
                    marks[i] = (mark.Id, mark.Target, mark.Activation, true);
            }
        fallenSpreads.RemoveAll(m => m.Target.Dead);
    }

    private void FallenEffect(uint action, ulong target)
    {
        if (!IsFallenKuribu || action is not (8636 or 8637)) return;
        var marks = action == 8636 ? fallenSpreads : fallenStacks;
        var index = marks.FindIndex(m => m.Target.Id == target);
        if (index < 0 && marks.Count > 0) index = 0;
        if (index >= 0) marks.RemoveAt(index);
    }

    private IEnumerable<DungeonAmbientAoe> FallenAoes()
    {
        if (!IsFallenKuribu || Active == null || Arena == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var spreads = fallenSpreads.Where(m => !m.Target.Dead).ToArray();
        foreach (var mark in spreads)
        {
            var position = QuantizedDungeonPosition(mark.Target.Position);
            yield return new($"fallen/spread/{mark.Id}/display", "Cure III", new CircleTemplate(10), position,
                Navigate: false, OutlineOnly: true);
            if (party.Length < 2) continue;
            if (mark.Removed || mark.Target.Id != PlayerId)
                yield return new($"fallen/spread/{mark.Id}/avoid", "Cure III", new CircleTemplate(11), position,
                    Display: false, Activation: mark.Activation);
            else
                foreach (var member in party.Where(a => !spreads.Any(m => !m.Removed && m.Target.Id == a.Id)))
                    yield return new($"fallen/spread/{mark.Id}/separate/{member.Id}", "Cure III", new CircleTemplate(11),
                        QuantizedDungeonPosition(member.Position), Display: false, Activation: mark.Activation);
        }
        var stacks = fallenStacks.Where(m => !m.Target.Dead).ToArray();
        var viewerMarked = stacks.Any(m => !m.Removed && m.Target.Id == PlayerId);
        foreach (var mark in stacks)
        {
            yield return new($"fallen/stack/{mark.Id}/display", "Cure V", new CircleTemplate(6),
                QuantizedDungeonPosition(mark.Target.Position), Navigate: false, OutlineOnly: true,
                Safe: !viewerMarked || !mark.Removed && mark.Target.Id == PlayerId);
            if (party.Length < 2 || mark.Removed || mark.Target.Id != PlayerId) continue;
            // Cure III and Cure V are separate source components; the other component's marks do not exclude buddies.
            var buddies = party.Where(a => !stacks.Any(m => !m.Removed && m.Target.Id == a.Id))
                .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
            if (buddies.Length > 0)
                yield return new($"fallen/stack/{mark.Id}/buddy", "寻找分摊同伴", new SohrKhaiWallHintTemplate(Arena, buddies),
                    Arena.Position, Display: false, Activation: mark.Activation);
        }
        if (party.Length >= 2 && stacks.Length > 0)
        {
            var circles = stacks.Where(m => m.Removed || m.Target.Id != PlayerId)
                .Select(m => (IGeometryShape)new CircleGeometry(QuantizedDungeonPosition(m.Target.Position), 6)).ToArray();
            if (circles.Length > 0)
                yield return new("fallen/stack/join", "进入任一分摊圈", new SohrKhaiWallHintTemplate(Arena, circles),
                    Arena.Position, Display: false, Activation: stacks[0].Activation);
        }
    }

    private IEnumerable<DungeonDamagePrediction> FallenDamage()
    {
        if (!IsFallenKuribu || Active == null) yield break;
        var marks = fallenSpreads.Where(m => !m.Target.Dead).ToArray();
        if (marks.Length == 0) yield break;
        var activation = marks.Min(m => m.Activation);
        foreach (var mark in marks.DistinctBy(m => m.Target.Id))
            if (_actors.GetValueOrDefault(mark.Target.Id) is { PartySlot: >= 0 } member)
                yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, activation, "Raidwide");
    }

    private IEnumerable<string> FallenHints(Vector3? player)
    {
        if (!IsFallenKuribu || Active == null || player is not { } position) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var spreads = fallenSpreads.Where(m => !m.Target.Dead).ToArray();
        if (spreads.Any(m => !m.Removed && m.Target.Id == PlayerId) && party.Any(a => a.Id != PlayerId
            && NativeEnemyDistanceSquared(a.Position, QuantizedDungeonPosition(position)) <= 100))
            yield return "Spread!";
        if (spreads.Any(m => (m.Removed || m.Target.Id != PlayerId) && NativeEnemyDistanceSquared(position,
            QuantizedDungeonPosition(m.Target.Position)) <= 100))
            yield return "GTFO from spreads!";
        var stacks = fallenStacks.Where(m => !m.Target.Dead).ToArray();
        var participating = stacks.Count(m => NativeEnemyDistanceSquared(position, QuantizedDungeonPosition(m.Target.Position)) <= 36);
        if (participating > 0 || stacks.Any(m => 1 + party.Count(a => (m.Removed || a.Id != m.Target.Id)
            && NativeEnemyDistanceSquared(a.Position, QuantizedDungeonPosition(m.Target.Position)) <= 36) < 2))
            yield return "Stack!";
    }

    private ulong FallenPriorityTarget(ulong current, Vector3 player) => _actors.Values
        .Where(a => Active != null && a.IsEnemy == true && a.Targetable && !a.Dead && !HasStatus(a, 325)
            && (MathF.Abs(a.Position.Y - player.Y) > 12 || a.NameId == 541 || a.Hp == null
                || (long)a.Hp.Value + (a.IncomingEffects?.Sum(e => (long)e.HpDelta) ?? 0) > 1)
            && a.ObjectIndex is { } index && index < 200 && (index & 1) == 0)
        .OrderByDescending(a => a.Id == current).ThenBy(a => NativeEnemyDistanceSquared(player, a.Position))
        .Select(a => a.Id).FirstOrDefault();

    public ulong TargetToClear(ulong current)
    {
        if (IsQuantum && Quantum.Active && Quantum.CurrentSlot >= 0 && _actors.GetValueOrDefault(current) is { } target
            && Quantum.TargetPriority(target) < 0) return current;
        if ((!IsFallenKuribu && !IsSummoningNode && !IsMainScenarioQuest && !IsNativeTreasure) || Active == null) return 0;
        // A nonzero clear request keeps PR's selector from reacquiring an invincible target even when none is selected.
        return _actors.GetValueOrDefault(current)?.IsEnemy == true ? current : PrimaryId;
    }
}
