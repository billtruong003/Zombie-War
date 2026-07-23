// Enemy VAT shader: vertex-animation-texture playback + banded specular, plus the two
// gameplay-driven effects ZombieBase needs - a white hit flash on damage and a dissolve on death.
//
// Deliberately UNLIT in the diffuse term. Albedo is used as-authored and the only lighting response
// is a quantised specular highlight. That is a mobile-performance decision: the diffuse toon ramp,
// rim fresnel and their tint lerps were the bulk of the per-pixel cost on a screen full of enemies,
// and the flat cartoon albedo of this art pack reads fine without them.
//
// The animated NORMAL map is still required even though there is no diffuse term - the specular
// highlight is driven by N·H, so with static bind-pose normals the highlight would sit frozen on
// the mesh while the body animates underneath it.
//
// Both _HitFlash and _Dissolve are PER-INSTANCE so a single shared material can drive a whole
// pooled horde: ZombieBase writes them through a MaterialPropertyBlock, exactly like VAT_Animator
// already does for the animation time.
Shader "ZombieWar/VAT/EnemyToon"
{
    Properties
    {
        [Header(Texture)]
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _PositionTexture ("Position Texture (VAT)", 2D) = "white" {}
        _NormalTexture ("Normal Texture (VAT, per-frame)", 2D) = "white" {}
        [Toggle] _UseAnimatedNormals ("Use Animated Normals", Float) = 0
        _PositionMin ("Position Min (Local Space)", Vector) = (0,0,0,0)
        _PositionMax ("Position Max (Local Space)", Vector) = (0,0,0,0)

        [Header(Stepped Specular)]
        _SpecSteps ("Specular Steps", Range(1, 5)) = 1.5
        _SpecSize ("Specular Size", Range(0, 1)) = 0.25
        _SpecIntensity ("Specular Intensity", Range(0, 3)) = 0.6

        [Header(Hit Flash)]
        [HDR] _HitFlashColor ("Hit Flash Color", Color) = (1, 1, 1, 1)

        [Header(Dissolve)]
        _DissolveNoiseTex ("Dissolve Noise (R)", 2D) = "white" {}
        [Toggle] _UseNoiseTex ("Use Noise Texture", Float) = 0
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1, 0.35, 0.1, 1)
        [HDR] _DissolveEdgeWidth ("Dissolve Edge Width", Range(0.001, 0.3)) = 0.08
        // Separate U and V tiling: enemy UV islands are not square, so a single scalar stretches
        // the burn pattern along whichever axis the unwrap compressed.
        _DissolveNoiseTiling ("Dissolve Noise Tiling (XY)", Vector) = (14, 14, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        // Shared declarations. Every pass includes this so the CBUFFER layout and the instancing
        // buffer stay byte-identical - a mismatch here shows up as flickering or wrong dissolve.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);         SAMPLER(sampler_MainTex);
        TEXTURE2D(_PositionTexture); SAMPLER(sampler_PositionTexture);
        TEXTURE2D(_NormalTexture);   SAMPLER(sampler_NormalTexture);
        TEXTURE2D(_DissolveNoiseTex); SAMPLER(sampler_DissolveNoiseTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _PositionMin;
            float4 _PositionMax;
            half _UseAnimatedNormals;
            half _SpecSteps;
            half _SpecSize;
            half _SpecIntensity;
            half4 _HitFlashColor;
            half4 _DissolveEdgeColor;
            half _DissolveEdgeWidth;
            float4 _DissolveNoiseTiling;
            half _UseNoiseTex;
        CBUFFER_END

        // Global do ToonLightRig push (Shader.SetGlobalVector) — hướng TỚI nguồn sáng.
        // Nằm ngoài CBUFFER vì là global, không per-material.
        float4 _ToonLightDirection;

        // Hướng sáng cho toon term: rig là authority; khi scene không có rig (vector ~0)
        // fallback về main light để mọi scene cũ vẫn render đúng.
        float3 ToonLightDir()
        {
            float3 rigDir = _ToonLightDirection.xyz;
            return dot(rigDir, rigDir) > 0.0001 ? normalize(rigDir) : normalize(_MainLightPosition.xyz);
        }

        UNITY_INSTANCING_BUFFER_START(PerInstance)
            UNITY_DEFINE_INSTANCED_PROP(float, _CurrentAnimNormalizedTime)
            UNITY_DEFINE_INSTANCED_PROP(float, _PreviousAnimNormalizedTime)
            UNITY_DEFINE_INSTANCED_PROP(float, _AnimationBlendWeight)
            UNITY_DEFINE_INSTANCED_PROP(float, _HitFlash)
            UNITY_DEFINE_INSTANCED_PROP(float, _Dissolve)
        UNITY_INSTANCING_BUFFER_END(PerInstance)

        float3 DecodeVAT(float vertexU, float timeV)
        {
            float4 enc = SAMPLE_TEXTURE2D_LOD(_PositionTexture, sampler_PositionTexture,
                         float2(vertexU, timeV), 0);
            return lerp(_PositionMin.xyz, _PositionMax.xyz, enc.xyz);
        }

        // Current animated object-space position, including the crossfade blend.
        float3 VATPosition(float vertexU)
        {
            float animT  = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _CurrentAnimNormalizedTime);
            float blendW = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _AnimationBlendWeight);
            float3 p = DecodeVAT(vertexU, animT);
            if (blendW > 0.001)
            {
                float prevT = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _PreviousAnimNormalizedTime);
                p = lerp(DecodeVAT(vertexU, prevT), p, blendW);
            }
            return p;
        }

        float3 DecodeVATNormal(float vertexU, float timeV)
        {
            float3 enc = SAMPLE_TEXTURE2D_LOD(_NormalTexture, sampler_NormalTexture,
                         float2(vertexU, timeV), 0).xyz;
            return enc * 2.0 - 1.0;
        }

        // The animated object-space normal for this frame. The baked mesh only carries its BIND-POSE
        // normals, so anything derived from them ignores the animation entirely - here that would
        // freeze the specular highlight in place while the body moves through it.
        // Falls back to the mesh normal when no normal map is bound, so a material baked by the old
        // pipeline still renders instead of going black.
        float3 VATNormal(float vertexU, float3 fallbackNormalOS)
        {
            if (_UseAnimatedNormals < 0.5) return fallbackNormalOS;

            float animT  = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _CurrentAnimNormalizedTime);
            float blendW = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _AnimationBlendWeight);
            float3 n = DecodeVATNormal(vertexU, animT);
            if (blendW > 0.001)
            {
                float prevT = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _PreviousAnimNormalizedTime);
                n = lerp(DecodeVATNormal(vertexU, prevT), n, blendW);
            }
            // Renormalise: both the bilinear tap and the crossfade lerp shorten the vector.
            return normalize(n);
        }

        // Dissolve mask. Prefers an authored noise texture so the burn pattern is art-directable
        // (swap the texture, get cracked/wispy/blocky), and falls back to cheap procedural value
        // noise when none is assigned - which keeps old materials rendering instead of popping.
        float DissolveNoise(float2 uv)
        {
            float2 tiling = max(_DissolveNoiseTiling.xy, 0.0001);

            if (_UseNoiseTex > 0.5)
                return SAMPLE_TEXTURE2D(_DissolveNoiseTex, sampler_DissolveNoiseTex, uv * tiling).r;

            float2 p = uv * tiling;
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = frac(sin(dot(i + float2(0, 0), float2(127.1, 311.7))) * 43758.5453);
            float b = frac(sin(dot(i + float2(1, 0), float2(127.1, 311.7))) * 43758.5453);
            float c = frac(sin(dot(i + float2(0, 1), float2(127.1, 311.7))) * 43758.5453);
            float d = frac(sin(dot(i + float2(1, 1), float2(127.1, 311.7))) * 43758.5453);
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        // Discards burnt-away pixels. Returns how close this pixel is to the burning edge (0..1),
        // so the colour pass can glow there. Dissolve 0 = fully solid, 1 = fully gone.
        float ApplyDissolveClip(float2 uv)
        {
            float dissolve = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Dissolve);
            if (dissolve <= 0.0001) return 0;
            float noise = DissolveNoise(uv);
            // Remap so dissolve==1 always clears every pixel regardless of the noise value.
            float threshold = dissolve * (1.0 + _DissolveEdgeWidth);
            clip(noise - threshold);
            return saturate(1.0 - (noise - threshold) / max(_DissolveEdgeWidth, 0.0001));
        }
        ENDHLSL

        // ── Forward: albedo + banded specular + hit flash + dissolve ─────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma target 3.5

            struct AppData
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 vertexIdUV : TEXCOORD1;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(AppData v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 localPos    = VATPosition(v.vertexIdUV.x);
                float3 localNormal = VATNormal(v.vertexIdUV.x, v.normalOS);

                VertexPositionInputs posIn = GetVertexPositionInputs(localPos);
                o.positionCS = posIn.positionCS;
                o.normalWS   = TransformObjectToWorldNormal(localNormal);
                o.viewDirWS  = GetWorldSpaceNormalizeViewDir(posIn.positionWS);
                o.uv         = TRANSFORM_TEX(v.uv, _MainTex);
                o.fogFactor  = ComputeFogFactor(posIn.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float edge = ApplyDissolveClip(i.uv);

                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // Unlit base - the art pack's albedo already carries its own shading.
                half3 result = albedo.rgb;

                // Banded specular: the only lighting term. Remap N·H into the highlight window,
                // then quantise it into _SpecSteps hard bands (ceil, so band 0 stays fully off).
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDirWS);
                float3 L = ToonLightDir();
                float3 H = normalize(L + V);

                half ndoth  = saturate(dot(N, H));
                half window = saturate((ndoth - (1.0 - _SpecSize)) / max(_SpecSize, 0.0001));
                half banded = ceil(window * _SpecSteps) / _SpecSteps;
                result += banded * _SpecIntensity * albedo.rgb;

                // Burning edge glows before the pixel disappears.
                result = lerp(result, _DissolveEdgeColor.rgb, edge);

                // Hit flash sits on top of everything so it reads at any light angle.
                float flash = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _HitFlash);
                result = lerp(result, _HitFlashColor.rgb, saturate(flash));

                result = MixFog(result, i.fogFactor);
                return half4(result, albedo.a);
            }
            ENDHLSL
        }

        // ── Shadow caster: same VAT deform + same dissolve clip ──────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct AppDataShadow
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 vertexIdUV : TEXCOORD1;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct V2FShadow
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            V2FShadow vertShadow(AppDataShadow v)
            {
                V2FShadow o = (V2FShadow)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 localPos = VATPosition(v.vertexIdUV.x);
                // Shadow bias is applied along the normal, so it must use the animated one too or
                // the bias would drift out of step with the deformed surface.
                float3 posWS  = TransformObjectToWorld(localPos);
                float3 normWS = TransformObjectToWorldNormal(VATNormal(v.vertexIdUV.x, v.normalOS));
                o.positionCS  = TransformWorldToHClip(
                    ApplyShadowBias(posWS, normWS, _MainLightPosition.xyz));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }

            half4 fragShadow(V2FShadow i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                ApplyDissolveClip(i.uv);   // a dissolving corpse must stop casting its shadow too
                return 0;
            }
            ENDHLSL
        }

        // ── Depth only ──────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            #pragma target 3.5

            struct AppDataDepth
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 vertexIdUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct V2FDepth
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            V2FDepth vertDepth(AppDataDepth v)
            {
                V2FDepth o = (V2FDepth)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionCS = TransformObjectToHClip(VATPosition(v.vertexIdUV.x));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 fragDepth(V2FDepth i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                ApplyDissolveClip(i.uv);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
