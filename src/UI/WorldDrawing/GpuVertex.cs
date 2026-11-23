using System.Numerics;

namespace MasterPlayer.Drawing;

internal struct GpuVertex
{
    internal Vector3 Position;

    private float padding0;

    internal Vector3 Aux;

    private float padding1;

    internal Vector4 Color;

    internal Vector4 Data;

    internal GpuVertex(Vector3 position, Vector3 aux, Vector4 color, Vector4 data)
    {
        Position = position;
        this.padding0 = 0f;
        Aux = aux;
        this.padding1 = 0f;
        Color = color;
        Data = data;
    }
}
