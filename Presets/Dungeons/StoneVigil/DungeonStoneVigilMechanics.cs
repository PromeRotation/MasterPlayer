using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<DungeonTyphoonLine> typhoons = [];
    private readonly HashSet<ulong> finishedTyphoons = [];
    private DungeonPendingCast? touchdown;

    private void SynchronizeStoneVigil()
    {
        typhoons.RemoveAll(l => !_actors.TryGetValue(l.Source, out var actor) || actor.Dead);
        if (Active?.BossOid != 0x38C7) return;
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x38C8 && !a.Dead && !finishedTyphoons.Contains(a.Id)))
        {
            if (typhoons.Any(l => MathF.Abs(l.Next.Z - actor.Position.Z) < 1)) continue;
            var line = new DungeonTyphoonLine(actor.Id, actor.Position);
            line.Initialize(_now);
            typhoons.Add(line);
        }
    }

    private void StoneVigilEffect(ulong source, uint action, DateTime now)
    {
        if (Active?.BossOid == 0x5AF && action == 1027) touchdown = null;
        if (Active?.BossOid != 0x38C7 || action != 28731 || !_actors.TryGetValue(source, out var actor)
            || actor.Position.X >= 56) return;
        var line = typhoons.FirstOrDefault(l => MathF.Abs(l.Next.Z - actor.Position.Z) < 1);
        if (line == null) return;
        line.Advance(actor.Position, now);
        if (line.Left == 0)
        {
            finishedTyphoons.Add(line.Source);
            typhoons.Remove(line);
        }
    }

    public void Timeline(ulong source, uint timeline, DateTime now)
    {
        EurekaTimeline(source, timeline);
        UltimaTimeline(source, timeline, now);
        AsuraTimeline(source, timeline, now);
        ZeleniaTimeline(source, timeline, now);
        QueenTimeline(source, timeline);
        HardTrialTimeline(source, timeline);
        YojimboTimeline(source, timeline, now);
        SkydeepTimeline(source, timeline);
        StrayTimeline(source, timeline, now);
        UnderTimeline(source, timeline, now);
        MesoTimeline(source, timeline);
        KeeperTimeline(source, timeline, now);
        BardamTimeline(source, timeline, now);
        ActionTimeline(source, timeline);
        if (Active?.BossOid != 0x5AF || timeline != 0x8E || source != PrimaryId) return;
        _now = now;
        var location = Quantize(new(0, -248), _actors[source].Position.Y);
        touchdown = new(source, new(1027, 0x5AF, "Touchdown", new CircleTemplate(5)),
            location, 0, now, now.AddSeconds(7.8));
    }
}
