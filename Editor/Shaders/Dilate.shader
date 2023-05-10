Shader "Hidden/Dilate"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);


            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            CBUFFER_END

            static const int MAX_STEPS = 8;
            static const int TEXEL_DIST = 1;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 offsets[8] = {
                    float2(-TEXEL_DIST, 0), float2(TEXEL_DIST, 0), float2(0, TEXEL_DIST), float2(0, -TEXEL_DIST), float2(-TEXEL_DIST, TEXEL_DIST),
                    float2(TEXEL_DIST, TEXEL_DIST), float2(TEXEL_DIST, -TEXEL_DIST), float2(-TEXEL_DIST, -TEXEL_DIST)
                };
                float4 sample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                float4 sampleMax = sample;
                for (int i = 0; i < MAX_STEPS; i++) {
                    float2 uv = IN.uv + offsets[i] * _MainTex_TexelSize.xy;
                    float4 offset_sample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                    sampleMax = max(offset_sample, sampleMax);
                }
                sample = sampleMax;

                return sample;
            }
            ENDHLSL
        }
    }
}