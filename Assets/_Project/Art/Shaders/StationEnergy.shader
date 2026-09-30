Shader "HordeCall/Station/Energy"
{
    // Phase A9b (owner 2026-09-30): the glowing lines of a station, drawn as flat ribbons whose
    // UV.x runs along the line (0 at its start, 1 at its end).
    //  - An energy pulse travels along every line: a bright band repeating _PulseRepeat times,
    //    moving at _PulseSpeed (the station speeds it up while the player stands in it).
    //  - Progress fills the lines: the part of each line below _Fill burns at full strength, the
    //    rest idles at _IdleLevel. The rim loop fills round, the spokes fill outward.
    //  - _Dissolve eats the lines away with the same noise as the body when the station leaves.
    // One texture (the packed FX noise, G channel), additive, no depth texture: mobile-safe.
    Properties
    {
        _NoiseTex ("Packed FX (noise in G)", 2D) = "gray" {}
        [HDR] _Tint ("Colour (set per station)", Color) = (0.3, 0.8, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 3.2
        _Fill ("Progress fill 0..1", Range(0, 1)) = 0
        _IdleLevel ("Unfilled brightness", Range(0, 1)) = 0.55
        _PulseSpeed ("Pulse speed", Float) = 0.6
        _PulseRepeat ("Pulses per line", Float) = 2
        _PulseWidth ("Pulse width", Range(0.02, 0.5)) = 0.18
        _Dissolve ("Dissolve 0..1", Range(0, 1)) = 0
        _NoiseScale ("Dissolve noise tiling (per metre)", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                float4 _Tint;
                float _Intensity, _Fill, _IdleLevel, _PulseSpeed, _PulseRepeat, _PulseWidth, _Dissolve, _NoiseScale;
            CBUFFER_END

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 wxz : TEXCOORD1; };

            V vert (A i)
            {
                V o;
                float3 w = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(w);
                o.uv = i.uv;
                o.wxz = w.xz;
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                float u = i.uv.x;
                // Soft across the ribbon, hard along it.
                float across = 1.0 - abs(i.uv.y * 2.0 - 1.0);
                float body = saturate(across * 1.6);

                float aa = max(fwidth(u), 1e-4);
                float filled = saturate((_Fill - u) / aa + 0.5);
                float level = lerp(_IdleLevel, 1.0, filled);

                float p = frac(u * _PulseRepeat - _Time.y * _PulseSpeed);
                float pulse = smoothstep(1.0 - _PulseWidth, 1.0, p) * (1.0 - smoothstep(0.98, 1.0, p));

                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.wxz * _NoiseScale).g;
                float alive = saturate((noise - _Dissolve * 1.05) * 12.0);

                half3 rgb = _Tint.rgb * _Intensity * body * (level + pulse * 1.4) * alive;
                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }
}
