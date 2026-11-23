using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MasterPlayer.Settings;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using PromeRotation.Spatial.Drawing;
using PromeRotation.Spatial.Geometry;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace MasterPlayer.Drawing;

internal sealed class DxRenderer : IDrawRenderer, IDisposable
{
    private struct FrameConstants
    {
        internal Matrix4x4 ViewProjection;

        internal Matrix4x4 InverseViewProjection;

        internal Vector4 CameraPosition;

        internal Vector4 ViewportAndTolerance;

        internal Vector4 Effects;

        internal Vector4 DepthInfo;

        internal Vector4 ClipRect;

        internal Vector4 Parameters0;

        internal Vector4 Parameters1;

        internal Vector4 Parameters2;
    }

    private readonly record struct CameraFrame(Matrix4x4 ViewProjection, Matrix4x4 InverseViewProjection, Vector3 Position, Vector3 Right, Vector4 NearPlane, bool ReversedDepth);

    private readonly record struct SortedBatch(RenderBatch Batch, float DistanceSquared, int Order);

    private readonly MasterPlayerSettings settings;

    private nint deviceHandle;

    private unsafe ID3D11Device* device;

    private unsafe ID3D11DeviceContext* immediate;

    private unsafe ID3D11DeviceContext* deferred;

    private unsafe ID3D11Texture2D* colorTexture;

    private unsafe ID3D11RenderTargetView* colorTarget;

    private unsafe ID3D11ShaderResourceView* colorView;

    private unsafe ID3D11Texture2D* resolvedTexture;

    private unsafe ID3D11RenderTargetView* resolvedTarget;

    private unsafe ID3D11ShaderResourceView* resolvedView;

    private unsafe ID3D11Texture2D* uiMaskTexture;

    private unsafe ID3D11ShaderResourceView* uiMaskView;

    private uint uiMaskWidth;

    private uint uiMaskHeight;

    private DXGI_FORMAT uiMaskFormat;

    private unsafe ID3D11Texture2D* depthTexture;

    private unsafe ID3D11DepthStencilView* depthTarget;

    private uint targetWidth;

    private uint targetHeight;

    private unsafe ID3D11Buffer* vertexBuffer;

    private uint vertexBufferBytes;

    private unsafe ID3D11Buffer* constantBuffer;

    private unsafe ID3D11InputLayout* inputLayout;

    private unsafe ID3D11VertexShader* solidVertexShader;

    private unsafe ID3D11VertexShader* strokeVertexShader;

    private unsafe ID3D11VertexShader* resolveVertexShader;

    private unsafe ID3D11PixelShader* solidPixelShader;

    private unsafe ID3D11PixelShader* strokePixelShader;

    private unsafe ID3D11PixelShader* resolvePixelShader;

    private unsafe ID3D11SamplerState* sampler;

    private unsafe ID3D11RasterizerState* rasterizer;

    private unsafe ID3D11DepthStencilState* depthLess;

    private unsafe ID3D11DepthStencilState* depthGreater;

    private unsafe ID3D11DepthStencilState* depthLessNoWrite;

    private unsafe ID3D11DepthStencilState* depthGreaterNoWrite;

    private unsafe ID3D11DepthStencilState* depthOff;

    private unsafe ID3D11BlendState* alphaBlend;

    private bool initializationFailed;

    private bool uiMaskActiveThisFrame;

    private bool disposed;

    private bool currentCameraReversedDepth;

    public DrawRendererKind Kind => DrawRendererKind.DirectX;

    public bool IsAvailable => !this.disposed && !this.initializationFailed && Svc.PluginInterface.UiBuilder.DeviceHandle != IntPtr.Zero;

    internal bool SceneDepthAvailable { get; private set; }

    internal string Status { get; private set; } = "等待首个绘制帧";

    internal string RenderStats { get; private set; } = "尚无绘制统计";

    internal string UiMaskStatus { get; private set; } = "等待游戏画面";

    internal DxRenderer(MasterPlayerSettings settings)
    {
        this.settings = settings;
    }

    internal void Reset()
    {
        ReleaseDevice();
        initializationFailed = false;
        Status = "等待首个绘制帧";
    }

    public bool Supports(DrawRenderItem item)
    {
        IGeometryShape geometry = item.Geometry;
        if (geometry is CircleGeometry || geometry is DonutGeometry || geometry is FanGeometry || geometry is RectangleGeometry || geometry is RayRectangleGeometry || geometry is LocalPolygonGeometry || geometry is WorldPolygonGeometry)
        {
            return true;
        }
        return false;
    }

