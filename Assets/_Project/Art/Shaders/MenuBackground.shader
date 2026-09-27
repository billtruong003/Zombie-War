// MenuBackground.shader — HordeCall menu background with depth (owner 2026-09-27: "a background
// with some depth looks far more artful than a flat one").
// Layers, back to front, all procedural (no textures):
//   1. vertical theme gradient
//   2. soft light rays fanning down from above the top edge, slowly turning
//   3. three parallax pattern layers (stars, coins, crosshair rings, plus signs) scrolling
//      diagonally; far layers are smaller, fainter, blurrier and slower, near layers larger and
//      crisper — the speed/size/blur difference is what reads as depth
//   4. drifting bokeh discs
//   5. vignette
// Unlit, pipeline-agnostic; use on a full-screen RawImage/Image or Graphics.Blit.
Shader "ZombieWar/MenuBackground"
{
    Properties
    {
        [Header(Gradient)]
        _TopColor ("Top", Color) = (0.62, 0.86, 1, 1)
        _BotColor ("Bottom", Color) = (0.25, 0.53, 0.88, 1)

        [Header(Rays)]
        _RayColor ("Ray Color", Color) = (1, 1, 1, 1)
        _RayStrength ("Ray Strength", Range(0,1)) = 0.18
        _RayCount ("Ray Count", Float) = 9
        _RaySpeed ("Ray Speed", Float) = 0.03

        [Header(Pattern layers)]
        _ShapeColor ("Shape Color", Color) = (1, 1, 1, 1)
        _ShapeAlpha ("Shape Alpha (near)", Range(0,1)) = 0.22
        _ShapeScale ("Shape Density", Float) = 4.2
        _ScrollSpeed ("Scroll Speed", Float) = 0.035
        _Angle ("Scroll Angle (deg)", Float) = 25

        [Header(Bokeh)]
        _BokehAlpha ("Bokeh Alpha", Range(0,1)) = 0.16

        [Header(Screen)]
        _Aspect ("Aspect (w/h, set by MenuBackground.cs)", Float) = 0.5625

        [Header(Vignette)]
        _Vignette ("Vignette", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest Always Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed4 _TopColor, _BotColor, _RayColor, _ShapeColor;
            float _RayStrength, _RayCount, _RaySpeed;
            float _ShapeAlpha, _ShapeScale, _ScrollSpeed, _Angle;
            float _BokehAlpha, _Vignette, _Aspect;

            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            float hash21(float2 p) { p = frac(p * float2(123.34, 345.45)); p += dot(p, p + 34.345); return frac(p.x * p.y); }
            float2 rot(float2 p, float a) { float s = sin(a), c = cos(a); return float2(c * p.x - s * p.y, s * p.x + c * p.y); }

            // Signed distances, unit cell space (shape radius ~0.3).
            float sdCircle(float2 p, float r) { return length(p) - r; }
            float sdRing(float2 p, float r, float w) { return abs(length(p) - r) - w; }
            float sdPlus(float2 p, float r, float w)
            {
                p = abs(p);
                float a = max(p.x - r, p.y - w), b = max(p.x - w, p.y - r);
                return min(a, b);
            }
            // Chubby five-point star: a polar radius with rounded lobes (reads "casual", not sharp).
            float sdStar(float2 p, float r)
            {
                float a = atan2(p.x, p.y);
                float lobe = pow(abs(cos(a * 2.5)), 4.0);
                return (length(p) - r * (0.45 + 0.55 * lobe)) * 0.8;
            }

            // One pattern layer: a jittered grid of shapes, some cells empty.
            float layer(float2 uv, float density, float size, float blur, float seed, float t)
            {
                float2 g = uv * density;
                float2 id = floor(g);
                float2 f = frac(g) - 0.5;
                float h = hash21(id + seed);
                if (h < 0.45) return 0.0;                       // sparse: about half the cells empty
                float2 jitter = (float2(hash21(id + seed + 3.1), hash21(id + seed + 7.7)) - 0.5) * 0.35;
                float2 p = rot(f - jitter, (h * 6.2831) + t * (h - 0.5) * 0.6);
                float s = size * lerp(0.75, 1.15, frac(h * 13.1));
                float kind = frac(h * 7.3);
                float d;
                if (kind < 0.35)      d = sdStar(p, s);
                else if (kind < 0.6)  d = sdCircle(p, s * 0.7);
                else if (kind < 0.8)  d = min(sdRing(p, s * 0.8, s * 0.12), sdPlus(p, s * 0.25, s * 0.06));
                else                  d = (abs(p.x) * 1.3 + abs(p.y)) * 0.7 - s * 0.6;   // gem diamond
                return 1.0 - smoothstep(-blur, blur, d);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float aspect = max(_Aspect, 0.1);
                float2 uv = i.uv;
                float2 suv = float2((uv.x - 0.5) * aspect, uv.y - 0.5);   // square units, centred
                float t = _Time.y;

                // 1. gradient
                fixed3 col = lerp(_BotColor.rgb, _TopColor.rgb, smoothstep(0.0, 1.0, uv.y));

                // 2. rays from a point above the top edge
                float2 rp = suv - float2(0.0, 0.85);
                float ang = atan2(rp.x, -rp.y);
                float rays = 0.5 + 0.5 * sin(ang * _RayCount + t * _RaySpeed * 6.2831);
                rays = smoothstep(0.55, 1.0, rays);
                float rayMask = saturate(1.0 - length(rp) * 0.9);
                col += _RayColor.rgb * rays * rayMask * _RayStrength;

                // 3. parallax pattern layers: far, mid, near
                float a = radians(_Angle);
                float2 dir = float2(sin(a), cos(a));
                float2 puv = rot(suv, a * 0.5);
                float far  = layer(puv + dir * t * _ScrollSpeed * 0.45, _ShapeScale * 1.8, 0.22, 0.09, 11.0, t);
                float mid  = layer(puv + dir * t * _ScrollSpeed * 0.75, _ShapeScale * 1.15, 0.26, 0.05, 23.0, t);
                float near = layer(puv + dir * t * _ScrollSpeed * 1.25, _ShapeScale * 0.75, 0.26, 0.015, 37.0, t);
                float shapes = far * 0.35 + mid * 0.6 + near;
                col = lerp(col, _ShapeColor.rgb, saturate(shapes * _ShapeAlpha));

                // 4. bokeh: a few large soft discs drifting upward
                float bok = 0.0;
                [unroll] for (int k = 0; k < 6; k++)
                {
                    float fk = k;
                    float2 c = float2((hash21(float2(fk, 1.3)) - 0.5) * aspect,
                                      frac(hash21(float2(fk, 5.1)) + t * (0.01 + 0.01 * hash21(float2(fk, 9.2)))) * 1.4 - 0.7);
                    float r = 0.08 + 0.12 * hash21(float2(fk, 2.7));
                    bok += (1.0 - smoothstep(r * 0.4, r, length(suv - c))) * 0.6;
                }
                col = lerp(col, _ShapeColor.rgb, saturate(bok * _BokehAlpha));

                // 5. vignette
                float2 d = uv - 0.5; d.x *= 0.8;
                col *= 1.0 - _Vignette * saturate(dot(d, d) * 2.2);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
