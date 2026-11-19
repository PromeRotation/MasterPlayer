using System.Numerics;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class Pt10Encounter() : PtBossEncounter(10, 0x460D)
{
    public override uint[] Actions { get; } = [44051, 44054, 44053, 44855, 44854, 44059, 44058, 44056, 44055, 44057];
    private bool spread;
    private int leafIndex;

    protected override void OnCast(Cast cast)
    {
        switch (cast.Action)
        {
            case 44855 or 44854:
                Hit(cast, new CircleTemplate(14), "Hedge Mazing");
                break;
            case 44057:
                // The live Action sheet is R15; BMR's queue used R14.
                var predicted = areas.Values.Where(a => a.Key.StartsWith("leaf/", StringComparison.Ordinal)).MinBy(a => Vector3.DistanceSquared(a.Position, cast.Position));
                if (predicted != null)
                    AddHit(predicted.Key, "压花", new CircleTemplate(15), cast.Position, 0, cast.Hit, cast.Source, cast.Action);
                else Hit(cast, new CircleTemplate(15), "压花");
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
        if (effect.ActionId == 44057) ResolveFirst("leaf/", effect.EffectPosition);
    }

    protected override void AddPersonalAreas(List<Area> result, DateTime now, ulong player)
    {
        PreviewNextWave(result, a => a.Key.StartsWith("leaf/", StringComparison.Ordinal) || a.Action == 44057);
        if (!spread) return;
        if (!actors.Values.Any(a => a.Oid == 0x460E && a.Targetable && !a.Dead)) { spread = false; return; }
        foreach (var actor in actors.Values.Where(a => a.Kind == 1 && !a.Dead && a.Id != player))
            result.Add(new($"spread/{actor.Id}", "小压花分散", new CircleTemplate(5), actor.Position, 0, now, DateTime.MaxValue));
    }

    protected override void ResetMechanics() { spread = false; leafIndex = 0; }
}
