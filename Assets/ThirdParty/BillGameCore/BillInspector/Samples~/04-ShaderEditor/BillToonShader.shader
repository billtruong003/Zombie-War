Shader "Bill/Sample Toon Shader"
{
    Properties
    {
        _H_Surface("Surface Options", Float) = 0

        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Color", Color) = (1,1,1,1)

        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0, 2)) = 1.0

        _H_Lighting("Lighting", Float) = 0

        [Toggle] _UseCelShading("Cel Shading", Float) = 1
        _CelSteps("Steps", Range(1, 8)) = 3
        _CelSoftness("Edge Softness", Range(0, 1)) = 0.05

        [BillGradient] _RampTex("Lighting Ramp", 2D) = "white" {}

        _H_Outline("Outline", Float) = 0

        [Toggle] _UseOutline("Enable Outline", Float) = 1
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width", Range(0, 5)) = 1.0

        _H_Rim("Rim Light", Float) = 0

        [Toggle] _UseRim("Enable Rim", Float) = 0
        _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimPower("Rim Power", Range(0.1, 10)) = 3.0
    }

    CustomEditor "BillShaderGUI"

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _USECELSHADING_ON
            #pragma shader_feature_local _USEOUTLINE_ON
            #pragma shader_feature_local _USERIM_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float fogCoord : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RampTex);    SAMPLER(sampler_RampTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _CelSteps;
                half _CelSoftness;
                half4 _OutlineColor;
                half _OutlineWidth;
                half4 _RimColor;
                half _RimPower;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(
                    TransformObjectToWorld(input.positionOS.xyz));
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normal = normalize(input.normalWS);

                // Simple NdotL lighting
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normal, mainLight.direction));

                #ifdef _USECELSHADING_ON
                    NdotL = floor(NdotL * _CelSteps) / _CelSteps;
                #endif

                half3 lighting = mainLight.color * NdotL + half3(0.1, 0.1, 0.12);

                half3 color = baseColor.rgb * lighting;

                #ifdef _USERIM_ON
                    half rim = 1.0 - saturate(dot(normalize(input.viewDirWS), normal));
                    rim = pow(rim, _RimPower);
                    color += _RimColor.rgb * rim;
                #endif

                color = MixFog(color, input.fogCoord);
                return half4(color, baseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
