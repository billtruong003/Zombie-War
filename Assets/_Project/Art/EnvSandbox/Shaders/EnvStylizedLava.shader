// Env Sandbox (2026-10-01, boiling pass 2026-10-02): stylized lava for basins, after MinionsArt's
// stylized lava breakdown (reimplemented):
//  - Molten flow: anti-tiled noise, distorted by two scrolling layers, smoothstepped into hot veins
//    over a dark cooling crust, with a white-hot core inside the veins.
//  - Crust plates (option): procedural cells of cooled rock drifting on the flow, glowing seams
//    between them.
//  - Boiling (option): procedural bubbles, each with its own life — a dome swells (bright core,
//    dark skin at its rim), pops, and a splash ring spreads and fades.
//  - The bank glows where lava meets the ground or a rock (camera depth texture).
Shader "HordeCall/EnvSandbox/Stylized Lava"
{
    Properties
    {
        [NoScaleOffset] _BasinMask ("Basin mask (white = deep)", 2D) = "black" {}
        _ZoneRect ("Zone (origin x, origin z, size)", Vector) = (0,0,40,0)
        [NoScaleOffset] _NoiseTex ("Noise (grayscale, tileable)", 2D) = "gray" {}

        _CrustColor ("Crust", Color) = (0.16,0.05,0.04,1)
        _HotColor ("Hot", Color) = (1,0.35,0.05,1)
        _CoreColor ("Core", Color) = (1,0.85,0.3,1)
        _EdgeColor ("Bank edge glow", Color) = (1,0.6,0.15,1)

        _Scale ("Lava noise scale (m)", Float) = 7
        _DistortScale ("Distortion noise scale (m)", Float) = 4
        _Distortion ("Distortion strength", Range(0,1)) = 0.35
        _Flow ("Flow direction (xy) and speed (z)", Vector) = (0.2,0.1,0.03,0)
        _CrackCut ("Crack threshold", Range(0,1)) = 0.55
        _CrackSoft ("Crack softness", Range(0.001,0.3)) = 0.08
        _EdgeWidth ("Edge band (m of lava)", Range(0,0.8)) = 0.25
        _Pulse ("Pulse", Range(0,1)) = 0.25
        _Brightness ("Brightness", Range(0.5,3)) = 1.2

        _Plates ("Crust plates", Range(0,1)) = 0
        _PlateScale ("Plate size (m)", Float) = 1.8
        _SeamWidth ("Plate seam width", Range(0.01,0.4)) = 0.12

        _BubbleAmount ("Boiling bubbles", Range(0,1)) = 0
        _BubbleScale ("Bubble spacing (m)", Float) = 1.6
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/_Project/Art/EnvSandbox/Shaders/EnvFluidCommon.hlsl"

            TEXTURE2D(_BasinMask); SAMPLER(sampler_BasinMask);
            TEXTURE2D(_NoiseTex);  SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ZoneRect;
                float4 _CrustColor, _HotColor, _CoreColor, _EdgeColor;
                float  _Scale, _DistortScale, _Distortion;
                float4 _Flow;
                float  _CrackCut, _CrackSoft, _EdgeWidth, _Pulse, _Brightness;
                float  _Plates, _PlateScale, _SeamWidth, _BubbleAmount, _BubbleScale, _BubbleRate;
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

            half4 Fragment(V i) : SV_Target
            {
                float2 xz = i.w.xz;
                float t = _Time.y * _Flow.z;
                float2 drift = _Flow.xy * t * _Scale;
                // Two scrolling distortion layers (different scales and directions) bend the lava noise.
                float d1 = N(xz, _DistortScale, drift);
                float d2 = N(xz + 7.3, _DistortScale * 1.9, -drift.yx * 0.7);
                float2 distort = (float2(d1, d2) - 0.5) * _Distortion * _Scale;
                float lava = N(xz + distort, _Scale, drift * 0.4);
                float lava2 = N(xz - distort * 1.5 + 3.1, _Scale * 0.45, 0);
                float v = lava * 0.7 + lava2 * 0.3;

                // Dark crust, hot veins where the noise is brightest, a white-hot core inside them.
                float hot = smoothstep(_CrackCut - _CrackSoft, _CrackCut + _CrackSoft, v);
                float core = smoothstep(_CrackCut + 0.12, _CrackCut + 0.2, v);
                half3 col = lerp(_CrustColor.rgb, _HotColor.rgb, hot);
                col = lerp(col, _CoreColor.rgb, core);

                // Crust plates riding the flow: cooled cells with glowing seams; the hot veins melt
                // through them.
                if (_Plates > 0.001)
                {
                    float f1, f2; float2 id, tc;
                    EnvVoronoi((xz + distort * 0.5 - drift * 0.6) / _PlateScale, 0.9, f1, f2, id, tc);
                    float seam = 1.0 - smoothstep(_SeamWidth * 0.4, _SeamWidth, f2 - f1);
                    float shade = 0.75 + 0.5 * EnvHash21(id);
                    half3 plate = _CrustColor.rgb * shade;
                    half3 seamCol = lerp(_HotColor.rgb, _CoreColor.rgb, seam * seam);
                    half3 plated = lerp(plate, seamCol, seam);
                    plated = lerp(plated, col, hot * 0.85);
                    col = lerp(col, plated, _Plates);
                }

                col *= 1.0 + _Pulse * sin(_Time.y * 1.7 + lava * 6.2831);

                // Boiling: domes swell with a bright core and a dark skin at the rim, pop, and leave a
                // splash ring.
                if (_BubbleAmount > 0.001)
                {
                    float3 b = EnvBubbles(xz + distort * 0.3, _BubbleScale, _BubbleRate, _BubbleAmount);
                    // A hot sphere: orange body, white-hot highlight near its top, a dark cooling skin
                    // at its rim; after the pop a bright splash ring.
                    float body = saturate(b.x * 2.0);
                    col = lerp(col, _HotColor.rgb, body);
                    col = lerp(col, _CoreColor.rgb, smoothstep(0.55, 0.95, b.x));
                    col += _CoreColor.rgb * pow(saturate(b.x), 8.0) * 0.6;
                    col = lerp(col, _CrustColor.rgb * 0.5, b.y);
                    col = lerp(col, _CoreColor.rgb, b.z * 0.85);
                }

                // The bank: where the basin is shallow the lava is freshly exposed and glows.
                float2 suv = i.screen.xy / i.screen.w;
                float depth = max(0.0, LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams) - i.screen.w);
                float edge = 1.0 - smoothstep(_EdgeWidth * 0.5, _EdgeWidth, depth);
                col = lerp(col, _EdgeColor.rgb, edge);

                // Lava is its own light: unlit.
                return half4(col * _Brightness, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
