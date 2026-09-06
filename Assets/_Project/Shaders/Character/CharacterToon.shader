// Nhân vật — toon đồ hoạ, nguyên mẫu M5+.1.
//
// Chống lại đúng bốn khuyết điểm đọc được trên ảnh H0:
//   1. tóc trắng phẳng lì, không còn mặt phẳng nào  -> một ranh giới sáng/tối rõ + ambient có trần
//   2. giày/chân trắng dính vào nhau                -> cùng lý do trên
//   3. mọi thứ như phun airbrush                    -> softness do hoạ sĩ đặt, không phải dốc N·L
//   4. bóng đổ đen vô hồn                           -> bóng mang MÀU (_ShadowTint)
//
// Không SH/GI, không vòng lặp additional light, không specular rộng — ba thứ đó là nguyên nhân gốc
// của cái nhìn "bợt và nhựa".
// KHÔNG có pass ShadowCaster — đây là quyết định có căn cứ, không phải thiếu sót.
//
// Đo trên chính project: Mobile_RPAsset có supportsMainLightShadows = False, shadowDistance = 0,
// supportsAdditionalLightShadows = False, và cả 5 renderer của Player.prefab đều đặt
// shadowCastingMode = Off, receiveShadows = False. Nghĩa là dự án KHÔNG dùng shadow map cho nhân vật
// ở bất kỳ đâu; cảm giác đứng trên mặt đất do mesh bóng tiếp đất gộp (CharacterContactShadows) lo.
//
// Bản nguyên mẫu từng có một pass tên "ShadowCaster" dùng TransformObjectToHClip thẳng, KHÔNG áp
// ApplyShadowBias. Nếu shadow map được bật lên sau này, pass đó sẽ tạo shadow acne/peter-panning mà
// vẫn mang cái tên nghe như đã đúng. Thà không có pass còn hơn có một pass giả đúng.
//
// Vì cùng lý do đó, GetMainLight() ở đây KHÔNG lấy shadow coordinate: không có shadow map nào để
// nhận. Đây không phải cắt bớt so với cách làm của MinionsArt — đơn giản là kiến trúc ánh sáng của
// dự án không có tầng đó.
Shader "ZombieWar/Character/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Atlas", 2D) = "white" {}
        [MainColor]   _BaseColor ("Tint", Color) = (1,1,1,1)

        [Header(Toon band)]
        _ShadowTint ("Shadow Tint", Color) = (0.55, 0.58, 0.72, 1)
        _LightThreshold ("Light Threshold", Range(0,1)) = 0.55
        _LightSoftness ("Band Softness", Range(0.001,0.6)) = 0.06

        [Header(Ambient floor)]
        _AmbientColor ("Ambient Color", Color) = (0.55, 0.60, 0.78, 1)
        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.28

        [Header(Specular)]
        _SpecStrength ("Specular Strength", Range(0,1)) = 0.0
        _SpecCutoff ("Specular Cutoff", Range(0,1)) = 0.92

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3.0
        _RimCutoff ("Rim Cutoff", Range(0,1)) = 0.55
        _RimStrength ("Rim Strength", Range(0,2)) = 0.35
        _RimAlbedoTint ("Rim Follows Albedo", Range(0,1)) = 0.6

        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "CharacterLighting.hlsl"

        // SRP Batcher: mọi property của material nằm trong đúng một CBUFFER tên UnityPerMaterial.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half4  _ShadowTint;
            half   _LightThreshold;
            half   _LightSoftness;
            half4  _AmbientColor;
            half   _AmbientStrength;
            half   _SpecStrength;
            half   _SpecCutoff;
            half4  _RimColor;
            half   _RimPower;
            half   _RimCutoff;
            half   _RimStrength;
            half   _RimAlbedoTint;
            half   _Cutoff;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                half3 albedo = tex.rgb;

                ZWLight light = ZW_GetCharacterLight(IN.positionWS);
                half3 viewDirWS = GetWorldSpaceViewDir(IN.positionWS);

                // MỘT ranh giới sáng/tối. Đây là thứ trả lại khối cho bề mặt trắng.
                half band = ZW_ToonBand(IN.normalWS, light.direction, _LightThreshold, _LightSoftness);

                // Bóng mang MÀU: nhân albedo với sắc bóng thay vì kéo về đen. Vải tối vì thế không
                // bị bẹp thành một mảng đen chết.
                half3 shadowed = albedo * _ShadowTint.rgb;
                half3 lit = albedo * light.color;
                half3 color = lerp(shadowed, lit, band);

                // Nền ambient có trần — nâng vùng tối mà KHÔNG xoá ranh giới.
                color += ZW_BoundedAmbient(albedo, _AmbientColor.rgb, _AmbientStrength);

                // Specular hẹp, mặc định TẮT. Vải và da không được nhận highlight bóng loáng.
                if (_SpecStrength > 0.001h)
                {
                    half spec = ZW_TightSpecular(IN.normalWS, light.direction, viewDirWS, _SpecCutoff, _SpecStrength);
                    color += spec * light.color;
                }

                // Viền chỉ ở phía ăn sáng, và pha theo màu albedo nên nó không thành đường neon trắng.
                half rim = ZW_LightFacingRim(IN.normalWS, viewDirWS, band, _RimPower, _RimCutoff, _RimStrength);
                half3 rimTint = lerp(_RimColor.rgb, _RimColor.rgb * albedo, _RimAlbedoTint);
                color += rim * rimTint * light.color;

                return half4(color, tex.a);
            }
            ENDHLSL
        }


        // DepthOnly: URP cần nó cho depth prepass/hiệu ứng phụ thuộc depth.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing
            #pragma target 3.0

            struct DAttr { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DVary { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            DVary depthVert (DAttr IN)
            {
                DVary OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 depthFrag (DVary IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    Fallback Off
}
