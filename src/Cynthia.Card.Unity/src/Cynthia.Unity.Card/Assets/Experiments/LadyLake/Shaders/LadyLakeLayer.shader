// 实验层表面：真实细分浮雕网格 + 软边抠像 + 伪体积光照 + 水下雾 + 轻微折射抖动。
// 不用 surface shader，全部手写 vertex/fragment，避免 2019.4 内置管线下的额外变体与关键字。
// 深度排序完全交给 RenderQueue（ZWrite Off + 双面 Cull Off），保证层与层之间不闪。
Shader "LegacyGwent/LadyLake/Layer"
{
    Properties
    {
        _MainTex ("Layer (RGBA)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MaskHands ("Use repaired hand meshes", Float) = 0
        _FingerOcclusion ("Only fingers in front of sword grip", Float) = 0
        _WristFadePlane ("Wrist blend plane in source pixels", Vector) = (0,0,1,0)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.35
        _Softness ("Edge Softness", Range(0.001,0.9)) = 0.35
        _LightDir ("Fake Light Dir", Vector) = (0.35, 0.72, -0.6, 0)
        _LightStrength ("Fake Light", Range(0,2)) = 0.5
        _Ambient ("Ambient", Range(0,2)) = 0.8
        _RimColor ("Rim Color", Color) = (0.55, 0.95, 0.85, 1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _RimStrength ("Rim Strength", Range(0,3)) = 0.35
        _EdgeGlow ("Silhouette Glow", Range(0,2)) = 0.05
        _FogColor ("Water Fog Color", Color) = (0.02, 0.10, 0.11, 1)
        _FogRange ("Fog Z Near/Deep", Vector) = (-0.35, 0.32, 0, 0)
        _FogStrength ("Fog Strength", Range(0,1)) = 0.85
        _Wobble ("Wobble (X,Y,SpeedX,SpeedY)", Vector) = (0.0018, 0.0011, 1.1, 0.8)
        _DepthTint ("Depth Tint", Color) = (0.80, 1.0, 0.95, 1)
        _DepthTintStrength ("Depth Tint Strength", Range(0,1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Cutoff;
            float _MaskHands;
            float _FingerOcclusion;
            float4 _WristFadePlane;
            float4 _MainTex_TexelSize;
            float _Softness;
            float4 _LightDir;
            float _LightStrength;
            float _Ambient;
            fixed4 _RimColor;
            float _RimPower;
            float _RimStrength;
            float _EdgeGlow;
            fixed4 _FogColor;
            float4 _FogRange;
            float _FogStrength;
            float4 _Wobble;
            fixed4 _DepthTint;
            float _DepthTintStrength;

            // 全局参数（由 LadyLakeShaderGlobals 每帧 / 每次取样写入）
            float _LadyLakeTime;
            float _LadyLakeIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 meta : TEXCOORD1;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 meta : TEXCOORD1;
                float3 wnormal : TEXCOORD2;
                float3 wpos : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.meta = v.meta;
                o.wnormal = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 轻微水折射：UV 抖动随时间流动（时间来自 _LadyLakeTime，Editor 取样与 Play 一致）
                float2 wob = _Wobble.xy * float2(
                    sin(_LadyLakeTime * _Wobble.z + i.wpos.y * 6.0),
                    cos(_LadyLakeTime * _Wobble.w + i.wpos.x * 5.0));

                if (_MaskHands > 0.5)
                {
                    if (i.uv.x > 0.28 && i.uv.x < 0.60 && i.uv.y > 0.835) discard;
                    if (i.uv.x > 0.68 && i.uv.y < 0.235) discard;
                }
                fixed4 tex = tex2D(_MainTex, i.uv + wob);
                // This pass shares the complete figure texture. Only fingertips
                // and the inner thumb cover the hilt; the intact palm stays behind it.
                if (_FingerOcclusion > .5)
                {
                    float top = 1 - i.uv.y;
                    if (top < .859 && (i.uv.x > .852 || top < .831)) discard;
                }

                // 软边抠像：cutoff 之上留一段平滑过渡，避免锯齿边与白/黑描边
                float a = smoothstep(_Cutoff, _Cutoff + _Softness, tex.a);
                if (_WristFadePlane.w > 0)
                {
                    float2 pixel = float2(i.uv.x, 1-i.uv.y) * _MainTex_TexelSize.zw;
                    a *= smoothstep(0, _WristFadePlane.w, dot(pixel, _WristFadePlane.xy) + _WristFadePlane.z);
                }
                clip(a - 0.004);

                float3 n = normalize(i.wnormal);
                float3 l = normalize(_LightDir.xyz);
                float lambert = saturate(dot(n, l));
                float wrapped = saturate(dot(n, l) * 0.5 + 0.5);
                float shade = lerp(wrapped, lambert, 0.65) * _LightStrength + _Ambient;

                float3 viewDir = normalize(_WorldSpaceCameraPos - i.wpos);
                float rim = pow(saturate(1.0 - saturate(dot(n, viewDir))), _RimPower) * _RimStrength;

                // +z 更远 -> 越深越接近水雾色
                float depthT = saturate((i.wpos.z - _FogRange.x) / max(1e-4, _FogRange.y - _FogRange.x));
                float fog = depthT * _FogStrength;

                float3 col = tex.rgb * _Color.rgb * shade;
                col = lerp(col, col * _DepthTint.rgb, _DepthTintStrength);
                col += _RimColor.rgb * rim;
                col += _RimColor.rgb * _EdgeGlow * saturate(i.meta.x) * (0.6 + 0.4 * _LadyLakeIntensity);
                col = lerp(col, _FogColor.rgb, fog);

                return fixed4(col, saturate(a * _Color.a));
            }
            ENDCG
        }
    }

    Fallback "Unlit/Transparent"
}
