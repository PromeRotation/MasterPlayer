using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DateTime twinNextPlummet;
    private DateTime twinNextSentence;
    private bool twinTankedByOT;
    private int twinSentenceHits;
    private readonly Dictionary<ulong, DateTime> twinSentenceCasts = [];
    private ulong twinFireball;
    private DateTime twinFireballAt;
    private DateTime twinNextFireball;
    private ulong twinConflag;
    private DateTime twinConflagAt;
    private DateTime twinNextConflag;
    private readonly HashSet<ulong> twinFettered = [];
    private Vector3? twinDiveTarget;
    private DateTime twinDiveAt;
    private DateTime twinProfusionAt;
    private int twinProfusionHits;
    private readonly List<(Vector3 Position, DateTime At)> twinTwisterPositions = [];
    private ulong twinDreadTarget;
    private ulong twinLiquidTarget;
    private int twinLiquidHits;
    private ulong twinHatchTarget;
    private readonly List<(Vector3 Position, DateTime At, int Key)> twinPuddles = [];
    private int twinPuddleKey;
    private readonly Dictionary<ulong, (DateTime Expiry, DateTime At, bool Present)> twinDisseminate = [];
    private readonly Dictionary<ulong, DungeonActor> twinMarkedActors = [];
    private string TwinTankRole => twinTankedByOT ? "OT" : "MT";
    private DungeonActor? TwinMarked(ulong id) => _actors.GetValueOrDefault(id) ?? twinMarkedActors.GetValueOrDefault(id);
    private ulong TwinMark(ulong id)
    {
        if (_actors.GetValueOrDefault(id) is not { } actor) return 0;
        twinMarkedActors[id] = actor;
        return id;
    }

    private void TwinObserve()
    {
        if (!IsTwintania || Active == null) return;
        TwinTick();
        foreach (var id in twinMarkedActors.Keys.ToArray())
            if (_actors.GetValueOrDefault(id) is { } actor) twinMarkedActors[id] = actor;
        if (TwinP2)
        {
            var fettered = _actors.Values.Where(a => HasStatus(a, 292)).Select(a => a.Id).ToHashSet();
            if (fettered.Contains(twinConflag) && !twinFettered.Contains(twinConflag)) twinConflag = 0;
            twinFettered.Clear(); twinFettered.UnionWith(fettered);
        }
        if (TwinP4)
        {
            if (!_actors.Values.Any(a => a.Oid == 0x7EA && !a.Dead)) twinDreadTarget = 0;
            if (twinTwisterPositions.Count == 0 && _actors.GetValueOrDefault(PrimaryId)?.Cast is { Action: 1249 } cast
                && cast.FinishAt <= _now)
                twinTwisterPositions.AddRange(_actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).Select(a => (a.Position, cast.FinishAt)));
            // Source clears predictions even when the first observed object has EventState 7.
            if (_actors.Values.Any(a => a.Oid == 0x1E8910)) twinTwisterPositions.Clear();
        }
        if (twinStage == 0 || TwinP5)
            foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1E88FE && a.EventState != 7))
            {
                var index = twinPuddles.FindIndex(p => Vector2.DistanceSquared(new(p.Position.X, p.Position.Z), new(actor.Position.X, actor.Position.Z)) <= 4);
                if (index >= 0) twinPuddles.RemoveAt(index);
            }
    }

    private void TwinCast(DungeonPendingCast cast)
    {
        if (!TwinCommonActive || cast.Rule.Action != 1458) return;
        if (twinSentenceCasts.TryGetValue(cast.Source, out var previous)
            && Math.Abs((cast.StartedAt - previous).TotalSeconds) < .25) return;
        twinSentenceCasts[cast.Source] = cast.StartedAt;
        twinNextSentence = _now.AddSeconds(36);
        twinTankedByOT = !twinTankedByOT;
    }

    private void TwinEffect(ulong source, uint action, ulong target, Vector3? location)
    {
        if (!IsTwintania || Active == null) return;
        if (TwinCommonActive)
        {
            if (action == 1240) twinNextPlummet = _now.AddSeconds(12.5);
            if (action == 1458) ++twinSentenceHits;
        }
        if (TwinP2)
        {
            if (action == 1452)
            {
                twinFireball = TwinMark(target);
                twinFireballAt = _now.AddSeconds(4.5); twinNextFireball = _now.AddSeconds(25);
            }
            if (action == 1246) twinFireball = 0;
            if (action == 1451)
            {
                twinConflag = TwinMark(target);
                twinConflagAt = _now.AddSeconds(2.5); twinNextConflag = _now.AddSeconds(35);
            }
        }
        if (TwinDiveActive)
        {
            if (action == 1456) { twinDiveTarget = _actors.GetValueOrDefault(target)?.Position; twinDiveAt = _now.AddSeconds(1.7); }
            if (action == 1247) twinDiveTarget = null;
        }
        if (twinStage == 17 && action == 1248) ++twinProfusionHits;
        if (TwinP4 && action == 1251) twinDreadTarget = TwinMark(target);
        if (TwinP5)
        {
            if (action == 1457) twinLiquidTarget = TwinMark(target);
            if (action == 670 && ++twinLiquidHits % 5 == 0) twinLiquidTarget = 0;
            if (action == 1453) twinHatchTarget = TwinMark(target);
            if (action == 1256) twinHatchTarget = 0;
        }
        if ((twinStage == 0 && action == 1243 || TwinP5 && action == 670)
            && (_actors.GetValueOrDefault(target)?.Position ?? location) is { } position)
            twinPuddles.Add((position, TwinP5 ? _now.AddSeconds(1.5) : _now, ++twinPuddleKey));
    }

    private void TwinStatus(ulong target, uint status, bool present, DateTime? expiry)
    {
        if (!IsTwintania || Active == null) return;
        if (TwinP2 && status == 292 && present)
        {
            twinFettered.Add(target);
            if (target == twinConflag) twinConflag = 0;
        }
        if (status == 348) twinDisseminate[target] = (expiry ?? default, _now, present);
    }

    private bool TwinKillHygieia()
    {
        var asclepius = _actors.Values.FirstOrDefault(a => a.Oid == 0x7E9);
        if (asclepius == null) return true;
        if (twinDisseminate.TryGetValue(asclepius.Id, out var pending) && _now < pending.At.AddSeconds(.5))
            return !pending.Present || (pending.Expiry - _now).TotalSeconds < 10;
        return !HasStatus(asclepius, 348) || (asclepius.StatusExpirations?.GetValueOrDefault(348u) - _now)?.TotalSeconds < 10;
    }

    private static string TwinCombatRole(DungeonActor actor) => actor.ClassJob switch
    {
        1 or 3 or 19 or 21 or 32 or 37 => "Tank",
        6 or 24 or 28 or 33 or 40 => "Healer",
        2 or 4 or 20 or 22 or 29 or 30 or 34 or 39 or 41 => "Melee",
        5 or 7 or 23 or 25 or 26 or 27 or 31 or 35 or 36 or 38 or 42 => "Ranged",
        _ => actor.PartyRole,
    };

    private int TwinPriority(DungeonActor actor)
    {
        if (twinAddsActive)
        {
            if (actor.Oid == 0x7E9) return 1;
            if (actor.Oid == 0x7E8)
            {
                if (actor.Hp == 1) return 0;
                var next = _actors.Values.Where(a => a.Oid == 0x7E8 && !a.Dead).MinBy(a => a.Id);
                if (TwinKillHygieia() && actor.Id == next?.Id) return 2;
                var predicted = (long)(actor.Hp ?? 0) + (actor.IncomingEffects?.Sum(e => e.HpDelta) ?? 0);
                return predicted < .3f * actor.MaxHp ? -1 : 1;
            }
        }
        if (actor.Id == PrimaryId) return 1;
        if (twinStage == 0 && actor.Oid == 0x7E7) return 2;
        if (TwinP2 && actor.Oid == 0x7F2) return 2;
        if (TwinP4 && actor.Oid == 0x7EA) return CoilRole(_actors.GetValueOrDefault(PlayerId)) != TwinTankRole ? 2 : 0;
        return -1;
    }
}
