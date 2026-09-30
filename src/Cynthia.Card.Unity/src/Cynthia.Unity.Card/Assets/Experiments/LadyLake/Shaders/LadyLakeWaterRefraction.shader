// 全屏轻微水折射 / 涟漪：GrabPass 抓取已渲染画面，用程序化波纹做极小 UV 偏移后写回。
// 强度默认只有 0.006（屏幕 UV 单位），并且随剑移动（wake）略微增强，不会糊掉画面。
Shader "LegacyGwent/LadyLake/WaterRefraction"
{
    Properties
    {
        _RippleStrength ("Ripple Strength", Range(0,0.05)) = 0.006
        _Tiling ("Ripple Tiling", Range(1,80)) = 26
        _Speed ("Ripple Speed", Range(0,6)) = 0.9
        _Tint ("Water Tint", Color) = (0.86, 1.0, 0.97, 1)
        _TintAmount ("Tint Amount", Range(0,1)) = 0.16
        _FxIntensity ("FX Intensity", Range(0,2)) = 1

        // 来自 Aeschna(15010100 / Thronebreaker) 水下材质集的流动噪声贴图
        _FlowTex ("Aeschna Flow Noise", 2D) = "gray" {}
        _FlowTiling ("Flow Tiling", Range(0.1,8)) = 1.6
        _FlowStrength ("Flow Strength", Range(0,1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        GrabPass { "_LadyLakeGrabTex" }

        Pass
        {
            Cull Off
            Lighting Off
            ZWrite Off
            Blend Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _LadyLakeGrabTex;
            float _RippleStrength;
            float _Tiling;
            float _Speed;
            fixed4 _Tint;
            float _TintAmount;
            float _FxIntensity;

            sampler2D _FlowTex;
            float _FlowTiling;
            float _FlowStrength;

            float _LadyLakeTime;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 grab : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grab = ComputeGrabScreenPos(o.pos);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 screenUv = i.grab.xy / max(1e-4, i.grab.w);

                float t = _LadyLakeTime * _Speed;
                float2 q = i.uv * _Tiling;

                float2 ripple = float2(
                    sin(q.y * 1.7 + t) + 0.5 * sin(q.x * 2.3 - t * 0.7),
                    cos(q.x * 1.9 - t * 0.85) + 0.5 * cos(q.y * 2.7 + t * 0.6));

                // Aeschna 流动噪声：给波纹叠一层缓慢漂移的扰动方向
                float2 flowUv = i.uv * _FlowTiling + _LadyLakeTime * 0.021;
                float flow = tex2D(_FlowTex, flowUv).r * 2.0 - 1.0;
                float2 flowDir = float2(flow, tex2D(_FlowTex, flowUv.yx * 1.31 + 0.29).r * 2.0 - 1.0);
                ripple += flowDir * _FlowStrength * 0.6;

                float strength = _RippleStrength * (0.5 + 0.5 * saturate(_FxIntensity));
                float2 offset = ripple * strength;

                float3 col = tex2D(_LadyLakeGrabTex, screenUv + offset).rgb;
                col = lerp(col, col * _Tint.rgb, _TintAmount);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
