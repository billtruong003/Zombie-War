// UIHazardStripe.shader — the yellow and black stripe on top of the radio card.
// Cost: no texture; one frac and one smoothstep on a thin strip. Needs UIRectUV (pixel-true
// stripes on any width). _Scroll > 0 makes the stripes crawl while a line is incoming.
Shader "ZombieWar/UI/HazardStripe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (unused)", 2D) = "white" {}
        _Color ("Stripe A", Color) = (1,0.76,0.16,1)
        _ColorB ("Stripe B", Color) = (0.06,0.08,0.12,1)
        _Period ("Period (px)", Float) = 28
        _Scroll ("Scroll (px/s)", Float) = 0

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

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 rect : TEXCOORD1; float2 size : TEXCOORD2; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 px : TEXCOORD0; float4 world : TEXCOORD1; };

            fixed4 _Color, _ColorB;
            half _Period, _Scroll;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.px = v.rect * max(v.size, 1);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float s = frac((i.px.x - i.px.y + _Time.y * _Scroll) / max(_Period, 1));
                fixed4 col = lerp(_Color, _ColorB, step(0.5, s));
                col *= i.color;

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
