# 游戏内 ImGui / DirectX 11 绘制

设置入口：`/prpr → 显示与绘制 → 游戏内绘制 → 绘制模式`。旧配置默认使用 ImGui；
选择和 DX 遮挡选项保存在 `MasterPlayer.json`。切换在下一绘制帧生效，已登记的持久
图形、实体绑定和连线无需重新注册；雷达悬浮窗继续使用原来的独立绘制。

## 实现和宿主边界

- `MasterPlayerPlugin.WorldDrawing` 持有独立的 PR `DrawManager`、`DrawDispatcher`
  和 `DxRendererAdapter`。雷达世界图层、脚本 Draw 门面和示例处理器统一向此实例登记。
- 所有元素使用 `RendererType.DirectX` 进入本地适配器，适配器根据本插件设置选择
  PR 的 `ImGuiRenderer` 或内置 `DxRenderer`，不读取或修改 PR 的默认绘制模式。
- PR 1.5.10.4 只公开全局后端的安装入口。独立适配器通过有明确签名的
  `UnsafeAccessor` 调用其 `Attach(IDrawRenderer)`；定向测试以实际安装的 PR DLL
  执行该调用。宿主更新后须重跑验证，避免改变其他插件的 DX 后端。
- `WorldDrawing` 随运行组件创建和释放；进入 PvP 先释放消费者、分发器订阅、
  注册表和 GPU 资源，离开后重建。切回 ImGui 的 GPU 释放延后到下一绘制帧，
  防止当前帧 ImGui DrawList 引用的纹理被提前释放。
- 禁用游戏内绘制时仍由各消费者沿用已有的登记/清理规则；智能走位和回放执行门控不变。

## DX 管线

来源为用户提供的 PromeDrawPlus.dll，ILSpy 反编译记录及作者见
`ThirdPartyNotices/PromeDrawPlus.NOTICE`。内置原有的实体形状网格、像素宽描边、
D3D11 deferred context、透明批次排序及合并、离屏颜色/深度目标和最终 ImGui 合成。
环形按内外轮廓连接；多边形和大角度扇形使用宿主三角化。
不依赖 PromeDrawPlus 插件或其图片、开发演示和实验性表面贴纸功能。

相机和游戏深度从与编译引用一致的 FFCS 读取。开启场景遮挡时，根据深度还原
世界位置，与图形到相机的距离比较；被遮挡透明度默认为 0。
UI 遮挡沿用原插件的游戏 backbuffer alpha 掩码，并显示不支持的尺寸/分辨率
缩放状态。未取得深度或掩码时，对应遮挡暂不可用，设置面板显示实际状态。

DX 初始化或提交失败后由本地分发器使用 ImGui，并在模式状态中显示原因；
切回 ImGui 后再选 DX 可重新初始化。分辨率和设备变化时重建相关资源。
TerraFX 编译引用与 Dalamud 匹配，发布 SDK 构建固定使用 `10.0.26100.6`，
运行时复用宿主共享程序集，不随插件复制一份。

## 验证

### 地面贴合

DX 设置中的「范围贴合地面」默认开启，仅调整显示，危险区、安全区的 XZ 判定和寻路数据不变。
水平范围按原始碰撞地表三角面裁剪填充，轮廓线独立裁剪并沿每个面插值高度；
不在台阶、洞口或断崖之间补斜面。显式带高度变化的三维图形保留原坐标。
贴地顶点额外抬高 0.015 码以缓解共面深度冲突，原高度偏移设置继续叠加。

`GroundSceneController` 在 Framework 线程复用 `NativeTerrainSceneSource` 的分帧采集和模型缓存实现，
后台解码、变换并构建绘制专用的原始支撑面索引。它独立于寻路地形的启停，
正式机制场地和关闭地形寻路时仍可使用，不使用经过腐蚀的通行图。
只保留朝上的支撑面，排除竖直面、背面及显式禁行材质；相接的面允许至多 0.65 码台阶，
同时处理不同细分模型之间的 T 接缝。范围锚点选择相近的支撑层；
锚点位于洞口时从自身轮廓补找，同一连通层内重叠表面优先选接近锚点高度的面。

有贴地绘制需求时采集玩家周围 40 码，活动地形每 0.5 秒复查，水平移动超过 4 码更新窗口；
旧窗口仍覆盖当前位置时可在换窗口期间保留。场景几何变化立即失效旧面，
换区、深宫楼层/布局、显著高度变化、关闭和卸载撤销任务，并按代际丢弃迟到结果。
关闭/换层时清理未完成的程序模型采集，避免复用相同 Scene 地址的上一层数据。
不访问实时地形的回放继续使用录像坐标；PvP 沿用全插件运行组件释放契约。

`GroundMeshCache` 按范围 ID、锚点和完整轮廓复用裁剪结果，颜色/透明度、相机变化不触发重新裁剪；
实体位置或地表变化时失效。缓存上限为 64 项、200000 个位置顶点，边线近裁剪在绘制帧执行。
地表尚未取得、锚点找不到支撑或范围超过采集窗口时，只绘制已确认的贴地部分，
不补回水平面；面板显示采集/构建错误与贴地范围数量，可关闭贴地恢复原平面绘制。

碰撞地表与游戏视觉模型可能不完全相同，细小装饰、活动平台、互相连通又上下重叠的复杂结构，
以及首次采集的等待时间仍需实机确认；本实现不承诺修改后的范围能贴合所有视觉表面。

```powershell
dotnet build MasterPlayer.csproj -p:CI=true
dotnet run --project tests/WorldDrawing/MasterPlayer.WorldDrawing.Tests.csproj -p:CI=true
dotnet run --project tests/WorldDrawing/MasterPlayer.WorldDrawing.Tests.csproj -p:CI=true -- --ground-only --ground-benchmark
dotnet run --project tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- --filter="World drawing settings"
```

测试链接生产渲染代码并引用实际 PR/Dalamud DLL，验证独立后端安装、圆环内孔、
大角度扇形、描边近裁剪、D3D11 WARP 创建六个 Shader、真实离屏绘制及像素读取、
透明度还原、模拟 UI 掩码和场景深度的遮挡像素、被遮挡透明度、分辨率变化和重复释放。
配置回归覆盖旧配置默认值、DX 选项保存读取及非法值处理。游戏相机、真实场景深度、
游戏 UI 遮挡、PvP 切换及热重载的实际表现仍须实机验收。
贴地专项覆盖坡面填充/边线与投影面积、内部隆起、圆环内孔、大角度扇形、上下层、
断崖/缺失支撑、台阶不补面、T 接缝、洞口锚点、材质/墙面/镜像、巨大三角面窗口裁剪、
缓存失效和后台生命周期，并将贴地网格提交 D3D11 WARP 后读取像素验证合成。
