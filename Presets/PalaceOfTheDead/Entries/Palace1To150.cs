using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.PalaceOfTheDead;

[MechanicScript("死者宫殿 1–150层战斗机制",
    [561u, 562u, 563u, 564u, 565u, 593u, 594u, 595u, 596u, 597u, 598u, 599u, 600u, 601u, 602u],
    "promeradar.potd1-150", "1.0.0", "XSZ",
    "MP原生普通层AOE、视线、打断、反伤、墙体及陷阱；Boss由各自独立脚本接管，不含探索或层间流程")]
public sealed class Palace1To150 : PalaceScript
{
    protected override bool IncludesBossMechanics => false;
}
