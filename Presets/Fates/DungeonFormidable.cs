using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsFormidable => territory == NativeFateCatalog.FORMIDABLE;
    public ulong FateSourcePlayerId { get; set; }
    private Vector3 formidablePathCenter;
    private Vector3 formidableViewCenter;
    private readonly HashSet<ulong> formidableCreated = [];
    private readonly Dictionary<ulong, DungeonActor> formidableMissiles = [];
    private readonly List<FormidableDischarge> formidableDischarges = [];
    private readonly List<FormidableSteamDome> formidableKnockbacks = [];
    private DungeonPendingCast? formidableFires;
    private DateTime formidableFireActivation;
    private int formidableFireCount;
    private readonly TreasurePlayerStatusState formidableStatuses = new([1269]);
    private readonly Dictionary<int, DateTime> formidableStay = [];
    private readonly Dictionary<ulong, int> formidablePartySlots = [];

    private void FormidableObserve()
    {
        if (!IsFormidable || Active == null) return;
        var sourcePlayer = FateSourcePlayerId != 0 ? _actors.GetValueOrDefault(FateSourcePlayerId)
            : _actors.Values.FirstOrDefault(a => a.PartySlot == 0);
        formidableViewCenter = sourcePlayer?.Position ?? default;
        if (MathF.Abs(formidableViewCenter.X - formidablePathCenter.X) > 5
            || MathF.Abs(formidableViewCenter.Z - formidablePathCenter.Z) > 5)
            formidablePathCenter = new(MathF.Round(formidableViewCenter.X), formidableViewCenter.Y,
                MathF.Round(formidableViewCenter.Z));
        if (Arena is not FormidableArenaBoundary boundary || boundary.Position != formidablePathCenter)
            Arena = boundary = new(formidablePathCenter, formidableViewCenter);
        else
            boundary.ViewCenter = formidableViewCenter;
        foreach (var id in formidableCreated.Where(id => !_actors.ContainsKey(id)).ToArray())
            FormidableRemoved(id);
        foreach (var actor in _actors.Values)
        {
            if (actor.PartySlot >= 0) formidablePartySlots[actor.Id] = actor.PartySlot;
            if (formidableCreated.Add(actor.Id))
            {
                formidableStatuses.BeginActorGeneration(actor.Id, _now);
                if (actor.Oid == 0x2BD5) formidableMissiles[actor.Id] = actor;
                if (actor.Oid is 0x2A9C or 0x2BDC)
                    formidableDischarges.Add(new(actor,
                        actor.Oid == 0x2A9C ? new DonutTemplate(9, 60) : new CircleTemplate(8),
                        QuantizedDungeonPosition(actor.Position), _now, _now.AddSeconds(actor.Oid == 0x2A9C ? 9.3 : 8.1)));
            }
            if (formidableMissiles.ContainsKey(actor.Id)) formidableMissiles[actor.Id] = actor;
        }
        formidableStatuses.Changed = FormidableStatusObserved;
        formidableStatuses.Observe(_actors.ToDictionary(p => p.Key, p => TreasureStatusActor(p.Value)), _now);
    }

    private void FormidableStatusObserved(ulong actorId, uint status, ReplayStatus? value)
    {
        var slot = _actors.GetValueOrDefault(actorId)?.PartySlot
            ?? formidablePartySlots.GetValueOrDefault(actorId, -1);
        if (slot < 0) return;
        if (value == null) formidableStay.Remove(slot);
        else formidableStay[slot] = value.ExpiresAt;
    }

    private void FormidableRemoved(ulong id)
    {
        formidableCreated.Remove(id);
        formidableMissiles.Remove(id);
        formidableDischarges.RemoveAll(a => a.Source.Id == id);
        formidableStatuses.Remove(id, _now);
        formidablePartySlots.Remove(id);
    }

    private void FormidableCast(DungeonPendingCast cast, bool correction)
    {
        if (!IsFormidable) return;
        if (cast.Rule.Action == 17395)
        {
            if (!correction || formidableFires != null) formidableFires = cast;
            if (!correction)
            {
                formidableFireCount = 0;
                formidableFireActivation = cast.FinishAt;
            }
        }
        if (cast.Rule.Action != 17394) return;
        var index = formidableKnockbacks.FindIndex(a => a.Cast.Source == cast.Source);
        if (index >= 0) formidableKnockbacks[index] = new(cast);
        else formidableKnockbacks.Add(new(cast));
    }

    private void FormidableFinish(DungeonPendingCast cast, bool correction)
    {
        if (IsFormidable && !correction && cast.Rule.Action == 17394)
            formidableKnockbacks.RemoveAll(a => a.Cast.Source == cast.Source);
        if (IsFormidable && !correction && cast.Rule.Action == 17401)
        {
            var index = fateStacks.FindIndex(s => s.Target.Id == cast.TargetId);
            if (index >= 0) fateStacks.RemoveAt(index);
        }
    }

    private void FormidableEffect(ulong source, uint action, ulong target)
    {
        if (!IsFormidable) return;
        if (action is 17395 or 18002)
        {
            formidableFireActivation = _now.AddSeconds(3.1);
            if (++formidableFireCount >= 7) formidableFires = null;
        }
        if (action == 17401)
        {
            var index = fateStacks.FindIndex(s => s.Target.Id == target);
            if (index >= 0) fateStacks.RemoveAt(index);
        }
        if (action == 18003) formidableMissiles.Remove(source);
        if (action is 17404 or 17405)
        {
            var index = formidableDischarges.FindIndex(a => a.Source.Id == source
                && a.Source.Oid == (action == 17404 ? 0x2A9Cu : 0x2BDCu));
            if (index >= 0) formidableDischarges.RemoveAt(index);
        }
    }

    private IEnumerable<DungeonAmbientAoe> FormidableAoes(DungeonActor primary)
    {
        var center = Active == null ? primary.Position : formidableViewCenter;
        yield return new("formidable/display/arena", "局部显示R30", new CircleTemplate(30), center,
            Navigate: false, OutlineOnly: true);
        var marker = new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]);
        var enemies = Active == null ? new[] { primary }
            : _actors.Values.Where(a => a.Id == PrimaryId || a.Oid == 0x2A74);
        foreach (var actor in enemies.Where(a => !a.Dead && a.Targetable))
        {
            var offset = actor.Position - center;
            offset.Y = 0;
            var outside = offset.LengthSquared() > 900;
            var position = outside ? center + Vector3.Normalize(offset) * 30 : actor.Position;
            yield return new($"formidable/enemy/{actor.Id}", "敌人及朝向", marker, position, actor.Rotation,
                Navigate: false, OutlineOnly: outside, Unclipped: true, Color: new(1, 0, 0, 1));
        }
        if (Active == null) yield break;
        if (formidableFires is { } fires)
            yield return new("formidable/fires", "Fires of Mt. Gulg", new DonutTemplate(10, 50),
                fires.Location, Activation: formidableFireActivation);
        foreach (var discharge in formidableDischarges)
            yield return new($"formidable/discharge/{discharge.Source.Id}", "Dwarven Discharge",
                discharge.Shape, discharge.Position, Activation: discharge.Activation);
        foreach (var missile in formidableMissiles.Values)
        {
            var offset = missile.Position - center;
            offset.Y = 0;
            var outside = offset.LengthSquared() > 900;
            var position = outside ? center + Vector3.Normalize(offset) * 30 : missile.Position;
            yield return new($"formidable/missile/{missile.Id}", "Dwarven Dynamite", marker, position,
                missile.Rotation, Navigate: false, Unclipped: true, OutlineOnly: outside, Color: new(1, .5f, 0, 1));
            yield return new($"formidable/missile/{missile.Id}/radius", "Missile R6", new CircleTemplate(6),
                missile.Position, Navigate: false, OutlineOnly: true);
        }
        foreach (var aoe in FateStackAoes()) yield return aoe;
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var from = player.Position;
        foreach (var wave in formidableKnockbacks)
        {
            if (KnockbackImmune(player, wave.Cast.FinishAt)) continue;
            var to = wave.Destination(from);
            foreach (var aoe in QuestKnockbackProjection($"formidable/steam/{wave.Cast.Source}", player, from, to,
                new CircleGeometry(formidableViewCenter, 30), new Vector4(1, 1, 0, 1)))
                yield return aoe with { Navigate = false };
            from = to;
        }
    }

    private IEnumerable<string> FormidableHints(Vector3 player)
    {
        if (formidableFires is { } fires && new DonutGeometry(fires.Location, 10, 50).Contains(player, 0)
            || formidableDischarges.Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(player, 0)))
            yield return "GTFO from aoe!";
        if (_actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;
        if (formidableStay.TryGetValue(viewer.PartySlot, out var activation))
        {
            yield return $"Pyretic / Stay @ {activation:O}";
            if (activation <= _now.AddSeconds(3)) yield return "Stop everything!";
            if (viewer.Dead) formidableStay.Remove(viewer.PartySlot);
        }
        var from = player;
        foreach (var wave in formidableKnockbacks)
        {
            if (KnockbackImmune(viewer, wave.Cast.FinishAt)) continue;
            from = wave.Destination(from);
            if (Vector2.DistanceSquared(new(from.X, from.Z), new(formidableViewCenter.X, formidableViewCenter.Z)) > 900)
            {
                yield return "Knockback into danger!";
                break;
            }
        }
    }

    private void ResetFormidable()
    {
        formidablePathCenter = default;
        formidableViewCenter = default;
        formidableCreated.Clear();
        formidableMissiles.Clear();
        formidableDischarges.Clear();
        formidableKnockbacks.Clear();
        formidableFires = null;
        formidableFireCount = 0;
        formidableStatuses.Clear();
        formidableStay.Clear();
        formidablePartySlots.Clear();
    }
}
