Shader "ShaderTester/ColorQuantize"
{
    Properties
    {
        _ColorSteps ("Color Steps", Range(1, 32)) = 4
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
            Name "PostColorQuantize"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ColorSteps;

            // reduces camera color precision into visible bands
            half4 Frag(Varyings input) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord.xy, _BlitMipLevel);
                float steps = max(_ColorSteps - 1, 1);
                color.rgb = floor(color.rgb * steps + 0.5) / steps;
                return color;
            }
            ENDHLSL
        }
    }
}
