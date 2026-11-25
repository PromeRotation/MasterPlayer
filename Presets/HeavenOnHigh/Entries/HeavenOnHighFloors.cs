using MasterPlayer.Mechanics.HeavenOnHigh;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts;

[MechanicScript("天之御柱：道中战斗机制", [770, 771, 772, 782, 773, 783, 774, 784, 775, 785],
    "masterplayer.hoh.floors", "1.0.0", "MasterPlayer",
    note: "仅战斗范围、视线、墙体与陷阱；不含探索、开箱、道具或楼层推进。")]
public sealed class HeavenOnHighFloors : HeavenOnHighFloorScript;
