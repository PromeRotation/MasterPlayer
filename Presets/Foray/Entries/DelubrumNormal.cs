using MasterPlayer.Mechanics.Bozja;
using MasterPlayer.Scripting;

namespace MasterPlayer.Scripts.Foray;

[MechanicScript("女王古殿", [936], "masterplayer.foray.delubrum.normal",
    "1.0.4", "PromeRadar", note: "求道之三位一体、女王骑士、天佑女王、大兀与博兹雅幻灵原生机制；未迁首领保留既有机制模块")]
public sealed class DelubrumNormal() : BozjaScript(936);
