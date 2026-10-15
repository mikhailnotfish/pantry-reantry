Shader "Hidden/Reentry/PlanetDetail"
{

    HLSLINCLUDE
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float3 _PlanetCenterRel;
    float  _PlanetRadius;
    float  _CamHeight;
    float3 _CamWorld;
    float2 _GroundOffset;
    float3 _SunDir;
    float  _Strength;
    float  _Relief;
    float  _BaseWave;

    uint HashU(uint x)
    {
        x ^= x >> 16; x *= 0x7feb352dU;
        x ^= x >> 15; x *= 0x846ca68bU;
        x ^= x >> 16;
        return x;
    }

    float Grad(float2 cell, float2 d)
    {
        uint h = HashU((uint)(int)cell.x * 1973U + HashU((uint)(int)cell.y + 9277U));
        float ang = (float)(h >> 8) * (6.2831853 / 16777216.0);
        return dot(float2(cos(ang), sin(ang)), d);
    }

    float GNoise(float2 p)
    {
        float2 i = floor(p);
        float2 f = p - i;
        float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
        float a = Grad(i, f);
        float b = Grad(i + float2(1, 0), f - float2(1, 0));
        float c = Grad(i + float2(0, 1), f - float2(0, 1));
        float d = Grad(i + float2(1, 1), f - float2(1, 1));
        return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
    }

    void Terrain(float2 q, float foot, out float h, out float av)
    {
        h = 0; av = 0;
        float lam = _BaseWave;
        float amp = 1;
        const float2x2 R = float2x2(0.8, -0.6, 0.6, 0.8);
        [loop]
        for (int i = 0; i < 11; i++)
        {
            float w = saturate(lam / (foot * 3.0) - 0.5);
            if (w <= 0) break;
            float n = GNoise(q / lam);
            h  += n * lam * 0.16 * amp * w;
            av += n * amp * w;
            q = mul(R, q) + 31.7;
            lam *= 0.5;
            amp *= 0.78;
        }
    }

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float depth = LoadCameraDepth(varyings.positionCS.xy);
        float3 src = CustomPassLoadCameraColor(varyings.positionCS.xy, 0);

        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
        float3 d = normalize(posInput.positionWS);

        float3 o = -_PlanetCenterRel;
        float b = dot(o, d);
        float K = _CamHeight * (2.0 * _PlanetRadius + _CamHeight);
        float disc = b * b - K;
        float t = -b - sqrt(max(disc, 0.0));
        float3 P = d * t;
        float3 N0 = normalize(o + P);

        float3 w3 = P + _CamWorld;
        float2 q = w3.xz - _GroundOffset;

        float2 fw = fwidth(q);
        float foot = clamp(max(fw.x, fw.y), 0.25, 2.0e5);

        bool hit = (depth <= 0.0) && (disc > 0.0) && (b < 0.0) && (t > 0.0);
        if (!hit) return float4(src, 1);

        float mx = max(src.r, max(src.g, src.b));
        float mn = min(src.r, min(src.g, src.b));
        float sat = (mx - mn) / max(mx, 1e-6);
        float ocean = saturate((src.b / max(src.r, 1e-6) - 1.2) / 0.6);
        float landM = saturate((sat - 0.10) / 0.10) * (1.0 - ocean);
        if (landM <= 0.001) return float4(src, 1);

        float h0, a0, h1, a1, h2, a2;
        float e = max(foot * 1.5, 1.0);
        Terrain(q, foot, h0, a0);
        Terrain(q + float2(e, 0), foot, h1, a1);
        Terrain(q + float2(0, e), foot, h2, a2);
        float gx = (h1 - h0) / e;
        float gz = (h2 - h0) / e;

        float3 L = _SunDir;
        float3 Tx = normalize(float3(1, 0, 0) - N0 * N0.x);
        float3 Tz = normalize(float3(0, 0, 1) - N0 * N0.z);
        float3 Np = normalize(N0 - _Relief * (gx * Tx + gz * Tz));
        float nl0 = saturate(dot(N0, L));
        float nlp = saturate(dot(Np, L));
        float shade = clamp((nlp + 0.12) / (nl0 + 0.12), 0.35, 1.9);

        float albedo = 1.0 + a0 * 0.45 * _Strength;
        float3 tint = float3(1.0 + a0 * 0.10 * _Strength, 1.0 + a0 * 0.03 * _Strength, 1.0 - a0 * 0.12 * _Strength);

        float3 lit = src * shade * albedo * tint;
        return float4(lerp(src, lit, landM), 1);
    }
    ENDHLSL

    SubShader
    {
        Pass
        {
            Name "Custom Pass 0"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}
