namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private ulong viperPrimary;
    private ulong viperGowrow;
    private bool viperGowrowDead;
    private bool viperGowrowRemoved;
    private bool viperCompleted;
    private bool viperWiping;
    internal ulong? SourcePlayerId { get; set; }
    private bool IsViper => territory == JobQuestCatalog.FANGS_OF_THE_VIPER;
    private DungeonActor? ViperSourcePlayer => SourcePlayerId is { } id
        ? _actors.GetValueOrDefault(id) : _actors.Values.FirstOrDefault(a => a.ObjectIndex == 0);
    // Normal play follows PlayerState; ARR follows object zero, independently of the viewing perspective.
    private bool ViperPull => ViperSourcePlayer?.InCombat == true
        && !viperCompleted && !viperWiping;

    private void ViperObserve()
    {
        if (!IsViper) return;
        var primary = _actors.Values.FirstOrDefault(a => a.Oid == 0x429F)?.Id ?? 0;
        if (viperPrimary != primary)
        {
            if (Active != null) ResetFight();
            viperPrimary = primary;
            viperGowrow = 0;
            viperGowrowDead = false;
            viperGowrowRemoved = false;
            viperCompleted = false;
        }
        if (!viperGowrowRemoved && _actors.GetValueOrDefault(viperGowrow) is { } gowrow)
            viperGowrowDead = gowrow.Dead;
        else if (viperGowrow != 0)
            viperGowrowRemoved = true;
    }

    private void ViperCacheGowrow()
    {
        if (!IsViper || viperGowrow != 0) return;
        if (_actors.Values.FirstOrDefault(a => a.Oid == 0x42A0) is not { } actor) return;
        viperGowrow = actor.Id;
        viperGowrowDead = actor.Dead;
    }

    private void ViperDirector(uint update)
    {
        if (!IsViper || update is not (0x40000005 or 0x40000011)) return;
        viperWiping = update == 0x40000005;
        if (!viperWiping) return;
        ResetFight();
        viperPrimary = 0;
        viperGowrow = 0;
        viperGowrowDead = false;
        viperGowrowRemoved = false;
        viperCompleted = false;
    }
}
