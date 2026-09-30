// Env Sandbox only (2026-10-01): stylized lava for basins, after MinionsArt's stylized lava
// breakdown (reimplemented): a world-projected noise scrolled in two layers distorts a second
// noise; its brightest part is smoothstepped into glowing cracks over a dark cooling crust. The
// edge glow where lava meets the bank or a rock comes from the camera depth texture.
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

            TEXTURE2D(_BasinMask); SAMPLER(sampler_BasinMask);
            TEXTURE2D(_NoiseTex);  SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ZoneRect;
                float4 _CrustColor, _HotColor, _CoreColor, _EdgeColor;
                float  _Scale, _DistortScale, _Distortion;
                float4 _Flow;
                float  _CrackCut, _CrackSoft, _EdgeWidth, _Pulse, _Brightness;
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

            float N(float2 uv) { return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv).r; }

            half4 Fragment(V i) : SV_Target
            {
                float2 xz = i.w.xz;
                float t = _Time.y * _Flow.z;
                // Two scrolling distortion layers (different scales and directions) bend the lava noise.
                float d1 = N(xz / _DistortScale + _Flow.xy * t);
                float d2 = N(xz / (_DistortScale * 1.9) - _Flow.yx * t * 0.7 + 0.31);
                float2 distort = (float2(d1, d2) - 0.5) * _Distortion;
                float lava = N(xz / _Scale + distort + _Flow.xy * t * 0.4);
                float lava2 = N(xz / (_Scale * 0.45) - distort * 1.5 + 0.57);
                float v = lava * 0.7 + lava2 * 0.3;

                // Dark crust, hot cracks where the noise is brightest, a white-hot core inside them.
                float hot = smoothstep(_CrackCut - _CrackSoft, _CrackCut + _CrackSoft, v);
                float core = smoothstep(_CrackCut + 0.12, _CrackCut + 0.2, v);
                half3 col = lerp(_CrustColor.rgb, _HotColor.rgb, hot);
                col = lerp(col, _CoreColor.rgb, core);
                col *= 1.0 + _Pulse * sin(_Time.y * 1.7 + lava * 6.2831);

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
