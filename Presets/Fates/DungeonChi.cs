using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsChi => territory == NativeFateCatalog.CHI;
    private readonly Dictionary<ulong, uint> chiCreated = [];
    private readonly HashSet<ulong> chiObservedCreated = [];
    private readonly List<ChiSequenceAoe> chiBunkers = [];
    private readonly List<ChiSequenceAoe> chiBombs = [];
    private readonly List<ChiSequenceAoe> chiCombos = [];
    private long chiSequenceId;

    private void ChiObserveMissing()
    {
        if (!IsChi) return;
        foreach (var id in chiObservedCreated.Where(id => !_actors.TryGetValue(id, out var actor)
            || FateCreatedActor(actor).Oid != chiCreated[id]).ToArray())
        {
            chiCreated.Remove(id);
            chiObservedCreated.Remove(id);
        }
    }

    private void ChiCreated(DungeonActor actor, bool observed = true)
    {
        if (!IsChi || Active == null) return;
        if (chiCreated.TryGetValue(actor.Id, out var previous) && previous != actor.Oid)
        {
            chiCreated.Remove(actor.Id);
            chiObservedCreated.Remove(actor.Id);
        }
        if (observed) chiObservedCreated.Add(actor.Id);
        if (!chiCreated.TryAdd(actor.Id, actor.Oid)) return;
        var delay = actor.Oid switch { 0x361E => 14.8, 0x3505 => 16.9, 0x364C => 10, 0x364D => 5.7, _ => 0 };
        if (delay == 0) return;
        var queue = actor.Oid is 0x361E or 0x3505 ? chiBunkers : chiBombs;
        queue.Add(new(++chiSequenceId, actor, null, new RectangleTemplate(20, 20), actor.Position,
            -.003f * (MathF.PI / 180), _now, _now.AddSeconds(delay)));
    }

    private void ChiCast(DungeonPendingCast cast, bool correction, DungeonPendingCast? previous)
    {
        if (!IsChi || cast.Rule.Action is not (25957 or 25955 or 25958 or 25956 or 25963 or 25960)) return;
        var shape = cast.Rule.Action switch
        {
            25957 or 25958 => (GeometryTemplate)RayRectangleTemplate.Symmetric(32, 120),
            25955 or 25956 => new DonutTemplate(16, 60),
            _ => new FanTemplate(45, MathF.PI),
        };
        var source = _actors[cast.Source];
        var backwards = cast.Rule.Action is 25958 or 25956 or 25963 or 25960;
        for (var stage = 0; stage < 2; ++stage)
        {
            var index = correction ? chiCombos.FindIndex(a => ReferenceEquals(a.Cast, previous) && a.Stage == stage) : -1;
            if (correction && index < 0) continue;
            var old = index >= 0 ? chiCombos[index] : null;
            var origin = cast.Location;
            if (stage == 1 && cast.Rule.Action is 25957 or 25958)
                origin = old?.Position ?? source.Position;
            var aoe = new ChiSequenceAoe(old?.Id ?? ++chiSequenceId, source, cast,
                stage == 0 ? shape : new FanTemplate(45, MathF.PI), origin,
                cast.Rotation + (stage == 1 && backwards ? MathF.PI : 0),
                cast.StartedAt, cast.FinishAt.AddSeconds(stage * 3.1), stage, old?.DangerColor ?? false);
            if (index >= 0) chiCombos[index] = aoe;
            else chiCombos.Add(aoe);
        }
    }

    private void ChiFinish(DungeonPendingCast cast)
    {
        if (!IsChi) return;
        if (cast.Rule.Action is 25101 or 25976 && chiBunkers.Count > 0) chiBunkers.RemoveAt(0);
        if (cast.Rule.Action is 27485 or 27486 && chiBombs.Count > 0) chiBombs.RemoveAt(0);
    }

    private void ChiEffect(uint action)
    {
        if (IsChi && action is 25955 or 25956 or 25957 or 25958 or 25960 or 25961 or 25963 or 25964
            && chiCombos.Count > 0)
            chiCombos.RemoveAt(0);
    }

    private IEnumerable<DungeonAmbientAoe> ChiAoes()
    {
        if (!IsChi || Active == null) yield break;
        foreach (var aoe in ChiCreatedAoes(chiBunkers, 2.5, "bunker")) yield return aoe;
        foreach (var aoe in ChiCreatedAoes(chiBombs, 3.5, "bomb")) yield return aoe;
        if (chiCombos.Count > 1) chiCombos[0] = chiCombos[0] with { DangerColor = true };
        for (var i = 0; i < chiCombos.Count; ++i)
        {
            var aoe = chiCombos[i];
            yield return new($"chi/combo/{aoe.Id}", "Carapace / Fore Arms / Rear Guns", aoe.Shape,
                aoe.Position, aoe.Rotation, Navigate: i == 0, Activation: aoe.Activation,
                Color: aoe.DangerColor ? new(1, 1, 0, 1) : null);
        }
        if (chiCombos.Count == 2 && _actors.GetValueOrDefault(PrimaryId) is { } boss && Arena != null)
        {
            var first = chiCombos[0];
            var band = RayRectangleTemplate.Symmetric(80, 4).Resolve(boss.Position
                - new Vector3(MathF.Sin(first.Rotation), 0, MathF.Cos(first.Rotation)) * 2, first.Rotation, null);
            yield return new("chi/combo/close", "组合技贴近首领轴线",
                new SohrKhaiWallHintTemplate(Arena, [band]), Arena.Position, Display: false, Activation: first.Activation);
        }
    }

    private static IEnumerable<DungeonAmbientAoe> ChiCreatedAoes(List<ChiSequenceAoe> queue, double window, string name)
    {
        if (queue.Count == 0) yield break;
        var dangerEnd = queue[0].Activation.AddSeconds(1);
        var displayEnd = queue[0].Activation.AddSeconds(window);
        var laterSet = queue[^1].Activation > dangerEnd;
        for (var i = 0; i < queue.Count && queue[i].Activation <= displayEnd; ++i)
        {
            var aoe = queue[i];
            var risky = aoe.Activation < dangerEnd;
            if (risky && laterSet) queue[i] = aoe = aoe with { DangerColor = true };
            yield return new($"chi/{name}/{aoe.Id}", name, aoe.Shape, aoe.Position, aoe.Rotation,
                Navigate: risky, Activation: aoe.Activation, Color: aoe.DangerColor ? new(1, 1, 0, 1) : null);
        }
    }

    private IEnumerable<string> ChiHints(Vector3 player)
    {
        if (ChiAoes().Any(a => a.Display && a.Navigate && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player, 0)))
            yield return "GTFO from aoe!";
    }

    private void ResetChi()
    {
        chiCreated.Clear();
        chiObservedCreated.Clear();
        chiBunkers.Clear();
        chiBombs.Clear();
        chiCombos.Clear();
        chiSequenceId = 0;
    }
}
