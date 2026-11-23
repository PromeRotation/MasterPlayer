namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TitanExtremeStep(string Name, float Expected, Func<bool> Ready,
    Action? Enter = null, Action? Exit = null, float? MaxOverdue = null, float CheckDelay = 0);
