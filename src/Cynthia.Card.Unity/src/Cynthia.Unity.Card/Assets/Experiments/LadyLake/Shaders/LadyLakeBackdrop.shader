// 深海暗色背景：左右两侧延伸出竖卡之外的暗色水体，带暗角与缓慢流动的微光带。
// 不透明、写深度，作为最底层（Queue=2000）。
Shader "LegacyGwent/LadyLake/Backdrop"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.035, 0.115, 0.125, 1)
        _BottomColor ("Bottom Color", Color) = (0.004, 0.018, 0.026, 1)
        _Vignette ("Vignette", Range(0,2)) = 0.65
        _VignettePower ("Vignette Power", Range(0.5,6)) = 1.6
        _WispColor ("Wisp Color", Color) = (0.22, 0.55, 0.46, 1)
        _WispStrength ("Wisp Strength", Range(0,2)) = 0.22
        _WispTiling ("Wisp Tiling", Range(0.5,12)) = 3.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite On
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _BottomColor;
            float _Vignette;
            float _VignettePower;
            fixed4 _WispColor;
            float _WispStrength;
            float _WispTiling;

            float _LadyLakeTime;
            float _LadyLakeIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                float3 col = lerp(_BottomColor.rgb, _TopColor.rgb, pow(saturate(uv.y), 0.85));

                float2 c = uv * 2.0 - 1.0;
                float vig = saturate(1.0 - dot(c, c) * 0.55);
                col *= lerp(1.0, pow(vig, _VignettePower), _Vignette);

                float wisp = sin(uv.y * _WispTiling + _LadyLakeTime * 0.11
                                 + sin(uv.x * 3.0 + _LadyLakeTime * 0.07) * 1.4);
                wisp = pow(saturate(wisp * 0.5 + 0.5), 3.0);
                col += _WispColor.rgb * wisp * _WispStrength
                       * (1.0 - saturate(uv.y * 1.15)) * saturate(_LadyLakeIntensity);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}
