Shader "Hidden/Paint"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
        }
        
        Cull Off ZWrite Off ZTest Always
        BlendOp [_BlendOp]
        Blend One One

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
                float3 positionWS : TECOORD0;
                float2 uv : TEXCOORD1;
            };

            float4 _MouseInfo;
            float4 _BrushColor;
            float _BrushOpacity;
            float _BrushHardness;
            float _BrushSize;
              int _BrushType;

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // https://iquilezles.org/articles/distfunctions2d/
            float sphere_brush(float3 position, float3 center, float radius, float hardness)
            {
                return 1 - saturate((distance(position, center) - radius) / (1 - hardness));
            }
            
            float square_brush(float3 position, float3 center, float size) {
                // Calculate the difference between the position and center vectors in the x and y dimensions separately
                float2 d = abs(position.xy - center.xy);

                // Subtract half of the size of the square-shaped brush from this vector to create a square that is centered at center
                // with sides of length size
                d -= float2(size, size) * 0.5f;

                // Create a square-shaped Signed Distance Field (SDF) by taking the maximum distance to the sides of the square
                float square_sdf = max(d.x, d.y);

                // Use saturate to clamp the brush intensity to a range of 0 to 1, with 1 representing the interior of the square and
                // 0 representing the exterior
                return step(0.1, saturate(1.0 - square_sdf / size));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float2 uv = IN.uv.xy;
                uv.y = 1.0 - uv.y;
                uv = uv * 2.0 - 1.0;

                OUT.positionHCS = float4(uv.xy, 0.0, 1.0);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = IN.uv;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float brush_mask;
                 if (_BrushType == 0) {
                    brush_mask = sphere_brush(IN.positionWS, _MouseInfo.xyz, _BrushSize, 1.0);
                } else {
                    brush_mask = square_brush(IN.positionWS, _MouseInfo.xyz, _BrushSize);
                }
                return _BrushColor * brush_mask;
            }
            ENDHLSL
        }
    }
}