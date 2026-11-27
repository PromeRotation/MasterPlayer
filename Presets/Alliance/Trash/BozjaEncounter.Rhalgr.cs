using MasterPlayer.Mechanics.Dungeons;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsRhalgr => Definition.Key == "A10RhalgrEmissary";
    private readonly List<BozjaRule> rhalgrRules = [];
    internal BozjaDelubrumTimeline? RhalgrTimeline { get; private set; }
    internal int RhalgrChargeCount { get; private set; }
    internal int RhalgrBlueCount { get; private set; }
    internal int RhalgrStrikeCount { get; private set; }
    internal int RhalgrChargeAoes => Casts.Count(c => c.Rule.Component == "DestructiveCharge");
    internal int RhalgrLightningCasters => Casts.Count(c => c.Rule.Component == "LightningBolt");

    private void BeginRhalgr()
    {
        rhalgrRules.AddRange(Definition.Rules);
        RhalgrTimeline = new(this);
        RhalgrTimeline.Begin();
    }

    private void ClearRhalgr()
    {
        RhalgrTimeline?.Stop();
        RhalgrTimeline = null;
        rhalgrRules.Clear();
        RhalgrChargeCount = RhalgrBlueCount = RhalgrStrikeCount = 0;
    }

    private void UpdateRhalgr()
    {
        if (!IsRhalgr || !Active) return;
        if (Primary is not { } primary || PrimaryPhaseComplete(primary))
        {
            Active = false;
            Completed = true;
            ClearRhalgr();
            ClearCrystals();
            ClearDahu();
            Casts.Clear();
            return;
        }
        // State conditions observe the previously published component state.
        RhalgrTimeline?.Update();
    }

    internal void ResetRhalgr(string name)
    {
        var rule = rhalgrRules.Single(r => r.Component == name);
        rhalgrRules.Remove(rule);
        Casts.RemoveAll(c => c.Rule == rule);
        if (name == "DestructiveCharge") RhalgrChargeCount = 0;
        if (name == "BoltsFromTheBlue") RhalgrBlueCount = 0;
        if (name == "Boltloop") ClearCrystals();
        rhalgrRules.Add(rule);
        // ResetComp creates a new component at the end of the active list.
        // Only current NPC spell casts are replayed; Map/Effect history is absent.
        foreach (var actor in Actors.Values)
            if (actor.Cast != null) Start(actor, rule);
    }

    private bool StartRhalgr(BozjaRule rule)
        => IsRhalgr && rule.Component is "BoltsFromTheBlue" or "DestructiveCharge";

    private void RhalgrMap(byte index, uint state)
    {
        if (!Active || !IsRhalgr || index != 0x25) return;
        var rotation = state switch
        {
            0x00080004 => -45f * (MathF.PI / 180),
            0x00100004 => 45f * (MathF.PI / 180),
            _ => 0,
        };
        if (rotation == 0 || Primary == null) return;
        var rule = rhalgrRules.Single(r => r.Component == "DestructiveCharge");
        var origin = DungeonEncounter.QuantizedDungeonPosition(Primary.Actor.Position);
        var activation = Now.AddSeconds(16.1d);
        Casts.Add(new(++serial, rule, Primary, origin, rotation, activation));
        Casts.Add(new(++serial, rule, Primary, origin, rotation + 180f * (MathF.PI / 180), activation));
    }

    private void RhalgrEffect(uint action)
    {
        if (!IsRhalgr) return;
        if (action == 28891)
        {
            ++RhalgrChargeCount;
            Casts.RemoveAll(c => c.Rule.Component == "DestructiveCharge");
        }
        if (action == 28900) ++RhalgrBlueCount;
        if (action == 28889) ++RhalgrStrikeCount;
    }
}
