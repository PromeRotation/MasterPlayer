using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed record BlundervilleRaceStatus(uint Id, ushort Param, DateTime ExpiresAt);

internal sealed class BlundervilleRaceState
{
    public const uint DirectorInstance = 0x8004002B;
    public static readonly Vector3 PickupCenter = new(-200, 5.421129f, 252);
    private readonly Dictionary<uint, BlundervilleRaceStatus> _statuses = [];
    private DateTime _lastTime;
    private bool _ended;

    public BlundervilleCourse Course { get; private set; }
    public bool IsRacing { get; private set; }
    public bool IsPrepared { get; private set; }
    public bool Qualified { get; private set; }
    public int Score { get; private set; }
    public DateTime StartedAt { get; private set; }
    public long RoundVersion { get; private set; }
    public ushort CarryCrystalParam => ActiveStatus(3704, _lastTime)?.Param ?? 0;
    public Vector3? DeliveryTarget => CarryCrystalParam switch
    {
        35 => new(-240.2991f, 6, 275.4989f),
        36 => new(-159.4339f, 6, 275.2527f),
        37 => new(-200.1303f, 6, 205.1951f),
        _ => null
    };
    public bool NeedsPickup => IsRacing && !Qualified && Course == BlundervilleCourse.Delivery && Score < 3 && CarryCrystalParam == 0;
    public bool HasMisdirection => ActiveStatus(3694, _lastTime) != null;
    public bool IsSlippery => ActiveStatus(3693, _lastTime) != null;
    public bool IsSlidingSlope => ActiveStatus(3703, _lastTime) != null;
    public bool IsSpinning => ActiveStatus(3702, _lastTime) != null;
    public bool IsForcedMoving => ActiveStatus(3737, _lastTime) != null;
    public uint? ForcedDirectionStatus => PendingForcedMove(_lastTime)?.Id;
    public DateTime? ForcedMoveExpiresAt => PendingForcedMove(_lastTime)?.ExpiresAt;
    public float? ForcedDirectionOffset => PendingForcedMove(_lastTime)?.Id switch
    {
        3698 => 0,
        3699 => MathF.PI,
        3700 => MathF.PI / 2,
        3701 => -MathF.PI / 2,
        _ => null
    };
    public DateTime? AccelerationBombExpiresAt => ActiveStatus(3802, _lastTime)?.ExpiresAt;

    public void Director(uint instance, uint command, uint param1, uint param2, uint param3, uint param4, DateTime now)
    {
        if (instance != DirectorInstance) return;
        Advance(now);
        switch (command)
        {
            case 5:
                Prepare();
                break;
            case 0x80000001:
                var course = BlundervilleRoutes.FromDirectorStage(param1);
                if (course == BlundervilleCourse.None) break;
                if (IsRacing && Course == course) break;
                Course = course;
                IsRacing = true;
                IsPrepared = false;
                Qualified = false;
                _ended = false;
                Score = 0;
                StartedAt = now;
                RoundVersion++;
                break;
            case 3 when Course == BlundervilleCourse.Delivery && param1 <= 3:
                Score = Math.Max(Score, (int)param1);
                break;
            case 7:
                Qualified = true;
                _ended = true;
                IsRacing = false;
                IsPrepared = false;
                break;
            case 6:
            case 8:
                IsRacing = false;
                IsPrepared = false;
                _ended = true;
                break;
        }
    }

    public void SynchronizeStatuses(IEnumerable<BlundervilleRaceStatus> statuses, DateTime now)
    {
        Advance(now);
        _statuses.Clear();
        foreach (var status in statuses)
        {
            if (status.Id is not (149 or 1548 or 3693 or 3694 or 3698 or 3699 or 3700 or 3701 or 3702 or 3703 or 3704 or 3708 or 3737 or 3782 or 3802)) continue;
            if (status.ExpiresAt < now) continue;
            if (!_statuses.TryGetValue(status.Id, out var old) || status.ExpiresAt > old.ExpiresAt) _statuses[status.Id] = status;
        }
    }

    public bool HasStatus(uint statusId, DateTime now) => ActiveStatus(statusId, now) != null;

