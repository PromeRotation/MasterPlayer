using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(ulong Source, Vector3 Position, float Rotation, int Step, DateTime Activation)> matoyaBreccia = [];
    private readonly Dictionary<ulong, (ulong Target, float HalfWidth, DateTime Activation)> matoyaBaits = [];
    private readonly Dictionary<ulong, ulong> matoyaSeenTethers = [];
    private readonly Dictionary<ulong, DateTime> matoyaTetherEvents = [];
    private readonly Dictionary<ulong, DungeonActor> matoyaBaitTargets = [];
    private readonly HashSet<uint> matoyaHoles = [];
    private readonly List<DungeonAmbientAoe> matoyaGurgles = [];
    private readonly Dictionary<ulong, (Vector3 Position, DateTime Activation)> matoyaGeysers = [];
    private readonly HashSet<ulong> matoyaSeenGeysers = [];
    private readonly Dictionary<ulong, DateTime> matoyaBirths = [];
    private readonly Dictionary<ulong, DateTime> matoyaSpreads = [];
    private readonly List<DungeonPendingCast> matoyaDelayedDamage = [];
    private (Vector3 Origin, float Rotation)? matoyaHuffCache;
    private (Vector3 Origin, float Rotation, float Distance, DateTime Activation)? matoyaHuff;
    private bool matoyaCloud;
    private bool matoyaGeysersActive;
    private bool matoyaBarbeque;
    private bool matoyaImminent;
    private long matoyaSerial;

    private void SynchronizeMatoya()
    {
        if (territory != MatoyaCatalog.TERRITORY || Arena == null) return;
        if (Active?.BossOid is 0x300C or 0x307F)
        {
            var tether = Active.BossOid == 0x300C ? 7u : 8u;
            foreach (var source in _actors.Values.Where(a => !a.Dead))
            {
                if (matoyaTetherEvents.TryGetValue(source.Id, out var changed) && _now < changed.AddSeconds(.5)) continue;
                var target = source.Tethers?.GetValueOrDefault(tether) ?? 0;
                if (target != 0)
                {
                    if (matoyaSeenTethers.GetValueOrDefault(source.Id) != target)
                        MatoyaTether(source.Id, target, tether, true, _now);
                }
                else
                {
                    matoyaSeenTethers.Remove(source.Id);
                    if (tether == 7) matoyaBaits.Remove(source.Id);
                }
            }
            foreach (var (id, bait) in matoyaBaits.ToArray())
            {
                if (!_actors.TryGetValue(id, out var source) || source.Dead) { matoyaBaits.Remove(id); continue; }
                if (_actors.TryGetValue(bait.Target, out var target)) matoyaBaitTargets[target.Id] = target;
                if (tether == 7 && (source.HitboxRadius is > 2 and <= 3 && bait.HalfWidth == 2
                    || source.HitboxRadius > 3 && bait.HalfWidth == 3))
                    matoyaBaits[id] = (bait.Target, bait.HalfWidth + 1, _now.AddSeconds(9.7));
            }
        }
        if (Active?.BossOid == 0x307F)
        {
            if (_actors.TryGetValue(PlayerId, out var player))
            {
                var cloud = MatoyaCatalog.NixieArena(Arena.Position.Y, true).Contains(player.Position, 0);
                if (cloud != matoyaCloud)
                {
                    matoyaCloud = cloud;
                    Arena = MatoyaCatalog.NixieArena(Arena.Position.Y, cloud);
                }
            }
            foreach (var geyser in _actors.Values.Where(a => a.Oid == 0x1EB0C7 && !a.Dead))
                if (matoyaSeenGeysers.Add(geyser.Id))
                    matoyaGeysers[geyser.Id] = (QuantizedDungeonPosition(geyser.Position),
                        matoyaBirths.GetValueOrDefault(geyser.Id, _now).AddSeconds(3.9));
        }
        foreach (var id in matoyaSpreads.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) matoyaSpreads.Remove(id);
    }

    private void MatoyaTether(ulong source, ulong target, uint tether, bool present, DateTime time)
    {
        if (territory != MatoyaCatalog.TERRITORY
            || !(Active?.BossOid == 0x300C && tether == 7 || Active?.BossOid == 0x307F && tether == 8)) return;
        matoyaTetherEvents[source] = time;
        if (present && _actors.ContainsKey(source) && _actors.ContainsKey(target))
        {
            matoyaSeenTethers[source] = target;
            matoyaBaitTargets[target] = _actors[target];
            matoyaBaits[source] = (target, tether == 7 ? 2 : 1.5f, time.AddSeconds(tether == 7 ? 8.2 : 5.4));
        }
        else if (tether == 7)
        {
            matoyaBaits.Remove(source);
            // A packet may arrive before the next object frame drops the tether.
            matoyaSeenTethers[source] = target;
        }
    }

    private void MatoyaMap(uint index, uint state, DateTime time)
    {
        if (territory != MatoyaCatalog.TERRITORY || Arena == null) return;
        if (Active?.BossOid == 0x300C && index is >= 0x0A and <= 0x0D)
        {
            if (state == 0x00020001) matoyaHoles.Add(index);
            else if (state == 0x00080004) matoyaHoles.Remove(index);
        }
        if (Active?.BossOid == 0x307F)
        {
            if (index == 0x12)
            {
                if (state == 0x00020001) matoyaGeysersActive = true;
                else if (state == 0x00080004) matoyaGeysersActive = false;
            }
            if (state == 0x00020001 && index is >= 0x13 and <= 0x1A)
            {
                var left = index < 0x17;
                var position = QuantizedDungeonPosition(new(left ? -20 : 20, Arena.Position.Y, -165 + (index - (left ? 0x13 : 0x17)) * 10));
                matoyaGurgles.Add(new($"matoya/gurgle/{++matoyaSerial}", "场边逐列水流",
                    RayRectangleTemplate.Symmetric(10, 60), position,
                    (left ? 89.999f : -90.004f) * MathF.PI / 180, Activation: time.AddSeconds(9)));
            }
        }
        if (Active?.BossOid == 0x300B && index == 0x10 && state == 0x00020001) matoyaBarbeque = true;
    }

    private void MatoyaCast(DungeonPendingCast cast)
    {
        if (territory != MatoyaCatalog.TERRITORY) return;
        var action = cast.Rule.Action;
        if (action == 21646)
        {
            matoyaBreccia.RemoveAll(s => s.Source == cast.Source && s.Step == 0);
            matoyaBreccia.Add((cast.Source, cast.Location, cast.Rotation, 0, cast.FinishAt));
        }
        if (action == 22810) matoyaHuffCache = (cast.Location, cast.Rotation);
        if (action == 20045 && matoyaHuffCache is { } cache) matoyaHuff = (cache.Origin, cache.Rotation, 50, cast.StartedAt.AddSeconds(5.4));
        if (action == 22807) matoyaImminent = false;
        if (action is 22803 or 22801)
        {
            matoyaDelayedDamage.RemoveAll(c => action == 22803 && c.Rule.Action == action
                || c.Source == cast.Source && c.Rule.Action == action && c.StartedAt == cast.StartedAt);
            matoyaDelayedDamage.Add(cast with { FinishAt = cast.FinishAt.AddSeconds(action == 22803 ? .8 : .9),
                TargetId = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId });
        }
    }

    private void MatoyaFinish(DungeonPendingCast cast, DateTime time)
    {
        if (territory != MatoyaCatalog.TERRITORY) return;
        var order = (int)cast.Rule.Action - 21646;
        if (order is >= 0 and <= 2)
        {
            var index = matoyaBreccia.FindIndex(s => s.Step == order
                && Math.Abs(s.Position.X - cast.Location.X) <= 1 && Math.Abs(s.Position.Z - cast.Location.Z) <= 1
                && Math.Abs(MathF.Atan2(MathF.Sin(s.Rotation - cast.Rotation), MathF.Cos(s.Rotation - cast.Rotation))) <= .05f);
            if (index >= 0)
            {
                var s = matoyaBreccia[index];
                if (order == 2) matoyaBreccia.RemoveAt(index);
                else matoyaBreccia[index] = (s.Source, s.Position, s.Rotation, order + 1, time.AddSeconds(1.5));
            }
        }
        if (cast.Rule.Action == 20020 && matoyaHuffCache is { } cache)
            matoyaHuff = (cache.Origin, cache.Rotation, 15, time.AddSeconds(10.9));
        if (cast.Rule.Action == 23331) matoyaImminent = true;
    }

    private void MatoyaEffect(ulong source, uint action, ulong target)
    {
        if (territory != MatoyaCatalog.TERRITORY) return;
        if (action == 22930) matoyaGurgles.Clear();
        if (action == 22928) matoyaBaits.Clear();
        if (action == 22926 && _actors.TryGetValue(source, out var caster))
        {
            var id = matoyaGeysers.FirstOrDefault(g => Math.Abs(g.Value.Position.X - caster.Position.X) <= 1
                && Math.Abs(g.Value.Position.Z - caster.Position.Z) <= 1).Key;
            matoyaGeysers.Remove(id);
        }
        if (action == 22819) matoyaSpreads.Remove(target);
        if (action == 22804) matoyaDelayedDamage.RemoveAll(c => c.Rule.Action == 22803);
        if (action == 22802)
        {
            var index = matoyaDelayedDamage.FindIndex(c => c.Rule.Action == 22801 && c.TargetId == target);
            if (index >= 0) matoyaDelayedDamage.RemoveAt(index);
        }
        if (action == 22821) matoyaBarbeque = false;
        if (action is 22811 or 22812) { matoyaHuff = null; matoyaHuffCache = null; }
    }

    private IEnumerable<DungeonDamagePrediction> MatoyaDamage()
    {
        foreach (var cast in matoyaDelayedDamage)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && (cast.Rule.Action == 22803 || a.Id == cast.TargetId)))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, cast.FinishAt, cast.Rule.Action == 22803 ? "Raidwide" : "Tankbuster");
        if (Active?.BossOid == 0x307F)
            foreach (var bait in matoyaBaits.Values)
                if (_actors.TryGetValue(bait.Target, out var actor) && !actor.Dead && actor.PartySlot >= 0)
                    yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, bait.Activation, "Tankbuster");
    }

    private void MatoyaRemoved(ulong source, DateTime time)
    {
        // Upstream destruction fires CastFinished before removing the actor.
        if (_casts.TryGetValue(source, out var cast)) MatoyaFinish(cast, time);
        matoyaBaits.Remove(source); matoyaSeenTethers.Remove(source); matoyaTetherEvents.Remove(source);
        // BaitAway permits dead targets. Retain their last position until hit/untether.
        matoyaSpreads.Remove(source); matoyaBirths.Remove(source);
    }

    private void ResetMatoya()
    {
        matoyaBreccia.Clear(); matoyaBaits.Clear(); matoyaSeenTethers.Clear(); matoyaTetherEvents.Clear(); matoyaBaitTargets.Clear(); matoyaHoles.Clear();
        matoyaGurgles.Clear(); matoyaGeysers.Clear(); matoyaSeenGeysers.Clear(); matoyaBirths.Clear(); matoyaSpreads.Clear(); matoyaDelayedDamage.Clear();
        matoyaHuff = null; matoyaHuffCache = null; matoyaCloud = false; matoyaGeysersActive = false; matoyaBarbeque = false; matoyaImminent = false; matoyaSerial = 0;
    }
}
