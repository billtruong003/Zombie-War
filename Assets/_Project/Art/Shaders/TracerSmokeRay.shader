Shader "ZombieWar/FX/TracerSmokeRay"
{
    // Fake "light ray" smoke streak for bullet tracers.
    // UV.y runs along the length (0 = pivot/bottom, 1 = tip). The _Dissolve param
    // (driven per-instance by MeshTracer via MaterialPropertyBlock) erases the streak
    // from the pivot upward, so the smoke "vanishes from the bottom up". Noise breaks
    // the ray into wispy smoke. Additive (SrcAlpha One) = glowing light-ray look.
    Properties
    {
        [HDR]_BaseColor      ("Color (HDR)", Color) = (1, 0.85, 0.55, 1)
        _NoiseTex            ("Noise", 2D) = "white" {}
        _Tiling              ("Noise Tiling (xy)", Vector) = (1, 3, 0, 0)
        _ScrollSpeed         ("Noise Scroll w/ dissolve (xy)", Vector) = (0, -1.2, 0, 0)
        _Dissolve            ("Dissolve (pivot->tip)", Range(0,1)) = 0
        _DissolveSoft        ("Dissolve Softness", Range(0.001,0.6)) = 0.22
        _HeadSoft            ("Head (tip) Fade", Range(0,0.6)) = 0.28
        _TailSoft            ("Tail (pivot) Fade", Range(0,0.6)) = 0.06
        _EdgeSoft            ("Side Fade (x)", Range(0,0.5)) = 0.4
        _NoiseCut            ("Noise Cutoff", Range(0,1)) = 0.22
        _NoiseCutSoft        ("Noise Cutoff Softness", Range(0.001,0.6)) = 0.4
        _Intensity           ("Intensity", Range(0,8)) = 1.6
        _Seed                ("Seed (xy offset)", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One   // additive-with-alpha -> glowing light-ray smoke
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _Tiling;
                float4 _ScrollSpeed;
                float4 _Seed;
                float  _Dissolve;
                float  _DissolveSoft;
                float  _HeadSoft;
                float  _TailSoft;
                float  _EdgeSoft;
                float  _NoiseCut;
                float  _NoiseCutSoft;
                float  _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float y = saturate(i.uv.y);            // along length: 0 pivot -> 1 tip
                float x = i.uv.x;

                // sample noise (scroll tied to dissolve progress so it "drifts" while fading)
                float2 nuv = i.uv * _Tiling.xy + _Seed.xy + _ScrollSpeed.xy * _Dissolve;
                half n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nuv).r;

                // erase from pivot upward as _Dissolve 0 -> 1
                float reveal = smoothstep(_Dissolve - _DissolveSoft, _Dissolve + _DissolveSoft, y);

                // soft ends
                float tail = smoothstep(0.0, max(_TailSoft, 1e-4), y);
                float head = smoothstep(1.0, 1.0 - max(_HeadSoft, 1e-4), y);

                // soft sides (safe if uv.x is ~constant)
                float side = smoothstep(0.0, max(_EdgeSoft, 1e-4), x)
                           * smoothstep(1.0, 1.0 - max(_EdgeSoft, 1e-4), x);

                // wispy noise breakup
                float wisp = smoothstep(_NoiseCut - _NoiseCutSoft, _NoiseCut + _NoiseCutSoft, n);

                float a = _BaseColor.a * reveal * tail * head * side * wisp * _Intensity;
                a = max(a, 0.0);

                return half4(_BaseColor.rgb, a); // Blend SrcAlpha One does col*a + dst
            }
            ENDHLSL
        }
    }
    Fallback Off
}
