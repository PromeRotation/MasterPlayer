using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsRhalgr => territory == MainScenarioQuestCatalog.RHALGRS_BEACON;
    private readonly List<(DungeonActor Actor, bool Removed, int Key)> rhalgrTermini = [];
    private int rhalgrTerminusKey;
    private readonly HashSet<ulong> rhalgrKnown = [];
    private DateTime? rhalgrTerminusActivation;
    private (DungeonPendingCast Cast, Vector3 Origin)? rhalgrGunblade;
    private readonly Dictionary<ulong, DungeonPendingCast> rhalgrStarted = [];

    private void RhalgrObserve()
    {
        if (!IsRhalgr || Active == null) return;
        for (var i = 0; i < rhalgrTermini.Count; ++i)
        {
            var item = rhalgrTermini[i];
            if (item.Removed) continue;
            if (_actors.TryGetValue(item.Actor.Id, out var actor) && actor.Oid == 0x1BCA)
                rhalgrTermini[i] = (actor, false, item.Key);
            else
            {
                rhalgrTermini[i] = (item.Actor, true, item.Key);
                rhalgrKnown.Remove(item.Actor.Id);
            }
        }
        foreach (var id in rhalgrKnown.Where(id => _actors.GetValueOrDefault(id)?.Oid != 0x1BCA).ToArray())
            rhalgrKnown.Remove(id);
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1BCA))
            if (rhalgrKnown.Add(actor.Id)) rhalgrTermini.Add((actor, false, ++rhalgrTerminusKey));
    }

    private void RhalgrCast(DungeonPendingCast cast)
    {
        if (!IsRhalgr) return;
        if (cast.Rule.Action == 8370) rhalgrTerminusActivation = cast.FinishAt;
        if (cast.Rule.Action != 8310 || !_actors.TryGetValue(cast.Source, out var actor)) return;
        var sameGeneration = rhalgrStarted.TryGetValue(cast.Source, out var previous)
            && Math.Abs((previous.StartedAt - cast.StartedAt).TotalSeconds) < .25
            && Math.Abs((previous.FinishAt - cast.FinishAt).TotalSeconds) < .75;
        rhalgrGunblade = (cast, sameGeneration && rhalgrGunblade is { } kb
            ? kb.Origin : QuantizedDungeonPosition(actor.Position));
        rhalgrStarted[cast.Source] = cast;
    }

    private void RhalgrFinish(DungeonPendingCast cast)
    {
        if (!IsRhalgr || cast.Rule.Action != 8310) return;
        rhalgrGunblade = null;
        rhalgrStarted.Remove(cast.Source);
    }

    private void RhalgrRemoved(ulong id)
    {
        rhalgrKnown.Remove(id);
        for (var i = 0; i < rhalgrTermini.Count; ++i)
            if (rhalgrTermini[i].Actor.Id == id)
                rhalgrTermini[i] = (rhalgrTermini[i].Actor, true, rhalgrTermini[i].Key);
    }

    private void RhalgrEffect(ulong source, uint action)
    {
        if (!IsRhalgr || action != 8337) return;
        var index = rhalgrTermini.FindIndex(t => t.Actor.Id == source && !t.Removed);
        if (index >= 0) rhalgrTermini.RemoveAt(index);
    }

    private Vector3 RhalgrLanding(DungeonActor player, Vector3 origin)
    {
        var delta = player.Position - origin;
        delta.Y = 0;
        if (delta.LengthSquared() == 0) return player.Position;
        var direction = Vector3.Normalize(delta);
        var distance = MathF.Min(10, QuestWindRay(player.Position, direction) - MathF.Max(.001f, player.HitboxRadius - .499f));
        return player.Position + direction * distance;
    }

    private IEnumerable<DungeonAmbientAoe> RhalgrAoes()
    {
        if (!IsRhalgr || Active == null || Arena == null) yield break;
        foreach (var (actor, _, key) in rhalgrTermini)
            yield return new($"rhalgr/terminus/{actor.Id}/{key}",
                "Terminus Est", RayRectangleTemplate.Symmetric(4, 41), actor.Position, actor.Rotation,
                Activation: rhalgrTerminusActivation ?? _now.AddSeconds(10));
        var marker = new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]);
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1BCA && !a.Dead))
        {
            var outside = !Arena.Contains(actor.Position);
            var position = actor.Position;
            if (outside)
            {
                var delta = position - Arena.Position;
                delta.Y = 0;
                var clamped = Arena.Position + Vector3.Normalize(delta) * 20;
                position = new(clamped.X, position.Y, clamped.Z);
            }
            yield return new($"rhalgr/marker/{actor.Id}", "Terminus Est marker", marker, position, actor.Rotation,
                Navigate: false, OutlineOnly: outside, Unclipped: true, Color: new Vector4(1, .5f, 0, 1));
        }
        if (rhalgrGunblade is not { } kb || _actors.GetValueOrDefault(PlayerId) is not { } player
            || WhorlImmune(player, kb.Cast.FinishAt)) yield break;
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1EA4D9))
        {
            var delta = actor.Position - kb.Origin;
            var distance = new Vector2(delta.X, delta.Z).Length();
            yield return new($"rhalgr/kb/avoid/{actor.Id}", "Avoid knockback into Chopping Block",
                new FanTemplate(100, 2 * MathF.Asin(5 / distance)), kb.Origin, MathF.Atan2(delta.X, delta.Z),
                Display: false, Activation: kb.Cast.FinishAt);
        }
        foreach (var aoe in QuestKnockbackProjection("rhalgr/kb", player, player.Position, RhalgrLanding(player, kb.Origin)))
            yield return aoe;
    }

    private IEnumerable<string> RhalgrHints(Vector3? position)
    {
        if (!IsRhalgr || Active == null || position is not { } p) yield break;
        if (rhalgrTermini.Any(t => RayRectangleTemplate.Symmetric(4, 41).Resolve(t.Actor.Position, t.Actor.Rotation, null).Contains(p)))
            yield return "GTFO from aoe!";
        if (TrialVoidzoneAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(p)))
            yield return "GTFO from voidzone!";
        if (rhalgrGunblade is { } kb && _actors.GetValueOrDefault(PlayerId) is { } player && !WhorlImmune(player, kb.Cast.FinishAt)
            && TrialVoidzoneAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(RhalgrLanding(player, kb.Origin))))
            yield return "About to be knocked into danger!";
    }
}
