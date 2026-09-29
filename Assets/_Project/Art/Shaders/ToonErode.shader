Shader "ZombieWar/FX/ToonErode"
{
    // Phase A2: the one shader for new skill effects (shockwave rings, puffs, slashes, flames,
    // clouds), in the look of the Epic Toon FX set: flat colour, hard edges, an eaten border.
    //
    // Technique (after MinionsArt's toon erosion), sized for mobile:
    //  - ONE texture, packed: R radial blob, G tiling noise, B ring, A soft streak. _ShapeMask picks
    //    the shape channel; the noise is always G. Two samples of the same texture, no more.
    //  - The effect eats itself away over its life: erosion comes from the particle's Custom1.x
    //    (custom vertex stream TEXCOORD0.z), so one material serves every lifetime curve.
    //  - The cut is a hard step, anti-aliased with fwidth (no soft particles, no depth texture).
    //  - A thin edge band in _EdgeColor just inside the cut: the toon "rim" of a dissolving shape.
    //  - Colour and fade from the vertex colour (Color over Lifetime), additive or alpha by blend enum.
    Properties
    {
        _MainTex ("Packed FX (R blob, G noise, B ring, A streak)", 2D) = "white" {}
        _ShapeMask ("Shape channel (R,G,B,A weights)", Vector) = (1, 0, 0, 0)
        _NoiseScale ("Noise tiling", Float) = 1.5
        _NoiseScroll ("Noise scroll (xy per second)", Vector) = (0, 0.25, 0, 0)
        _NoiseAmount ("Noise bite into the shape", Range(0, 1.5)) = 0.6
        _Distort ("UV distortion by noise", Range(0, 0.5)) = 0.08
        _Erode ("Base erosion (added to Custom1.x)", Range(0, 1)) = 0.1
        _EdgeWidth ("Edge band width", Range(0, 0.4)) = 0.08
        [HDR] _EdgeColor ("Edge colour", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1
        _Tint ("Tint (meshes: set per renderer with a property block)", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5   // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10  // OneMinusSrcAlpha
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull [_Cull]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _ShapeMask;
                float4 _NoiseScroll;
                float4 _EdgeColor;
                float4 _Tint;
                float _NoiseScale, _NoiseAmount, _Distort, _Erode, _EdgeWidth, _Intensity;
            CBUFFER_END

            // uv.xy: texture coordinates; uv.z: Custom1.x erosion (particles; 0 for meshes/lines).
            struct A { float4 pos : POSITION; float4 uv : TEXCOORD0; half4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float4 uv : TEXCOORD0; half4 col : COLOR; };

            V vert (A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv.xy = TRANSFORM_TEX(i.uv.xy, _MainTex);
                o.uv.z = i.uv.z;
                o.uv.w = 0;
                o.col = i.col * _Tint;
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                float2 uv = i.uv.xy;
                half noise = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv * _NoiseScale + _NoiseScroll.xy * _Time.y).g;
                half4 packed = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + (noise - 0.5h) * _Distort);
                half shape = dot(packed, (half4)_ShapeMask);

                // The value the erosion eats: the shape, bitten by the noise.
                float v = shape - noise * _NoiseAmount * (1.0 - shape);
                float erode = saturate(_Erode + i.uv.z);
                float aa = max(fwidth(v), 1e-4);
                float alive = saturate((v - erode) / aa);
                float inner = saturate((v - erode - _EdgeWidth) / aa);
                float edge = alive - inner;

                half3 rgb = lerp(i.col.rgb, _EdgeColor.rgb, edge) * _Intensity;
                half a = alive * i.col.a;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
