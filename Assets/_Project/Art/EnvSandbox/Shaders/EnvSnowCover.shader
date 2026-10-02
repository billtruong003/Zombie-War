// Snow that has settled on a prop (2026-09-30, reworked 2026-10-02 after owner feedback), after
// MinionsArt's "Toon Snow" breakdown (reimplemented, not copied):
//  1. snow colour on the faces that look up toward the snow direction, its edge broken up by a world
//     noise so it does not follow every facet in a straight line;
//  2. no vertex push any more: low-poly props have split hard edges, and pushing the split vertices
//     along their own normals tore the faces apart. The thickness is faked instead with a cold grey
//     band just below the snow line (the shadowed lip of the snow) and the rim light;
//  3. a rim light on the snow for a cold glow.
// Writes depth and normals like the other decor, so the outline and the water's foam see it.
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
        _SnowNoise ("Snow edge breakup", Range(0,1)) = 0.35
        _SnowNoiseScale ("Snow edge noise size (m)", Float) = 0.6
        _SnowEdgeColor ("Snow lip (grey under the edge)", Color) = (0.62,0.68,0.8,1)
        _SnowEdgeWidth ("Snow lip width", Range(0,0.6)) = 0.22
        [HideInInspector] _SnowHeight ("Unused (was the vertex push)", Float) = 0
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

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _SnowColor;
            float4 _SnowDirection;
            float  _SnowAmount, _SnowSoftness, _SnowHeight;
            float  _SnowNoise, _SnowNoiseScale, _SnowEdgeWidth;
            float4 _SnowEdgeColor;
            float4 _RimColor;
            float  _RimPower;
            float  _LightWrap, _AmbientBoost;
            half4  _AmbientFallback;
            float  _ToonSteps, _ToonSoftness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/ToonLightContract.hlsl"
            #include "Assets/_Project/Art/Shaders/ToonPointLights.hlsl"
            #include "Assets/_Project/Art/Shaders/MapLight.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

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

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            // Smooth value noise in world space (0..1).
            float WorldNoise(float3 w)
            {
                float3 p = w / max(_SnowNoiseScale, 0.01);
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = lerp(lerp(Hash31(i), Hash31(i + float3(1, 0, 0)), f.x), lerp(Hash31(i + float3(0, 1, 0)), Hash31(i + float3(1, 1, 0)), f.x), f.y);
                float b = lerp(lerp(Hash31(i + float3(0, 0, 1)), Hash31(i + float3(1, 0, 1)), f.x), lerp(Hash31(i + float3(0, 1, 1)), Hash31(i + float3(1, 1, 1)), f.x), f.y);
                return lerp(a, b, f.z);
            }

            V Vertex(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.w = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.w);
                o.n = TransformObjectToWorldNormal(i.n);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 Fragment(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.n);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;

                // How much a face looks up toward the snow, wobbled by the world noise so the snow line
                // wanders across the facets instead of tracing them.
                float d = dot(n, normalize(_SnowDirection.xyz)) + (WorldNoise(i.w) - 0.5) * _SnowNoise;
                float edge = 1.0 - _SnowAmount * 2.0;       // amount 0 → no face, 1 → everything but the underside
                half snow = smoothstep(edge - _SnowSoftness, edge + _SnowSoftness, d);
                // The lip: a cold grey band just under the snow line, as if the snow had thickness
                // and shaded the face below it.
                half lip = smoothstep(edge - _SnowSoftness - _SnowEdgeWidth, edge - _SnowSoftness, d) * (1.0h - snow);
                albedo = lerp(albedo, albedo * _SnowEdgeColor.rgb, lip);
                albedo = lerp(albedo, _SnowColor.rgb, snow);

                float3 lightDir; half3 lightColor;
                bool directional = ZW_ResolveToonLight(_AmbientFallback.rgb, lightDir, lightColor);
                float ndotl = saturate(dot(n, lightDir));
                float wrapped = lerp(ndotl, ndotl * 0.5 + 0.5, _LightWrap);
                half2 mapLight = ZW_MapLight(i.w);
                half3 lighting = (lightColor * (directional ? ToonBand(wrapped) : 1.0) + SampleSH(n) * _AmbientBoost * mapLight.y) * ZW_ShadowTint(mapLight.x);
                lighting += ZW_ToonPointLights(i.w, n, 0.25h) * mapLight.y;

                float3 viewDir = normalize(GetWorldSpaceViewDir(i.w));
                half rim = 1.0h - saturate(dot(viewDir, n));
                half3 emission = _RimColor.rgb * pow(rim, _RimPower) * snow;
                return half4(albedo * lighting + emission, 1.0h);
            }
            ENDHLSL
        }

        // Depth for the depth texture (water foam, lava edges) and the outline's normals.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DVert
            #pragma fragment DFrag
            #pragma multi_compile_instancing
            struct DA { float4 pos : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DV { float4 pos : SV_POSITION; };
            DV DVert(DA i) { UNITY_SETUP_INSTANCE_ID(i); DV o; o.pos = TransformObjectToHClip(i.pos.xyz); return o; }
            half DFrag(DV i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex NVert
            #pragma fragment NFrag
            #pragma multi_compile_instancing
            struct NA { float4 pos : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct NV { float4 pos : SV_POSITION; float3 n : TEXCOORD0; };
            NV NVert(NA i) { UNITY_SETUP_INSTANCE_ID(i); NV o; o.pos = TransformObjectToHClip(i.pos.xyz); o.n = TransformObjectToWorldNormal(i.n); return o; }
            half4 NFrag(NV i) : SV_Target { return half4(NormalizeNormalPerPixel(i.n), 0.0); }
            ENDHLSL
        }
    }
    FallBack Off
}
