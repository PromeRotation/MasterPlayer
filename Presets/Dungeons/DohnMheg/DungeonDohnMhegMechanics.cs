using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly HashSet<(ulong Source, uint Action, DateTime Start)> dohnSeenCasts = [];
    private readonly Dictionary<ulong, DateTime> dohnStacks = [];
    private readonly List<(ulong Source, DungeonAmbientAoe Aoe)> dohnGeysers = [];
    private readonly Dictionary<ulong, (uint State, DateTime Time, bool Packet)> dohnAnimations = [];
    private readonly List<ulong> dohnSaplings = [];
    private readonly Dictionary<ulong, DateTime> dohnBirths = [];
    private readonly Dictionary<ulong, (uint Id, ulong Target)> dohnTethers = [];
    private readonly HashSet<ulong> dohnTetherEvents = [];
    private DungeonAmbientAoe? dohnSwinge;
    private DungeonAmbientAoe? dohnBile;
    private DungeonAmbientAoe? dohnTentacles;
    private DateTime dohnFeedingAt;
    private int dohnBileHits;
    private int dohnTentacleHits;
    private int dohnGeyserId;
    private ulong dohnLyreId;
    internal bool DohnChasm { get; private set; }
    internal bool DohnPulling => territory == DohnMhegCatalog.TERRITORY && _casts.Values.Any(c => c.Rule.Action == 13519);
    internal Vector3[] DohnBridgeRoute()
    {
        if (!DohnChasm || DohnPulling || Arena == null || DohnLyre is not { } lyre || !_actors.TryGetValue(PlayerId, out var player)) return [];
        if (Vector2.DistanceSquared(new(player.Position.X, player.Position.Z), new(lyre.Position.X, lyre.Position.Z)) <= 9) return [];
        return DohnMhegBridgePath.Build(player.Position, lyre.Position, Arena);
    }
    private DungeonActor? DohnLyre => Active?.BossOid == 0xF14
        ? _actors.Values.FirstOrDefault(a => a.Oid == 0xF63 && !a.Dead) : null;
    private bool DohnForbidden(DungeonActor actor) => territory == DohnMhegCatalog.TERRITORY
        && actor.Oid == 0xF63 && _actors.TryGetValue(PlayerId, out var player)
        && Vector2.DistanceSquared(new(player.Position.X, player.Position.Z), new(actor.Position.X, actor.Position.Z)) > 15;

    private void DohnCast(DungeonPendingCast cast)
    {
        if (territory != DohnMhegCatalog.TERRITORY || !dohnSeenCasts.Add((cast.Source, cast.Rule.Action, cast.StartedAt))) return;
        if (cast.Rule.Action == 8906)
            dohnSwinge = new("dohn/swinge", "成长体型顺劈", new FanTemplate(50 + _actors[cast.Source].HitboxRadius, MathF.PI / 3),
                _actors[cast.Source].Position, cast.Rotation, Activation: cast.FinishAt);
        if (cast.Rule.Action == 13547)
        {
            dohnBileHits = 0;
            dohnBile = new("dohn/bile", "六段腐蚀喷吐", new FanTemplate(24.875f, MathF.PI / 2),
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
        }
        if (cast.Rule.Action == 13952)
        {
            dohnTentacleHits = 0;
            dohnTentacles = new("dohn/tentacles", "五段触手十字", new DungeonCrossTemplate(38.875f, 3.5f),
                cast.Location, _actors[PrimaryId].Rotation + MathF.PI / 4, Activation: cast.FinishAt.AddSeconds(1));
        }
    }

    private void DohnFinish(DungeonPendingCast cast, bool completed)
    {
        if (territory != DohnMhegCatalog.TERRITORY) return;
        if (cast.Rule.Action == 8906) dohnSwinge = null;
        if (completed && cast.Rule.Action == 13498 && !DohnChasm)
        {
            DohnChasm = true;
            Arena = DohnMhegArenas.Chasm(Arena!.Position.Y);
        }
        if (completed && cast.Rule.Action == 15723) DohnRestoreArena();
        if (completed) return;
        if (cast.Rule.Action == 13547) { dohnBile = null; dohnBileHits = 0; }
        if (cast.Rule.Action == 13952) { dohnTentacles = null; dohnTentacleHits = 0; }
    }

    private void DohnRestoreArena()
    {
        if (!DohnChasm || Arena == null) return;
        DohnChasm = false;
        Arena = DohnMhegArenas.Last(Arena.Position.Y);
    }

    private void DohnAnimation(ulong source, uint state, DateTime time, bool packet)
    {
        if (Active?.BossOid != 0x3F2 || !_actors.TryGetValue(source, out var actor) || actor.Oid is not (0x1EAAA1 or 0x1EAAA2)) return;
        if (dohnAnimations.TryGetValue(source, out var old))
        {
            if (!packet && old.State == state || packet && old.Packet && old.State == state && time <= old.Time) return;
            // An event can follow its restored world frame. Replace that forecast
            // with the original packet clock, without creating a second wave.
            if (packet && !old.Packet && old.State == state)
                dohnGeysers.RemoveAll(g => g.Source == source);
        }
        dohnAnimations[source] = (state, time, packet);
        if (state != 0x00100020) return;
        Vector2[] offsets = actor.Oid == 0x1EAAA1 ? [new(-9, 15), new(0, -16)] : [new(-9, -15), new(0, 5), new(7, -7)];
        var c = MathF.Cos(actor.Rotation);
        var s = MathF.Sin(actor.Rotation);
        foreach (var offset in offsets)
        {
            // WDir.Rotate follows the game's heading convention.
            var position = actor.Position + new Vector3(c * offset.X + s * offset.Y, 0, c * offset.Y - s * offset.X);
            dohnGeysers.Add((source, new($"dohn/geyser/{++dohnGeyserId}", "连续喷泉", new CircleTemplate(6),
                QuantizedDungeonPosition(position), Activation: time.AddSeconds(5.1))));
        }
    }

    private void DohnIcon(ulong target, uint icon, DateTime time)
    {
        if (Active?.BossOid == 0x3F2 && icon == 62) dohnStacks.TryAdd(target, time.AddSeconds(5.1));
    }

    private void DohnCreated(ulong source, DateTime time)
    {
        if (territory != DohnMhegCatalog.TERRITORY) return;
        dohnBirths.TryAdd(source, time);
        if (_actors.GetValueOrDefault(source)?.Oid == 0xEFB)
            dohnFeedingAt = dohnBirths[source].AddSeconds(10.9);
    }

    private void DohnTether(ulong source, ulong target, uint id, bool present)
    {
        if (Active?.BossOid != 0x98E || id is not (17 or 84)) return;
        dohnTetherEvents.Add(source);
        if (present) dohnTethers[source] = (id, target);
        else dohnTethers.Remove(source);
    }

    private void DohnEffect(ulong source, uint action)
    {
        if (territory != DohnMhegCatalog.TERRITORY) return;
        if (action == 8840) dohnStacks.Clear();
        if (action == 8800 && dohnGeysers.Count > 0) dohnGeysers.RemoveAt(0);
        if (action == 8899) { dohnTethers.Remove(source); dohnTetherEvents.Add(source); }
        if (action is 13547 or 13548 && dohnBile != null && ++dohnBileHits == 6) dohnBile = null;
        if (action is 13952 or 13953 && dohnTentacles != null && ++dohnTentacleHits == 5) dohnTentacles = null;
    }

    private void SynchronizeDohn()
    {
        if (territory != DohnMhegCatalog.TERRITORY) return;
        foreach (var id in dohnStacks.Keys.Where(id => !_actors.TryGetValue(id, out var actor) || actor.Dead).ToArray()) dohnStacks.Remove(id);
        if (Active?.BossOid == 0xF14 && DohnChasm && _actors.Values.Any(a => a.Oid == 0xF63 && a.Dead))
            DohnRestoreArena();
        if (DohnLyre is { } lyre) dohnLyreId = lyre.Id;
        if (Active?.BossOid != 0x98E) return;
        foreach (var sapling in _actors.Values.Where(a => a.Oid == 0xEFB && !a.Dead))
            if (!dohnSaplings.Contains(sapling.Id))
            {
                dohnSaplings.Add(sapling.Id);
                dohnFeedingAt = dohnBirths.GetValueOrDefault(sapling.Id, _now).AddSeconds(10.9);
            }
        dohnSaplings.RemoveAll(id => !_actors.TryGetValue(id, out var actor) || actor.Dead);
        foreach (var actor in _actors.Values.Where(a => !a.Dead && !dohnTetherEvents.Contains(a.Id)))
        {
            var tether = actor.Tethers?.FirstOrDefault(t => t.Key is 17 or 84) ?? default;
            if (tether.Key != 0) dohnTethers[actor.Id] = (tether.Key, tether.Value);
            else dohnTethers.Remove(actor.Id);
        }
        foreach (var source in dohnTethers.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead
            || !_actors.TryGetValue(dohnTethers[id].Target, out var target) || target.Dead).ToArray()) dohnTethers.Remove(source);
    }

    private void DohnRemoved(ulong source)
    {
        dohnStacks.Remove(source);
        dohnGeysers.RemoveAll(g => g.Source == source);
        dohnSaplings.Remove(source);
        dohnTethers.Remove(source);
        foreach (var id in dohnTethers.Keys.Where(id => dohnTethers[id].Target == source).ToArray()) dohnTethers.Remove(id);
        if (dohnLyreId == source) { DohnRestoreArena(); dohnLyreId = 0; }
    }

    private void ResetDohn()
    {
        dohnSeenCasts.Clear(); dohnStacks.Clear(); dohnGeysers.Clear(); dohnAnimations.Clear();
        dohnSaplings.Clear(); dohnBirths.Clear(); dohnTethers.Clear(); dohnTetherEvents.Clear();
        dohnSwinge = dohnBile = dohnTentacles = null;
        DohnChasm = false; dohnBileHits = dohnTentacleHits = dohnGeyserId = 0; dohnFeedingAt = default;
        dohnLyreId = 0;
    }
}
