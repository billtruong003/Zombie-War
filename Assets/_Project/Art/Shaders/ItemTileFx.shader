// ItemTileFx.shader — rarity look for item tiles (guns, outfits, rewards). Owner 2026-09-27:
// "an image shader, backgrounds with layered effects, especially icons and costumes".
// On the tile's own rounded sprite (alpha = shape), coloured by the vertex colour:
//   1. soft diagonal stripes (depth, not flat fill)
//   2. inner rim glow in the rarity colour (Rare and up)
//   3. a light sheen sweeping across every few seconds (Epic and up)
// ItemTileFx.cs writes the tile's 0..1 rect position into uv1, so effects follow the tile, not the
// 9-slice sprite UVs. UI stencil and RectMask2D clipping are supported (scroll views).
Shader "ZombieWar/UI/ItemTileFx"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlowColor ("Rim Glow Color", Color) = (1,0.8,0.3,1)
        _Glow ("Rim Glow", Range(0,2)) = 0
        _GlowWidth ("Rim Width", Range(0.01,0.5)) = 0.1
        _Stripes ("Stripe Strength", Range(0,0.3)) = 0.06
        _StripeScale ("Stripe Scale", Float) = 7
        _Sheen ("Sheen", Range(0,1)) = 0
        _SheenPeriod ("Sheen Period (s)", Float) = 3.2
        _Aspect ("Tile Aspect (w/h)", Float) = 1

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
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 tile : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float2 tile : TEXCOORD1; float4 world : TEXCOORD2; };

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color, _GlowColor;
            float _Glow, _GlowWidth, _Stripes, _StripeScale, _Sheen, _SheenPeriod, _Aspect;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.tile = v.tile;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 col = i.color;
                float2 t = i.tile;
                float2 sq = float2(t.x * max(_Aspect, 0.01), t.y);

                // 1. diagonal stripes: a lighter band every other stripe
                float s = frac((sq.x + sq.y) * _StripeScale);
                col.rgb += _Stripes * smoothstep(0.45, 0.5, s) * (1 - smoothstep(0.95, 1.0, s));
                // soft top-light so the tile reads as a lit surface
                col.rgb += 0.06 * t.y;

                // 2. inner rim glow in the rarity colour
                float edge = min(min(t.x, 1 - t.x) * max(_Aspect, 0.01), min(t.y, 1 - t.y));
                float rim = 1 - smoothstep(0.0, _GlowWidth, edge);
                col.rgb = lerp(col.rgb, _GlowColor.rgb, saturate(rim * _Glow * 0.8));

                // 3. sheen: a slanted light band crossing the tile once per period
                float ph = frac(_Time.y / max(_SheenPeriod, 0.1)) * 2.4 - 0.7;
                float band = 1 - smoothstep(0.0, 0.09, abs((sq.x - sq.y * 0.6) / max(_Aspect, 0.3) - ph));
                col.rgb += band * _Sheen * 0.35;

                col.a *= tex.a;
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
