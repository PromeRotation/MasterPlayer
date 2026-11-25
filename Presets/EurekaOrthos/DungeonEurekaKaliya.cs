using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool eurekaBarofieldActive;
    private EurekaKaliyaAoe? eurekaBarofield;
    private readonly List<EurekaKaliyaAoe> eurekaKaliyaCombined = [];
    private readonly List<EurekaKaliyaAoe> eurekaKaliyaSides = [];
    private readonly List<EurekaKaliyaAoe> eurekaKaliyaCentral = [];
    private readonly List<EurekaResonance> eurekaResonances = [];
    private readonly EurekaMagnetism?[] eurekaMagnetisms = new EurekaMagnetism?[4];
    private readonly Dictionary<AlphaActorState, ReplayStatus?[]> eurekaKaliyaStatuses = [];
    internal bool EurekaBarofieldActive => eurekaBarofieldActive;
    internal EurekaKaliyaAoe? EurekaBarofield => eurekaBarofield;
    internal IReadOnlyList<EurekaKaliyaAoe> EurekaKaliyaCombined => eurekaKaliyaCombined;
    internal IReadOnlyList<EurekaKaliyaAoe> EurekaKaliyaSides => eurekaKaliyaSides;
    internal IReadOnlyList<EurekaKaliyaAoe> EurekaKaliyaCentral => eurekaKaliyaCentral;
    internal IReadOnlyList<EurekaResonance> EurekaResonances => eurekaResonances;
    internal IReadOnlyList<EurekaMagnetism?> EurekaMagnetisms => eurekaMagnetisms;
    private bool IsEurekaKaliya => territory == EurekaOrthosCatalog.PROTO_KALIYA;

    private void EurekaKaliyaCast(DungeonPendingCast cast)
    {
        if (!IsEurekaKaliya || !cast.Observed) return;
        EurekaKaliyaAoe Aoe(string kind, GeometryTemplate shape, float rotation = 0, double extra = 0)
            => new(++eurekaGeneration, kind, cast, shape, cast.Location, rotation, cast.FinishAt.AddSeconds(extra));
        switch (cast.Rule.Action)
        {
            case 31427:
            case 31429:
                if (!eurekaBarofieldActive)
                {
                    eurekaBarofield = Aoe("barofield", new CircleTemplate(5), extra: .7d);
                    eurekaBarofieldActive = true;
                }
                break;
            case 32930:
                eurekaKaliyaCombined.Add(Aoe("ring", new DonutTemplate(8, 30)));
                eurekaKaliyaCombined.Add(Aoe("cross", new DungeonCrossTemplate(20, 2.5f)));
                break;
            case 31432:
                eurekaKaliyaCombined.Add(Aoe("cannons", new DungeonCrossTemplate(43, 2.5f), cast.Rotation));
                var cross = eurekaKaliyaCombined.FindIndex(a => a.Kind == "cross");
                if (cross >= 0) eurekaKaliyaCombined.RemoveAt(cross);
                break;
            case 32934:
            case 32935:
                eurekaKaliyaSides.Add(Aoe("sides", new FanTemplate(25.5f, MathF.PI), cast.Rotation));
                if (eurekaKaliyaSides.Count >= 99)
                    DungeonAoeOrder.Sort(eurekaKaliyaSides, a => a.Activation);
                break;
            case 32933:
                eurekaKaliyaCentral.Add(Aoe("central", new FanTemplate(25.5f, 120 * (MathF.PI / 180f)), cast.Rotation));
                break;
            case 31422:
                if (eurekaActors.TryGetValue(cast.Source, out var source)
                    && eurekaActors.TryGetValue(cast.TargetId, out var target))
                    eurekaResonances.Add(new(++eurekaGeneration, source, target, cast));
                break;
        }
    }

    private static void EurekaKaliyaRemoveFirst(List<EurekaKaliyaAoe> items, ulong source)
    {
        var index = items.FindIndex(a => a.Cast.Source == source);
        if (index >= 0) items.RemoveAt(index);
    }

    private void EurekaKaliyaFinish(DungeonPendingCast cast)
    {
        if (!IsEurekaKaliya || !cast.Observed) return;
        EurekaKaliyaRemoveFirst(eurekaKaliyaSides, cast.Source);
        if (cast.Rule.Action is 32930 or 31432)
            EurekaKaliyaRemoveFirst(eurekaKaliyaCombined, cast.Source);
        if (cast.Rule.Action == 32933)
            EurekaKaliyaRemoveFirst(eurekaKaliyaCentral, cast.Source);
    }

    private void EurekaKaliyaUnknownFinish(DungeonActor previous, DungeonActor current)
    {
        if (!IsEurekaKaliya || previous.Cast is not { } old || !EurekaSpellSource(previous.Id)
            || Definitions[0].Casts.Any(c => c.Action == old.Action)) return;
        var next = current.Cast;
        if (next == null || old.Action != next.Action || old.Location != next.Location
            || old.Rotation != next.Rotation || old.TargetId != next.TargetId || old.TotalTime != next.TotalTime)
            EurekaKaliyaRemoveFirst(eurekaKaliyaSides, previous.Id);
    }

    private static ReplayStatus?[] EurekaKaliyaStatusSlots(DungeonActor actor)
    {
        var slots = new ReplayStatus?[60];
        if (actor.StatusSlots is { } recorded)
        {
            for (var index = 0; index < recorded.Count; ++index)
            {
                var status = recorded[index];
                var slot = status.SlotIndex ?? index;
                if ((uint)slot < 60 && status.Id != 0)
                    slots[slot] = status with { SourceId = status.SourceId == 0xE0000000 ? 0 : status.SourceId };
            }
        }
        else if (actor.Statuses is { } ids)
            for (var slot = 0; slot < Math.Min(60, ids.Length); ++slot)
            {
                var id = ids[slot];
                if (id != 0)
                    slots[slot] = new(id, actor.StatusParams?.GetValueOrDefault(id) ?? 0,
                        actor.StatusSources?.GetValueOrDefault(id) ?? 0,
                        actor.StatusExpirations?.GetValueOrDefault(id) ?? default);
            }
        return slots;
    }

    private void EurekaKaliyaStatus(AlphaActorState actor)
    {
        if (!IsEurekaKaliya || Active == null || !eurekaEnabled) return;
        var next = EurekaKaliyaStatusSlots(actor.Actor);
        var old = eurekaKaliyaStatuses.GetValueOrDefault(actor) ?? new ReplayStatus?[60];
        if (!eurekaChimeraBootstrapped || actor.Actor.NativeType != 0x202)
            for (var slot = 0; slot < next.Length; ++slot)
            {
                var previous = old[slot];
                var current = next[slot];
                var same = previous?.Id == current?.Id && previous?.SourceId == current?.SourceId;
                if (previous?.Id == 3420 && !same)
                {
                    Arena = EurekaOrthosCatalog.Arena(territory, Arena?.Position.Y ?? actor.Actor.Position.Y);
                    eurekaBarofieldActive = false;
                }
                if (current?.Id == 3420 && (!same || previous?.Param != current.Param
                    || (current.ExpiresAt - previous!.ExpiresAt).TotalSeconds > 1d))
                {
                    eurekaBarofield = null;
                    Arena = EurekaOrthosArenas.ProtoKaliya(Arena?.Position.Y ?? actor.Actor.Position.Y);
                }
            }
        eurekaKaliyaStatuses[actor] = next;
    }

    private void EurekaKaliyaTether(AlphaActorState source, (uint Type, ulong Target) tether, bool present)
    {
        if (!IsEurekaKaliya || tether.Type != 38) return;
        var slot = EurekaInputParty.FirstOrDefault(p => p.EntityId == tether.Target)?.Slot ?? -1;
        // The source array has four slots. Invalid roster input has no representable source slot.
        if (slot is < 0 or >= 4) return;
        if (!present)
        {
            eurekaMagnetisms[slot] = null;
            return;
        }
        if (!eurekaActors.TryGetValue(tether.Target, out var target)) return;
        static bool Has(AlphaActorState a, uint status) => EurekaKaliyaStatusSlots(a.Actor).Any(s => s?.Id == status);
        var pull = Has(source, 3417) && Has(target, 3418) || Has(source, 3416) && Has(target, 3419);
        var away = Has(source, 3417) && Has(target, 3419) || Has(source, 3416) && Has(target, 3418);
        if (pull || away)
            eurekaMagnetisms[slot] = new(++eurekaGeneration, source, target, slot, source.Actor.Position,
                pull, _now, _now.AddSeconds(10d));
    }

    private IEnumerable<(AlphaActorState Source, AlphaActorState Target)> EurekaKaliyaCleaves()
    {
        foreach (var source in eurekaActors.Values.Where(a => a.Actor.Oid == 0x3D18 && !a.Actor.Dead && a.Actor.Targetable))
            if (eurekaActors.TryGetValue(source.Actor.TargetId, out var target))
                yield return (source, target);
    }

    private static float EurekaKaliyaAngle(AlphaActorState source, AlphaActorState target, bool cleave = false)
        => !cleave && ReferenceEquals(source, target) ? source.Actor.Rotation
            : MathF.Atan2(target.Actor.Position.X - source.Actor.Position.X, target.Actor.Position.Z - source.Actor.Position.Z);

    private IEnumerable<DungeonAmbientAoe> EurekaKaliyaBait(AlphaActorState source, AlphaActorState target,
        string key, float radius, DateTime activation, bool cleave)
    {
        var shape = new FanTemplate(radius, MathF.PI / 2);
        var origin = cleave ? QuantizedDungeonPosition(source.Actor.Position) : source.Actor.Position;
        var angle = EurekaKaliyaAngle(source, target, cleave);
        yield return new(key + "/display", cleave ? "AutoAttack" : "Resonance", shape, origin, angle,
            Navigate: false, OutlineOnly: cleave || target.Actor.Id == PlayerId);
        if (!ReferenceEquals(target, eurekaActors.GetValueOrDefault(PlayerId)))
            yield return new(key + "/avoid", "Cleave", shape, origin,
                angle, Display: false, Activation: activation);
        else if (cleave || !ReferenceEquals(source, target))
            foreach (var member in CurrentPartyMembers.Select(p => eurekaActors.GetValueOrDefault(p.EntityId))
                .Where(a => a != null && !a.Actor.Dead && a.Actor.Id != PlayerId))
                yield return new($"{key}/party/{member!.Actor.Id}", "Bait away from raid",
                    new FanTemplate(100, MathF.PI / 2), origin, EurekaKaliyaAngle(source, member, cleave),
                    Display: false, Activation: activation);
    }

    internal EurekaMagnetism? EurekaKaliyaMagnetism()
    {
        if (!IsEurekaKaliya || Active == null || !eurekaEnabled || !_actors.ContainsKey(PlayerId)) return null;
        var slot = CurrentPartyMembers.FirstOrDefault(p => p.EntityId == PlayerId)?.Slot ?? -1;
        return slot is >= 0 and < 4 ? eurekaMagnetisms[slot] : null;
    }

    internal (Vector3 Position, float Rotation)[] EurekaKaliyaDrones()
        => eurekaActors.Values.Where(a => a.Actor.Oid == 0x3D19)
            .Select(a => (a.Actor.Position, a.Actor.Rotation)).ToArray();

    internal (Vector3 From, Vector3 To, bool Unsafe)? EurekaKaliyaMovement()
    {
        if (EurekaKaliyaMagnetism() is not { } kb || Arena == null
            || !eurekaKaliyaCombined.Any(a => a.Kind == "ring")) return null;
        var from = _actors[PlayerId].Position;
        var direction = from - kb.Origin;
        direction.Y = 0;
        var length = direction.Length();
        if (length == 0) return null;
        direction /= length;
        if (kb.Pull) direction = -direction;
        var distance = kb.Pull ? MathF.Min(kb.Distance, length) : kb.Distance;
        distance = MathF.Min(distance, QuestWindRay(from, direction) + (.5f - .499f));
        var to = from + direction * distance;
        return (from, to, !Arena.Contains(to, 0) || eurekaKaliyaCombined.Any(a =>
            DungeonSourceGeometry.Contains(a.Shape, a.Origin, a.Rotation, to)));
    }

    private IEnumerable<DungeonAmbientAoe> EurekaKaliyaAoes()
    {
        if (!IsEurekaKaliya) yield break;
        foreach (var aoe in eurekaKaliyaCombined.Concat(eurekaKaliyaSides).Concat(eurekaKaliyaCentral)
            .Concat(eurekaBarofield is { } barofield ? [barofield] : []))
            yield return new($"eureka/kaliya/{aoe.Kind}/{aoe.Generation}", aoe.Cast.Rule.Name,
                aoe.Shape, aoe.Origin, aoe.Rotation, Activation: aoe.Activation);
        if (!eurekaBarofieldActive)
            foreach (var (source, target) in EurekaKaliyaCleaves())
                foreach (var aoe in EurekaKaliyaBait(source, target, $"eureka/kaliya/cleave/{source.Actor.Id}", 11, default, true))
                    yield return aoe;
        foreach (var bait in eurekaResonances.Where(b => !b.Source.Actor.Dead))
            foreach (var aoe in EurekaKaliyaBait(bait.Source, bait.Target, $"eureka/kaliya/resonance/{bait.Generation}", 12, bait.Cast.FinishAt, false))
                yield return aoe;
        if (EurekaKaliyaMovement() is { } movement)
            foreach (var aoe in QuestKnockbackProjection("eureka/kaliya/magnetism", _actors[PlayerId], movement.From, movement.To))
                yield return aoe;
    }

    private IEnumerable<DungeonDamagePrediction> EurekaKaliyaDamage()
    {
        if (!IsEurekaKaliya || Active == null || !eurekaEnabled || eurekaResonances.Count == 0) yield break;
        var activeTargets = eurekaResonances.Where(b => !b.Source.Actor.Dead).Select(b => b.Target.Actor.Id).ToHashSet();
        foreach (var member in CurrentPartyMembers.Where(p => p.Slot is >= 0 and < 64 && activeTargets.Contains(p.EntityId)))
            yield return new(member.EntityId, member.Slot, "", "", eurekaResonances[0].Cast.FinishAt, "Tankbuster");
    }

    private IEnumerable<string> EurekaKaliyaHints(Vector3 player)
    {
        if (!IsEurekaKaliya || Active == null || !eurekaEnabled) yield break;
        if (eurekaKaliyaCombined.Concat(eurekaKaliyaSides).Concat(eurekaKaliyaCentral)
            .Concat(eurekaBarofield is { } barofield ? [barofield] : [])
            .Any(a => DungeonSourceGeometry.Contains(a.Shape, a.Origin, a.Rotation, player)))
            yield return "GTFO from aoe!";
        if (eurekaResonances.Count != 0) yield return "Tankbuster cleave";
        foreach (var bait in eurekaResonances.Where(b => !b.Source.Actor.Dead))
        {
            var shape = new FanTemplate(12, MathF.PI / 2);
            var origin = bait.Source.Actor.Position;
            var rotation = EurekaKaliyaAngle(bait.Source, bait.Target);
            if (bait.Target.Actor.Id == PlayerId)
            {
                if (CurrentPartyMembers.Any(p => p.EntityId != PlayerId && eurekaActors.GetValueOrDefault(p.EntityId) is { } member
                    && !member.Actor.Dead && DungeonSourceGeometry.Contains(shape, origin, rotation, member.Actor.Position))) yield return "Bait away from raid!";
            }
            else if (DungeonSourceGeometry.Contains(shape, origin, rotation, player)) yield return "GTFO from baited aoe!";
        }
        if (EurekaKaliyaCleaves().Any(c => c.Target.Actor.Id != PlayerId
            && DungeonSourceGeometry.Contains(new FanTemplate(11, MathF.PI / 2), c.Source.Actor.Position,
                EurekaKaliyaAngle(c.Source, c.Target, true), QuantizedDungeonPosition(player)))) yield return "GTFO from cleave!";
        if (EurekaKaliyaMovement()?.Unsafe == true) yield return "About to be knocked into danger!";
    }

    private void ResetEurekaKaliya()
    {
        eurekaBarofieldActive = false;
        eurekaBarofield = null;
        eurekaKaliyaCombined.Clear();
        eurekaKaliyaSides.Clear();
        eurekaKaliyaCentral.Clear();
        eurekaResonances.Clear();
        Array.Clear(eurekaMagnetisms);
        eurekaKaliyaStatuses.Clear();
    }
}
