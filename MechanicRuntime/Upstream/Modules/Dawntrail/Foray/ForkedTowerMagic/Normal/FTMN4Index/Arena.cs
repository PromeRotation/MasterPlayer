// 目录战异形场地几何（2026-08-07 用户实测确认，爆弹怪三点硬验证）：
// 坐标系 BossMod WPos(X,Z)，场地中心 (0,-628)，Z 增=南。
// 内圈即死区：正六边形边长 6（顶点 (±6,0)/(±3,±5.196)），以 DifferenceShapes 挖洞实现
// （2026-08-07 用户实测修正：雷达图挖空显示，进入坠落即死；参考 DD99EminentGrief/Q1FinalVerse 中心塌陷挖洞先例）。
// 中圈常驻区：正六边形边长 15（顶点 (±15,0)/(±7.5,±12.99)）。
// 外圈：6 个边长 15 的正方形挂在六边形六条边外侧（内边=六边形边，沿外法线外延 15）；
// 场地周期（2026-08-07 用户实测修正：元素控制读条完毕生成 / 元素整合读条完毕回收）：
// 初始仅南/东北/西北 3 个 → 元素控制（48394）读条结束 → 全部 6 个 → 元素整合（48401）读条结束 → 回收额外 3 个。
// 硬验证：正方形外缘中点 = 被召唤的爆弹怪（4B60）生成点 南(0,-600)/东北(24.25,-642)/西北(-24.25,-642)，
// 三场回放两轮生成坐标完全一致（外缘中点世界坐标：南 (0,-600.01)、东北 (24.24,-641.99)、西北 (-24.24,-641.99)）。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN4Index;

static class IndexArena
{
    public static readonly WPos Center = new(0f, -628f);

    // flat-top 正六边形顶点（外接圆半径=边长）：顶点角度 30°+i*60°（BossMod 角度 0=南，方向(sinθ,cosθ)）。
    // 例（edge=15）：i0 东南(7.5,-615.01) i1 东(15,-628) i2 东北(7.5,-640.99) i3 西北(-7.5,-640.99) i4 西(-15,-628) i5 西南(-7.5,-615.01)
    public static WPos[] Hexagon(float edge)
    {
        var verts = new WPos[6];
        for (var i = 0; i < 6; ++i)
        {
            verts[i] = Center + (30f + 60f * i).Degrees().ToDirection() * edge;
        }
        return verts;
    }

    // 挂在六边形一条边外侧的正方形：内边=a-b（六边形边），沿外法线（中心→边中点方向）外延 15
    static PolygonCustom SquareOnEdge(WPos a, WPos b)
    {
        var mid = new WPos((a.X + b.X) * 0.5f, (a.Z + b.Z) * 0.5f);
        var normal = (mid - Center).Normalized();
        return new([a, b, b + normal * 15f, a + normal * 15f]);
    }

    private static readonly Shape DeathHole = new PolygonCustom(Hexagon(6f)); // 内圈即死区挖洞（边长 6 正六边形，与 Hexagon 顶点生成器一致）
    private static readonly WPos[] Hex15 = Hexagon(15f); // 中圈常驻六边形（边长 15），顶点顺序 0东南 1东 2东北 3西北 4西 5西南
    private static readonly Shape Hex15Shape = new PolygonCustom(Hex15);
    private static readonly PolygonCustom SquareS = SquareOnEdge(Hex15[5], Hex15[0]); // 南
    private static readonly PolygonCustom SquareSE = SquareOnEdge(Hex15[0], Hex15[1]); // 东南
    private static readonly PolygonCustom SquareNE = SquareOnEdge(Hex15[1], Hex15[2]); // 东北
    private static readonly PolygonCustom SquareN = SquareOnEdge(Hex15[2], Hex15[3]); // 北
    private static readonly PolygonCustom SquareNW = SquareOnEdge(Hex15[3], Hex15[4]); // 西北
    private static readonly PolygonCustom SquareSW = SquareOnEdge(Hex15[4], Hex15[5]); // 西南

    public static readonly Shape[] InitialShapes = [Hex15Shape, SquareS, SquareNE, SquareNW]; // 初始：南/东北/西北 3 平台
    public static readonly Shape[] FullShapes = [Hex15Shape, SquareS, SquareSE, SquareNE, SquareN, SquareNW, SquareSW]; // 元素阶段：全部 6 平台
    public static readonly Shape[] ExtraShapes = [SquareSE, SquareSW, SquareN]; // 元素整合读条期间禁入的额外 3 平台（东南/西南/北）
    // 雷达中心固定 (0,-628)（2026-08-07 用户要求：切换 bounds 时中心不跳变；真实场地中心由爆弹怪 R28 验证，
    // 初始 3 平台包围盒中心本为 (0,-624.25)，用 CenterOverride 统一为 (0,-628)，多边形顶点同步平移）
    public static readonly ArenaBoundsCustom InitialBounds = new(InitialShapes, [DeathHole], CenterOverride: new WPos(0f, -628f)); // 挖洞：内圈六边形即死区
    public static readonly ArenaBoundsCustom FullBounds = new(FullShapes, [DeathHole], CenterOverride: new WPos(0f, -628f));
}
