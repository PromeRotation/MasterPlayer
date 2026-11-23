using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private int deadEndsWind;
    private long deadEndsSerial;
    private readonly List<DeadEndsExaflareLine> deadEndsLines = [];
    private readonly Dictionary<(ulong Source, DateTime Start), long> deadEndsSeenLines = [];
    private readonly List<DungeonAmbientAoe> deadEndsExaAoes = [];
    private DungeonPendingCast? deadEndsWave;
    private int deadEndsWaveWind;
    private ulong deadEndsSolitude;
    private DateTime deadEndsSolitudeAt;
    private DungeonAmbientAoe? deadEndsRing;
    private bool deadEndsSmall;
    private DungeonPendingCast? deadEndsElimination;
    private readonly List<DungeonAmbientAoe> deadEndsVoidPredictions = [];
    private readonly List<ulong> deadEndsLightOrder = [];

    internal DungeonActor? DeadEndsCleanseTarget(uint job)
    {
        if (territory != DeadEndsCatalog.TERRITORY || Active?.BossOid != 0x34C4
            || job is not (6 or 24 or 28 or 33 or 40 or 23) || !_actors.TryGetValue(PlayerId, out var player)) return null;
        return _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Targetable && HasStatus(a, 2965)
            && Vector3.DistanceSquared(player.Position, a.Position) <= MathF.Pow(30 + a.HitboxRadius, 2))
            .OrderBy(a => a.Id != PlayerId).ThenBy(a => a.PartySlot).FirstOrDefault();
    }

    private void DeadEndsCast(DungeonPendingCast cast)
    {
        if (territory != DeadEndsCatalog.TERRITORY || Arena == null) return;
        if (cast.Rule.Action is 25920 or 25949)
        {
            var target = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId;
            delayedDamage[cast.Source] = cast with { TargetId = target };
        }
        if (cast.Rule.Action == 25919 && _actors.TryGetValue(cast.Source, out var actor))
        {
            var key = (cast.Source, cast.StartedAt);
            if (!deadEndsSeenLines.ContainsKey(key))
            {
                var count = DeadEndsExplosionCount(actor.Position, Arena.Position, deadEndsWind);
                var line = new DeadEndsExaflareLine(++deadEndsSerial, cast.Source, actor.Position,
                    Vector3.UnitZ * (deadEndsWind == 1 ? 6 : -6), cast.FinishAt, count);
                deadEndsSeenLines[key] = line.Id;
                deadEndsLines.Add(line); RebuildDeadEndsExa();
            }
        }
        if (cast.Rule.Action == 28347)
        {
            if (deadEndsWave?.Source != cast.Source || deadEndsWave.StartedAt != cast.StartedAt) deadEndsWaveWind = deadEndsWind;
            deadEndsWave = cast;
        }
        if (cast.Rule.Action == 25936 && !deadEndsSmall)
            deadEndsRing = new("deadends/ring", "场地即将缩小", new DonutTemplate(16, 20), Arena.Position, Activation: cast.FinishAt.AddSeconds(.4));
        if (cast.Rule.Action == 25935) deadEndsElimination = cast;
        if (cast.Rule.Action == 25939 && !deadEndsLightOrder.Contains(cast.Source)) deadEndsLightOrder.Add(cast.Source);
    }

    internal static int DeadEndsExplosionCount(Vector3 position, Vector3 center, int wind)
    {
        if (wind == 0) return 0;
        if (wind == 1)
        {
            if (position.Z > -178) return 1;
            return position.Z switch
            {
                -184.5f => 3, -187.5f => 4, -191.4f => 5, -194.5f => 6, -196.9f => 7,
                _ => Vector3.DistanceSquared(position, center) > 100 && position.Z == -178 ? 3 : 4,
            };
        }
        if (position.Z < -178) return 1;
        return position.Z switch
        {
            -171.5f => 3, -168.5f => 4, -164.6f => 5, -161.5f => 6, -159.1f => 7,
            _ => Vector3.DistanceSquared(position, center) > 100 && position.Z == -178 ? 3 : 4,
        };
    }

    private void DeadEndsFinish(DungeonPendingCast cast, DateTime time)
    {
        if (territory != DeadEndsCatalog.TERRITORY) return;
        if (cast.Rule.Action is 25919 or 28348)
        {
            var index = deadEndsLines.FindIndex(l => Math.Abs(l.Next.X - cast.Location.X) <= 1 && Math.Abs(l.Next.Z - cast.Location.Z) <= 1);
            if (index >= 0)
            {
                var line = deadEndsLines[index];
                if (line.Left <= 1) deadEndsLines.RemoveAt(index);
                else deadEndsLines[index] = line with { Next = cast.Location + line.Advance, NextAt = time.AddSeconds(2), Left = line.Left - 1 };
                if (deadEndsLines.Count == 0) deadEndsWind = 0;
                RebuildDeadEndsExa();
            }
        }
        if (cast.Rule.Action == 28347) deadEndsWave = null;
        deadEndsLightOrder.Remove(cast.Source);
    }

    private void RebuildDeadEndsExa()
    {
        deadEndsExaAoes.Clear();
        foreach (var line in deadEndsLines)
        {
            for (var i = 0; i < Math.Min(line.Left, 5); ++i)
            {
                var at = line.NextAt;
                if (i > 0) at = (at > _now ? at : _now).AddSeconds(2 * i);
                deadEndsExaAoes.Add(new($"deadends/exa/{line.Id}/{i}", "风向连续瘴气", new CircleTemplate(6), line.Next + line.Advance * i, Activation: at));
            }
        }
    }

    private void DeadEndsMap(uint index, uint state)
    {
        if (territory != DeadEndsCatalog.TERRITORY || Arena == null) return;
        if (index == 0x2B && state is 0x00020080 or 0x00200040) deadEndsWind = state == 0x00020080 ? 1 : 2;
        if (Active?.BossOid == 0x34C6 && index == 0x17 && state == 0x00020001)
        {
            deadEndsSmall = true; deadEndsRing = null; Arena = DeadEndsArenas.PeacekeeperSmall(Arena.Position.Y);
        }
    }

    private void DeadEndsStatus(ulong target, uint status, bool present, DateTime time)
    {
        if (territory == DeadEndsCatalog.TERRITORY && Active?.BossOid == 0x34C4 && status == 2966 && present && deadEndsSolitude == 0)
        {
            deadEndsSolitude = target; deadEndsSolitudeAt = time.AddSeconds(5);
        }
    }

    private DungeonActor[] DeadEndsVoidzones()
    {
        var zones = _actors.Values.Where(a => a.Oid == 0x1EB5F7).ToArray();
        return zones.FirstOrDefault()?.EventState != 7 ? zones : [];
    }

    private void SynchronizeDeadEnds()
    {
        if (territory != DeadEndsCatalog.TERRITORY) return;
        var members = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        if (!members.Any(a => HasStatus(a, 2966))) deadEndsSolitude = 0;
        else if (deadEndsSolitude == 0)
        {
            var first = members.First(a => HasStatus(a, 2966)); deadEndsSolitude = first.Id; deadEndsSolitudeAt = _now.AddSeconds(5);
        }
        if (deadEndsElimination is { } bait && (!_actors.TryGetValue(bait.Source, out var source) || source.Dead
            || !_actors.TryGetValue(bait.TargetId, out var target) || target.Dead)) deadEndsElimination = null;
        foreach (var zone in DeadEndsVoidzones())
        {
            var index = deadEndsVoidPredictions.FindIndex(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(zone.Position.X, zone.Position.Z)) <= 4);
            if (index >= 0) deadEndsVoidPredictions.RemoveAt(index);
        }
    }

    private void DeadEndsEffect(ulong source, uint action, ulong target, Vector3? location, DateTime time)
    {
        if (territory != DeadEndsCatalog.TERRITORY) return;
        if (action == 25935 && deadEndsElimination?.Source == source) deadEndsElimination = null;
        if (action == 28360 && (_actors.TryGetValue(target, out var actor) ? actor.Position : location) is { } position)
            deadEndsVoidPredictions.Add(new($"deadends/void/predict/{++deadEndsSerial}", "电场即将出现", new CircleTemplate(9), position, Activation: time.AddSeconds(.7)));
    }

    private void ResetDeadEnds()
    {
        deadEndsWind = 0; deadEndsSerial = 0; deadEndsLines.Clear(); deadEndsSeenLines.Clear(); deadEndsExaAoes.Clear();
        deadEndsWave = null; deadEndsWaveWind = 0; deadEndsSolitude = 0; deadEndsSolitudeAt = default;
        deadEndsRing = null; deadEndsSmall = false; deadEndsElimination = null; deadEndsVoidPredictions.Clear(); deadEndsLightOrder.Clear();
    }
}
