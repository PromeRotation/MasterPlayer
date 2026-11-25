using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<(ulong Id, DungeonPendingCast Start, int Order, DateTime Activation)> bekkoRings = [];
    private readonly Dictionary<ulong, DungeonPendingCast> bekkoStarted = [];
    private readonly List<ulong> bekkoYukikaze = [];
    private ulong bekkoRingId;
    private bool IsBekko => territory == JobQuestCatalog.BATTLE_ON_BEKKO;

    private void BekkoCast(DungeonPendingCast cast)
    {
        if (!IsBekko) return;
        if (cast.Rule.Action == 8446 && !bekkoYukikaze.Contains(cast.Source))
            bekkoYukikaze.Add(cast.Source);
        if (cast.Rule.Action != 8439) return;
        if (bekkoStarted.TryGetValue(cast.Source, out var previous)
            && Math.Abs((previous.StartedAt - cast.StartedAt).TotalSeconds) < .25
            && Math.Abs((previous.FinishAt - cast.FinishAt).TotalSeconds) < .75)
        {
            var index = bekkoRings.FindIndex(r => ReferenceEquals(r.Start, previous));
            if (index >= 0)
            {
                var ring = bekkoRings[index];
                bekkoRings[index] = (ring.Id, cast, ring.Order, ring.Order == 0 ? cast.FinishAt : ring.Activation);
            }
        }
        else
            bekkoRings.Add((++bekkoRingId, cast, 0, cast.FinishAt));
        bekkoStarted[cast.Source] = cast;
    }

    private void BekkoFinish(DungeonPendingCast cast)
    {
        if (!IsBekko) return;
        bekkoStarted.Remove(cast.Source);
        if (cast.Rule.Action == 8446) bekkoYukikaze.Remove(cast.Source);
        if (cast.Rule.Action is < 8439 or > 8442) return;
        var order = (int)cast.Rule.Action - 8439;
        var index = bekkoRings.FindIndex(r => r.Order == order
            && MathF.Abs(r.Start.Location.X - cast.Location.X) <= 1
            && MathF.Abs(r.Start.Location.Z - cast.Location.Z) <= 1
            && MathF.Abs(MathF.Atan2(MathF.Sin(r.Start.Rotation - cast.Rotation), MathF.Cos(r.Start.Rotation - cast.Rotation))) <= .05f);
        if (index < 0) return;
        var ring = bekkoRings[index];
        if (order == 3) bekkoRings.RemoveAt(index);
        else bekkoRings[index] = (ring.Id, ring.Start, order + 1, _now.AddSeconds(2.5));
    }

    private IEnumerable<DungeonAmbientAoe> BekkoAoes()
    {
        if (!IsBekko || Active == null) yield break;
        foreach (var ring in bekkoRings)
        {
            var inner = 2 + ring.Order * 5;
            yield return new($"bekko/ring/{ring.Id}", "Ugetsuzan", new PtDonutSectorTemplate(inner, inner + 5, MathF.PI),
                ring.Start.Location, ring.Start.Rotation, Activation: ring.Activation);
        }
        foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1E8EA9 && a.EventState != 7))
            yield return new($"bekko/voidzone/{zone.Id}", "GTFO from voidzone!", new CircleTemplate(4),
                zone.Position, Obstacle: true);
    }
}
