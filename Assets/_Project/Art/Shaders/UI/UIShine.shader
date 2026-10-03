// UIShine.shader — a slanted light band crossing a button, card or rarity frame every few seconds.
// Cost: 1 texture sample (the sprite itself), the band is a distance-to-line in uv: one frac, one
// abs, one smoothstep. The band only lights the sprite's own opaque pixels.
// Works on simple sprites (uv 0..1). For 9-sliced or atlased sprites feed the rect position in uv1
// as ItemTileFx does, and switch _UseRectUV on.
Shader "ZombieWar/UI/Shine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ShineColor ("Shine Colour", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0,1.5)) = 0.55
        _Width ("Band Width", Range(0.01,0.5)) = 0.1
        _Slant ("Slant", Range(-1.5,1.5)) = 0.55
        _Period ("Period (s)", Float) = 2.6
        _Sweep ("Sweep Share of Period", Range(0.1,1)) = 0.45
        [Toggle] _UseRectUV ("Use Rect UV (uv1)", Float) = 0

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

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 rect : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float2 rect : TEXCOORD1; float4 world : TEXCOORD2; };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color, _ShineColor;
            half _Intensity, _Width, _Slant, _Period, _Sweep, _UseRectUV;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.rect = lerp(v.uv, v.rect, _UseRectUV);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                // The band crosses during the first _Sweep of each period, then rests off the sprite.
                half ph = frac(_Time.y / max(_Period, 0.1)) / _Sweep;
                half pos = ph * (1 + abs(_Slant) + 2 * _Width) - _Width - max(_Slant, 0);
                half d = abs(i.rect.x + i.rect.y * _Slant - pos);
                half band = 1 - smoothstep(0, _Width, d);
                col.rgb += _ShineColor.rgb * band * _Intensity;

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
