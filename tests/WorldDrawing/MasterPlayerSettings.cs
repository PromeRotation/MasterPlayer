namespace MasterPlayer.Settings;

// 原生离屏测试不加载游戏配置；渲染器、网格、分发接入均链接实际生产代码。
public sealed class MasterPlayerSettings
{
    public WorldDrawingMode WorldDrawingMode;
    public bool DxSceneOcclusion;
    public bool DxGroundProjection = true;
    public bool DxUiMask;
    public float DxOccludedAlpha;
    public float DxHeightOffset;
}
