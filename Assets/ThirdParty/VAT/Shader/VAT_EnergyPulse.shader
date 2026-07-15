Shader "BillTheDev/VAT/EnergyPulse"
{
    Properties
    {
        [Header(Base)]
        _MainTex ("Pattern", 2D) = "white"{}
        [HDR] _BaseColor ("Color", Color) = (0, 0.5, 1, 1)
        _EnergyIntensity ("Intensity", Range(0, 20)) = 2.0

        [Header(Flow)]
        _FlowDirection ("Flow (XYZ)", Vector) = (1, 0, 0, 0)
        _FlowSpeed ("Speed", Float) = 1.0
        _PulseDensity ("Density", Float) = 5.0
        _PulseWidth ("Width", Range(0.01, 1.0)) = 0.2
        
        [Header(System)]
        [HideInInspector] _PositionTexture ("Pos Tex", 2D) = "white" {}
        [HideInInspector] _NormalTexture ("Norm Tex", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "EnergyPulse"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
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
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float4 _FlowDirection;
                float _EnergyIntensity;
                float _FlowSpeed;
                float _PulseDensity;
                float _PulseWidth;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                ApplyVAT(input.vertexId.x, input.positionOS.xyz, input.normalOS);

                VertexPositionInputs vPos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vPos.positionCS;
                output.positionWS = vPos.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
                
                float phase = dot(input.positionWS, normalize(_FlowDirection.xyz)) * _PulseDensity - (_Time.y * _FlowSpeed);
                float wave = frac(phase);
                float pulse = smoothstep(0.5 - _PulseWidth*0.5 - 0.1, 0.5 - _PulseWidth*0.5, wave) * 
                              (1.0 - smoothstep(0.5 + _PulseWidth*0.5, 0.5 + _PulseWidth*0.5 + 0.1, wave));
                
                float energy = pulse * mask * _EnergyIntensity;
                return half4(_BaseColor.rgb * energy, saturate(energy * _BaseColor.a));
            }
            ENDHLSL
        }
    }
}