using System.Numerics;

namespace MasterPlayer.Drawing;

internal readonly record struct RenderBatch(
    DrawPipeline Pipeline,
    GpuVertex[] Vertices,
    nint Texture = 0,
    Vector4 Parameters0 = default,
    Vector4 Parameters1 = default,
    Vector4 Parameters2 = default)
{
    internal bool IsTransparent
        => Pipeline == DrawPipeline.Stroke || Vertices.Any(vertex => vertex.Color.W < 0.999f);
}
