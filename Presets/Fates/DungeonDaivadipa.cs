using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsDaivadipa => territory == NativeFateCatalog.DAIVADIPA;
    private readonly List<DaivadipaOrbAoe> daivadipaLit = [];
    private readonly List<DaivadipaOrbAoe> daivadipaBurn = [];
    private readonly List<ulong> daivadipaHalfCasters = [];
    private readonly Dictionary<uint, List<ulong>> daivadipaOrbOrder = [];
    private readonly Dictionary<ulong, uint> daivadipaActorOrder = [];
    private readonly TreasurePlayerStatusState daivadipaStatuses = new([1958, 1960, 1959, 1961, 1257, 3629]);
    private DateTime? daivadipaRaidwide;

    private void DaivadipaObserveActors()
    {
        if (!IsDaivadipa) return;
        foreach (var id in daivadipaActorOrder.Keys.Where(id => !_actors.ContainsKey(id)).ToArray())
            daivadipaActorOrder.Remove(id);
        foreach (var actor in _actors.Values)
        {
            if (!daivadipaActorOrder.ContainsKey(actor.Id))
                daivadipaStatuses.BeginActorGeneration(actor.Id, _now);
            daivadipaActorOrder[actor.Id] = actor.Oid;
        }
        foreach (var (oid, order) in daivadipaOrbOrder)
        {
            order.RemoveAll(id => !_actors.TryGetValue(id, out var actor) || actor.Oid != oid);
            foreach (var id in daivadipaActorOrder.Where(a => a.Value == oid).Select(a => a.Key))
                if (!order.Contains(id)) order.Add(id);
        }
    }

    private IEnumerable<DungeonActor> DaivadipaOrbs(uint oid)
    {
        if (!daivadipaOrbOrder.TryGetValue(oid, out var order))
            daivadipaOrbOrder[oid] = order = daivadipaActorOrder.Where(a => a.Value == oid).Select(a => a.Key).ToList();
        return order.Select(id => _actors[id]);
    }

    private void DaivadipaCast(DungeonPendingCast cast, bool correction)
    {
        if (!IsDaivadipa || correction) return;
        if (cast.Rule.Action is 26508 or 26509 && !daivadipaHalfCasters.Contains(cast.Source))
            daivadipaHalfCasters.Add(cast.Source);
        if (cast.Rule.Action == 26517) daivadipaRaidwide = cast.FinishAt.AddSeconds(1);
        if (cast.Rule.Action is not (26499 or 26498)) return;
        var blue = cast.Rule.Action == 26499;
        Add(0x3570, daivadipaLit, new RayRectangleTemplate(-5, 5, 50), blue ? 2.2 : 4.4, true);
        Add(0x356F, daivadipaLit, new RayRectangleTemplate(-5, 5, 50), blue ? 4.4 : 2.2, true);
        Add(0x3572, daivadipaBurn, new CircleTemplate(10), blue ? 2.2 : 6.2, false);
        Add(0x3571, daivadipaBurn, new CircleTemplate(10), blue ? 6.2 : 2.2, false);
        if (!blue)
        {
            daivadipaLit.Reverse();
            daivadipaBurn.Reverse();
        }

        void Add(uint oid, List<DaivadipaOrbAoe> list, GeometryTemplate shape, double delay, bool directional)
        {
            foreach (var orb in DaivadipaOrbs(oid))
            {
                var rotation = 0f;
                if (directional) rotation = orb.Position.X < -632 ? 89.999f * MathF.PI / 180 : MathF.PI;
                list.Add(new(orb, shape, QuantizedDungeonPosition(orb.Position), rotation, cast.FinishAt.AddSeconds(delay)));
            }
        }
    }

    private void DaivadipaFinish(DungeonPendingCast cast)
    {
        if (!IsDaivadipa) return;
        daivadipaHalfCasters.Remove(cast.Source);
        if (cast.Rule.Action is 26501 or 26500 && daivadipaLit.Count > 0) daivadipaLit.RemoveAt(0);
        if (cast.Rule.Action is 26507 or 26506 && daivadipaBurn.Count > 0) daivadipaBurn.RemoveAt(0);
    }

    private IEnumerable<(Vector3 From, Vector3 To, float Rotation)> DaivadipaMovements =>
        IsDaivadipa && _actors.GetValueOrDefault(PlayerId) is { } actor
            ? daivadipaStatuses.Movements(TreasureStatusActor(actor), _now) : [];

    private IEnumerable<DungeonAmbientAoe> DaivadipaAoes()
    {
        if (!IsDaivadipa || Active == null) yield break;
        var lit = daivadipaLit.TakeWhile(a => a.Activation < daivadipaLit[0].Activation.AddSeconds(1)).ToArray();
        foreach (var (aoe, index) in lit.Select((a, i) => (a, i)))
            yield return new($"fate/lit/{index}", "Lit Path", aoe.Shape, aoe.Position, aoe.Rotation, Activation: aoe.Activation);
        foreach (var (aoe, index) in daivadipaBurn.Take(8).Select((a, i) => (a, i)))
            yield return new($"fate/burn/{index}", "Burn", aoe.Shape, aoe.Position, aoe.Rotation, Activation: aoe.Activation);
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var segment = 0;
        foreach (var move in DaivadipaMovements)
            foreach (var aoe in QuestKnockbackProjection($"fate/march/{segment++}", player, move.From, move.To))
                yield return aoe with { Name = "Divine Call / Forced march", Navigate = false,
                    Rotation = aoe.Id.EndsWith("/player") ? move.Rotation : aoe.Rotation };
    }

    private bool DaivadipaUnsafe(Vector3 position)
    {
        if (daivadipaHalfCasters.Count > 0 && _casts.GetValueOrDefault(daivadipaHalfCasters[0]) is { } half
            && half.Rule.Shape!.Resolve(half.Location, half.Rotation, null).Contains(position, 0)) return true;
        return daivadipaLit.Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position, 0));
    }

    private IEnumerable<string> DaivadipaHints(Vector3 player)
    {
        if (daivadipaRaidwide != null) yield return "Raidwide";
        var action = _actors.GetValueOrDefault(PrimaryId)?.Cast?.Action;
        var direction = action switch
        {
            27080 => "backwards", 26520 => "right", 27079 => "fowards", 26519 => "left", _ => null
        };
        if (direction != null) yield return $"Apply {direction} march debuff";
        if (DaivadipaAoes().Where(a => a.Navigate).Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player, 0)))
            yield return "GTFO from aoe!";
        var last = DaivadipaMovements.LastOrDefault();
        if (last.From == last.To) yield break;
        if (daivadipaHalfCasters.Count > 0)
        {
            if (DaivadipaUnsafe(last.To)) yield return "Aim for safe spot!";
        }
        else if (daivadipaLit.Count > 0)
            yield return DaivadipaUnsafe(last.To) ? "Aim into AOEs!" : "Aim into AOEs";
    }

    private IEnumerable<DungeonDamagePrediction> FateDamage()
    {
        foreach (var prediction in FateBaitDamage()) yield return prediction;
        if ((IsChi || IsMica || IsTtokrrone) && Active != null)
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 25969 or 38707 or 37339))
                foreach (var actor in FateParty)
                    yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, cast.FinishAt, "Raidwide");
        if (IsFormidable && Active != null)
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 17393))
                foreach (var actor in FateParty)
                    yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, cast.FinishAt, "Raidwide");
        if (!IsDaivadipa || Active == null) yield break;
        foreach (var pending in _casts.Values.Where(c => c.Rule.Action == 26510))
        {
            var caster = _actors.GetValueOrDefault(pending.Source);
            var cast = caster?.Cast;
            var target = cast?.TargetId ?? pending.TargetId;
            if (target == pending.Source) target = caster?.TargetId ?? 0;
            if (FateParty.FirstOrDefault(a => a.Id == target) is { } actor)
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name,
                    cast?.FinishAt ?? pending.FinishAt, "Tankbuster");
        }
        if (daivadipaRaidwide is not { } activation) yield break;
        foreach (var actor in FateParty)
            yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, activation, "Raidwide");
    }

    private void ResetDaivadipa()
    {
        daivadipaLit.Clear();
        daivadipaBurn.Clear();
        daivadipaHalfCasters.Clear();
        daivadipaOrbOrder.Clear();
        daivadipaStatuses.Clear();
        daivadipaRaidwide = null;
    }
}
