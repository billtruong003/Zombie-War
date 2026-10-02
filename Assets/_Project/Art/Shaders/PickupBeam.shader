// Fake light ray over a dropped item (owner 02/10: items read from afar without a real light).
// One quad turned round the vertical axis to face the camera, added on top: bright at the foot,
// fading up, soft at the sides, thin streaks drifting up and a slow pulse. Colour per item
// (Pickup.cs, a property block); the quad's scale is the beam's width and height.
Shader "HordeCall/Fx/Pickup Beam"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1, 0.85, 0.4, 1)
        _Intensity ("Intensity", Range(0, 4)) = 2.2
        _Pulse ("Pulse", Range(0, 1)) = 0.3
        _StreakSpeed ("Streak speed", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Intensity, _Pulse, _StreakSpeed;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BeamColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V Vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o; UNITY_TRANSFER_INSTANCE_ID(i, o);
                // The quad's x runs across the beam, y up: turn x to face the camera round world Y.
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                float3 scale = float3(length(GetObjectToWorldMatrix()._m00_m10_m20), length(GetObjectToWorldMatrix()._m01_m11_m21), 1);
                float3 toCam = _WorldSpaceCameraPos - origin; toCam.y = 0;
                float3 side = normalize(cross(float3(0, 1, 0), toCam + float3(1e-4, 0, 0)));
                float3 w = origin + side * i.pos.x * scale.x + float3(0, 1, 0) * i.pos.y * scale.y;
                o.pos = TransformWorldToHClip(w);
                o.uv = i.uv;
                o.seed = frac(origin.x * 0.731 + origin.z * 0.317);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _BeamColor);
                half3 col = tint.a > 0 ? tint.rgb : _Color.rgb;
                float across = 1.0 - abs(i.uv.x * 2.0 - 1.0);
                float side = across * across;
                float up = pow(saturate(1.0 - i.uv.y), 1.6);
                // Streaks: a few thin vertical bands drifting up.
                float bands = sin(i.uv.x * 23.0 + i.seed * 6.0) * 0.5 + 0.5;
                float drift = frac(i.uv.y * 2.5 - _Time.y * _StreakSpeed + i.seed);
                float streak = bands * smoothstep(0.0, 0.3, drift) * (1.0 - smoothstep(0.3, 1.0, drift));
                float pulse = 1.0 + _Pulse * sin(_Time.y * 3.0 + i.seed * 6.2831);
                float a = side * up * (0.65 + streak * 0.6) * pulse * _Intensity;
                return half4(col * a, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
