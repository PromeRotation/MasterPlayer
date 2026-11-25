using System.Numerics;
using MasterPlayer.Mechanics.DeepDungeon;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.EurekaOrthos;

internal sealed class EurekaFloorEncounter : DeepDungeonFloorEncounter
{
    private readonly List<(PalaceTrashCaster Source, float Radius)> circles = [];
    private readonly List<(PalaceTrashCaster Source, float Radius)> knockbacks = [];
    private readonly List<(PalaceTrashCaster Source, GeometryTemplate Shape, float Offset)> voidzones = [];
    private readonly List<(PalaceTrashCaster Source, DateTime Until)> spikes = [];
    private readonly List<PalaceTrashCaster> los = [];
    private readonly Dictionary<ulong, EurekaLosMap> losCache = [];
    private readonly HashSet<PalaceTrashCaster> removed = [];
    private readonly Dictionary<PalaceTrashCaster, ReplayStatus?[]> statuses = [];
    private readonly DateTime[] roleExpiry = new DateTime[4];
    private readonly DateTime[] jobExpiry = new DateTime[4];
    private readonly bool[] penalty = new bool[4];
    private ReplayPartyMember[] party = [];
    internal int Floor { get; set; }
    internal uint Territory { get; set; }
    internal bool AutoLOS { get; set; }
    internal string? LosDiagnostic { get; private set; }
    internal static uint[] Territories => [1099, 1100, 1101, 1102, 1103, 1104, 1105, 1106, 1107, 1108];
    internal static uint[] SpecialActions => [32437, 32667, 32896, 32737, 32762, 32761, 32731,
        32444, 32910, 32979, 33186, 32732, 32470, 32878, 33043, 32500, 32452, 32467, 32410, 32413, 32477];

    internal override void SynchronizeFloor(IEnumerable<ReplayActorState> observations, DateTime now, bool observe,
        Func<ReplayActorState, PalaceCast?> resolve, Action<ReplayActorState>? created,
        Func<ReplayActorState, uint, bool>? ownsCast, ReplayPartyMember[] nextParty)
    {
        Synchronize(observations, now, observe, resolve, created, ownsCast, ObserveStatuses);
        // The original feed applies all Actor cast/status operations before Party.
        if (observe) party = nextParty;
    }

    protected override void AddZone(PalaceTrashCaster source, DateTime now)
    {
        var action = source.Action;
        if (action is 32381 or 33167 or 33175 || action == 32798 && Floor < 60) stuns.Add(source);
        if (action is 32389 or 32431 or 32798 or 32470 or 32878) interrupts.Add(source);
        var radius = action switch
        {
            32437 or 32667 => 40, 32896 => 30, 32737 => 20, 32762 or 32761 => 33, 32731 => 5, _ => 0,
        };
        if (radius > 0) gazes.Add(new(source, action, new CircleTemplate(radius)));
        radius = action switch { 32444 or 32910 => 30, 32979 => 44, 33186 => 60, _ => 0 };
        if (radius > 0) donuts.Add((source, 8, radius));
        if (action == 32732) circles.Add((source, 40));
        radius = action switch
        {
            32470 or 32878 or 32467 => 30, 33043 or 32500 => 50, 32452 => 60, 32410 => 40, _ => 0,
        };
        if (radius > 0)
        {
            if (!AutoLOS) circles.Add((source, radius));
            else if (EurekaLosMap.Create(Territory, source.Actor.Position, radius) is { } bitmap)
            {
                losCache[source.Actor.Id] = bitmap;
                los.Add(source);
                LosDiagnostic = null;
            }
            else LosDiagnostic = $"未找到视线遮挡位图：{source.Actor.Id:X}/{action}";
        }
        if (action is 32413 or 32477) knockbacks.Add((source, action == 32413 ? 15 : 25));
        if (action is 32737 or 32444 or 32910 or 32979 or 33186 or 32413 or 32477 or 32424 or 32461)
            hintDisabled.Add(source);
    }

    protected override void OnFinished(PalaceTrashCaster source, DateTime now)
    {
        if (source.Action is 33180 or 32952) spikes.Add((source, now.AddSeconds(10d)));
    }

    protected override void OnRemoved(PalaceTrashCaster source)
    {
        removed.Add(source);
        if (statuses.Remove(source, out var old))
            foreach (var value in old)
                if (value != null) Lose(source, value);
    }

