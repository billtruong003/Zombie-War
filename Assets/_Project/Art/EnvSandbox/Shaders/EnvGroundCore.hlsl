// The ground body every map ground shader shares (2026-10-02). Each map's ground shader (Shaders/Maps/)
// defines only what it uses before including this; the sandbox "Ground" defines everything.
//   GROUND_DEBUG       biome weight debug views (_DebugMode)
//   GROUND_NORMALMAP   per-layer normal maps
//   GROUND_BED         a pebbly bed on basin floors seen through clear water
//   GROUND_LITTER      forest floor: moss patches and fallen leaves
//   GROUND_CRACK_TEX   painted crack mask, glowing near basins
//   GROUND_CRACK_PROC  procedural volcanic cracks: thin, exact, lava glowing through
//   GROUND_SNOW        trails pressed into the snow (SnowTrails) and sparkles
#ifndef ENV_GROUND_CORE_INCLUDED
#define ENV_GROUND_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Assets/_Project/Art/Shaders/ToonLightContract.hlsl"
#include "Assets/_Project/Art/EnvSandbox/Shaders/EnvFluidCommon.hlsl"
#include "Assets/_Project/Art/Shaders/ToonPointLights.hlsl"
#include "Assets/_Project/Art/Shaders/MapLight.hlsl"

TEXTURE2D(_DryTex);      SAMPLER(sampler_DryTex);
TEXTURE2D(_GrassTex);    SAMPLER(sampler_GrassTex);
TEXTURE2D(_SandTex);     SAMPLER(sampler_SandTex);
TEXTURE2D(_RockTex);     SAMPLER(sampler_RockTex);
TEXTURE2D(_NoiseTex);    SAMPLER(sampler_NoiseTex);
TEXTURE2D(_DryNormal);   SAMPLER(sampler_DryNormal);
TEXTURE2D(_GrassNormal); SAMPLER(sampler_GrassNormal);
TEXTURE2D(_SandNormal);  SAMPLER(sampler_SandNormal);
TEXTURE2D(_RockNormal);  SAMPLER(sampler_RockNormal);
TEXTURE2D(_BasinMask);   SAMPLER(sampler_BasinMask);
TEXTURE2D(_CrackTex);    SAMPLER(sampler_CrackTex);
#if defined(GROUND_SNOW)
TEXTURE2D(_ZWSnowTrail); SAMPLER(sampler_ZWSnowTrail);
float4 _ZWSnowTrailST;      // x = 1 / window (m), y = 1 / texels, z = window
float4 _ZWSnowTrailCentre;  // xy = player XZ, z = radius the trails show in, w = on
#endif

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
    float4 _ZoneRect;
    float  _BasinFromUV;
    float  _BankDarken;
    float  _CrackTiling;
    float  _CrackStrength;
    float4 _CrackColor;
    float4 _CrackGlow;
    float  _CrackGlowReach;
    float  _CrackGlowPulse;
    float  _CrackThin;
    float  _CrackGlowBase;
    float  _CrackGlowFlicker;
    float4 _CrackGlowCore;
    float  _CracksProc;
    float  _CrackCell;
    float  _CrackWidth;
    float  _CrackFine;
    float4 _BedColor;
    float4 _BedColor2;
    float  _BedStrength;
    float4 _LitterColor;
    float4 _LitterColor2;
    float4 _MossColor;
    float  _LitterAmount;
    float  _LitterScale;
    float  _MossAmount;
    float4 _SnowTrailColor;
    float  _SnowTrailDepth;
    float  _SparkleAmount;
    float  _SparkleScale;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 color      : COLOR;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float4 weights    : TEXCOORD0;
    float2 worldXZ    : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
    float  basin      : TEXCOORD3;
    float3 positionWS : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings GroundVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.positionWS = positionWS;
    output.weights = input.color;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.basin = input.uv.x;

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

half4 GroundFragment(Varyings input) : SV_Target
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
#if defined(GROUND_DEBUG)
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
#endif

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
    float3 normalWS = normalize(input.normalWS);

    // Basins: the ground darkens toward a basin (a wet or scorched bank).
    float2 zoneUV = (worldXZ - _ZoneRect.xy) / max(_ZoneRect.z, 0.001) + 0.5;
    // Baked tiles carry the basin value in the mesh (one material per theme); the concept
    // zones read their own mask texture.
    float basin = _BasinFromUV > 0.5 ? input.basin : SAMPLE_TEXTURE2D(_BasinMask, sampler_BasinMask, zoneUV).r;
    // Under clear water the floor turns into a sandy, pebbly bed; without it the grass of
    // the bank just carries on below the surface.
