Shader "Reentry/Stars"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 3
        _Visibility ("Visibility (0-1)", Range(0,1)) = 1
        _PlanetCenter ("Planet centre relative to camera (m)", Vector) = (0,-6371000,0,0)
        _PlanetRadius ("Planet radius (m)", Float) = 6371000
        _AtmoHeight ("Atmosphere height (m)", Float) = 100000
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 col : TEXCOORD1;
            };

            float _Intensity;
            float _Visibility;
            float4 _PlanetCenter;
            float _PlanetRadius;
            float _AtmoHeight;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                float3 d = normalize(v.normal);
                float3 C = _PlanetCenter.xyz;
                float t = dot(C, d);
                float closest = t > 0.0 ? length(C - d * t) : 1.0e9;
                float height = closest - _PlanetRadius;
                float visible = smoothstep(0.0, _AtmoHeight, height);

                o.col = v.color.rgb * v.color.a * _Intensity * _Visibility * visible;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r2 = dot(p, p);
                float falloff = exp(-5.0 * r2);
                return fixed4(i.col * falloff, 1.0);
            }
            ENDCG
        }
    }
}
