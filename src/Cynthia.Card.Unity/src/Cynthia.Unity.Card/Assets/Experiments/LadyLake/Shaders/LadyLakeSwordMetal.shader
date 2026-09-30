// 剑的背面与侧壁：暗银金属。与正面共用同一套挤出网格的另外两个 submesh，
// 因此从背面 / 侧面看是一把有厚度的剑，而不是第二把剑。
Shader "LegacyGwent/LadyLake/SwordMetal"
{
    Properties
    {
        _Color ("Metal Color", Color) = (0.19, 0.21, 0.24, 1)
        _SpecColor2 ("Specular Color", Color) = (0.75, 0.82, 0.86, 1)
        _Shininess ("Shininess", Range(1,128)) = 42
        _LightDir ("Fake Light Dir", Vector) = (0.35, 0.72, -0.6, 0)
        _Ambient ("Ambient", Range(0,1)) = 0.28
        _GlowColor ("Edge Glow Color", Color) = (1.0, 0.80, 0.40, 1)
        _EdgeGlow ("Edge Glow", Range(0,2)) = 0.25
        _FogColor ("Water Fog Color", Color) = (0.02, 0.10, 0.11, 1)
        _FogRange ("Fog Z Near/Deep", Vector) = (-0.45, 0.10, 0, 0)
        _FogStrength ("Fog Strength", Range(0,1)) = 0.5

        // 由 LadyLakeSwordRig 通过 MaterialPropertyBlock 逐帧写入
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

            fixed4 _Color;
            fixed4 _SpecColor2;
            float _Shininess;
            float4 _LightDir;
            float _Ambient;
            fixed4 _GlowColor;
            float _EdgeGlow;
            fixed4 _FogColor;
            float4 _FogRange;
            float _FogStrength;

            float _LadyLakeTime;
            float _LadyLakeLoop;
            float _LadyLakeIntensity;

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
                float2 meta : TEXCOORD1;
                float3 wnormal : TEXCOORD2;
                float3 wpos : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.meta = v.meta;
                o.wnormal = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.wnormal);
                float3 l = normalize(_LightDir.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);

                float diffuse = saturate(dot(n, l));
                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(n, h)), _Shininess) * 0.8;

                float pulse = 0.75 + 0.25 * sin(_LadyLakeLoop * 6.2831853 * 3.0 + i.wpos.y * 2.0);
                float glow = _EdgeGlow * _GlowStrength * pulse;

                float depthT = saturate((i.wpos.z - _FogRange.x) / max(1e-4, _FogRange.y - _FogRange.x));
                float fog = depthT * _FogStrength;

                float3 col = _Color.rgb * (diffuse * 0.85 + _Ambient);
                col += _SpecColor2.rgb * spec;
                col += _GlowColor.rgb * glow;
                col = lerp(col, _FogColor.rgb, fog);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}
