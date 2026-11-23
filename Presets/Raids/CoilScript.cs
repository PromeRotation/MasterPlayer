using MasterPlayer.Scripting;

namespace MasterPlayer.Mechanics.Dungeons;

public abstract class CoilScript(uint territory) : DungeonScript(territory)
{
    [UserSetting("队伍第1槽分工")]
    public CoilAssignment Slot1 { get => Settings.Get(nameof(Slot1), CoilAssignment.Unassigned); set => Settings.Set(nameof(Slot1), value); }
    [UserSetting("队伍第2槽分工")]
    public CoilAssignment Slot2 { get => Settings.Get(nameof(Slot2), CoilAssignment.Unassigned); set => Settings.Set(nameof(Slot2), value); }
    [UserSetting("队伍第3槽分工")]
    public CoilAssignment Slot3 { get => Settings.Get(nameof(Slot3), CoilAssignment.Unassigned); set => Settings.Set(nameof(Slot3), value); }
    [UserSetting("队伍第4槽分工")]
    public CoilAssignment Slot4 { get => Settings.Get(nameof(Slot4), CoilAssignment.Unassigned); set => Settings.Set(nameof(Slot4), value); }
    [UserSetting("队伍第5槽分工")]
    public CoilAssignment Slot5 { get => Settings.Get(nameof(Slot5), CoilAssignment.Unassigned); set => Settings.Set(nameof(Slot5), value); }
    [UserSetting("队伍第6槽分工")]
    public CoilAssignment Slot6 { get => Settings.Get(nameof(Slot6), CoilAssignment.Unassigned); set => Settings.Set(nameof(Slot6), value); }
    [UserSetting("队伍第7槽分工")]
    public CoilAssignment Slot7 { get => Settings.Get(nameof(Slot7), CoilAssignment.Unassigned); set => Settings.Set(nameof(Slot7), value); }
    [UserSetting("队伍第8槽分工")]
    public CoilAssignment Slot8 { get => Settings.Get(nameof(Slot8), CoilAssignment.Unassigned); set => Settings.Set(nameof(Slot8), value); }

    protected override IReadOnlyList<string> PartyMechanicAssignments
        => [Slot1.ToString(), Slot2.ToString(), Slot3.ToString(), Slot4.ToString(),
            Slot5.ToString(), Slot6.ToString(), Slot7.ToString(), Slot8.ToString()];
}
