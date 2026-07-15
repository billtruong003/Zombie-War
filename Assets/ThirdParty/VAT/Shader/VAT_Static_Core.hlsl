#ifndef VAT_STATIC_CORE_INCLUDED
#define VAT_STATIC_CORE_INCLUDED

sampler2D _PositionTexture;
sampler2D _NormalTexture;
float4 _PositionMin;
float4 _PositionMax;

float _AnimSpeed;
float _AnimStartV; 
float _AnimEndV;   

float3 DecodePos(float u, float v)
{
    float4 raw = tex2Dlod(_PositionTexture, float4(u, v, 0, 0));
    return lerp(_PositionMin.xyz, _PositionMax.xyz, raw.xyz);
}

float3 DecodeNorm(float u, float v)
{
    float4 raw = tex2Dlod(_NormalTexture, float4(u, v, 0, 0));
    return normalize(raw.xyz * 2.0 - 1.0);
}

void ApplyStaticVAT(float vertexU, inout float3 positionOS, inout float3 normalOS)
{
    float time = frac(_Time.y * _AnimSpeed);
    float v = lerp(_AnimStartV, _AnimEndV, time);
    
    positionOS = DecodePos(vertexU, v);
    normalOS = DecodeNorm(vertexU, v);
}
#endif