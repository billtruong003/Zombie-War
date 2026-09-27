// AvatarFrame.shader — profile picture frames (owner 2026-09-27: custom frames to win and wear).
// Drawn on a plain quad that is a margin bigger than the avatar: a rounded ring (SDF) whose colour
// runs around it (two colours, or a rainbow for Legend), a bevel highlight, a sheen sweep and an
// outer glow in the margin. AvatarFrameView.cs writes the quad's 0..1 position into uv1.
Shader "ZombieWar/UI/AvatarFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ColorA ("Colour A", Color) = (1,0.9,0.5,1)
        _ColorB ("Colour B", Color) = (0.85,0.6,0.1,1)
        _GlowColor ("Glow", Color) = (1,0.85,0.4,1)
        _GlowStrength ("Glow Strength", Range(0,1)) = 0.3
        _Width ("Ring Width", Range(0.01,0.2)) = 0.08
        _Margin ("Glow Margin", Range(0,0.3)) = 0.12
        _Radius ("Corner Radius", Range(0,0.5)) = 0.22
        _Sheen ("Sheen", Range(0,1)) = 0.6
        _Rainbow ("Rainbow", Range(0,1)) = 0
        _Motion ("Motion", Range(0,1)) = 1

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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
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
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 rect : TEXCOORD1; float4 world : TEXCOORD2; };

            fixed4 _Color, _ColorA, _ColorB, _GlowColor;
            float _GlowStrength, _Width, _Margin, _Radius, _Sheen, _Rainbow, _Motion;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.rect = v.rect;
                o.color = v.color * _Color;
                return o;
            }

            float3 hue(float h) { return saturate(abs(frac(h + float3(0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0) - 1.0); }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Motion;
                float2 p = i.rect - 0.5;
                float b = 0.5 - _Margin;
                float r = min(_Radius, b);
                float2 q = abs(p) - (b - r);
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;   // < 0 inside the avatar box
                float aa = fwidth(d) * 1.2 + 1e-4;

                // ring: from the box edge inwards by _Width
                float ring = smoothstep(aa, -aa, d) * smoothstep(-_Width - aa, -_Width + aa, d);
                float around = atan2(p.y, p.x) / 6.2831853 + 0.5;
                float3 col = lerp(_ColorA.rgb, _ColorB.rgb, 0.5 + 0.5 * cos((around + t * 0.08) * 6.2831853));
                col = lerp(col, hue(around + t * 0.12), _Rainbow);
                // bevel: lighter on the outer half of the ring, darker on the inner lip
                float k = saturate(-d / _Width);
                col *= lerp(1.18, 0.78, k);
                // sheen
                float ph = frac(t / 3.5) * 2.6 - 1.3;
                float band = 1.0 - smoothstep(0.0, 0.08, abs(p.x + p.y * 0.6 - ph));
                col += band * _Sheen * 0.55;

                // outer glow in the margin, pulsing gently
                float glow = exp(-max(d, 0.0) / max(_Margin * 0.35, 1e-3)) * step(0.0, d) * _GlowStrength * (0.8 + 0.2 * sin(t * 2.2));
                float3 gcol = lerp(_GlowColor.rgb, hue(around + t * 0.12), _Rainbow);

                float alpha = saturate(ring + glow);
                float3 outc = (col * ring + gcol * glow) / max(alpha, 1e-4);
                fixed4 o = fixed4(outc, alpha) * i.color;
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
