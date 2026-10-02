// Snowfall on the GPU (owner 02/10: falling snow without the CPU cost of a particle system, and
// working on WebGL and on phones without compute shaders, where VFX Graph cannot run).
// Snowfall.cs builds one mesh of N flakes once; each flake's four corners carry its random seed
// (uv1: x, z; uv2: y, size). Everything else happens here, every frame, for free on the CPU:
//  - the flake's place in a box round the player that wraps, so the snow never runs out;
//  - fall speed, wind drift and a sideways sway, each a little different per flake;
//  - facing the camera, a soft round shape, fading near the ground (it melts) and at the box edges.
Shader "HordeCall/Fx/Snowfall"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 0.95)
        _RimColor ("Rim (reads on white snow)", Color) = (0.78, 0.84, 0.95, 1)
        _Box ("Box round the player (x, y, z m)", Vector) = (26, 9, 32, 0)
        _FallSpeed ("Fall speed (m/s)", Float) = 1.6
        _Wind ("Wind (x, z m/s)", Vector) = (0.7, 0.2, 0, 0)
        _Sway ("Sway (m)", Float) = 0.35
        _Size ("Flake size (m)", Vector) = (0.06, 0.16, 0, 0)
        _GroundY ("Ground height", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _RimColor, _Box, _Wind, _Size;
                float  _FallSpeed, _Sway, _GroundY;
            CBUFFER_END
            float4 _ZWSnowCentre;   // xyz = the player, set by Snowfall.cs

            struct A { float4 pos : POSITION; float2 corner : TEXCOORD0; float2 seedXZ : TEXCOORD1; float2 seedYS : TEXCOORD2; };
            struct V { float4 pos : SV_POSITION; float2 corner : TEXCOORD0; float fade : TEXCOORD1; };

            V Vert(A i)
            {
                V o;
                float t = _Time.y;
                float3 box = _Box.xyz;
                float speed = _FallSpeed * (0.7 + 0.6 * frac(i.seedYS.x * 7.13));
                // Start position in the box, then fall and drift; wrap round the player.
                float3 p = float3(i.seedXZ.x, i.seedYS.x, i.seedXZ.y) * box;
                p.y -= t * speed;
                p.xz += t * _Wind.xy * (0.8 + 0.4 * frac(i.seedXZ.x * 3.7));
                float phase = t * (1.2 + frac(i.seedXZ.y * 5.3)) + i.seedYS.x * 40.0;
                p.x += sin(phase) * _Sway;
                p.z += cos(phase * 0.8) * _Sway * 0.6;
                float3 rel = p - _ZWSnowCentre.xyz;
                rel.xz = (frac(rel.xz / box.xz + 0.5) - 0.5) * box.xz;    // wrap round the player
                rel.y = frac((p.y - _GroundY) / box.y) * box.y;            // wrap from the ground up
                float3 centre = float3(_ZWSnowCentre.x, _GroundY, _ZWSnowCentre.z) + rel;

                // Face the camera.
                float size = lerp(_Size.x, _Size.y, i.seedYS.y);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 w = centre + (right * (i.corner.x - 0.5) + up * (i.corner.y - 0.5)) * size;
                o.pos = TransformWorldToHClip(w);
                o.corner = i.corner;
                // Melts in the last 0.4 m, thins out at the top and at the sides of the box.
                float ground = saturate(rel.y / 0.4);
                float top = 1.0 - smoothstep(0.85, 1.0, rel.y / box.y);
                float2 edge = abs(rel.xz) / (box.xz * 0.5);
                float side = 1.0 - smoothstep(0.8, 1.0, max(edge.x, edge.y));
                o.fade = ground * top * side;
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                // A white core in a cold grey-blue ring: white alone vanishes over white snow.
                float d = length(i.corner - 0.5) * 2.0;
                float a = (1.0 - smoothstep(0.75, 1.0, d)) * i.fade * _Color.a;
                half3 col = lerp(_Color.rgb, _RimColor.rgb, smoothstep(0.55, 0.8, d));
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
