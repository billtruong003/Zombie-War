// Bóng tiếp đất cho nhân vật — MỘT pass, MỘT material, MỘT renderer cho cả người chơi lẫn toàn bộ quái.
//
// Không phải bóng thật: đây là vệt tiếp đất tạo cảm giác nhân vật đứng trên mặt đất, đọc được từ
// camera nhìn xuống. Toàn bộ hình dạng nằm trong vertex + UV của mesh gộp, độ đậm nằm ở vertex color,
// nên KHÔNG có property nào theo từng nhân vật và KHÔNG cần một material riêng cho ai cả.
//
// Cố ý CHỈ có một pass. Không ShadowCaster, không DepthOnly, không DepthNormals, không
// OutlineSelectionMask: bóng tiếp đất không được đổ bóng, không được ghi depth prepass và tuyệt đối
// không được viền — một đường viền quanh vệt bóng sẽ biến nó thành miếng dán đen.
Shader "ZombieWar/Environment/CharacterContactShadow"
{
    Properties
    {
        _Tint ("Tint", Color) = (0, 0, 0, 1)
        _FalloffPower ("Falloff Power", Range(0.25, 6)) = 1.6
        _CoreStrength ("Core Strength", Range(0.5, 3)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent-100"   // sau mặt đất, TRƯỚC hiệu ứng trong suốt khác
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ContactShadow"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1          // nhích về phía camera để không z-fight với mặt đất

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0     // giữ ở mức WebGL 2 chạy được

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half  _FalloffPower;
                half  _CoreStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Hình ellipse tính THẲNG từ UV, không sample texture nào cả.
                //
                // Bản đầu lấy độ mờ từ một Texture2D sinh lúc chạy. Kết quả trên màn hình là những
                // MẢNG CHỮ NHẬT cứng: khi texture không tới được material, sampler trả về "white",
                // falloff thành 1 ở mọi pixel, và cả quad bị tô đặc tới tận góc. Một đường dẫn có thể
                // hỏng thầm lặng như vậy không đáng giữ, vì hình dạng ở đây chỉ là một hàm bán kính.
                //
                // Quad đã bị kéo theo chiều rộng/dài trong world, nên hình tròn trong không gian UV
                // tự trở thành ellipse đúng tỉ lệ nhân vật.
                float2 p = IN.uv * 2.0 - 1.0;          // [-1,1] quanh tâm quad
                float radiusSq = dot(p, p);            // 1.0 đúng tại mép ellipse nội tiếp

                // saturate() cắt sạch bốn góc: mọi pixel ngoài ellipse có alpha đúng bằng 0, nên
                // không bao giờ nhìn thấy cạnh của quad.
                half falloff = pow(saturate(1.0 - radiusSq), _FalloffPower);

                half alpha = saturate(falloff * _CoreStrength) * IN.color.a * _Tint.a;
                return half4(_Tint.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
