namespace MasterPlayer.Spatial.Geometry;

/// <summary>Geometry 公共库支持的几何图形类型。</summary>
public enum GeometryKind
{
    /// <summary>圆形。</summary>
    Circle,

    /// <summary>月环/中空圆。</summary>
    Donut,

    /// <summary>扇形。</summary>
    Fan,

    /// <summary>以中心点定位的矩形。</summary>
    Rectangle,

    /// <summary>以后边中心为起点向前延伸的射线矩形。</summary>
    RayRectangle,

    /// <summary>局部坐标点构造的多边形。</summary>
    LocalPolygon,

    /// <summary>世界坐标点构造的多边形。</summary>
    WorldPolygon
}
