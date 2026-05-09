Shader "ShaderTester/Post Dominant Pixelate"
{
    Properties
    {
        _BlockSize ("Block Size", Range(1, 16)) = 6
        _ColorSteps ("Color Set", Range(2, 32)) = 6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off

        Pass
        {
            Name "PostDominantPixelate"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BlockSize;
            float _ColorSteps;

            // snaps a color to the configured color set
            half3 QuantizeColor(half3 color)
            {
                float steps = max(_ColorSteps - 1, 1);
                return floor(color * steps + 0.5) / steps;
            }

            // returns the most common quantized color inside the current pixel block
            half4 Frag(Varyings input) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                const int max_block_size = 16;
                int block_size = clamp((int)round(_BlockSize), 1, max_block_size);
                float2 texture_size = _BlitTexture_TexelSize.zw;
                float2 pixel = floor(input.texcoord.xy * texture_size);
                float2 block_origin = floor(pixel / block_size) * block_size;
                int middle = block_size / 2;
                float2 middle_pixel = block_origin + float2(middle + 0.5, middle + 0.5);
                half4 middle_color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(middle_pixel / texture_size), _BlitMipLevel);
                half3 best_color = QuantizeColor(middle_color.rgb);
                float best_count = 0;

                for (int candidate_y = 0; candidate_y < max_block_size; candidate_y++)
                {
                    for (int candidate_x = 0; candidate_x < max_block_size; candidate_x++)
                    {
                        if (candidate_x >= block_size || candidate_y >= block_size)
                        {
                            continue;
                        }

                        float2 candidate_pixel = block_origin + float2(candidate_x + 0.5, candidate_y + 0.5);
                        half3 candidate_color = QuantizeColor(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(candidate_pixel / texture_size), _BlitMipLevel).rgb);
                        float count = 0;

                        for (int y = 0; y < max_block_size; y++)
                        {
                            for (int x = 0; x < max_block_size; x++)
                            {
                                if (x >= block_size || y >= block_size)
                                {
                                    continue;
                                }

                                float2 sample_pixel = block_origin + float2(x + 0.5, y + 0.5);
                                half3 sample_color = QuantizeColor(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(sample_pixel / texture_size), _BlitMipLevel).rgb);
                                count += all(sample_color == candidate_color) ? 1 : 0;
                            }
                        }

                        if (count > best_count)
                        {
                            best_count = count;
                            best_color = candidate_color;
                        }
                    }
                }

                return half4(best_color, middle_color.a);
            }
            ENDHLSL
        }
    }
}
