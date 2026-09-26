Shader "ZombieWar/FX/SkillShield"
{
    // M8 Kinetic Shield (owner: "transparent inside, rendered on the outside, not inside").
    // A sphere around the player that is clear in the middle and glows only at its silhouette
    // (fresnel), with a faint hex lattice riding that rim. Back faces are culled, so the inside of
    // the bubble is never drawn over the player. _Flash lights the whole shell for the block moment.
    Properties
    {
        [HDR] _Color  ("Rim colour (HDR)", Color) = (0.62, 0.45, 1.6, 1)
        _RimPower     ("Rim power", Range(0.5, 8)) = 2.6
        _RimAlpha     ("Rim alpha", Range(0, 1)) = 0.85
        _HexScale     ("Hex scale", Float) = 9
        _HexLine      ("Hex line width", Range(0.01, 0.3)) = 0.07
        _HexAlpha     ("Hex alpha (on the rim)", Range(0, 1)) = 0.45
        _Flash        ("Flash", Range(0, 1)) = 0
        _Fade         ("Fade (0 hidden, 1 shown)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _RimPower, _RimAlpha, _HexScale, _HexLine, _HexAlpha, _Flash, _Fade;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; float3 op : TEXCOORD2; };

            V vert (A i)
            {
                V o;
                float3 wp = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(wp);
                o.n = TransformObjectToWorldNormal(i.n);
                o.v = GetWorldSpaceViewDir(wp);
                o.op = i.pos.xyz;
                return o;
            }

            // Distance to the nearest hex edge in a hex grid (0 on the edge).
            float HexEdge (float2 p)
            {
                const float2 s = float2(1.0, 1.7320508);
                float4 hc = floor(float4(p, p - float2(0.5, 1.0)) / s.xyxy) + 0.5;
                float4 h = float4(p - hc.xy * s, p - (hc.zw + 0.5) * s);
                float2 q = dot(h.xy, h.xy) < dot(h.zw, h.zw) ? h.xy : h.zw;
                q = abs(q);
                return 0.5 - max(dot(q, s * 0.5), q.x);
            }

            half4 frag (V i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 v = normalize(i.v);
                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower);

                // Hex lattice from the sphere's own direction (no UV seam at the poles that matters here).
                float3 d = normalize(i.op);
                float2 hp = float2(atan2(d.z, d.x) / 6.2831853 + 0.5, d.y * 0.5 + 0.5) * float2(_HexScale * 2.0, _HexScale);
                float hex = 1.0 - smoothstep(0.0, _HexLine, HexEdge(hp));

                float a = rim * _RimAlpha + hex * _HexAlpha * (0.25 + rim) + _Flash * (0.35 + rim);
                a *= _Fade;
                half3 rgb = _Color.rgb * (0.6 + rim) + _Flash;
                return half4(rgb, saturate(a));
            }
            ENDHLSL
        }
    }
}
