#ifndef ZW_CHARACTER_LIGHTING_INCLUDED
#define ZW_CHARACTER_LIGHTING_INCLUDED

// Lõi ánh sáng nhân vật — dùng chung cho ToonPrototype và MetalPrototype.
//
// Khác biệt cốt lõi so với shader toon của vendor: KHÔNG cộng SH/GI, KHÔNG chạy vòng lặp
// additional light, KHÔNG specular rộng. Ba thứ đó cộng lại chính là cái làm nhân vật hiện tại bị
// "bợt và nhựa": tóc trắng mất hẳn khối vì SH nâng đều mọi hướng pháp tuyến, còn dải sáng/tối thì
// bị làm mềm tới mức không còn đọc ra mặt phẳng nào.
//
// Ở đây ánh sáng được dựng theo hướng ĐỒ HOẠ: một ranh giới sáng/tối do hoạ sĩ đặt, một nền ambient
// có trần, và bóng đổ mang MÀU chứ không phải màu đen vật lý.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Hướng sáng giả toàn cục do ToonLightRig đẩy xuống. alpha của màu = cờ "rig đang bật".
float4 _ToonLightDirection;
float4 _ToonLightColor;

struct ZWLight
{
    half3 direction;   // hướng TỚI nguồn sáng
    half3 color;
};

/// <summary>
/// Nguồn sáng của nhân vật: ưu tiên rig giả, không có thì lùi về main light thật của URP.
///
/// Bắt buộc phải có cả hai đường: gameplay chạy bằng ToonLightRig, còn sân khấu nhân vật ở Hub dùng
/// một Directional Light riêng. Cùng một nhân vật phải đọc ra giống nhau ở cả hai chỗ.
/// </summary>
ZWLight ZW_GetCharacterLight(float3 positionWS)
{
    ZWLight l;

    // alpha > 0 nghĩa là rig đã push màu; vector 0 nghĩa là rig tắt.
    bool rigValid = _ToonLightColor.a > 0.001 && dot(_ToonLightDirection.xyz, _ToonLightDirection.xyz) > 0.0001;

    if (rigValid)
    {
        l.direction = normalize(_ToonLightDirection.xyz);
        l.color = _ToonLightColor.rgb;
    }
    else
    {
        Light mainLight = GetMainLight();
        l.direction = mainLight.direction;
        l.color = mainLight.color;
    }

    return l;
}

/// <summary>
/// Dải sáng/tối toon: MỘT ranh giới, độ mềm do hoạ sĩ đặt.
///
/// Dùng smoothstep quanh ngưỡng thay vì nhân trực tiếp N·L. Nhân trực tiếp cho một dốc liên tục —
/// đó chính là cái gradient phun mềm đang làm tóc trắng phẳng lì. Một ranh giới nghĩa là bề mặt
/// trắng vẫn còn hai mảng giá trị khác nhau để mắt đọc ra khối.
/// </summary>
half ZW_ToonBand(half3 normalWS, half3 lightDir, half threshold, half softness)
{
    half ndotl = dot(normalize(normalWS), lightDir) * 0.5h + 0.5h;   // [0,1]
    half hw = max(softness, 0.0005h) * 0.5h;
    return smoothstep(threshold - hw, threshold + hw, ndotl);
}

/// <summary>
/// Viền theo Fresnel, CHẶN bởi phía đang hướng sáng.
///
/// Nhân với dải sáng là điểm mấu chốt: viền không được chạy vòng quanh toàn bộ hình như một quầng
/// neon, và không được xuyên qua vùng đang trong bóng. Nó chỉ đỡ cho phần silhouette đang ăn sáng.
/// </summary>
half ZW_LightFacingRim(half3 normalWS, half3 viewDirWS, half lightBand, half power, half cutoff, half strength)
{
    half fresnel = 1.0h - saturate(dot(normalize(normalWS), normalize(viewDirWS)));
    half rim = pow(abs(fresnel) + 1e-4h, max(power, 0.01h));
    rim = smoothstep(cutoff, min(cutoff + 0.12h, 0.999h), rim);
    return rim * lightBand * strength;
}

/// <summary>
/// Ambient CÓ TRẦN, do hoạ sĩ đặt màu — không phải SampleSH.
///
/// SampleSH nâng sáng theo pháp tuyến, nên mọi mặt trắng đều bị kéo về cùng một giá trị và khối biến
/// mất. Ở đây ambient là một hằng số nhân vào albedo, nên nó nâng nền mà KHÔNG xoá ranh giới.
/// </summary>
half3 ZW_BoundedAmbient(half3 albedo, half3 ambientColor, half ambientStrength)
{
    return albedo * ambientColor * ambientStrength;
}

/// <summary>Dải specular hẹp theo N·H — chỉ dùng cho vai trò vật liệu cần nó.</summary>
half ZW_TightSpecular(half3 normalWS, half3 lightDir, half3 viewDirWS, half cutoff, half strength)
{
    half3 h = normalize(lightDir + normalize(viewDirWS));
    half ndoth = saturate(dot(normalize(normalWS), h));
    half spec = smoothstep(cutoff, min(cutoff + 0.06h, 0.999h), ndoth);
    return spec * strength;
}

#endif // ZW_CHARACTER_LIGHTING_INCLUDED
