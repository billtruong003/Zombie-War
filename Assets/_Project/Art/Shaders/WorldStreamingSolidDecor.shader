// Shader cho hinh hoc trang tri DAC (than cay, go, da) da duoc gop vao mot mesh moi chunk.
//
// Doi tuong duy nhat cua no la mot atlas opaque + tint theo vertex color. Khong PBR, khong
// MaterialPropertyBlock, khong trang thai rieng cho tung chunk — ca ring dung chung mot material.
Shader "ZombieWar/World Streaming/Solid Decor"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Solid atlas", 2D) = "white" {}
        _BaseColor ("Base color", Color) = (1,1,1,1)

        // Vertex color luu tint o thang mot nua (tint 1.0 -> 127) vi kenh chi co 8 bit.
        _TintScale ("Vertex tint scale", Float) = 2

        _LightWrap ("Light wrap", Range(0,1)) = 0.45
        _AmbientBoost ("Ambient boost", Range(0,2)) = 1
        _AmbientFallback ("Ambient fallback (no rig, no light)", Color) = (0.78, 0.78, 0.82, 1)
        _ToonSteps ("Toon light steps", Range(2,5)) = 3
        _ToonSoftness ("Toon edge softness", Range(0.001,0.2)) = 0.035
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "SolidDecorForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/ToonLightContract.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _TintScale;
                float  _LightWrap;
                float  _AmbientBoost;
                half4  _AmbientFallback;
                float  _ToonSteps;
                float  _ToonSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float ToonBand(float value)
            {
                float intervals = max(_ToonSteps - 1.0, 1.0);
                float scaled = saturate(value) * intervals;
                float lower = floor(scaled);
                float transition = smoothstep(0.5 - _ToonSoftness, 0.5 + _ToonSoftness, frac(scaled));
                return saturate((lower + transition) / intervals);
            }

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                albedo.rgb *= _BaseColor.rgb * input.color.rgb * _TintScale;

                float3 normalWS = normalize(input.normalWS);
                // M4.5: hop dong toon dung chung thay cho GetMainLight() — xem ToonLightContract.hlsl.
                float3 lightDir;
                half3 lightColor;
                bool directional = ZW_ResolveToonLight(_AmbientFallback.rgb, lightDir, lightColor);

                float ndotl = saturate(dot(normalWS, lightDir));
                float wrapped = lerp(ndotl, ndotl * 0.5 + 0.5, _LightWrap);

                half3 lighting = lightColor * (directional ? ToonBand(wrapped) : 1.0)
                               + SampleSH(normalWS) * _AmbientBoost;
                return half4(albedo.rgb * lighting, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
