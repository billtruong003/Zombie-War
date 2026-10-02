#ifndef ZOMBIEWAR_TOON_POINT_LIGHTS_INCLUDED
#define ZOMBIEWAR_TOON_POINT_LIGHTS_INCLUDED

// The game's own point lights (2026-10-02), not URP lights: no Light components, no Forward+ cluster
// build, no URP light loop, no shadow maps, no extra keyword or variant.
// ToonPointLights.cs culls and ranks the lights on the CPU every frame and hands the lit set over as
// two small global arrays; the count follows the graphics tier (0 on low).
//
// Per pixel, per light:
//   - squared distance first; outside the range -> next light (one dot, no sqrt)
//   - toon ramp: half-lambert cut hard with smoothstep(0.53, 0.57) (MinionsArt), so the lit edge
//     follows the form instead of reading as a flat 2D disc
//   - falloff (1 - d^2/r^2)^2 through a smoothstep (_ZWPointFalloff: lower = brighter further out)
//   - no texture read, no shadow, no PBR

#define ZW_MAX_POINT_LIGHTS 16

float4 _ZWPointLightPos[ZW_MAX_POINT_LIGHTS];     // xyz = position, w = range (m)
float4 _ZWPointLightColor[ZW_MAX_POINT_LIGHTS];   // rgb = colour x intensity
float  _ZWPointLightCount;
float  _ZWPointFalloff;

// Light from every point light on a surface, to multiply by albedo. backFill lifts the unlit side a
// little (0 = hard toon, ~0.2 for foliage and ground so a lamp still tints what faces away).
half3 ZW_ToonPointLights(float3 positionWS, half3 normalWS, half backFill)
{
    half3 sum = 0;
    int count = min((int)_ZWPointLightCount, ZW_MAX_POINT_LIGHTS);
    float falloff = max(_ZWPointFalloff, 0.05);
    [loop] for (int i = 0; i < count; i++)
    {
        float4 p = _ZWPointLightPos[i];
        float3 toLight = p.xyz - positionWS;
        float d2 = dot(toLight, toLight);
        float r2 = p.w * p.w;
        if (d2 >= r2) continue;

        half3 l = toLight * rsqrt(max(d2, 1e-4));
        half ramp = smoothstep(0.53h, 0.57h, dot(normalWS, l) * 0.5h + 0.5h);
        ramp = lerp(backFill, 1.0h, ramp);
        float t = 1.0 - d2 / r2;
        half atten = smoothstep(0.0, falloff, t * t);
        sum += _ZWPointLightColor[i].rgb * (ramp * atten);
    }
    return sum;
}

#endif
