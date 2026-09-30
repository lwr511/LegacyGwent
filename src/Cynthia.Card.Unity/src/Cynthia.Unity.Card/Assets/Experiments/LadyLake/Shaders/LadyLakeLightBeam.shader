// 水下体积光束：软边梯形网格 + 横向高斯衰减 + 纵向渐隐 + 缓慢流动条纹。
// 叠加混合、不写深度，默认排在人物之后（Queue=3012）以免把脸糊白。
Shader "LegacyGwent/LadyLake/LightBeam"
{
    Properties
    {
        _Color ("Beam Color", Color) = (0.80, 1.0, 0.76, 1)
        _Strength ("Strength", Range(0,3)) = 0.45
        _Falloff ("Cross Falloff", Range(0.5,8)) = 2.2
        _AlongStart ("Along Fade Start", Range(0,1)) = 0.35
        _ShimmerSpeed ("Shimmer Speed", Range(0,4)) = 0.42
        _Bands ("Bands", Range(0,12)) = 5
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
            float _Falloff;
            float _AlongStart;
            float _ShimmerSpeed;
            float _Bands;

            float _LadyLakeTime;
            float _LadyLakeIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 meta : TEXCOORD1;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 meta : TEXCOORD1;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.meta = v.meta;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float cross = pow(saturate(1.0 - abs(i.uv.x * 2.0 - 1.0)), _Falloff);
                float along = smoothstep(0.0, max(0.01, _AlongStart), i.uv.y);

                float shimmer = 0.7 + 0.3 * sin(i.uv.y * 7.0 - _LadyLakeTime * _ShimmerSpeed + i.meta.x * 6.283);
                float bands = 0.65 + 0.35 * sin(i.uv.y * _Bands + i.uv.x * 2.0 + i.meta.x * 3.0 + _LadyLakeTime * 0.3);

                float amount = cross * along * shimmer * bands * i.color.a * _Strength * saturate(_LadyLakeIntensity);
                return fixed4(_Color.rgb * i.color.rgb, saturate(amount));
            }
            ENDCG
        }
    }

    Fallback "Unlit/Transparent"
}
