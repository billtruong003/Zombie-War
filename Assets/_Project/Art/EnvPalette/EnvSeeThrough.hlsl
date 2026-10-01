// See-through for map decoration (2026-10-01): whatever stands between the camera and the player
// (a canopy, a rock, a crate) is dithered away inside a cone that narrows toward the camera, so the
// player is never hidden. Opaque and cheap: a screen-space 4x4 Bayer clip, no transparency, no sort.
// _ZW_SeeThrough is set every frame by GrassBenders: xyz = player feet, w = cone radius at the
// player (0 = off, e.g. menus and sandboxes without a player).
#ifndef ZW_ENV_SEE_THROUGH
#define ZW_ENV_SEE_THROUGH

float4 _ZW_SeeThrough;

float ZW_Bayer4(float2 pixel)
{
    uint x = (uint)pixel.x & 3u, y = (uint)pixel.y & 3u;
    const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    return (m[y * 4u + x] + 0.5) / 16.0;
}

// positionCS.xy is the pixel position in the fragment stage.
void ZW_SeeThroughClip(float3 positionWS, float4 positionCS)
{
    if (_ZW_SeeThrough.w <= 0.0) return;
    float3 target = _ZW_SeeThrough.xyz + float3(0.0, 1.0, 0.0);
    float3 cam = _WorldSpaceCameraPos;
    float3 v = target - cam;
    float t = dot(positionWS - cam, v) / max(dot(v, v), 1e-4);
    if (t <= 0.15 || t >= 0.97) return;
    float d = length(positionWS - (cam + v * t));
    float r = _ZW_SeeThrough.w * t;
    // Keep a quarter of the pixels: the occluder still reads as a shape.
    float fade = (1.0 - smoothstep(r * 0.55, r, d)) * smoothstep(0.15, 0.35, t) * 0.75;
    clip(ZW_Bayer4(positionCS.xy) - fade);
}

#endif
