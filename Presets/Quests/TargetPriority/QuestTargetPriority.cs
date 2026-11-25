using System.Numerics;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Quests;

internal static class QuestTargetPriority
{
    private static readonly HashSet<uint> invincibleStatuses =
    [
        151, 198, 325, 328, 385, 394, 469, 529, 592, 656, 671, 775, 776, 895, 969, 981, 1240, 1302,
        1303, 1567, 1570, 1697, 1829, 1936, 2413, 2654, 3012, 3039, 3052, 3054, 4410, 4175
    ];
    internal static bool OwnsTerritory(uint territory) => territory is 1190 or 960 or 1192 or 814 or 540 or 957 or 818 or 545 or 547 or 552 or 476 or 488 or 484 or 466 or 233 or 252 or 260 or 275 or 315 or 461 or 414 or 669 or 673 or 251 or 304 or 309 or 410 or 273 or 317 or 314 or 323 or 307 or 498 or 267 or 1127 or 1128 or 1129;
    internal static bool PartialOverride(uint territory) => territory is 1190 or 960 or 1192 or 814 or 540 or 957 or 818 or 545 or 547 or 552 or 476 or 488 or 484 or 466 or 273 or 317 or 314 or 323 or 307 or 498 or 267 or 1127 or 1128 or 1129;

    internal static int Priority(uint territory, ReplayActorState actor)
        => AssignedPriority(territory, actor) ?? 0;

    internal static bool ExplicitlyPointless(uint territory, ReplayActorState actor)
        => LegalTarget(actor) && AssignedPriority(territory, actor) == -1;

    private static int? AssignedPriority(uint territory, ReplayActorState actor)
    {
        var oid = actor.Oid;
        return territory switch
        {
            540 when oid == 0x1564 => -4,
            233 => oid switch { 0x224 => 0, 0x376 => 1, _ => 2 },
            252 => oid == 0x386 ? 0 : 1,
            260 => oid switch { 0x2BD or 0x2BE => 2, 0x2BA => 0, _ => 1 },
            275 => oid == 0x3C7 ? 0 : 1,
            315 => oid == 0x640 ? 0 : 1,
            461 => oid == 0x106C ? 1 : 0,
            414 => oid switch { 0xDC9 => 3, 0xDBE => 1, _ => 2 },
            669 => oid == 0x1AE8 ? 0 : 1,
            673 => oid switch { 0x1C82 or 0x1EB4 => 3, 0x1C81 => 2, _ => 1 },
            410 when actor.Hp == 1 => 0,
            410 => oid == 0xDB7 ? 2 : 1,
            476 when oid is >= 0x1268 and <= 0x126D => 5,
            488 when oid == 0x1236 => 5,
            484 when oid == 0x11E4 => 5,
            466 when oid == 0x1231 => 5,
            273 when oid == 0x3BC => 2,
            317 when oid == 0x6B1 => 0,
            314 when oid == 0x815 => 0,
            323 when oid == 0x5EC => 5,
            307 when oid == 0x6EA => 1,
            307 when oid == 0x6E9 => -1,
            498 when oid == 0x11D2 => 5,
            267 when oid == 0x2A4 => 0x2A5,
            818 when oid is 0x2A8E or 0x2A8F => 0,
            814 when oid == 0x2A74 => 0,
            547 when oid != 0x157A => 1,
            552 when oid == 0x1591 => 1,
            _ => null,
        };
    }

    internal static ulong Select(uint territory, IEnumerable<ReplayActorState> actors, ulong current, Vector3 position,
        IReadOnlySet<ulong>? party = null, uint? allowedFateId = null, Func<ReplayActorState, int?>? assignedPriority = null)
        => actors.Where(a => OwnsTerritory(territory) && LegalTarget(a)
                && EffectivePriority(territory, a, position, party, allowedFateId, assignedPriority) >= 0)
            .OrderByDescending(a => EffectivePriority(territory, a, position, party, allowedFateId, assignedPriority))
            .ThenByDescending(a => a.Id == current)
            .ThenBy(a => Vector3.DistanceSquared(a.Position, position))
            .ThenBy(a => a.Id).FirstOrDefault()?.Id ?? 0;

    internal static IEnumerable<ulong> ForbiddenTargets(uint territory, IEnumerable<ReplayActorState> actors, Vector3 position,
        IReadOnlySet<ulong>? party = null, uint? allowedFateId = null)
        => actors.Where(a => OwnsTerritory(territory) && LegalTarget(a)
            && EffectivePriority(territory, a, position, party, allowedFateId, null) == -4).Select(a => a.Id);

    private static bool LegalTarget(ReplayActorState actor)
        => actor.Id is not (0 or 0xE0000000) && actor.Kind == 2 && actor.IsEnemy == true && actor.Targetable && !actor.Dead
            && actor.ObjectIndex is { } index && index < 200 && (index & 1) == 0;

    private static int EffectivePriority(uint territory, ReplayActorState actor, Vector3 position,
        IReadOnlySet<ulong>? party, uint? allowedFateId, Func<ReplayActorState, int?>? assignedPriority)
    {
        // Fate and height precede PendingDead. Only the resulting Pointless value resists later setters.
        var priority = -3;
        if (actor.FateId is > 0 && actor.FateId != allowedFateId || MathF.Abs(actor.Position.Y - position.Y) > 12)
            priority = -2;
        else if (actor.NameId != 541 && (long)actor.Hp + actor.IncomingEffects.Sum(e => (long)e.HpDelta) <= 1)
            priority = -1;
        else if (actor.AggroPlayer || actor.InCombat && party?.Contains(actor.TargetId) == true)
            priority = 0;
        if (priority == -1) return priority;
        if (territory is not (1190 or 960 or 1192 or 814 or 540 or 957 or 818 or 545 or 547 or 552 or 1127 or 1128 or 1129) && actor.Statuses.Any(s => invincibleStatuses.Contains(s.Id))) priority = -2;
        var assigned = assignedPriority?.Invoke(actor) ?? AssignedPriority(territory, actor);
        return PartialOverride(territory) ? assigned ?? priority : assigned ?? 0;
    }
}
