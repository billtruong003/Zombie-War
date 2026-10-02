// Tundra ground (2026-10-02): snow layers, faint cold fissures, an ice-blue bed under the ice,
// trails pressed by every walker (SnowTrails, after MinionsArt's interactive snow) and sparkles.
// Body: EnvGroundCore.hlsl; only the features below are compiled in.
Shader "HordeCall/Map/Tundra Ground"
{
    Properties
    {
        [Header(Surface layers)]
        [NoScaleOffset] _DryTex   ("Dry albedo",   2D) = "white" {}
        [NoScaleOffset] _GrassTex ("Grass albedo", 2D) = "white" {}
        [NoScaleOffset] _SandTex  ("Sand albedo",  2D) = "white" {}
        [NoScaleOffset] _RockTex  ("Rock albedo",  2D) = "white" {}

        _DryTint   ("Dry tint",   Color) = (1,1,1,1)
        _GrassTint ("Grass tint", Color) = (1,1,1,1)
        _SandTint  ("Sand tint",  Color) = (1,1,1,1)
        _RockTint  ("Rock tint",  Color) = (1,1,1,1)

        // Kich thuoc mot lan lap, tinh bang MET. So lon = hoa tiet to hon.
        _DryTiling   ("Dry tiling (m)",   Float) = 6
        _GrassTiling ("Grass tiling (m)", Float) = 5
        _SandTiling  ("Sand tiling (m)",  Float) = 8
        _RockTiling  ("Rock tiling (m)",  Float) = 7

        [Header(Normal maps)]
        [Toggle(_NORMALMAP)] _UseNormalMap ("Use normal maps", Float) = 0
        [NoScaleOffset][Normal] _DryNormal   ("Dry normal",   2D) = "bump" {}
        [NoScaleOffset][Normal] _GrassNormal ("Grass normal", 2D) = "bump" {}
        [NoScaleOffset][Normal] _SandNormal  ("Sand normal",  2D) = "bump" {}
        [NoScaleOffset][Normal] _RockNormal  ("Rock normal",  2D) = "bump" {}
        _NormalStrength ("Normal strength", Range(0,2)) = 1

        [Header(Blending)]
        // >1 lam kenh troi noi bat hon, tranh canh bon mau trung binh thanh mot mang be be.
        _BlendSharpness ("Blend sharpness", Range(1,8)) = 3
        // Lam meo ranh gioi bang noise de duong chuyen khong bi tron nhu ve bang co.
        _WeightJitter ("Weight jitter", Range(0,1)) = 0.18

        [Header(Macro variation)]
        [NoScaleOffset] _NoiseTex ("Noise (grayscale, tileable)", 2D) = "gray" {}
        _MacroScale ("Macro noise scale (m)", Float) = 145
        _MacroStrength ("Macro strength", Range(0,1)) = 0.28
        _DetailScale ("Jitter noise scale (m)", Float) = 23

        [Header(Stylization)]
        _TextureStrength ("Texture detail strength", Range(0,1)) = 0.5
        _ToonSteps ("Toon light steps", Range(2,5)) = 3
        _ToonSoftness ("Toon edge softness", Range(0.001,0.2)) = 0.035

        [Header(Lighting)]
        _LightWrap ("Light wrap", Range(0,1)) = 0.55
        _AmbientBoost ("Ambient boost", Range(0,2)) = 1
        _AmbientFallback ("Ambient fallback (no rig, no light)", Color) = (0.78, 0.78, 0.82, 1)

        [Header(Debug)]
        // 0 = mat dat co texture, 1 = trong so biome tho, 2 = chi be mat troi nhat.
        _DebugMode ("Debug mode", Float) = 0
        _DryDebugColor   ("Dry debug",   Color) = (0.42, 0.32, 0.21, 1)
        _GrassDebugColor ("Grass debug", Color) = (0.27, 0.42, 0.20, 1)
        _SandDebugColor  ("Sand debug",  Color) = (0.74, 0.68, 0.45, 1)
        _RockDebugColor  ("Rock debug",  Color) = (0.44, 0.46, 0.50, 1)

        // Dat truoc cho floating origin: logic = physical + offset. M2A luon la 0.
        _WorldOriginOffset ("World origin offset", Vector) = (0,0,0,0)

        [Header(Env Sandbox)]
        [NoScaleOffset] _BasinMask ("Basin mask (white = deep)", 2D) = "black" {}
        _ZoneRect ("Zone (origin x, origin z, size)", Vector) = (0,0,40,0)
        [Toggle] _BasinFromUV ("Basin from mesh UV.x (tiles)", Float) = 0
        _BankDarken ("Bank darkening", Range(0,1)) = 0.35
        [NoScaleOffset] _CrackTex ("Crack mask (white = crack)", 2D) = "black" {}
        _CrackTiling ("Crack tiling (m)", Float) = 9
        _CrackStrength ("Crack darkening", Range(0,1)) = 0
        _CrackColor ("Crack colour", Color) = (0.08,0.05,0.05,1)
        _CrackGlow ("Crack glow near basins", Color) = (1,0.45,0.08,1)
        _CrackGlowReach ("Glow reach (mask units)", Range(0,1)) = 0
        _CrackGlowPulse ("Glow pulse speed", Float) = 1.3
        // 2026-10-02 volcanic ground: thinner cracks, lava glowing through them everywhere.
        _CrackThin ("Crack thinning (0 = as painted)", Range(0,0.95)) = 0
        _CrackGlowBase ("Crack glow away from basins", Range(0,1)) = 0
        _CrackGlowFlicker ("Crack glow flicker", Range(0,1)) = 0
        [HDR] _CrackGlowCore ("Crack glow core (thin centre)", Color) = (1,0.8,0.35,1)
        [Toggle(_CRACKS_PROC)] _CracksProc ("Procedural cracks (volcanic)", Float) = 0
        _CrackCell ("Procedural crack cell (m)", Float) = 3.5
        _CrackWidth ("Procedural crack width (cell units)", Range(0.005,0.2)) = 0.035
        _CrackFine ("Fine crack layer", Range(0,1)) = 0.6
        // 2026-10-02 see-through water: what the floor of a basin looks like under clear water.
        _BedColor ("Basin bed colour", Color) = (0.62,0.54,0.38,1)
        _BedColor2 ("Basin bed second tone", Color) = (0.45,0.42,0.33,1)
        _BedStrength ("Basin bed strength (0 = ground darkens only)", Range(0,1)) = 0
        // 2026-10-02 forest floor.
        _LitterAmount ("Fallen leaves", Range(0,1)) = 0
        _LitterScale ("Leaf spacing (m)", Float) = 0.55
        _LitterColor ("Leaf colour", Color) = (0.55,0.32,0.12,1)
        _LitterColor2 ("Leaf second colour", Color) = (0.42,0.4,0.14,1)
        _MossAmount ("Moss patches", Range(0,1)) = 0
        _MossColor ("Moss colour", Color) = (0.22,0.36,0.12,1)
        // 2026-10-02 snow (tundra).
        _SnowTrailColor ("Snow trail tint", Color) = (0.62,0.72,0.95,1)
        _SnowTrailDepth ("Snow trail relief", Range(0,8)) = 3
        _SnowTrailWobble ("Snow trail wobble (m)", Range(0,1.5)) = 0.6
        _SnowTrailBreakup ("Snow trail edge noise", Range(0,1)) = 0.6
        _SparkleAmount ("Snow sparkles", Range(0,4)) = 0
        _SparkleScale ("Sparkle cell (m)", Float) = 0.35
        _BumpStrength ("Rough normal", Range(0,2)) = 0
        _BumpScale ("Rough scale (m)", Float) = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "GroundForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex GroundVertex
            #pragma fragment GroundFragment
            #pragma target 3.0
            #define GROUND_BED
            #define GROUND_CRACK_TEX
            #define GROUND_SNOW
            #include "Assets/_Project/Art/EnvSandbox/Shaders/EnvGroundCore.hlsl"
            ENDHLSL
        }

        // Writes the ground into the camera depth texture, so water, lava and toxic can measure
        // how deep they are (URP builds that texture from a depth prepass of DepthOnly passes).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 DepthVertex(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half DepthFragment() : SV_Target { return 0; }
            ENDHLSL
        }

        // Same, for the DepthNormals prepass: the outline asks for normals, and then URP builds the
        // depth texture from DepthNormals passes instead of DepthOnly ones.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DNVertex
            #pragma fragment DNFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct DNA { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct DNV { float4 pos : SV_POSITION; float3 n : TEXCOORD0; };
            DNV DNVertex(DNA i) { DNV o; o.pos = TransformObjectToHClip(i.positionOS.xyz); o.n = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 DNFragment(DNV i) : SV_Target { return half4(NormalizeNormalPerPixel(i.n), 0.0); }
            ENDHLSL
        }
    }

    FallBack Off
}
