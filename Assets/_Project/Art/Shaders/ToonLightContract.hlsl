#ifndef ZOMBIEWAR_TOON_LIGHT_CONTRACT_INCLUDED
#define ZOMBIEWAR_TOON_LIGHT_CONTRACT_INCLUDED

// Hợp đồng ánh sáng toon dùng chung cho MỌI shader do project sở hữu.
//
// Vì sao tồn tại: dự án không chiếu sáng bằng directional light thật. `ToonLightRig` đẩy hướng và
// màu sáng qua `Shader.SetGlobalVector/Color`, còn directional light trong scene bị TẮT ở cả năm map.
// Shader nào gọi thẳng `GetMainLight()` sẽ nhận một nguồn sáng rỗng và chỉ còn sống nhờ ambient SH —
// đúng nguyên nhân khiến mặt đất thủ tục của Map_Level5 ra màu đen trong khi Map_Level4 vẫn nâu:
// hai scene có dữ liệu SH khác nhau, và không scene nào có ánh sáng thật để bù.
//
// Công thức được đặt ở MỘT chỗ chứ không chép vào từng shader. Ba shader thế giới thủ tục dùng chung
// hàm này, nên khi hợp đồng đổi thì chúng không thể trôi ra khỏi nhau — và cũng không thể lệch khỏi
// `VAT_EnemyToon`, shader tham chiếu đã định nghĩa thứ tự ưu tiên bên dưới.
//
// Thứ tự ưu tiên (giống hệt VAT_EnemyToon):
//   1. Global của rig  → map tự quyết ánh sáng, không cần directional thật.
//   2. URP main light  → scene cũ chưa có rig vẫn đúng.
//   3. Ambient authored→ KHÔNG BAO GIỜ đen. Đây là điều khoản quan trọng nhất của hợp đồng.

// Global do ToonLightRig push. Nằm ngoài CBUFFER vì là global, không phải per-material.
float4 _ToonLightDirection;
half4 _ToonLightColor;

// Màu dùng khi không có cả rig lẫn main light. Mỗi shader tự khai báo trong CBUFFER của nó.
// (khai báo ở shader, không ở đây, để giữ SRP Batcher hợp lệ)

/// Trả về hướng TỚI nguồn sáng và màu sáng.
/// Trả về false khi rơi xuống nhánh ambient — lúc đó không có hướng sáng thật, nên phía gọi nên
/// tắt banding: vẽ một dải tối giả trên một nguồn sáng không tồn tại chỉ tạo nhiễu.
bool ZW_ResolveToonLight(half3 ambientFallback, out float3 lightDir, out half3 lightColor)
{
    float3 rigDir = _ToonLightDirection.xyz;
    if (dot(rigDir, rigDir) > 0.0001)
    {
        lightDir = normalize(rigDir);
        // alpha là cờ "rig có màu": global mặc định (0,0,0,0) không được phép bôi đen cả scene.
        lightColor = _ToonLightColor.a > 0.5 ? _ToonLightColor.rgb : half3(1, 1, 1);
        return true;
    }

    float3 mainDir = _MainLightPosition.xyz;
    if (dot(mainDir, mainDir) > 0.0001 && dot(_MainLightColor.rgb, _MainLightColor.rgb) > 0.0001)
    {
        lightDir = normalize(mainDir);
        lightColor = _MainLightColor.rgb;
        return true;
    }

    lightDir = normalize(float3(0.35, 0.75, 0.35));
    lightColor = ambientFallback;
    return false;
}

#endif
