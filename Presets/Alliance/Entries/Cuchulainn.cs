using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("魔航船虚无方舟·丘库雷因", [508], "masterplayer.alliance.cuchulainn",
    "1.0.0", "PromeRadar", note: "原生标准组件；只覆盖源实际激活行为")]
public sealed class Cuchulainn : BozjaScript
{
    public Cuchulainn() : base(508, "A13Cuchulainn") { }
}
