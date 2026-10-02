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
// Body: EnvWaterCore.hlsl. The maps use their own cut-down shaders (Shaders/Maps/).
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

        _FoamNoise ("Noisy intersection foam", Range(0,1)) = 0
        _FoamNoiseScale ("Foam noise scale (m)", Float) = 1.6
        _FoamNoiseDist ("Foam reach (m of water)", Range(0.01,1.5)) = 0.45

        _ScumAmount ("Floating flecks (duckweed, leaves)", Range(0,1)) = 0
        _ScumScale ("Fleck spacing (m)", Float) = 0.45
        _ScumColor ("Fleck colour", Color) = (0.42,0.6,0.18,1)
        _ScumColor2 ("Fleck second colour", Color) = (0.3,0.48,0.14,1)

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
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma shader_feature_local _REFRACT
            #if defined(_REFRACT)
            #define WATER_REFRACT
            #endif
            #define WATER_WAVES
            #define WATER_CLEAR
            #define WATER_CAUSTICS
            #define WATER_FOAM_NOISE
            #define WATER_BUBBLES
            #define WATER_GLOW
            #define WATER_SCUM
            #define WATER_ICE
            #include "Assets/_Project/Art/EnvSandbox/Shaders/EnvWaterCore.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
