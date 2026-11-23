using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DateTime> gulgBirths = [];
    private readonly HashSet<ulong> gulgSeen = [];
    private readonly Dictionary<ulong, DungeonAmbientAoe> gulgDonuts = [];
    private readonly List<(ulong Id, Vector3 Position, DateTime Activation)> gulgTowers = [];
    private readonly List<DungeonAmbientAoe> gulgExegesis = [];
    private readonly Dictionary<ulong, DungeonPendingCast> gulgDamage = [];
    private readonly HashSet<ulong> gulgOrbs = [];
    private readonly List<DungeonActor> gulgRings = [];
    private readonly HashSet<ulong> gulgResolvedRings = [];
    private readonly Dictionary<ulong, (uint State, DateTime Time)> gulgAnimations = [];
    private DateTime? gulgRingsmith;
    private int gulgGoldHits;
    private DungeonAmbientAoe? gulgPenance;
    private bool gulgSmall;
    private bool IsGulgPrejudice => territory == MtGulgCatalog.TERRITORY && Active?.BossOid == 0x28F2;
    internal bool WantsGulgSprint => Active?.BossOid == 0x27CE && gulgSmall;
    private bool GulgApathyReady => _actors.Values.Any(a => a.Oid == 0x28F0 && !a.Dead && (a.Targetable || a.InCombat));
    private bool GulgPrejudiceMember(DungeonActor a) => !a.Dead && MtGulgCatalog.IsPrejudice(a.Oid);
    private bool GulgCanStart(DungeonActor a) => a.InCombat && (!MtGulgCatalog.IsPrejudice(a.Oid) || !GulgApathyReady);

    private void GulgSelectPrimary()
    {
        if (!IsGulgPrejudice) return;
        if (GulgApathyReady || !_actors.Values.Any(GulgPrejudiceMember)) { ResetFight(); return; }
        if (!_actors.TryGetValue(PrimaryId, out var primary) || !GulgPrejudiceMember(primary))
            PrimaryId = _actors.Values.First(GulgPrejudiceMember).Id;
    }

    private void GulgCast(DungeonPendingCast cast)
    {
        if (territory != MtGulgCatalog.TERRITORY) return;
        var delay = cast.Rule.Action switch { 15627 or 15625 => .5, 15636 or 15634 => .8, _ => 0 };
        if (delay > 0 || cast.Rule.Action == 15611)
            gulgDamage[cast.Source] = cast with
            {
                FinishAt = cast.FinishAt.AddSeconds(delay),
                TargetId = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId,
            };
        if (cast.Rule.Action == 15644)
            gulgPenance = new("gulg/penance", "缩场持续危险", new DonutTemplate(14.5f, 30), Arena!.Position,
                Activation: cast.FinishAt.AddSeconds(.7));
        if (cast.Rule.Action is not (16989 or 16987 or 15622 or 16988)) return;
        gulgExegesis.Clear();
        Vector2[] points = cast.Rule.Action switch
        {
            16989 => [new(-240, -50), new(-250, -40), new(-230, -40), new(-250, -60), new(-230, -60)],
            16987 => [new(-250, -50), new(-230, -50)],
            15622 => [new(-240, -60), new(-240, -40)],
            _ => [new(-240, -50)],
        };
        foreach (var point in points)
            gulgExegesis.Add(new($"gulg/exegesis/{gulgExegesis.Count}", "九宫格判定",
                cast.Rule.Action == 16988 ? new DungeonCrossTemplate(15, 5) : new RectangleTemplate(10, 10),
                new(point.X, cast.Location.Y, point.Y), Activation: cast.FinishAt.AddSeconds(.4)));
    }

    private void GulgFinish(DungeonPendingCast cast, bool completed)
    {
        if (territory != MtGulgCatalog.TERRITORY) return;
        if (cast.Rule.Action == 15630 && completed) gulgDonuts.Clear();
        if (completed) return;
        gulgDamage.Remove(cast.Source);
        if (cast.Rule.Action is 16989 or 16987 or 15622 or 16988) gulgExegesis.Clear();
        if (cast.Rule.Action == 15644) gulgPenance = null;
        if (cast.Rule.Action == 15630) gulgDonuts.Remove(cast.Source);
    }

    private void GulgCreated(ulong source, DateTime time)
    {
        if (territory != MtGulgCatalog.TERRITORY) return;
        if (!gulgBirths.TryGetValue(source, out var previous) || time < previous) gulgBirths[source] = time;
        if (gulgDonuts.TryGetValue(source, out var donut))
            gulgDonuts[source] = donut with { Activation = gulgBirths[source].AddSeconds(10.6) };
        var index = gulgTowers.FindIndex(t => t.Id == source);
        if (index >= 0) gulgTowers[index] = gulgTowers[index] with { Activation = gulgBirths[source].AddSeconds(7.6) };
    }

    private void SynchronizeGulg(Vector3 player)
    {
        if (territory != MtGulgCatalog.TERRITORY) return;
        if (IsGulgPrejudice && Arena?.Position != player) Arena = new CircleGeometry(player, 30);
        if (Active?.BossOid is not (0x27CC or 0x27CE)) return;
        foreach (var actor in _actors.Values.Where(a => !a.Dead && a.Oid is 0x27CD or 0x1EAACF or 0x27D0 or 0x1EAB62))
        {
            if (!gulgSeen.Add(actor.Id)) continue;
            var birth = gulgBirths.GetValueOrDefault(actor.Id, _now);
            if (Active.BossOid == 0x27CC)
            {
                if (actor.Oid == 0x27CD)
                    gulgDonuts[actor.Id] = new($"gulg/donut/{actor.Id}", "球体延迟环形",
                        new DonutTemplate(5, 15), QuantizedDungeonPosition(actor.Position), Activation: birth.AddSeconds(10.6));
                if (actor.Oid == 0x1EAACF) gulgTowers.Add((actor.Id, QuantizedDungeonPosition(actor.Position), birth.AddSeconds(7.6)));
            }
            else
            {
                if (actor.Oid == 0x27D0) gulgOrbs.Add(actor.Id);
                if (actor.Oid == 0x1EAB62) gulgRings.Add(actor);
            }
        }
        foreach (var source in gulgOrbs.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray()) gulgOrbs.Remove(source);
    }

    private void GulgEffect(uint action, DateTime time)
    {
        if (territory != MtGulgCatalog.TERRITORY) return;
        if (action == 15623) gulgExegesis.Clear();
        if (action is 15633 or 15632 && gulgTowers.Count > 0) gulgTowers.RemoveAt(0);
        var visual = action switch { 15611 => 15611u, 15628 => 15627u, 15626 => 15625u, 15637 => 15636u, 15635 => 15634u, _ => 0u };
        foreach (var id in gulgDamage.Where(d => d.Value.Rule.Action == visual).Select(d => d.Key).ToArray()) gulgDamage.Remove(id);
        if (action == 15652) gulgRingsmith = time;
        if (action != 15655) return;
        var first = gulgRings.FirstOrDefault(a => !gulgResolvedRings.Contains(a.Id));
        if (first != null) gulgResolvedRings.Add(first.Id);
        if (++gulgGoldHits < 6) return;
        gulgGoldHits = 0; gulgRings.Clear(); gulgResolvedRings.Clear(); gulgRingsmith = null;
    }

    private void GulgAnimation(ulong source, uint state, DateTime time, bool packet)
    {
        if (Active?.BossOid != 0x27CE || !_actors.TryGetValue(source, out var actor)) return;
        if (gulgAnimations.TryGetValue(source, out var old) && (old.State == state && (!packet || time <= old.Time))) return;
        gulgAnimations[source] = (state, time);
        if (state == 0x00040008)
        {
            if (gulgSmall) { Arena = Active.Arena(actor.Position); gulgSmall = false; }
            var orb = gulgOrbs.FirstOrDefault(id => _actors.TryGetValue(id, out var a)
                && Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(actor.Position.X, actor.Position.Z)) <= 16);
            if (orb != 0) gulgOrbs.Remove(orb);
        }
        if (state == 0x00010002)
        {
            gulgPenance = null;
            if (!gulgSmall) { Arena = MtGulgArenas.ObscenitySmall(Arena!.Position.Y); gulgSmall = true; }
        }
    }

    private IEnumerable<DungeonDamagePrediction> GulgDamage()
    {
        foreach (var cast in gulgDamage.Values)
            foreach (var actor in _actors.Values.Where(a => !a.Dead && a.PartySlot >= 0
                && (cast.Rule.Action is 15627 or 15636 || a.Id == cast.TargetId || cast.TargetId == cast.Source && a.Id == _actors.GetValueOrDefault(cast.Source)?.TargetId)))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, cast.FinishAt,
                    cast.Rule.Action is 15627 or 15636 ? "Raidwide" : "Tankbuster");
    }

    private void GulgRemoved(ulong source)
    {
        gulgDonuts.Remove(source); gulgTowers.RemoveAll(t => t.Id == source); gulgOrbs.Remove(source); gulgDamage.Remove(source);
        gulgResolvedRings.Add(source);
    }

    private void ResetGulg()
    {
        gulgBirths.Clear(); gulgSeen.Clear(); gulgDonuts.Clear(); gulgTowers.Clear(); gulgExegesis.Clear(); gulgDamage.Clear();
        gulgOrbs.Clear(); gulgRings.Clear(); gulgResolvedRings.Clear(); gulgAnimations.Clear();
        gulgPenance = null; gulgRingsmith = null; gulgGoldHits = 0; gulgSmall = false;
    }
}
