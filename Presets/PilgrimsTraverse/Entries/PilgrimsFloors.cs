using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.PilgrimsTraverse;

[MechanicScript("朝圣交错路楼层机制", [1281, 1282, 1283, 1284, 1285, 1286, 1287, 1288, 1289, 1290],
    "masterplayer.pilgrims.floors", "1.0.0", "MasterPlayer", note: "仅战斗机制；无探索、交互、道具或层间推进")]
public sealed class PilgrimsFloors : PilgrimsFloorScript;
