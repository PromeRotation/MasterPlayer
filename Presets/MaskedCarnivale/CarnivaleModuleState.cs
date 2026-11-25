namespace MasterPlayer.Mechanics.Dungeons;

// Constructor components belong to the module while it is pending as well as loaded.
internal sealed class CarnivaleModuleState(AlphaActorState primary)
{
    internal AlphaActorState Primary { get; set; } = primary;
    internal bool Started { get; set; }
    internal List<(long Generation, AlphaActorState State)> Explosions { get; } = [];
    internal List<CarnivaleInterruptCast> Interrupts { get; } = [];
    internal List<CarnivaleLineOfSightCast> LastSongs { get; } = [];
    internal bool LastSongCasting { get; set; }
    internal bool LastSongNeedsBootstrap { get; set; }
    internal List<CarnivaleBallastSequence> Ballasts { get; } = [];
    internal Dictionary<ulong, long> BallastVisuals { get; } = [];
    internal bool BallastNeedsBootstrap { get; set; }
    internal List<CarnivaleChargeCast> Charges { get; } = [];
    internal List<AlphaActorState> ChargeKegs { get; } = [];
    internal Dictionary<ulong, long> ChargeInputs { get; } = [];
    internal CarnivaleChargeKnockback? ChargeKnockback { get; set; }
    internal bool ChargeNeedsBootstrap { get; set; }
    internal CarnivaleKnockbackImmunity ChargeImmunity { get; } = new();
    internal bool VoidzoneNeedsBootstrap { get; set; }
    internal bool PuddingNeedsBootstrap { get; set; }
    internal CarnivaleEyeState? Eyes { get; set; }
    internal bool VoltageNeedsBootstrap { get; set; }
    internal List<CarnivaleLineOfSightCast> Voltages { get; } = [];
    internal bool AzulmagiaNeedsBootstrap { get; set; }
    internal bool WebCasting { get; set; }
    internal CarnivaleSiegfriedState? Siegfried { get; set; }
    internal CarnivaleGoldorState? Goldor { get; set; }
    internal CarnivaleGogoState? Gogo { get; set; }
    internal CarnivaleRedState? Red { get; set; }
    internal CarnivaleDraugarState? Draugar { get; set; }
    internal CarnivaleSnorterState? Snorter { get; set; }
    internal bool PapaNeedsBootstrap { get; set; }
    internal CarnivaleDadJoke? DadJoke { get; set; }
    internal bool PitNeedsBootstrap { get; set; }
    internal bool SapNeedsBootstrap { get; set; }
    internal List<(long Generation, DungeonPendingCast Cast)> Saps { get; } = [];
    internal bool ChimeraNeedsBootstrap { get; set; }
    internal bool TyphonNeedsBootstrap { get; set; }
    internal string ImpSongHint { get; set; } = "Interrupt!";
    internal List<CarnivaleKnockbackCast> CastKnockbacks { get; } = [];
    internal List<(long Generation, DungeonPendingCast Cast)> TyphonFireballs { get; } = [];
    internal bool RebekkahNeedsBootstrap { get; set; }
    internal DungeonPendingCast? ReflectCast { get; set; }
    internal bool ReflectCasting { get; set; }
    internal bool ReflectActive { get; set; }
    internal DungeonPendingCast? BlindTrigger { get; set; }
    internal bool BlindCasting { get; set; }
    internal CarnivaleBlindness Blindness { get; } = new();
    internal List<CarnivaleVoidzoneCast> RayCasts { get; } = [];
    internal List<CarnivaleVoidzonePrediction> RayPredictions { get; } = [];
}