#if defined(GROUND_BED)
    if (_BedStrength > 0.001)
    {
        // Smooth sand with soft ripples, and a pebble in about one cell of four (a pebble in every
        // cell, with dark seams between, read as paving).
        float pebbleF1, pebbleF2; float2 pid, ptc;
        EnvVoronoi(worldXZ / 0.22, 1.0, pebbleF1, pebbleF2, pid, ptc);
        float ph = EnvHash21(pid);
        float pebble = (1.0 - smoothstep(0.2, 0.28, pebbleF1)) * step(ph, 0.26);
        half3 sand = _BedColor.rgb * (0.9 + 0.2 * SampleNoise(worldXZ, 2.2));
        half3 bed = lerp(sand, _BedColor2.rgb * (0.8 + 0.4 * EnvHash21(pid + 1.3)), pebble);
        albedo = lerp(albedo, bed, smoothstep(0.03, 0.2, basin) * _BedStrength);
    }
#endif
#if defined(GROUND_LITTER)
    // Forest floor: moss patches, then fallen leaves scattered in drifts (procedural cells, two
    // tones each, a darker edge), thinning out toward the water.
    float dry = 1.0 - smoothstep(0.0, 0.12, basin);
    float mossN = SampleNoise(worldXZ + 17.3, 11.0) * 0.6 + SampleNoise(worldXZ * 1.7 - 4.1, 5.0) * 0.4;
    albedo = lerp(albedo, _MossColor.rgb * (0.85 + 0.3 * SampleNoise(worldXZ, 1.3)), smoothstep(0.52, 0.6, mossN) * _MossAmount * dry);
    if (_LitterAmount > 0.001)
    {
        float lf1, lf2; float2 lid, ltc;
        EnvVoronoi(worldXZ / _LitterScale, 1.0, lf1, lf2, lid, ltc);
        float lh = EnvHash21(lid);
        // An elongated leaf: the offset to the cell centre stretched along a per-leaf angle.
        float ang = lh * 6.2831;
        float2 dir = float2(cos(ang), sin(ang));
        float2 q = float2(dot(ltc, dir), dot(ltc, float2(-dir.y, dir.x)) * 1.9);
        float lr = 0.22 + 0.16 * EnvHash21(lid + 2.7);
        float leafD = length(q);
        float leaf = 1.0 - smoothstep(lr * 0.85, lr, leafD);
        float edge = smoothstep(lr * 0.55, lr * 0.85, leafD);
        float drift = smoothstep(0.42, 0.58, SampleNoise(worldXZ - 9.1, 7.0) + _LitterAmount * 0.3 - 0.15);
        leaf *= step(lh, 0.8) * drift * dry;
        half3 leafCol = lerp(_LitterColor.rgb, _LitterColor2.rgb, EnvHash21(lid + 7.3)) * (1.0 - edge * 0.3);
        albedo = lerp(albedo, leafCol, leaf);
    }
#endif
    albedo *= 1.0 - _BankDarken * smoothstep(0.02, 0.25, basin);

    // Cracks: dark lines everywhere, glowing within reach of a basin (and, on volcanic
    // ground, everywhere a little). Thinning keeps only the centre of the painted line.
#if defined(GROUND_CRACK_PROC)
    // Volcanic cracks from procedural cells: a true distance to the cell edge, so the width
    // is exact and thin, nothing repeats, and a finer second layer splits the plates.
    float2 warp = (float2(SampleNoise(worldXZ, 9.0), SampleNoise(worldXZ + 5.3, 9.0)) - 0.5) * 1.4;
    float cf1, cf2, ff1, ff2; float2 cid, ctc;
    EnvVoronoi((worldXZ + warp) / _CrackCell, 1.0, cf1, cf2, cid, ctc);
    EnvVoronoi((worldXZ + warp * 0.6) / (_CrackCell * 0.38) + 11.7, 1.0, ff1, ff2, cid, ctc);
    float wide = 1.0 - smoothstep(_CrackWidth * 0.35, _CrackWidth, cf2 - cf1);
    float fine = (1.0 - smoothstep(_CrackWidth * 0.2, _CrackWidth * 0.6, ff2 - ff1)) * _CrackFine;
    float crack = max(wide, fine * 0.7);
#elif defined(GROUND_CRACK_TEX)
    float crackRaw = SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, worldXZ / max(_CrackTiling, 0.001)).r;
    float crack = smoothstep(_CrackThin, 1.0, crackRaw);
#else
    float crack = 0.0;
