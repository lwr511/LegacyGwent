// 水底流动焦散：纯程序（无贴图），多层正弦叠加取脊线后锐化。
// 叠加混合铺在背景层之上（Queue=3010），由全局 _LadyLakeTime 驱动，可完全复现。
Shader "LegacyGwent/LadyLake/Caustics"
{
    Properties
    {
        _Color ("Caustic Color", Color) = (0.55, 0.95, 0.72, 1)
        _Strength ("Strength", Range(0,3)) = 0.55
        _Tiling ("Tiling", Range(0.5,16)) = 3.2
        _Sharpness ("Sharpness", Range(1,24)) = 7
        _Speed ("Speed", Range(0,4)) = 0.55
        _Scroll ("Scroll XY", Vector) = (0.06, 0.13, 0, 0)
        _VerticalFade ("Vertical Fade", Range(0,2)) = 0.45

        // 来自 Aeschna(15010100 / Thronebreaker) 原版水底焦散材质
        // 15010100_164296__15010300_Aeschna_RiverbedCaustics.mat 的贴图
        _CausticsTex ("Aeschna Caustics", 2D) = "white" {}
        _CausticsStrength ("Caustics Texture Strength", Range(0,2)) = 0.75
        _CausticsTiling ("Caustics Tiling", Range(0.2,6)) = 1.15
        _CausticsScroll ("Caustics Scroll (XY)", Vector) = (0.045, 0.085, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Strength;
            float _Tiling;
            float _Sharpness;
            float _Speed;
            float4 _Scroll;
            float _VerticalFade;

            sampler2D _CausticsTex;
            float _CausticsStrength;
            float _CausticsTiling;
            float4 _CausticsScroll;

            float _LadyLakeTime;
            float _LadyLakeLoop;
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
                float t = _LadyLakeTime * _Speed;
                float2 p = (i.uv + _Scroll.xy * _LadyLakeTime) * _Tiling;

                float v = 0.0;
                v += sin(p.x * 1.7 + sin(p.y * 1.3 + t));
                v += sin(p.y * 1.9 + sin(p.x * 1.1 - t * 0.8));
                v += 0.5 * sin(p.x * 3.1 + p.y * 2.3 + t * 1.4);
                v *= 0.4;

                float ridge = pow(saturate(1.0 - abs(v)), _Sharpness);
                float fade = lerp(1.0, saturate(i.uv.y * 1.4), _VerticalFade);

                // Aeschna 原版水底焦散贴图：两层反向缓慢流动叠加，只做形态调制，运动仍由程序驱动
                float2 cuv = i.uv * _CausticsTiling;
                float texA = tex2D(_CausticsTex, cuv + _CausticsScroll.xy * _LadyLakeTime).r;
                float texB = tex2D(_CausticsTex, cuv * 1.37 - _CausticsScroll.xy * _LadyLakeTime * 0.7 + 0.37).r;
                float texPattern = saturate(texA * 0.65 + texB * 0.55);
                float pattern = lerp(1.0, texPattern * 1.6, saturate(_CausticsStrength));

                float amount = ridge * pattern * _Strength * fade * saturate(_LadyLakeIntensity);
                return fixed4(_Color.rgb, saturate(amount));
            }
            ENDCG
        }
    }

    Fallback "Unlit/Transparent"
}
