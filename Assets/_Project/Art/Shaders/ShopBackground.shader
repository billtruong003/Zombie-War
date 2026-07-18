// ShopBackground.shader — animated portrait shop background.
// Kỹ thuật chung (KHÔNG copy asset gốc): screen-independent UV + scrolling plasma pattern
// + vertical 3-stop gradient + vignette. Xem Docs/WEAPON_DESIGN.md §9.
// Unlit, pipeline-agnostic (chạy cả URP lẫn Built-in). Procedural => không cần gán texture.
Shader "ZombieWar/ShopBackground"
{
    Properties
    {
        [Header(Gradient portrait)]
        _TopColor    ("Top Color",    Color) = (0.05, 0.06, 0.10, 1)
        _MidColor    ("Mid Color",    Color) = (0.12, 0.14, 0.22, 1)
        _BotColor    ("Bottom Color", Color) = (0.03, 0.03, 0.06, 1)
        _MidPoint    ("Mid Point (y)", Range(0,1)) = 0.5
        _MidSharp    ("Mid Sharpness", Range(0.05,1)) = 0.55

        [Header(Scrolling pattern)]
        _PatternColor    ("Pattern Color", Color) = (0.30, 0.45, 0.75, 1)
        _PatternStrength ("Pattern Strength", Range(0,1)) = 0.35
        _PatternScale    ("Pattern Scale", Float) = 3.0
        _ScrollSpeed     ("Scroll Speed", Float) = 0.03
        _WarpSpeed       ("Warp Speed", Float) = 0.15
        _WarpAmount      ("Warp Amount", Range(0,1)) = 0.4

        [Header(Vignette)]
        _Vignette      ("Vignette Strength", Range(0,1)) = 0.55
        _VignettePower ("Vignette Power", Range(0.5,4)) = 1.6
        _VignetteAspect("Vignette Aspect (x squeeze)", Range(0.2,1)) = 0.55

        [Header(Optional texture multiply)]
        _MainTex ("Pattern Tex (optional)", 2D) = "white" {}
    }

    SubShader
    {
        // Background: vẽ trước, không ghi depth, không cull — dùng cho fullscreen quad hoặc UI RawImage.
        Tags { "RenderType"="Opaque" "Queue"="Background" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _TopColor, _MidColor, _BotColor, _PatternColor;
            float _MidPoint, _MidSharp;
            float _PatternStrength, _PatternScale, _ScrollSpeed, _WarpSpeed, _WarpAmount;
            float _Vignette, _VignettePower, _VignetteAspect;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Value-noise 2D + fbm (không cần texture). Rẻ, đủ mượt cho background.
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }
            float vnoise(float2 p)
            {
                float2 i = floor(p); float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }
            float fbm(float2 p)
            {
                float s = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++) { s += a * vnoise(p); p *= 2.02; a *= 0.5; }
                return s;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // ── Screen-independent UV: chia X theo aspect để pattern không méo trên màn dọc.
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float2 suv = i.uv;
                suv.x = (suv.x - 0.5) * aspect + 0.5;

                float t = _Time.y;

                // ── Layer 1: vertical 3-stop gradient (đỉnh tối → giữa sáng → đáy tối).
                float yl = smoothstep(_MidPoint - _MidSharp, _MidPoint, i.uv.y);          // bottom→mid
                float yu = smoothstep(_MidPoint, _MidPoint + _MidSharp, i.uv.y);          // mid→top
                fixed4 grad = lerp(lerp(_BotColor, _MidColor, yl), _TopColor, yu);

                // ── Layer 2: scrolling plasma pattern (domain-warp bằng fbm, cuộn chậm).
                float2 puv = suv * _PatternScale;
                float2 warp = float2(fbm(puv + t * _WarpSpeed),
                                     fbm(puv + 7.3 + t * _WarpSpeed)) - 0.5;
                puv += warp * _WarpAmount;
                puv.y -= t * _ScrollSpeed;                     // cuộn dọc lên trên
                float pat = fbm(puv);
                pat = smoothstep(0.35, 0.85, pat);             // cut off cho có "sợi"
                pat *= tex2D(_MainTex, TRANSFORM_TEX(suv, _MainTex)).r; // optional texture multiply (mặc định white=1)

                fixed4 col = grad + _PatternColor * (pat * _PatternStrength);

                // ── Layer 3: vignette (bóp X để focus cột card giữa trên màn dọc).
                float2 d = i.uv - 0.5;
                d.x /= max(_VignetteAspect, 0.001);
                float vig = 1.0 - _Vignette * pow(saturate(dot(d, d) * 4.0), _VignettePower);
                col.rgb *= vig;

                col.a = 1.0;
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
