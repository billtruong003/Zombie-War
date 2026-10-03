// UISpotlight.shader — dims the screen except a rounded hole around the target (first gun cell,
// a button), with a soft edge and a thin bright rim that breathes.
// Cost: no texture, one rounded-box distance per pixel. Full-screen, so it is the most expensive
// FTUE effect by area; keep it on screen only while the step is active. Needs UIRectUV; the hole
// (centre and size in the overlay's pixels) is written by UISpotlightHole.
Shader "ZombieWar/UI/Spotlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (unused)", 2D) = "white" {}
        _Color ("Dim Colour", Color) = (0.03,0.05,0.09,0.88)
        _RimColor ("Rim Colour", Color) = (1,0.82,0.24,1)
        _Hole ("Hole (centre xy, size zw, px)", Vector) = (540,960,320,280)
        _Radius ("Corner Radius (px)", Float) = 26
        _Feather ("Feather (px)", Float) = 14
        _Rim ("Rim Width (px)", Float) = 4
        _Speed ("Rim Pulse Speed", Float) = 3

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

            fixed4 _Color, _RimColor;
            float4 _Hole;
            half _Radius, _Feather, _Rim, _Speed;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.px = v.rect * max(v.size, 1);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Signed distance to the rounded hole: negative inside.
                float2 q = abs(i.px - _Hole.xy) - (_Hole.zw * 0.5 - _Radius);
                float sd = length(max(q, 0)) + min(max(q.x, q.y), 0) - _Radius;
                half dim = smoothstep(0, _Feather, sd);
                half rim = (1 - smoothstep(0, _Rim, abs(sd - _Rim))) * (0.65 + 0.35 * sin(_Time.y * _Speed));
                fixed4 col = i.color;
                col.a *= dim;
                col = lerp(col, _RimColor, rim * _RimColor.a);

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
