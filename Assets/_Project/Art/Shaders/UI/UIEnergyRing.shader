// UIEnergyRing.shader — a thin energy ring for FTUE highlights, the joystick hint and the station
// reticle: dashed segments that crawl around, a bright comet riding them, a soft halo.
// Cost: 1 texture sample (noise, for the energy shimmer along the ring), one atan2, one length,
// a few frac/smoothstep. Drawn on a plain Image (no sprite), so the quad's uv is the ring space.
// Keep the Image small: atan2 is the only non-trivial op and the cost scales with its pixels.
Shader "ZombieWar/UI/EnergyRing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (unused)", 2D) = "white" {}
        _Color ("Colour", Color) = (1,0.82,0.24,1)
        [NoScaleOffset] _NoiseTex ("Noise (R)", 2D) = "gray" {}
        _Radius ("Radius", Range(0.1,0.95)) = 0.82
        _Thickness ("Thickness", Range(0.005,0.2)) = 0.05
        _Dashes ("Dashes", Float) = 18
        _DashFill ("Dash Fill", Range(0.1,1)) = 0.62
        _Spin ("Spin (turns/s)", Float) = 0.08
        _Comet ("Comet Strength", Range(0,2)) = 1
        _CometSpeed ("Comet Speed (turns/s)", Float) = 0.45
        _Halo ("Halo", Range(0,1)) = 0.35
        _Shimmer ("Shimmer", Range(0,1)) = 0.35

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
        Blend SrcAlpha One
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

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 p : TEXCOORD0; float4 world : TEXCOORD1; };

            sampler2D _NoiseTex;
            fixed4 _Color;
            half _Radius, _Thickness, _Dashes, _DashFill, _Spin, _Comet, _CometSpeed, _Halo, _Shimmer;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.p = v.uv * 2 - 1;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half r = length(i.p);
                half a = atan2(i.p.y, i.p.x) * 0.15915 + 0.5;          // 0..1 around the ring
                half dr = abs(r - _Radius);
                half aa = fwidth(r) * 1.5;
                half ring = 1 - smoothstep(_Thickness * 0.5 - aa, _Thickness * 0.5 + aa, dr);
                half halo = (1 - smoothstep(0, _Thickness * 4, dr)) * _Halo;

                half dash = step(frac(a * _Dashes - _Time.y * _Spin * _Dashes), _DashFill);
                half c = frac(a - _Time.y * _CometSpeed);
                c *= c; c *= c; c *= c;                                   // sharp head, long fading tail
                half n = tex2D(_NoiseTex, float2(a * 3, _Time.y * 0.3)).r;

                half e = ring * (dash * (1 - _Shimmer + _Shimmer * n * 2) + c * _Comet) + halo * (0.6 + c);
                fixed4 col = fixed4(i.color.rgb * (1 + c * _Comet), saturate(e) * i.color.a);

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