    internal override void Effect(ulong actorId, uint action)
    {
        if (Source(actorId) is not { } source) return;
        var shape = action switch
        {
            33178 => new FanTemplate(7, MathF.PI / 2) as GeometryTemplate,
            32748 => new FanTemplate(6, MathF.PI / 2),
            33193 => new FanTemplate(6, 2 * MathF.PI / 3),
            32491 or 32492 => new FanTemplate(12, MathF.PI),
            32413 => new CircleTemplate(6),
            33195 => new CircleTemplate(52),
            _ => null,
        };
        if (shape != null) voidzones.Add((source, shape, action == 32491 ? MathF.PI : 0));
        if (action is 32473 or 32749 or 32412 or 32654 or 32493 or 32494 or 32680)
        {
            var index = voidzones.FindIndex(v => v.Source == source);
            if (index >= 0) voidzones.RemoveAt(index);
        }
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
            if (hot && changed && before != null) Lose(source, before);
            if (!hot || after == null) continue;
            if (!changed && before != null && before.Param == after.Param
                && (after.ExpiresAt - before.ExpiresAt).TotalSeconds <= 1d) continue;
            var slot = Array.FindIndex(party, p => p.EntityId == source.Actor.Id);
            if (slot < 0) continue;
            slot = party[slot].Slot;
            if (slot is < 0 or >= 4) continue;
            if (after.Id is 160 or 1209) roleExpiry[slot] = after.ExpiresAt;
            else if (after.Id == 2663) jobExpiry[slot] = after.ExpiresAt;
            else if (after.Id is 1096 or 1512) penalty[slot] = true;
        }
        statuses[source] = next;
    }

    private void Lose(PalaceTrashCaster source, ReplayStatus value)
    {
        if (value.Id is not (197 or 198)) return;
        var index = spikes.FindIndex(s => s.Source == source);
        if (index >= 0) spikes.RemoveAt(index);
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
        foreach (var area in base.Collect(now, player)) yield return area;
        if (!ZoneEnabled || BossActive) yield break;
        circles.RemoveAll(s => s.Source.Cast == null);
        knockbacks.RemoveAll(s => s.Source.Cast == null);
        voidzones.RemoveAll(s => s.Source.Actor.Dead || removed.Contains(s.Source));
        for (var i = circles.Count - 1; i >= 0; --i)
        {
            var circle = circles[i];
            var source = circle.Source.Actor;
            yield return new($"zone/circle/{i}", "大范围撤离", new CircleTemplate(circle.Radius),
                Quantized(source.Position), 0, Activation(circle.Source, now), DateTime.MaxValue);
            var distance = Vector2.Distance(new(player.Position.X, player.Position.Z), new(source.Position.X, source.Position.Z));
            if (circle.Radius >= 30 && distance <= circle.Radius)
                yield return new($"zone/escape/{i}", "撤离站位", new DonutTemplate(distance + 10, 10000),
                    source.Position, 0, now, DateTime.MaxValue, Safe: true, NavigationOnly: true, Priority: 100,
                    OverrideDangerMargin: false);
        }
        var slot = party.FirstOrDefault(p => p.EntityId == player.Id)?.Slot ?? -1;
        for (var i = knockbacks.Count - 1; i >= 0; --i)
        {
            var kb = knockbacks[i];
            var hit = Activation(kb.Source, now);
            if (slot is >= 0 and < 4 && (penalty[slot] || roleExpiry[slot] > hit || jobExpiry[slot] > hit)) continue;
            yield return new($"zone/knockback/{i}", "免疫外击退范围", new CircleTemplate(kb.Radius),
                kb.Source.Actor.Position, 0, hit, DateTime.MaxValue);
        }
        for (var i = voidzones.Count - 1; i >= 0; --i)
        {
            var zone = voidzones[i];
            yield return new($"zone/void/{i}", "连续判定范围", zone.Shape, Quantized(zone.Source.Actor.Position),
                zone.Source.Actor.Rotation + zone.Offset, now, DateTime.MaxValue);
        }
        for (var i = los.Count - 1; i >= 0; --i)
        {
            // IterAndExpire processes expiry and output interleaved in reverse
            // order. With same-ID generations, an old expiry can remove the new
            // cache only after that new entry has contributed this frame.
            if (los[i].Cast == null)
            {
                losCache.Remove(los[i].Actor.Id);
                los.RemoveAt(i);
            }
            else if (losCache.TryGetValue(los[i].Actor.Id, out var map))
                foreach (var area in map.Areas($"zone/los/{i}", now, player.Position.Y)) yield return area;
        }
    }

    internal override void ClearFloor()
    {
        base.ClearFloor();
        circles.Clear();
        spikes.Clear();
        los.Clear();
        Array.Clear(roleExpiry);
        Array.Clear(jobExpiry);
        Array.Clear(penalty);
        LosDiagnostic = null;
        // Source ClearState deliberately retains voidzones, knockbacks and LOS cache.
    }

    internal override void Reset()
    {
        base.Reset();
        ClearFloor();
        knockbacks.Clear();
        voidzones.Clear();
        losCache.Clear();
        removed.Clear();
        statuses.Clear();
        party = [];
    }
}
