Shader "Section Painter/Combine Section Textures"
{
    Properties
    {
        _MainTex ("source", 2D) = "black" {}
        
        _TexR ("_TexR", 2D) = "black"
        _TexG ("_TexG", 2D) = "black"
    }

    SubShader
    {
        Cull Off
		ZWrite Off
		ZTest Always
        
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

           
            TEXTURE2D(_TexR);
            SAMPLER(sampler_TexR);

            TEXTURE2D(_TexG);
            SAMPLER(sampler_TexG);
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 R = SAMPLE_TEXTURE2D(_TexR, sampler_TexR, IN.uv);
                half4 G = SAMPLE_TEXTURE2D(_TexG, sampler_TexG, IN.uv);
                return float4(R.x, G.y, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}