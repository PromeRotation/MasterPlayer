using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using MasterPlayer.Drawing;
using MasterPlayer.Settings;
using PromeRotation.Spatial.Drawing;
using PromeRotation.Spatial.Drawing.Renderers;
using PromeRotation.Spatial.Geometry;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

internal static class Program
{
    private const BindingFlags PRIVATE_INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;

    private static unsafe void Main()
    {
        var adapter = new DxRendererAdapter();
        var testRenderer = new TestRenderer();
        typeof(WorldDrawing).GetMethod("AttachBackend", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [adapter, testRenderer]);
        Require(adapter.IsAvailable, "独立 DX 适配器未安装后端");
        testRenderer.IsAvailable = false;
        Require(!adapter.IsAvailable, "后端可用性未传递");
        Console.WriteLine("PASS installed PR: independent adapter binding and availability");

        var batches = new List<RenderBatch>();
        var fill = new Vector4(1, 0, 0, 0.5f);
        var circle = new CircleGeometry(new Vector3(0, 0.5f, 0), 0.5f);
        MeshBuilder.AddGeometry(batches, new("circle", circle, new DrawStyle(fill, null)), new(0, 0, 0, -1), 0);
        Require(batches.Count == 1 && batches[0].IsTransparent, "圆填充网格未生成");
        var ring = new List<RenderBatch>();
        MeshBuilder.AddGeometry(ring, new("ring", new DonutGeometry(Vector3.Zero, 1, 2), new DrawStyle(fill, null)), Vector4.Zero, 0);
        Require(ring.Count == 1, "圆环填充网格未生成");
        foreach (var vertex in ring[0].Vertices)
            Require(vertex.Position.Length() >= 0.99f, "圆环内孔被填充");
        var fan = new List<RenderBatch>();
        MeshBuilder.AddGeometry(fan, new("fan", new FanGeometry(Vector3.Zero, 0, 2, MathF.PI * 1.5f), new DrawStyle(fill, null)), Vector4.Zero, 0.02f);
        Require(fan.Count == 1 && fan[0].Vertices.All(v => MathF.Abs(v.Position.Y - 0.02f) < 0.0001f),
            "大角度扇形三角化或高度偏移失败");
        var clipped = new List<RenderBatch>();
        MeshBuilder.AddGeometry(clipped, new("clipped", circle, new DrawStyle(null, Vector4.One)), new(0, 0, 0, 1), 0);
        Require(clipped.Count == 0, "近裁剪面背后的描边未剔除");
        Console.WriteLine("PASS meshes: circle, donut hole, reflex fan, height and near-plane clipping");

        ID3D11Device* device = null;
        ID3D11DeviceContext* immediate = null;
        Check(DirectX.D3D11CreateDevice(null, D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_WARP, HMODULE.NULL,
            0, null, 0, 7, &device, null, &immediate));
        ID3D11DeviceContext* deferred = null;
        Check(device->CreateDeferredContext(0, &deferred));
        var settings = new MasterPlayerSettings();
        using var renderer = new DxRenderer(settings);
        SetPointer(renderer, "device", device, typeof(ID3D11Device*));
        SetPointer(renderer, "immediate", immediate, typeof(ID3D11DeviceContext*));
        SetPointer(renderer, "deferred", deferred, typeof(ID3D11DeviceContext*));
        Invoke(renderer, "CreatePipelines");
        Invoke(renderer, "CreateFixedResources");
        Require((bool)Invoke(renderer, "EnsureTargets", 64u, 64u)!, "离屏渲染目标创建失败");
        Require((bool)Invoke(renderer, "EnsureVertexBuffer", (uint)(batches[0].Vertices.Length * sizeof(GpuVertex)))!, "顶点缓冲创建失败");

        var projection = new Matrix4x4(1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 1);
        var cameraType = typeof(DxRenderer).GetNestedType("CameraFrame", BindingFlags.NonPublic)!;
        var camera = Activator.CreateInstance(cameraType, projection, projection, Vector3.UnitY, Vector3.UnitX, new Vector4(0, 0, 0, -1), false)!;
        var noView = Pointer.Box(null, typeof(ID3D11ShaderResourceView*));
        Invoke(renderer, "RecordFrame", batches, camera, 64u, 64u, noView, 0u, 0u,
            Pointer.Box(null, typeof(ID3D11Texture2D*)), noView);

        var texture = (ID3D11Texture2D*)Pointer.Unbox(typeof(DxRenderer).GetField("resolvedTexture", PRIVATE_INSTANCE)!.GetValue(renderer)!);
        D3D11_TEXTURE2D_DESC description;
        texture->GetDesc(&description);
        description.Usage = D3D11_USAGE.D3D11_USAGE_STAGING;
        description.BindFlags = 0;
        description.CPUAccessFlags = 0x20000;
        ID3D11Texture2D* staging = null;
        Check(device->CreateTexture2D(&description, null, &staging));
        try
        {
            immediate->CopyResource((ID3D11Resource*)staging, (ID3D11Resource*)texture);
            D3D11_MAPPED_SUBRESOURCE mapped;
            Check(immediate->Map((ID3D11Resource*)staging, 0, D3D11_MAP.D3D11_MAP_READ, 0, &mapped));
            try
            {
                var pixel = (byte*)mapped.pData + 32 * mapped.RowPitch + 32 * 4;
                Require(pixel[0] > 245 && pixel[1] < 5 && pixel[2] < 5 && pixel[3] is >= 125 and <= 130,
                    $"离屏合成像素不符：{pixel[0]},{pixel[1]},{pixel[2]},{pixel[3]}");
                var corner = (byte*)mapped.pData;
                Require(corner[3] == 0, "图形之外应保持透明");
            }
            finally
            {
                immediate->Unmap((ID3D11Resource*)staging, 0);
            }
        }
        finally
        {
            staging->Release();
        }

        // 合成遮挡使用真正的 GPU SRV，以固定输入核验 Shader 的效果。
        ID3D11Texture2D* maskSource = null;
        var maskPixels = Enumerable.Repeat(0xFF000000u, 64 * 64).ToArray();
        description.Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT;
        description.BindFlags = 8;
        description.CPUAccessFlags = 0;
        fixed (uint* pixels = maskPixels)
        {
            var initial = new D3D11_SUBRESOURCE_DATA { pSysMem = pixels, SysMemPitch = 64 * 4 };
            Check(device->CreateTexture2D(&description, &initial, &maskSource));
        }
        try
        {
            Invoke(renderer, "EnsureUiMaskTexture", description);
            var maskView = typeof(DxRenderer).GetField("uiMaskView", PRIVATE_INSTANCE)!.GetValue(renderer)!;
            typeof(DxRenderer).GetField("uiMaskActiveThisFrame", PRIVATE_INSTANCE)!.SetValue(renderer, true);
            Invoke(renderer, "RecordFrame", batches, camera, 64u, 64u, noView, 0u, 0u,
                Pointer.Box(maskSource, typeof(ID3D11Texture2D*)), maskView);
            Require(ReadCenterAlpha(device, immediate, texture) == 0, "不透明 UI 遮挡未生效");
            typeof(DxRenderer).GetField("uiMaskActiveThisFrame", PRIVATE_INSTANCE)!.SetValue(renderer, false);
        }
        finally
        {
            maskSource->Release();
        }

        ID3D11Texture2D* sceneDepth = null;
        ID3D11ShaderResourceView* sceneDepthView = null;
        description.Format = DXGI_FORMAT.DXGI_FORMAT_R32_FLOAT;
        var depthPixels = Enumerable.Repeat(0.8f, 64 * 64).ToArray();
        fixed (float* pixels = depthPixels)
        {
            var initial = new D3D11_SUBRESOURCE_DATA { pSysMem = pixels, SysMemPitch = 64 * 4 };
            Check(device->CreateTexture2D(&description, &initial, &sceneDepth));
        }
        try
        {
            Check(device->CreateShaderResourceView((ID3D11Resource*)sceneDepth, null, &sceneDepthView));
            settings.DxSceneOcclusion = true;
            typeof(DxRenderer).GetProperty("SceneDepthAvailable", PRIVATE_INSTANCE)!.SetValue(renderer, true);
            var boxedDepth = Pointer.Box(sceneDepthView, typeof(ID3D11ShaderResourceView*));
            Invoke(renderer, "RecordFrame", batches, camera, 64u, 64u, boxedDepth, 64u, 64u,
                Pointer.Box(null, typeof(ID3D11Texture2D*)), noView);
            Require(ReadCenterAlpha(device, immediate, texture) == 0, "场景深度遮挡未隐藏图形");
            settings.DxOccludedAlpha = 0.25f;
            Invoke(renderer, "RecordFrame", batches, camera, 64u, 64u, boxedDepth, 64u, 64u,
                Pointer.Box(null, typeof(ID3D11Texture2D*)), noView);
            Require(ReadCenterAlpha(device, immediate, texture) is >= 30 and <= 33, "被遮挡透明度未正确应用");
        }
        finally
        {
            if (sceneDepthView != null)
                sceneDepthView->Release();
            sceneDepth->Release();
        }
        Require((bool)Invoke(renderer, "EnsureTargets", 128u, 96u)!, "分辨率变化后重建目标失败");
        renderer.Reset();
        renderer.Dispose();
        Console.WriteLine("PASS D3D11 WARP: six shaders, rendering, alpha resolve, UI mask, scene occlusion, resize and repeated disposal");
    }