    public unsafe void RenderFrame(IReadOnlyList<DrawRenderItem> items)
    {
        if (items.Count == 0)
            return;
        if (!TryGetCamera(out var frame))
        {
            Status = "当前帧没有可用相机";
            return;
        }
        ImGuiViewportPtr mainViewport = ImGuiHelpers.MainViewport;
        uint num = (uint)MathF.Max(1f, MathF.Round(mainViewport.Size.X));
        uint num2 = (uint)MathF.Max(1f, MathF.Round(mainViewport.Size.Y));
        if (!EnsureDevice() || !EnsureTargets(num, num2))
        {
            return;
        }
        List<RenderBatch> list = new List<RenderBatch>(items.Count * 3 + 5);
        foreach (DrawRenderItem item in items)
        {
            MeshBuilder.AddGeometry(list, item, frame.NearPlane, this.settings.DxHeightOffset);
        }
        this.uiMaskActiveThisFrame = TryPrepareUiMask(num, num2, out var source, out var view);
        if (list.Count == 0)
        {
            SceneDepthAvailable = false;
            RenderStats = "Draw Call=0；Batch=0；顶点=0";
            Status = "DX11 就绪";
            return;
        }
        int count = list.Count;
        list = PrepareBatches(list, frame.Position);
        int value = list.Sum((RenderBatch batch) => batch.Vertices.Length);
        RenderStats = $"Draw Call={list.Count + 1}（含 1 次合成）；Batch={count}→{list.Count}；顶点={value}";
        bool flag = this.settings.DxSceneOcclusion;
        ID3D11ShaderResourceView* view2 = null;
        uint width = 0u;
        uint height = 0u;
        SceneDepthAvailable = flag && TryGetSceneDepth(num, num2, out view2, out width, out height);
        int num3 = list.Max((RenderBatch batch) => batch.Vertices.Length);
        if (EnsureVertexBuffer((uint)(num3 * sizeof(GpuVertex))))
        {
            try
            {
                RecordFrame(list, in frame, num, num2, view2, width, height, source, view);
            }
            catch (Exception ex)
            {
                this.initializationFailed = true;
                Status = "DX11 提交失败：" + ex.Message;
                throw;
            }
            ImGui.GetBackgroundDrawList().AddImage(new ImTextureID(this.resolvedView), mainViewport.Pos, mainViewport.Pos + mainViewport.Size);
            Status = ((!flag) ? "DX11 就绪" : (SceneDepthAvailable ? "DX11 就绪，场景深度可用" : "DX11 就绪；当前帧未取得场景深度"));
        }
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.disposed = true;
            ReleaseDevice();
        }
    }

    private unsafe bool EnsureDevice()
    {
        nint deviceHandle = Svc.PluginInterface.UiBuilder.DeviceHandle;
        if (deviceHandle == IntPtr.Zero)
        {
            Status = "Dalamud 尚未提供 D3D11 Device";
            return false;
        }
        if (this.device != null && deviceHandle == this.deviceHandle)
        {
            return true;
        }
        ReleaseDevice();
        this.initializationFailed = false;
        this.deviceHandle = deviceHandle;
        try
        {
            ID3D11Device* device = null;
            Guid iID_ID3D11Device = IID.IID_ID3D11Device;
            Check(((IUnknown*)deviceHandle)->QueryInterface(&iID_ID3D11Device, (void**)(&device)));
            this.device = device;
            ID3D11DeviceContext* immediate = null;
            ID3D11DeviceContext* deferred = null;
            this.device->GetImmediateContext(&immediate);
            this.immediate = immediate;
            Check(this.device->CreateDeferredContext(0u, &deferred));
            this.deferred = deferred;
            CreatePipelines();
            CreateFixedResources();
            return true;
        }
        catch (Exception ex)
        {
            this.initializationFailed = true;
            Status = "DX11 初始化失败：" + ex.Message;
            Svc.Log.Error(ex, "[PromeRadar] DX11 初始化失败。");
            ReleaseDevice();
            return false;
        }
    }

    private unsafe bool EnsureTargets(uint width, uint height)
    {
        if (this.colorTexture != null && this.targetWidth == width && this.targetHeight == height)
        {
            return true;
        }
        ReleaseTargets();
        try
        {
            D3D11_TEXTURE2D_DESC d3D11_TEXTURE2D_DESC = new D3D11_TEXTURE2D_DESC
            {
                Width = width,
                Height = height,
                MipLevels = 1u,
                ArraySize = 1u,
                Format = DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM,
                SampleDesc = new DXGI_SAMPLE_DESC(1u, 0u),
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = 40u
            };
            ID3D11Texture2D* ptr = null;
            ID3D11RenderTargetView* colorTarget = null;
            ID3D11ShaderResourceView* colorView = null;
            ID3D11Texture2D* ptr2 = null;
            ID3D11RenderTargetView* resolvedTarget = null;
            ID3D11ShaderResourceView* resolvedView = null;
            Check(this.device->CreateTexture2D(&d3D11_TEXTURE2D_DESC, null, &ptr));
            this.colorTexture = ptr;
            Check(this.device->CreateRenderTargetView((ID3D11Resource*)ptr, null, &colorTarget));
            this.colorTarget = colorTarget;
            Check(this.device->CreateShaderResourceView((ID3D11Resource*)ptr, null, &colorView));
            this.colorView = colorView;
            Check(this.device->CreateTexture2D(&d3D11_TEXTURE2D_DESC, null, &ptr2));
            this.resolvedTexture = ptr2;
            Check(this.device->CreateRenderTargetView((ID3D11Resource*)ptr2, null, &resolvedTarget));
            this.resolvedTarget = resolvedTarget;
            Check(this.device->CreateShaderResourceView((ID3D11Resource*)ptr2, null, &resolvedView));
            this.resolvedView = resolvedView;
            D3D11_TEXTURE2D_DESC d3D11_TEXTURE2D_DESC2 = new D3D11_TEXTURE2D_DESC
            {
                Width = width,
                Height = height,
                MipLevels = 1u,
                ArraySize = 1u,
                Format = DXGI_FORMAT.DXGI_FORMAT_D32_FLOAT,
                SampleDesc = new DXGI_SAMPLE_DESC(1u, 0u),
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = 64u
            };
            ID3D11Texture2D* ptr3 = null;
            ID3D11DepthStencilView* depthTarget = null;
            Check(this.device->CreateTexture2D(&d3D11_TEXTURE2D_DESC2, null, &ptr3));
            this.depthTexture = ptr3;
            Check(this.device->CreateDepthStencilView((ID3D11Resource*)ptr3, null, &depthTarget));
            this.depthTarget = depthTarget;
            this.targetWidth = width;
            this.targetHeight = height;
            return true;
        }
        catch (Exception ex)
        {
            this.initializationFailed = true;
            Status = $"创建 {width}x{height} 渲染目标失败：{ex.Message}";
            Svc.Log.Error(ex, "[PromeRadar] 创建渲染目标失败。");
            ReleaseTargets();
            return false;
        }
    }

    private unsafe bool TryPrepareUiMask(uint width, uint height, out ID3D11Texture2D* source, out ID3D11ShaderResourceView* view)
    {
        source = null;
        view = null;
        if (!this.settings.DxUiMask)
        {
            UiMaskStatus = "游戏 UI 遮挡已关闭";
            return false;
        }
        try
        {
            Device* ptr = Device.Instance();
            Texture* ptr2 = ((ptr != null && ptr->SwapChain != null) ? ptr->SwapChain->BackBuffer : null);
            ID3D11Texture2D* ptr3 = (ID3D11Texture2D*)((ptr2 != null) ? ptr2->D3D11Texture2D : null);
            if (ptr3 == null)
            {
                return SetUiMaskFallback("正在等待游戏画面");
            }
            D3D11_TEXTURE2D_DESC sourceDesc = default(D3D11_TEXTURE2D_DESC);
            ptr3->GetDesc(&sourceDesc);
            if (sourceDesc.Width != width || sourceDesc.Height != height || sourceDesc.SampleDesc.Count != 1)
            {
                return SetUiMaskFallback("当前画面尺寸不受支持");
            }
            RenderTargetManager* ptr4 = RenderTargetManager.Instance();
            Texture* ptr5 = ((ptr4 != null) ? ptr4->DepthStencil : null);
            if (ptr5 != null && ptr5->ActualWidth != 0 && ptr5->ActualHeight != 0 && (ptr5->ActualWidth != sourceDesc.Width || ptr5->ActualHeight != sourceDesc.Height))
            {
                return SetUiMaskFallback("当前分辨率缩放模式不受支持");
            }
            EnsureUiMaskTexture(in sourceDesc);
            source = ptr3;
            view = this.uiMaskView;
            UiMaskStatus = "游戏 UI 遮挡已启用";
            return true;
        }
        catch (Exception exception)
        {
            ReleaseUiMaskResources();
            Svc.Log.Warning(exception, "[PromeRadar] 游戏 UI 遮挡初始化失败。");
            return SetUiMaskFallback("启动失败，已回退关闭");
        }
    }

    private unsafe void EnsureUiMaskTexture(in D3D11_TEXTURE2D_DESC sourceDesc)
    {
        DXGI_FORMAT dXGI_FORMAT = NormalizeUiMaskFormat(sourceDesc.Format);
        if (this.uiMaskTexture != null && this.uiMaskWidth == sourceDesc.Width && this.uiMaskHeight == sourceDesc.Height && this.uiMaskFormat == dXGI_FORMAT)
        {
            return;
        }
        ReleaseUiMaskResources();
        D3D11_TEXTURE2D_DESC d3D11_TEXTURE2D_DESC = sourceDesc;
        d3D11_TEXTURE2D_DESC.Format = dXGI_FORMAT;
        d3D11_TEXTURE2D_DESC.Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT;
        d3D11_TEXTURE2D_DESC.BindFlags = 8u;
        d3D11_TEXTURE2D_DESC.CPUAccessFlags = 0u;
        d3D11_TEXTURE2D_DESC.MiscFlags = 0u;
        ID3D11Texture2D* ptr = null;
        ID3D11ShaderResourceView* ptr2 = null;
        try
        {
            Check(this.device->CreateTexture2D(&d3D11_TEXTURE2D_DESC, null, &ptr));
            Check(this.device->CreateShaderResourceView((ID3D11Resource*)ptr, null, &ptr2));
            this.uiMaskTexture = ptr;
            this.uiMaskView = ptr2;
            this.uiMaskWidth = d3D11_TEXTURE2D_DESC.Width;
            this.uiMaskHeight = d3D11_TEXTURE2D_DESC.Height;
            this.uiMaskFormat = d3D11_TEXTURE2D_DESC.Format;
        }
        catch
        {
            Release((IUnknown*)ptr2);
            Release((IUnknown*)ptr);
            throw;
        }
    }

    private static DXGI_FORMAT NormalizeUiMaskFormat(DXGI_FORMAT format)
    {
        DXGI_FORMAT result;
        switch (format)
        {
        case DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_TYPELESS:
        case DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:
            result = DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM;
            break;
        case DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_TYPELESS:
        case DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:
            result = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM;
            break;
        default:
            result = format;
            break;
        }
        return result;
    }

    private bool SetUiMaskFallback(string reason)
    {
        string text = "游戏 UI 遮挡暂不可用：" + reason;
        if (!string.Equals(UiMaskStatus, text, StringComparison.Ordinal))
        {
            Svc.Log.Warning("[PromeRadar] UIMask " + text);
        }
        UiMaskStatus = text;
        return false;
    }

    private unsafe void CreatePipelines()
    {
        var source = ReadShaderSource();
        ID3DBlob* solid = null;
        ID3DBlob* stroke = null;
        ID3DBlob* resolve = null;
        ID3DBlob* solidPixel = null;
        ID3DBlob* strokePixel = null;
        ID3DBlob* resolvePixel = null;
        try
        {
            solid = Compile(source, "SolidVs", "vs_5_0");
            stroke = Compile(source, "StrokeVs", "vs_5_0");
            resolve = Compile(source, "ResolveVs", "vs_5_0");
            solidPixel = Compile(source, "SolidPs", "ps_5_0");
            strokePixel = Compile(source, "StrokePs", "ps_5_0");
            resolvePixel = Compile(source, "ResolvePs", "ps_5_0");
            this.solidVertexShader = CreateVertexShader(solid);
            this.strokeVertexShader = CreateVertexShader(stroke);
            this.resolveVertexShader = CreateVertexShader(resolve);
            this.solidPixelShader = CreatePixelShader(solidPixel);
            this.strokePixelShader = CreatePixelShader(strokePixel);
            this.resolvePixelShader = CreatePixelShader(resolvePixel);
            CreateInputLayout(solid);
        }
        finally
        {
            Release((void**)&solid);
            Release((void**)&stroke);
            Release((void**)&resolve);
            Release((void**)&solidPixel);
            Release((void**)&strokePixel);
            Release((void**)&resolvePixel);
        }
    }

    private unsafe ID3D11VertexShader* CreateVertexShader(ID3DBlob* code)
    {
        ID3D11VertexShader* result = null;
        Check(this.device->CreateVertexShader(code->GetBufferPointer(), code->GetBufferSize(), null, &result));
        return result;
    }

    private unsafe ID3D11PixelShader* CreatePixelShader(ID3DBlob* code)
    {
        ID3D11PixelShader* result = null;
        Check(this.device->CreatePixelShader(code->GetBufferPointer(), code->GetBufferSize(), null, &result));
        return result;
    }

    private unsafe void CreateInputLayout(ID3DBlob* vertexCode)
    {
        ReadOnlySpan<byte> readOnlySpan = "POSITION\0"u8;
        ReadOnlySpan<byte> readOnlySpan2 = "NORMAL\0"u8;
        ReadOnlySpan<byte> readOnlySpan3 = "COLOR\0"u8;
        ReadOnlySpan<byte> readOnlySpan4 = "TEXCOORD\0"u8;
        fixed (byte* semantic = readOnlySpan)
        {
            fixed (byte* semantic2 = readOnlySpan2)
            {
                fixed (byte* semantic3 = readOnlySpan3)
                {
                    fixed (byte* semantic4 = readOnlySpan4)
                    {
                        D3D11_INPUT_ELEMENT_DESC* ptr = stackalloc D3D11_INPUT_ELEMENT_DESC[4];
                        *ptr = InputElement(semantic, DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT, 0u);
                        ptr[1] = InputElement(semantic2, DXGI_FORMAT.DXGI_FORMAT_R32G32B32_FLOAT, 16u);
                        ptr[2] = InputElement(semantic3, DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT, 32u);
                        ptr[3] = InputElement(semantic4, DXGI_FORMAT.DXGI_FORMAT_R32G32B32A32_FLOAT, 48u);
                        ID3D11InputLayout* inputLayout = null;
                        Check(this.device->CreateInputLayout(ptr, 4u, vertexCode->GetBufferPointer(), vertexCode->GetBufferSize(), &inputLayout));
                        this.inputLayout = inputLayout;
                    }
                }
            }
        }
    }

    private unsafe static D3D11_INPUT_ELEMENT_DESC InputElement(byte* semantic, DXGI_FORMAT format, uint offset)
    {
        return new D3D11_INPUT_ELEMENT_DESC
        {
            SemanticName = (sbyte*)semantic,
            Format = format,
            InputSlot = 0u,
            AlignedByteOffset = offset,
            InputSlotClass = D3D11_INPUT_CLASSIFICATION.D3D11_INPUT_PER_VERTEX_DATA
        };
    }

    private unsafe void CreateFixedResources()
    {
        D3D11_BUFFER_DESC d3D11_BUFFER_DESC = new D3D11_BUFFER_DESC
        {
            ByteWidth = (uint)sizeof(FrameConstants),
            Usage = D3D11_USAGE.D3D11_USAGE_DYNAMIC,
            BindFlags = 4u,
            CPUAccessFlags = 65536u
        };
        ID3D11Buffer* constantBuffer = null;
        Check(this.device->CreateBuffer(&d3D11_BUFFER_DESC, null, &constantBuffer));
        this.constantBuffer = constantBuffer;
        D3D11_SAMPLER_DESC d3D11_SAMPLER_DESC = new D3D11_SAMPLER_DESC
        {
            Filter = D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_LINEAR,
            AddressU = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
            AddressV = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
            AddressW = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
            ComparisonFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_NEVER,
            MinLOD = 0f,
            MaxLOD = float.MaxValue
        };
        ID3D11SamplerState* sampler = null;
        Check(this.device->CreateSamplerState(&d3D11_SAMPLER_DESC, &sampler));
        this.sampler = sampler;
        D3D11_RASTERIZER_DESC d3D11_RASTERIZER_DESC = new D3D11_RASTERIZER_DESC
        {
            FillMode = D3D11_FILL_MODE.D3D11_FILL_SOLID,
            CullMode = D3D11_CULL_MODE.D3D11_CULL_NONE,
            DepthClipEnable = 1
        };
        ID3D11RasterizerState* rasterizer = null;
        Check(this.device->CreateRasterizerState(&d3D11_RASTERIZER_DESC, &rasterizer));
        this.rasterizer = rasterizer;
        this.depthLess = CreateDepthState(D3D11_COMPARISON_FUNC.D3D11_COMPARISON_LESS_EQUAL, enabled: true, writeDepth: true);
        this.depthGreater = CreateDepthState(D3D11_COMPARISON_FUNC.D3D11_COMPARISON_GREATER_EQUAL, enabled: true, writeDepth: true);
        this.depthLessNoWrite = CreateDepthState(D3D11_COMPARISON_FUNC.D3D11_COMPARISON_LESS_EQUAL, enabled: true, writeDepth: false);
        this.depthGreaterNoWrite = CreateDepthState(D3D11_COMPARISON_FUNC.D3D11_COMPARISON_GREATER_EQUAL, enabled: true, writeDepth: false);
        this.depthOff = CreateDepthState(D3D11_COMPARISON_FUNC.D3D11_COMPARISON_ALWAYS, enabled: false, writeDepth: false);
        D3D11_BLEND_DESC d3D11_BLEND_DESC = default(D3D11_BLEND_DESC);
        d3D11_BLEND_DESC.RenderTarget[0] = new D3D11_RENDER_TARGET_BLEND_DESC
        {
            BlendEnable = 1,
            SrcBlend = D3D11_BLEND.D3D11_BLEND_SRC_ALPHA,
            DestBlend = D3D11_BLEND.D3D11_BLEND_INV_SRC_ALPHA,
            BlendOp = D3D11_BLEND_OP.D3D11_BLEND_OP_ADD,
            SrcBlendAlpha = D3D11_BLEND.D3D11_BLEND_ONE,
            DestBlendAlpha = D3D11_BLEND.D3D11_BLEND_INV_SRC_ALPHA,
            BlendOpAlpha = D3D11_BLEND_OP.D3D11_BLEND_OP_ADD,
            RenderTargetWriteMask = 15
        };
        ID3D11BlendState* alphaBlend = null;
        Check(this.device->CreateBlendState(&d3D11_BLEND_DESC, &alphaBlend));
        this.alphaBlend = alphaBlend;
    }

    private unsafe ID3D11DepthStencilState* CreateDepthState(D3D11_COMPARISON_FUNC comparison, bool enabled, bool writeDepth)
    {
        ID3D11DepthStencilState* result = null;
        D3D11_DEPTH_STENCIL_DESC d3D11_DEPTH_STENCIL_DESC = new D3D11_DEPTH_STENCIL_DESC
        {
            DepthEnable = (enabled ? 1 : 0),
            DepthWriteMask = ((enabled & writeDepth) ? D3D11_DEPTH_WRITE_MASK.D3D11_DEPTH_WRITE_MASK_ALL : D3D11_DEPTH_WRITE_MASK.D3D11_DEPTH_WRITE_MASK_ZERO),
            DepthFunc = comparison,
            StencilEnable = 0
        };
        Check(this.device->CreateDepthStencilState(&d3D11_DEPTH_STENCIL_DESC, &result));
        return result;
    }

    private unsafe bool EnsureVertexBuffer(uint requiredBytes)
    {
        if (this.vertexBuffer != null && this.vertexBufferBytes >= requiredBytes)
        {
            return true;
        }
        Release((IUnknown*)this.vertexBuffer);
        this.vertexBuffer = null;
        this.vertexBufferBytes = Math.Max(4096u, NextPowerOfTwo(requiredBytes));
        D3D11_BUFFER_DESC d3D11_BUFFER_DESC = new D3D11_BUFFER_DESC
        {
            ByteWidth = this.vertexBufferBytes,
            Usage = D3D11_USAGE.D3D11_USAGE_DYNAMIC,
            BindFlags = 1u,
            CPUAccessFlags = 65536u
        };
        ID3D11Buffer* ptr = null;
        HRESULT hRESULT = this.device->CreateBuffer(&d3D11_BUFFER_DESC, null, &ptr);
        if (hRESULT.FAILED)
        {
            Release((IUnknown*)ptr);
            this.initializationFailed = true;
            Status = $"创建顶点缓冲失败：0x{hRESULT.Value:X8}";
            return false;
        }
        this.vertexBuffer = ptr;
        return true;
    }

    private static List<RenderBatch> PrepareBatches(IReadOnlyList<RenderBatch> source, Vector3 cameraPosition)
    {
        List<SortedBatch> list = new List<SortedBatch>(source.Count);
        List<SortedBatch> list2 = new List<SortedBatch>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            RenderBatch batch = source[i];
            SortedBatch item = new SortedBatch(batch, GetDistanceSquared(in batch, cameraPosition), i);
            (batch.IsTransparent ? list2 : list).Add(item);
        }
        list.Sort((SortedBatch left2, SortedBatch right) => Compare(in left2, in right, backToFront: false));
        list2.Sort((SortedBatch left2, SortedBatch right) => Compare(in left2, in right, backToFront: true));
        List<RenderBatch> list3 = new List<RenderBatch>(source.Count);
        foreach (SortedBatch item2 in list)
        {
            list3.Add(item2.Batch);
        }
        foreach (SortedBatch item3 in list2)
        {
            list3.Add(item3.Batch);
        }
        List<RenderBatch> list4 = new List<RenderBatch>(list3.Count);
        int num = 0;
        while (num < list3.Count)
        {
            RenderBatch left = list3[num];
            int num2 = num + 1;
            int num3 = left.Vertices.Length;
            for (; num2 < list3.Count && CanMerge(in left, list3[num2]); num2++)
            {
                num3 += list3[num2].Vertices.Length;
            }
            if (num2 == num + 1)
            {
                list4.Add(left);
            }
            else
            {
                GpuVertex[] array = new GpuVertex[num3];
                int num4 = 0;
                for (int num5 = num; num5 < num2; num5++)
                {
                    GpuVertex[] vertices = list3[num5].Vertices;
                    Array.Copy(vertices, 0, array, num4, vertices.Length);
                    num4 += vertices.Length;
                }
                list4.Add(left with
                {
                    Vertices = array
                });
            }
            num = num2;
        }
        return list4;
    }

    private static int Compare(in SortedBatch left, in SortedBatch right, bool backToFront)
    {
        int num = (backToFront ? right.DistanceSquared.CompareTo(left.DistanceSquared) : left.DistanceSquared.CompareTo(right.DistanceSquared));
        return (num != 0) ? num : left.Order.CompareTo(right.Order);
    }

    private static float GetDistanceSquared(in RenderBatch batch, Vector3 cameraPosition)
    {
        if (batch.Vertices.Length == 0)
        {
            return -1f;
        }
        Vector3 zero = Vector3.Zero;
        GpuVertex[] vertices = batch.Vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            GpuVertex gpuVertex = vertices[i];
            zero += gpuVertex.Position;
        }
        zero /= (float)batch.Vertices.Length;
        return Vector3.DistanceSquared(cameraPosition, zero);
    }

    private static bool CanMerge(in RenderBatch left, in RenderBatch right)
    {
        return left.Pipeline == right.Pipeline && left.Texture == right.Texture && left.IsTransparent == right.IsTransparent && left.Parameters0 == right.Parameters0 && left.Parameters1 == right.Parameters1 && left.Parameters2 == right.Parameters2;
    }

    private unsafe void RecordFrame(IReadOnlyList<RenderBatch> batches, in CameraFrame camera, uint width, uint height, ID3D11ShaderResourceView* sceneDepthView, uint sceneDepthWidth, uint sceneDepthHeight, ID3D11Texture2D* uiMaskSource, ID3D11ShaderResourceView* uiMaskView)
    {
        if (this.uiMaskActiveThisFrame)
        {
            this.deferred->CopyResource((ID3D11Resource*)this.uiMaskTexture, (ID3D11Resource*)uiMaskSource);
        }
        this.deferred->RSSetState(this.rasterizer);
        this.deferred->IASetInputLayout(this.inputLayout);
        this.deferred->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        uint num = (uint)sizeof(GpuVertex);
        uint num2 = 0u;
        ID3D11Buffer* vertexBuffer = this.vertexBuffer;
        ID3D11Buffer* constantBuffer = this.constantBuffer;
        ID3D11SamplerState* sampler = this.sampler;
        this.deferred->IASetVertexBuffers(0u, 1u, &vertexBuffer, &num, &num2);
        this.deferred->VSSetConstantBuffers(0u, 1u, &constantBuffer);
        this.deferred->PSSetConstantBuffers(0u, 1u, &constantBuffer);
        this.deferred->PSSetSamplers(0u, 1u, &sampler);
        byte* intPtr = stackalloc byte[16];
        // IL initblk instruction
        Unsafe.InitBlock(intPtr, 0, 16);
        float* colorRGBA = (float*)intPtr;
        this.deferred->ClearRenderTargetView(this.colorTarget, colorRGBA);
        float depth = (camera.ReversedDepth ? 0f : 1f);
        this.deferred->ClearDepthStencilView(this.depthTarget, 1u, depth, 0);
        ID3D11RenderTargetView* colorTarget = this.colorTarget;
        this.deferred->OMSetRenderTargets(1u, &colorTarget, this.depthTarget);
        D3D11_VIEWPORT d3D11_VIEWPORT = new D3D11_VIEWPORT(0f, 0f, width, height);
        this.deferred->RSSetViewports(1u, &d3D11_VIEWPORT);
        ID3D11ShaderResourceView** ptr = stackalloc ID3D11ShaderResourceView*[3];
        this.currentCameraReversedDepth = camera.ReversedDepth;
        foreach (RenderBatch batch2 in batches)
        {
            RenderBatch batch = batch2;
            UploadVertices(batch.Vertices);
            UploadConstants(CreateConstants(in camera, width, height, sceneDepthWidth, sceneDepthHeight, in batch));
            BindPipeline(in batch);
            *ptr = (SceneDepthAvailable ? sceneDepthView : null);
            ptr[1] = ((batch.Texture == IntPtr.Zero) ? ((ID3D11ShaderResourceView*)null) : ((ID3D11ShaderResourceView*)batch.Texture));
            ptr[2] = null;
            this.deferred->PSSetShaderResources(0u, 3u, ptr);
            this.deferred->Draw((uint)batch.Vertices.Length, 0u);
        }
        ID3D11RenderTargetView* resolvedTarget = this.resolvedTarget;
        this.deferred->OMSetRenderTargets(1u, &resolvedTarget, null);
        this.deferred->ClearRenderTargetView(this.resolvedTarget, colorRGBA);
        this.deferred->OMSetBlendState(null, null, uint.MaxValue);
        this.deferred->OMSetDepthStencilState(this.depthOff, 0u);
        this.deferred->IASetInputLayout(null);
        this.deferred->VSSetShader(this.resolveVertexShader, null, 0u);
        this.deferred->PSSetShader(this.resolvePixelShader, null, 0u);
        *ptr = null;
        ptr[1] = this.colorView;
        ptr[2] = (this.uiMaskActiveThisFrame ? uiMaskView : null);
        this.deferred->PSSetShaderResources(0u, 3u, ptr);
        this.deferred->Draw(3u, 0u);
        ID3D11ShaderResourceView** ptr2 = stackalloc ID3D11ShaderResourceView*[3];
        for (int i = 0; i < 3; i++)
        {
            ptr2[i] = null;
        }
        this.deferred->PSSetShaderResources(0u, 3u, ptr2);
        ID3D11CommandList* pCommandList = null;
        Check(this.deferred->FinishCommandList(0, &pCommandList));
        try
        {
            this.immediate->ExecuteCommandList(pCommandList, 1);
        }
        finally
        {
            Release((void**)(&pCommandList));
        }
    }

    private unsafe void BindPipeline(in RenderBatch batch)
    {
        var stroke = batch.Pipeline == DrawPipeline.Stroke;
        this.deferred->VSSetShader(stroke ? this.strokeVertexShader : this.solidVertexShader, null, 0);
        this.deferred->PSSetShader(stroke ? this.strokePixelShader : this.solidPixelShader, null, 0);
        this.deferred->OMSetBlendState(batch.IsTransparent ? this.alphaBlend : null, null, uint.MaxValue);
        var depth = this.currentCameraReversedDepth ? this.depthGreater : this.depthLess;
        if (batch.IsTransparent)
            depth = this.currentCameraReversedDepth ? this.depthGreaterNoWrite : this.depthLessNoWrite;
        this.deferred->OMSetDepthStencilState(depth, 0);
    }

    private FrameConstants CreateConstants(in CameraFrame camera, uint width, uint height, uint sceneDepthWidth, uint sceneDepthHeight, in RenderBatch batch)
    {
        return new FrameConstants
        {
            ViewProjection = camera.ViewProjection,
            InverseViewProjection = camera.InverseViewProjection,
            CameraPosition = new Vector4(camera.Position, 0f),
            ViewportAndTolerance = new Vector4(width, height, 0f, 0.02f),
            Effects = new Vector4(SceneDepthAvailable ? 1f : 0f, this.settings.DxSceneOcclusion ? this.settings.DxOccludedAlpha : 1f, 0f, this.uiMaskActiveThisFrame ? 1f : 0f),
            DepthInfo = new Vector4(camera.ReversedDepth ? 1f : 0f, sceneDepthWidth, sceneDepthHeight, this.settings.DxSceneOcclusion ? 1f : 0f),
            ClipRect = Vector4.Zero,
            Parameters0 = batch.Parameters0,
            Parameters1 = batch.Parameters1,
            Parameters2 = batch.Parameters2
        };
    }

    private unsafe void UploadVertices(GpuVertex[] vertices)
    {
        D3D11_MAPPED_SUBRESOURCE d3D11_MAPPED_SUBRESOURCE = default(D3D11_MAPPED_SUBRESOURCE);
        Check(this.deferred->Map((ID3D11Resource*)this.vertexBuffer, 0u, D3D11_MAP.D3D11_MAP_WRITE_DISCARD, 0u, &d3D11_MAPPED_SUBRESOURCE));
        fixed (GpuVertex* source = vertices)
        {
            Buffer.MemoryCopy(source, d3D11_MAPPED_SUBRESOURCE.pData, this.vertexBufferBytes, vertices.Length * sizeof(GpuVertex));
        }
        this.deferred->Unmap((ID3D11Resource*)this.vertexBuffer, 0u);
    }

    private unsafe void UploadConstants(in FrameConstants constants)
    {
        D3D11_MAPPED_SUBRESOURCE d3D11_MAPPED_SUBRESOURCE = default(D3D11_MAPPED_SUBRESOURCE);
        Check(this.deferred->Map((ID3D11Resource*)this.constantBuffer, 0u, D3D11_MAP.D3D11_MAP_WRITE_DISCARD, 0u, &d3D11_MAPPED_SUBRESOURCE));
        *(FrameConstants*)d3D11_MAPPED_SUBRESOURCE.pData = constants;
        this.deferred->Unmap((ID3D11Resource*)this.constantBuffer, 0u);
    }







    private unsafe static bool TryGetSceneDepth(uint viewportWidth, uint viewportHeight, out ID3D11ShaderResourceView* view, out uint width, out uint height)
    {
        view = null;
        width = 0u;
        height = 0u;
        RenderTargetManager* ptr = RenderTargetManager.Instance();
        Texture* ptr2 = ((ptr != null) ? ptr->DepthStencil : null);
        if (ptr2 == null || ptr2->D3D11ShaderResourceView == null)
        {
            return false;
        }
        width = ptr2->ActualWidth;
        height = ptr2->ActualHeight;
        if (width == 0 || height == 0 || width > ptr2->AllocatedWidth || height > ptr2->AllocatedHeight || MathF.Abs((float)width / (float)height - (float)viewportWidth / (float)viewportHeight) > 0.02f)
        {
            width = 0u;
            height = 0u;
            return false;
        }
        view = (ID3D11ShaderResourceView*)ptr2->D3D11ShaderResourceView;
        return true;
    }

    private unsafe static bool TryGetCamera(out CameraFrame frame)
    {
        Control* ptr = Control.Instance();
        FFXIVClientStructs.FFXIV.Client.Game.Camera* ptr2 = ((ptr != null) ? ptr->CameraManager.GetActiveCamera() : null);
        FFXIVClientStructs.FFXIV.Client.Graphics.Render.Camera* ptr3 = ((ptr2 != null) ? ptr2->SceneCamera.RenderCamera : null);
        if (ptr == null || ptr3 == null)
        {
            frame = default(CameraFrame);
            return false;
        }
        Matrix4x4 matrix4x = ptr->ViewProjectionMatrix;
        Matrix4x4 matrix4x2 = ptr3->ProjectionMatrix;
        if (!Matrix4x4.Invert(matrix4x, out var result) || !Matrix4x4.Invert(matrix4x2, out var result2))
        {
            frame = default(CameraFrame);
            return false;
        }
        Matrix4x4 matrix = matrix4x * result2;
        if (!Matrix4x4.Invert(matrix, out var result3))
        {
            frame = default(CameraFrame);
            return false;
        }
        Vector4 nearPlane = new Vector4(matrix.M13, matrix.M23, matrix.M33, matrix.M43 + ptr3->NearPlane);
        Vector3 translation = result3.Translation;
        Vector3 right = Vector3.Normalize(new Vector3(result3.M11, result3.M12, result3.M13));
        frame = new CameraFrame(matrix4x, result, translation, right, nearPlane, IsReversedDepth(matrix4x2, ptr3->NearPlane));
        return true;
    }

    private static bool IsReversedDepth(Matrix4x4 projection, float nearPlane)
    {
        float num = MathF.Max(nearPlane, 0.01f);
        float num2 = num * projection.M34 + projection.M44;
        float num3 = ((num2 > 0f) ? 1f : (-1f));
        float num4 = ProjectDepth(projection, num3 * num);
        float num5 = ProjectDepth(projection, num3 * 10000f);
        return float.IsFinite(num4) && float.IsFinite(num5) && num4 > num5;
    }

    private static float ProjectDepth(Matrix4x4 projection, float z)
    {
        float num = z * projection.M33 + projection.M43;
        float num2 = z * projection.M34 + projection.M44;
        return num / num2;
    }

    private static byte[] ReadShaderSource()
    {
        Assembly executingAssembly = Assembly.GetExecutingAssembly();
        string name = executingAssembly.GetManifestResourceNames().Single((string resource) => resource.EndsWith("DrawPlus.hlsl", StringComparison.Ordinal));
        using Stream stream = executingAssembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("找不到内嵌 DrawPlus Shader。");
        using MemoryStream memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private unsafe static ID3DBlob* Compile(byte[] source, string entryPoint, string target)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(entryPoint + "\0");
        byte[] bytes2 = Encoding.ASCII.GetBytes(target + "\0");
        fixed (byte* pSrcData = source)
        {
            fixed (byte* pEntrypoint = bytes)
            {
                fixed (byte* pTarget = bytes2)
                {
                    ID3DBlob* result = null;
                    ID3DBlob* ptr = null;
                    HRESULT hRESULT = DirectX.D3DCompile(pSrcData, (nuint)source.Length, null, null, null, (sbyte*)pEntrypoint, (sbyte*)pTarget, 0u, 0u, &result, &ptr);
                    if (hRESULT.FAILED)
                    {
                        string text = ((ptr == null) ? $"HRESULT 0x{hRESULT.Value:X8}" : (Marshal.PtrToStringUTF8((nint)ptr->GetBufferPointer(), checked((int)ptr->GetBufferSize())) ?? "未知 Shader 错误"));
                        Release((void**)(&ptr));
                        throw new InvalidOperationException("编译 " + entryPoint + " 失败：" + text);
                    }
                    Release((void**)(&ptr));
                    return result;
                }
            }
        }
    }

    private unsafe void ReleaseDevice()
    {
        SceneDepthAvailable = false;
        this.uiMaskActiveThisFrame = false;
        ReleaseUiMaskResources();
        ReleaseTargets();
        Release((IUnknown*)this.vertexBuffer);
        this.vertexBuffer = null;
        Release((IUnknown*)this.constantBuffer);
        this.constantBuffer = null;
        Release((IUnknown*)this.inputLayout);
        this.inputLayout = null;
        Release((IUnknown*)this.solidVertexShader);
        this.solidVertexShader = null;
        Release((IUnknown*)this.strokeVertexShader);
        this.strokeVertexShader = null;
        Release((IUnknown*)this.resolveVertexShader);
        this.resolveVertexShader = null;
        Release((IUnknown*)this.solidPixelShader);
        this.solidPixelShader = null;
        Release((IUnknown*)this.strokePixelShader);
        this.strokePixelShader = null;
        Release((IUnknown*)this.resolvePixelShader);
        this.resolvePixelShader = null;
        Release((IUnknown*)this.sampler);
        this.sampler = null;
        Release((IUnknown*)this.rasterizer);
        this.rasterizer = null;
        Release((IUnknown*)this.depthLess);
        this.depthLess = null;
        Release((IUnknown*)this.depthGreater);
        this.depthGreater = null;
        Release((IUnknown*)this.depthLessNoWrite);
        this.depthLessNoWrite = null;
        Release((IUnknown*)this.depthGreaterNoWrite);
        this.depthGreaterNoWrite = null;
        Release((IUnknown*)this.depthOff);
        this.depthOff = null;
        Release((IUnknown*)this.alphaBlend);
        this.alphaBlend = null;
        Release((IUnknown*)this.deferred);
        this.deferred = null;
        Release((IUnknown*)this.immediate);
        this.immediate = null;
        Release((IUnknown*)this.device);
        this.device = null;
        this.vertexBufferBytes = 0u;
        this.deviceHandle = IntPtr.Zero;
    }

    private unsafe void ReleaseTargets()
    {
        Release((IUnknown*)this.depthTarget);
        this.depthTarget = null;
        Release((IUnknown*)this.depthTexture);
        this.depthTexture = null;
        Release((IUnknown*)this.resolvedView);
        this.resolvedView = null;
        Release((IUnknown*)this.resolvedTarget);
        this.resolvedTarget = null;
        Release((IUnknown*)this.resolvedTexture);
        this.resolvedTexture = null;
        Release((IUnknown*)this.colorView);
        this.colorView = null;
        Release((IUnknown*)this.colorTarget);
        this.colorTarget = null;
        Release((IUnknown*)this.colorTexture);
        this.colorTexture = null;
        this.targetWidth = 0u;
        this.targetHeight = 0u;
    }

    private unsafe void ReleaseUiMaskResources()
    {
        Release((IUnknown*)this.uiMaskView);
        this.uiMaskView = null;
        Release((IUnknown*)this.uiMaskTexture);
        this.uiMaskTexture = null;
        this.uiMaskWidth = 0u;
        this.uiMaskHeight = 0u;
        this.uiMaskFormat = DXGI_FORMAT.DXGI_FORMAT_UNKNOWN;
    }

    private unsafe static void Release(void** resource)
    {
        if (*resource != null)
        {
            ((IUnknown*)(*resource))->Release();
            *resource = null;
        }
    }

    private unsafe static void Release(IUnknown* resource)
    {
        if (resource != null)
        {
            resource->Release();
        }
    }

    private static void Check(HRESULT result)
    {
        if (result.FAILED)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    private static uint NextPowerOfTwo(uint value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}
