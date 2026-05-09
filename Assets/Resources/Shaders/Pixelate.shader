Shader "ShaderTester/Pixelate"
{
    Properties
    {
        _BlockSize ("Block Size", Range(1, 32)) = 6
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
            Name "PostPixelate"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BlockSize;

            // returns the average color inside the current pixel block
            half4 Frag(Varyings input) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                const int max_block_size = 32;
                int block_size = clamp((int)round(_BlockSize), 1, max_block_size);
                float2 texture_size = _BlitTexture_TexelSize.zw;
                float2 pixel = floor(input.texcoord.xy * texture_size);
                float2 block_origin = floor(pixel / block_size) * block_size;
                half4 color = 0;
                float sample_count = 0;

                for (int y = 0; y < max_block_size; y++)
                {
                    for (int x = 0; x < max_block_size; x++)
                    {
                        if (x >= block_size || y >= block_size)
                        {
                            continue;
                        }

                        float2 sample_pixel = block_origin + float2(x + 0.5, y + 0.5);
                        float2 sample_uv = saturate(sample_pixel / texture_size);
                        color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, sample_uv, _BlitMipLevel);
                        sample_count += 1;
                    }
                }

                return color / max(sample_count, 1);
            }
            ENDHLSL
        }
    }
}
