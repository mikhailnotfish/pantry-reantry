Shader "Reentry/Surface"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Heat ("Heat (0-1)", Range(0,1)) = 0
        _Plasma ("Plasma (0-1)", Range(0,1)) = 0
        _FlowDir ("Flow Direction (world, direction of motion)", Vector) = (1,-0.1,0,0)
        _EmissionStrength ("Emission Strength (HDR)", Float) = 6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Heat;
                half _Plasma;
                float4 _FlowDir;
                half _EmissionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = n.normalWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float ValueNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(Hash(i + float3(0,0,0)), Hash(i + float3(1,0,0)), f.x),
                         lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x),
                         lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x), f.y),
                    f.z);
            }

            half3 HeatRamp(half t)
            {
                half3 c0 = half3(0.35, 0.02, 0.00);
                half3 c1 = half3(1.00, 0.28, 0.02);
                half3 c2 = half3(1.00, 0.78, 0.40);
                half3 c3 = half3(0.75, 0.85, 1.20);
                half3 c = lerp(c0, c1, saturate(t * 3.0));
                c = lerp(c, c2, saturate(t * 3.0 - 1.0));
                c = lerp(c, c3, saturate(t * 3.0 - 2.0));
                return c;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 flow = normalize(_FlowDir.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                half windward = saturate(dot(N, flow));

                float3 q = IN.positionWS * 4.0;
                q -= flow * dot(q, flow) * 0.7;
                q -= flow * (_Time.y * 10.0);
                half n = ValueNoise(q);

                half heatMask = lerp(0.35, 1.0, pow(windward, 0.7));
                half t = saturate(_Heat * heatMask * (0.75 + 0.5 * n));

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                half3 albedo = baseTex.rgb * lerp(1.0, 0.25, saturate(_Heat * 1.5));

                Light mainLight = GetMainLight();
                half3 lit = albedo * (saturate(dot(N, mainLight.direction)) * mainLight.color + 0.2);

                half incand = smoothstep(0.05, 1.0, t);
                half3 emission = HeatRamp(t) * incand * _EmissionStrength;

                half rim = pow(1.0 - saturate(dot(N, V)), 3.0);
                half sheath = _Plasma * (rim * 0.8 + windward * 0.5) * (0.6 + 0.8 * n);
                emission += HeatRamp(0.55 + 0.45 * _Plasma) * sheath * _EmissionStrength * 0.6;

                return half4(lit + emission, 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
    }
}
