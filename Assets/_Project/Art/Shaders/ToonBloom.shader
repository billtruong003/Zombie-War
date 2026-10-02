// Cheap bloom for phones (2026-10-02), replacing URP's Bloom (its mip chain runs about twice the
// passes at higher resolutions). Dual filter after Marius Bjorge, "Bandwidth-Efficient Rendering",
// SIGGRAPH 2015: everything below quarter resolution, a 5-tap downsample and an 8-tap upsample with
// a constant offset give a wide, smooth glow for very little bandwidth.
//   0 Prefilter: full colour -> quarter size, 4 bilinear taps, soft threshold (knee)
//   1 Down:      dual filter downsample
//   2 Up:        dual filter upsample
//   3 Composite: bloom x intensity x tint, added onto the camera colour (Blend One One)
Shader "Hidden/HordeCall/ToonBloom"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _BloomParams;   // x threshold, y knee, z intensity, w unused
        float4 _BloomTint;

        half3 Sample(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb; }

        half4 FragPrefilter(Varyings i) : SV_Target
        {
            float2 t = _BlitTexture_TexelSize.xy;
            half3 c = (Sample(i.texcoord + t * float2(-1, -1)) + Sample(i.texcoord + t * float2(1, -1)) +
                       Sample(i.texcoord + t * float2(-1, 1)) + Sample(i.texcoord + t * float2(1, 1))) * 0.25h;
            half bright = max(c.r, max(c.g, c.b));
            half knee = max(_BloomParams.y, 1e-4h);
            half soft = clamp(bright - _BloomParams.x + knee, 0.0h, 2.0h * knee);
            soft = soft * soft / (4.0h * knee + 1e-4h);
            half contrib = max(soft, bright - _BloomParams.x) / max(bright, 1e-4h);
            return half4(c * contrib, 1.0h);
        }

        half4 FragDown(Varyings i) : SV_Target
        {
            float2 h = _BlitTexture_TexelSize.xy * 0.5;
            half3 c = Sample(i.texcoord) * 4.0h;
            c += Sample(i.texcoord - h);
            c += Sample(i.texcoord + h);
            c += Sample(i.texcoord + float2(h.x, -h.y));
            c += Sample(i.texcoord - float2(h.x, -h.y));
            return half4(c * 0.125h, 1.0h);
        }

        half4 FragUp(Varyings i) : SV_Target
        {
            float2 h = _BlitTexture_TexelSize.xy * 0.5;
            half3 c = Sample(i.texcoord + float2(-h.x * 2.0, 0.0));
            c += Sample(i.texcoord + float2(-h.x, h.y)) * 2.0h;
            c += Sample(i.texcoord + float2(0.0, h.y * 2.0));
            c += Sample(i.texcoord + float2(h.x, h.y)) * 2.0h;
            c += Sample(i.texcoord + float2(h.x * 2.0, 0.0));
            c += Sample(i.texcoord + float2(h.x, -h.y)) * 2.0h;
            c += Sample(i.texcoord + float2(0.0, -h.y * 2.0));
            c += Sample(i.texcoord + float2(-h.x, -h.y)) * 2.0h;
            return half4(c / 12.0h, 1.0h);
        }

        half4 FragComposite(Varyings i) : SV_Target
        {
            return half4(Sample(i.texcoord) * _BloomParams.z * _BloomTint.rgb, 0.0h);
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass { Name "Prefilter" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPrefilter
        ENDHLSL }
        Pass { Name "Down" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDown
        ENDHLSL }
        Pass { Name "Up" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUp
        ENDHLSL }
        Pass { Name "Composite" Blend One One HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
        ENDHLSL }
    }
}
