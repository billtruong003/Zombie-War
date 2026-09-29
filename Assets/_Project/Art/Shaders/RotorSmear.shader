// Rotor smear for a flat disc (a quad or a 12-triangle fan, UV 0..1 across the disc).
// The ink strokes that run around the centre are baked into T_HC_RotorSmear (R = ink, G = disc,
// B = hub; made from noise offline), so the fragment does ONE texture sample and a few lerps.
// The spin is a UV rotation in the vertex shader: smooth, or frame by frame like a hand-drawn smear.
Shader "HordeCall/RotorSmear"
{
    Properties
    {
        [NoScaleOffset] _MaskMap ("Masks (R ink, G disc, B hub)", 2D) = "black" {}
        [HDR] _InkColor ("Ink", Color) = (1.47, 1.47, 1.47, 1)
        _DiscColor ("Disc (blur)", Color) = (0.59, 0.59, 0.59, 0.34)
        _HubColor ("Hub", Color) = (0.22, 0.24, 0.3, 1)
        _Speed ("Turns per second (smooth)", Float) = 3
        _StepFps ("Frames per second (0 = smooth)", Float) = 0
        _StepDeg ("Degrees per frame", Float) = 67
        _PreviewTime ("Preview time (-1 = live)", Float) = -1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _InkColor, _DiscColor, _HubColor;
                float _Speed, _StepFps, _StepDeg, _PreviewTime;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                float t = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                float turns = _StepFps > 0 ? floor(t * _StepFps) * (_StepDeg / 360.0) : t * _Speed;
                float s, c; sincos(turns * 6.2831853, s, c);
                float2 p = v.uv - 0.5;
                o.uv = float2(c * p.x - s * p.y, s * p.x + c * p.y) + 0.5;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half3 m = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv).rgb;
                // ink "over" the soft disc, then the solid hub on top
                half discA = _DiscColor.a * m.g;
                half inkA = m.r * _InkColor.a;
                half outA = inkA + discA * (1 - inkA);
                half3 rgb = (_InkColor.rgb * inkA + _DiscColor.rgb * discA * (1 - inkA)) / max(outA, 1e-3);
                rgb = lerp(rgb, _HubColor.rgb, m.b);
                return half4(rgb, lerp(outA, _HubColor.a, m.b));
            }
            ENDHLSL
        }
    }
}
