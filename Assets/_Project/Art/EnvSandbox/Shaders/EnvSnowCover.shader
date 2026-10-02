// Env Sandbox only (2026-09-30): a snow layer that settles on a prop, after MinionsArt's "Toon Snow"
// breakdown (reimplemented, not copied):
//  1. snow colour where the angle between the normal and the snow direction passes a threshold;
//  2. the same test in the vertex shader pushes those vertices out along the normal, so the snow
//     reads as a thick layer, not paint;
//  3. a rim light on the snow for a cold glow.
// Lighting is the project's toon contract (ToonLightRig), like the world's solid decor shader.
Shader "HordeCall/EnvSandbox/Snow Cover"
{
    Properties
    {
        _BaseMap ("Base (palette or albedo)", 2D) = "white" {}
        _BaseColor ("Base colour", Color) = (1,1,1,1)
        _SnowColor ("Snow colour", Color) = (0.94,0.97,1,1)
        _SnowDirection ("Snow falls from (world)", Vector) = (0,1,0.15,0)
        _SnowAmount ("Snow amount", Range(0,1)) = 0.45
        _SnowSoftness ("Snow edge softness", Range(0.001,0.3)) = 0.04
        _SnowHeight ("Snow thickness (m)", Range(0,0.2)) = 0.05
        [HDR] _RimColor ("Rim colour", Color) = (0.55,0.75,1,1)
        _RimPower ("Rim power", Range(0.5,8)) = 3
        _LightWrap ("Light wrap", Range(0,1)) = 0.45
        _AmbientBoost ("Ambient boost", Range(0,2)) = 1
        _AmbientFallback ("Ambient fallback", Color) = (0.78,0.78,0.82,1)
        _ToonSteps ("Toon light steps", Range(2,5)) = 3
        _ToonSoftness ("Toon edge softness", Range(0.001,0.2)) = 0.035
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/ToonLightContract.hlsl"
            #include "Assets/_Project/Art/Shaders/ToonPointLights.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _SnowColor;
                float4 _SnowDirection;
                float  _SnowAmount, _SnowSoftness, _SnowHeight;
                float4 _RimColor;
                float  _RimPower;
                float  _LightWrap, _AmbientBoost;
                half4  _AmbientFallback;
                float  _ToonSteps, _ToonSoftness;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 w : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };

            // Same toon banding as the world's solid decor shader.
            float ToonBand(float value)
            {
                float intervals = max(_ToonSteps - 1.0, 1.0);
                float scaled = saturate(value) * intervals;
                float lower = floor(scaled);
                float transition = smoothstep(0.5 - _ToonSoftness, 0.5 + _ToonSoftness, frac(scaled));
                return saturate((lower + transition) / intervals);
            }

            half SnowMask(float3 nWS)
            {
                float d = dot(nWS, normalize(_SnowDirection.xyz));
                float edge = 1.0 - _SnowAmount * 2.0;       // amount 0 → no face, 1 → everything but the underside
                return smoothstep(edge - _SnowSoftness, edge + _SnowSoftness, d);
            }

            V Vertex(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                float3 nWS = TransformObjectToWorldNormal(i.n);
                float3 pOS = i.pos.xyz + i.n * (_SnowHeight * SnowMask(nWS));
                o.w = TransformObjectToWorld(pOS);
                o.pos = TransformWorldToHClip(o.w);
                o.n = nWS;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 Fragment(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.n);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half snow = SnowMask(n);
                albedo = lerp(albedo, _SnowColor.rgb, snow);

                float3 lightDir; half3 lightColor;
                bool directional = ZW_ResolveToonLight(_AmbientFallback.rgb, lightDir, lightColor);
                float ndotl = saturate(dot(n, lightDir));
                float wrapped = lerp(ndotl, ndotl * 0.5 + 0.5, _LightWrap);
                half3 lighting = lightColor * (directional ? ToonBand(wrapped) : 1.0) + SampleSH(n) * _AmbientBoost;
                lighting += ZW_ToonPointLights(i.w, n, 0.25h);

                float3 viewDir = normalize(GetWorldSpaceViewDir(i.w));
                half rim = 1.0h - saturate(dot(viewDir, n));
                half3 emission = _RimColor.rgb * pow(rim, _RimPower) * snow;
                return half4(albedo * lighting + emission, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
