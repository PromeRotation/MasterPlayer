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

```powershell
dotnet build MasterPlayer.csproj -p:CI=true
dotnet run --project tests/WorldDrawing/MasterPlayer.WorldDrawing.Tests.csproj -p:CI=true
dotnet run --project tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- --filter="World drawing settings"
```

测试链接生产渲染代码并引用实际 PR/Dalamud DLL，验证独立后端安装、圆环内孔、
大角度扇形、描边近裁剪、D3D11 WARP 创建六个 Shader、真实离屏绘制及像素读取、
透明度还原、模拟 UI 掩码和场景深度的遮挡像素、被遮挡透明度、分辨率变化和重复释放。
配置回归覆盖旧配置默认值、DX 选项保存读取及非法值处理。游戏相机、真实场景深度、
游戏 UI 遮挡、PvP 切换及热重载的实际表现仍须实机验收。
