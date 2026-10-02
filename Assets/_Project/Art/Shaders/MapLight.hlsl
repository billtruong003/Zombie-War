#ifndef ZOMBIEWAR_MAP_LIGHT_INCLUDED
#define ZOMBIEWAR_MAP_LIGHT_INCLUDED

// Baked shadows and ambient occlusion of the map (2026-10-02), instead of realtime shadow maps and
// SSAO: MapLightBaker writes one texture per map, BakedMapStreamer binds it with the map's place in
// the world. One bilinear read per pixel, wrapping with the map (it tiles).
//   R  sun visibility       G  ambient occlusion
//   B  height of the caster shading this spot (/12 m): a surface above it is out of that shadow,
//      so a canopy shades the enemies under it without darkening its own top
//   A  ground height ((y + 4) / 8): occlusion fades out with the height above the ground

TEXTURE2D(_ZWMapLight); SAMPLER(sampler_ZWMapLight);
float4 _ZWMapLightST;     // xy = 1 / map size, zw = -origin / map size
float  _ZWMapLightOn;
half4  _ZWShadowTint;     // rgb: what sunlight drops to in shadow (per map, toon-tinted, not black)

// x = sun visibility (0 in shadow, 1 lit), y = ambient occlusion (1 = open).
half2 ZW_MapLight(float3 positionWS)
{
    if (_ZWMapLightOn < 0.5) return half2(1, 1);
    float2 uv = positionWS.xz * _ZWMapLightST.xy + _ZWMapLightST.zw;
    half4 m = SAMPLE_TEXTURE2D_LOD(_ZWMapLight, sampler_ZWMapLight, uv, 0);
    // The bake is soft at its texel size; a toon shadow wants a firm edge.
    m.r = saturate((m.r - 0.5h) * 2.2h + 0.5h);
    half casterTop = m.b * 12.0h;
    half groundY = m.a * 8.0h - 4.0h;
    half above = (half)positionWS.y - groundY;
    // From 0.75 m under the caster's height up, a surface is out of its shadow: low things (a
    // bridge deck, a crate) cast the very shadow recorded under them and must not darken themselves.
    // The ground itself always takes the full shadow.
    half sun = lerp(m.r, 1.0h, saturate(((half)positionWS.y - casterTop) * 2.0h + 1.5h) * step(0.01h, m.b) * saturate(above * 3.0h));
    // Occlusion belongs to the ground and the feet of things: gone 0.6 m up.
    half ao = lerp(m.g, 1.0h, saturate(above / 0.6h));
    return half2(sun, ao);
}

// The multiplier a lit colour takes in the map's shadow: the map's shadow tint, faded in by how
// much of the sun is blocked. The hero's planar shadow (CharacterToon) multiplies the same tint, so
// both shadows read as one.
half3 ZW_ShadowTint(half sun)
{
    return lerp(_ZWShadowTint.rgb, half3(1, 1, 1), sun);
}

#endif
