Shader "ZombieWar/FX/SkillLine"
{
    // M8 skill pass: the material for every skill line (lightning arcs, ground rings, telegraphs).
    // The old arc material was Opaque + ZWrite, so vertex alpha did nothing and every ring and bolt
    // blinked off at full strength instead of fading.
    //
    // Needs no texture: the soft profile across the line comes from UV.y (LineRenderer: UV.x runs
    // along the line, UV.y across it). Colour and alpha come from the vertex colour, so one
    // material serves every power and nothing is instanced at runtime.
    Properties
    {
        _Intensity ("Intensity", Range(0, 8)) = 1.4
        _Core      ("Core sharpness (higher = thinner hot centre)", Range(0.5, 12)) = 3
        _CoreBoost ("White-hot core", Range(0, 2)) = 0.6
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5   // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1   // One (additive)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
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
                float _Intensity;
                float _Core;
                float _CoreBoost;
            CBUFFER_END

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };

            V vert (A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = i.uv;
                o.col = i.col;
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                // 1 at the centre line, 0 at both edges.
                float across = 1.0 - abs(i.uv.y * 2.0 - 1.0);
                float glow = saturate(across);
                glow *= glow;
                float core = pow(saturate(across), _Core);
                half3 rgb = i.col.rgb * (glow + core) + core * _CoreBoost;
                half a = saturate((glow * 0.8 + core) * i.col.a);
                return half4(rgb * _Intensity, a);
            }
            ENDHLSL
        }
    }
}
