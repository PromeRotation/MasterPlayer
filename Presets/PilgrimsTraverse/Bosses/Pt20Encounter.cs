using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class Pt20Encounter() : PtBossEncounter(20, 0x485C)
{
    public override uint[] Actions { get; } = [43454, 43455, 43456, 43457, 43458, 43459, 43460, 43461, 43463, 43464, 43462];
    private int burstIndex;
    private readonly HashSet<(ulong, ushort)> burstTelegraphs = [];
    private int embraceHits;
    private ushort rootGrowth;
    private ulong baitTarget;
    private DateTime baitHit;
    private Vector3? root;
    private Cast? touchdown;

    protected override void OnCast(Cast cast)
    {
        switch (cast.Action)
        {
            case 43455:
                touchdown = cast;
                break;
            case >= 43456 and <= 43459:
                var predicted = areas.Values.FirstOrDefault(a => a.Key.StartsWith("burst/", StringComparison.Ordinal) && a.Action == cast.Action);
                if (predicted != null) AddHit(predicted.Key, "Burst", new CircleTemplate(11), cast.Position, 0, cast.Hit, cast.Source, cast.Action);
                else Hit(cast, new CircleTemplate(11), "Burst");
                break;
            case 43461:
                baitTarget = 0;
                root = cast.Position;
                embraceHits = 0;
                rootGrowth = 0;
                Hit(cast, new CircleTemplate(6), "Bare-root Planting");
                AddCross("embrace/", "Wood's Embrace 最终十字", cast.Position, 30, cast.Hit.AddSeconds(4.4), DateTime.MaxValue);
                break;
            case 43464:
                Hit(cast, new CircleTemplate(6), "Dense Planting");
                break;
        }
    }

    private void AddCross(string prefix, string name, Vector3 position, float range, DateTime from, DateTime until, bool obstacle = false)
    {
        for (var i = 0; i < 4; ++i)
        {
            var key = prefix + i;
            areas[key] = new(key, name, new RectangleTemplate(6, range * 2), position,
                Rad(i * 45 - .003f), from, until, Obstacle: obstacle);
        }
    }

    protected override void OnStatus(ulong actor, ReplayStatus status, bool gained, DateTime now)
    {
        if (!gained) return;
        if (status.Id == 2056)
        {
            Vector3? position = status.Param switch
            {
                0x395 => new(-293.65f, 0, -306.365f),
                0x396 => new(-306.365f, 0, -306.365f),
                0x397 => new(-293.365f, 0, -293.365f),
                0x398 => new(-306.365f, 0, -293.365f),
                _ => null
            };
            if (position is not { } p || !burstTelegraphs.Add((actor, status.Param))) return;
            var previous = areas.Values.Where(a => a.Key.StartsWith("burst/", StringComparison.Ordinal)).MaxBy(a => a.From);
            var hit = previous == null ? now.AddSeconds(14.7) : previous.From.AddSeconds(.2 + 1.3);
            AddHit($"burst/{burstIndex++}", "Touchdown 连续爆炸", new CircleTemplate(11), p, 0, hit,
                action: 43455u + (uint)burstTelegraphs.Count);
        }
        if (status.Id == 1909 && (root ?? actors.GetValueOrDefault(actor)?.Position) is { } origin)
        {
            rootGrowth = Math.Max(rootGrowth, status.Param);
            AddCross("roots/", "生长中的树根", origin, 3 + 3 * rootGrowth, now, DateTime.MaxValue, true);
        }
    }

    protected override void OnIcon(ulong target, uint icon, DateTime now)
    {
        if (icon == 23) { baitTarget = target; baitHit = now.AddSeconds(5.2); }
    }

    protected override void OnEffect(LogSystemActionEffectEvent effect, DateTime now)
    {
        if (effect.ActionId == 43455) touchdown = null;
        if (effect.ActionId is >= 43456 and <= 43459)
        {
            // Self-target Burst packets carry an irrelevant (0,0,0) effect position.
            var resolved = areas.Values.FirstOrDefault(a => a.Key.StartsWith("burst/", StringComparison.Ordinal)
                && a.Action == effect.ActionId && (a.Owner == 0 || a.Owner == effect.SourceId));
            if (resolved != null) areas.Remove(resolved.Key);
            if (!areas.Keys.Any(k => k.StartsWith("burst/", StringComparison.Ordinal))) burstTelegraphs.Clear();
        }
        if (effect.ActionId == 43462 && ++embraceHits >= 30)
        { RemoveGroup("embrace/"); RemoveGroup("roots/"); root = null; embraceHits = 0; }
    }

    protected override void AddPersonalAreas(List<Area> result, DateTime now, ulong player)
    {
        PreviewNextWave(result, a => a.Key.StartsWith("burst/", StringComparison.Ordinal) || a.Action is >= 43456 and <= 43459);
        if (touchdown is { } cast && now <= cast.Hit.AddSeconds(.5) && !KnockbackImmune(player, cast.Hit))
        {
            var bursts = result.Where(a => a.Name.Contains("Burst", StringComparison.OrdinalIgnoreCase) || a.Key.StartsWith("burst/", StringComparison.Ordinal))
                .OrderBy(a => a.From).Take(3).Select(a => a.Geometry).ToArray();
            var key = "touchdown/landing";
            result.Add(new(key, "Touchdown 击退落点", new PtKnockbackTemplate(Boundary, cast.Position, 10, LandingDangers: bursts),
                Center, 0, cast.Hit.AddSeconds(-.2), cast.Hit.AddSeconds(.15), NavigationOnly: true));
            result.Add(new("touchdown/goal", "Touchdown 预站位", new CircleTemplate(4), cast.Position, 0, now, cast.Hit, Safe: true));
        }
        if (baitTarget != 0 && now <= baitHit.AddSeconds(2) && actors.TryGetValue(baitTarget, out var target))
        {
            if (baitTarget == player)
                result.Add(new("root-bait/goal", "树根沿边诱导", new DonutTemplate(13.5f, 14.709f), Center, 0, now, baitHit, Safe: true));
            else
            {
                result.Add(new("root-bait/circle", "树根点名", new CircleTemplate(6), target.Position, 0, baitHit.AddSeconds(-.2), baitHit.AddSeconds(.15)));
                for (var i = 0; i < 4; ++i)
                    result.Add(new($"root-bait/{i}", "树根点名十字", new RectangleTemplate(6, 60), target.Position,
                        Rad(i * 45 - .003f), baitHit.AddSeconds(-.2), baitHit.AddSeconds(.15)));
            }
        }
    }

    public override void Cancel(ulong source) { base.Cancel(source); if (touchdown?.Source == source) touchdown = null; }
    protected override void ResetMechanics() { burstIndex = embraceHits = 0; rootGrowth = 0; burstTelegraphs.Clear(); baitTarget = 0; root = null; touchdown = null; }
}
