// Shader cho hinh hoc foliage da duoc gop vao mot mesh moi chunk.
//
// Alpha CLIPPING, khong phai alpha blending: co/la khong bao gio duoc doi thu tu sap xep, va toan bo
// ring chi dung mot material duy nhat. Khong MaterialPropertyBlock, khong trang thai theo chunk.
//
// Foliage thuong khong do bong — viec do tat o muc renderer nen shader khong can pass ShadowCaster.
Shader "ZombieWar/World Streaming/Foliage"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Foliage atlas (RGB + alpha)", 2D) = "white" {}
        _BaseColor ("Base color", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5

        // Vertex color luu tint o thang mot nua (tint 1.0 -> 127) vi kenh chi co 8 bit.
        _TintScale ("Vertex tint scale", Float) = 2

        _LightWrap ("Light wrap", Range(0,1)) = 0.65
        _AmbientBoost ("Ambient boost", Range(0,2)) = 1
        _AmbientFallback ("Ambient fallback (no rig, no light)", Color) = (0.78, 0.78, 0.82, 1)

        // La la mat phang hai mat; backface can duoc chieu sang nhu mat truoc.
        _BacklightWrap ("Two sided lighting", Range(0,1)) = 1
        _ToonSteps ("Toon light steps", Range(2,5)) = 3
        _ToonSoftness ("Toon edge softness", Range(0.001,0.2)) = 0.035

        // --- Gio dung chung (M4.6C.3) ---
        // _WindStrength = 0 la duong an toan: khong dich chuyen dinh nao, ket qua giong het truoc.
        _WindStrength ("Wind strength (m)", Range(0,1)) = 0.18
        _WindFrequency ("Wind spatial frequency (1/m)", Float) = 0.09
        _WindSpeed ("Wind speed", Float) = 1.35
        _WindDirection ("Wind direction XZ", Vector) = (1,0,0.35,0)
        _GustStrength ("Gust strength", Range(0,2)) = 0.55
        _GustScale ("Gust spatial scale (1/m)", Float) = 0.021
        // Cho san floating origin, giong hop dong cua shader mat dat. Mac dinh 0.
        _WorldOriginOffset ("World origin offset", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "AlphaTest"
        }

        Pass
        {
            Name "FoliageForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Off

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
                float  _Cutoff;
                float  _TintScale;
                float  _LightWrap;
                float  _AmbientBoost;
                half4  _AmbientFallback;
                float  _BacklightWrap;
                float  _ToonSteps;
                float  _ToonSoftness;
                float  _WindStrength;
                float  _WindFrequency;
                float  _WindSpeed;
                float4 _WindDirection;
                float  _GustStrength;
                float  _GustScale;
                float4 _WorldOriginOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                // UV1.x = pha cua khom, UV1.y = bien do theo loai (ghi boi DecorationAccumulator).
                float2 windData   : TEXCOORD1;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Gio: lay pha theo TOA DO THE GIOI, khong theo toa do chunk.
            //
            // Chunk chi rong 32 m va duoc tai su dung lien tuc; neu pha bam vao toa do cuc bo thi
            // truong gio se lap lai moi 32 m va nhay moi lan chunk duoc recycle. Lay theo the gioi
            // (cong ca _WorldOriginOffset cho floating origin sau nay) thi hai chunk ke nhau nam
            // tren cung mot song, va mot khom co quay lai dung pha cu.
            //
            // Do cung nam o vertex color alpha: goc ~0 nen dung yen, ngon ~1 nen dua manh nhat.
            float3 ApplyWind(float3 positionOS, float stiffness, float2 windData)
            {
                float amplitude = _WindStrength * windData.y;
                if (amplitude <= 0.0) return positionOS;

                float3 worldPos = TransformObjectToWorld(positionOS);
                float2 worldXZ = worldPos.xz + _WorldOriginOffset.xz;

                float2 dir = normalize(float2(_WindDirection.x, _WindDirection.z) + 1e-5);
                float along = dot(worldXZ, dir);

                float phase = windData.x * 6.2831853;
                float wave = sin(along * _WindFrequency + _Time.y * _WindSpeed + phase);
                float gust = sin(along * _GustScale - _Time.y * _WindSpeed * 0.37 + phase * 0.5);
                float sway = wave * (1.0 + _GustStrength * gust * 0.5);

                // Chi lech ngang, va nhan binh phuong do cung: goc gan nhu bat dong, ngon moi ngua han.
                float bend = stiffness * stiffness * amplitude * sway;
                float3 offsetWS = float3(dir.x * bend, 0, dir.y * bend);
                return positionOS + TransformWorldToObjectDir(offsetWS, false);
            }

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

                float3 positionOS = ApplyWind(input.positionOS.xyz, input.color.a, input.windData);
                output.positionCS = TransformObjectToHClip(positionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(albedo.a - _Cutoff);

                albedo.rgb *= _BaseColor.rgb * input.color.rgb * _TintScale;

                float3 normalWS = normalize(input.normalWS);
                // M4.5: hop dong toon dung chung thay cho GetMainLight() — xem ToonLightContract.hlsl.
                float3 lightDir;
                half3 lightColor;
                bool directional = ZW_ResolveToonLight(_AmbientFallback.rgb, lightDir, lightColor);

                // Cull Off nen nua so mat quay lung lai anh sang. Lay tri tuyet doi de mat sau
                // khong bi den kit, roi wrap them cho mem.
                float ndotl = dot(normalWS, lightDir);
                ndotl = lerp(saturate(ndotl), abs(ndotl), _BacklightWrap);
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
