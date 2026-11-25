using System.Numerics;
using MasterPlayer.Mechanics.DeepDungeon;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class PilgrimsFloorEncounter : DeepDungeonFloorEncounter
{
    private readonly List<(PalaceTrashCaster Source, GeometryTemplate Shape)> sectors = [];
    private readonly List<(PalaceTrashCaster Source, float Radius)> circles = [];
    private readonly List<PalaceTrashCaster> knockbacks = [];
    private readonly List<(PalaceTrashCaster Source, GeometryTemplate Shape, float Offset, int Counter)> voidzones = [];
    private readonly List<(PalaceTrashCaster Source, DateTime Until)> spikes = [];
    private readonly HashSet<PalaceTrashCaster> removed = [];
    private readonly Dictionary<PalaceTrashCaster, ReplayStatus?[]> statuses = [];
    private readonly DateTime[] roleExpiry = new DateTime[4];
    private readonly DateTime[] jobExpiry = new DateTime[4];
    private readonly bool[] penalty = new bool[4];
    private ReplayPartyMember[] party = [];

    internal static uint[] SpecialActions => [42091, 44758, 44732, 44852, 41427, 42523, 43133, 41512, 44727];
    internal static bool OwnsTerritory(uint territory) => territory is >= 1281 and <= 1290;
    // These native MP consumers also retain packet-locked first hits and recorded
    // follow-up timings. Do not silence them when replacing AutoClear.
    internal override bool PreservesGenericDodge(uint action)
        => action is 44690 or 44691 or 42214 or 42216 or 42516 or 42578 or 42748 or 43132 or 44759;

    internal override void SynchronizeFloor(IEnumerable<ReplayActorState> observations, DateTime now, bool observe,
        Func<ReplayActorState, PalaceCast?> resolve, Action<ReplayActorState>? created,
        Func<ReplayActorState, uint, bool>? ownsCast, ReplayPartyMember[] nextParty)
    {
        Synchronize(observations, now, observe, resolve, created, ownsCast, ObserveStatuses);
        if (observe) party = nextParty;
    }

    protected override void AddZone(PalaceTrashCaster source, DateTime now)
    {
        switch (source.Action)
        {
            case 42091:
                sectors.Add((source, new PtDonutSectorTemplate(3, 30, 1.5f * MathF.PI)));
                hintDisabled.Add(source);
                break;
            case 44758:
            case 44732:
                donuts.Add((source, 5, source.Action == 44758 ? 60 : 30));
                hintDisabled.Add(source);
                break;
            case 44852:
                interrupts.Add(source);
                stuns.Add(source);
                break;
            case 41427:
                knockbacks.Add(source);
                hintDisabled.Add(source);
                break;
            case 42523:
            case 43133:
                circles.Add((source, source.Action == 42523 ? 45 : 46));
                break;
            case 41512:
            case 44727:
                stuns.Add(source);
                break;
        }
    }

    protected override void OnFinished(PalaceTrashCaster source, DateTime now)
    {
        if (source.Action == 42212) spikes.Add((source, now.AddSeconds(10d)));
    }

    protected override void OnRemoved(PalaceTrashCaster source)
    {
        removed.Add(source);
        if (statuses.Remove(source, out var old) && old.Any(s => s?.Id == 4579))
            spikes.RemoveAll(s => s.Source == source);
    }

    internal override void Effect(ulong actorId, uint action)
    {
        if (Source(actorId) is not { } source) return;
        GeometryTemplate? shape = action switch
        {
            42748 => new CircleTemplate(10),
            42578 => new DonutTemplate(10, 40),
            42516 => new DonutTemplate(5, 10),
            42214 or 42216 => new FanTemplate(30, MathF.PI),
            44759 => new FanTemplate(8, MathF.PI),
            _ => null,
        };
        var offset = action switch { 42214 => MathF.PI / 2, 42216 => -MathF.PI / 2, _ => 0 };
        if (shape != null) voidzones.Add((source, shape, offset, action == 44759 ? 3 : 0));
        if (action == 44760)
        {
            // The source searches by InstanceID, and does not filter shape or
            // originating action. In particular a Counter=0 entry is consumed.
            var index = voidzones.FindIndex(v => v.Source.Actor.Id == actorId);
            if (index < 0) return;
            var zone = voidzones[index];
            if (--zone.Counter <= 0) voidzones.RemoveAt(index);
            else voidzones[index] = zone;
        }
        else if (action is 42746 or 42749 or 42215 or 42217 or 42517)
            voidzones.RemoveAll(v => v.Source == source);
    }

    private void ObserveStatuses(PalaceTrashCaster source, bool hot)
    {
        statuses.TryGetValue(source, out var old);
        var next = new ReplayStatus?[60];
        for (var i = 0; i < source.Actor.Statuses.Length; ++i)
        {
            var value = source.Actor.Statuses[i];
            var index = value.SlotIndex ?? i;
            if (index is >= 0 and < 60) next[index] = value with
            {
                SourceId = value.SourceId == 0xE0000000 ? 0 : value.SourceId,
                ExpiresAt = value.ZeroRemainingTime == true ? source.Clock : value.ExpiresAt,
            };
        }
        for (var i = 0; i < 60; ++i)
        {
            var before = old?[i];
            var after = next[i];
            var changed = before?.Id != after?.Id || before?.SourceId != after?.SourceId;
            if (hot && changed && before?.Id == 4579) spikes.RemoveAll(s => s.Source == source);
            if (!hot || after == null) continue;
            if (!changed && before != null && before.Param == after.Param
                && (after.ExpiresAt - before.ExpiresAt).TotalSeconds <= 1d) continue;
            var index = Array.FindIndex(party, p => p.EntityId == source.Actor.Id);
            if (index < 0 || party[index].Slot is < 0 or >= 4) continue;
            var slot = party[index].Slot;
            if (after.Id is 160 or 1209) roleExpiry[slot] = after.ExpiresAt;
            else if (after.Id == 2663) jobExpiry[slot] = after.ExpiresAt;
            else if (after.Id is 1096 or 1512) penalty[slot] = true;
        }
        statuses[source] = next;
    }

    internal override ulong[] SpikedTargets(DateTime now)
    {
        if (!ZoneEnabled || BossActive) return [];
        spikes.RemoveAll(s => s.Until <= now);
        return spikes.Where(s => Source(s.Source.Actor.Id) == s.Source && !s.Source.Actor.Dead
            && s.Source.Actor.Targetable && s.Source.Actor.IsEnemy == true).Select(s => s.Source.Actor.Id).ToArray();
    }

    internal override IEnumerable<PalaceArea> Collect(DateTime now, ReplayActorState player)
    {
        foreach (var area in base.Collect(now, player))
        {
            if (area.Action == 43132 && Source(area.Owner) is { } source)
                yield return area with { Position = source.Actor.Position, Rotation = source.Actor.Rotation };
            else yield return area;
        }
        if (!ZoneEnabled || BossActive) yield break;
        sectors.RemoveAll(s => s.Source.Cast == null);
        circles.RemoveAll(s => s.Source.Cast == null);
        knockbacks.RemoveAll(s => s.Cast == null);
        voidzones.RemoveAll(s => s.Source.Actor.Dead || removed.Contains(s.Source));
        for (var i = sectors.Count - 1; i >= 0; --i)
        {
            var (source, shape) = sectors[i];
            yield return new($"zone/sector/{i}", "大地钻击", shape, source.Cast!.Location,
                source.Cast.Rotation ?? 0, Activation(source, now), DateTime.MaxValue, source.Actor.Id, source.Action);
        }
        for (var i = circles.Count - 1; i >= 0; --i)
        {
            var (source, radius) = circles[i];
            yield return new($"zone/circle/{i}", "大范围撤离", new CircleTemplate(radius),
                Quantized(source.Actor.Position), 0, Activation(source, now), DateTime.MaxValue);
            var distance = Vector2.Distance(new(player.Position.X, player.Position.Z), new(source.Actor.Position.X, source.Actor.Position.Z));
            if (distance <= radius)
                yield return new($"zone/escape/{i}", "撤离站位", new DonutTemplate(distance + 10, 10000),
                    source.Actor.Position, 0, now, DateTime.MaxValue, Safe: true, NavigationOnly: true, Priority: 100,
                    OverrideDangerMargin: false);
        }
        var slot = party.FirstOrDefault(p => p.EntityId == player.Id)?.Slot ?? -1;
        for (var i = knockbacks.Count - 1; i >= 0; --i)
        {
            var source = knockbacks[i];
            var hit = Activation(source, now);
            if (slot is >= 0 and < 4 && (penalty[slot] || roleExpiry[slot] > hit || jobExpiry[slot] > hit)) continue;
            yield return new($"zone/knockback/{i}", "免疫外吸引范围", new CircleTemplate(15),
                source.Actor.Position, 0, hit, DateTime.MaxValue);
        }
        for (var i = voidzones.Count - 1; i >= 0; --i)
        {
            var zone = voidzones[i];
            yield return new($"zone/void/{i}", "连续判定范围", zone.Shape, Quantized(zone.Source.Actor.Position),
                zone.Source.Actor.Rotation + zone.Offset, now, DateTime.MaxValue);
        }
        foreach (var actor in Actors.Where(a => a.Oid == 0x491B && !a.Dead && a.TargetId == player.Id))
        {
            // Unlike AutoClear's NPCRemainingTime, this test uses RemainingTime
            // with no additional .3 seconds and a strict one-second boundary.
            var source = Source(actor.Id)!;
            if (source.Cast is { } cast)
            {
                Activation(source, now);
                var total = cast.TotalTime ?? (float)((cast.FinishAt - cast.StartedAt).TotalSeconds - .3d);
                if (total - source.Elapsed >= 1f) continue;
            }
            yield return new($"zone/turtle/{actor.Id}", "避开乌龟普攻", new CircleTemplate(actor.HitboxRadius + 5.5f),
                actor.Position, 0, now, DateTime.MaxValue);
        }
    }

    internal override void ClearFloor()
    {
        base.ClearFloor();
        sectors.Clear();
        circles.Clear();
        spikes.Clear();
        Array.Clear(roleExpiry);
        Array.Clear(jobExpiry);
        Array.Clear(penalty);
        // AutoClear.ClearState retains voidzones and knockback lists.
    }

    internal override void Reset()
    {
        base.Reset();
        ClearFloor();
        knockbacks.Clear();
        voidzones.Clear();
        removed.Clear();
        statuses.Clear();
        party = [];
    }
}
