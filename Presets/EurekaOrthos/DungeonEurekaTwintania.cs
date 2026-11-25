using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool EurekaInputScan { get; set; }
    private readonly HashSet<AlphaActorState> eurekaTwisterCreated = [];
    private readonly EurekaTwister eurekaTwister = new(31468);
    private readonly EurekaTwister eurekaDiveTwister = new(31471);
    private readonly HashSet<ulong> eurekaTwisterInputs = [];
    private readonly List<EurekaTurbineKnockback> eurekaTurbines = [];
    private DungeonAmbientAoe? eurekaDive;
    private bool eurekaDivePreparing;
    internal EurekaTwister EurekaTwister => eurekaTwister;
    internal EurekaTwister EurekaDiveTwister => eurekaDiveTwister;
    internal IReadOnlyList<EurekaTurbineKnockback> EurekaTurbines => eurekaTurbines;

    private IEnumerable<Vector3> EurekaAliveParty() => CurrentPartyMembers
        .Select(p => _actors.GetValueOrDefault(p.EntityId))
        .Where(a => a != null && !a.Dead).Select(a => a!.Position);

    private void EurekaTwintaniaCreated(DungeonActor actor)
    {
        if (territory != EurekaOrthosCatalog.TWINTANIA || actor.Oid != 0x1E8910
            || !eurekaActors.TryGetValue(actor.Id, out var state) || !eurekaTwisterCreated.Add(state)) return;
        eurekaTwister.Positions.Clear();
        eurekaDiveTwister.Positions.Clear();
    }

    private void EurekaTwintaniaCast(DungeonPendingCast cast)
    {
        if (territory != EurekaOrthosCatalog.TWINTANIA || !cast.Observed) return;
        if (cast.Rule.Action is 31468 or 31471 && eurekaTwisterInputs.Add(cast.Source))
            (cast.Rule.Action == 31468 ? eurekaTwister : eurekaDiveTwister).Start(cast);
        if (cast.Rule.Action == 31467)
        {
            var index = eurekaTurbines.FindIndex(k => k.Cast.Source == cast.Source);
            if (index >= 0) eurekaTurbines[index] = new(cast);
            else eurekaTurbines.Add(new(cast));
        }
    }

    private void EurekaTwintaniaFinish(DungeonPendingCast cast)
    {
        if (territory != EurekaOrthosCatalog.TWINTANIA || !cast.Observed) return;
        if (cast.Rule.Action is 31468 or 31471)
        {
            (cast.Rule.Action == 31468 ? eurekaTwister : eurekaDiveTwister).Finish(_now, EurekaAliveParty());
            eurekaTwisterInputs.Remove(cast.Source);
        }
        if (cast.Rule.Action == 31471)
        {
            eurekaDive = null;
            eurekaDivePreparing = false;
        }
        if (cast.Rule.Action == 31467)
        {
            var index = eurekaTurbines.FindIndex(k => k.Cast.Source == cast.Source);
            if (index >= 0) eurekaTurbines.RemoveAt(index);
        }
    }

    private void EurekaTwintaniaUpdate()
    {
        if (territory != EurekaOrthosCatalog.TWINTANIA || Active == null || eurekaWiping) return;
        var hasTwisters = _actors.Values.Any(a => a.Oid == 0x1E8910);
        eurekaTwister.Update(_now, hasTwisters, EurekaAliveParty());
        eurekaDiveTwister.Update(_now, hasTwisters, EurekaAliveParty());
    }

    private void EurekaTimeline(ulong source, uint timeline)
    {
        if (territory != EurekaOrthosCatalog.TWINTANIA || Active == null || !eurekaEnabled
            || source != PrimaryId || !_actors.TryGetValue(source, out var actor)) return;
        if (timeline == 0x1E3A) eurekaDivePreparing = true;
        else if (timeline == 0x1E43 && eurekaDivePreparing)
            eurekaDive = new("eureka/dive", "Twisting Dive", RayRectangleTemplate.Symmetric(15, 50),
                QuantizedDungeonPosition(actor.Position), actor.Rotation, Activation: _now.AddSeconds(6.9d));
    }

    internal void UpdateEurekaFrame(DateTime now)
    {
        _now = now;
        EurekaUpdate();
        if (territory == EurekaOrthosCatalog.TWINTANIA) TrialVoidzoneObserve();
    }

    internal (Vector3 Origin, Vector3[] Winds, DateTime Activation)? EurekaTurbineNavigation()
    {
        if (territory != EurekaOrthosCatalog.TWINTANIA || Active == null || !eurekaEnabled
            || Arena == null || !_actors.ContainsKey(PlayerId) || eurekaTurbines.Count == 0) return null;
        return (Arena.Position, TrialVoidzoneAoes().Select(a => a.Position).ToArray(), eurekaTurbines[0].Cast.FinishAt);
    }

    internal IEnumerable<(EurekaTurbineKnockback Source, Vector3 From, Vector3 To, bool Unsafe)> EurekaTurbineMovements(DungeonActor player)
    {
        if (territory != EurekaOrthosCatalog.TWINTANIA || Arena == null) yield break;
        var from = player.Position;
        foreach (var source in eurekaTurbines)
        {
            var direction = from - source.Cast.Location;
            direction.Y = 0;
            if (direction.LengthSquared() == 0) continue;
            var to = from + Vector3.Normalize(direction) * source.Distance;
            var unsafeLanding = !Arena.Contains(to, 0) || TrialVoidzoneAoes()
                .Any(a => Vector2.DistanceSquared(new(to.X, to.Z), new(a.Position.X, a.Position.Z)) <= 25);
            yield return (source, from, to, unsafeLanding);
            from = to;
        }
    }

    private IEnumerable<DungeonAmbientAoe> EurekaTwintaniaAoes()
    {
        if (territory != EurekaOrthosCatalog.TWINTANIA) yield break;
        foreach (var twister in new[] { eurekaTwister, eurekaDiveTwister })
        {
            for (var i = 0; i < twister.Positions.Count; ++i)
                yield return new($"eureka/twister/{twister.Action}/{i}", "Twister", new CircleTemplate(1.5f),
                    twister.Positions[i], Activation: twister.Activation);
            foreach (var actor in _actors.Values.Where(a => a.Oid == 0x1E8910 && a.EventState != 7))
                yield return new($"eureka/twister/{twister.Action}/live/{actor.Id}", "Twister", new CircleTemplate(1.5f), actor.Position);
        }
        if (eurekaDive != null) yield return eurekaDive;
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var index = 0;
        foreach (var movement in EurekaTurbineMovements(player))
            foreach (var aoe in QuestKnockbackProjection($"eureka/turbine/{index++}", player, movement.From, movement.To))
                yield return aoe;
    }

    private IEnumerable<string> EurekaTwintaniaHints(Vector3 player)
    {
        if (territory != EurekaOrthosCatalog.TWINTANIA || Active == null || !eurekaEnabled) yield break;
        if (_casts.Values.Any(c => c.Observed && c.Rule.Action is 31468 or 31471))
            yield return "Twisters soon, get moving!";
        if (EurekaTwintaniaAoes().Any(a => a.Id.StartsWith("eureka/twister/")
            && Vector2.DistanceSquared(new(player.X, player.Z), new(a.Position.X, a.Position.Z)) <= 2.25f))
            yield return "GTFO from twister!";
        if (TrialVoidzoneAoes().Any(a => Vector2.DistanceSquared(new(player.X, player.Z), new(a.Position.X, a.Position.Z)) <= 25))
            yield return "GTFO from voidzone!";
        if (_actors.GetValueOrDefault(PlayerId) is { } actor && EurekaTurbineMovements(actor).Any(m => m.Unsafe))
            yield return "About to be knocked into danger!";
    }
}
