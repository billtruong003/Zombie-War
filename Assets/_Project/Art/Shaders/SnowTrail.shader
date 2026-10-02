// Snow trails (2026-10-02, after MinionsArt's interactive snow, without the mesh displacement): a
// small render texture wrapping over the world holds how deep the snow is pressed. SnowTrails.cs
// fades it a little every frame (pass 0) and presses a soft disc under every walker (pass 1). The
// tundra ground reads it as relief (EnvGroundCore, GROUND_SNOW).
Shader "Hidden/HordeCall/SnowTrail"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        float _Decay;   // what is left of a trail after this frame
        struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
        // The quad is placed straight in clip space (the matrices are identity).
        V Vert(A i)
        {
            UNITY_SETUP_INSTANCE_ID(i);
            V o;
            o.pos = float4(mul(UNITY_MATRIX_M, float4(i.pos.xyz, 1)).xy, 0.5, 1);
            o.uv = i.uv;
            return o;
        }
        ENDHLSL

        // 0: fade (multiplies what is there).
        Pass
        {
            Blend Zero SrcColor
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(V i) : SV_Target { return half4(_Decay, _Decay, _Decay, _Decay); }
            ENDHLSL
        }

        // 1: press (keeps the deeper of the two).
        Pass
        {
            Blend One One
            BlendOp Max
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            half4 Frag(V i) : SV_Target
            {
                float d = length(i.uv * 2.0 - 1.0);
                half press = saturate(1.0 - smoothstep(0.15, 1.0, d));   // soft: a hard rim aliases into steps
                return half4(press, press, press, press);
            }
            ENDHLSL
        }
    }
}
