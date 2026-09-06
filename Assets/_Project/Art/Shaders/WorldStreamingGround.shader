// Shader mat dat cho the gioi stream (M2A).
//
// Bon lop be mat (Dry / Grass / Sand / Rock) duoc pha theo bon trong so biome do CPU sinh ra va ghi
// vao RGBA vertex color. Shader KHONG tu sinh ra mot he biome thu hai — no chi to mau, lam net ranh
// gioi va them bien doi vi mo. BiomeSampler van la nguon su that duy nhat.
//
// UV lay tu toa do the gioi XZ nen hoa tiet chay lien tuc qua bien chunk; khong co UV nao reset moi
// 32 m. _WorldOriginOffset de san cho floating origin sau nay, mac dinh bang 0.
//
// Mot material duy nhat dung chung cho ca 25 chunk. Khong co material instance cho tung chunk.
Shader "ZombieWar/World Streaming/Ground"
{
    Properties
    {
        [Header(Surface layers)]
        [NoScaleOffset] _DryTex   ("Dry albedo",   2D) = "white" {}
        [NoScaleOffset] _GrassTex ("Grass albedo", 2D) = "white" {}
        [NoScaleOffset] _SandTex  ("Sand albedo",  2D) = "white" {}
        [NoScaleOffset] _RockTex  ("Rock albedo",  2D) = "white" {}

        _DryTint   ("Dry tint",   Color) = (1,1,1,1)
        _GrassTint ("Grass tint", Color) = (1,1,1,1)
        _SandTint  ("Sand tint",  Color) = (1,1,1,1)
        _RockTint  ("Rock tint",  Color) = (1,1,1,1)

        // Kich thuoc mot lan lap, tinh bang MET. So lon = hoa tiet to hon.
        _DryTiling   ("Dry tiling (m)",   Float) = 6
        _GrassTiling ("Grass tiling (m)", Float) = 5
        _SandTiling  ("Sand tiling (m)",  Float) = 8
        _RockTiling  ("Rock tiling (m)",  Float) = 7

        [Header(Normal maps)]
        [Toggle(_NORMALMAP)] _UseNormalMap ("Use normal maps", Float) = 0
        [NoScaleOffset][Normal] _DryNormal   ("Dry normal",   2D) = "bump" {}
        [NoScaleOffset][Normal] _GrassNormal ("Grass normal", 2D) = "bump" {}
        [NoScaleOffset][Normal] _SandNormal  ("Sand normal",  2D) = "bump" {}
        [NoScaleOffset][Normal] _RockNormal  ("Rock normal",  2D) = "bump" {}
        _NormalStrength ("Normal strength", Range(0,2)) = 1

        [Header(Blending)]
        // >1 lam kenh troi noi bat hon, tranh canh bon mau trung binh thanh mot mang be be.
        _BlendSharpness ("Blend sharpness", Range(1,8)) = 3
        // Lam meo ranh gioi bang noise de duong chuyen khong bi tron nhu ve bang co.
        _WeightJitter ("Weight jitter", Range(0,1)) = 0.18

        [Header(Macro variation)]
        [NoScaleOffset] _NoiseTex ("Noise (grayscale, tileable)", 2D) = "gray" {}
        _MacroScale ("Macro noise scale (m)", Float) = 145
        _MacroStrength ("Macro strength", Range(0,1)) = 0.28
        _DetailScale ("Jitter noise scale (m)", Float) = 23

        [Header(Stylization)]
        _TextureStrength ("Texture detail strength", Range(0,1)) = 0.5
        _ToonSteps ("Toon light steps", Range(2,5)) = 3
        _ToonSoftness ("Toon edge softness", Range(0.001,0.2)) = 0.035

        [Header(Lighting)]
        _LightWrap ("Light wrap", Range(0,1)) = 0.55
        _AmbientBoost ("Ambient boost", Range(0,2)) = 1
        _AmbientFallback ("Ambient fallback (no rig, no light)", Color) = (0.78, 0.78, 0.82, 1)

        [Header(Debug)]
        // 0 = mat dat co texture, 1 = trong so biome tho, 2 = chi be mat troi nhat.
        _DebugMode ("Debug mode", Float) = 0
        _DryDebugColor   ("Dry debug",   Color) = (0.42, 0.32, 0.21, 1)
        _GrassDebugColor ("Grass debug", Color) = (0.27, 0.42, 0.20, 1)
        _SandDebugColor  ("Sand debug",  Color) = (0.74, 0.68, 0.45, 1)
        _RockDebugColor  ("Rock debug",  Color) = (0.44, 0.46, 0.50, 1)

        // Dat truoc cho floating origin: logic = physical + offset. M2A luon la 0.
        _WorldOriginOffset ("World origin offset", Vector) = (0,0,0,0)
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
            Name "GroundForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0
            #pragma shader_feature_local_fragment _NORMALMAP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/ToonLightContract.hlsl"

            TEXTURE2D(_DryTex);      SAMPLER(sampler_DryTex);
            TEXTURE2D(_GrassTex);    SAMPLER(sampler_GrassTex);
            TEXTURE2D(_SandTex);     SAMPLER(sampler_SandTex);
            TEXTURE2D(_RockTex);     SAMPLER(sampler_RockTex);
            TEXTURE2D(_NoiseTex);    SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_DryNormal);   SAMPLER(sampler_DryNormal);
            TEXTURE2D(_GrassNormal); SAMPLER(sampler_GrassNormal);
            TEXTURE2D(_SandNormal);  SAMPLER(sampler_SandNormal);
            TEXTURE2D(_RockNormal);  SAMPLER(sampler_RockNormal);

            CBUFFER_START(UnityPerMaterial)
                float4 _DryTint;
                float4 _GrassTint;
                float4 _SandTint;
                float4 _RockTint;
                float4 _DryDebugColor;
                float4 _GrassDebugColor;
                float4 _SandDebugColor;
                float4 _RockDebugColor;
                float4 _WorldOriginOffset;
                float  _DryTiling;
                float  _GrassTiling;
                float  _SandTiling;
                float  _RockTiling;
                float  _UseNormalMap;
                float  _NormalStrength;
                float  _BlendSharpness;
                float  _WeightJitter;
                float  _MacroScale;
                float  _MacroStrength;
                float  _DetailScale;
                float  _TextureStrength;
                float  _ToonSteps;
                float  _ToonSoftness;
                float  _LightWrap;
                float  _AmbientBoost;
                half4  _AmbientFallback;
                float  _DebugMode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 weights    : TEXCOORD0;
                float2 worldXZ    : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.weights = input.color;

                // Toa do logic = toa do vat ly + offset goc. M2A: offset luon bang 0.
                output.worldXZ = positionWS.xz + _WorldOriginOffset.xz;
                return output;
            }

            float SampleNoise(float2 worldXZ, float scale)
            {
                return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, worldXZ / max(scale, 0.001)).r;
            }

            float ToonBand(float value)
            {
                float intervals = max(_ToonSteps - 1.0, 1.0);
                float scaled = saturate(value) * intervals;
                float lower = floor(scaled);
                float transition = smoothstep(0.5 - _ToonSoftness, 0.5 + _ToonSoftness, frac(scaled));
                return saturate((lower + transition) / intervals);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 worldXZ = input.worldXZ;

                // --- Trong so biome ------------------------------------------------------------
                // Chuan hoa lai sau noi suy, roi lam net. Ca hai buoc chi anh huong HIEN THI;
                // du lieu biome tren CPU khong he thay doi.
                float4 raw = max(input.weights, 0.0);
                raw /= max(raw.r + raw.g + raw.b + raw.a, 1e-4);

                // A shared scalar disappears after normalization. Convert one smooth noise sample
                // into four phase-shifted, zero-sum layer biases so jitter changes relative weights.
                float jitterPhase = SampleNoise(worldXZ, _DetailScale) * TWO_PI;
                float jitterSin;
                float jitterCos;
                sincos(jitterPhase, jitterSin, jitterCos);
                float4 jitterBias = float4(jitterSin, jitterCos, -jitterSin, -jitterCos);
                float4 w = raw * max(1.0 + jitterBias * _WeightJitter, 0.0);
                w = max(w, 0.0);
                w = pow(w, _BlendSharpness);
                w /= max(w.r + w.g + w.b + w.a, 1e-4);

                // --- Che do chan doan ----------------------------------------------------------
                if (_DebugMode > 0.5)
                {
                    float4 d = raw;
                    if (_DebugMode > 1.5)
                    {
                        float peak = max(max(d.r, d.g), max(d.b, d.a));
                        d = step(peak - 1e-5, d);
                        d /= max(d.r + d.g + d.b + d.a, 1e-4);
                    }

                    half3 debugRgb = d.r * _DryDebugColor.rgb
                                   + d.g * _GrassDebugColor.rgb
                                   + d.b * _SandDebugColor.rgb
                                   + d.a * _RockDebugColor.rgb;
                    return half4(debugRgb, 1.0h);
                }

                // --- Albedo bon lop -------------------------------------------------------------
                half3 albedo =
                      w.r * SAMPLE_TEXTURE2D(_DryTex,   sampler_DryTex,   worldXZ / max(_DryTiling,   0.001)).rgb * _DryTint.rgb
                    + w.g * SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, worldXZ / max(_GrassTiling, 0.001)).rgb * _GrassTint.rgb
                    + w.b * SAMPLE_TEXTURE2D(_SandTex,  sampler_SandTex,  worldXZ / max(_SandTiling,  0.001)).rgb * _SandTint.rgb
                    + w.a * SAMPLE_TEXTURE2D(_RockTex,  sampler_RockTex,  worldXZ / max(_RockTiling,  0.001)).rgb * _RockTint.rgb;

                // Texture chi cung cap vat lieu; palette biome giu quyen dieu khien art direction.
                // Keo albedo ve cac khoi mau ro rang de tranh doc nhu PBR photographic tren mat phang.
                half3 biomeColor = w.r * _DryDebugColor.rgb
                                 + w.g * _GrassDebugColor.rgb
                                 + w.b * _SandDebugColor.rgb
                                 + w.a * _RockDebugColor.rgb;
                albedo = lerp(biomeColor, albedo, _TextureStrength);

                // Bien doi sang toi o quy mo lon, pha cam giac lap lai cua texture.
                float macro = SampleNoise(worldXZ, _MacroScale);
                albedo *= lerp(1.0 - _MacroStrength, 1.0 + _MacroStrength * 0.5, macro);

                // --- Phap tuyen -----------------------------------------------------------------
                // Mat phang XZ nen TBN la hang so: T=(1,0,0), B=(0,0,1), N=(0,1,0).
                // Khong can tangent trong mesh, va vi the cung khong the co duong noi tangent.
                float3 normalWS = float3(0, 1, 0);

                #if defined(_NORMALMAP)
                    float3 nts =
                          w.r * UnpackNormalScale(SAMPLE_TEXTURE2D(_DryNormal,   sampler_DryNormal,   worldXZ / max(_DryTiling,   0.001)), _NormalStrength)
                        + w.g * UnpackNormalScale(SAMPLE_TEXTURE2D(_GrassNormal, sampler_GrassNormal, worldXZ / max(_GrassTiling, 0.001)), _NormalStrength)
                        + w.b * UnpackNormalScale(SAMPLE_TEXTURE2D(_SandNormal,  sampler_SandNormal,  worldXZ / max(_SandTiling,  0.001)), _NormalStrength)
                        + w.a * UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNormal,  sampler_RockNormal,  worldXZ / max(_RockTiling,  0.001)), _NormalStrength);

                    nts = normalize(nts + float3(0, 0, 1e-4));
                    normalWS = normalize(float3(nts.x, nts.z, nts.y));
                #endif

                // --- Anh sang ---------------------------------------------------------------------
                // M4.5: nguon sang lay tu hop dong toon dung chung, KHONG phai GetMainLight().
                // Ca nam map deu tat directional light that (ToonLightRig moi la nguon sang), nen
                // GetMainLight() tra ve mau den va mat dat chi con song nho ambient SH cua scene —
                // Map_Level5 co SH toi nen ra mau den han.
                float3 lightDir;
                half3 lightColor;
                bool directional = ZW_ResolveToonLight(_AmbientFallback.rgb, lightDir, lightColor);

                float ndotl = saturate(dot(normalWS, lightDir));
                float wrapped = lerp(ndotl, ndotl * 0.5 + 0.5, _LightWrap);
                // Khong co huong sang that thi khong ke dai: mot dai toi gia chi tao nhieu.
                float toonLight = directional ? ToonBand(wrapped) : 1.0;

                half3 ambient = SampleSH(normalWS) * _AmbientBoost;
                half3 lighting = lightColor * toonLight + ambient;

                return half4(albedo * lighting, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
