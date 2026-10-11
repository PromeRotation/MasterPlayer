using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

internal static class BlundervilleDeliveryPlanner
{
    public static BlundervilleRaceDecision Apply(BlundervilleRaceDecision decision, BlundervilleRaceState state,
        BlundervilleEncounter encounter, Vector3 player, DateTime now)
    {
        if (state.Course != BlundervilleCourse.Delivery || state.CarryCrystalParam == 0
            || state.DeliveryTarget is not { } destination || decision.Hold
            || Vector2.Distance(new(player.X, player.Z), new(BlundervilleRaceState.PickupCenter.X,
                BlundervilleRaceState.PickupCenter.Z)) > 5 || encounter.DeliveryReady(destination, now)) return decision;
        return decision with { Hold = true, Hint = "等待搬运通道动画周期确认" };
    }
}
