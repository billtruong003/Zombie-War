// Tundra ice (2026-10-02): a still, frozen surface over a blue bed, cracks, frost at the banks. No
// waves.
// Body: EnvWaterCore.hlsl; only the features below are compiled in.
Shader "HordeCall/Map/Tundra Ice"
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
            #define WATER_CLEAR
            #define WATER_FOAM_NOISE
            #define WATER_ICE
            #include "Assets/_Project/Art/EnvSandbox/Shaders/EnvWaterCore.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
