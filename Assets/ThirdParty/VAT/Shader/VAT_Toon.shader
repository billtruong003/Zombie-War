Shader "BillTheDev/VAT/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _RampTex ("Ramp Texture", 2D) = "white" {}
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.1, 10)) = 3.0
        [HideInInspector] _PositionTexture ("Pos Tex", 2D) = "white" {}
        [HideInInspector] _NormalTexture ("Norm Tex", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        
        Pass
        {
            Name "ToonLit"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "VAT_Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 vertexId : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _RimColor;
                float _RimPower;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RampTex); SAMPLER(sampler_RampTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                ApplyVAT(input.vertexId.x, input.positionOS.xyz, input.normalOS);
                
                VertexPositionInputs vPos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs vNorm = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vPos.positionCS;
                output.positionWS = vPos.positionWS;
                output.normalWS = vNorm.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 N = NormalizeNormalPerPixel(input.normalWS);
                float3 V = GetWorldSpaceViewDir(input.positionWS);
                
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half NdotL = dot(N, mainLight.direction) * 0.5 + 0.5;
                half3 ramp = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(NdotL, 0.5)).rgb;
                
                half3 lightColor = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation * ramp;
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                
                // Rim
                half rim = 1.0 - saturate(dot(V, N));
                half3 rimEmission = _RimColor.rgb * pow(rim, _RimPower) * mainLight.distanceAttenuation;
                
                return half4((albedo * lightColor) + rimEmission, 1.0);
            }
            ENDHLSL
        }
        
        // Reuse ShadowCaster from SimpleLit
        UsePass "BillTheDev/VAT/SimpleLit/ShadowCaster"
    }
}