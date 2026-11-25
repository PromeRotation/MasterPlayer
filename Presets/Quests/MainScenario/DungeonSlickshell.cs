using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsSlickshell => territory == MainScenarioQuestCatalog.HIS_FORGOTTEN_HOME;
    private DungeonActor? slickshellPrimary;
    private bool slickshellPrimaryRemoved;
    private bool slickshellWiping;
    private readonly List<(DungeonActor Source, DungeonActor Target, bool SourceRemoved, bool Removed, bool IsSource, DateTime Activation)> slickshellBaits = [];

    internal bool UsesSlickshellNativeEffect(uint action) => IsSlickshell && action == 1087;

    private void SlickshellObserve()
    {
        if (!IsSlickshell || Active == null) return;
        if (_actors.TryGetValue(PrimaryId, out var primary) && primary.Oid == Active.BossOid)
        {
            slickshellPrimary = primary;
            slickshellPrimaryRemoved = false;
        }
        else if (!slickshellPrimaryRemoved)
            SlickshellRemoved(PrimaryId);
        for (var i = slickshellBaits.Count - 1; i >= 0; --i)
        {
            var bait = slickshellBaits[i];
            if (!bait.SourceRemoved)
            {
                var source = _actors.GetValueOrDefault(bait.Source.Id);
                bait.Source = source ?? bait.Source;
                bait.SourceRemoved = source == null;
            }
            if (!bait.Removed)
            {
                var target = _actors.GetValueOrDefault(bait.Target.Id);
                bait.Target = target ?? bait.Target;
                bait.Removed = target == null;
            }
            slickshellBaits[i] = bait;
            if (bait.Target.Dead) slickshellBaits.RemoveAt(i);
        }
    }

    private void SlickshellIcon(ulong targetId, ulong actorId, uint icon)
    {
        if (!IsSlickshell || Active == null || icon != 26 || slickshellPrimary == null) return;
        var target = _actors.GetValueOrDefault(targetId) ?? _actors.GetValueOrDefault(actorId);
        if (target == null) return;
        slickshellBaits.Add((slickshellPrimary, target, slickshellPrimaryRemoved, false,
            !slickshellPrimaryRemoved && target.Id == PrimaryId, _now.AddSeconds(5.1d)));
    }

    private void SlickshellCast(DungeonPendingCast cast, bool correction)
    {
        if (IsSlickshell && cast.Rule.Action == 1087 && !correction)
            slickshellBaits.Clear();
    }

    private void SlickshellRemoved(ulong id)
    {
        if (!IsSlickshell) return;
        if (id == PrimaryId)
        {
            slickshellBaits.Clear();
            slickshellPrimaryRemoved = true;
        }
        for (var i = 0; i < slickshellBaits.Count; ++i)
        {
            var bait = slickshellBaits[i];
            if (bait.Target.Id == id)
            {
                bait.Removed = true;
                slickshellBaits[i] = bait;
            }
        }
    }

    private void SlickshellCreated(ulong id, uint oid)
    {
        if (!IsSlickshell || Active == null) return;
        // A new Actor may reuse an ID before a complete frame observes the missing old object.
        for (var i = 0; i < slickshellBaits.Count; ++i)
        {
            var bait = slickshellBaits[i];
            if (bait.Source.Id == id) bait.SourceRemoved = true;
            if (bait.Target.Id == id) bait.Removed = true;
            slickshellBaits[i] = bait;
        }
        if (id == PrimaryId && oid == Active.BossOid) slickshellPrimaryRemoved = true;
    }

    private IEnumerable<DungeonAmbientAoe> SlickshellAoes()
    {
        if (!IsSlickshell || Active == null) yield break;
        var shape = new CircleTemplate(5);
        foreach (var (bait, index) in slickshellBaits.Select((b, i) => (b, i)))
        {
            if (bait.Source.Dead) continue;
            var own = !bait.Removed && bait.Target.Id == PlayerId;
            yield return new($"slickshell/bait/{index}/display", "Blizzard III 诱导", shape, bait.Target.Position,
                Navigate: !own, OutlineOnly: bait.Target.Id == PlayerId, Activation: bait.Activation);
            if (!own || bait.IsSource) continue;
            foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                yield return new($"slickshell/bait/{index}/away/{member.Id}", "诱导远离队友", shape,
                    member.Position, Display: false, Activation: bait.Activation);
        }
    }

    private IEnumerable<DungeonDamagePrediction> SlickshellDamage()
    {
        if (!IsSlickshell || Active == null || slickshellBaits.Count == 0) yield break;
        foreach (var bait in slickshellBaits.Where(b => !b.Source.Dead).DistinctBy(b => b.Target.Id))
        {
            var member = CurrentPartyMembers.FirstOrDefault(p => p.EntityId == bait.Target.Id);
            var slot = member?.Slot ?? _actors.GetValueOrDefault(bait.Target.Id)?.PartySlot ?? -1;
            if (slot >= 0)
                yield return new(bait.Target.Id, slot, member?.Role ?? bait.Target.PartyRole, bait.Target.Name,
                    slickshellBaits[0].Activation, "Raidwide");
        }
    }

    private IEnumerable<string> SlickshellHints(Vector3? player)
    {
        if (!IsSlickshell || Active == null || player is not { } p) yield break;
        static float DistanceSquared(Vector3 a, Vector3 b) => Vector2.DistanceSquared(new(a.X, a.Z), new(b.X, b.Z));
        if (slickshellBaits.Any(b => !b.Source.Dead && b.Target.Id == PlayerId && _actors.Values.Any(a =>
            a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && DistanceSquared(a.Position, b.Target.Position) <= 25)))
            yield return "Bait away from raid!";
        if (slickshellBaits.Any(b => !b.Source.Dead && b.Target.Id != PlayerId && DistanceSquared(p, b.Target.Position) <= 25))
            yield return "GTFO from baited aoe!";
    }

    private void SlickshellDirector(uint update)
    {
        if (!IsSlickshell || update is not (0x40000005 or 0x40000011)) return;
        slickshellWiping = update == 0x40000005;
        if (slickshellWiping) ResetFight();
    }

    private void ResetSlickshell()
    {
        slickshellBaits.Clear();
        slickshellPrimary = null;
        slickshellPrimaryRemoved = false;
    }
}
