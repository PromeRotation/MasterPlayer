using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsCll3 => Definition.PrimaryOid == 0x2E01;
    internal DateTime[] Cll3Fire { get; } = new DateTime[64];
    internal DateTime[] Cll3Ice { get; } = new DateTime[64];
    private DateTime cll3IceActivation;
    private readonly Dictionary<BozjaActor, Vector3> cll3PreviousPositions = [];

    internal void BeginCll3Frame()
    {
        BeginArtFrame();
        BeginOzmaFrame();
        if (!IsCll3) return;
        cll3PreviousPositions.Clear();
        foreach (var actor in Actors.Values) cll3PreviousPositions.Add(actor, actor.Actor.Position);
    }

    private void ClearCll3()
    {
        Array.Clear(Cll3Fire);
        Array.Clear(Cll3Ice);
        cll3IceActivation = default;
    }

    private void UpdateCll3()
    {
        if (!IsCll3 || cll3IceActivation == default || cll3IceActivation >= Now) return;
        Array.Clear(Cll3Ice);
        cll3IceActivation = default;
    }

    private void Cll3Created(BozjaActor actor, BozjaRule? rule = null)
    {
        if (!IsCll3 || !Active || !actor.NativeNpc || actor.Actor.Oid != 0x1EB037
            || rule != null && rule.Kind != BozjaRuleKind.Cll3Arena) return;
        Arena = BozjaCll3Catalog.Reduced;
        Center = Arena.Position;
        // ArenaChange deliberately retains its original warning after the wall.
    }

    private void BeginCll3Rule(BozjaActor actor, BozjaRule rule)
    {
        Cll3Created(actor, rule);
        if (actor.Cast != null) Start(actor, rule);
        if (rule.Kind == BozjaRuleKind.Cll3Water)
            foreach (var status in actor.Statuses.OfType<ReplayStatus>()) ImmuneStatus(actor, status, true);
    }

    private void StartCll3(BozjaActor actor, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.Cll3Arena)
        {
            if (ReferenceEquals(Arena, BozjaCll3Catalog.Reduced)) return;
            Casts.RemoveAll(c => c.Rule == rule);
            Casts.Add(new(++serial, rule, actor, Center, 0, Activation(actor, 1.2d)));
        }
        else if (rule.Kind == BozjaRuleKind.Cll3Water)
            Casts.Add(new(++serial, rule, actor, actor.Cast!.Location, actor.Cast.Rotation ?? 0, Activation(actor)));
        else
        {
            var fire = rule.Component == "FireIV";
            var activation = Activation(actor, fire ? 0 : 2d);
            Array.Fill(fire ? Cll3Fire : Cll3Ice, activation);
            if (!fire) cll3IceActivation = activation;
        }
    }

    private void Cll3Status(BozjaActor actor, ReplayStatus status, bool gain)
    {
        if (!IsCll3 || !Active || gain || status.Id != 960) return;
        var slot = ViewerSlot(actor);
        if (slot is < 0 or >= 64) return;
        Cll3Fire[slot] = Cll3Ice[slot] = default;
    }

    internal (string Mode, DateTime Activation)? Cll3SpecialMode(BozjaActor viewer)
    {
        if (!IsCll3 || !Active) return null;
        var slot = ViewerSlot(viewer);
        if (slot is < 0 or >= 64) return null;
        var ice = Cll3Ice[slot];
        var fire = Cll3Fire[slot];
        // Blizzard is the earlier component, so equal activation retains it.
        if (ice != default && (fire == default || ice <= fire)) return ("Freezing", ice);
        return fire == default ? null : ("Pyretic", fire);
    }

    private IEnumerable<BozjaHint> Cll3Hints(BozjaActor viewer, BozjaRule rule)
    {
        if (!IsCll3) yield break;
        if (rule.Kind == BozjaRuleKind.Cll3Water)
        {
            if (Cll3WaterMovements(viewer, rule).Any(m => !Arena.Contains(m.To, 0)))
                yield return new("About to be knocked into danger!");
            yield break;
        }
        if (rule.Kind != BozjaRuleKind.Cll3StayMove) yield break;
        var slot = ViewerSlot(viewer);
        if (slot is < 0 or >= 64) yield break;
        var fire = rule.Component == "FireIV";
        var states = fire ? Cll3Fire : Cll3Ice;
        var activation = states[slot];
        if (activation == default) yield break;
        if (activation <= Now.AddSeconds(fire ? 3d : 4d))
        {
            // Preserve StayMove's self-comparison of PrevPosition. It does not
            // detect finite X/Z movement; Move additionally compares height.
            var previous = cll3PreviousPositions.GetValueOrDefault(viewer, viewer.Actor.Position);
            var invalidPrevious = float.IsNaN(previous.X) || float.IsNaN(previous.Z);
            yield return fire
                ? new("Stop everything!", invalidPrevious || viewer.Actor.Cast != null || viewer.Actor.TargetId != 0)
                : new("Move!", !invalidPrevious && viewer.Actor.Position.Y == previous.Y);
        }
        // Source AddHints returns this frame's text before clearing a dead slot.
        if (viewer.Actor.Dead) states[slot] = default;
    }

    internal IEnumerable<(BozjaCast Cast, Vector3 From, Vector3 To)> Cll3WaterMovements(BozjaActor viewer,
        BozjaRule? onlyRule = null)
    {
        if (!IsCll3 || !Active) yield break;
        foreach (var rule in ActiveRules.Where(r => r.Kind == BozjaRuleKind.Cll3Water && (onlyRule == null || r == onlyRule)))
        {
            var from = viewer.Actor.Position;
            foreach (var cast in Casts.Where(c => c.Rule == rule))
            {
                if (WindImmune(viewer, cast.Activation)) continue;
                var delta = from - cast.Origin;
                var length = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
                if (!(length > 0)) continue;
                var inverse = 1f / length;
                var to = from + 12 * new Vector3(delta.X * inverse, 0, delta.Z * inverse);
                yield return (cast, from, to);
                from = to;
            }
        }
    }

    private IEnumerable<BozjaZone> Cll3Navigation(BozjaRule rule)
    {
        if (!IsCll3) yield break;
        if (rule.Kind == BozjaRuleKind.Cll3Water && Casts.FirstOrDefault(c => c.Rule == rule) is { } first)
            yield return new($"cll3/water/{rule.Component}", rule.Component, first.Activation,
                null, default, 0, [first.Origin], JoinRadius: 13, Goal: true);
        if (rule.Kind == BozjaRuleKind.Cll3Arena)
            foreach (var cast in Casts.Where(c => c.Rule == rule))
                yield return new($"cll3/arena/{cast.Generation}", rule.Component, cast.Activation,
                    cast.Shape, cast.Origin, cast.Rotation);
    }
}
