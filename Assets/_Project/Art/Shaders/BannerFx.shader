// BannerFx.shader — living background for premium banners (Shop starter pack, boutique, Gacha
// banners). Owner 2026-09-27: "a flat background with a sprite on it looks cheap; premium things
// must look worth the money". Drawn on the banner's own rounded sprite (alpha = shape):
//   diagonal two-colour gradient that breathes, slow rotating rays behind the item, a soft core
//   glow, a drifting diamond pattern, twinkling sparkles rising, a sheen sweep and a rim glow.
// BannerFx.cs writes the banner's 0..1 rect position into uv1 and its aspect into _Aspect, and
// scales _Motion down on Low graphics. UI stencil and RectMask2D clipping are supported.
Shader "ZombieWar/UI/BannerFx"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ColorA ("Colour A (top left)", Color) = (0.46,0.39,0.87,1)
        _ColorB ("Colour B (bottom right)", Color) = (0.9,0.28,0.49,1)
        _Accent ("Accent (rays, glow)", Color) = (1,0.85,0.5,1)
        _RayCenter ("Ray Centre (0..1)", Vector) = (0.75,0.55,0,0)
        _RayCount ("Ray Count", Float) = 9
        _RayStrength ("Ray Strength", Range(0,1)) = 0.22
        _Glow ("Core Glow", Range(0,1)) = 0.35
        _Pattern ("Pattern Strength", Range(0,0.3)) = 0.07
        _PatternScale ("Pattern Scale", Float) = 7
        _Sparkle ("Sparkles", Range(0,1)) = 0.8
        _Sheen ("Sheen", Range(0,1)) = 0.6
        _SheenPeriod ("Sheen Period (s)", Float) = 4.5
        _Rim ("Rim Colour", Color) = (1,0.84,0.35,1)
        _RimStrength ("Rim Strength", Range(0,1.5)) = 0
        _Motion ("Motion", Range(0,1)) = 1
        _Aspect ("Aspect (w/h)", Float) = 2

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 rect : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float2 rect : TEXCOORD1; float4 world : TEXCOORD2; };

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color, _ColorA, _ColorB, _Accent, _Rim;
            float4 _RayCenter, _ClipRect;
            float _RayCount, _RayStrength, _Glow, _Pattern, _PatternScale, _Sparkle, _Sheen, _SheenPeriod, _RimStrength, _Motion, _Aspect;

            v2f vert (appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.rect = v.rect;
                o.color = v.color * _Color;
                return o;
            }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            fixed4 frag (v2f i) : SV_Target
            {
                float a = max(_Aspect, 0.01);
                float2 p = i.rect;
                float2 q = float2(p.x * a, p.y);
                float t = _Time.y * _Motion;

                // 1. breathing diagonal gradient
                float g = saturate(dot(p - 0.5, normalize(float2(1.0, -0.7))) * 0.95 + 0.5 + sin(t * 0.35) * 0.07);
                float3 col = lerp(_ColorA.rgb, _ColorB.rgb, g);

                // 2. rays turning slowly behind the item, and a soft core glow
                float2 d = q - float2(_RayCenter.x * a, _RayCenter.y);
                float r = length(d);
                float ang = atan2(d.y, d.x);
                float rays = pow(0.5 + 0.5 * cos(ang * _RayCount + t * 0.3), 5.0) * smoothstep(1.3, 0.05, r);
                col += _Accent.rgb * rays * _RayStrength;
                col += _Accent.rgb * exp(-r * r * 7.0) * _Glow;

                // 3. drifting diamond outlines
                float2 pu = q * _PatternScale + float2(t * 0.05, -t * 0.08);
                float2 c = frac(pu) - 0.5;
                float dm = abs(c.x) + abs(c.y);
                float pat = smoothstep(0.24, 0.22, dm) - smoothstep(0.19, 0.17, dm);
                col += pat * _Pattern;

                // 4. sparkles rising and twinkling (one in three cells has one)
                float2 sp = q * 6.0 + float2(0.0, -t * 0.22);
                float2 id = floor(sp);
                float2 f = frac(sp) - 0.5;
                float h = hash21(id);
                float2 off = (float2(hash21(id + 3.1), hash21(id + 7.7)) - 0.5) * 0.6;
                float2 e = f - off;
                float tw = 0.5 + 0.5 * sin(t * 3.0 + h * 40.0);
                float live = step(0.66, h);
                float star = smoothstep(0.07, 0.0, length(e))
                           + smoothstep(0.012, 0.0, abs(e.x)) * smoothstep(0.16, 0.0, abs(e.y)) * 0.7
                           + smoothstep(0.012, 0.0, abs(e.y)) * smoothstep(0.16, 0.0, abs(e.x)) * 0.7;
                col += star * live * tw * _Sparkle;

                // 5. sheen sweep
                float ph = frac(t / max(_SheenPeriod, 0.1)) * 2.8 - 0.9;
                float band = 1.0 - smoothstep(0.0, 0.07, abs((q.x - q.y * 0.55) / a - ph));
                col += band * _Sheen * 0.3;

                // 6. rim glow (premium) and a light vignette
                float edge = min(min(p.x, 1.0 - p.x) * a, min(p.y, 1.0 - p.y));
                float rim = 1.0 - smoothstep(0.0, 0.06, edge);
                col = lerp(col, _Rim.rgb, saturate(rim * _RimStrength));
                col *= lerp(1.0, 0.82, saturate(length(p - 0.5) * 1.1));

                fixed4 o = fixed4(col, 1.0) * i.color;
                o.a *= tex2D(_MainTex, i.uv).a;
                #ifdef UNITY_UI_CLIP_RECT
                o.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(o.a - 0.001);
                #endif
                return o;
            }
            ENDCG
        }
    }
}