    public bool RecoverInProgress(BlundervilleCourse course, DateTime now)
    {
        Advance(now);
        if (_ended || IsPrepared || Qualified || IsRacing || Course != BlundervilleCourse.None || course == BlundervilleCourse.None
            || HasStatus(149, now) || HasStatus(3782, now) || HasStatus(3708, now)) return false;
        Course = course;
        IsRacing = true;
        StartedAt = now;
        RoundVersion++;
        return true;
    }

    public bool CanMove(DateTime now)
        => IsRacing && !Qualified && !HasStatus(149, now) && !HasStatus(3708, now) && !HasStatus(3782, now)
            && !HasStatus(3737, now) && !MustStopForAccelerationBomb(now);

    public bool CanJump(DateTime now)
        => CanMove(now) && !HasStatus(3703, now);

    // 3703 is only applied on the FirstB final ramp (z -338..-367); five ARRs
    // climb it at 1.8–2.1m/s (p90 2.0). The old 3.6m/s timed pusher rows at
    // half their real crossing time (15:10:49 ARR push at z=-355).
    internal const float SlidingSlopeSpeed = 2f;

    public float NavigationBaseSpeed(DateTime now)
        => !IsRacing || Qualified ? 6f : HasStatus(3702, now) ? 3.6f : HasStatus(3693, now) ? 3f : HasStatus(3703, now) ? SlidingSlopeSpeed : HasStatus(1548, now) ? 7.8f : 6f;

    // 19 recorded 3802 expiries: the last movement 0.56–1.33s before expiry
    // never exploded, 0.29–0.61s exploded (0.56–0.61s overlap). The old 0.6s
    // stop sat on that border (01:01 and 02:33 ARRs exploded); stop at 1.2s.
    public const float AccelerationBombStopMargin = 1.2f;
    // Within this window a stop is taken early wherever it is safe to stand.
    public const float AccelerationBombSafeStopWindow = 2.5f;

    public bool MustStopForAccelerationBomb(DateTime now, float marginSeconds = AccelerationBombStopMargin)
    {
        if (!_statuses.TryGetValue(3802, out var bomb)) return false;
        if (bomb.ExpiresAt == DateTime.MaxValue) return true;
        var remaining = (bomb.ExpiresAt - now).TotalSeconds;
        var margin = float.IsFinite(marginSeconds) ? Math.Clamp(marginSeconds, 0, 5) : AccelerationBombStopMargin;
        return remaining >= -0.3 && remaining <= margin;
    }

    public bool CanMoveToward(Vector3 player, Vector3 target, float? arrowDirection, DateTime now, float toleranceRadians = 0.12f)
    {
        if (!CanMove(now) || !Finite(player) || !Finite(target)) return false;
        if (!HasStatus(3694, now)) return true;
        if (arrowDirection is not { } arrow || !float.IsFinite(arrow)) return false;
        var delta = target - player;
        if (delta.X * delta.X + delta.Z * delta.Z <= 0.0001f) return false;
        var desired = MathF.Atan2(delta.X, delta.Z);
        var difference = MathF.Abs(MathF.IEEERemainder(desired - arrow, MathF.Tau));
        var tolerance = float.IsFinite(toleranceRadians) ? Math.Clamp(toleranceRadians, 0, MathF.PI) : 0.12f;
        return difference <= tolerance;
    }

    public void Reset()
    {
        Course = BlundervilleCourse.None;
        IsRacing = false;
        IsPrepared = false;
        Qualified = false;
        Score = 0;
        StartedAt = default;
        _ended = false;
        _lastTime = default;
        _statuses.Clear();
        RoundVersion++;
    }

    private void Prepare()
    {
        Course = BlundervilleCourse.None;
        IsRacing = false;
        IsPrepared = true;
        Qualified = false;
        Score = 0;
        StartedAt = default;
        _ended = false;
        _statuses.Clear();
        RoundVersion++;
    }

    private void Advance(DateTime now)
    {
        if (_lastTime != default && now < _lastTime) Reset();
        _lastTime = now;
    }

    private BlundervilleRaceStatus? ActiveStatus(uint id, DateTime now)
        => _statuses.TryGetValue(id, out var status) && status.ExpiresAt >= now ? status : null;

    private BlundervilleRaceStatus? PendingForcedMove(DateTime now)
        => _statuses.Values.Where(s => (s.Id is 3698 or 3699 or 3700 or 3701) && s.ExpiresAt >= now)
            .MinBy(s => s.ExpiresAt);

    private static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
}
