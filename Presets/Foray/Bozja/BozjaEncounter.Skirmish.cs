using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsSkirmish => Definition.FateId != 0;
    internal string? FateDiagnostic => IsSkirmish && Primary != null && currentFate == null
        ? "录像缺少当前FATE原始身份、中心和半径，未猜测场地" : null;
    private ReplayActiveFate? currentFate;
    private uint cachedFateId;

    internal void SetFate(ReplayActiveFate? value) => currentFate = value;

    private void UpdateSkirmishArena()
    {
        if (!IsSkirmish || Primary is not { } primary || currentFate is not { } fate) return;
        var radius = Arena is CircleGeometry circle ? circle.Radius : 30f;
        if (fate.Id != 0 && fate.Radius > 0
            && (primary.CreationFateId > 0 ? primary.CreationFateId == fate.Id : Definition.FateId == fate.Id))
        {
            if (cachedFateId != fate.Id || MathF.Abs(Center.X - fate.Center.X) > .01f
                || MathF.Abs(Center.Z - fate.Center.Z) > .01f || MathF.Abs(radius - fate.Radius) > .01f)
            {
                cachedFateId = fate.Id;
                Center = fate.Center with { Y = 0 };
                Arena = new CircleGeometry(Center, fate.Radius);
            }
        }
        else if (cachedFateId == 0)
        {
            Center = primary.Actor.Position with { Y = 0 };
            Arena = new CircleGeometry(Center, 30);
        }
    }

    private bool SkirmishWantsReset(BozjaActor viewer)
        => IsSkirmish && (currentFate == null || !Primary!.Actor.InCombat
            || cachedFateId != 0 && currentFate.Id != cachedFateId
            || DistanceSquared(viewer.Actor.Position, Center) > (SampleRadius + 10) * (SampleRadius + 10));

    private void ResetSkirmishArena(BozjaActor? primary)
    {
        cachedFateId = 0;
        if (!IsSkirmish || primary == null) return;
        Center = primary.Actor.Position with { Y = 0 };
        Arena = new CircleGeometry(Center, 30);
    }
}
