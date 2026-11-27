using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Alliance;

[MechanicScript("影之国·究极神兵原型", [627], "masterplayer.alliance.protoultima",
    "1.0.0", "PromeRadar", note: "原生标准组件；只覆盖源实际激活行为")]
public sealed class ProtoUltima : BozjaScript
{
    public ProtoUltima() : base(627, "A33ProtoUltima") { }
}
