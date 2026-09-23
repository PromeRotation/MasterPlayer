namespace BossMod;

[ConfigDisplay(Name = "回放", Order = 0)]
public sealed class ReplayManagementConfig : ConfigNode
{
    [PropertyDisplay("显示回放管理界面")]
    public bool ShowUI = false;

    [PropertyDisplay("进入或录制无完整模块副本时显示聊天提醒")]
    public bool ImportantDutyAlert = true;

    [PropertyDisplay("副本开始/结束或野外模块启停时自动录制回放")]
    public bool AutoRecord = false;

    [PropertyDisplay("在副本录像中自动录制", tooltip: "需要开启自动录制")]
    public bool AutoARR = false;

    [PropertyDisplay("保留的最大回放数量")]
    [PropertySlider(0, 1000)]
    public int MaxReplays = 0;

    [PropertyDisplay("在回放中记录并存储服务器数据包")]
    public bool RecordServerPackets = false;

    [PropertyDisplay("将服务器数据包导出到 dalamud.log")]
    public bool DumpServerPackets = false;

    [PropertyDisplay("导出到 dalamud.log 时忽略其他玩家的数据包")]
    public bool DumpServerPacketsPlayerOnly = false;

    [PropertyDisplay("将客户端数据包导出到 dalamud.log")]
    public bool DumpClientPackets = false;

    [PropertyDisplay("录制日志格式")]
    public ReplayLogFormat WorldLogFormat = ReplayLogFormat.BinaryCompressed;
    public string ReplayFolder = "";
}
