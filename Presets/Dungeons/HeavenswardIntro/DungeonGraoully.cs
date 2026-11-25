using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly HashSet<ulong> graoullyObservedPrey = [];
    private readonly Dictionary<int, ulong> graoullyPartners = [];
    private readonly Dictionary<ulong, ushort> graoullyIce = [];
    private ulong graoullyPrey;
    private bool IsGraoully => territory == HeavenswardIntroCatalog.GRAOULLY;

    private void GraoullyStatus(ulong target, uint status, bool present, ushort param = 0)
    {
        if (!IsGraoully) return;
        if (status == 905)
        {
            graoullyIce[target] = present ? param : (ushort)0;
            return;
        }
        if (status != 904) return;
        if (present)
        {
            graoullyPrey = target;
            graoullyObservedPrey.Add(target);
        }
        else
        {
            graoullyObservedPrey.Remove(target);
            if (graoullyPrey == target) graoullyPrey = 0;
        }
    }

    private void SynchronizeGraoully()
    {
        if (!IsGraoully || Active == null) return;
        foreach (var (slot, id) in graoullyPartners.ToArray())
            if (!_actors.TryGetValue(id, out var actor) || !GraoullyFriendlyNpc(actor))
                graoullyPartners.Remove(slot);
        foreach (var actor in _actors.Values)
        {
            if (!GraoullyFriendlyNpc(actor) || actor.PartySlot is >= 0 and < 24
                || graoullyPartners.ContainsValue(actor.Id)) continue;
            var slot = actor.PartySlot is >= 24 and < 64 ? actor.PartySlot
                : Enumerable.Range(24, 40).FirstOrDefault(s => !graoullyPartners.ContainsKey(s));
            if (slot >= 24) graoullyPartners[slot] = actor.Id;
        }
        foreach (var actor in _actors.Values)
        {
            var stacks = actor.StatusParams?.GetValueOrDefault(905u) ?? 0;
            if (statusChanges.TryGetValue((actor.Id, 905), out var change) && _now < change.ChangedAt.AddSeconds(.5))
                stacks = change.Present ? change.Param : (ushort)0;
            graoullyIce[actor.Id] = (ushort)stacks;
            var present = HasStatus(actor, 904);
            if (present && graoullyObservedPrey.Add(actor.Id)) graoullyPrey = actor.Id;
            else if (!present && graoullyObservedPrey.Contains(actor.Id)) GraoullyStatus(actor.Id, 904, false);
        }
        foreach (var id in graoullyObservedPrey.Where(id => !_actors.ContainsKey(id)).ToArray())
            GraoullyStatus(id, 904, false);
    }

    private static bool GraoullyFriendlyNpc(DungeonActor actor) => actor.FriendlyNpc && actor.Targetable
        && !IgnoredFriendlyNpc(actor.Oid);

    internal static bool IgnoredFriendlyNpc(uint oid) => oid is 0xE19 or 0xE18 or 0xE1A or 0x2C11 or 0x2C0F or 0x2C10 or 0x2C0E or 0x2C12 or 0x2EFE
            or 0x418F or 0x464E or 0x4697 or 0x35BC or 0x3657 or 0x3658 or 0x2ED7 or 0x2EDB or 0x2EDA
            or 0x2E92 or 0x2ECA or 0x2E94 or 0x2E96 or 0x2E90 or 0x3265 or 0x3264 or 0x31A8 or 0x391B or 0x3EFA;

    private IEnumerable<DungeonAmbientAoe> GraoullyAoes()
    {
        if (!IsGraoully || Active == null || !_actors.TryGetValue(graoullyPrey, out var prey)
            || !_actors.TryGetValue(PrimaryId, out var boss)) yield break;
        var shape = new FanTemplate(27, 130 * MathF.PI / 180);
        if (boss.Targetable)
            yield return new("graoully/prey/display", "Prey顺劈方向", shape, boss.Position,
                MathF.Atan2(prey.Position.X - boss.Position.X, prey.Position.Z - boss.Position.Z),
                Navigate: false, OutlineOnly: true);
        if (!_actors.TryGetValue(PlayerId, out var viewer)) yield break;
        DungeonActor? partner = null;
        if (viewer.PartySlot == 0)
            partner = _actors.GetValueOrDefault(graoullyPartners.GetValueOrDefault(24));
        else if (viewer.PartySlot >= 0 || graoullyPartners.ContainsValue(viewer.Id)) partner = viewer;
        if (partner == null) yield break;
        var stacks = graoullyIce.GetValueOrDefault(prey.Id);
        if ((stacks & 0xFF) == 3)
        {
            yield return new("graoully/prey/swap", "三层薄冰交接", new CircleTemplate(2), partner.Position,
                Display: false, Safe: true, GoalPriority: 1, OverrideDangerMargin: false);
        }
        else
        {
            yield return new("graoully/prey/separate", "避免提前交接", new CircleTemplate(5), partner.Position,
                Display: false, Activation: _now.AddSeconds(1));
            if (boss.Targetable)
                yield return new("graoully/prey/avoid-cleave", "Prey顺劈避让", shape, boss.Position,
                    MathF.Atan2(partner.Position.X - boss.Position.X, partner.Position.Z - boss.Position.Z),
                    Display: false, Activation: _now.AddSeconds(1));
        }
    }
}
