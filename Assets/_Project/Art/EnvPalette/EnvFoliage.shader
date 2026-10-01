// Env palette foliage (2026-10-01): the one material for every leaf, bush, grass and fern of the
// decoration kit. Built on the world streaming foliage shader (alpha clip, two-sided toon light,
// world-phase wind) plus interactive bending:
//  - Up to 16 "benders" (the player, the nearest enemies, stations) come from a global array set by
//    GrassBenders each frame. Each pushes the plant away from its centre and presses it down,
//    strongest at the tip (vertex colour alpha = stiffness: 0 at the root, 1 at the top).
//  - No state is kept, so plants stand back up as soon as a bender has passed (option A of the plan).
// Vertex data written by the palette tool: colour rgb = tint (0.5 = none), alpha = stiffness;
// UV1.x = wind phase, UV1.y = wind amplitude.
Shader "HordeCall/Env/Foliage"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Foliage atlas (RGB + alpha)", 2D) = "white" {}
        _BaseColor ("Base color", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        _TintScale ("Vertex tint scale", Float) = 2
        _LightWrap ("Light wrap", Range(0,1)) = 0.65
        _AmbientBoost ("Ambient boost", Range(0,2)) = 1
        _AmbientFallback ("Ambient fallback", Color) = (0.78, 0.78, 0.82, 1)
        _BacklightWrap ("Two sided lighting", Range(0,1)) = 1
        _ToonSteps ("Toon light steps", Range(2,5)) = 3
        _ToonSoftness ("Toon edge softness", Range(0.001,0.2)) = 0.035
        _WindStrength ("Wind strength (m)", Range(0,1)) = 0.12
        _WindFrequency ("Wind spatial frequency (1/m)", Float) = 0.09
        _WindSpeed ("Wind speed", Float) = 1.35
        _WindDirection ("Wind direction XZ", Vector) = (1,0,0.35,0)
        _GustStrength ("Gust strength", Range(0,2)) = 0.55
        _GustScale ("Gust spatial scale (1/m)", Float) = 0.021
        _BendStrength ("Bend push (m)", Range(0,2)) = 0.7
        _BendPress ("Bend press down", Range(0,1)) = 0.55
    }

    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float  _Cutoff, _TintScale, _LightWrap, _AmbientBoost;
            half4  _AmbientFallback;
            float  _BacklightWrap, _ToonSteps, _ToonSoftness;
            float  _WindStrength, _WindFrequency, _WindSpeed;
            float4 _WindDirection;
            float  _GustStrength, _GustScale, _BendStrength, _BendPress;
        CBUFFER_END

        // Set by GrassBenders: xyz = position, w = radius.
        float4 _ZW_Benders[16];
        float  _ZW_BenderCount;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 uv         : TEXCOORD0;
            float2 windData   : TEXCOORD1;
            float4 color      : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float3 Displace(float3 positionOS, float stiffness, float2 windData)
        {
            float3 worldPos = TransformObjectToWorld(positionOS);
            float s2 = stiffness * stiffness;

            // Wind: phase from world position, so neighbouring chunks share one wave.
            float2 dir = normalize(float2(_WindDirection.x, _WindDirection.z) + 1e-5);
            float along = dot(worldPos.xz, dir);
            float phase = windData.x * 6.2831853;
            float wave = sin(along * _WindFrequency + _Time.y * _WindSpeed + phase);
            float gust = sin(along * _GustScale - _Time.y * _WindSpeed * 0.37 + phase * 0.5);
            float sway = wave * (1.0 + _GustStrength * gust * 0.5) * _WindStrength * windData.y * s2;
            worldPos.xz += dir * sway;

            // Benders: push away from each and press down, fading to nothing at the radius.
            int count = (int)_ZW_BenderCount;
            [loop] for (int i = 0; i < 16; i++)
            {
                if (i >= count) break;
                float4 b = _ZW_Benders[i];
                float2 d = worldPos.xz - b.xz;
                float dist = length(d);
                float k = saturate(1.0 - dist / max(b.w, 0.01));
                // Only what grows near the ground bends; a tree's canopy above a passer-by does not.
                k = k * k * s2 * saturate(1.0 - (worldPos.y - b.y) / 1.6);
                worldPos.xz += (d / max(dist, 0.05)) * k * _BendStrength;
                worldPos.y -= k * _BendPress * stiffness;
            }
            return TransformWorldToObject(worldPos);
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/ToonLightContract.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : TEXCOORD2;
            };

            float ToonBand(float value)
            {
                float intervals = max(_ToonSteps - 1.0, 1.0);
                float scaled = saturate(value) * intervals;
                float lower = floor(scaled);
                float transition = smoothstep(0.5 - _ToonSoftness, 0.5 + _ToonSoftness, frac(scaled));
                return saturate((lower + transition) / intervals);
            }

            Varyings Vertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                o.positionCS = TransformObjectToHClip(Displace(input.positionOS.xyz, input.color.a, input.windData));
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                o.color = input.color;
                return o;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(albedo.a - _Cutoff);
                albedo.rgb *= _BaseColor.rgb * input.color.rgb * _TintScale;
                float3 normalWS = normalize(input.normalWS);
                float3 lightDir; half3 lightColor;
                bool directional = ZW_ResolveToonLight(_AmbientFallback.rgb, lightDir, lightColor);
                float ndotl = dot(normalWS, lightDir);
                ndotl = lerp(saturate(ndotl), abs(ndotl), _BacklightWrap);
                float wrapped = lerp(ndotl, ndotl * 0.5 + 0.5, _LightWrap);
                half3 lighting = lightColor * (directional ? ToonBand(wrapped) : 1.0) + SampleSH(normalWS) * _AmbientBoost;
                return half4(albedo.rgb * lighting, 1.0h);
            }
            ENDHLSL
        }

        // Depth for water / lava edges and the outline (which asks for DepthNormals).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DV
            #pragma fragment DF
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V DV(Attributes i) { V o; o.pos = TransformObjectToHClip(Displace(i.positionOS.xyz, i.color.a, i.windData)); o.uv = i.uv; return o; }
            half DF(V i) : SV_Target { clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DV
            #pragma fragment DF
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; };
            V DV(Attributes i) { V o; o.pos = TransformObjectToHClip(Displace(i.positionOS.xyz, i.color.a, i.windData)); o.uv = i.uv; o.n = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 DF(V i) : SV_Target { clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff); return half4(NormalizeNormalPerPixel(i.n), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
