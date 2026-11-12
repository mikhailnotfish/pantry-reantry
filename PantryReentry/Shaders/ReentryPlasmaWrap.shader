Shader "Reentry/PlasmaWrap"
{
    Properties
    {
        _Plasma ("Plasma (0-1)", Range(0,1)) = 0
        _Intensity ("Intensity", Float) = 3
        _FlowDir ("Flow direction (world)", Vector) = (1,0,0,0)
        _CenterOffset ("Offset from this renderer's pivot to object centre (world)", Vector) = (0,0,0,0)
        _Inflate ("Shock layer thickness (m)", Float) = 0.3
        _WakeLen ("Wake length (m)", Float) = 2
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 viewNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float3 rel : TEXCOORD2;
                float windward : TEXCOORD3;
                float leeward : TEXCOORD4;
            };

            float _Plasma;
            float _Intensity;
            float4 _FlowDir;
            float4 _CenterOffset;
            float _Inflate;
            float _WakeLen;

            v2f vert(appdata v)
            {
                v2f o;

                float3 pivotW = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                float3 N = normalize(UnityObjectToWorldNormal(v.normal));
                float3 flow = normalize(_FlowDir.xyz);

                float front = dot(N, flow);
                float windward = saturate(front);
                float leeward = saturate(-front);

                worldPos += N * _Inflate * (0.35 + 0.65 * windward);

                float3 centerW = pivotW + _CenterOffset.xyz;
                float3 rel = worldPos - centerW;

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));

                float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz;
                o.viewNormal = mul((float3x3)UNITY_MATRIX_V, N);
                o.viewDir = -viewPos;
                o.rel = worldPos - centerW;
                o.windward = windward;
                o.leeward = leeward;
                return o;
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

            float3 HeatRamp(float t)
            {
                float3 c0 = float3(0.45, 0.04, 0.00);
                float3 c1 = float3(1.00, 0.30, 0.03);
                float3 c2 = float3(1.00, 0.80, 0.45);
                float3 c3 = float3(0.70, 0.85, 1.20);
                float3 c = lerp(c0, c1, saturate(t * 3.0));
                c = lerp(c, c2, saturate(t * 3.0 - 1.0));
                c = lerp(c, c3, saturate(t * 3.0 - 2.0));
                return c;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.viewNormal);
                float3 V = normalize(i.viewDir);
                float3 flow = normalize(_FlowDir.xyz);

                float rim = pow(1.0 - saturate(abs(dot(N, V))), 1.5);
                float shock = pow(i.windward, 1.5);

                float tail = 1.0 - smoothstep(0.35, 1.0, i.leeward) * 0.6;

                float a = dot(i.rel, flow);
                float3 lateral = i.rel - flow * a;
                float3 q = lateral * 4.0 + flow * (a * 1.2 + _Time.y * 8.0);
                float n = lerp(0.5, 1.3, ValueNoise(q));

                float glow = (0.2 + 0.8 * rim + 1.2 * shock) * tail * n;
                float alpha = saturate(glow * _Plasma);

                float t = saturate(_Plasma * 1.1 - i.leeward * 0.9 + shock * 0.2);
                float3 col = HeatRamp(t) * _Intensity * (1.0 + shock * 1.2);

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
