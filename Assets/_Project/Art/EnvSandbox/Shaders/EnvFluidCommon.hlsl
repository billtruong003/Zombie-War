// Shared helpers for the env fluids (water, toxic, ice, lava) and the volcanic ground (2026-10-02).
//  - AntiTileNoise: the tiling noise texture read twice, rotated and scaled by unrelated factors,
//    and domain-warped, so no repeat period shows (the old single read looped every few metres).
//  - Voronoi: procedural cells (no texture, nothing repeats), used for bubbles and crust plates.
//  - Bubbles: one bubble per cell with its own size, place and life phase; it swells, then pops
//    into a ring that fades (MinionsArt's boiling lava / toxic recipe, reimplemented).
#ifndef ENV_FLUID_COMMON_INCLUDED
#define ENV_FLUID_COMMON_INCLUDED

float2 EnvHash22(float2 p)
{
    float3 a = frac(float3(p.xyx) * float3(123.34, 234.34, 345.65));
    a += dot(a, a + 34.45);
    return frac(float2(a.x * a.y, a.y * a.z));
}

float EnvHash21(float2 p)
{
    p = frac(p * float2(233.34, 851.73));
    p += dot(p, p + 23.45);
    return frac(p.x * p.y);
}

// Two reads of a tiling noise at unrelated scales and angles, the second warped by the first.
// tex/samp: the noise texture; xz in metres; scale in metres; scroll in metres.
float EnvAntiTileNoise(TEXTURE2D_PARAM(tex, samp), float2 xz, float scale, float2 scroll)
{
    const float2x2 rotA = float2x2(0.8, -0.6, 0.6, 0.8);       // ~37°
    const float2x2 rotB = float2x2(0.28, 0.96, -0.96, 0.28);   // ~74°
    float2 p = (xz + scroll) / max(scale, 0.001);
    float a = SAMPLE_TEXTURE2D_LOD(tex, samp, mul(rotA, p), 0).r;
    float2 q = mul(rotB, p * 0.613) + (a - 0.5) * 0.35 + 0.17;
    float b = SAMPLE_TEXTURE2D_LOD(tex, samp, q, 0).r;
    return a * 0.55 + b * 0.45;
}

// F1 distance, F2 distance and the id of the nearest cell. uv in cell units. jitter 0..1.
void EnvVoronoi(float2 uv, float jitter, out float f1, out float f2, out float2 cellId, out float2 toCentre)
{
    float2 g = floor(uv), f = frac(uv);
    f1 = 8.0; f2 = 8.0; cellId = g; toCentre = 0;
    [unroll] for (int y = -1; y <= 1; y++)
    [unroll] for (int x = -1; x <= 1; x++)
    {
        float2 o = float2(x, y);
        float2 c = o + EnvHash22(g + o) * jitter;
        float2 r = c - f;
        float d = dot(r, r);
        if (d < f1) { f2 = f1; f1 = d; cellId = g + o; toCentre = r; }
        else if (d < f2) { f2 = d; }
    }
    f1 = sqrt(f1); f2 = sqrt(f2);
}

// Bubbles over a surface. xz in metres, size = mean bubble spacing (m), rate = lives per second,
// density = share of cells holding a bubble. Returns: x = dome (0..1, swelling bubble body),
// y = rim (bright outline of the dome), z = pop ring (expanding ring after the pop).
float3 EnvBubbles(float2 xz, float size, float rate, float density)
{
    float f1, f2; float2 id, toC;
    EnvVoronoi(xz / max(size, 0.001), 0.85, f1, f2, id, toC);
    float h = EnvHash21(id);
    if (h > density) return 0;
    float life = frac(_Time.y * rate * (0.7 + 0.6 * EnvHash21(id + 3.1)) + h * 7.0);
    float radius = 0.18 + 0.22 * EnvHash21(id + 9.7);             // in cell units
    float d = length(toC);
    // 0..0.8 of the life: the bubble swells; 0.8..1: it has popped and a ring spreads out.
    float swell = smoothstep(0.0, 0.8, life);
    float r = radius * swell;
    float inside = 1.0 - smoothstep(r * 0.85, r, d);
    float alive = step(life, 0.8);
    float dome = inside * alive * (1.0 - d / max(r, 1e-3) * 0.6);
    float rim = (smoothstep(r * 0.7, r * 0.9, d) - smoothstep(r * 0.9, r * 1.02, d)) * alive;
    float pop = saturate((life - 0.8) / 0.2);
    float ringR = radius * (1.0 + pop * 0.9);
    float ring = (smoothstep(ringR - 0.06, ringR - 0.02, d) - smoothstep(ringR - 0.02, ringR + 0.02, d)) * (1.0 - alive) * (1.0 - pop);
    return float3(saturate(dome), saturate(rim), saturate(ring));
}

#endif
