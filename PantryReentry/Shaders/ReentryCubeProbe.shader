Shader "Hidden/Reentry/CubeProbe"
{
    Properties
    {
        _Cube ("Cube", CUBE) = "" {}
    }

    SubShader
    {
        Pass
        {
            ZTest Always
            Cull Off
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            samplerCUBE _Cube;

            float4 frag(v2f_img i) : SV_Target
            {
                float lon = (i.uv.x * 2.0 - 1.0) * UNITY_PI;
                float lat = (i.uv.y - 0.5) * UNITY_PI;
                float3 d = float3(cos(lat) * cos(lon), sin(lat), cos(lat) * sin(lon));
                return texCUBElod(_Cube, float4(d, 0.0));
            }
            ENDCG
        }
    }
}
