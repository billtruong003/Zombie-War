Shader "ZombieWar/Weapon/SkinSet"
{
    // M8 gun skin sets (owner: sell skins by set so one look covers every gun without per-gun art).
    // Every pattern is procedural 3D noise in object space, so it wraps any gun with no UV work.
    // The gun's own albedo is kept as a luminance detail layer, so its parts still read.
    // _Style picks the set: 0 Inferno, 1 Cosmos, 2 Frostbite, 3 Biohazard, 4 Neon Circuit, 5 Gilded.
    Properties
    {
        _BaseMap      ("Gun albedo (detail)", 2D) = "white" {}
        _Style        ("Style", Float) = 0
        _ColorA       ("Base colour", Color) = (0.08, 0.07, 0.07, 1)
        _ColorB       ("Second colour", Color) = (0.35, 0.08, 0.04, 1)
        [HDR] _ColorC ("Glow colour (HDR)", Color) = (4, 1.2, 0.2, 1)
        _Scale        ("Pattern scale", Float) = 6
        _Speed        ("Animation speed", Float) = 1
        _Detail       ("Albedo detail", Range(0, 1)) = 0.45
        _Glow         ("Glow strength", Range(0, 3)) = 1
        _Spec         ("Specular", Range(0, 2)) = 0.4
        _Gloss        ("Gloss power", Range(4, 256)) = 48
        _RimPower     ("Rim power", Range(1, 8)) = 3
        _Ramp0        ("Palette dark", Color) = (0.05, 0.05, 0.05, 1)
        _Ramp1        ("Palette mid", Color) = (0.3, 0.3, 0.3, 1)
        _Ramp2        ("Palette light", Color) = (0.8, 0.8, 0.8, 1)
        _Metal        ("Metal (fake reflection)", Range(0, 1)) = 0.2
        _Edge         ("Edge highlight", Range(0, 3)) = 1
        [HDR] _EdgeColor ("Edge colour", Color) = (1, 1, 1, 1)
        [Toggle] _ToonMetal ("Toon metal (stepped specular)", Float) = 0
        _SpecCut      ("Toon spec cut", Range(0, 1)) = 0.82
        _SpecColor2   ("Toon spec colour", Color) = (1, 0.85, 0.3, 1)
        _HiOffset     ("Toon highlight offset", Range(0, 1)) = 0.93
        _HiColor      ("Toon highlight colour (a = strength)", Color) = (1, 0.98, 0.8, 0.6)
        _RimCut       ("Toon rim cut", Range(0, 1)) = 0.72
        _RimColor     ("Toon rim colour", Color) = (0.9, 0.6, 0.15, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _ColorA, _ColorB, _ColorC;
            float _Style, _Scale, _Speed, _Detail, _Glow, _Spec, _Gloss, _RimPower;
            float4 _Ramp0, _Ramp1, _Ramp2, _EdgeColor;
            float _Metal, _Edge;
            float _ToonMetal, _SpecCut, _HiOffset, _RimCut;
            float4 _SpecColor2, _HiColor, _RimColor;
        CBUFFER_END

        // Set per renderer (MaterialPropertyBlock) by WeaponSkinApplier: the gun root's world-to-local
        // matrix and the gun's length, so the pattern is continuous across every part of one gun and
        // the same size on a pistol and a rifle. _SkinSize 0 falls back to the mesh's own space.
        float4x4 _SkinRootW2L;
        float _SkinSize;

        float Hash31(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float3 Hash33(float3 p)
        {
            return float3(Hash31(p), Hash31(p + 31.7), Hash31(p + 57.3));
        }

        float Noise3(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash31(i + float3(0,0,0)), Hash31(i + float3(1,0,0)), f.x),
                             lerp(Hash31(i + float3(0,1,0)), Hash31(i + float3(1,1,0)), f.x), f.y),
                        lerp(lerp(Hash31(i + float3(0,0,1)), Hash31(i + float3(1,0,1)), f.x),
                             lerp(Hash31(i + float3(0,1,1)), Hash31(i + float3(1,1,1)), f.x), f.y), f.z);
        }

        float Fbm(float3 p)
        {
            float v = 0.0, a = 0.5;
            for (int o = 0; o < 4; o++) { v += a * Noise3(p); p = p * 2.03 + 11.7; a *= 0.5; }
            return v;
        }

        // Distance to the nearest and second-nearest feature point (cellular noise).
        float2 Voronoi(float3 p)
        {
            float3 i = floor(p), f = frac(p);
            float d1 = 8.0, d2 = 8.0;
            for (int z = -1; z <= 1; z++)
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                float3 g = float3(x, y, z);
                float3 r = g + Hash33(i + g) - f;
                float d = dot(r, r);
                if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
            }
            return float2(sqrt(d1), sqrt(d2));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 wp : TEXCOORD2; float3 op : TEXCOORD3; };

            V vert (A i)
            {
                V o;
                o.wp = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.wp);
                o.n = TransformObjectToWorldNormal(i.n);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.op = i.pos.xyz;
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 v = normalize(GetWorldSpaceViewDir(i.wp));
                Light light = GetMainLight();
                float3 l = light.direction;
                float ndl = saturate(dot(n, l) * 0.5 + 0.5);            // half-lambert: toon-friendly
                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(n, h)), _Gloss) * _Spec;
                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower);
                float lum = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb, float3(0.299, 0.587, 0.114));
                float detail = 1.0;
                // Gradient map (owner: recolour from the gun's own palette, not a flat coat): each part
                // keeps its light/dark place in the gun but takes the set's colours, so parts stay apart.
                float k = saturate(pow(max(lum, 1e-4), 0.8));
                float3 grad = k < 0.5 ? lerp(_Ramp0.rgb, _Ramp1.rgb, k * 2.0) : lerp(_Ramp1.rgb, _Ramp2.rgb, k * 2.0 - 1.0);
                grad = lerp(_Ramp1.rgb, grad, saturate(0.5 + _Detail));   // _Detail: how far parts spread apart
                // Bevel edges: low-poly faces meet at hard normals; the normal jumps there, nowhere else.
                float edge = saturate(length(fwidth(n)) * 2.5 - 0.15) * _Edge;
                // Fake environment for metal: warm sky, bright horizon band, dark ground.
                float3 rv = reflect(-v, n);
                float3 env = lerp(float3(0.10, 0.09, 0.08), float3(0.95, 0.92, 0.85), smoothstep(-0.25, 0.35, rv.y));
                env += float3(1.2, 1.1, 0.9) * pow(saturate(1.0 - abs(rv.y - 0.05) * 6.0), 3.0);

                float t = _Time.y * _Speed;
                float3 sp = _SkinSize > 0.0 ? mul(_SkinRootW2L, float4(i.wp, 1.0)).xyz / _SkinSize : i.op;
                float3 p = sp * _Scale;
                // Planar coordinates on the face the surface points along (lines stay thin on every side).
                float3 rn = _SkinSize > 0.0 ? normalize(mul((float3x3)_SkinRootW2L, n)) : n;
                float3 an = abs(rn);
                float2 pp = an.x > an.y && an.x > an.z ? p.zy : (an.y > an.z ? p.xz : p.xy);
                float3 albedo = _ColorA.rgb;
                float3 glow = 0;
                int style = (int)round(_Style);

                if (style == 0)          // Inferno: charcoal shell, lava veins that crawl
                {
                    float f = Fbm(p * 1.1 + float3(0, -t * 0.25, 0));
                    float ridge = 1.0 - abs(f * 2.0 - 1.0);
                    float vein = smoothstep(0.93, 0.99, ridge);
                    float heat = smoothstep(0.8, 0.95, ridge);
                    albedo = lerp(grad, _ColorB.rgb, heat * 0.8);
                    float pulse = 0.7 + 0.3 * sin(t * 3.0 + f * 12.0);
                    glow = _ColorC.rgb * (vein * pulse + heat * 0.12) + _ColorC.rgb * 0.06 * rim;
                }
                else if (style == 1)     // Cosmos: night body, drifting nebula, twinkling stars
                {
                    float neb = Fbm(p * 0.7 + float3(t * 0.04, 0, t * 0.03));
                    albedo = lerp(grad, _ColorB.rgb, smoothstep(0.35, 0.8, neb) * 0.7);
                    float3 cell = floor(p * 2.2);
                    float3 fp = frac(p * 2.2) - 0.5;
                    float starOn = step(0.6, Hash31(cell));
                    float star = starOn * smoothstep(0.16, 0.0, length(fp - (Hash33(cell) - 0.5) * 0.6));
                    float twinkle = 0.6 + 0.4 * sin(t * 4.0 + Hash31(cell + 3.1) * 40.0);
                    float band = smoothstep(0.55, 0.8, neb);
                    glow = _ColorC.rgb * (star * twinkle * 2.0 + band * 0.35) + float3(0.9, 0.35, 1.4) * smoothstep(0.62, 0.8, neb) * 0.35 + float3(0.4, 0.8, 1.6) * rim * 0.5;
                }
                else if (style == 2)     // Frostbite: cracked ice crystals, white edges, frost rim
                {
                    float2 vo = Voronoi(p * 0.8);
                    float edge = 1.0 - smoothstep(0.02, 0.09, vo.y - vo.x);
                    albedo = grad * lerp(0.85, 1.1, saturate(vo.x * 1.4));
                    float crack = 1.0 - smoothstep(0.02, 0.09, vo.y - vo.x);
                    albedo = lerp(albedo, float3(0.95, 0.98, 1.0), crack * 0.8);
                    float sparkle = step(0.97, Hash31(floor(p * 6.0))) * (0.5 + 0.5 * sin(t * 5.0 + Hash31(floor(p * 6.0)) * 30.0));
                    glow = _ColorC.rgb * (rim * 0.6 + sparkle * 0.8 + crack * 0.12);
                }
                else if (style == 3)     // Biohazard: dark olive metal, acid dripping down, pulsing
                {
                    float drip = Fbm(float3(p.x * 1.4, p.y * 0.35 + t * 0.35, p.z * 1.4));
                    float goo = smoothstep(0.52, 0.66, drip);
                    float2 bub = Voronoi(p * 1.2 + float3(0, -t * 0.4, 0));
                    float bubble = smoothstep(0.16, 0.1, bub.x) * goo;
                    albedo = lerp(grad, _ColorB.rgb, goo);
                    float pulse = 0.65 + 0.35 * sin(t * 2.2);
                    glow = _ColorC.rgb * (goo * 0.55 * pulse + bubble * 1.2) + _ColorC.rgb * 0.08 * rim;
                }
                else if (style == 4)     // Neon Circuit: matte black, traces that pulse along the grid
                {
                    float2 q = pp * 1.6;
                    float2 cell = floor(q);
                    float2 fq = frac(q);
                    float pick = Hash31(float3(cell, 1.0));
                    float lineX = smoothstep(0.045, 0.0, abs(fq.y - 0.5)) * step(0.45, pick);
                    float lineY = smoothstep(0.045, 0.0, abs(fq.x - 0.5)) * step(pick, 0.55);
                    float node = smoothstep(0.13, 0.09, length(fq - 0.5)) * step(0.75, Hash31(float3(cell, 7.0)));
                    float trace = saturate(lineX + lineY + node);
                    float runner = pow(0.5 + 0.5 * sin((q.x + q.y) * 1.3 - t * 5.0), 8.0);
                    float3 hue = lerp(_ColorC.rgb, float3(2.8, 0.25, 2.4), step(0.6, Hash31(float3(floor(q * 0.25), 13.0))));
                    albedo = grad;
                    glow = hue * trace * (0.35 + runner * 1.4) + hue * 0.1 * rim;
                }
                else                     // Gilded: polished gold with engraved scrollwork
                {
                    float2 g = pp * 1.4;
                    float scroll = sin(g.x * 3.0 + sin(g.y * 2.6) * 2.4) + 0.6 * sin(g.y * 5.0 + sin(g.x * 1.7) * 1.8);
                    float groove = 1.0 - smoothstep(0.0, 0.16, abs(scroll));
                    albedo = grad * (1.0 - groove * 0.45);
                    float sweep = pow(saturate(sin(dot(i.wp, float3(1, 0.4, 0.6)) * 3.0 - t * 1.5)), 24.0);
                    glow = _ColorC.rgb * (sweep * 0.6 + rim * 0.25);
                    spec *= 1.0 - groove * 0.7;
                }

                // Three-step toon light keeps the flat faces reading as separate planes.
                float band = ndl < 0.45 ? 0.45 : (ndl < 0.8 ? 0.78 : 1.0);
                float3 diffuse = albedo * detail * (0.28 + 0.85 * band * light.color);
                float3 metal = albedo * env * (0.55 + 0.45 * band);
                // Specular takes the surface colour on metal and stays soft elsewhere, so a big flat face
                // turned to the light does not wash out to plain grey.
                float3 specCol = lerp(albedo * 0.7 + 0.08, albedo * 1.8 + 0.1, _Metal);
                float3 lit = lerp(diffuse, metal, _Metal) + spec * specCol * light.color;
                lit += _EdgeColor.rgb * edge * (0.35 + 0.65 * band);
                if (_ToonMetal > 0.5)
                {
                    // Toon metal (owner reference): hard steps instead of smooth reflection.
                    // Specular from view + light cut to a flat bright colour, a light-only highlight
                    // stepped on top, and a stepped rim. Flat low-poly faces read as polished plates.
                    // A studio key light fixed to the camera (upper left, in front), like the reference:
                    // gold reads the same in icons, shop and run whatever the scene light does.
                    float3 camRight = UNITY_MATRIX_V[0].xyz, camUp = UNITY_MATRIX_V[1].xyz, camBack = UNITY_MATRIX_V[2].xyz;
                    float3 tl = normalize(-0.7 * camRight + 0.75 * camUp + 0.3 * camBack);
                    float sDot = dot(normalize(v + tl), n);
                    float specMask = step(_SpecCut, sDot);
                    float shade = dot(tl, n) < 0.0 ? 0.62 : 1.0;
                    float3 toon = lerp(albedo * shade, _SpecColor2.rgb, specMask);
                    toon += step(_HiOffset, dot(tl, n)) * _HiColor.rgb * _HiColor.a;
                    toon += step(_RimCut, 1.0 - saturate(dot(n, v))) * _RimColor.rgb * _RimColor.a;
                    toon += _EdgeColor.rgb * edge * 0.6;
                    lit = toon * (0.6 + 0.4 * light.color);
                }
                return half4(lit + glow * _Glow, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos : POSITION; };
            float4 vert (A i) : SV_POSITION { return TransformObjectToHClip(i.pos.xyz); }
            half frag () : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float3 _LightDirection;
            struct A { float4 pos : POSITION; float3 n : NORMAL; };
            float4 vert (A i) : SV_POSITION
            {
                float3 wp = TransformObjectToWorld(i.pos.xyz);
                float3 wn = TransformObjectToWorldNormal(i.n);
                float4 c = TransformWorldToHClip(ApplyShadowBias(wp, wn, _LightDirection));
                #if UNITY_REVERSED_Z
                    c.z = min(c.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    c.z = max(c.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return c;
            }
            half frag () : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
