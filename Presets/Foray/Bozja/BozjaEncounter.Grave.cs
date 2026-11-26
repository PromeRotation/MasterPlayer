using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsGrave => Definition.PrimaryOid == 0x2E35;
    private bool graveArenaShrunk;
    private bool graveDual;
    private readonly List<BozjaAoe> gravePurges = [];
    private List<BozjaActor>? graveSpheres;
    private readonly Dictionary<ulong, BozjaMarchState> graveMarch = [];
    internal int GraveActiveMarches { get; private set; }
    internal IEnumerable<BozjaActor> GraveSpheres => graveSpheres ??=
        Actors.Values.Where(a => a.Actor.Oid == 0x3232).ToList();

    private void ClearGrave()
    {
        graveArenaShrunk = graveDual = false;
        gravePurges.Clear();
        graveSpheres = null;
        graveMarch.Clear();
        GraveActiveMarches = 0;
    }

    private void GraveCreated(BozjaActor actor)
    {
        if (!Active || !IsGrave) return;
        if (actor.Actor.Oid == 0x3232) graveSpheres?.Add(actor);
        if (!actor.NativeNpc) return;
        if (actor.Actor.Oid != 0x1EB173) return;
        Center = DungeonEncounter.QuantizedDungeonPosition(Center);
        Arena = new CircleGeometry(Center, 25);
        graveArenaShrunk = true;
        Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.GraveArena);
    }

    private void StartGrave(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.GraveArena)
        {
            if (graveArenaShrunk) return;
            Casts.RemoveAll(c => c.Rule == rule);
            Casts.Add(new(++serial, rule, actor, Center, 0, Activation(actor, .8d)));
        }
        if (rule.Kind != BozjaRuleKind.SoulPurge) return;
        if (spell.ActionId == 24098) graveDual = true;
        else
        {
            var circle = spell.ActionId == 24099;
            Add(circle, 0);
            if (graveDual)
            {
                Add(!circle, 2.1f);
                graveDual = false;
            }
        }
        void Add(bool circle, double delay)
            => gravePurges.Add(new(new(++serial, rule, actor, spell.Location, 0, Activation(actor, delay))
            { ShapeOverride = circle ? new CircleTemplate(10) : new DonutTemplate(10, 30) }, false));
    }

    private void GraveEffect(uint action)
    {
        if (IsGrave && gravePurges.Count != 0 && action is 24099 or 24100 or 24101 or 24102)
            gravePurges.RemoveAt(0);
    }

    internal IEnumerable<BozjaAoe> GraveAoes(BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.GraveArena)
            foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false);
        if (rule.Kind == BozjaRuleKind.ShockSphere)
            foreach (var actor in GraveSpheres)
                yield return new(new(-actor.BirthOrder, rule, actor, actor.Actor.Position, actor.Actor.Rotation, default), false);
        if (rule.Kind != BozjaRuleKind.SoulPurge) yield break;
        for (var i = 0; i < gravePurges.Count; ++i)
        {
            // The first element is recolored only while Count>1. Its Danger
            // color remains after a query and later FIFO promotion.
            var aoe = gravePurges[i] with { Risky = i == 0 };
            if (i == 0 && gravePurges.Count > 1) aoe = aoe with { Danger = true };
            gravePurges[i] = aoe;
            yield return aoe;
        }
    }

    private void GraveStatus(BozjaActor actor, ReplayStatus status, bool gain)
    {
        if (!IsGrave) return;
        var kind = status.Id switch { 2161 => 0, 2163 => 1, 2162 => 2, 2164 => 3, 1257 or 3629 => 4, _ => -1 };
        if (kind < 0) return;
        if (!graveMarch.TryGetValue(actor.Actor.Id, out var state))
            graveMarch.Add(actor.Actor.Id, state = new());
        state.Apply(kind, gain, status.ExpiresAt);
        if (kind == 4) GraveActiveMarches += gain ? 1 : -1;
    }

    internal IEnumerable<(Vector3 From, Vector3 To, float Direction)> GraveMovements(BozjaActor viewer)
    {
        if (!Active || !IsGrave || !graveMarch.TryGetValue(viewer.Actor.Id, out var state)) yield break;
        foreach (var move in state.Movements(viewer.Actor.Position, viewer.Actor.Rotation, Now))
            yield return move;
    }

    private bool GraveMarchUnsafe(BozjaActor viewer)
    {
        var moves = GraveMovements(viewer).ToArray();
        if (moves.Length == 0 || moves[^1].From == moves[^1].To) return false;
        return Casts.Any(c => c.Rule.Component == "PurifyingLight" && !c.Contains(moves[^1].To));
    }
}
