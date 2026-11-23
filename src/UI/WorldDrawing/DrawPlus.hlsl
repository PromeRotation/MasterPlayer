cbuffer FrameData : register(b0)
{
    row_major float4x4 ViewProjection;
    row_major float4x4 InverseViewProjection;
    float4 CameraPosition;
    float4 ViewportAndTolerance;
    float4 Effects;
    float4 DepthInfo;
    float4 ClipRect;
    float4 Parameters0;
    float4 Parameters1;
    float4 Parameters2;
};

Texture2D<float> SceneDepth : register(t0);
Texture2D<float4> ImageTexture : register(t1);
Texture2D<float4> UiMaskTexture : register(t2);
SamplerState LinearSampler : register(s0);

struct VertexInput
{
    float3 Position : POSITION;
    float3 Aux : NORMAL;
    float4 Color : COLOR;
    float4 Data : TEXCOORD0;
};

struct PixelInput
{
    float4 Position : SV_POSITION;
    float3 World : TEXCOORD0;
    float4 Color : COLOR;
    float2 Uv : TEXCOORD1;
    float3 Normal : TEXCOORD2;
    noperspective float2 StrokeData : TEXCOORD3;
};

struct ResolveInput
{
    float4 Position : SV_POSITION;
    float2 Uv : TEXCOORD0;
};

bool TryReconstructWorld(float2 uv, float depth, out float3 world)
{
    world = 0.0;
    float clearDepth = DepthInfo.x > 0.5 ? 0.0 : 1.0;
    if (abs(depth - clearDepth) <= 0.000001)
    {
        return false;
    }

    float4 clip = float4(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0, depth, 1.0);
    float4 homogeneous = mul(clip, InverseViewProjection);
    if (abs(homogeneous.w) <= 0.000001 || any(isnan(homogeneous)) || any(isinf(homogeneous)))
    {
        return false;
    }

    world = homogeneous.xyz / homogeneous.w;
    return !any(isnan(world)) && !any(isinf(world));
}

float ReadSceneDepthUv(float2 uv)
{
    uv = saturate(uv);
    uint2 activeSize = (uint2)DepthInfo.yz;
    uint2 coordinate = min((uint2)(uv * activeSize), activeSize - 1);
    return SceneDepth.Load(int3(coordinate, 0));
}

float ReadSceneDepth(float2 pixel)
{
    return ReadSceneDepthUv(pixel / ViewportAndTolerance.xy);
}

float4 ApplyEffects(float4 color, float3 world, float2 pixel, bool testOcclusion)
{
    if (Effects.z > 0.5 &&
        pixel.x >= ClipRect.x && pixel.y >= ClipRect.y &&
        pixel.x <= ClipRect.z && pixel.y <= ClipRect.w)
    {
        discard;
    }

    if (testOcclusion && Effects.x > 0.5 && DepthInfo.w > 0.5)
    {
        float2 uv = pixel / ViewportAndTolerance.xy;
        float3 sceneWorld;
        if (TryReconstructWorld(uv, ReadSceneDepth(pixel), sceneWorld))
        {
            float sceneDistance = distance(CameraPosition.xyz, sceneWorld);
            float geometryDistance = distance(CameraPosition.xyz, world);
            if (geometryDistance > sceneDistance + ViewportAndTolerance.w)
            {
                color.a *= Effects.y;
            }
        }
    }

    if (color.a <= 0.001)
    {
        discard;
    }

    return color;
}

PixelInput SolidVs(VertexInput input)
{
    PixelInput output;
    output.Position = mul(float4(input.Position, 1.0), ViewProjection);
    output.World = input.Position;
    output.Color = input.Color;
    output.Uv = input.Data.xy;
    output.Normal = input.Aux;
    output.StrokeData = 0.0;
    return output;
}

