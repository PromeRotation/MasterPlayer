namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntChestThumpState
{
    private int casts;
    private int extraHits;
    private bool casting;
    internal DateTime Activation { get; private set; }
    internal string? Hint
    {
        get
        {
            if (!casting) return null;
            return casts == 0 ? "Raidwide" : "Raidwide x5";
        }
    }

    internal void Start(DateTime activation)
    {
        casting = true;
        Activation = activation;
    }

    internal void Finish()
    {
        ++casts;
        if (casts == 1) casting = false;
    }

    internal void Effect()
    {
        if (++extraHits != 4) return;
        casting = false;
        extraHits = 0;
    }
}
