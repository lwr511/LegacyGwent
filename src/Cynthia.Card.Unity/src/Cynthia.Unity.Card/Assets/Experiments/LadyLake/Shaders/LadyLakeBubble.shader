// 气泡：程序化圆形面片，边缘环 + 中心极淡体 + 左上高光。不依赖任何贴图。
// 顶点色携带每颗气泡的 alpha（含上升渐隐与 wake 强度）。
Shader "LegacyGwent/LadyLake/Bubble"
{
    Properties
    {
        _Color ("Bubble Tint", Color) = (0.60, 0.90, 0.88, 1)
        _HighlightColor ("Highlight Color", Color) = (1,1,1,1)
        _Strength ("Strength", Range(0,3)) = 1
        _RimWidth ("Rim Width", Range(0.02,0.6)) = 0.17
        _BodyAlpha ("Body Alpha", Range(0,1)) = 0.10
        _HighlightStrength ("Highlight Strength", Range(0,2)) = 0.7
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _HighlightColor;
            float _Strength;
            float _RimWidth;
            float _BodyAlpha;
            float _HighlightStrength;

            float _LadyLakeTime;
            float _LadyLakeIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (i.color.a <= 0.002) discard;

                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);

                float inner = 1.0 - _RimWidth;
                float ring = smoothstep(inner, inner + _RimWidth * 0.35, r)
                             * (1.0 - smoothstep(0.94, 1.0, r));
                float body = (1.0 - smoothstep(0.0, 1.0, r)) * _BodyAlpha;

                float2 hp = p - float2(-0.34, 0.36);
                float highlight = exp(-dot(hp, hp) * 16.0) * _HighlightStrength;

                float amount = (ring * 0.95 + body + highlight)
                               * i.color.a * _Strength * (0.55 + 0.45 * saturate(_LadyLakeIntensity));

                float3 col = lerp(_Color.rgb, _HighlightColor.rgb, saturate(highlight));
                return fixed4(col, saturate(amount));
            }
            ENDCG
        }
    }

    Fallback "Unlit/Transparent"
}
