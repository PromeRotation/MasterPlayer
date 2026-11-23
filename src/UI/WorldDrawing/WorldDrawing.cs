using System.Runtime.CompilerServices;
using MasterPlayer.Settings;
using PromeRotation.Spatial.Drawing;
using PromeRotation.Spatial.Drawing.Renderers;

namespace MasterPlayer.Drawing;

/// <summary>独立注册表和分发器；只切换本插件的绘制，不占用 PR 的全局 DX 后端。</summary>
internal sealed class WorldDrawing : IDrawRenderer, IDisposable
{
    private readonly MasterPlayerSettings settings;
    private readonly ImGuiRenderer imGui = new();
    private readonly DxRenderer dx;
    private readonly DrawDispatcher dispatcher;
    private bool renderedDirectX;
    private bool disposed;

    internal DrawManager Manager { get; } = new();
    public DrawRendererKind Kind => DrawRendererKind.DirectX;
    public bool IsAvailable => settings.WorldDrawingMode != WorldDrawingMode.DirectX || dx.IsAvailable;
    internal string Status => settings.WorldDrawingMode == WorldDrawingMode.DirectX
        ? dx.Status + (dx.IsAvailable ? "" : "；当前使用 ImGui 绘制")
        : "ImGui 绘制";
    internal string UiMaskStatus => dx.UiMaskStatus;

    internal WorldDrawing(MasterPlayerSettings settings)
    {
        this.settings = settings;
        dx = new DxRenderer(settings);
        var adapter = new DxRendererAdapter();
        try
        {
            AttachBackend(adapter, this);
            dispatcher = new DrawDispatcher(Manager, imGui, adapter);
        }
        catch
        {
            Manager.Dispose();
            dx.Dispose();
            throw;
        }
    }

    // PR 仅公开全局后端的安装入口。对独立适配器调用相同的 Attach，
    // 保留宿主的绑定、时序和连线实现，也避免替换其他插件注册的后端。
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Attach")]
    private static extern void AttachBackend(DxRendererAdapter adapter, IDrawRenderer renderer);

    public bool Supports(DrawRenderItem item) => imGui.Supports(item);

    public void RenderFrame(IReadOnlyList<DrawRenderItem> items)
    {
        if (disposed)
            return;
        if (settings.WorldDrawingMode == WorldDrawingMode.DirectX)
        {
            renderedDirectX = true;
            dx.RenderFrame(items);
        }
        else
        {
            // 上一帧的 ImGui DrawList 已完成消费，此时才能释放其中引用的 DX 纹理。
            if (renderedDirectX)
            {
                dx.Reset();
                renderedDirectX = false;
            }
            imGui.RenderFrame(items);
        }
    }

    internal void SetMode(WorldDrawingMode mode)
    {
        settings.WorldDrawingMode = mode;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        dispatcher.Dispose();
        Manager.Dispose();
        dx.Dispose();
    }
}
