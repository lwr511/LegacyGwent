// 剑正面：sword.png + 沿真实剑边（顶点烘焙的 edgeProximity）的暖金脉冲 + 护手附近 rune 光。
// 不是粗荧光棒：发光只出现在轮廓内侧很窄的一圈，并且被 alpha 剪影严格限制在剑身内部。
Shader "LegacyGwent/LadyLake/Sword"
{
    Properties
    {
        _MainTex ("Sword (RGBA)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.35
        _Softness ("Edge Softness", Range(0.001,0.9)) = 0.3
        _LightDir ("Fake Light Dir", Vector) = (0.35, 0.72, -0.6, 0)
        _LightStrength ("Fake Light", Range(0,2)) = 0.55
        _Ambient ("Ambient", Range(0,2)) = 0.7
        _GlowColor ("Blade Glow Color", Color) = (1.0, 0.80, 0.40, 1)
        _EdgeGlow ("Edge Glow", Range(0,4)) = 1.5
        _EdgePower ("Edge Power", Range(0.5,6)) = 2.0
        _RuneColor ("Rune Color", Color) = (1.0, 0.93, 0.62, 1)
        _RuneV ("Rune V", Range(0,1)) = 0.36
        _RuneSpread ("Rune Spread", Range(1,40)) = 9
        _RuneStrength ("Rune Strength", Range(0,4)) = 1.1
        _PulseSpeed ("Pulse Speed", Range(0,8)) = 2.1
        _PulseAmount ("Pulse Amount", Range(0,1)) = 0.35
        _PulseCycles ("Pulse Cycles (integer, per loop)", Range(1,8)) = 3
        _FogColor ("Water Fog Color", Color) = (0.02, 0.10, 0.11, 1)
        _FogRange ("Fog Z Near/Deep", Vector) = (-0.45, 0.10, 0, 0)
        _FogStrength ("Fog Strength", Range(0,1)) = 0.5

        // 以下三项由 LadyLakeSwordRig 通过 MaterialPropertyBlock 逐帧写入；
        // 必须声明在 Properties 里，MaterialPropertyBlock 才会生效。
        _GlowStrength ("Glow Strength", Range(0,3)) = 0.2
        _WakeStrength ("Wake Strength", Range(0,2)) = 0
        _HeldPhase ("Held Phase", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Back
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
            float _Softness;
            float4 _LightDir;
            float _LightStrength;
            float _Ambient;
            fixed4 _GlowColor;
            float _EdgeGlow;
            float _EdgePower;
            fixed4 _RuneColor;
            float _RuneV;
            float _RuneSpread;
            float _RuneStrength;
            float _PulseSpeed;
            float _PulseAmount;
            float _PulseCycles;
            fixed4 _FogColor;
            float4 _FogRange;
            float _FogStrength;

            // _LadyLakeTime = 连续环境时间（水流），_LadyLakeLoop = 0..1 动作循环相位
            float _LadyLakeTime;
            float _LadyLakeLoop;
            float _LadyLakeIntensity;

            // 由 LadyLakeSwordRig 通过 MaterialPropertyBlock 写入
            float _GlowStrength;
            float _WakeStrength;
            float _HeldPhase;

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
                fixed4 tex = tex2D(_MainTex, i.uv);
                float a = smoothstep(_Cutoff, _Cutoff + _Softness, tex.a);
                clip(a - 0.004);

                float3 n = normalize(i.wnormal);
                float3 l = normalize(_LightDir.xyz);
                float shade = (saturate(dot(n, l)) * 0.5 + 0.5) * _LightStrength + _Ambient;

                float edge = pow(saturate(i.meta.x), _EdgePower);
                // 脉冲用动作循环相位 + 整数周期 -> 循环首尾完全一致，不会在回卷时跳变
                float pulsePhase = _LadyLakeLoop * 6.2831853 * _PulseCycles + i.wpos.y * 2.0;
                float pulse = 1.0 - _PulseAmount + _PulseAmount * sin(pulsePhase);

                float glow = edge * _EdgeGlow * _GlowStrength * pulse;
                float wake = edge * _WakeStrength * 0.10;

                float inside = saturate(1.0 - i.meta.x);
                float runeBand = exp(-pow((i.uv.y - _RuneV) * _RuneSpread, 2.0));
                float rune = runeBand * inside * _RuneStrength * _GlowStrength * pulse * _HeldPhase;

                float depthT = saturate((i.wpos.z - _FogRange.x) / max(1e-4, _FogRange.y - _FogRange.x));
                float fog = depthT * _FogStrength;

                float3 col = tex.rgb * _Color.rgb * shade;
                col += _GlowColor.rgb * (glow + wake);
                col += _RuneColor.rgb * rune;
                col = lerp(col, _FogColor.rgb, fog);

                return fixed4(col, saturate(a * _Color.a + (glow + wake + rune) * 0.35));
            }
            ENDCG
        }
    }

    Fallback "Unlit/Transparent"
}
