// Editor-only (MapLightBaker, 2026-10-02): renders the map from the sun or from above and writes,
// per pixel, the nearest surface's distance along the view (R) and its world height (G).
// Leaves keep their alpha cut so canopies cast leafy shadows, not squares.
Shader "Hidden/HordeCall/MapLightBake"
{
    Properties
    {
        _BaseMap ("Base", 2D) = "white" {}
        _Cutoff ("Alpha cut", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float _Cutoff;
            float3 _BakeViewDir;   // the direction the bake camera looks along (world)

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 w : TEXCOORD1; };

            V Vert(A i)
            {
                V o;
                o.w = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.w);
                o.uv = i.uv;
                return o;
            }

            float4 Frag(V i) : SV_Target
            {
                if (_Cutoff > 0.001)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return float4(dot(i.w, _BakeViewDir), i.w.y, 0, 1);
            }
            ENDHLSL
        }
    }
}
