using PromeRotation.Spatial.Drawing;

internal sealed class TestRenderer : IDrawRenderer
{
    public DrawRendererKind Kind => DrawRendererKind.DirectX;
    public bool IsAvailable { get; set; } = true;
    public bool Supports(DrawRenderItem item) => true;
    public void RenderFrame(IReadOnlyList<DrawRenderItem> items) { }
}
