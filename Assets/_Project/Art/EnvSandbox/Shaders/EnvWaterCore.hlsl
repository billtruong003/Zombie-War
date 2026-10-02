// The water body every water shader shares (2026-10-02). Each map's water shader (Shaders/Maps/)
// defines only the features it uses before including this, so a map pays for its own look and
// nothing else; the sandbox "Stylized Water" defines them all and switches them with values.
//   WATER_WAVES       vertex waves
//   WATER_CLEAR       see-through: the surface tint closes up with depth over the basin floor
//   WATER_CAUSTICS    moving light lines on the floor
//   WATER_FOAM_NOISE  intersection foam broken up by scrolling noise (ameye.dev stylized water):
//                     solid at the bank, then blobs that thin out with depth
//   WATER_BUBBLES     toxic bubbles that swell and pop
//   WATER_GLOW        emissive surface (toxic)
//   WATER_SCUM        floating duckweed / leaf flecks gathered in drifting patches (murky water)
//   WATER_ICE         crack mask over a frozen surface
//   WATER_REFRACT     floor read from the camera opaque texture (with _REFRACT on)
#ifndef ENV_WATER_CORE_INCLUDED
#define ENV_WATER_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#if defined(WATER_REFRACT)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#endif
#include "Assets/_Project/Art/Shaders/ToonLightContract.hlsl"
#include "Assets/_Project/Art/EnvSandbox/Shaders/EnvFluidCommon.hlsl"
#include "Assets/_Project/Art/Shaders/ToonPointLights.hlsl"
#include "Assets/_Project/Art/Shaders/MapLight.hlsl"

TEXTURE2D(_BasinMask); SAMPLER(sampler_BasinMask);
TEXTURE2D(_NoiseTex);  SAMPLER(sampler_NoiseTex);
TEXTURE2D(_CrackTex);  SAMPLER(sampler_CrackTex);
CBUFFER_START(UnityPerMaterial)
    float4 _ZoneRect;
    float4 _ShallowColor, _DeepColor, _FoamColor, _StreakColor, _Emission, _BubbleColor, _CrackColor, _CausticColor;
    float  _DepthRange, _Clarity, _MinAlpha, _RefractStrength, _Caustics, _CausticScale;
    float  _FoamWidth, _FoamSoft, _FoamWobble, _StreakScale, _StreakCut, _StreakAlpha;
    float4 _Flow;
    float  _WaveHeight, _WaveScale, _WaveSpeed, _BubbleAmount, _BubbleScale, _BubbleRate;
    float  _CrackTiling, _CrackAmount, _LightWrap;
    half4  _AmbientFallback;
    float  _FoamNoise, _FoamNoiseScale, _FoamNoiseDist;
    float4 _ScumColor, _ScumColor2;
    float  _ScumAmount, _ScumScale;
CBUFFER_END

struct A { float4 pos : POSITION; };
struct V { float4 pos : SV_POSITION; float3 w : TEXCOORD0; float4 screen : TEXCOORD1; };

float WaterNoise(float2 xz, float scale, float2 scroll)
{
    return EnvAntiTileNoise(TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), xz, scale, scroll * scale);
}

V WaterVertex(A i)
{
    V o;
    float3 w = TransformObjectToWorld(i.pos.xyz);
#if defined(WATER_WAVES)
    float t = _Time.y * _WaveSpeed;
    w.y += (WaterNoise(w.xz, _WaveScale, float2(t * 0.03, t * 0.02)) - 0.5) * 2.0 * _WaveHeight;
#endif
    o.w = w;
    o.pos = TransformWorldToHClip(w);
    o.screen = ComputeScreenPos(o.pos);
    return o;
}

half4 WaterFragment(V i) : SV_Target
{
    float2 xz = i.w.xz;
    float2 suv = i.screen.xy / i.screen.w;
    // Water depth in metres along the view ray (scene depth minus the surface's own depth).
    float depth = max(0.0, LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams) - i.screen.w);
    float2 flow = _Flow.xy * _Flow.z * _Time.y;

    // Shallow → deep in two toon steps (hard edges read better than a smooth gradient).
    float d = saturate(depth / _DepthRange);
    half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.35, 0.45, d) * 0.6 + smoothstep(0.75, 0.85, d) * 0.4);

    // Scrolling streaks: two anti-tiled noise layers crossing, thresholded into flat shapes.
    float n1 = WaterNoise(xz, _StreakScale, flow);
    float n2 = WaterNoise(xz + 13.7, _StreakScale * 1.7, -flow * 0.7);
    float streak = step(_StreakCut, n1 * 0.6 + n2 * 0.4) * _StreakAlpha;

    float bubble = 0;
#if defined(WATER_BUBBLES)
    if (_BubbleAmount > 0.001)
    {
        float3 bub = EnvBubbles(xz, _BubbleScale, _BubbleRate, _BubbleAmount);
        bubble = saturate(bub.x * 0.75 + bub.y + bub.z);
    }
#endif

    float crack = 0;
#if defined(WATER_ICE)
    crack = SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, xz / max(_CrackTiling, 0.001)).r * _CrackAmount;
#endif

    // Floating duckweed / leaf flecks: small cells, kept only inside slow drifting patches and away
    // from the bank line, two greens per fleck.
    float scum = 0; half3 scumCol = 0;
#if defined(WATER_SCUM)
    if (_ScumAmount > 0.001)
    {
        float2 sp = xz + flow * 0.35;
        float patch = smoothstep(0.5, 0.62, WaterNoise(sp + 41.3, _ScumScale * 6.0, 0) + _ScumAmount * 0.25);
        float f1, f2; float2 id, tc;
        EnvVoronoi(sp / _ScumScale, 0.9, f1, f2, id, tc);
        float h = EnvHash21(id);
        float r = 0.18 + 0.2 * h;
        scum = (1.0 - smoothstep(r * 0.8, r, f1)) * patch * step(h, 0.85);
        scumCol = lerp(_ScumColor.rgb, _ScumColor2.rgb, EnvHash21(id + 5.1));
    }
