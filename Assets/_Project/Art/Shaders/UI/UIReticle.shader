// UIReticle.shader — targeting corners around the thing to use (station pad, card, gun cell,
// joystick): four L brackets with a dark edge, breathing in and out.
// Cost: no texture, a few min/step/smoothstep per pixel on one quad. Needs UIRectUV (uv1 + size),
// so the brackets keep their pixel thickness and length on any rect.
Shader "ZombieWar/UI/Reticle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (unused)", 2D) = "white" {}
        _Color ("Colour", Color) = (1,0.82,0.24,1)
        _EdgeColor ("Edge Colour", Color) = (0.06,0.08,0.12,0.85)
        _Thickness ("Thickness (px)", Float) = 9
        _Length ("Arm Length (px)", Float) = 46
        _Edge ("Edge (px)", Float) = 3
        _Pulse ("Pulse (px)", Float) = 8
        _Speed ("Pulse Speed", Float) = 4

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
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 px : TEXCOORD0; float2 size : TEXCOORD1; float4 world : TEXCOORD2; };

            fixed4 _Color, _EdgeColor;
            half _Thickness, _Length, _Edge, _Pulse, _Speed;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.size = max(v.size, 1);
                o.px = v.rect * o.size;
                o.color = v.color * _Color;
                return o;
            }

            // Coverage of an L bracket of thickness th and arm length len, in a corner at (0,0).
            half Bracket(float2 d, half th, half len)
            {
                half arms = max(step(d.x, th) * step(d.y, len), step(d.y, th) * step(d.x, len));
                return arms * step(0, d.x) * step(0, d.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half inset = _Pulse * (0.5 + 0.5 * sin(_Time.y * _Speed));
                // Distance to the nearest vertical and horizontal edge, after the breathing inset.
                float2 d = min(i.px, i.size - i.px) - inset;
                half inner = Bracket(d - _Edge, _Thickness, _Length);
                half outer = Bracket(d, _Thickness + _Edge * 2, _Length + _Edge * 2);
                fixed4 col = lerp(_EdgeColor, i.color, inner);
                col.a *= outer;

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
