#ifndef VAT_CORE_INCLUDED
#define VAT_CORE_INCLUDED

sampler2D _PositionTexture;
sampler2D _NormalTexture;
float4 _PositionMin;
float4 _PositionMax;

UNITY_INSTANCING_BUFFER_START(VAT_Props)
UNITY_DEFINE_INSTANCED_PROP(float, _CurrentAnimNormalizedTime)
UNITY_DEFINE_INSTANCED_PROP(float, _PreviousAnimNormalizedTime)
UNITY_DEFINE_INSTANCED_PROP(float, _AnimationBlendWeight)
UNITY_INSTANCING_BUFFER_END(VAT_Props)

float3 DecodePos(float u, float v) {
    float4 raw = tex2Dlod(_PositionTexture, float4(u, v, 0, 0));
    return lerp(_PositionMin.xyz, _PositionMax.xyz, raw.xyz);
}

float3 DecodeNorm(float u, float v) {
    float4 raw = tex2Dlod(_NormalTexture, float4(u, v, 0, 0));
    return normalize(raw.xyz * 2.0 - 1.0);
}

void ApplyVAT(float vertexU, inout float3 positionOS, inout float3 normalOS) {
    float curT = UNITY_ACCESS_INSTANCED_PROP(VAT_Props, _CurrentAnimNormalizedTime);
    float w = UNITY_ACCESS_INSTANCED_PROP(VAT_Props, _AnimationBlendWeight);

    float3 p = DecodePos(vertexU, curT);
    float3 n = DecodeNorm(vertexU, curT);

    if (w > 0.001) {
        float prevT = UNITY_ACCESS_INSTANCED_PROP(VAT_Props, _PreviousAnimNormalizedTime);
        p = lerp(DecodePos(vertexU, prevT), p, w);
        n = normalize(lerp(DecodeNorm(vertexU, prevT), n, w));
    }

    positionOS = p;
    normalOS = n;
}
#endif