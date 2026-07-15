Shader "BillTheDev/VAT/Optimized_VAT"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _PositionTexture ("Position Texture (VAT)", 2D) = "white" {}
        _PositionMin ("Position Min (Local Space)", Vector) = (0,0,0,0)
        _PositionMax ("Position Max (Local Space)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Back // Tắt Cull để tránh lỗi render mặt sau, hữu ích cho các animation phức tạp

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "UnityCG.cginc"

            struct AppData
            {
                float4 vertex       : POSITION;
                float2 uv           : TEXCOORD0;
                float2 vertexIdUV   : TEXCOORD1; // UV1 chứa tọa độ U để xác định vertex
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VertexToFragment
            {
                float2 uv       : TEXCOORD0;
                float4 vertex   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _PositionTexture;
            float4 _PositionMin;
            float4 _PositionMax;
            
            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _CurrentAnimNormalizedTime)  // Tọa độ V cho frame hiện tại
                UNITY_DEFINE_INSTANCED_PROP(float, _PreviousAnimNormalizedTime) // Tọa độ V cho frame của clip trước đó (khi blending)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimationBlendWeight)     // Trọng số blend (0-1)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            // Giải mã vị trí từ texture, chuyển từ không gian [0,1] về không gian local của animation
            float3 DecodeLocalPosition(float vertexU, float timeV)
            {
                // tex2Dlod cho phép chỉ định Mip level, bắt buộc trong vertex shader
                float4 encodedPosition = tex2Dlod(_PositionTexture, float4(vertexU, timeV, 0, 0));
                // Dùng lerp để giải mã vị trí từ khoảng [0,1] về lại bounds gốc
                return lerp(_PositionMin.xyz, _PositionMax.xyz, encodedPosition.xyz);
            }

            VertexToFragment vert (AppData v)
            {
                VertexToFragment o;
                UNITY_SETUP_INSTANCE_ID(v);
                
                // Lấy tọa độ U đã được bake vào UV1 để xác định vertex này là pixel nào trên chiều ngang của texture
                float vertexU = v.vertexIdUV.x;
                
                float currentAnimTime = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _CurrentAnimNormalizedTime);
                float blendWeight = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _AnimationBlendWeight);

                // Lấy vị trí của animation hiện tại
                float3 localPosition = DecodeLocalPosition(vertexU, currentAnimTime);

                // Nếu đang trong quá trình blend (cross-fade)
                if (blendWeight > 0.001)
                {
                    float previousAnimTime = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _PreviousAnimNormalizedTime);
                    // Lấy vị trí của animation trước đó
                    float3 previousLocalPosition = DecodeLocalPosition(vertexU, previousAnimTime);
                    // Trộn tuyến tính giữa vị trí cũ và mới
                    localPosition = lerp(previousLocalPosition, localPosition, blendWeight);
                }
                
                // Chuyển từ local space sang clip space để render
                float4 worldPosition = mul(unity_ObjectToWorld, float4(localPosition, 1.0));
                o.vertex = mul(UNITY_MATRIX_VP, worldPosition);
                o.uv = v.uv;
                
                return o;
            }
            
            sampler2D _MainTex;

            fixed4 frag (VertexToFragment i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Mobile/VertexLit"
}