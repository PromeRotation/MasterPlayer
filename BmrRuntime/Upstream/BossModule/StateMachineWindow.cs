using Dalamud.Bindings.ImGui;
namespace BossMod;

[SkipLocalsInit]
public sealed class StateMachineWindow : UIWindow
{
    private readonly Timeline _timeline = new();
    private readonly ColumnStateMachineTree _col;

    public StateMachineWindow(BossModule module) : base($"{module.GetType().Name} timeline", true, new(600f, 600f))
    {
        _col = _timeline.Columns.Add(new ColumnStateMachineTree(_timeline, new(module.StateMachine), module.StateMachine));
        _timeline.MaxTime = _col.Tree.TotalMaxTime;
    }

    public override void Draw()
    {
        if (ImGui.CollapsingHeader("设置"))
        {
            ImGui.Checkbox("绘制未命名节点", ref _col.DrawUnnamedNodes);
            ImGui.Checkbox("仅显示死刑节点", ref _col.DrawTankbusterNodesOnly);
            ImGui.Checkbox("仅显示团队AOE节点", ref _col.DrawRaidwideNodesOnly);
        }

        _timeline.CurrentTime = null;
        if (_col.ControlledSM?.ActiveState != null)
        {
            var dt = _col.ControlledSM.ActiveState.Duration - _col.ControlledSM.TimeSinceTransitionClamped;
            var activeNode = _col.Tree.Nodes[_col.ControlledSM.ActiveState.ID];
            _timeline.CurrentTime = activeNode.Time - dt;
        }

        _timeline.Draw();
    }
}
