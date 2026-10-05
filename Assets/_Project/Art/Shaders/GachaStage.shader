// GachaStage.shader — the backdrop of the gacha chest show (backlog #12, mockup U5, owner 05/10:
// "use shaders for the dark screen, not simple 2D; drop the ring under the feet; make it as
// beautiful as possible"). One full-screen quad, everything procedural:
//   1. a deep radial gradient tinted by the rarity colour
//   2. god rays fanning from the prize, turning slowly, soft-edged, brighter near the centre
//   3. a bloom-like glow at the centre that breathes while the chest charges
//   4. twinkling dust drifting upward (hash grid, no textures)
//   5. a shock wave ring + flash when the chest bursts (_Burst 0..1)
//   6. a vignette so the prize stays the brightest thing on screen
// GachaSpotlight.cs drives _Charge, _Rays, _Spin, _Burst and _T (unscaled seconds).
Shader "ZombieWar/UI/GachaStage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TierColor ("Rarity Color", Color) = (0.85,0.9,1,1)
        _Base ("Base Color", Color) = (0.03,0.03,0.08,1)
        _Center ("Center (uv)", Vector) = (0.5,0.53,0,0)
        _Aspect ("Aspect (w/h)", Float) = 0.5625
        _Charge ("Charge", Range(0,1)) = 0
        _Rays ("Rays", Range(0,1.5)) = 0.3
        _RayCount ("Ray Count", Float) = 14
        _Spin ("Spin (radians)", Float) = 0
        _Glow ("Glow", Range(0,2)) = 0.5
        _Burst ("Burst", Range(0,1)) = 1
        _Dust ("Dust", Range(0,2)) = 1
        _T ("Time (s)", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            fixed4 _Color, _TierColor, _Base;
            float4 _Center;
            float _Aspect, _Charge, _Rays, _RayCount, _Spin, _Glow, _Burst, _Dust, _T;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            // Twinkling points on a jittered grid, drifting upward; returns brightness.
            float dust(float2 p, float scale, float speed, float t)
            {
                p.y -= t * speed;
                float2 cell = floor(p * scale);
                float2 f = frac(p * scale) - 0.5;
                float h = hash21(cell);
                float2 off = float2(hash21(cell + 7.1), hash21(cell + 3.3)) - 0.5;
                float d = length(f - off * 0.7);
                float tw = 0.5 + 0.5 * sin(t * (1.5 + h * 3.0) + h * 40.0);
                float on = step(0.72, h);                 // only some cells hold a mote
                return on * tw * smoothstep(0.09, 0.0, d);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv - _Center.xy;
                p.x *= _Aspect;                            // circles stay round on a tall screen
                float r = length(p);
                float ang = atan2(p.y, p.x);
                float3 tier = _TierColor.rgb;

                // 1. radial gradient: a warm pool of the rarity colour fading to deep navy
                float pool = exp(-r * r * 6.0);
                float3 col = lerp(_Base.rgb, _Base.rgb + tier * 0.16, pool);
                col += tier * 0.035 * exp(-r * 1.6);

                // 2. god rays: a fan of soft wedges that turns; two layers at different counts
                float a1 = ang * _RayCount * 0.5 + _Spin;
                float a2 = ang * (_RayCount * 0.5 + 3.0) - _Spin * 0.6;
                float w1 = pow(saturate(0.5 + 0.5 * cos(a1 * 2.0)), 6.0);
                float w2 = pow(saturate(0.5 + 0.5 * cos(a2 * 2.0)), 10.0);
                float fall = exp(-r * 3.1) * smoothstep(0.02, 0.16, r);     // fade with distance, not at the very centre
                float rays = (w1 * 0.6 + w2 * 0.3) * fall * _Rays;
                col += tier * rays;

                // 3. centre glow: breathes faster as the charge builds
                float breathe = 0.85 + 0.15 * sin(_T * lerp(2.0, 14.0, _Charge));
                float glow = exp(-r * r * lerp(30.0, 18.0, _Charge)) * _Glow * breathe;
                col += lerp(tier, float3(1, 1, 1), 0.4) * glow * 0.6;

                // 4. dust: two layers, near ones bigger and faster
                float d = dust(i.uv * float2(_Aspect, 1.0), 9.0, 0.03, _T) * 0.8
                        + dust(i.uv * float2(_Aspect, 1.0) + 3.7, 17.0, 0.015, _T) * 0.5;
                col += lerp(tier, float3(1, 1, 1), 0.6) * d * _Dust * (0.35 + 0.65 * pool + _Charge * 0.4);

                // 5. burst: an expanding bright ring and a flash that fades
                float b = saturate(_Burst);
                float ringR = b * 1.35;
                float ring = exp(-pow((r - ringR) / (0.025 + 0.06 * b), 2.0)) * (1.0 - b);
                col += lerp(tier, float3(1, 1, 1), 0.6) * ring * 1.6;
                col += float3(1, 1, 1) * (1.0 - smoothstep(0.0, 0.35, b)) * step(b, 0.999) * 0.55 * exp(-r * 1.2);

                // 6. vignette
                float2 uvc = i.uv - 0.5;
                float vig = 1.0 - smoothstep(0.35, 0.85, length(uvc * float2(1.0, 0.8)));
                col *= lerp(0.22, 1.0, vig);

                // soft shoulder: bright rays roll off instead of clipping the whole screen to white-gold
                col = col / (1.0 + col * 0.55);
                return fixed4(col * 1.12, i.color.a);
            }
            ENDCG
        }
    }
}