#endif

    // Shore foam: a thin line where the water meets the bank or a rock, wobbling with the noise,
    // and an optional softer band behind it.
    float wob = (WaterNoise(xz, 3.0, flow * 1.5) - 0.5) * 2.0 * _FoamWobble;
    float foamLine = 1.0 - step(_FoamWidth + wob, depth);
    float foamBand = _FoamSoft > 0.001 ? (1.0 - smoothstep(_FoamWidth, _FoamWidth + _FoamSoft, depth + wob)) * 0.45 : 0.0;
    float foam = max(foamLine, foamBand);
#if defined(WATER_FOAM_NOISE)
    // ameye.dev: noise above a cut-off that rises with depth — solid at the bank, broken blobs
    // further out, gone at _FoamNoiseDist. Two scales so the blobs have a ragged edge.
    if (_FoamNoise > 0.001)
    {
        float fnA = WaterNoise(xz, _FoamNoiseScale, flow * 1.2);
        float fnB = WaterNoise(xz + 7.9, _FoamNoiseScale * 0.37, -flow * 0.8);
        float fn = fnA * 0.65 + fnB * 0.35;
        float cut = saturate(depth / max(_FoamNoiseDist, 0.001));
        float blobs = smoothstep(cut * 0.75 + 0.2, cut * 0.75 + 0.24, fn) * (1.0 - step(1.0, cut));
        foam = max(foam, blobs * _FoamNoise);
    }
#endif

    float3 lightDir; half3 lightColor;
    bool directional = ZW_ResolveToonLight(_AmbientFallback.rgb, lightDir, lightColor);
    float ndotl = saturate(lightDir.y);
    half lit = directional ? lerp(ndotl, ndotl * 0.5 + 0.5, _LightWrap) : 1.0;
    half3 light = lightColor * lit + SampleSH(float3(0, 1, 0));
    light *= ZW_ShadowTint(ZW_MapLight(i.w).x);   // tree and bridge shadows on the water
    light += ZW_ToonPointLights(i.w, half3(0, 1, 0), 1.0h);

    // How much of the floor still shows through the water above it.
    float clarity = 0;
#if defined(WATER_CLEAR)
    clarity = _Clarity > 0.001 ? exp(-depth / _Clarity) : 0.0;
#endif

    half3 caustic = 0;
#if defined(WATER_CAUSTICS)
    // Light lines on the floor, fading with depth. The floor position comes back from the depth
    // texture, so they lie on the basin bottom, not on the surface.
    if (_Caustics > 0.001)
    {
        float3 floorWS = ComputeWorldSpacePosition(suv, SampleSceneDepth(suv), UNITY_MATRIX_I_VP);
        float a1, b1, a2, b2; float2 id, tc;
        EnvVoronoi(floorWS.xz / _CausticScale + _Time.y * 0.12, 1.0, a1, b1, id, tc);
        EnvVoronoi(floorWS.xz / (_CausticScale * 1.37) - _Time.y * 0.09 + 3.3, 1.0, a2, b2, id, tc);
        float lines = (1.0 - smoothstep(0.0, 0.08, b1 - a1)) * (1.0 - smoothstep(0.0, 0.12, b2 - a2));
        lines = saturate(lines * 1.6 + (1.0 - smoothstep(0.0, 0.05, b1 - a1)) * 0.35);
        caustic = _CausticColor.rgb * lines * _Caustics * clarity;
    }
#endif

    half3 surface = lerp(col, _StreakColor.rgb, streak);
    surface = lerp(surface, _BubbleColor.rgb, bubble);
    surface = lerp(surface, _CrackColor.rgb, crack);
    surface = lerp(surface, scumCol, scum);
    half3 litSurface = surface * light;
    half3 glow = 0;
#if defined(WATER_GLOW)
    glow = _Emission.rgb * (1.0 - foam) * (1.0 - scum) + _BubbleColor.rgb * bubble * 0.4;
#endif
    float solid = max(max(streak, bubble), max(crack, scum));

#if defined(WATER_REFRACT)
    {
        // The floor seen through the water: wobbled, but never pulled from something standing in
        // front of the surface (a leg, a bridge plank) — fall back to the straight read then.
        float2 wobble = (float2(n1, n2) - 0.5) * _RefractStrength * saturate(depth * 3.0);
        float2 ruv = suv + wobble;
        if (LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams) < i.screen.w) ruv = suv;
        half3 floorCol = SampleSceneColor(ruv) * lerp(_DeepColor.rgb, _ShallowColor.rgb, clarity * 0.5 + 0.5) + caustic;
        half3 rgbR = lerp(litSurface, floorCol, clarity * (1.0 - solid));
        rgbR = lerp(rgbR, _FoamColor.rgb * light, foam);
        return half4(rgbR + glow, 1.0h);
    }
#else
    // Blended: the surface tint closes up with depth; streaks, bubbles, cracks, flecks and foam stay
    // solid.
    half3 rgb = lerp(litSurface, _FoamColor.rgb * light, foam);
    float alpha = lerp(1.0, _MinAlpha, clarity);
    alpha = max(alpha, max(solid, foam));
    // Caustics light the floor where it shows through: scaled so that, after the alpha blend, they
    // land on the floor's share of the pixel.
    rgb += caustic * (1.0 - alpha) / max(alpha, 0.1);
    return half4(rgb + glow, alpha);
#endif
}

#endif
