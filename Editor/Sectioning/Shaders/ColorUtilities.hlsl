#ifndef TINTIN_COLOR_UTILITIES_INCLUDED
#define TINTIN_COLOR_UTILITIES_INCLUDED

void RGBtoCMYK_float(float3 rgb, out float4 cmyk)
{
    // https://www.eembc.org/techlit/datasheets/cmyk_consumer.pdf
    //#if defined(SHADERGRAPH_PREVIEW)
    //cmyk = float4(0.0, 0.0, 0.0, 0.0);
    //#else
    float k = 1.0 - max(rgb.r, max(rgb.g, rgb.b));
    float c = (1.0 - rgb.r - k) / (1.0 - k);
    float m = (1.0 - rgb.g - k) / (1.0 - k);
    float y = (1.0 - rgb.b - k) / (1.0 - k);
    cmyk = float4(c, m, y, k);

    /*float c = 1.0 - rgb.r;
    float m = 1.0 - rgb.g;
    float y = 1.0 - rgb.b;
    float k = min(c, min(m, y));
    cmyk = float4(c - k, m - k, y - k, k);*/
    //#endif
}

float4 RGBtoCMYK(float3 rgb)
{
    float c = 1.0 - rgb.r;
    float m = 1.0 - rgb.g;
    float y = 1.0 - rgb.b;
    float k = min(c, min(m, y));
    return float4(c - k, m - k, y - k, k);
}

half3 RGBToHSV(half3 In)
{
    half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    half4 P = lerp(half4(In.bg, K.wz), half4(In.gb, K.xy), step(In.b, In.g));
    half4 Q = lerp(half4(P.xyw, In.r), half4(In.r, P.yzx), step(P.x, In.r));
    half D = Q.x - min(Q.w, Q.y);
    half E = 1e-10;
    return half3(abs(Q.z + (Q.w - Q.y) / (6.0 * D + E)), D / (Q.x + E), Q.x);
}

half3 HSVToRGB(half3 In)
{
    half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    half3 P = abs(frac(In.xxx + K.xyz) * 6.0 - K.www);
    return In.z * lerp(K.xxx, saturate(P - K.xxx), In.y);
}

#endif