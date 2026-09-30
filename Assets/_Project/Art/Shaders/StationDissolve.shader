Shader "HordeCall/Station/Dissolve"
{
    // Phase A9b: a station body while it appears or leaves (about a second either way). At rest the
    // body keeps the toon-lit palette material; only the transition swaps to this one.
    //  - Dissolves from the bottom up, broken up by the packed FX noise (G), with a hard glowing
    //    edge in the station's colour just above the cut.
    //  - Lighting is a two-band toon step of the main light plus SH ambient: enough for one second.
    //  - Object-space height: the model's pivot sits on its base, _Height is its top.
    Properties
    {
        _BaseMap ("Palette", 2D) = "white" {}
        _NoiseTex ("Packed FX (noise in G)", 2D) = "gray" {}
        _Dissolve ("Dissolve 0..1", Range(0, 1)) = 0
        _Height ("Model height (m)", Float) = 1
        _NoiseScale ("Noise tiling (per metre)", Float) = 0.8
        _NoiseAmount ("Noise in the cut (m)", Float) = 0.35
        _EdgeWidth ("Edge band (m)", Float) = 0.08
        [HDR] _EdgeColor ("Edge colour (set per station)", Color) = (0.4, 0.9, 1, 1)
        _ShadowTint ("Shadow tint", Color) = (0.55, 0.6, 0.72, 1)
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NoiseTex_ST;
                float4 _EdgeColor;
                float4 _ShadowTint;
                float _Dissolve, _Height, _NoiseScale, _NoiseAmount, _EdgeWidth;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 w : TEXCOORD2; float h : TEXCOORD3; };

            V vert (A i)
            {
                V o;
                o.w = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.w);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.n = TransformObjectToWorldNormal(i.n);
                o.h = i.pos.y;
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.w.xz * _NoiseScale + i.w.y * 0.37).g;
                // The cut rises from below the base to above the top as _Dissolve goes 0 -> 1.
                float cut = lerp(-_NoiseAmount - _EdgeWidth, _Height + _NoiseAmount, _Dissolve);
                float v = i.h + (noise - 0.5) * 2.0 * _NoiseAmount;
                clip(v - cut);
                float edge = 1.0 - saturate((v - cut) / max(_EdgeWidth, 1e-3));

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                Light l = GetMainLight();
                half ndl = dot(normalize(i.n), l.direction);
                half lit = step(0.0, ndl);
                half3 shade = lerp(_ShadowTint.rgb, 1.0, lit) * l.color;
                half3 rgb = albedo * (shade * 0.85 + SampleSH(normalize(i.n)) + 0.2);   // matched by eye to the toon-lit body at rest
                rgb = lerp(rgb, _EdgeColor.rgb, edge);
                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }
}
