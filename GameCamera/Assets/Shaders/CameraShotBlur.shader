Shader "Hidden/GameCamera/CameraShotEdgeBlur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _ShotBlurParameters;

        half4 FilterEdges(Varyings input, float2 axis)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            half4 original = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
            float2 screenUV = (uv - _BlitScaleBias.zw) / _BlitScaleBias.xy;
            float2 distanceFromCenter = abs(screenUV * 2.0 - 1.0);
            float edgeMask = smoothstep(0.4, 1.0, max(distanceFromCenter.x, distanceFromCenter.y));
            float strength = edgeMask * _ShotBlurParameters.x;
            // The middle of the image keeps its original pixel values and avoids extra samples.
            UNITY_BRANCH
            if (strength <= 0.0)
                return original;

            float2 offset = _ShotBlurParameters.yz * axis;
            // Bilinear pairs implement a normalized nine-tap Gaussian with five fetches.
            half3 blurred = original.rgb * 0.2270270270;
            blurred += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + offset * 1.3846153846, 0).rgb * 0.3162162162;
            blurred += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv - offset * 1.3846153846, 0).rgb * 0.3162162162;
            blurred += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + offset * 3.2307692308, 0).rgb * 0.0702702703;
            blurred += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv - offset * 3.2307692308, 0).rgb * 0.0702702703;
            return half4(lerp(original.rgb, blurred, strength), original.a);
        }

        half4 BlurHorizontal(Varyings input) : SV_Target
        {
            return FilterEdges(input, float2(1, 0));
        }

        half4 BlurVertical(Varyings input) : SV_Target
        {
            return FilterEdges(input, float2(0, 1));
        }
        ENDHLSL

        Pass
        {
            Name "Horizontal Edge Blur"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment BlurHorizontal
            ENDHLSL
        }

        Pass
        {
            Name "Vertical Edge Blur"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment BlurVertical
            ENDHLSL
        }
    }
    Fallback Off
}
