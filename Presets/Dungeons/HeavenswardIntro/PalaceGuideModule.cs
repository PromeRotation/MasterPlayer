namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class PalaceGuideModule(AlphaActorState primary)
{
    internal AlphaActorState Primary { get; set; } = primary;
    internal bool Started { get; set; }
    internal bool Finished { get; set; }
    internal bool Active => Started && !Finished;
    internal int GudannaCastIndex { get; set; }
    internal bool GudannaRotationDisabled { get; set; }
    internal bool YaquaruInitialized { get; set; }
    internal List<AlphaActorState> YaquaruPuddles { get; } = [];
    internal PalaceYaquaruHeavy YaquaruHeavy { get; } = new();
    internal List<PalaceBlackRiderCharge> BlackRiderCharges { get; } = [];
    internal Dictionary<ulong, long> BlackRiderInputs { get; } = [];
    internal Dictionary<ulong, DungeonCastObservation> BlackRiderObservedCasts { get; } = [];
    internal bool BlackRiderInitialized { get; set; }
    internal Dictionary<ulong, int> BlackRiderEventRoster { get; } = [];
    internal PalaceBlackRiderKnockback? BlackRiderKnockback { get; set; }
    internal CarnivaleKnockbackImmunity BlackRiderImmunity { get; } = new();
}
