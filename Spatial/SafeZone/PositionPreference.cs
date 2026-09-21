namespace MasterPlayer.Spatial.SafeZone;

/// <summary>
/// 目标区内目标点选取偏好。
/// <para>Solver 在已选定的 <see cref="AreaKind.Goal"/> 区域内根据该偏好挑选具体落点。</para>
/// </summary>
public enum PositionPreference
{
    /// <summary>使用区域几何中心。</summary>
    Center,

    /// <summary>选择离区域边界最远的内点。</summary>
    Deepest,

    /// <summary>选择距离玩家当前位置最近的区域内点。</summary>
    Nearest,

    /// <summary>外部脚本自行决定具体目标点。</summary>
    Custom
}
