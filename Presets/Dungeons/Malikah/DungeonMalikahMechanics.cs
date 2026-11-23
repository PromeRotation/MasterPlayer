using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DungeonAmbientAoe? malikahRound;
    private DungeonPendingCast? malikahSwiftCast;
    private float? malikahSwiftIncrement;
    private int malikahSwiftRemaining;
    private float malikahSwiftRotation;
    private DateTime malikahSwiftActivation;
    private Vector3 malikahSwiftOrigin;
    private readonly HashSet<(ulong Source, DateTime Start)> malikahSeenSwift = [];
    private readonly Dictionary<ulong, DungeonPendingCast> malikahStreaks = [];

    private void MalikahCast(DungeonPendingCast cast)
    {
        if (territory != MalikahCatalog.TERRITORY) return;
        if (cast.Rule.Action == 15591)
        {
            var delta = cast.Location - _actors[cast.Source].Position;
            var length = MathF.Min(10, new Vector2(delta.X, delta.Z).Length());
            malikahRound = new("malikah/round", "回转跳跃及击退范围", new DungeonCapsuleTemplate(9, length),
                cast.Location, cast.Rotation + MathF.PI, Activation: cast.FinishAt.AddSeconds(.9));
        }
        if (cast.Rule.Action == 15599 && malikahSeenSwift.Add((cast.Source, cast.StartedAt)))
        {
            malikahSwiftCast = cast;
            malikahSwiftOrigin = _actors[cast.Source].Position;
            malikahSwiftRotation = cast.Rotation;
            malikahSwiftActivation = cast.FinishAt;
            StartMalikahSwift();
        }
        if (MalikahCatalog.IsStreak(cast.Rule.Action)) malikahStreaks[cast.Source] = cast;
    }

    private void StartMalikahSwift()
    {
        // Zero is a valid packet heading; readiness is explicit, unlike the source sentinel.
        if (malikahSwiftCast != null && malikahSwiftIncrement != null && malikahSwiftRemaining == 0)
            malikahSwiftRemaining = 6;
    }

    private void MalikahIcon(ulong target, uint icon)
    {
        if (Active?.BossOid != 0x267A || target != PrimaryId || icon is not (156 or 157) || malikahSwiftRemaining > 0) return;
        malikahSwiftIncrement = (icon == 156 ? -1 : 1) * MathF.PI / 3;
        StartMalikahSwift();
    }

    private void MalikahFinish(DungeonPendingCast cast, bool completed)
    {
        if (territory != MalikahCatalog.TERRITORY) return;
        if (MalikahCatalog.IsStreak(cast.Rule.Action)) malikahStreaks.Remove(cast.Source);
        if (completed) return;
        if (cast.Rule.Action == 15591) malikahRound = null;
        if (cast.Rule.Action == 15599) ClearMalikahSwift();
    }

    private void MalikahEffect(uint action, DateTime time)
    {
        if (territory != MalikahCatalog.TERRITORY) return;
        if (action == 15592) malikahRound = null;
        if (action is not (15599 or 15600) || malikahSwiftRemaining == 0) return;
        if (--malikahSwiftRemaining == 0) ClearMalikahSwift();
        else
        {
            malikahSwiftRotation += malikahSwiftIncrement!.Value;
            malikahSwiftActivation = time.AddSeconds(1.1);
        }
    }

    private void ClearMalikahSwift()
    {
        malikahSwiftCast = null; malikahSwiftIncrement = null; malikahSwiftRemaining = 0;
    }

    private void MalikahRemoved(ulong source)
    {
        malikahStreaks.Remove(source);
        if (source == PrimaryId) { malikahRound = null; ClearMalikahSwift(); }
    }

    private void ResetMalikah()
    {
        malikahRound = null; ClearMalikahSwift(); malikahSeenSwift.Clear(); malikahStreaks.Clear();
    }

    private IEnumerable<DungeonAmbientAoe> MalikahAoes()
    {
        if (territory != MalikahCatalog.TERRITORY || Arena == null) yield break;
        if (malikahRound != null) yield return malikahRound;
        if (malikahSwiftRemaining > 0)
            for (var i = Math.Min(2, malikahSwiftRemaining) - 1; i >= 0; --i)
                yield return new($"malikah/swift/{i}", i == 0 ? "旋转喷吐当前段" : "旋转喷吐下一段",
                    new FanTemplate(50, MathF.PI / 3), malikahSwiftOrigin, malikahSwiftRotation + malikahSwiftIncrement!.Value * i,
                    Activation: i == 0 ? malikahSwiftActivation : (malikahSwiftActivation > _now ? malikahSwiftActivation : _now).AddSeconds(1.1));
        if (malikahStreaks.Values.OrderBy(c => c.FinishAt).ThenBy(c => c.Source).FirstOrDefault() is { } streak)
            yield return new("malikah/streak", streak.Rule.Name, streak.Rule.Shape!, streak.Location, streak.Rotation, Activation: streak.FinishAt);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 15590))
            if (_actors.TryGetValue(cast.TargetId, out var target) && !target.Dead)
            {
                yield return new($"malikah/stack/{cast.Source}", "四人分摊", new CircleTemplate(6), target.Position,
                    Navigate: false, Safe: true, Activation: cast.FinishAt);
                if (PlayerId != target.Id) yield return new($"malikah/stack/enter/{cast.Source}", "进入四人分摊",
                    new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(target.Position, 6)]), Arena.Position,
                    Display: false, Activation: cast.FinishAt);
            }
        var geysers = _actors.Values.Where(a => Active?.BossOid == 0x267A && a.Oid == 0x1EAAC9 && !a.Dead && a.EventState != 7).ToArray();
        foreach (var geyser in geysers)
            yield return new($"malikah/geyser/{geyser.Id}", "持续喷泉", new CircleTemplate(4), geyser.Position);
        if (!_actors.TryGetValue(PlayerId, out var player) || player.Dead) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 15596))
        {
            if (KnockbackImmune(player, cast.FinishAt)) continue;
            var dangers = geysers.Select(a => (IGeometryShape)new CircleGeometry(a.Position, 4)).ToArray();
            var template = new CastrumKnockbackTemplate(Arena, cast.Location, 20, dangers);
            yield return new($"malikah/pressure/{cast.Source}", "击退后避开喷泉", template, Arena.Position, Display: false, Activation: cast.FinishAt);
            foreach (var preview in DungeonMovementPreview($"malikah/pressure/preview/{cast.Source}", "水压击退落点",
                player.Position, template.Landing(player.Position))) yield return preview;
        }
    }
}
