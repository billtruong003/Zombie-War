Shader "BillTheDev/VAT/Ghost_VAT"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _GhostColor ("Ghost Color", Color) = (1, 1, 1, 0.5) // Màu và độ trong suốt của "con ma"
        _FresnelColor ("Fresnel Color", Color) = (1, 1, 1, 1) // Màu của viền sáng
        _FresnelPower ("Fresnel Power", Range(0.1, 10.0)) = 2.0 // Độ dày và sắc nét của viền sáng

        // --- Thuộc tính VAT giữ nguyên ---
        _PositionTexture ("Position Texture (VAT)", 2D) = "white" {}
        _PositionMin ("Position Min (Local Space)", Vector) = (0,0,0,0)
        _PositionMax ("Position Max (Local Space)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        // --- Tags cần thiết cho hiệu ứng trong suốt ---
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            // --- Bật chế độ blend cho sự trong suốt ---
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off // Tắt ghi vào Z-buffer để các vật thể trong suốt khác có thể render phía sau

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
                float3 normal       : NORMAL; // Cần normal để tính Fresnel
                float2 vertexIdUV   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VertexToFragment
            {
                float2 uv           : TEXCOORD0;
                float4 vertex       : SV_POSITION;
                float3 worldPos     : TEXCOORD1; // Vị trí world space
                float3 worldNormal  : TEXCOORD2; // Normal world space
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // --- Khai báo các thuộc tính mới ---
            sampler2D _MainTex;
            fixed4 _GhostColor;
            fixed4 _FresnelColor;
            float _FresnelPower;

            // --- Biến và hàm VAT giữ nguyên ---
            sampler2D _PositionTexture;
            float4 _PositionMin;
            float4 _PositionMax;
            
            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _CurrentAnimNormalizedTime)
                UNITY_DEFINE_INSTANCED_PROP(float, _PreviousAnimNormalizedTime)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimationBlendWeight)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            float3 DecodeLocalPosition(float vertexU, float timeV)
            {
                float4 encodedPosition = tex2Dlod(_PositionTexture, float4(vertexU, timeV, 0, 0));
                return lerp(_PositionMin.xyz, _PositionMax.xyz, encodedPosition.xyz);
            }

            // --- VERTEX SHADER (Giữ nguyên logic animation, chỉ thêm tính toán cho Fresnel) ---
            VertexToFragment vert (AppData v)
            {
                VertexToFragment o;
                UNITY_SETUP_INSTANCE_ID(v);
                
                float vertexU = v.vertexIdUV.x;
                
                float currentAnimTime = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _CurrentAnimNormalizedTime);
                float blendWeight = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _AnimationBlendWeight);

                float3 localPosition = DecodeLocalPosition(vertexU, currentAnimTime);

                if (blendWeight > 0.001)
                {
                    float previousAnimTime = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _PreviousAnimNormalizedTime);
                    float3 previousLocalPosition = DecodeLocalPosition(vertexU, previousAnimTime);
                    localPosition = lerp(previousLocalPosition, localPosition, blendWeight);
                }
                
                // Tính toán các giá trị cần cho fragment shader
                float4 worldPosition = mul(unity_ObjectToWorld, float4(localPosition, 1.0));
                o.vertex = mul(UNITY_MATRIX_VP, worldPosition);
                o.uv = v.uv;
                o.worldPos = worldPosition.xyz;
                // Chuyển normal từ object space sang world space
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                
                return o;
            }
            
            // --- FRAGMENT SHADER (Áp dụng hiệu ứng ghost) ---
            fixed4 frag (VertexToFragment i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                // Lấy màu albedo từ texture
                fixed4 albedo = tex2D(_MainTex, i.uv);
                
                // --- Tính toán hiệu ứng Fresnel ---
                // Lấy hướng nhìn từ camera tới fragment trên world space
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos.xyz);
                // Tích vô hướng giữa hướng nhìn và normal
                float dotProduct = 1.0 - saturate(dot(viewDir, i.worldNormal));
                // Lũy thừa để kiểm soát độ sắc nét của viền
                float fresnel = pow(dotProduct, _FresnelPower);
                
                // Kết hợp màu albedo, màu ghost và màu fresnel
                fixed3 finalColor = albedo.rgb * _GhostColor.rgb;
                finalColor += _FresnelColor.rgb * fresnel;
                
                // Độ trong suốt cuối cùng là alpha của GhostColor
                float finalAlpha = _GhostColor.a;

                return fixed4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Transparent/VertexLit"
}