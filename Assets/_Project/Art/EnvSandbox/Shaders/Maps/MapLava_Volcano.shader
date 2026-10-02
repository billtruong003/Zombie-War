// Volcano lava (2026-10-02, owner round 2: two lava types after MinionsArt, hard contrast, no orange
// haze). Flat toon bands instead of gradients: black crust → deep red cooling rim → orange → yellow
// core, each step a hard edge, so the lava reads at a glance from the game camera.
//  - Molten (default): the basin is liquid; dark crust floats on it in drifting slabs with a red rim,
//    yellow-hot veins run through, bubbles swell and pop.
//  - Crust (_LAVA_CRUST): the basin has skinned over; black plates with thin white-hot seams, and
//    vents where the noise melts through, boiling.
// The bank where lava meets ground or rock is a thin hot line (camera depth texture).
// Unlit: lava is its own light. The core goes above 1 so the toon bloom catches it, not the rest.
Shader "HordeCall/Map/Volcano Lava"
{
    Properties
    {
        [KeywordEnum(Molten, Crust)] _Lava ("Lava type", Float) = 0
        [NoScaleOffset] _NoiseTex ("Noise (grayscale, tileable)", 2D) = "gray" {}
        [NoScaleOffset] _BasinMask ("Basin mask (unused, kept for the builder)", 2D) = "black" {}
        _ZoneRect ("Zone (unused, kept for the builder)", Vector) = (0,0,40,0)

        _CrustColor ("Crust", Color) = (0.035,0.025,0.025,1)
        _RimColor ("Cooling rim", Color) = (0.45,0.05,0.02,1)
        _HotColor ("Hot", Color) = (1,0.3,0.03,1)
        [HDR] _CoreColor ("Core", Color) = (1.6,1.05,0.3,1)
        [HDR] _EdgeColor ("Bank line", Color) = (1.5,0.75,0.2,1)

        _Scale ("Lava noise scale (m)", Float) = 6
        _DistortScale ("Distortion noise scale (m)", Float) = 3.5
        _Distortion ("Distortion strength", Range(0,1)) = 0.4
        _Flow ("Flow direction (xy) and speed (z)", Vector) = (0.2,0.1,0.025,0)
        _CrustCut ("Crust coverage (molten)", Range(0,1)) = 0.42
        _CoreCut ("Core above (molten)", Range(0,1)) = 0.64
        _RimWidth ("Cooling rim width", Range(0,0.2)) = 0.045
        _EdgeWidth ("Bank line (m of lava)", Range(0,0.6)) = 0.12
        _Pulse ("Pulse", Range(0,1)) = 0.12

        _PlateScale ("Plate size (m, crust)", Float) = 1.5
        _SeamWidth ("Plate seam width (crust)", Range(0.01,0.4)) = 0.08
        _VentCut ("Vents above (crust)", Range(0,1)) = 0.66

        _BubbleAmount ("Boiling bubbles", Range(0,1)) = 0.45
        _BubbleScale ("Bubble spacing (m)", Float) = 1.3
        _BubbleRate ("Bubble lives per second", Float) = 0.45
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma shader_feature_local _LAVA_MOLTEN _LAVA_CRUST
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/_Project/Art/EnvSandbox/Shaders/EnvFluidCommon.hlsl"

            TEXTURE2D(_NoiseTex);  SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_BasinMask); SAMPLER(sampler_BasinMask);
            CBUFFER_START(UnityPerMaterial)
                float4 _ZoneRect;
                float4 _CrustColor, _RimColor, _HotColor, _CoreColor, _EdgeColor;
                float  _Scale, _DistortScale, _Distortion;
                float4 _Flow;
                float  _CrustCut, _CoreCut, _RimWidth, _EdgeWidth, _Pulse;
                float  _PlateScale, _SeamWidth, _VentCut;
                float  _BubbleAmount, _BubbleScale, _BubbleRate;
            CBUFFER_END

            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 w : TEXCOORD0; float4 screen : TEXCOORD1; };

            V Vertex(A i)
            {
                V o;
                o.w = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.w);
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float N(float2 xz, float scale, float2 scroll)
            {
                return EnvAntiTileNoise(TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), xz, scale, scroll);
            }

            // A hard band edge: a pixel or so of softening only, so steps stay crisp but not jagged.
            float Step(float edge, float x) { return smoothstep(edge - 0.008, edge + 0.008, x); }

            // The molten colour of a value: orange, then the yellow core above coreCut.
            half3 Molten(float v, float coreCut)
            {
                return lerp(_HotColor.rgb, _CoreColor.rgb, Step(coreCut, v));
            }

            half3 Boil(half3 col, float2 xz, float mask)
            {
                if (_BubbleAmount < 0.001) return col;
                float3 b = EnvBubbles(xz, _BubbleScale, _BubbleRate, _BubbleAmount);
                // Flat toon dome: orange body, yellow top, a dark skin ring at the rim; then a yellow
                // splash ring.
                float body = Step(0.02, b.x) * mask;
                col = lerp(col, _HotColor.rgb, body);
                col = lerp(col, _CoreColor.rgb, Step(0.55, b.x) * mask);
                col = lerp(col, _RimColor.rgb * 0.5, Step(0.5, b.y) * mask);
                col = lerp(col, _CoreColor.rgb, Step(0.5, b.z) * mask);
                return col;
            }

            half4 Fragment(V i) : SV_Target
            {
                float2 xz = i.w.xz;
                float t = _Time.y * _Flow.z;
                float2 drift = _Flow.xy * t * _Scale;
                float d1 = N(xz, _DistortScale, drift);
                float d2 = N(xz + 7.3, _DistortScale * 1.9, -drift.yx * 0.7);
                float2 distort = (float2(d1, d2) - 0.5) * _Distortion * _Scale;
                float v = N(xz + distort, _Scale, drift * 0.4) * 0.7 + N(xz - distort * 1.5 + 3.1, _Scale * 0.45, 0) * 0.3;
                half3 col;

            #if defined(_LAVA_CRUST)
                // Skinned-over basin: plates drift with the flow, seams glow white-hot in the middle.
                float f1, f2; float2 id, tc;
                EnvVoronoi((xz + distort * 0.4 - drift * 0.5) / _PlateScale, 0.9, f1, f2, id, tc);
                float gap = f2 - f1;
                float seam = 1.0 - Step(_SeamWidth, gap);
                float seamCore = 1.0 - Step(_SeamWidth * 0.4, gap);
                float shade = 0.8 + 0.45 * EnvHash21(id);
                col = _CrustColor.rgb * shade;
                col = lerp(col, _RimColor.rgb, 1.0 - Step(_SeamWidth + _RimWidth, gap));
                col = lerp(col, _HotColor.rgb, seam);
                col = lerp(col, _CoreColor.rgb, seamCore);
                // Vents: where the noise peaks the crust has melted through and the lava boils.
                float vent = Step(_VentCut, v);
                float ventRim = Step(_VentCut - _RimWidth, v) * (1.0 - vent);
                col = lerp(col, _RimColor.rgb, ventRim);
                col = lerp(col, Molten(v, _VentCut + 0.08), vent);
                col = Boil(col, xz + distort * 0.3, vent);
            #else
                // Liquid basin: slabs of crust float where the noise is low, a red cooling rim around
                // each, molten orange elsewhere with yellow veins.
                float crust = 1.0 - Step(_CrustCut, v);
                float rim = (1.0 - Step(_CrustCut + _RimWidth, v)) * (1.0 - crust);
                float slab = 0.85 + 0.3 * N(xz * 3.1 - drift, 2.0, 0);
                col = Molten(v, _CoreCut);
                col = lerp(col, _RimColor.rgb, rim);
                col = lerp(col, _CrustColor.rgb * slab, crust);
                col = Boil(col, xz + distort * 0.3, 1.0 - crust);
            #endif

                // Hot parts breathe a little; the crust stays put.
                float hot = saturate(dot(col, half3(0.3, 0.5, 0.2)) * 2.0);
                col *= 1.0 + _Pulse * hot * sin(_Time.y * 1.7 + v * 6.2831);

                // The bank: a thin hot line where the lava meets the ground or a rock.
                float2 suv = i.screen.xy / i.screen.w;
                float depth = max(0.0, LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams) - i.screen.w);
                float wob = (N(xz, 2.0, drift) - 0.5) * 0.08;
                col = lerp(col, _EdgeColor.rgb, 1.0 - Step(_EdgeWidth + wob, depth));
                col = lerp(col, _RimColor.rgb, (1.0 - Step(_EdgeWidth * 1.8 + wob, depth)) * Step(_EdgeWidth + wob, depth));
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
