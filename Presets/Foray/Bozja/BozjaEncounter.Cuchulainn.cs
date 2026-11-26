using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsCuchulainn => Definition.PrimaryOid == 0x31AB;
    internal static readonly Vector3[] CuchPositions =
    [
        new(637.27f, 0, -174.667f), new(662.71f, 0, -174.667f),
        new(662.71f, 0, -200.133f), new(637.27f, 0, -200.133f),
    ];
    private BozjaOrderTemplate? cuchCircles;
    private BozjaOrderTemplate? cuchInverted;
    private BozjaCast? cuchVoidzone;
    private BozjaCast? cuchMorphZone;
    private BozjaCast? cuchJump;
    private bool cuchMorphActive;
    private ulong cuchGelatinous;
    private ulong cuchMisdirection;
    private readonly Dictionary<ulong, BozjaMarchState> cuchMarch = [];
    private CuchulainnGeometry? cuchWalls;
    internal int CuchNumJumps { get; private set; }
    internal int CuchActiveMarches { get; private set; }
    internal int CuchRaidwideNumCasts { get; private set; }
    internal BozjaOrderTemplate CuchCircles => cuchCircles ??= new(Center, CuchPositions, false, 5.676f, 40);
    private BozjaOrderTemplate CuchInverted => cuchInverted ??= new(Center, CuchPositions, true, 5.676f, 40);

    private void ClearCuchulainn()
    {
        cuchVoidzone = cuchMorphZone = cuchJump = null;
        cuchWalls = null;
        cuchMorphActive = false;
        cuchGelatinous = cuchMisdirection = 0;
        cuchMarch.Clear();
        CuchNumJumps = CuchActiveMarches = CuchRaidwideNumCasts = 0;
    }

    private void StartCuchulainn(BozjaActor actor, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.CuchFlesh)
        {
            if (cuchVoidzone == null && actor.Cast!.ActionId == 23695)
                cuchVoidzone = new(++serial, rule, actor, Center, 0, Activation(actor, .2d)) { ShapeOverride = CuchCircles };
            else if (actor.Cast!.ActionId == 23682)
            {
                // Source does not reset the counter on another visual Start.
                cuchMorphActive = true;
                cuchMorphZone = new(++serial, rule, actor, Center, 0, Activation(actor)) { ShapeOverride = CuchInverted };
            }
        }
        else if (rule.Kind == BozjaRuleKind.Raidwide)
        {
            // RaidwideCasts loops its actual array without a break. DAL2 has
            // PutrifiedSoul1 twice and omits PutrifiedSoul2.
            foreach (var action in rule.Actions.Where(a => a == actor.Cast!.ActionId))
                Casts.Add(new(++serial, rule, actor, actor.Cast!.Location, actor.Cast.Rotation ?? 0, Activation(actor))
                    { Action = action });
        }
    }

    private void CuchEffect(BozjaActor actor, uint action, Vector3? position)
    {
        if (!IsCuchulainn) return;
        if (action is 23683 or 24953)
        {
            if (cuchMorphActive && ++CuchNumJumps == 5)
            {
                CuchNumJumps = 0;
                cuchMorphActive = false;
            }
            if (position is { } target)
            {
                var rule = Definition.Rules.Single(r => r.Kind == BozjaRuleKind.CuchJumps);
                cuchJump = new(++serial, rule, actor, target, 0, Now.AddSeconds(1d));
            }
        }
        else if (action == 23684)
            cuchJump = null;
        if (action == 23692 && SeekerBaits.Count > 0)
            SeekerBaits.RemoveAt(0);
        CuchRaidwideNumCasts += Definition.Rules.Single(r => r.Kind == BozjaRuleKind.Raidwide).Actions.Count(a => a == action);
    }

    private void CuchStatus(BozjaActor actor, ReplayStatus status, bool gain, BozjaRule rule)
    {
        if (!IsCuchulainn) return;
        var slot = ViewerSlot(actor);
        if (slot is >= 0 and < 64)
        {
            var bit = 1UL << slot;
            if (rule.Kind == BozjaRuleKind.CuchFlesh && status.Id == 2543)
                cuchGelatinous = gain ? cuchGelatinous | bit : cuchGelatinous & ~bit;
            if (rule.Component == "GhastlyAura" && status.Id is 1422 or 2936 or 3694 or 3909)
                cuchMisdirection = gain ? cuchMisdirection | bit : cuchMisdirection & ~bit;
        }
        if (rule.Kind != BozjaRuleKind.CuchMarch) return;
        var kind = status.Id switch { 2161 => 0, 2163 => 1, 2162 => 2, 2164 => 3, 1257 or 3629 => 4, _ => -1 };
        if (kind < 0) return;
        if (!cuchMarch.TryGetValue(actor.Actor.Id, out var state))
            cuchMarch.Add(actor.Actor.Id, state = new());
        state.Apply(kind, gain, status.ExpiresAt);
        if (kind == 4) CuchActiveMarches += gain ? 1 : -1;
    }

    private bool CuchNeedsMorph(BozjaActor? viewer)
    {
        var slot = viewer == null ? -1 : ViewerSlot(viewer);
        return cuchMorphActive && (slot is < 0 or >= 64 || (cuchGelatinous & (1UL << slot)) == 0);
    }

    internal bool CuchMisdirection(BozjaActor viewer)
    {
        var slot = ViewerSlot(viewer);
        return Active && IsCuchulainn && slot is >= 0 and < 64 && (cuchMisdirection & (1UL << slot)) != 0;
    }

    private IEnumerable<BozjaAoe> CuchAoes(BozjaActor? viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.CuchJumps && cuchJump is { } jump)
            yield return new(jump, false);
        if (rule.Kind != BozjaRuleKind.CuchFlesh) yield break;
        var morph = CuchNeedsMorph(viewer);
        if ((morph ? cuchMorphZone : cuchVoidzone) is { } zone)
            yield return new(zone, false, SafeColor: morph);
    }

    private IEnumerable<BozjaHint> CuchHints(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.CuchFlesh)
        {
            if (CuchNeedsMorph(viewer)) yield return new("Move into a voidzone to turn into pudding!");
            else if (cuchVoidzone?.Contains(viewer.Actor.Position) == true) yield return new("GTFO from aoe!");
        }
        if (rule.Kind == BozjaRuleKind.CuchMarch)
        {
            var moves = CuchMovements(viewer).ToArray();
            if (moves.Length > 0 && moves[^1].From != moves[^1].To && CuchCircles.Check(moves[^1].To))
                yield return new("Aim for safe spot!");
        }
    }

    internal IEnumerable<(Vector3 From, Vector3 To, float Direction)> CuchMovements(BozjaActor viewer)
    {
        if (!Active || !IsCuchulainn || !cuchMarch.TryGetValue(viewer.Actor.Id, out var state)) yield break;
        foreach (var move in state.Movements(viewer.Actor.Position, viewer.Actor.Rotation, Now,
            (from, direction) => (cuchWalls ??= new(Arena)).WallDistance(from, direction)))
            yield return move;
    }

    internal IEnumerable<(float Center, float HalfWidth, DateTime Activation)> CuchDirections(BozjaActor viewer)
    {
        if (!Active || !IsCuchulainn || !cuchMarch.TryGetValue(viewer.Actor.Id, out var state) || state.Pending.Count == 0) yield break;
        var move = state.Pending[0];
        var (sin, cos) = Math.SinCos(move.Direction);
        foreach (var origin in CuchPositions)
        {
            var delta = origin - viewer.Actor.Position;
            var distance = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            if (distance is <= 6 or >= 25) continue;
            var forward = delta.X * (float)sin + delta.Z * (float)cos;
            var sideways = delta.X * (float)cos - delta.Z * (float)sin;
            yield return (MathF.Atan2(sideways, forward), (float)Math.Asin(6f / distance), move.Activation);
        }
    }
}