    private static unsafe byte ReadCenterAlpha(ID3D11Device* device, ID3D11DeviceContext* immediate, ID3D11Texture2D* texture)
    {
        D3D11_TEXTURE2D_DESC description;
        texture->GetDesc(&description);
        description.Usage = D3D11_USAGE.D3D11_USAGE_STAGING;
        description.BindFlags = 0;
        description.CPUAccessFlags = 0x20000;
        ID3D11Texture2D* staging = null;
        Check(device->CreateTexture2D(&description, null, &staging));
        try
        {
            immediate->CopyResource((ID3D11Resource*)staging, (ID3D11Resource*)texture);
            D3D11_MAPPED_SUBRESOURCE mapped;
            Check(immediate->Map((ID3D11Resource*)staging, 0, D3D11_MAP.D3D11_MAP_READ, 0, &mapped));
            try
            {
                return *((byte*)mapped.pData + 32 * mapped.RowPitch + 32 * 4 + 3);
            }
            finally
            {
                immediate->Unmap((ID3D11Resource*)staging, 0);
            }
        }
        finally
        {
            staging->Release();
        }
    }

    private static object? Invoke(object target, string method, params object?[] args)
        => target.GetType().GetMethod(method, PRIVATE_INSTANCE)!.Invoke(target, args);

    private static unsafe void SetPointer(object target, string field, void* value, Type type)
        => target.GetType().GetField(field, PRIVATE_INSTANCE)!.SetValue(target, Pointer.Box(value, type));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Check(HRESULT result) => Marshal.ThrowExceptionForHR(result);
}
