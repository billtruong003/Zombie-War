// Env Sandbox only (2026-10-01): stylized water for basins, one shader for water, toxic and ice.
// Follows the toon-water recipe (MinionsArt, Roystan): water depth from the camera depth texture
// (scene depth minus the surface's own depth, in metres) drives shallow → deep colour and the
// foam, so foam also rings every rock, root or leg that breaks the surface. Scrolling noise
// streaks and gentle waves on top. The surface renders after the depth copy (transparent queue).
//  - Toxic: green colours, an emissive glow and bubbles (noise spots that swell and pop).
//  - Ice: waves and flow at 0, a crack mask over the surface, the foam band reads as snow.
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
        _FoamColor ("Shore foam", Color) = (0.95,0.98,1,1)
        _FoamWidth ("Foam band (m of water)", Range(0,0.6)) = 0.12
        _FoamWobble ("Foam wobble", Range(0,0.2)) = 0.05

        _StreakColor ("Streaks", Color) = (0.75,0.95,1,1)
        _StreakScale ("Streak noise scale (m)", Float) = 6
        _StreakCut ("Streak threshold", Range(0,1)) = 0.72
        _Flow ("Flow direction (xy) and speed (z)", Vector) = (0.3,0.15,0.05,0)

        _WaveHeight ("Wave height (m)", Range(0,0.2)) = 0.04
        _WaveScale ("Wave scale (m)", Float) = 5
        _WaveSpeed ("Wave speed", Float) = 1

        [HDR] _Emission ("Emission (toxic glow)", Color) = (0,0,0,1)
        _BubbleAmount ("Bubbles", Range(0,1)) = 0
        _BubbleScale ("Bubble scale (m)", Float) = 2.2
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
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/_Project/Art/Shaders/ToonLightContract.hlsl"

            TEXTURE2D(_BasinMask); SAMPLER(sampler_BasinMask);
            TEXTURE2D(_NoiseTex);  SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_CrackTex);  SAMPLER(sampler_CrackTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ZoneRect;
                float4 _ShallowColor, _DeepColor, _FoamColor, _StreakColor, _Emission, _BubbleColor, _CrackColor;
                float  _DepthRange, _FoamWidth, _FoamWobble, _StreakScale, _StreakCut;
                float4 _Flow;
                float  _WaveHeight, _WaveScale, _WaveSpeed, _BubbleAmount, _BubbleScale;
                float  _CrackTiling, _CrackAmount, _LightWrap;
                half4  _AmbientFallback;
            CBUFFER_END

            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 w : TEXCOORD0; float4 screen : TEXCOORD1; };

            float Noise(float2 xz, float scale, float2 scroll)
            {
                return SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, xz / max(scale, 0.001) + scroll, 0).r;
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
                // Water depth in metres: what the depth texture sees behind the surface, minus the surface.
                float2 suv = i.screen.xy / i.screen.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float depth = max(0.0, sceneEye - i.screen.w);
                float2 flow = _Flow.xy * _Flow.z * _Time.y;

                // Shallow → deep in two toon steps (hard edges read better than a smooth gradient).
                float d = saturate(depth / _DepthRange);
                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.35, 0.45, d) * 0.6 + smoothstep(0.75, 0.85, d) * 0.4);

                // Scrolling streaks: two noise layers crossing, thresholded into flat shapes.
                float n1 = Noise(xz, _StreakScale, flow);
                float n2 = Noise(xz, _StreakScale * 1.7, -flow * 0.7 + 0.37);
                float streak = step(_StreakCut, n1 * 0.6 + n2 * 0.4);
                col = lerp(col, _StreakColor.rgb, streak * 0.55);

                // Bubbles (toxic): noise spots that grow and pop over time.
                float b = Noise(xz, _BubbleScale, float2(0.13, 0.71));
                float phase = frac(_Time.y * 0.35 + b * 5.0);
                float bubble = step(0.8 - phase * 0.12, b) * step(phase, 0.85) * _BubbleAmount;
                col = lerp(col, _BubbleColor.rgb, bubble);

                // Ice cracks.
                float crack = SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, xz / max(_CrackTiling, 0.001)).r * _CrackAmount;
                col = lerp(col, _CrackColor.rgb, crack);

                // Shore foam: a band where the basin is shallow, wobbling with the noise.
                float wob = (Noise(xz, 3.0, flow * 1.5) - 0.5) * 2.0 * _FoamWobble;
                float foam = 1.0 - step(_FoamWidth + wob, depth);
                col = lerp(col, _FoamColor.rgb, foam);

                float3 lightDir; half3 lightColor;
                bool directional = ZW_ResolveToonLight(_AmbientFallback.rgb, lightDir, lightColor);
                float ndotl = saturate(lightDir.y);
                half lit = directional ? lerp(ndotl, ndotl * 0.5 + 0.5, _LightWrap) : 1.0;
                half3 rgb = col * (lightColor * lit + SampleSH(float3(0, 1, 0)));
                return half4(rgb + _Emission.rgb * (1.0 - foam) + _BubbleColor.rgb * bubble * 0.4, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
