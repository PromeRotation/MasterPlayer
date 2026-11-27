using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("希望之炮台：“塔”·2P：装备飞行装置", [966], "masterplayer.alliance.paradigmtrash",
    "1.0.0", "PromeRadar", note: "原生标准组件；只覆盖源实际激活行为")]
public sealed class ParadigmTrash : BozjaScript
{
    public ParadigmTrash() : base(966, "A30Trash") { }
}