PixelInput StrokeVs(VertexInput input)
{
    float4 start = mul(float4(input.Position, 1.0), ViewProjection);
    float4 end = mul(float4(input.Aux, 1.0), ViewProjection);
    float2 startNdc = start.xy / start.w;
    float2 endNdc = end.xy / end.w;
    float2 deltaPixels = (endNdc - startNdc) * ViewportAndTolerance.xy * 0.5;
    float lengthSquared = dot(deltaPixels, deltaPixels);
    float2 normal = lengthSquared > 0.0001
        ? float2(-deltaPixels.y, deltaPixels.x) * rsqrt(lengthSquared)
        : float2(0.0, 0.0);
    float4 position = lerp(start, end, input.Data.x);
    float halfThickness = input.Data.z * 0.5;
    float outerDistance = halfThickness + 1.0;
    float expandedThickness = outerDistance * 2.0;
    position.xy += normal * (expandedThickness / ViewportAndTolerance.xy) * input.Data.y * position.w;

    PixelInput output;
    output.Position = position;
    output.World = lerp(input.Position, input.Aux, input.Data.x);
    output.Color = input.Color;
    output.Uv = 0.0;
    output.Normal = 0.0;
    output.StrokeData = float2(input.Data.y * outerDistance, halfThickness);
    return output;
}

PixelInput SpriteVs(VertexInput input)
{
    float4 position = mul(float4(input.Position, 1.0), ViewProjection);
    float2 pixelOffset = input.Data.xy * input.Data.zw * 0.5;
    position.xy += pixelOffset * (2.0 / ViewportAndTolerance.xy) * position.w;

    PixelInput output;
    output.Position = position;
    output.World = input.Position;
    output.Color = input.Color;
    output.Uv = input.Aux.xy;
    output.Normal = 0.0;
    output.StrokeData = 0.0;
    return output;
}

PixelInput ProjectionVs(VertexInput input)
{
    PixelInput output;
    output.Position = float4(input.Position, 1.0);
    output.World = 0.0;
    output.Color = input.Color;
    output.Uv = input.Data.xy;
    output.Normal = 0.0;
    output.StrokeData = 0.0;
    return output;
}

float4 SolidPs(PixelInput input) : SV_TARGET
{
    return ApplyEffects(input.Color, input.World, input.Position.xy, true);
}

float4 StrokePs(PixelInput input) : SV_TARGET
{
    float distanceFromCenter = abs(input.StrokeData.x);
    float width = max(fwidth(input.StrokeData.x), 0.0001);
    float coverage = 1.0 - smoothstep(
        input.StrokeData.y - width * 0.5,
        input.StrokeData.y + width * 0.5,
        distanceFromCenter);
    float4 color = input.Color;
    color.a *= saturate(coverage);
    return ApplyEffects(color, input.World, input.Position.xy, true);
}

float4 TexturedPs(PixelInput input) : SV_TARGET
{
    float4 color = ImageTexture.Sample(LinearSampler, input.Uv) * input.Color;
    return ApplyEffects(color, input.World, input.Position.xy, true);
}

float4 SpherePs(PixelInput input) : SV_TARGET
{
    float3 light = normalize(float3(-0.4, 0.8, -0.3));
    float shade = 0.35 + 0.65 * saturate(dot(normalize(input.Normal), light));
    float4 color = float4(input.Color.rgb * shade, input.Color.a);
    return ApplyEffects(color, input.World, input.Position.xy, true);
}

float4 ProjectionPs(PixelInput input) : SV_TARGET
{
    if (Effects.x < 0.5)
    {
        discard;
    }

    float2 screenUv = input.Position.xy / ViewportAndTolerance.xy;
    float3 world;
    if (!TryReconstructWorld(screenUv, ReadSceneDepth(input.Position.xy), world))
    {
        discard;
    }
    float3 delta = world - Parameters0.xyz;
    float2 coordinates = float2(
        dot(delta, Parameters1.xyz),
        dot(delta, Parameters2.xyz));
    float2 uv = coordinates / (2.0 * float2(Parameters0.w, Parameters1.w)) + 0.5;
    if (any(uv < 0.0) || any(uv > 1.0) || abs(delta.y) > Parameters2.w)
    {
        discard;
    }

    float4 color = ImageTexture.Sample(LinearSampler, uv);
    return ApplyEffects(color, world, input.Position.xy, false);
}

ResolveInput ResolveVs(uint vertexId : SV_VertexID)
{
    ResolveInput output;
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
    output.Position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    output.Uv = uv;
    return output;
}

float4 ResolvePs(ResolveInput input) : SV_TARGET
{
    float4 color = ImageTexture.Sample(LinearSampler, input.Uv);
    if (color.a <= 0.001)
    {
        discard;
    }

    color.rgb /= color.a;
    if (Effects.w > 0.5)
    {
        uint2 pixel = (uint2)input.Position.xy;
        color.a *= 1.0 - saturate(UiMaskTexture.Load(int3(pixel, 0)).a);
    }

    return color;
}
