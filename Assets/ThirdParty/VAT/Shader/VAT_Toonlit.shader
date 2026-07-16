Shader "BillTheDev/VAT/URP_ToonLit_VAT"
{
    Properties
    {
        [Header(Texture)]
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _PositionTexture ("Position Texture (VAT)", 2D) = "white" {}
        _PositionMin ("Position Min (Local Space)", Vector) = (0,0,0,0)
        _PositionMax ("Position Max (Local Space)", Vector) = (0,0,0,0)

        [Header(Toon Shading)]
        _ToonSteps ("Cel Steps", Range(1, 8)) = 3
        _ToonSmoothing ("Step Smoothing", Range(0.001, 0.3)) = 0.04
        _ShadowColor ("Shadow Tint", Color) = (0.15, 0.1, 0.2, 1)
        _LitColor ("Lit Tint", Color) = (1, 1, 1, 1)
        _ShadowOffset ("Light Bias (dịch ranh giới sáng/tối)", Range(-0.5, 0.5)) = 0.0

        [Header(Rim Light)]
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power (Fresnel)", Range(0.5, 12)) = 4
        _RimIntensity ("Rim Brightness", Range(0, 5)) = 1.5
        _RimThreshold ("Rim Step Cutoff", Range(0, 1)) = 0.5

        [Header(Specular)]
        _SpecSize ("Specular Size", Range(0, 1)) = 0.2
        _SpecIntensity ("Specular Intensity", Range(0, 3)) = 0.8

        [Header(Emission)]
        _EmissionMask ("Emission Mask (R = Emissive)", 2D) = "black" {}
        _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 8)) = 1
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

        // ═══════════════════════════════════════════════════════════
        //  FORWARD PASS — 100% Fake Toon (chỉ lấy hướng Directional Light)
        //  KHÔNG sample shadow map, KHÔNG tính attenuation
        //  Chỉ cần: light direction → NdotL → toon steps → xong
        // ═══════════════════════════════════════════════════════════
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

            // Chỉ include Core — KHÔNG Lighting.hlsl, KHÔNG Shadows.hlsl
            // → không có shadow map sampling, không có phức tạp gì
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct AppData
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float2 vertexIdUV   : TEXCOORD1;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 viewDirWS    : TEXCOORD2;
                float  fogFactor    : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Textures
            TEXTURE2D(_MainTex);            SAMPLER(sampler_MainTex);
            TEXTURE2D(_PositionTexture);    SAMPLER(sampler_PositionTexture);
            TEXTURE2D(_EmissionMask);       SAMPLER(sampler_EmissionMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _PositionMin;
                float4 _PositionMax;
                half _ToonSteps;
                half _ToonSmoothing;
                half4 _ShadowColor;
                half4 _LitColor;
                half _ShadowOffset;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half _RimThreshold;
                half _SpecSize;
                half _SpecIntensity;
                half4 _EmissionColor;
                half _EmissionIntensity;
            CBUFFER_END

            // VAT per-instance
            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _CurrentAnimNormalizedTime)
                UNITY_DEFINE_INSTANCED_PROP(float, _PreviousAnimNormalizedTime)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimationBlendWeight)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            float3 DecodeVAT(float vertexU, float timeV)
            {
                float4 enc = SAMPLE_TEXTURE2D_LOD(_PositionTexture, sampler_PositionTexture,
                             float2(vertexU, timeV), 0);
                return lerp(_PositionMin.xyz, _PositionMax.xyz, enc.xyz);
            }

            // ─── Vertex ───
            Varyings vert(AppData v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // VAT decode
                float vU     = v.vertexIdUV.x;
                float animT  = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _CurrentAnimNormalizedTime);
                float blendW = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _AnimationBlendWeight);

                float3 localPos = DecodeVAT(vU, animT);

                if (blendW > 0.001)
                {
                    float prevT = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _PreviousAnimNormalizedTime);
                    localPos = lerp(DecodeVAT(vU, prevT), localPos, blendW);
                }

                VertexPositionInputs posIn = GetVertexPositionInputs(localPos);
                o.positionCS = posIn.positionCS;
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.viewDirWS  = GetWorldSpaceNormalizeViewDir(posIn.positionWS);
                o.uv         = TRANSFORM_TEX(v.uv, _MainTex);
                o.fogFactor  = ComputeFogFactor(posIn.positionCS.z);

                return o;
            }

            // ─── Fragment ───
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDirWS);

                // ══════ FAKE LIGHT ══════
                // _MainLightPosition.xyz = hướng directional light (URP tự cung cấp qua cbuffer)
                // Không cần GetMainLight(), không cần shadow coord, không cần gì phức tạp
                float3 L = normalize(_MainLightPosition.xyz);

                // ══════ TOON RAMP ══════
                half ndotl = dot(N, L) * 0.5 + 0.5;             // Half-Lambert → [0, 1]
                ndotl = saturate(ndotl + _ShadowOffset);         // Bias dịch ranh giới sáng/tối

                half stepped = floor(ndotl * _ToonSteps) / _ToonSteps;
                half toon = smoothstep(stepped - _ToonSmoothing,
                                       stepped + _ToonSmoothing, ndotl);

                // Shadow tint ↔ Lit tint
                half3 result = albedo.rgb * lerp(_ShadowColor.rgb, _LitColor.rgb, toon);

                // ══════ TOON SPECULAR ══════
                float3 H = normalize(L + V);
                half spec = step(1.0 - _SpecSize, saturate(dot(N, H))) * _SpecIntensity;
                result += spec * albedo.rgb;

                // ══════ RIM LIGHT ══════
                half fresnel = pow(1.0 - saturate(dot(N, V)), _RimPower);
                half rimMask = saturate(dot(N, L));              // Chỉ phía có ánh sáng
                half rim = step(_RimThreshold, fresnel * rimMask) * _RimIntensity;
                result += rim * _RimColor.rgb * albedo.rgb;

                // ══════ EMISSION ══════
                half emMask = SAMPLE_TEXTURE2D(_EmissionMask, sampler_EmissionMask, i.uv).r;
                result += emMask * albedo.rgb * _EmissionColor.rgb * _EmissionIntensity;

                // Fog
                result = MixFog(result, i.fogFactor);

                return half4(result, albedo.a);
            }
            ENDHLSL
        }

        // ═══════════════════════════════════════════════════════════
        //  SHADOW CASTER — Boss vẫn đổ bóng lên ground
        //  Dùng ApplyShadowBias() CỦA URP (không define lại)
        // ═══════════════════════════════════════════════════════════
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_PositionTexture); SAMPLER(sampler_PositionTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _PositionMin;
                float4 _PositionMax;
                half _ToonSteps;
                half _ToonSmoothing;
                half4 _ShadowColor;
                half4 _LitColor;
                half _ShadowOffset;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half _RimThreshold;
                half _SpecSize;
                half _SpecIntensity;
                half4 _EmissionColor;
                half _EmissionIntensity;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _CurrentAnimNormalizedTime)
                UNITY_DEFINE_INSTANCED_PROP(float, _PreviousAnimNormalizedTime)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimationBlendWeight)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            struct AppDataShadow
            {
                float4 positionOS : POSITION;
                float2 vertexIdUV : TEXCOORD1;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct V2FShadow
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 DecodeVAT(float vertexU, float timeV)
            {
                float4 enc = SAMPLE_TEXTURE2D_LOD(_PositionTexture, sampler_PositionTexture,
                             float2(vertexU, timeV), 0);
                return lerp(_PositionMin.xyz, _PositionMax.xyz, enc.xyz);
            }

            V2FShadow vertShadow(AppDataShadow v)
            {
                V2FShadow o = (V2FShadow)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float vU     = v.vertexIdUV.x;
                float animT  = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _CurrentAnimNormalizedTime);
                float blendW = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _AnimationBlendWeight);

                float3 localPos = DecodeVAT(vU, animT);

                if (blendW > 0.001)
                {
                    float prevT = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _PreviousAnimNormalizedTime);
                    localPos = lerp(DecodeVAT(vU, prevT), localPos, blendW);
                }

                // Transform + shadow bias (dùng hàm URP có sẵn, KHÔNG define lại)
                float3 posWS  = TransformObjectToWorld(localPos);
                float3 normWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS  = TransformWorldToHClip(
                    ApplyShadowBias(posWS, normWS, _MainLightPosition.xyz)
                );

                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return o;
            }

            half4 fragShadow(V2FShadow i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ═══════════════════════════════════════════════════════════
        //  DEPTH ONLY
        // ═══════════════════════════════════════════════════════════
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_PositionTexture); SAMPLER(sampler_PositionTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _PositionMin;
                float4 _PositionMax;
                half _ToonSteps;
                half _ToonSmoothing;
                half4 _ShadowColor;
                half4 _LitColor;
                half _ShadowOffset;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half _RimThreshold;
                half _SpecSize;
                half _SpecIntensity;
                half4 _EmissionColor;
                half _EmissionIntensity;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _CurrentAnimNormalizedTime)
                UNITY_DEFINE_INSTANCED_PROP(float, _PreviousAnimNormalizedTime)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimationBlendWeight)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            struct AppDataDepth
            {
                float4 positionOS : POSITION;
                float2 vertexIdUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct V2FDepth
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 DecodeVAT(float vertexU, float timeV)
            {
                float4 enc = SAMPLE_TEXTURE2D_LOD(_PositionTexture, sampler_PositionTexture,
                             float2(vertexU, timeV), 0);
                return lerp(_PositionMin.xyz, _PositionMax.xyz, enc.xyz);
            }

            V2FDepth vertDepth(AppDataDepth v)
            {
                V2FDepth o = (V2FDepth)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float vU     = v.vertexIdUV.x;
                float animT  = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _CurrentAnimNormalizedTime);
                float blendW = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _AnimationBlendWeight);

                float3 localPos = DecodeVAT(vU, animT);

                if (blendW > 0.001)
                {
                    float prevT = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _PreviousAnimNormalizedTime);
                    localPos = lerp(DecodeVAT(vU, prevT), localPos, blendW);
                }

                o.positionCS = TransformObjectToHClip(localPos);
                return o;
            }

            half4 fragDepth(V2FDepth i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