#endif
    albedo = lerp(albedo, _CrackColor.rgb, crack * _CrackStrength);
    float nearBasin = smoothstep(0.0, max(_CrackGlowReach, 1e-3), basin + _CrackGlowReach * 0.35);
    float reach = max(nearBasin * step(1e-3, _CrackGlowReach), _CrackGlowBase);
    float pulse = 0.75 + 0.25 * sin(_Time.y * _CrackGlowPulse + worldXZ.x * 0.3 + worldXZ.y * 0.2);
    float flick = SampleNoise(worldXZ + _Time.y * float2(0.7, 0.4), 6.0);
    pulse *= lerp(1.0, 0.45 + flick * 1.1, _CrackGlowFlicker);
    half3 glowCol = lerp(_CrackGlow.rgb, _CrackGlowCore.rgb, smoothstep(0.55, 1.0, crack));
    half3 glow = glowCol * crack * reach * pulse;

    #if defined(GROUND_NORMALMAP)
        float3 nts =
              w.r * UnpackNormalScale(SAMPLE_TEXTURE2D(_DryNormal,   sampler_DryNormal,   worldXZ / max(_DryTiling,   0.001)), _NormalStrength)
            + w.g * UnpackNormalScale(SAMPLE_TEXTURE2D(_GrassNormal, sampler_GrassNormal, worldXZ / max(_GrassTiling, 0.001)), _NormalStrength)
            + w.b * UnpackNormalScale(SAMPLE_TEXTURE2D(_SandNormal,  sampler_SandNormal,  worldXZ / max(_SandTiling,  0.001)), _NormalStrength)
            + w.a * UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNormal,  sampler_RockNormal,  worldXZ / max(_RockTiling,  0.001)), _NormalStrength);

        nts = normalize(nts + float3(0, 0, 1e-4));
        normalWS = normalize(float3(nts.x, nts.z, nts.y));
    #endif

    half3 sparkle = 0;
#if defined(GROUND_SNOW)
    // Trails: the pressed depth darkens toward a cold blue, and its slope tilts the normal so the toon
    // light shades one wall of every footprint and lights the other (no vertices move).
    float trail = 0;
    if (_ZWSnowTrailCentre.w > 0.5)
    {
        float2 tuv = worldXZ * _ZWSnowTrailST.x;
        float2 tx = float2(_ZWSnowTrailST.y * 1.5, 0);
        float c = SAMPLE_TEXTURE2D(_ZWSnowTrail, sampler_ZWSnowTrail, tuv).r;
        float gx = SAMPLE_TEXTURE2D(_ZWSnowTrail, sampler_ZWSnowTrail, tuv + tx).r - SAMPLE_TEXTURE2D(_ZWSnowTrail, sampler_ZWSnowTrail, tuv - tx).r;
        float gz = SAMPLE_TEXTURE2D(_ZWSnowTrail, sampler_ZWSnowTrail, tuv + tx.yx).r - SAMPLE_TEXTURE2D(_ZWSnowTrail, sampler_ZWSnowTrail, tuv - tx.yx).r;
        float reach = saturate((_ZWSnowTrailCentre.z - distance(worldXZ, _ZWSnowTrailCentre.xy)) / 4.0);
        trail = c * reach;
        normalWS = normalize(normalWS + float3(gx, 0, gz) * reach * _SnowTrailDepth);
        albedo = lerp(albedo, albedo * _SnowTrailColor.rgb, smoothstep(0.15, 0.9, trail));
    }
    // Sparkles: one glint in a few cells, lit only from some view angles, so they twinkle as the
    // camera follows the player.
    if (_SparkleAmount > 0.001)
    {
        float2 sp = worldXZ / _SparkleScale;
        float2 cell = floor(sp);
        float h = EnvHash21(cell);
        float2 f = frac(sp) - 0.5 - (EnvHash22(cell) - 0.5) * 0.6;
        float3 V = normalize(GetWorldSpaceViewDir(input.positionWS));
        float tw = frac(h * 13.7 + dot(V, float3(5.3, 3.1, 7.9)) * 1.5);
        float glint = step(0.93, h) * smoothstep(0.8, 1.0, tw) * (1.0 - smoothstep(0.03, 0.09, length(f)));
        sparkle = glint * _SparkleAmount * (1.0 - smoothstep(0.1, 0.4, trail));
    }
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

    half2 mapLight = ZW_MapLight(input.positionWS);   // baked sun visibility, AO
    half3 ambient = SampleSH(normalWS) * _AmbientBoost * mapLight.y;
    half3 lighting = (lightColor * toonLight + ambient) * ZW_ShadowTint(mapLight.x);
    lighting += ZW_ToonPointLights(input.positionWS, normalWS, 0.25h) * mapLight.y;

    return half4(albedo * lighting + glow + sparkle * lighting, 1.0h);
}

#endif
