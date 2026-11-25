namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CarnivaleRedState
{
    internal bool NeedsBootstrap { get; set; } = true;
    internal CarnivalePyreticState Pyretic { get; } = new();
    internal CarnivaleFerrofluidCast? FerrofluidKnockback { get; set; }
    internal CarnivaleFerrofluidCast? FerrofluidAoe { get; set; }
}
