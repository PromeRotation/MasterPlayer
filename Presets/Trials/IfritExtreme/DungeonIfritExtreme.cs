using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsIfritExtreme => territory == IfritExtremeCatalog.TERRITORY;
    private bool ifritExWiping;
    private bool ifritExInitialized;
    private int ifritExWave;
    private IfritExtremeStep ifritExStep;
    private DateTime ifritExEnteredAt;
    private DateTime ifritExNailsAt;
    private int ifritExIncinerates;
    private readonly List<ulong> ifritExNails = [];
    private ulong ifritExWindTarget;
    private int ifritExWindsLeft;
    private DateTime ifritExWindAt;
    private DateTime ifritExWindShowAfter;
    private DateTime ifritExBaitDeadline;
    private readonly HashSet<ulong> ifritExBaiters = [];
    private readonly HashSet<ulong> ifritExFetters = [];
    private ushort ifritExFetterStrength;
    private DateTime ifritExHellfireAt;
    private bool IfritExNormal => ifritExStep is IfritExtremeStep.BeforeNails or IfritExtremeStep.Disappear or IfritExtremeStep.Enrage;
    private bool IfritExNails => ifritExStep == IfritExtremeStep.Nails;
    private bool IfritExAi => IfritExNormal || IfritExNails || IfritExHellfire;
    private bool IfritExHellfire => ifritExStep is IfritExtremeStep.HellfireStart or IfritExtremeStep.HellfireEnd or IfritExtremeStep.InvincibleEnd;
    private bool IfritExIncinerate => IfritExNormal || IfritExNails || ifritExStep == IfritExtremeStep.PlumeStart;
    private bool IfritExEruption => ifritExStep is IfritExtremeStep.BeforeNails or IfritExtremeStep.Nails or IfritExtremeStep.Enrage;
    private bool IfritExFettersEnabled => IfritExEruption && (ifritExWave > 0 || ifritExStep == IfritExtremeStep.Enrage);
    private bool IfritExPlumes => ifritExStep is IfritExtremeStep.PlumeEnd or IfritExtremeStep.PlumeResolve;
    private bool IfritExErupting => IfritExEruption && _casts.Values.Any(c => c.Rule.Action == 1677);
    private DungeonActor? IfritExBoss => _actors.GetValueOrDefault(PrimaryId);
    private bool IfritExCasting => _casts.ContainsKey(PrimaryId) || IfritExBoss?.Cast != null;
    private bool IfritExSmallNails => _actors.Values.Any(a => a.Oid == 0xD4 && a.Targetable && !a.Dead);
    private DungeonActor? IfritExNextNail => ifritExNails.Count > 0 ? _actors.GetValueOrDefault(ifritExNails[0]) : null;
    private string IfritExTankRole
    {
        get
        {
            if (IfritExNails)
            {
                var mask = ifritExWave switch { 0 => 0x8u, 1 => 0x7u, _ => 0x3C70u };
                return ifritExIncinerates < 32 && (mask & (1u << ifritExIncinerates)) != 0 ? "OT" : "MT";
            }
            if (ifritExStep == IfritExtremeStep.BeforeNails || IfritExHellfire) return ifritExWave == 1 ? "OT" : "MT";
            return "Unassigned";
        }
    }

    private void IfritExDirector(uint command)
    {
        if (!IsIfritExtreme) return;
        if (command == 0x40000005) { ifritExWiping = true; ResetFight(); }
        if (command == 0x40000011) ifritExWiping = false;
    }

    private void IfritExObserve()
    {
        if (!IsIfritExtreme || Active == null || IfritExBoss is not { } boss) return;
        if (!ifritExInitialized)
        {
            ifritExInitialized = true;
            IfritExEnter(IfritExtremeStep.BeforeNails);
        }
        ifritExNails.RemoveAll(id => !_actors.TryGetValue(id, out var nail) || nail.Dead);
        // A zero-caster frame clears baiters, independently of the visual cast's timer.
        if (!IfritExErupting) ifritExBaiters.Clear();
        for (var transitions = 0; transitions < 32; ++transitions)
        {
            var ready = ifritExStep switch
            {
                IfritExtremeStep.BeforeNails => IfritExSmallNails,
                IfritExtremeStep.Nails => !boss.Targetable,
                IfritExtremeStep.HellfireStart or IfritExtremeStep.PlumeStart => IfritExCasting,
                IfritExtremeStep.HellfireEnd or IfritExtremeStep.PlumeEnd => !IfritExCasting,
                IfritExtremeStep.InvincibleEnd => !HasStatus(boss, 775),
                IfritExtremeStep.PlumeResolve => !_casts.Values.Any(c => c.Rule.Action == 1359),
                IfritExtremeStep.Disappear => !boss.Targetable || IfritExSmallNails,
                IfritExtremeStep.Cyclone => boss.Targetable,
                _ => false,
            };
            double? timeout = ifritExStep switch
            {
                IfritExtremeStep.BeforeNails => 1001,
                IfritExtremeStep.Nails => (ifritExWave switch { 0 => 45, 1 => 75, _ => 115 }) + 1000,
                IfritExtremeStep.InvincibleEnd => 15.1,
                IfritExtremeStep.PlumeResolve => 1.9,
                IfritExtremeStep.Disappear => 23.5,
                _ => null,
            };
            if (!ready)
            {
                if (timeout is not { } limit || (_now - ifritExEnteredAt).TotalSeconds < limit) break;
                warning?.Invoke($"Ifrit Extreme：{ifritExStep} 未收到预期事件，按源超时推进。");
            }
            if (ifritExStep == IfritExtremeStep.Enrage) break;
            if (ifritExStep == IfritExtremeStep.Cyclone)
            {
                ++ifritExWave;
                IfritExEnter(ifritExWave < 3 ? IfritExtremeStep.BeforeNails : IfritExtremeStep.Enrage);
            }
            else IfritExEnter(ifritExStep + 1);
        }
    }

    private void IfritExEnter(IfritExtremeStep step)
    {
        var hadEruption = IfritExEruption;
        var hadFetters = IfritExFettersEnabled;
        var hadIncinerate = IfritExIncinerate;
        ifritExStep = step;
        ifritExEnteredAt = _now;
        if (!hadIncinerate && IfritExIncinerate || step == IfritExtremeStep.Nails) ifritExIncinerates = 0;
        if (hadEruption && !IfritExEruption) { ifritExBaiters.Clear(); ifritExBaitDeadline = default; }
        if (hadFetters && !IfritExFettersEnabled) { ifritExFetters.Clear(); ifritExFetterStrength = 0; }
        if (!hadFetters && IfritExFettersEnabled)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && HasStatus(a, 377)))
                IfritExStatus(actor.Id, true, actor.StatusParams?.GetValueOrDefault(377u) ?? 0);
        if (step == IfritExtremeStep.Nails)
        {
            ifritExNailsAt = _now;
            IfritExSnapshotNails();
            ifritExNails.RemoveAll(id => !_actors.TryGetValue(id, out var nail) || nail.Dead);
        }
        if (step == IfritExtremeStep.HellfireStart)
            ifritExHellfireAt = _casts.Values.FirstOrDefault(c => c.Rule.Action == 1536)?.FinishAt ?? _now.AddSeconds(4.7);
        foreach (var id in _casts.Values.Where(c => !IfritExCastAllowed(c.Rule.Action)).Select(c => c.Source).ToArray()) _casts.Remove(id);
        // Component activation replays currently observed casts, e.g. plume helpers
        // already casting before the boss visual ends.
        foreach (var actor in _actors.Values.Where(a => a.Cast != null).ToArray())
            if (!_casts.ContainsKey(actor.Id) && IfritExCastAllowed(actor.Cast!.Action))
                StartCast(actor.Id, actor.Cast!, _now, fromPacket: false);
    }

    private void IfritExSnapshotNails()
    {
        ifritExNails.Clear();
        var small = _actors.Values.Where(a => a.Oid == 0xD4).ToArray();
        var first = small.MinBy(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(15, 0)));
        if (first != null)
        {
            ifritExNails.Add(first.Id);
            var angle = MathF.Atan2(first.Position.X, first.Position.Z);
            ifritExNails.AddRange(small.Where(a => a.Id != first.Id).OrderBy(a =>
            {
                var delta = angle - MathF.Atan2(a.Position.X, a.Position.Z);
                return delta < 0 ? delta + MathF.Tau : delta;
            }).ThenBy(a => a.Position.X * a.Position.X + a.Position.Z * a.Position.Z).Select(a => a.Id));
        }
        ifritExNails.AddRange(_actors.Values.Where(a => a.Oid == 0x91B).Select(a => a.Id));
    }

    private bool IfritExCastAllowed(uint action) => action switch
    {
        1529 or 1536 or 1356 => true,
        1676 or 1677 => IfritExEruption,
        1359 => IfritExPlumes,
        1532 => ifritExStep == IfritExtremeStep.Cyclone,
        _ => false,
    };

    private void IfritExCast(DungeonPendingCast cast)
    {
        if (!IsIfritExtreme) return;
        if (cast.Rule.Action == 1529)
        {
            ifritExWindTarget = _actors.ContainsKey(cast.TargetId) ? cast.TargetId : 0;
            ifritExWindsLeft = 3;
            ifritExWindAt = _now.AddSeconds(5.4);
            ifritExWindShowAfter = _now.AddSeconds(3.4);
        }
        if (cast.Rule.Action == 1676) ifritExBaitDeadline = _now.AddSeconds(1);
        if (cast.Rule.Action == 1677 && _now < ifritExBaitDeadline
            && _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).MinBy(a =>
                Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(cast.Location.X, cast.Location.Z))) is { } baiter)
            ifritExBaiters.Add(baiter.Id);
        if (cast.Rule.Action == 1536 && IfritExHellfire) ifritExHellfireAt = cast.FinishAt;
    }

    private void IfritExEffect(uint action, DateTime at)
    {
        if (!IsIfritExtreme) return;
        if (action == 1528 && IfritExIncinerate) ++ifritExIncinerates;
        if (action != 1530) return;
        if (--ifritExWindsLeft == 0) { ifritExWindTarget = 0; ifritExWindShowAfter = DateTime.MaxValue; }
        else ifritExWindAt = at.AddSeconds(6);
    }

    private void IfritExStatus(ulong target, bool present, ushort strength)
    {
        if (!IsIfritExtreme || !IfritExFettersEnabled || _actors.GetValueOrDefault(target) is not { PartySlot: >= 0 }) return;
        if (present) ifritExFetters.Add(target);
        else ifritExFetters.Remove(target);
        ifritExFetterStrength = present ? strength : (ushort)0;
    }

    private void ResetIfritEx()
    {
        ifritExInitialized = false;
        ifritExWave = 0;
        ifritExStep = default;
        ifritExNails.Clear(); ifritExBaiters.Clear(); ifritExFetters.Clear();
        ifritExWindTarget = 0;
        ifritExWindsLeft = ifritExIncinerates = 0;
        ifritExWindShowAfter = DateTime.MaxValue;
        ifritExBaitDeadline = default;
        ifritExFetterStrength = 0;
    }
}
