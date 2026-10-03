// UIDissolve.shader — a UI element burning in (feature unlock, gacha reveal, radio pop-in) or out.
// Cost: 2 texture samples (sprite + dissolve noise), one smoothstep, one step. _Progress is driven
// by code (BillTween), so the shader never computes time.
//   0 = hidden, 1 = fully shown; the glowing edge sits on the noise threshold.
Shader "ZombieWar/UI/Dissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [NoScaleOffset] _NoiseTex ("Dissolve Noise (R)", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 1
        _NoiseRange ("Noise Min/Max (x,y)", Vector) = (0,1,0,0)
        _Progress ("Progress", Range(0,1)) = 1
        _EdgeWidth ("Edge Width", Range(0.005,0.3)) = 0.08
        _EdgeColor ("Edge Colour", Color) = (1,0.78,0.25,1)
        _EdgeBoost ("Edge Brightness", Range(0,3)) = 1.4

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

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 world : TEXCOORD1; };

            sampler2D _MainTex, _NoiseTex;
            float4 _MainTex_ST;
            fixed4 _Color, _EdgeColor;
            half _NoiseScale, _Progress, _EdgeWidth, _EdgeBoost;
            half4 _NoiseRange;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                // Most noise textures do not span 0..1; stretch the measured range so _Progress is linear.
                half n = saturate((tex2D(_NoiseTex, i.uv * _NoiseScale).r - _NoiseRange.x) / max(_NoiseRange.y - _NoiseRange.x, 0.01));
                // Stretch the threshold past both ends so 0 and 1 are fully hidden and fully shown.
                half t = _Progress * (1 + _EdgeWidth) - _EdgeWidth;
                half edge = 1 - smoothstep(t, t + _EdgeWidth, n);
                col.rgb = lerp(col.rgb, _EdgeColor.rgb * _EdgeBoost, edge * step(t, n) * step(0.001, _Progress));
                col.a *= step(n, t + _EdgeWidth);

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
