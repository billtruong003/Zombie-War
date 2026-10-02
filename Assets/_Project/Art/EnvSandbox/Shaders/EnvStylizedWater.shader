// Env Sandbox (2026-10-01, see-through pass 2026-10-02): stylized water for basins, one shader for
// water, toxic and ice. Toon-water recipe (MinionsArt, Roystan): water depth from the camera depth
// texture (scene depth minus the surface's own depth, in metres) drives shallow → deep colour,
// clarity and the shore foam, so foam also rings every rock or leg that breaks the surface.
//  - Clarity: the surface is blended over the basin floor, clear where shallow and closing up with
//    depth (1 - e^(-depth / clarity)). Clarity 0 = the old opaque surface.
//  - _REFRACT (option): the floor is read from the camera opaque texture with a wobble instead of
//    blended, and tinted by the water it is seen through. Needs the camera opaque texture.
//  - Caustics: moving light lines on the floor, from procedural cells (no texture repeat).
//  - Foam: a thin intersection line plus an optional soft band.
//  - Noise reads are anti-tiled (two rotated reads, warped), so the streaks never show a period.
//  - Toxic: an emissive glow and procedural bubbles that swell and pop (EnvFluidCommon).
//  - Ice: waves and flow at 0, a crack mask over the surface, the foam line reads as snow.
Shader "HordeCall/EnvSandbox/Stylized Water"
{
    Properties
    {
        [NoScaleOffset] _BasinMask ("Basin mask (white = deep)", 2D) = "black" {}
        _ZoneRect ("Zone (origin x, origin z, size)", Vector) = (0,0,40,0)
        [NoScaleOffset] _NoiseTex ("Noise (grayscale, tileable)", 2D) = "gray" {}

        _ShallowColor ("Shallow", Color) = (0.35,0.8,0.85,1)
        _DeepColor ("Deep", Color) = (0.1,0.35,0.6,1)
        _DepthRange ("Depth that reads as deep (m)", Range(0.05,3)) = 0.5
        _Clarity ("Clarity: depth at which the floor fades out (m, 0 = opaque)", Range(0,3)) = 0
        _MinAlpha ("Surface tint at the very edge", Range(0,1)) = 0.15
        [Toggle(_REFRACT)] _Refract ("Refraction (camera opaque texture)", Float) = 0
        _RefractStrength ("Refraction wobble", Range(0,0.1)) = 0.025
        _Caustics ("Caustics on the floor", Range(0,1)) = 0
        _CausticScale ("Caustic cell size (m)", Float) = 1.4
        _CausticColor ("Caustic colour", Color) = (0.85,1,1,1)

        _FoamColor ("Shore foam", Color) = (0.95,0.98,1,1)
        _FoamWidth ("Foam line (m of water)", Range(0,0.6)) = 0.12
        _FoamSoft ("Soft foam band (m of water)", Range(0,0.6)) = 0
        _FoamWobble ("Foam wobble", Range(0,0.2)) = 0.05

        _StreakColor ("Streaks", Color) = (0.75,0.95,1,1)
        _StreakScale ("Streak noise scale (m)", Float) = 6
        _StreakCut ("Streak threshold", Range(0,1)) = 0.72
        _StreakAlpha ("Streak strength", Range(0,1)) = 0.55
        _Flow ("Flow direction (xy) and speed (z)", Vector) = (0.3,0.15,0.05,0)

        _WaveHeight ("Wave height (m)", Range(0,0.2)) = 0.04
        _WaveScale ("Wave scale (m)", Float) = 5
        _WaveSpeed ("Wave speed", Float) = 1

        [HDR] _Emission ("Emission (toxic glow)", Color) = (0,0,0,1)
        _BubbleAmount ("Bubbles", Range(0,1)) = 0
        _BubbleScale ("Bubble spacing (m)", Float) = 2.2
        _BubbleRate ("Bubble lives per second", Float) = 0.35
        _BubbleColor ("Bubble colour", Color) = (0.8,1,0.4,1)

        [NoScaleOffset] _CrackTex ("Crack mask (ice)", 2D) = "black" {}
        _CrackTiling ("Crack tiling (m)", Float) = 6
        _CrackAmount ("Cracks", Range(0,1)) = 0
        _CrackColor ("Crack colour", Color) = (0.55,0.7,0.85,1)

        _LightWrap ("Light wrap", Range(0,1)) = 0.7
        _AmbientFallback ("Ambient fallback", Color) = (0.78,0.78,0.82,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma shader_feature_local _REFRACT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Assets/_Project/Art/Shaders/ToonLightContract.hlsl"
            #include "Assets/_Project/Art/EnvSandbox/Shaders/EnvFluidCommon.hlsl"
            #include "Assets/_Project/Art/Shaders/ToonPointLights.hlsl"

            TEXTURE2D(_BasinMask); SAMPLER(sampler_BasinMask);
            TEXTURE2D(_NoiseTex);  SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_CrackTex);  SAMPLER(sampler_CrackTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ZoneRect;
                float4 _ShallowColor, _DeepColor, _FoamColor, _StreakColor, _Emission, _BubbleColor, _CrackColor, _CausticColor;
                float  _DepthRange, _Clarity, _MinAlpha, _RefractStrength, _Caustics, _CausticScale;
                float  _FoamWidth, _FoamSoft, _FoamWobble, _StreakScale, _StreakCut, _StreakAlpha;
                float4 _Flow;
                float  _WaveHeight, _WaveScale, _WaveSpeed, _BubbleAmount, _BubbleScale, _BubbleRate;
                float  _CrackTiling, _CrackAmount, _LightWrap;
                half4  _AmbientFallback;
            CBUFFER_END

            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 w : TEXCOORD0; float4 screen : TEXCOORD1; };

            float Noise(float2 xz, float scale, float2 scroll)
            {
                return EnvAntiTileNoise(TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), xz, scale, scroll * scale);
            }

            // Water depth in metres along the view ray at a screen position.
            float WaterDepth(float2 suv, float surfaceEye)
            {
                return max(0.0, LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams) - surfaceEye);
            }

            V Vertex(A i)
            {
                V o;
                float3 w = TransformObjectToWorld(i.pos.xyz);
                float t = _Time.y * _WaveSpeed;
                w.y += (Noise(w.xz, _WaveScale, float2(t * 0.03, t * 0.02)) - 0.5) * 2.0 * _WaveHeight;
                o.w = w;
                o.pos = TransformWorldToHClip(w);
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            half4 Fragment(V i) : SV_Target
            {
                float2 xz = i.w.xz;
                float2 suv = i.screen.xy / i.screen.w;
                float depth = WaterDepth(suv, i.screen.w);
                float2 flow = _Flow.xy * _Flow.z * _Time.y;

                // Shallow → deep in two toon steps (hard edges read better than a smooth gradient).
                float d = saturate(depth / _DepthRange);
                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.35, 0.45, d) * 0.6 + smoothstep(0.75, 0.85, d) * 0.4);

                // Scrolling streaks: two anti-tiled noise layers crossing, thresholded into flat shapes.
                float n1 = Noise(xz, _StreakScale, flow);
                float n2 = Noise(xz + 13.7, _StreakScale * 1.7, -flow * 0.7);
                float streak = step(_StreakCut, n1 * 0.6 + n2 * 0.4) * _StreakAlpha;

                // Bubbles (toxic): procedural, each with its own life; swell, then pop into a ring.
                float3 bub = _BubbleAmount > 0.001 ? EnvBubbles(xz, _BubbleScale, _BubbleRate, _BubbleAmount) : 0;
                float bubble = saturate(bub.x * 0.75 + bub.y + bub.z);

                // Ice cracks.
                float crack = SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, xz / max(_CrackTiling, 0.001)).r * _CrackAmount;

                // Shore foam: a thin line where the water meets the bank or a rock, wobbling with the noise,
                // and an optional softer band behind it.
                float wob = (Noise(xz, 3.0, flow * 1.5) - 0.5) * 2.0 * _FoamWobble;
                float foamLine = 1.0 - step(_FoamWidth + wob, depth);
                float foamBand = _FoamSoft > 0.001 ? (1.0 - smoothstep(_FoamWidth, _FoamWidth + _FoamSoft, depth + wob)) * 0.45 : 0.0;
                float foam = max(foamLine, foamBand);

                float3 lightDir; half3 lightColor;
                bool directional = ZW_ResolveToonLight(_AmbientFallback.rgb, lightDir, lightColor);
                float ndotl = saturate(lightDir.y);
                half lit = directional ? lerp(ndotl, ndotl * 0.5 + 0.5, _LightWrap) : 1.0;
                half3 light = lightColor * lit + SampleSH(float3(0, 1, 0));
                light += ZW_ToonPointLights(i.w, half3(0, 1, 0), 1.0h);

                // How much of the floor still shows through the water above it.
                float clarity = _Clarity > 0.001 ? exp(-depth / _Clarity) : 0.0;

                // Caustics: light lines on the floor, fading with depth. The floor position comes back
                // from the depth texture, so they lie on the basin bottom, not on the surface.
                half3 caustic = 0;
                if (_Caustics > 0.001)
                {
                    float3 floorWS = ComputeWorldSpacePosition(suv, SampleSceneDepth(suv), UNITY_MATRIX_I_VP);
                    float a1, b1, a2, b2; float2 id, tc;
                    EnvVoronoi(floorWS.xz / _CausticScale + _Time.y * 0.12, 1.0, a1, b1, id, tc);
                    EnvVoronoi(floorWS.xz / (_CausticScale * 1.37) - _Time.y * 0.09 + 3.3, 1.0, a2, b2, id, tc);
                    float lines = (1.0 - smoothstep(0.0, 0.08, b1 - a1)) * (1.0 - smoothstep(0.0, 0.12, b2 - a2));
                    lines = saturate(lines * 1.6 + (1.0 - smoothstep(0.0, 0.05, b1 - a1)) * 0.35);
                    caustic = _CausticColor.rgb * lines * _Caustics * clarity;
                }

                half3 surface = lerp(col, _StreakColor.rgb, streak);
                surface = lerp(surface, _BubbleColor.rgb, bubble);
                surface = lerp(surface, _CrackColor.rgb, crack);
                half3 litSurface = surface * light;
                half3 glow = _Emission.rgb * (1.0 - foam) + _BubbleColor.rgb * bubble * 0.4;

            #if defined(_REFRACT)
                // The floor seen through the water: wobbled, but never pulled from something standing
                // in front of the surface (a leg, a bridge plank) — fall back to the straight read then.
                float2 wobble = (float2(n1, n2) - 0.5) * _RefractStrength * saturate(depth * 3.0);
                float2 ruv = suv + wobble;
                if (LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams) < i.screen.w) ruv = suv;
                half3 floorCol = SampleSceneColor(ruv) * lerp(_DeepColor.rgb, _ShallowColor.rgb, clarity * 0.5 + 0.5) + caustic;
                half3 rgb = lerp(litSurface, floorCol, clarity * (1.0 - max(streak, bubble)) * (1.0 - crack));
                rgb = lerp(rgb, _FoamColor.rgb * light, foam);
                return half4(rgb + glow, 1.0h);
            #else
                // Blended: the surface tint closes up with depth; streaks, bubbles, cracks and foam stay solid.
                half3 rgb = lerp(litSurface, _FoamColor.rgb * light, foam);
                float alpha = _Clarity > 0.001 ? lerp(1.0, _MinAlpha, clarity) : 1.0;
                alpha = max(alpha, max(max(streak, bubble), max(crack, foam)));
                // Caustics light the floor where it shows through: scaled so that, after the alpha
                // blend, they land on the floor's share of the pixel.
                rgb += caustic * (1.0 - alpha) / max(alpha, 0.1);
                return half4(rgb + glow, alpha);
            #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
