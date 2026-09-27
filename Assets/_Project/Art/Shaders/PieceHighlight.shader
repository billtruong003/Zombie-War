// PieceHighlight.shader — outline for the costume piece that just changed in the Studio (owner
// 2026-09-27: "outline the item that was swapped"). Inverted hull: back faces pushed out along the
// view-space normal by a constant screen width, drawn as an extra material on the piece's renderer.
// StudioScreen pulses _Color alpha and removes the material again.
Shader "ZombieWar/PieceHighlight"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.79, 0.24, 1)
        _Width ("Width", Range(0, 0.05)) = 0.02
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Width;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                float2 off = normalize(TransformViewToProjection(n.xy) + 1e-5);
                // Constant on screen: scale by w, and by aspect so it is round, not oval.
                off.x *= _ScreenParams.y / _ScreenParams.x;
                o.pos.xy += off * _Width * o.pos.w;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target { return _Color; }
            ENDCG
        }
    }
}
