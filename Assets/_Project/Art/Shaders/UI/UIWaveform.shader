// UIWaveform.shader — the "speaking" bars next to the radio channel label.
// Cost: 1 noise sample per pixel (bar height from a scrolling noise row, so nothing random is
// computed), a frac and two steps. _Speaking (0..1) is set by RadioCallView every frame from the voice
// line's loudness (RadioVoice.Level, envelopes from Tools/vo_envelopes.py), so the bars move with it.
Shader "ZombieWar/UI/Waveform"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (unused)", 2D) = "white" {}
        _Color ("Colour", Color) = (0.66,0.88,0.35,1)
        [NoScaleOffset] _NoiseTex ("Noise (R)", 2D) = "gray" {}
        _NoiseRange ("Noise Min/Max (x,y)", Vector) = (0.45,0.95,0,0)
        _Bars ("Bars", Float) = 5
        _Gap ("Gap", Range(0.1,0.8)) = 0.4
        _Speed ("Speed", Float) = 2.2
        _Speaking ("Speaking", Range(0,1)) = 1
        _Idle ("Idle Height", Range(0,1)) = 0.18

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

            sampler2D _NoiseTex;
            fixed4 _Color;
            float4 _NoiseRange;
            half _Bars, _Gap, _Speed, _Speaking, _Idle;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float x = i.uv.x * _Bars;
                half bar = floor(x);
                half inBar = step(_Gap * 0.5, frac(x)) * step(frac(x), 1 - _Gap * 0.5);
                // Each bar jitters on its own (noise row scrolling fast enough to read as syllables);
                // the voice's loudness (_Speaking) sets how high they all go.
                half n = saturate((tex2D(_NoiseTex, float2(bar * 0.19 + 0.07, _Time.y * _Speed * 0.6)).r - _NoiseRange.x) / max(_NoiseRange.y - _NoiseRange.x, 0.01));
                half h = _Idle + (1 - _Idle) * _Speaking * lerp(0.35, 1, n);
                // Bars grow from the bottom.
                fixed4 col = i.color;
                col.a *= inBar * step(i.uv.y, h);

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
