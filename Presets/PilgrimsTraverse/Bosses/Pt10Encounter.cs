using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class Pt10Encounter() : PtBossEncounter(10, 0x460D)
{
    public override uint[] Actions { get; } = [44051, 44054, 44053, 44855, 44854, 44059, 44058, 44056, 44055, 44057];
    private bool spread;
    private int leafIndex;
    protected override int TargetPriority(ReplayActorState actor)
        => actor.Oid == 0x460E ? 1 : base.TargetPriority(actor);
    public override IReadOnlyList<ulong> ForbiddenDotTargets
        => Active ? actors.Values.Where(a => a.Oid == 0x460E && !a.Dead && a.Targetable).Select(a => a.Id).ToArray() : [];
    private bool SpreadActive => Active && spread && actors.Values.Any(a => a.Oid == 0x460E && a.Targetable && !a.Dead);
    internal IReadOnlyList<ulong> PredictedDamageTargets => SpreadActive
        ? actors.Values.Where(a => a.Kind == 1 && !a.Dead).Select(a => a.Id).ToArray() : [];
    internal string? PredictedDamageText => PredictedDamageTargets.Count > 0
        ? "Raidwide×1（小压花分散，判定时间未知）" : null;

    public override IEnumerable<string> Hints(DateTime now, ulong player)
    {
        // GenericStackSpread publishes one Raidwide prediction for this group,
        // with default activation. The old MP bridge consumed it as diagnostics.
        if (PredictedDamageText is { } prediction) yield return "预计伤害：" + prediction;
    }

    protected override void OnCast(Cast cast)
    {
        switch (cast.Action)
        {
            case 44855 or 44854:
                Hit(cast, new CircleTemplate(14), "Hedge Mazing");
                break;
            case 44057:
                // The live Action sheet is R15; BMR's queue used R14.
                var predicted = areas.Values.Where(a => a.Key.StartsWith("leaf/", StringComparison.Ordinal)
                    && a.Owner == 0 && Vector3.DistanceSquared(a.Position, cast.Position) < 1).MinBy(a => a.From);
                AddHit(predicted?.Key ?? $"leaf/{leafIndex++}", "压花", new CircleTemplate(15),
                    cast.Position, 0, cast.Hit, cast.Source, cast.Action);
                break;
            case 44055:
                spread = true;
                break;
        }
    }

    protected override void OnEffect(LogSystemActionEffectEvent effect, DateTime now)
    {
        if (effect.ActionId == 44058 && effect.EffectPosition is { } position)
        {
            var count = areas.Values.Count(a => a.Key.StartsWith("leaf/", StringComparison.Ordinal));
            AddHit($"leaf/{leafIndex++}", "压花连跳", new CircleTemplate(15), position, 0, now.AddSeconds(8.9 - .3 * count));
        }
        if (effect.ActionId == 44057)
        {
            // Self-target hits have no ground position; reused helpers may already be casting a later jump.
            var resolved = areas.Values.Where(a => a.Key.StartsWith("leaf/", StringComparison.Ordinal)
                && a.Owner == effect.SourceId && a.Action == effect.ActionId).MinBy(a => a.From);
            if (resolved != null) areas.Remove(resolved.Key);
        }
    }

    protected override void AddPersonalAreas(List<Area> result, DateTime now, ulong player)
    {
        PreviewNextWave(result, a => a.Key.StartsWith("leaf/", StringComparison.Ordinal) || a.Action == 44057, 3);
        if (!spread) return;
        if (!actors.Values.Any(a => a.Oid == 0x460E && a.Targetable && !a.Dead)) { spread = false; return; }
        foreach (var actor in actors.Values.Where(a => a.Kind == 1 && !a.Dead))
            result.Add(new($"spread/{actor.Id}", "小压花分散", new CircleTemplate(5), actor.Position, 0, now, DateTime.MaxValue,
                PreviewOnly: actor.Id == player, OutlineOnly: true));
    }

    protected override void ResetMechanics() { spread = false; leafIndex = 0; }
}
