Shader "Debug/Section Marker"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"="Opaque"
        }

        ZWrite Off
        Cull Off

        Pass
        {
            Name "Debug SectionPainer"

            HLSLPROGRAM
            #pragma vertex Vert // vertex function is provided by Blit.hlsl
            #pragma fragment frag

            // INCLUDES
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "CommonDebugViews.hlsl"
            #include "ColorUtilities.hlsl"
            #include "DeclareSectioningTexture.hlsl"

            // FRAGMENT SHADER
            float4 frag(Varyings IN) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                float2 uv = UnityStereoTransformScreenSpaceTex(IN.texcoord);

                // sample source color
                half4 color = half4(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).xyz, 1);
                        
                #if defined(DEBUG_VIEW)
                    // sample sectioning texture
                    float4 sectioning = SampleSceneSectioning(uv) * float4(R_CHANNEL, G_CHANNEL, B_CHANNEL, 1.0);
                    //return sectioning;
                    half3 rgb_color = HSVToRGB(half3((sectioning.r + sectioning.g + sectioning.b) / 3.0 * 360.0, 1.0, 1.0));
                    return float4(rgb_color.x, rgb_color.y, rgb_color.z, 1.0);
                    //return float4(sectioning.r, 0.0, 0.0, 1.0);
                #endif

                return color;
            }
            ENDHLSL
        }
    }
}