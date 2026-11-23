using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Hunts;

// TerritoryIntendedUse=1 from the matching CN game data: open-world sessions use local terrain.
[MechanicScript("狩猎基础机制", [
    134u, 135u, 137u, 138u, 139u, 140u, 141u, 145u, 146u, 147u, 148u,
    152u, 153u, 154u, 155u, 156u, 180u, 250u, 397u, 398u, 399u, 400u, 401u, 402u,
    612u, 613u, 614u, 620u, 621u, 622u, 813u, 814u, 815u, 816u, 817u, 818u,
    956u, 957u, 958u, 959u, 960u, 961u, 1187u, 1188u, 1189u, 1190u, 1191u, 1192u],
    "masterplayer.hunts.simple", "1.0.0", "XSZ", "按敌人实例处理范围与伤害预告；保留局部地形。")]
public sealed class SimpleHunts : SimpleHuntScript;
