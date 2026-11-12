Shader "Reentry/PlasmaWake"
{
    Properties
    {
        _Plasma ("Plasma (0-1)", Range(0,1)) = 0
        _Intensity ("Intensity", Float) = 3
        _FlowDir ("Flow direction (world)", Vector) = (1,0,0,0)
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 viewNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float3 objPos : TEXCOORD2;
                float s : TEXCOORD3;
                float fade : TEXCOORD4;
            };

            float _Plasma;
            float _Intensity;
            float4 _FlowDir;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz;
                float3 worldN = UnityObjectToWorldNormal(v.normal);
                o.viewNormal = mul((float3x3)UNITY_MATRIX_V, worldN);
                o.viewDir = -viewPos;
                o.objPos = v.vertex.xyz;
                o.s = v.uv.x;
                o.fade = v.uv.y;
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
                float s = saturate(i.s);

                float rim = pow(1.0 - saturate(abs(dot(N, V))), 1.2);

                float3 flow = normalize(_FlowDir.xyz);
                float along = dot(i.objPos, flow);
                float3 lateral = i.objPos - flow * along;
                float3 p = lateral * 4.0 + flow * (along * 1.2 + _Time.y * 6.0);
                float n = lerp(0.4, 1.4, ValueNoise(p));

                float fadeIn = lerp(0.55, 1.0, smoothstep(0.0, 0.08, s));
                float fadeOut = pow(1.0 - s, 1.6);

                float glow = (0.3 + 0.9 * rim) * n * fadeIn * fadeOut;
                float alpha = saturate(glow * _Plasma * i.fade);

                float t = saturate(_Plasma * 0.9 - s * 0.9);
                float3 col = HeatRamp(t) * _Intensity;

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
