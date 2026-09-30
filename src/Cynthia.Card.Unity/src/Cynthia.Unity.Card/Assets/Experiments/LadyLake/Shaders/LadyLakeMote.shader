// 漂浮微尘：极小的叠加光点，数量多但很暗，不会在脸上形成亮斑。
Shader "LegacyGwent/LadyLake/Mote"
{
    Properties
    {
        _Color ("Mote Color", Color) = (0.72, 0.95, 0.80, 1)
        _Strength ("Strength", Range(0,3)) = 0.5
        _Falloff ("Falloff", Range(0.5,16)) = 6
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
                float amount = exp(-dot(p, p) * _Falloff) * i.color.a * _Strength
                               * (0.5 + 0.5 * saturate(_LadyLakeIntensity));

                return fixed4(_Color.rgb, saturate(amount));
            }
            ENDCG
        }
    }

    Fallback "Unlit/Transparent"
}
