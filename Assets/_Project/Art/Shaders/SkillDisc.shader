Shader "ZombieWar/FX/SkillDisc"
{
    // M8 skill pass: a flat, soft disc on the ground — the shadow that grows under a falling bomb,
    // frost patches, charge fills. Drawn on a quad lying on the ground; colour and alpha come from
    // _Color (set per renderer through a MaterialPropertyBlock, so nothing is instanced).
    // _Ring > 0 turns the disc into a ring of that width (0..0.5 of the radius), for charge rings.
    Properties
    {
        _Color ("Colour", Color) = (0, 0, 0, 0.5)
        _Softness ("Edge softness", Range(0.01, 0.5)) = 0.35
        _Ring ("Ring width (0 = filled disc)", Range(0, 0.5)) = 0
        _Fill ("Ring fill (0..1, clockwise)", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Softness, _Ring, _Fill;
            CBUFFER_END

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            V vert (A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }

            half4 frag (V i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                float r = length(c) * 2.0;                 // 0 centre, 1 edge
                float a = 1.0 - smoothstep(1.0 - _Softness, 1.0, r);
                if (_Ring > 0.0)
                {
                    float inner = 1.0 - _Ring * 2.0;
                    a *= smoothstep(inner - 0.04, inner + 0.02, r);
                    float ang = atan2(c.x, c.y) / 6.2831853 + 0.5;   // 0..1 around, starting at the top
                    a *= step(ang, _Fill);
                }
                return half4(_Color.rgb, _Color.a * a);
            }
            ENDHLSL
        }
    }
}
