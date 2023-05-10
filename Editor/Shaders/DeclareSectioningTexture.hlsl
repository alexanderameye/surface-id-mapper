#ifndef TBI_DECLARE_SECTIONING_TEXTURE_INCLUDED
#define TBI_DECLARE_SECTIONING_TEXTURE_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D_X_FLOAT(_CameraSectioningTexture);
SAMPLER(sampler_CameraSectioningTexture);

uniform float4 _CameraSectioningTexture_TexelSize;
float4 _CameraSectioningTexture_ST;

float4 SampleSceneSectioning(float2 uv)
{
    return SAMPLE_TEXTURE2D_X(_CameraSectioningTexture, sampler_CameraSectioningTexture, UnityStereoTransformScreenSpaceTex(uv));
}
#endif