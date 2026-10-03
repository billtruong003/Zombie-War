// UIHologram.shader — the radio portrait (live RenderTexture of the agent) seen as a hologram.
// Cost: 2 texture samples (one noise row lookup, one portrait), a handful of ALU, no loops.
//   noise  : one sample of a tiling noise texture per pixel row drives both the glitch shift and
//            the flicker, so nothing random is computed in the shader
//   tint   : luminance-keyed lerp toward the hologram colour keeps the agent's own colours
//   lines  : frac + step on screen-independent uv.y (no sin)
//   fade   : the bottom of the portrait fades out like a projection
// Low tier: set _Glitch to 0 and the noise lookup still drives flicker only (same cost);
// for the cheapest path use a plain UI material, the portrait still reads.
Shader "ZombieWar/UI/Hologram"
{
    Properties
    {
        [PerRendererData] _MainTex ("Portrait", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [NoScaleOffset] _NoiseTex ("Noise (R)", 2D) = "gray" {}
        _HoloColor ("Hologram Colour", Color) = (0.55,1,0.72,1)
        _HoloMix ("Hologram Mix", Range(0,1)) = 0.3
        _Lines ("Scanlines", Float) = 90
        _LineStrength ("Scanline Strength", Range(0,0.5)) = 0.16
        _LineSpeed ("Scanline Speed", Float) = 1.5
        _Glitch ("Glitch Shift", Range(0,0.05)) = 0.014
        _GlitchSpeed ("Glitch Speed", Float) = 0.9
        _Flicker ("Flicker", Range(0,0.4)) = 0.1
        _Fade ("Bottom Fade", Range(0,1)) = 0.22

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
            fixed4 _Color, _HoloColor;
            half _HoloMix, _Lines, _LineStrength, _LineSpeed, _Glitch, _GlitchSpeed, _Flicker, _Fade;
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
                // One noise value per row band, scrolling in time: rare bright rows become glitches.
                half n = tex2D(_NoiseTex, float2(0.37, i.uv.y * 0.35 - _Time.y * _GlitchSpeed)).r;
                half g = saturate((n - 0.72) * 4.0);
                float2 uv = i.uv;
                uv.x += g * _Glitch;

                fixed4 tex = tex2D(_MainTex, uv);
                half lum = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                half3 rgb = lerp(tex.rgb, _HoloColor.rgb * (0.35 + lum * 0.9), _HoloMix);

                half scan = 1 - _LineStrength * step(0.5, frac(i.uv.y * _Lines - _Time.y * _LineSpeed));
                rgb *= scan * (1 - _Flicker * n);
                rgb += _HoloColor.rgb * g * 0.5;

                fixed4 col = fixed4(rgb, tex.a) * i.color;
                col.a *= saturate(i.uv.y / max(_Fade, 0.001));
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
